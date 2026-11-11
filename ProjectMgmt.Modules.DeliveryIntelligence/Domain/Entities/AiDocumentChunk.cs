namespace DeliveryIntelligence.Domain.Entities;

/// <summary>
/// Một đoạn văn bản của tài liệu Spec Hub kèm vector embedding, dùng cho tìm kiếm ngữ nghĩa (RAG).
/// Vector đã chuẩn hóa độ dài 1 nên tích vô hướng chính là độ tương đồng cosine.
/// </summary>
public class AiDocumentChunk
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string? Heading { get; set; }
    public int? PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    /// <summary>Dấu phiên bản của tài liệu nguồn + model + cách chia đoạn; đổi thì phải đánh chỉ mục lại.</summary>
    public string SourceVersion { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    /// <summary>float32 little-endian, đã chuẩn hóa.</summary>
    public byte[] Embedding { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
