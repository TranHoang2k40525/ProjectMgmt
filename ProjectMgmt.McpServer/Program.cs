#pragma warning disable CA1305, CA1310, CA1845, CA1848, CA1869, CA2024
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Planning.Infrastructure;
using ProjectMgmt.Core.Security;

namespace ProjectMgmt.McpServer;

internal sealed record McpScopeAuthContext(
    Guid? UserId,
    Guid? OrgId,
    IReadOnlySet<Guid> AllowedProjectIds,
    string? ProvidedApiToken,
    string? ExpectedApiToken)
{
    public static McpScopeAuthContext FromEnvironment(IConfiguration? configuration)
    {
        var userIdStr = Environment.GetEnvironmentVariable("PROJECTMGMT_MCP_USER_ID");
        var orgIdStr = Environment.GetEnvironmentVariable("PROJECTMGMT_MCP_ORG_ID");
        var allowedRaw = Environment.GetEnvironmentVariable("PROJECTMGMT_MCP_ALLOWED_PROJECTS");
        var providedToken = Environment.GetEnvironmentVariable("PROJECTMGMT_API_TOKEN");
        var expectedToken = configuration?["Ai:McpServer:ApiToken"]
            ?? Environment.GetEnvironmentVariable("PROJECTMGMT_MCP_SERVER_API_TOKEN");

        Guid? userId = Guid.TryParse(userIdStr?.Trim(), out var u) && u != Guid.Empty ? u : null;
        Guid? orgId = Guid.TryParse(orgIdStr?.Trim(), out var o) && o != Guid.Empty ? o : null;

        var allowed = new HashSet<Guid>();
        if (!string.IsNullOrWhiteSpace(allowedRaw))
        {
            foreach (var token in allowedRaw.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(token, out var pid) && pid != Guid.Empty)
                {
                    allowed.Add(pid);
                }
            }
        }

        return new McpScopeAuthContext(
            userId,
            orgId,
            allowed,
            string.IsNullOrWhiteSpace(providedToken) ? null : providedToken.Trim(),
            string.IsNullOrWhiteSpace(expectedToken) ? null : expectedToken.Trim());
    }
}

public static class Program
{
    private const long MaxSingleEntryUncompressedBytes = 25L * 1024 * 1024;
    private const long MaxTotalUncompressedBytes = 50L * 1024 * 1024;
    private const int MaxZipEntries = 1000;
    private const int MaxExtractedTextLength = 500_000;

    private static IConfiguration? _configuration;
    private static string? _connectionString;
    private static string? _specHubRoot;
    private static McpScopeAuthContext? _authContext;

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
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
        _connectionString = _configuration.GetConnectionString("ProjectMgmt")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__ProjectMgmt")
            ?? "Server=127.0.0.1;Port=3306;Database=projectmgmt;User=root;Password=;SslMode=None;AllowPublicKeyRetrieval=True;";
        _authContext = McpScopeAuthContext.FromEnvironment(_configuration);

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

                var response = await ProcessJsonRpcRequestAsync(jsonReq, _authContext);
                if (response != null)
                {
                    await writer.WriteLineAsync(response.ToJsonString());
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

    internal static async Task<JsonObject?> ProcessJsonRpcRequestAsync(
        JsonObject jsonReq,
        McpScopeAuthContext? authContext = null,
        Func<PlanningDbContext>? planningFactory = null,
        Func<DeliveryIntelligenceDbContext>? deliveryFactory = null,
        Func<IdentityExperienceDbContext>? identityFactory = null)
    {
        var id = jsonReq["id"];
        var method = jsonReq["method"]?.GetValue<string>();

        if (method == "initialize")
        {
            return new JsonObject
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
        }

        if (method == "notifications/initialized")
        {
            return null;
        }

        if (method == "tools/list")
        {
            return new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = new JsonObject
                {
                    ["tools"] = GetToolsList()
                }
            };
        }

        if (method == "tools/call")
        {
            var toolName = jsonReq["params"]?["name"]?.GetValue<string>() ?? "";
            var arguments = jsonReq["params"]?["arguments"] as JsonObject ?? new JsonObject();

            var effectiveAuth = authContext ?? _authContext ?? McpScopeAuthContext.FromEnvironment(_configuration);
            var createPlanning = planningFactory ?? CreatePlanningDbContext;
            var createDelivery = deliveryFactory ?? CreateDeliveryDbContext;
            var createIdentity = identityFactory ?? CreateIdentityDbContext;

            await using var planningForAuth = createPlanning();
            await using var identityForAuth = createIdentity();
            var (authorized, authError) = await AuthorizeToolCallAsync(arguments, effectiveAuth, planningForAuth, identityForAuth);
            if (!authorized)
            {
                return new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id?.DeepClone(),
                    ["error"] = new JsonObject
                    {
                        ["code"] = -32001,
                        ["message"] = authError ?? "Unauthorized project scope access"
                    }
                };
            }

            var toolResult = await HandleToolCallAsync(toolName, arguments, createPlanning, createDelivery);
            return new JsonObject
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
        }

