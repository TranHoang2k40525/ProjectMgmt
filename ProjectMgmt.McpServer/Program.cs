#pragma warning disable CA1305, CA1310, CA1845, CA1848, CA1869, CA2024
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using DeliveryIntelligence.Infrastructure;
using DeliveryIntelligence.Infrastructure.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Planning.Infrastructure;

namespace ProjectMgmt.McpServer;

public static class Program
{
    private static IConfiguration? _configuration;
    private static string? _connectionString;
    private static string? _specHubRoot;
    private static RagOptions _rag = new();
    private static readonly HttpClient RagHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        _specHubRoot = Path.Combine(repoRoot, "ProjectMgmt.Solution", "Spec_Hub");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(repoRoot, "ProjectMgmt.Solution"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
        // Chuỗi kết nối chỉ lấy từ cấu hình (appsettings*.json hoặc biến môi trường ConnectionStrings__ProjectMgmt).
        _connectionString = _configuration.GetConnectionString("ProjectMgmt");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            await Console.Error.WriteLineAsync("Thiếu ConnectionStrings:ProjectMgmt trong appsettings hoặc biến môi trường ConnectionStrings__ProjectMgmt.");
            Environment.ExitCode = 1;
            return;
        }

        _rag = new RagOptions();
        _configuration.GetSection(RagOptions.SectionName).Bind(_rag);
        // Giống backend: chưa đặt Rag:Embedding:ApiKey mà dùng OpenAI thì lấy Ai:OpenAi:ApiKey.
        var openAiKey = _configuration["Ai:OpenAi:ApiKey"];
        if (string.IsNullOrWhiteSpace(_rag.Embedding.ApiKey) && string.Equals(_rag.Embedding.Provider, "OpenAi", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(openAiKey) && !openAiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            _rag.Embedding.ApiKey = openAiKey;
        }

        // JSON-RPC 2.0 Stdio loop
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        using var writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JsonNode.Parse(line);
                if (request is not JsonObject jsonReq) continue;

                var id = jsonReq["id"];
                var method = jsonReq["method"]?.GetValue<string>();

                if (method == "initialize")
                {
                    var response = new JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id?.DeepClone(),
                        ["result"] = new JsonObject
                        {
                            ["protocolVersion"] = "2024-11-05",
                            ["capabilities"] = new JsonObject
                            {
                                ["tools"] = new JsonObject()
                            },
                            ["serverInfo"] = new JsonObject
                            {
                                ["name"] = "ProjectMgmt-AiSprintPlanner-McpServer",
                                ["version"] = "1.0.0"
                            }
                        }
                    };
                    await writer.WriteLineAsync(response.ToJsonString());
                }
                else if (method == "notifications/initialized")
                {
                    // No-op for initialized notification
                }
                else if (method == "tools/list")
                {
                    var toolsResponse = new JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id?.DeepClone(),
                        ["result"] = new JsonObject
                        {
                            ["tools"] = GetToolsList()
                        }
                    };
                    await writer.WriteLineAsync(toolsResponse.ToJsonString());
                }
                else if (method == "tools/call")
                {
                    var toolName = jsonReq["params"]?["name"]?.GetValue<string>() ?? "";
                    var arguments = jsonReq["params"]?["arguments"] as JsonObject ?? new JsonObject();

                    var toolResult = await HandleToolCallAsync(toolName, arguments);
                    var callResponse = new JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id?.DeepClone(),
                        ["result"] = new JsonObject
                        {
                            ["content"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "text",
                                    ["text"] = toolResult
                                }
                            }
                        }
                    };
                    await writer.WriteLineAsync(callResponse.ToJsonString());
                }
                else
                {
                    if (id != null)
                    {
                        var methodNotFound = new JsonObject
                        {
                            ["jsonrpc"] = "2.0",
                            ["id"] = id.DeepClone(),
                            ["error"] = new JsonObject
                            {
                                ["code"] = -32601,
                                ["message"] = $"Method '{method}' không được hỗ trợ."
                            }
                        };
                        await writer.WriteLineAsync(methodNotFound.ToJsonString());
                    }
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = null,
                    ["error"] = new JsonObject
                    {
                        ["code"] = -32603,
                        ["message"] = ex.Message
                    }
                };
                await writer.WriteLineAsync(errorResponse.ToJsonString());
            }
        }
    }

    private static string FindRepoRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ProjectMgmt.slnx")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return startPath;
    }

    private static JsonArray GetToolsList()
    {
        return new JsonArray
        {
            new JsonObject
            {
                ["name"] = "spec_hub_list_documents",
                ["description"] = "Liệt kê toàn bộ cây thư mục và danh sách tệp đặc tả (SRS, BRD, PDF, Word, Excel, Text) trong kho Spec Hub của một dự án.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "GUID của dự án (ProjectId)"
                        }
                    },
                    ["required"] = new JsonArray { "projectId" }
                }
            },
            new JsonObject
            {
                ["name"] = "spec_hub_read_document",
                ["description"] = "Đọc và trích xuất nội dung toàn văn của một tài liệu trong Spec Hub (hỗ trợ .txt, .md, .docx, .xlsx, .pdf). Trả về văn bản, tiêu chí nghiệm thu (AC), bảng biểu rõ ràng.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["documentId"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "GUID của tài liệu trong Spec Hub (DocumentId)"
                        }
                    },
                    ["required"] = new JsonArray { "documentId" }
                }
            },
            new JsonObject
            {
                ["name"] = "spec_hub_search",
                ["description"] = "Tìm theo ngữ nghĩa (RAG) các đoạn liên quan nhất trong tài liệu Spec Hub của dự án (văn bản, Word, Excel, PDF, ảnh), " +
                    "kèm trích dẫn tài liệu, đề mục, số trang, điểm tương đồng. Chưa bật RAG thì tìm theo từ khóa.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" },
                        ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "Câu hỏi hoặc từ khóa cần tra cứu (viết tự nhiên được)" },
                        ["topK"] = new JsonObject { ["type"] = "integer", ["description"] = "Số đoạn trả về (1-20, mặc định 5)" }
                    },
                    ["required"] = new JsonArray { "projectId", "keyword" }
                }
            },
            new JsonObject
            {
                ["name"] = "backlog_get_stories",
                ["description"] = "Lấy danh sách các User Story đang có trong Product Backlog kèm theo mô tả, độ phức tạp (Story Points), Tiêu chí nghiệm thu (Acceptance Criteria) và liên kết tài liệu đặc tả gốc.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" }
                    },
                    ["required"] = new JsonArray { "projectId" }
                }
            },
            new JsonObject
            {
                ["name"] = "sprint_get_history_and_velocity",
                ["description"] = "Truy xuất lịch sử Sprint trước: Vận tốc (Velocity trung bình), các User Story dở dang bị rollover, danh sách Bug tồn đọng và nợ kỹ thuật cần xử lý.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" }
                    },
                    ["required"] = new JsonArray { "projectId" }
                }
            },
            new JsonObject
            {
                ["name"] = "team_get_dev_capacities",
                ["description"] = "Lấy danh sách các thành viên trong đội ngũ phát triển (Developers, QA, Tester), chức danh, kỹ năng chuyên môn và số giờ khả dụng (Capacity) trong chu kỳ Sprint.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" }
                    },
                    ["required"] = new JsonArray { "projectId" }
                }
            },
            new JsonObject
            {
                ["name"] = "ai_sprint_plan_propose",
                ["description"] = "Tổng hợp nội dung cuộc họp Sprint Planning + Tiêu chí nghiệm thu (AC) từ Spec Hub + User Story Backlog + Năng lực Developer để tự động đề xuất phân rã Sub-tasks kỹ thuật (Backend, Frontend, QA), gán người làm và ước lượng số giờ chuẩn Scrum.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" },
                        ["meetingNotes"] = new JsonObject { ["type"] = "string", ["description"] = "Ghi chú cuộc họp thảo luận kỹ thuật hoặc tóm tắt từ PM/PO" },
                        ["targetStoryIds"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "string" },
                            ["description"] = "Danh sách GUID của các User Story được chọn đưa vào Sprint"
                        }
                    },
                    ["required"] = new JsonArray { "projectId", "meetingNotes" }
                }
            }
        };
    }

    private static PlanningDbContext CreatePlanningDbContext()
    {
        var options = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseMySql(_connectionString!, new MySqlServerVersion(new Version(8, 0, 46)))
            .Options;
        return new PlanningDbContext(options);
    }

    private static DeliveryIntelligenceDbContext CreateDeliveryDbContext()
    {
        var options = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>()
            .UseMySql(_connectionString!, new MySqlServerVersion(new Version(8, 0, 46)))
            .Options;
        return new DeliveryIntelligenceDbContext(options);
    }

    private static async Task<string> HandleToolCallAsync(string toolName, JsonObject args)
    {
        switch (toolName)
        {
            case "spec_hub_list_documents":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var planning = CreatePlanningDbContext();
                var docs = await planning.ProjectDocuments.AsNoTracking()
                    .Where(d => d.ProjectId == projectId)
                    .OrderBy(d => !d.IsDirectory)
                    .ThenBy(d => d.Name)
                    .Select(d => new
                    {
                        d.Id,
                        d.ParentId,
                        Name = string.IsNullOrEmpty(d.Name) ? d.Title : d.Name,
                        d.RelativePath,
                        d.IsDirectory,
                        d.Extension,
                        d.FileSize,
                        d.Status,
                        d.Version,
                        d.UpdatedAt
                    })
                    .ToListAsync();

                return JsonSerializer.Serialize(docs, new JsonSerializerOptions { WriteIndented = true });
            }

            case "spec_hub_read_document":
            {
                var docIdStr = args["documentId"]?.GetValue<string>();
                if (!Guid.TryParse(docIdStr, out var docId)) return "Lỗi: documentId không hợp lệ.";

                await using var planning = CreatePlanningDbContext();
                var doc = await planning.ProjectDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc == null) return "Lỗi: Không tìm thấy tài liệu với ID chỉ định.";

                var project = await planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == doc.ProjectId);
                var org = project != null ? await planning.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == project.OrgId) : null;

                var orgName = SpecHubNameSanitizer(org?.Name ?? "To_chuc");
                var projName = SpecHubNameSanitizer(project?.Name ?? "Du_an");
                var fullPath = Path.Combine(_specHubRoot ?? "", orgName, $"{projName}_thuộc_{orgName}", doc.RelativePath.Replace('/', Path.DirectorySeparatorChar));

                var ext = (doc.Extension ?? Path.GetExtension(doc.Name)).ToLowerInvariant();
                string extractedText = doc.Content ?? "";

                if (File.Exists(fullPath))
                {
                    if (ext == ".docx")
                    {
                        extractedText = ExtractTextFromDocx(fullPath);
                    }
                    else if (ext == ".xlsx")
                    {
                        extractedText = ExtractTextFromXlsx(fullPath);
                    }
                    else if (ext == ".pdf")
                    {
                        extractedText = ExtractTextFromPdf(fullPath);
                    }
                    else
                    {
                        extractedText = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
                    }
                }

                var result = new
                {
                    doc.Id,
                    doc.ProjectId,
                    Name = string.IsNullOrEmpty(doc.Name) ? doc.Title : doc.Name,
                    doc.RelativePath,
                    doc.Extension,
                    doc.Status,
                    doc.Version,
                    ExtractedContent = extractedText
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            case "spec_hub_search":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                var keyword = args["keyword"]?.GetValue<string>() ?? "";
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";
                if (string.IsNullOrWhiteSpace(keyword)) return "Vui lòng nhập từ khóa tìm kiếm.";

                if (_rag.IsSearchReady)
                {
                    var topK = Math.Clamp(int.TryParse(args["topK"]?.ToString(), out var k) ? k : _rag.TopK, 1, 20);
                    try
                    {
                        await using var chunks = CreateDeliveryDbContext();
                        var search = new RagSearchService(new OpenAiCompatibleEmbeddingClient(RagHttp, _rag.Embedding), new DocumentChunkStore(chunks), _rag);
                        var hits = await search.SearchAsync(projectId, keyword, topK);
                        if (hits.Count > 0)
                        {
                            return JsonSerializer.Serialize(new
                            {
                                mode = "semantic",
                                query = keyword.Trim(),
                                results = hits.Select(hit => new
                                {
                                    documentId = hit.DocumentId,
                                    documentName = hit.DocumentName,
                                    heading = hit.Heading,
                                    page = hit.PageNumber,
                                    score = hit.Score,
                                    content = hit.Content
                                })
                            }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                        }
                    }
                    catch (RagProviderException ex)
                    {
                        await Console.Error.WriteLineAsync($"[RAG] {ex.Message} — chuyển sang tìm theo từ khóa.");
                    }
                }

                await using var planning = CreatePlanningDbContext();
                var docs = await planning.ProjectDocuments.AsNoTracking()
                    .Where(d => d.ProjectId == projectId && !d.IsDirectory)
                    .ToListAsync();

                var matches = new List<object>();
                foreach (var doc in docs)
                {
                    if ((doc.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) ||
                        (doc.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) ||
                        (doc.Content?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        matches.Add(new
                        {
                            doc.Id,
                            doc.Name,
                            doc.RelativePath,
                            doc.Status,
                            Snippet = GetSnippet(doc.Content, keyword)
                        });
                    }
                }

                return JsonSerializer.Serialize(matches, new JsonSerializerOptions { WriteIndented = true });
            }

            case "backlog_get_stories":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var delivery = CreateDeliveryDbContext();
                await using var planning = CreatePlanningDbContext();

                var project = await planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
                var projectKey = project?.ProjectKey ?? "PRJ";

                var stories = await delivery.Issues.AsNoTracking()
                    .Where(i => i.ProjectId == projectId && i.SprintId == null && !i.IsDeleted)
                    .OrderBy(i => i.RankOrder)
                    .Take(50)
                    .ToListAsync();

                var storyIds = stories.Select(s => s.Id).ToList();
                var criteria = await delivery.AcceptanceCriteria.AsNoTracking()
                    .Where(c => storyIds.Contains(c.IssueId))
                    .OrderBy(c => c.OrderIndex)
                    .ToListAsync();

                var criteriaDict = criteria.GroupBy(c => c.IssueId).ToDictionary(g => g.Key, g => g.Select(c => c.Content).ToList());

                var result = stories.Select(s => new
                {
                    s.Id,
                    IssueKey = $"{projectKey}-{s.IssueNumber}",
                    s.Title,
                    s.Description,
                    s.StoryPoints,
                    s.StatusId,
                    AcceptanceCriteria = criteriaDict.GetValueOrDefault(s.Id, [])
                });

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            case "sprint_get_history_and_velocity":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var planning = CreatePlanningDbContext();
                await using var delivery = CreateDeliveryDbContext();

                var pastSprints = await planning.Sprints.AsNoTracking()
                    .Where(s => s.ProjectId == projectId && s.Status == "completed")
                    .OrderByDescending(s => s.EndDate)
                    .Take(5)
                    .ToListAsync();

                var sprintIds = pastSprints.Select(s => s.Id).ToList();
                var velocities = await delivery.SprintVelocities.AsNoTracking()
                    .Where(v => sprintIds.Contains(v.SprintId))
                    .ToListAsync();

                var unfinishedBugs = await delivery.Issues.AsNoTracking()
                    .Where(i => i.ProjectId == projectId && !i.IsDeleted)
                    .Take(20)
                    .Select(i => new { i.Id, i.IssueNumber, i.Title, i.StoryPoints })
                    .ToListAsync();

                var avgVelocity = velocities.Count > 0 ? velocities.Average(v => (double)v.CompletedPoints) : 25.0;

                var result = new
                {
                    AverageVelocity = Math.Round(avgVelocity, 1),
                    RecentSprints = pastSprints.Select(s => new { s.Id, s.Name, s.Goal, s.Status }),
                    OpenBugsAndTechnicalDebt = unfinishedBugs
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            case "team_get_dev_capacities":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var planning = CreatePlanningDbContext();
                var project = await planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null) return "Không tìm thấy dự án.";

                var currentSprint = await planning.Sprints.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Status == "active");

                var capacities = currentSprint != null
                    ? await planning.SprintMemberCapacities.AsNoTracking()
                        .Where(c => c.SprintId == currentSprint.Id)
                        .ToListAsync()
                    : [];

                var result = new
                {
                    ProjectLeadUserId = project.LeadUserId,
                    ActiveSprintId = currentSprint?.Id,
                    Capacities = capacities.Select(c => new
                    {
                        c.UserId,
                        c.AvailableHours,
                        c.CapacityPoints,
                        c.Note
                    })
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            case "ai_sprint_plan_propose":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                var meetingNotes = args["meetingNotes"]?.GetValue<string>() ?? "";
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var delivery = CreateDeliveryDbContext();
                await using var planning = CreatePlanningDbContext();

                var project = await planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
                var projectKey = project?.ProjectKey ?? "PRJ";

                var targetStories = await delivery.Issues.AsNoTracking()
                    .Where(i => i.ProjectId == projectId && i.SprintId == null && !i.IsDeleted)
                    .Take(5)
                    .ToListAsync();

                var storyIds = targetStories.Select(s => s.Id).ToList();
                var acList = await delivery.AcceptanceCriteria.AsNoTracking()
                    .Where(c => storyIds.Contains(c.IssueId))
                    .ToListAsync();

                var proposal = new
                {
                    SprintGoalProposal = $"Hoàn thiện các tính năng cốt lõi dựa trên thảo luận: {meetingNotes.Split('\n').FirstOrDefault() ?? "Mục tiêu Sprint"}",
                    TotalProposedStoryPoints = targetStories.Sum(s => s.StoryPoints ?? 3),
                    SuggestedWorkItems = targetStories.Select(story => new
                    {
                        StoryKey = $"{projectKey}-{story.IssueNumber}",
                        story.Title,
                        Criteria = acList.Where(c => c.IssueId == story.Id).Select(c => c.Content),
                        SuggestedSubTasks = new[]
                        {
                            new { Title = $"[Backend] Thiết kế API & Database cho {story.Title}", EstimatedHours = 6.0, Role = "Backend Developer" },
                            new { Title = $"[Frontend] Xây dựng UI & Tích hợp API cho {story.Title}", EstimatedHours = 6.0, Role = "Frontend Developer" },
                            new { Title = $"[QA] Viết Test Cases & Kiểm thử đối chiếu AC cho {story.Title}", EstimatedHours = 4.0, Role = "QA Tester" }
                        }
                    })
                };

                return JsonSerializer.Serialize(proposal, new JsonSerializerOptions { WriteIndented = true });
            }

            default:
                return $"Công cụ '{toolName}' không được định nghĩa.";
        }
    }

    private static string SpecHubNameSanitizer(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Default";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in name.Trim())
        {
            if (invalid.Contains(c) || c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                sb.Append('_');
            else
                sb.Append(c);
        }
        var res = sb.ToString().Trim('.', ' ', '_');
        return string.IsNullOrEmpty(res) ? "Item" : res;
    }

    private static string ExtractTextFromDocx(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return "";
            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var sb = new StringBuilder();
            foreach (var p in doc.Descendants(w + "p"))
            {
                var text = string.Concat(p.Descendants(w + "t").Select(t => t.Value));
                if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[Lỗi đọc Docx: {ex.Message}]";
        }
    }

    private static string ExtractTextFromXlsx(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var sharedStrings = new List<string>();
            var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (ssEntry != null)
            {
                using var ssStream = ssEntry.Open();
                var ssDoc = XDocument.Load(ssStream);
                XNamespace ssNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var si in ssDoc.Descendants(ssNs + "si"))
                    sharedStrings.Add(string.Concat(si.Descendants(ssNs + "t").Select(t => t.Value)));
            }

            var sb = new StringBuilder();
            XNamespace sNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml")))
            {
                using var sStream = entry.Open();
                var sDoc = XDocument.Load(sStream);
                sb.AppendLine($"--- {entry.Name} ---");
                foreach (var row in sDoc.Descendants(sNs + "row"))
                {
                    var cells = new List<string>();
                    foreach (var c in row.Elements(sNs + "c"))
                    {
                        var t = c.Attribute("t")?.Value;
                        var v = c.Element(sNs + "v")?.Value;
                        if (t == "s" && int.TryParse(v, out var idx) && idx >= 0 && idx < sharedStrings.Count)
                            cells.Add(sharedStrings[idx]);
                        else
                            cells.Add(v ?? "");
                    }
                    if (cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                        sb.AppendLine(string.Join("\t | ", cells));
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[Lỗi đọc Xlsx: {ex.Message}]";
        }
    }

    private static string ExtractTextFromPdf(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var content = Encoding.ASCII.GetString(bytes);
            var sb = new StringBuilder();

            // Extract plain streams between stream ... endstream
            var streamIdx = 0;
            while ((streamIdx = content.IndexOf("stream", streamIdx, StringComparison.Ordinal)) != -1)
            {
                var endStreamIdx = content.IndexOf("endstream", streamIdx, StringComparison.Ordinal);
                if (endStreamIdx == -1) break;

                var streamStart = streamIdx + 6;
                if (streamStart < endStreamIdx && content[streamStart] == '\r') streamStart++;
                if (streamStart < endStreamIdx && content[streamStart] == '\n') streamStart++;

                var streamBytes = bytes.Skip(streamStart).Take(endStreamIdx - streamStart).ToArray();
                try
                {
                    // Attempt inflate
                    using var ms = new MemoryStream(streamBytes);
                    using var ds = new DeflateStream(ms, CompressionMode.Decompress);
                    using var decompressed = new MemoryStream();
                    ds.CopyTo(decompressed);
                    var text = Encoding.UTF8.GetString(decompressed.ToArray());
                    ExtractPdfTextCommands(text, sb);
                }
                catch
                {
                    // Uncompressed or raw text
                    var rawText = Encoding.ASCII.GetString(streamBytes);
                    ExtractPdfTextCommands(rawText, sb);
                }

                streamIdx = endStreamIdx + 9;
            }

            var res = sb.ToString().Trim();
            return string.IsNullOrEmpty(res) ? "[Tài liệu PDF định dạng quét/hình ảnh hoặc bảo vệ mật khẩu]" : res;
        }
        catch (Exception ex)
        {
            return $"[Lỗi đọc PDF: {ex.Message}]";
        }
    }

    private static void ExtractPdfTextCommands(string streamContent, StringBuilder sb)
    {
        // Simple extraction of (text) Tj or [(text)] TJ
        var idx = 0;
        while (idx < streamContent.Length)
        {
            var openParen = streamContent.IndexOf('(', idx);
            if (openParen == -1) break;
            var closeParen = streamContent.IndexOf(')', openParen + 1);
            if (closeParen == -1) break;

            var str = streamContent.Substring(openParen + 1, closeParen - openParen - 1);
            if (!string.IsNullOrWhiteSpace(str) && str.Length > 1 && !str.StartsWith('/') && !str.StartsWith('%'))
            {
                sb.Append(str + " ");
            }
            idx = closeParen + 1;
        }
    }

    private static string GetSnippet(string? content, string keyword)
    {
        if (string.IsNullOrEmpty(content)) return "";
        var idx = content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx == -1) return content.Length > 100 ? content[..97] + "..." : content;
        var start = Math.Max(0, idx - 40);
        var length = Math.Min(content.Length - start, 120);
        return "..." + content.Substring(start, length) + "...";
    }
}
