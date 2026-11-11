using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure.Services.Rag;

public sealed record RagSearchHit(Guid ChunkId, Guid DocumentId, string DocumentName, string? Heading, int? PageNumber, string Content, double Score);

public sealed record RagIndexedChunk(RagChunk Chunk, float[] Vector);

/// <summary>
/// Lưu và tìm các đoạn tài liệu trong bảng AiDocumentChunk. Tìm kiếm luôn lọc theo ProjectId và model,
/// tính cosine trong bộ nhớ (đủ nhanh cho vài chục nghìn đoạn mỗi dự án).
/// </summary>
public sealed class DocumentChunkStore(DeliveryIntelligenceDbContext db)
{
    /// <summary>DocumentId → SourceVersion của các tài liệu đã đánh chỉ mục trong dự án.</summary>
    public async Task<Dictionary<Guid, string>> GetIndexedVersionsAsync(Guid projectId, CancellationToken ct = default) =>
        await db.AiDocumentChunks.AsNoTracking()
            .Where(chunk => chunk.ProjectId == projectId)
            .GroupBy(chunk => chunk.DocumentId)
            .Select(group => new { group.Key, Version = group.Min(chunk => chunk.SourceVersion) })
            .ToDictionaryAsync(row => row.Key, row => row.Version ?? string.Empty, ct);

    /// <summary>Thay toàn bộ đoạn của một tài liệu trong một lần SaveChanges (một transaction).</summary>
    public async Task ReplaceDocumentAsync(
        Guid projectId, Guid documentId, string documentName, string sourceVersion, string model,
        IReadOnlyList<RagIndexedChunk> chunks, CancellationToken ct = default)
    {
        var existing = await db.AiDocumentChunks.Where(chunk => chunk.DocumentId == documentId).ToListAsync(ct);
        db.AiDocumentChunks.RemoveRange(existing);
        var now = DateTime.UtcNow;
        db.AiDocumentChunks.AddRange(chunks.Select(item => new AiDocumentChunk
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            DocumentId = documentId,
            ChunkIndex = item.Chunk.Index,
            DocumentName = Truncate(documentName, 500),
            Heading = item.Chunk.Heading is null ? null : Truncate(item.Chunk.Heading, 500),
            PageNumber = item.Chunk.PageNumber,
            Content = item.Chunk.Content,
            SourceVersion = sourceVersion,
            EmbeddingModel = model,
            Dimensions = item.Vector.Length,
            Embedding = VectorMath.ToBytes(VectorMath.Normalize(item.Vector)),
            CreatedAt = now
        }));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Xóa đoạn của các tài liệu không còn trong Spec Hub (đã xóa hoặc thành thư mục).</summary>
    public async Task<int> RemoveDocumentsExceptAsync(Guid projectId, IReadOnlySet<Guid> keepDocumentIds, CancellationToken ct = default)
    {
        var stale = await db.AiDocumentChunks
            .Where(chunk => chunk.ProjectId == projectId && !keepDocumentIds.Contains(chunk.DocumentId))
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;
        db.AiDocumentChunks.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        return stale.Select(chunk => chunk.DocumentId).Distinct().Count();
    }

    public async Task<IReadOnlyList<RagSearchHit>> SearchAsync(
        Guid projectId, string model, float[] normalizedQuery, int topK, double minScore, CancellationToken ct = default)
    {
        var candidates = await db.AiDocumentChunks.AsNoTracking()
            .Where(chunk => chunk.ProjectId == projectId && chunk.EmbeddingModel == model && chunk.Dimensions == normalizedQuery.Length)
            .Select(chunk => new { chunk.Id, chunk.Embedding })
            .ToListAsync(ct);

        var top = candidates
            .Select(candidate => (candidate.Id, Score: (double)VectorMath.Dot(normalizedQuery, VectorMath.FromBytes(candidate.Embedding))))
            .Where(item => item.Score >= minScore)
            .OrderByDescending(item => item.Score)
            .Take(Math.Clamp(topK, 1, 50))
            .ToList();
        if (top.Count == 0) return [];

        var ids = top.Select(item => item.Id).ToList();
        var details = await db.AiDocumentChunks.AsNoTracking()
            .Where(chunk => ids.Contains(chunk.Id))
            .Select(chunk => new { chunk.Id, chunk.DocumentId, chunk.DocumentName, chunk.Heading, chunk.PageNumber, chunk.Content })
            .ToDictionaryAsync(chunk => chunk.Id, ct);

        return top
            .Where(item => details.ContainsKey(item.Id))
            .Select(item =>
            {
                var chunk = details[item.Id];
                return new RagSearchHit(chunk.Id, chunk.DocumentId, chunk.DocumentName, chunk.Heading, chunk.PageNumber, chunk.Content, Math.Round(item.Score, 4));
            })
            .ToList();
    }

    public async Task<(int Documents, int Chunks)> CountAsync(Guid projectId, CancellationToken ct = default)
    {
        var documents = await db.AiDocumentChunks.AsNoTracking().Where(chunk => chunk.ProjectId == projectId).Select(chunk => chunk.DocumentId).Distinct().CountAsync(ct);
        var chunks = await db.AiDocumentChunks.AsNoTracking().CountAsync(chunk => chunk.ProjectId == projectId, ct);
        return (documents, chunks);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>Tìm kiếm ngữ nghĩa: embed câu hỏi rồi xếp hạng các đoạn của đúng dự án.</summary>
public sealed class RagSearchService(IEmbeddingClient embedder, DocumentChunkStore store, RagOptions options)
{
    public async Task<IReadOnlyList<RagSearchHit>> SearchAsync(Guid projectId, string query, int? topK = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var vectors = await embedder.EmbedAsync([query.Trim()], ct);
        return await store.SearchAsync(projectId, embedder.Model, VectorMath.Normalize(vectors[0]), topK ?? options.TopK, options.MinScore, ct);
    }
}