        if (id != null)
        {
            return new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id.DeepClone(),
                ["error"] = new JsonObject
                {
                    ["code"] = -32601,
                    ["message"] = $"Method '{method}' không được hỗ trợ."
                }
            };
        }

        return null;
    }

    internal static async Task<(bool Authorized, string? ErrorMessage)> AuthorizeToolCallAsync(
        JsonObject args,
        McpScopeAuthContext auth,
        PlanningDbContext planning,
        IdentityExperienceDbContext? identity = null)
    {
        if (!string.IsNullOrWhiteSpace(auth.ExpectedApiToken))
        {
            var expectedBytes = Encoding.UTF8.GetBytes(auth.ExpectedApiToken);
            var providedBytes = Encoding.UTF8.GetBytes(auth.ProvidedApiToken ?? string.Empty);
            if (providedBytes.Length != expectedBytes.Length ||
                !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                return (false, "Unauthorized project scope access: PROJECTMGMT_API_TOKEN không hợp lệ hoặc bị thiếu.");
            }
        }

        var hasProjectArg = args.ContainsKey("projectId");
        var hasDocumentArg = args.ContainsKey("documentId");
        if (!hasProjectArg && !hasDocumentArg)
        {
            return (true, null);
        }

        // Fail-closed: không cho phép truy vấn bất kỳ projectId/documentId nào nếu thiếu hoàn toàn danh tính phạm vi
        if (!auth.UserId.HasValue && !auth.OrgId.HasValue && auth.AllowedProjectIds.Count == 0)
        {
            return (false, "Unauthorized project scope access: Chưa cấu hình danh tính phạm vi (PROJECTMGMT_MCP_USER_ID, PROJECTMGMT_MCP_ORG_ID hoặc PROJECTMGMT_MCP_ALLOWED_PROJECTS).");
        }

        Guid? targetProjectId = null;
        if (hasProjectArg)
        {
            var projectIdStr = args["projectId"]?.GetValue<string>();
            if (!Guid.TryParse(projectIdStr, out var parsedProjectId) || parsedProjectId == Guid.Empty)
            {
                return (false, "Unauthorized project scope access: projectId không hợp lệ.");
            }
            targetProjectId = parsedProjectId;
        }

        if (hasDocumentArg)
        {
            var docIdStr = args["documentId"]?.GetValue<string>();
            if (!Guid.TryParse(docIdStr, out var parsedDocId) || parsedDocId == Guid.Empty)
            {
                return (false, "Unauthorized project scope access: documentId không hợp lệ.");
            }

            var doc = await planning.ProjectDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == parsedDocId);
            if (doc == null)
            {
                return (false, "Unauthorized project scope access: Không tìm thấy tài liệu trong phạm vi được phép.");
            }

            if (targetProjectId.HasValue && targetProjectId.Value != doc.ProjectId)
            {
                return (false, "Unauthorized project scope access: documentId không thuộc projectId được yêu cầu.");
            }

            targetProjectId = doc.ProjectId;
        }

        if (!targetProjectId.HasValue)
        {
            return (false, "Unauthorized project scope access: Không xác định được phạm vi dự án.");
        }

        if (auth.AllowedProjectIds.Count > 0 && !auth.AllowedProjectIds.Contains(targetProjectId.Value))
        {
            return (false, "Unauthorized project scope access: Dự án không nằm trong danh sách PROJECTMGMT_MCP_ALLOWED_PROJECTS được cấp phép.");
        }

        var project = await planning.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == targetProjectId.Value && !p.IsDeleted);
        if (project == null)
        {
            return (false, "Unauthorized project scope access: Dự án không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        if (auth.OrgId.HasValue && project.OrgId != auth.OrgId.Value)
        {
            return (false, "Unauthorized project scope access: Dự án không thuộc phạm vi tổ chức PROJECTMGMT_MCP_ORG_ID.");
        }

        if (auth.UserId.HasValue && project.LeadUserId != auth.UserId.Value)
        {
            if (identity == null)
            {
                return (false, "Unauthorized project scope access: Thiếu ngữ cảnh kiểm tra vai trò người dùng.");
            }

            var isDeactivated = await identity.Users.AsNoTracking()
                .AnyAsync(u => u.Id == auth.UserId.Value && !u.IsActive);
            if (isDeactivated)
            {
                return (false, "Unauthorized project scope access: Tài khoản người dùng đã bị khóa.");
            }

            var userRoles = await identity.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == auth.UserId.Value)
                .ToListAsync();

            var hasScopedAccess = userRoles.Any(ur =>
                (string.Equals(ur.ScopeType, "Project", StringComparison.OrdinalIgnoreCase) && ur.ScopeId == project.Id) ||
                ((string.Equals(ur.ScopeType, "Organization", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(ur.ScopeType, "Org", StringComparison.OrdinalIgnoreCase)) && ur.ScopeId == project.OrgId) ||
                string.Equals(ur.ScopeType, "System", StringComparison.OrdinalIgnoreCase));

            if (!hasScopedAccess)
            {
                return (false, "Unauthorized project scope access: Người dùng không có quyền truy cập dự án hoặc tổ chức này.");
            }
        }

        return (true, null);
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
                ["description"] = "Tìm kiếm từ khóa, thuật ngữ kỹ thuật hoặc quy tắc nghiệp vụ xuyên suốt tất cả các tài liệu đặc tả trong Spec Hub của dự án.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" },
                        ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "Từ khóa cần tra cứu" }
                    },
                    ["required"] = new JsonArray { "projectId", "keyword" }
                }
            },
            new JsonObject
            {
                ["name"] = "backlog_get_stories",
                ["description"] = "Lấy danh sách toàn bộ hạng mục trong Product Backlog (User Story, Task, Bug) kèm loại công việc, mức ưu tiên, nhãn, Story Points và Tiêu chí nghiệm thu (AC).",
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
                ["name"] = "project_get_active_config",
                ["description"] = "Truy xuất cấu hình đang được ÁP DỤNG THỰC TẾ của dự án: Quy trình Workflow (Board, trạng thái, cờ khởi tạo/hoàn thành, luồng chuyển tiếp), Mức độ ưu tiên đang dùng, Loại công việc (Issue Types) và danh sách Nhãn (Labels).",
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

    private static IdentityExperienceDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityExperienceDbContext>()
            .UseMySql(_connectionString!, new MySqlServerVersion(new Version(8, 0, 46)))
            .Options;
        return new IdentityExperienceDbContext(options);
    }

    private static async Task<string> HandleToolCallAsync(
        string toolName,
        JsonObject args,
        Func<PlanningDbContext>? planningFactory = null,
        Func<DeliveryIntelligenceDbContext>? deliveryFactory = null)
    {
        PlanningDbContext OpenPlanningDb() => (planningFactory ?? CreatePlanningDbContext)();
        DeliveryIntelligenceDbContext OpenDeliveryDb() => (deliveryFactory ?? CreateDeliveryDbContext)();

        switch (toolName)
        {
            case "spec_hub_list_documents":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var planning = OpenPlanningDb();
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

                await using var planning = OpenPlanningDb();
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

                var safeContent = InputGuardrail.SanitizeDocumentContent(extractedText).SanitizedText;
                if (safeContent.Length > MaxExtractedTextLength)
                {
                    safeContent = safeContent[..MaxExtractedTextLength] + "\n...[Đã cắt gọn do vượt giới hạn kích thước văn bản an toàn]";
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
                    ExtractedContent = safeContent
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            case "spec_hub_search":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                var keyword = args["keyword"]?.GetValue<string>() ?? "";
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";
                if (string.IsNullOrWhiteSpace(keyword)) return "Vui lòng nhập từ khóa tìm kiếm.";

                await using var planning = OpenPlanningDb();
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
                            Snippet = InputGuardrail.SanitizeDocumentContent(GetSnippet(doc.Content, keyword)).SanitizedText
                        });
                    }
                }

                return JsonSerializer.Serialize(matches, new JsonSerializerOptions { WriteIndented = true });
            }

            case "backlog_get_stories":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var delivery = OpenDeliveryDb();
                await using var planning = OpenPlanningDb();

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
                    s.EstimatedHours,
                    s.StatusId,
                    AcceptanceCriteria = criteriaDict.GetValueOrDefault(s.Id, [])
                });

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            case "project_get_active_config":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var planning = OpenPlanningDb();
                await using var delivery = OpenDeliveryDb();

                var customPriorities = await planning.Priorities.AsNoTracking()
                    .Where(p => p.ProjectId == projectId)
                    .OrderBy(p => p.Level)
                    .ToListAsync();
                var priorities = customPriorities.Count > 0
                    ? customPriorities
                    : await planning.Priorities.AsNoTracking().Where(p => p.ProjectId == null).OrderBy(p => p.Level).ToListAsync();

                var statuses = await planning.WorkflowStatuses.AsNoTracking()
                    .Where(s => s.ProjectId == projectId)
                    .OrderBy(s => s.OrderIndex)
                    .ToListAsync();
                var transitions = await planning.WorkflowTransitions.AsNoTracking()
                    .Where(t => t.ProjectId == projectId)
                    .ToListAsync();
                var board = await planning.Boards.AsNoTracking()
                    .Where(b => b.ProjectId == projectId)
                    .OrderByDescending(b => b.IsDefault)
                    .FirstOrDefaultAsync();
                var issueTypes = await planning.IssueTypes.AsNoTracking()
                    .Where(t => t.ProjectId == projectId)
                    .OrderBy(t => t.OrderIndex)
                    .ToListAsync();
                var labels = await delivery.Labels.AsNoTracking()
                    .Where(l => l.ProjectId == projectId)
                    .OrderBy(l => l.Name)
                    .ToListAsync();

                var statusMap = statuses.ToDictionary(s => s.Id, s => s.Name);
                var result = new
                {
                    WorkflowName = board?.Name,
                    WorkflowType = board?.Type ?? "Scrum",
                    InitialStatus = statuses.FirstOrDefault(s => s.IsInitial)?.Name ?? statuses.FirstOrDefault()?.Name ?? "To Do",
                    Statuses = statuses.Select(s => new { s.Id, s.Name, s.Category, s.OrderIndex, s.IsInitial, s.IsActiveWork, s.IsDone, s.IsCanceled }),
                    Transitions = transitions.Where(t => statusMap.ContainsKey(t.FromStatusId) && statusMap.ContainsKey(t.ToStatusId))
                        .Select(t => new { From = statusMap[t.FromStatusId], To = statusMap[t.ToStatusId], t.Name }),
                    IsCustomPriorities = customPriorities.Count > 0,
                    Priorities = priorities.Select(p => new { p.Id, p.Name, p.Level, p.ColorHex }),
                    IssueTypes = issueTypes.Select(t => new { t.Id, t.Name, t.IsSubtask, t.OrderIndex }),
                    Labels = labels.Select(l => new { l.Id, l.Name, l.ColorHex })
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            case "sprint_get_history_and_velocity":
            {
                var projectIdStr = args["projectId"]?.GetValue<string>();
                if (!Guid.TryParse(projectIdStr, out var projectId)) return "Lỗi: projectId không hợp lệ.";

                await using var planning = OpenPlanningDb();
                await using var delivery = OpenDeliveryDb();

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

                await using var planning = OpenPlanningDb();
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

                await using var delivery = OpenDeliveryDb();
                await using var planning = OpenPlanningDb();

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

    private static XDocument LoadSafeXmlEntry(ZipArchiveEntry entry, ref long totalUncompressedBytes)
    {
        if (entry.Length > MaxSingleEntryUncompressedBytes)
        {
            throw new InvalidOperationException(
                $"Entry '{entry.FullName}' có kích thước giải nén vượt ngưỡng an toàn {MaxSingleEntryUncompressedBytes / (1024 * 1024)}MB.");
        }

        using var rawStream = entry.Open();
        using var bounded = new MemoryStream();
        var buffer = new byte[81920];
        long entryBytes = 0;
        int read;
        while ((read = rawStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            entryBytes += read;
            if (entryBytes > MaxSingleEntryUncompressedBytes)
            {
                throw new InvalidOperationException(
                    $"Entry '{entry.FullName}' vượt dung lượng giải nén tối đa ({MaxSingleEntryUncompressedBytes / (1024 * 1024)}MB).");
            }
            if (totalUncompressedBytes + read > MaxTotalUncompressedBytes)
            {
                throw new InvalidOperationException(
                    $"Tổng dung lượng giải nén của gói tài liệu vượt ngưỡng an toàn ({MaxTotalUncompressedBytes / (1024 * 1024)}MB).");
            }
            totalUncompressedBytes += read;
            bounded.Write(buffer, 0, read);
        }

        bounded.Position = 0;
        var xmlSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var xmlReader = XmlReader.Create(bounded, xmlSettings);
        return XDocument.Load(xmlReader);
    }

    private static string ExtractTextFromDocx(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            if (archive.Entries.Count > MaxZipEntries)
            {
                return $"[Cảnh báo bảo mật Docx: Số lượng tệp nén ({archive.Entries.Count}) vượt ngưỡng tối đa {MaxZipEntries}.]";
            }

            long totalUncompressed = 0;
            var hasImages = archive.Entries.Any(e =>
                e.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase) && e.Length > 0);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return "";
            var doc = LoadSafeXmlEntry(entry, ref totalUncompressed);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var sb = new StringBuilder();
            foreach (var p in doc.Descendants(w + "p"))
            {
                var text = string.Concat(p.Descendants(w + "t").Select(t => t.Value));
                if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                if (sb.Length >= MaxExtractedTextLength) break;
            }
            if (hasImages)
            {
                sb.Insert(0, "[CẢNH BÁO TRÍCH XUẤT HÌNH ẢNH/SCAN]: Tài liệu Word chứa hình ảnh hoặc sơ đồ nhúng không thể đọc hết bằng văn bản thuần.\n");
            }
            return sb.Length > MaxExtractedTextLength ? sb.ToString(0, MaxExtractedTextLength) : sb.ToString();
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
            if (archive.Entries.Count > MaxZipEntries)
            {
                return $"[Cảnh báo bảo mật Xlsx: Số lượng tệp nén ({archive.Entries.Count}) vượt ngưỡng tối đa {MaxZipEntries}.]";
            }

            long totalUncompressed = 0;
            var sharedStrings = new List<string>();
            var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (ssEntry != null)
            {
                var ssDoc = LoadSafeXmlEntry(ssEntry, ref totalUncompressed);
                XNamespace ssNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var si in ssDoc.Descendants(ssNs + "si"))
                    sharedStrings.Add(string.Concat(si.Descendants(ssNs + "t").Select(t => t.Value)));
            }

            var sb = new StringBuilder();
            XNamespace sNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml")))
            {
                var sDoc = LoadSafeXmlEntry(entry, ref totalUncompressed);
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
                    if (sb.Length >= MaxExtractedTextLength) break;
                }
                if (sb.Length >= MaxExtractedTextLength) break;
            }
            return sb.Length > MaxExtractedTextLength ? sb.ToString(0, MaxExtractedTextLength) : sb.ToString();
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
            if (bytes.Length == 0)
            {
                return "[Tài liệu PDF định dạng quét/hình ảnh hoặc bảo vệ mật khẩu]";
            }

            var content = Encoding.ASCII.GetString(bytes);
            var hasEmbeddedImages = content.Contains("/Subtype /Image", StringComparison.Ordinal)
                || content.Contains("/Subtype/Image", StringComparison.Ordinal);
            var sb = new StringBuilder();

            // Extract plain streams between stream ... endstream (bounded to prevent zip-bomb/OOM)
            var streamIdx = 0;
            long totalDecompressed = 0;
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
                    using var ms = new MemoryStream(streamBytes);
                    using var ds = new DeflateStream(ms, CompressionMode.Decompress);
                    using var decompressed = new MemoryStream();
                    var buffer = new byte[65536];
                    int read;
                    while ((read = ds.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalDecompressed += read;
                        if (decompressed.Length + read > MaxSingleEntryUncompressedBytes ||
                            totalDecompressed > MaxTotalUncompressedBytes)
                        {
                            throw new InvalidOperationException("Luồng PDF giải nén vượt ngưỡng bộ nhớ an toàn.");
                        }
                        decompressed.Write(buffer, 0, read);
                    }
                    var text = Encoding.UTF8.GetString(decompressed.ToArray());
                    ExtractPdfTextCommands(text, sb);
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch
                {
                    var rawText = Encoding.ASCII.GetString(streamBytes);
                    ExtractPdfTextCommands(rawText, sb);
                }

                if (sb.Length >= MaxExtractedTextLength) break;
                streamIdx = endStreamIdx + 9;
            }

            var res = sb.ToString().Trim();
            if (string.IsNullOrEmpty(res))
            {
                return "[Tài liệu PDF định dạng quét/hình ảnh hoặc bảo vệ mật khẩu]";
            }

            var alnumCount = res.Count(char.IsLetterOrDigit);
            if ((bytes.Length >= 2048 && alnumCount < 50) || hasEmbeddedImages)
            {
                return $"[CẢNH BÁO TRÍCH XUẤT HÌNH ẢNH/SCAN]: Tài liệu PDF có ít văn bản khả đọc hoặc chứa hình ảnh/scan.\n{res}";
            }

            return res.Length > MaxExtractedTextLength ? res[..MaxExtractedTextLength] : res;
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
