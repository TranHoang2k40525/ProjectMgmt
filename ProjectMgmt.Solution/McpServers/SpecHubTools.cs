namespace ProjectMgmt.Solution.McpServers;

using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure;
using Planning.Infrastructure.Services;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.VectorSearch;

public class SpecHubTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions JsonIndentedOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private static string SerializeJson<T>(T value, bool indented = false) =>
        JsonSerializer.Serialize(value, indented ? JsonIndentedOptions : JsonOptions);

    private readonly PlanningDbContext _planning;
    private readonly ISpecHubStorageService _specStorage;
    private readonly ILogger<SpecHubTools> _logger;
    private readonly IVectorSearchService? _vectorSearch;

    public SpecHubTools(
        PlanningDbContext planning,
        ISpecHubStorageService specStorage,
        ILogger<SpecHubTools> logger,
        IVectorSearchService? vectorSearch = null)
    {
        _planning = planning;
        _specStorage = specStorage;
        _logger = logger;
        _vectorSearch = vectorSearch;
    }

    public IReadOnlyList<AiToolDefinition> GetToolDefinitions()
    {
        return new List<AiToolDefinition>
        {
            new(
                "spec_hub_list_documents",
                "Liệt kê toàn bộ cây thư mục và danh sách tệp đặc tả (SRS, BRD, PDF, Word, Excel, Text) trong kho Spec Hub của một dự án.",
                new JsonObject
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
            ),
            new(
                "spec_hub_read_document",
                "Đọc và trích xuất nội dung toàn văn của một tài liệu trong Spec Hub (hỗ trợ .txt, .md, .docx, .xlsx, .pdf). Trả về văn bản, tiêu chí nghiệm thu (AC), bảng biểu rõ ràng.",
                new JsonObject
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
            ),
            new(
                "spec_hub_search",
                "Tìm theo ngữ nghĩa (RAG) các đoạn liên quan nhất trong toàn bộ tài liệu Spec Hub của dự án (văn bản, Word, Excel, PDF, ảnh). " +
                "Trả về nội dung đoạn kèm trích dẫn: tài liệu, đề mục, số trang, điểm tương đồng. Chưa bật RAG thì tìm theo từ khóa.",
                new JsonObject
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
            )
        };
    }

    public async Task<string> ListDocumentsAsync(Guid projectId, IProjectScopeContext scope, CancellationToken ct = default)
    {
        if (projectId == Guid.Empty)
        {
            return SerializeJson(new { error = "INVALID_PROJECT_ID", message = "Lỗi: projectId không hợp lệ." });
        }

        ProjectScopeGuard.ValidateProject(scope, projectId);

        var project = await _planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return SerializeJson(new { error = "PROJECT_NOT_FOUND", message = "Lỗi: Không tìm thấy dự án với ID chỉ định." });
        }

        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        var docs = await _planning.ProjectDocuments.AsNoTracking()
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
            .ToListAsync(ct);

        return SerializeJson(docs, indented: true);
    }

    public async Task<string> ReadDocumentAsync(Guid documentId, IProjectScopeContext scope, CancellationToken ct = default)
    {
        if (documentId == Guid.Empty)
        {
            return SerializeJson(new { error = "INVALID_DOCUMENT_ID", message = "Lỗi: documentId không hợp lệ." });
        }

        ProjectScopeGuard.EnsureValidScope(scope);

        var doc = await _planning.ProjectDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ProjectId == scope.ProjectId, ct);
        if (doc == null)
        {
            return SerializeJson(new { error = "DOCUMENT_NOT_FOUND", message = "Lỗi: Không tìm thấy tài liệu với ID chỉ định." });
        }

        var project = await _planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == doc.ProjectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return SerializeJson(new { error = "PROJECT_NOT_FOUND", message = "Lỗi: Không tìm thấy dự án chứa tài liệu này." });
        }

        ProjectScopeGuard.Validate(scope, project.OrgId, doc.ProjectId);

        var org = await _planning.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == project.OrgId, ct);
        var orgName = org?.Name ?? "DefaultOrg";
        var projName = project.Name ?? "DefaultProj";

        string extractedText = doc.Content ?? string.Empty;
        var ext = (doc.Extension ?? Path.GetExtension(doc.Name) ?? string.Empty).ToLowerInvariant();

        try
        {
            if (!string.IsNullOrWhiteSpace(doc.RelativePath))
            {
                if (ext == ".docx")
                {
                    var preview = await _specStorage.ExtractDocxPreviewAsync(orgName, projName, doc.RelativePath, ct);
                    if (!string.IsNullOrWhiteSpace(preview.RawText))
                    {
                        extractedText = preview.RawText;
                    }
                }
                else if (ext == ".xlsx")
                {
                    var preview = await _specStorage.ExtractXlsxPreviewAsync(orgName, projName, doc.RelativePath, ct);
                    var sb = new StringBuilder();
                    foreach (var sheet in preview.Sheets)
                    {
                        sb.AppendLine($"[Sheet: {sheet.Name}]");
                        foreach (var row in sheet.Rows)
                        {
                            sb.AppendLine(string.Join(" | ", row));
                        }
                    }
                    if (sb.Length > 0) extractedText = sb.ToString();
                }
                else if (ext == ".pdf")
                {
                    var (stream, _, _) = await _specStorage.GetFileStreamAsync(orgName, projName, doc.RelativePath, ct);
                    await using (stream)
                    {
                        extractedText = await ExtractPdfAsync(stream, ct);
                    }
                }
                else
                {
                    var text = await _specStorage.ReadTextFileAsync(orgName, projName, doc.RelativePath, ct);
                    if (!string.IsNullOrWhiteSpace(text)) extractedText = text;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SpecHubTools] Không thể đọc nội dung vật lý của tài liệu {DocId}, dùng fallback DB", documentId);
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

        return SerializeJson(result, indented: true);
    }

    public async Task<string> SearchAsync(Guid projectId, string keyword, IProjectScopeContext scope, CancellationToken ct = default) =>
        await SearchAsync(projectId, keyword, scope, topK: 5, ct);

    public async Task<string> SearchAsync(Guid projectId, string keyword, IProjectScopeContext scope, int topK, CancellationToken ct = default)
    {
        if (projectId == Guid.Empty)
        {
            return SerializeJson(new { error = "INVALID_PROJECT_ID", message = "Lỗi: projectId không hợp lệ." });
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return SerializeJson(new { error = "EMPTY_KEYWORD", message = "Vui lòng nhập từ khóa tìm kiếm." });
        }

        ProjectScopeGuard.ValidateProject(scope, projectId);

        var project = await _planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return SerializeJson(new { error = "PROJECT_NOT_FOUND", message = "Lỗi: Không tìm thấy dự án với ID chỉ định." });
        }

        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        if (_vectorSearch != null)
        {
            // Ngữ nghĩa khi RAG đã bật; ResilientVectorSearchService tự quay về từ khóa nếu RAG tắt hoặc API lỗi.
            var hits = await _vectorSearch.SearchRelevantSpecsAsync(projectId, keyword.Trim(), Math.Clamp(topK, 1, 20), ct);
            if (hits.Count > 0)
            {
                return SerializeJson(new
                {
                    mode = hits.Any(hit => hit.Metadata.ContainsKey("chunkId")) ? "semantic" : "keyword",
                    query = keyword.Trim(),
                    results = hits.Select(hit => new
                    {
                        documentId = hit.Metadata.GetValueOrDefault("documentId") ?? hit.Id,
                        documentName = hit.Title,
                        heading = hit.Metadata.GetValueOrDefault("heading"),
                        page = hit.Metadata.GetValueOrDefault("page"),
                        score = hit.Score,
                        content = hit.ContentSnippet
                    })
                }, indented: true);
            }
        }

        var docs = await _planning.ProjectDocuments.AsNoTracking()
            .Where(d => d.ProjectId == projectId && !d.IsDirectory)
            .ToListAsync(ct);

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

        return SerializeJson(matches, indented: true);
    }

    public async Task<string> SearchRelevantSpecsAsync(IProjectScopeContext scope, string keyword, int topK = 5, CancellationToken ct = default)
    {
        return await SearchAsync(scope.ProjectId, keyword, scope, topK, ct);
    }

    private static async Task<string> ExtractPdfAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            var content = Encoding.ASCII.GetString(bytes);
            var sb = new StringBuilder();

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
                    using var decompStream = new MemoryStream(streamBytes);
                    using var ds = new DeflateStream(decompStream, CompressionMode.Decompress);
                    using var decompressed = new MemoryStream();
                    await ds.CopyToAsync(decompressed, ct);
                    var text = Encoding.UTF8.GetString(decompressed.ToArray());
                    ExtractPdfCommands(text, sb);
                }
                catch
                {
                    var rawText = Encoding.ASCII.GetString(streamBytes);
                    ExtractPdfCommands(rawText, sb);
                }

                streamIdx = endStreamIdx + 9;
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "[Tài liệu PDF định dạng quét/hình ảnh hoặc bảo vệ mật khẩu]" : result;
        }
        catch
        {
            return "[Không thể trích xuất văn bản từ tệp PDF này]";
        }
    }

    private static void ExtractPdfCommands(string streamContent, StringBuilder sb)
    {
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
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var idx = content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx == -1) return content.Length > 100 ? content[..97] + "..." : content;
        var start = Math.Max(0, idx - 40);
        var length = Math.Min(content.Length - start, 120);
        return "..." + content.Substring(start, length) + "...";
    }
}
