namespace DeliveryIntelligence.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}
