namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;

public class AiDataset
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Languages { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AiDatasetVersion
{
    public Guid Id { get; set; }
    public Guid DatasetId { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public int SampleCount { get; set; }
    public int TrainCount { get; set; }
    public int ValidationCount { get; set; }
    public int TestCount { get; set; }
    public int? SplitSeed { get; set; }
    public string? Checksum { get; set; }
    public string? ExportPath { get; set; }
    public bool IsFrozen { get; set; }
    public DateTime? FrozenAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AiDatasetSample
{
    public Guid Id { get; set; }
    public Guid DatasetVersionId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceRefId { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public string Language { get; set; } = "vi";
    public int? TokenCount { get; set; }
    public string SplitType { get; set; } = "Train";
    public string QualityStatus { get; set; } = "Raw";
    public decimal? QualityScore { get; set; }
    public bool IsPiiRedacted { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "NotReviewed";
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AiDataCleaningRule
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string? Config { get; set; }
    public string AppliesTo { get; set; } = "All";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

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

public class AiTrainingRun
{
    public Guid Id { get; set; }
    public Guid DatasetVersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string BaseModelCode { get; set; } = string.Empty;
    public string Method { get; set; } = "LoRA";
    public string? Hyperparameters { get; set; }
    public string Status { get; set; } = "Queued";
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

public class AiEvaluationResult
{
    public Guid Id { get; set; }
    public Guid? TrainingRunId { get; set; }
    public Guid? ModelId { get; set; }
    public Guid DatasetVersionId { get; set; }
    public string SplitType { get; set; } = "Test";
    public string MetricName { get; set; } = string.Empty;
    public decimal MetricValue { get; set; }
    public int? SampleSize { get; set; }
    public string? Notes { get; set; }
    public DateTime EvaluatedAt { get; set; }
}
