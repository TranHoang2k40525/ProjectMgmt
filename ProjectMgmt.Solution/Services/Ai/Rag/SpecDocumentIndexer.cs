namespace ProjectMgmt.Solution.Services.Ai.Rag;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DeliveryIntelligence.Infrastructure.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using Planning.Infrastructure.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

public sealed record RagSyncResult(int Indexed, int Unchanged, int Removed, int Skipped, IReadOnlyList<string> Errors)
{
    public static RagSyncResult Empty { get; } = new(0, 0, 0, 0, []);
}

/// <summary>
/// Đối chiếu tài liệu Spec Hub của dự án với bảng AiDocumentChunk: chỉ đánh chỉ mục lại tài liệu đổi phiên bản,
/// xóa đoạn của tài liệu đã xóa. Văn bản: txt/md/docx/xlsx qua Spec Hub storage, PDF theo trang (PdfPig),
/// ảnh và trang PDF scan qua model vision.
/// </summary>
public sealed class SpecDocumentIndexer(
    PlanningDbContext planning,
    ISpecHubStorageService storage,
    DocumentChunkStore store,
    IEmbeddingClient embedder,
    IImageTextReader imageReader,
    IOptions<RagOptions> ragOptions,
    ILogger<SpecDocumentIndexer> logger)
{
    private static readonly Dictionary<string, string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".csv", ".json", ".xml", ".yaml", ".yml", ".html", ".htm", ".sql", ""
    };

    private const int MinPageTextLength = 30;
    private const int MaxImagesPerDocument = 20;

    /// <summary>Phiên bản đã thử nhưng không ra chữ (ảnh trống, PDF scan khi chưa cấu hình vision): không gọi API lại.</summary>
    private static readonly ConcurrentDictionary<string, byte> EmptyVersions = new();

    private RagOptions Options => ragOptions.Value;

    public async Task<IReadOnlyList<Guid>> ProjectsWithDocumentsAsync(CancellationToken ct = default) =>
        await planning.ProjectDocuments.AsNoTracking()
            .Where(doc => !doc.IsDirectory)
            .Join(planning.Projects.AsNoTracking().Where(project => !project.IsDeleted), doc => doc.ProjectId, project => project.Id, (doc, _) => doc.ProjectId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<RagSyncResult> SyncProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!Options.IsSearchReady) return RagSyncResult.Empty;

        var project = await planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project is null) return RagSyncResult.Empty;
        var org = await planning.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == project.OrgId, ct);
        var orgName = org?.Name ?? string.Empty;

        var documents = await planning.ProjectDocuments.AsNoTracking()
            .Where(doc => doc.ProjectId == projectId && !doc.IsDirectory)
            .ToListAsync(ct);
        var indexed = await store.GetIndexedVersionsAsync(projectId, ct);
        var removed = await store.RemoveDocumentsExceptAsync(projectId, documents.Select(doc => doc.Id).ToHashSet(), ct);

        int done = 0, unchanged = 0, skipped = 0;
        var errors = new List<string>();
        foreach (var doc in documents)
        {
            var version = SourceVersion(doc);
            if (indexed.TryGetValue(doc.Id, out var current) && current == version) { unchanged++; continue; }
            if (EmptyVersions.ContainsKey(version) || doc.FileSize > Options.MaxFileBytes) { skipped++; continue; }

            var name = DisplayName(doc);
            try
            {
                var segments = await ExtractAsync(doc, orgName, project.Name, ct);
                var chunks = TextChunker.Split(segments, Options.ChunkSize, Options.ChunkOverlap, Options.MaxChunksPerDocument);
                if (chunks.Count == 0)
                {
                    EmptyVersions.TryAdd(version, 0);
                    skipped++;
                    continue;
                }
                var vectors = await EmbedAsync(chunks, name, ct);
                await store.ReplaceDocumentAsync(projectId, doc.Id, name, version, embedder.Model, chunks.Zip(vectors, (chunk, vector) => new RagIndexedChunk(chunk, vector)).ToList(), ct);
                done++;
            }
            catch (RagProviderException ex)
            {
                // Lỗi API (key sai, hết hạn mức...): dừng cả lượt, lần sau thử lại.
                logger.LogWarning("[RAG] Dừng đánh chỉ mục dự án {ProjectId}: {Message}", projectId, ex.Message);
                errors.Add($"{name}: {ex.Message}");
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[RAG] Không đánh chỉ mục được tài liệu {DocumentId}", doc.Id);
                errors.Add($"{name}: {ex.Message}");
            }
        }

        if (done > 0 || removed > 0 || errors.Count > 0)
        {
            logger.LogInformation("[RAG] Dự án {ProjectId}: {Indexed} đánh chỉ mục, {Unchanged} giữ nguyên, {Removed} xóa, {Skipped} bỏ qua, {Errors} lỗi",
                projectId, done, unchanged, removed, skipped, errors.Count);
        }
        return new RagSyncResult(done, unchanged, removed, skipped, errors);
    }

    private string SourceVersion(ProjectDocument doc)
    {
        var raw = string.Join('|', doc.Id, (doc.UpdatedAt ?? doc.CreatedAt).Ticks, doc.FileSize, doc.Version, doc.Content.Length,
            embedder.Model, Options.ChunkSize, Options.ChunkOverlap, Options.IsVisionReady ? Options.Vision.Model : "-");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string DisplayName(ProjectDocument doc) =>
        !string.IsNullOrWhiteSpace(doc.Name) ? doc.Name : string.IsNullOrWhiteSpace(doc.Title) ? doc.Id.ToString() : doc.Title;

    private async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<RagChunk> chunks, string documentName, CancellationToken ct)
    {
        var vectors = new List<float[]>(chunks.Count);
        foreach (var batch in chunks.Chunk(Math.Max(1, Options.EmbeddingBatchSize)))
        {
            // Kèm tên tài liệu và đề mục để vector mang ngữ cảnh của đoạn.
            var inputs = batch.Select(chunk => $"{documentName}{(chunk.Heading is null ? "" : " › " + chunk.Heading)}\n{chunk.Content}").ToList();
            vectors.AddRange(await embedder.EmbedAsync(inputs, ct));
        }
        return vectors;
    }

    private async Task<IReadOnlyList<RagTextSegment>> ExtractAsync(ProjectDocument doc, string orgName, string projectName, CancellationToken ct)
    {
        var ext = (doc.Extension ?? Path.GetExtension(doc.Name) ?? string.Empty).ToLowerInvariant();
        if (!ext.StartsWith('.') && ext.Length > 0) ext = "." + ext;
        var fromDb = string.IsNullOrWhiteSpace(doc.Content) ? [] : new[] { new RagTextSegment(doc.Content) };
        if (string.IsNullOrWhiteSpace(doc.RelativePath)) return fromDb;

        if (ImageMimeTypes.TryGetValue(ext, out var mime))
        {
            if (!imageReader.IsConfigured || doc.FileSize > Options.MaxImageBytes) return fromDb;
            var bytes = await ReadBytesAsync(orgName, projectName, doc.RelativePath, ct);
            var text = await imageReader.ReadAsync(bytes, mime, ct);
            return string.IsNullOrWhiteSpace(text) ? fromDb : [new RagTextSegment(text, Heading: DisplayName(doc))];
        }

        switch (ext)
        {
            case ".pdf":
                return await ExtractPdfAsync(await ReadBytesAsync(orgName, projectName, doc.RelativePath, ct), ct);
            case ".docx":
                var docx = await storage.ExtractDocxPreviewAsync(orgName, projectName, doc.RelativePath, ct);
                return string.IsNullOrWhiteSpace(docx.RawText) ? fromDb : [new RagTextSegment(docx.RawText)];
            case ".xlsx":
                var xlsx = await storage.ExtractXlsxPreviewAsync(orgName, projectName, doc.RelativePath, ct);
                return xlsx.Sheets
                    .Select(sheet => new RagTextSegment(string.Join('\n', sheet.Rows.Select(row => string.Join(" | ", row))), Heading: $"Sheet: {sheet.Name}"))
                    .ToList();
            default:
                if (!TextExtensions.Contains(ext)) return fromDb;
                var content = await storage.ReadTextFileAsync(orgName, projectName, doc.RelativePath, ct);
                return string.IsNullOrWhiteSpace(content) ? fromDb : [new RagTextSegment(content)];
        }
    }

    private async Task<byte[]> ReadBytesAsync(string orgName, string projectName, string relativePath, CancellationToken ct)
    {
        var (stream, _, _) = await storage.GetFileStreamAsync(orgName, projectName, relativePath, ct);
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
    }

    /// <summary>Mỗi trang một khối; trang gần như không có chữ (scan) thì đọc ảnh trong trang bằng vision.</summary>
    private async Task<IReadOnlyList<RagTextSegment>> ExtractPdfAsync(byte[] bytes, CancellationToken ct)
    {
        var segments = new List<RagTextSegment>();
        var imagesRead = 0;
        using var pdf = PdfDocument.Open(bytes);
        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var text = ContentOrderTextExtractor.GetText(page);
            if (text.Trim().Length < MinPageTextLength && imageReader.IsConfigured)
            {
                var pageText = new StringBuilder(text);
                foreach (var image in page.GetImages())
                {
                    if (imagesRead >= MaxImagesPerDocument) break;
                    if (!image.TryGetPng(out var png) || png.Length > Options.MaxImageBytes) continue;
                    imagesRead++;
                    pageText.AppendLine().Append(await imageReader.ReadAsync(png, "image/png", ct));
                }
                text = pageText.ToString();
            }
            if (!string.IsNullOrWhiteSpace(text)) segments.Add(new RagTextSegment(text, page.Number));
        }
        return segments;
    }
}
