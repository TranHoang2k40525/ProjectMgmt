namespace DeliveryIntelligence.Domain.Entities;

public class AiTrainingRun
{
    public Guid Id { get; set; }
    public Guid DatasetVersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string BaseModelCode { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? Hyperparameters { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ArtifactPath { get; set; }
    public string? LogPath { get; set; }
    public decimal? TrainLoss { get; set; }
    public decimal? ValidationLoss { get; set; }
    public int? DurationSeconds { get; set; }
    public string? HardwareInfo { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
