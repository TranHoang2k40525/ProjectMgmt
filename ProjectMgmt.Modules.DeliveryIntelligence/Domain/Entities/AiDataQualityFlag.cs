namespace DeliveryIntelligence.Domain.Entities;

public class AiDataQualityFlag
{
    public Guid Id { get; set; }
    public Guid SampleId { get; set; }
    public Guid RuleId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? FieldPath { get; set; }
    public bool IsResolved { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime DetectedAt { get; set; }
}
