namespace DeliveryIntelligence.Domain.Entities;

public class AiEvaluationResult
{
    public Guid Id { get; set; }
    public Guid? TrainingRunId { get; set; }
    public Guid? ModelId { get; set; }
    public Guid DatasetVersionId { get; set; }
    public string SplitType { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public decimal MetricValue { get; set; }
    public int? SampleSize { get; set; }
    public string? Notes { get; set; }
    public DateTime EvaluatedAt { get; set; }
}
