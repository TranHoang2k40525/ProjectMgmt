namespace DeliveryIntelligence.Domain.Entities;

public class AiDatasetSample
{
    public Guid Id { get; set; }
    public Guid DatasetVersionId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceRefId { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public string InputJson { get; set; } = string.Empty;
    public string OutputJson { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public string SplitType { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = string.Empty;
    public decimal? QualityScore { get; set; }
    public bool IsPiiRedacted { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
