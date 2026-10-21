namespace DeliveryIntelligence.Application.Dto;

public class DatasetDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<string> Languages { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class CreateDatasetDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Languages { get; set; } = ["vi"];
}

public class DatasetVersionDto
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
    public bool IsFrozen { get; set; }
    public DateTime? FrozenAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDatasetVersionDto
{
    public string VersionTag { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class DatasetSampleDto
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
}

public class SaveDatasetSampleDto
{
    public string SourceType { get; set; } = "ManualCurated";
    public Guid? SourceRefId { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public string Language { get; set; } = "vi";
    public string SplitType { get; set; } = "Train";
}

public class SampleQueryDto
{
    public string? QualityStatus { get; set; }
    public string? ReviewStatus { get; set; }
    public string? SourceType { get; set; }
    public string? SplitType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CleaningRuleDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Config { get; set; }
    public string AppliesTo { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsSupported { get; set; }
}

public class CreateCleaningRuleDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string? Config { get; set; }
    public string AppliesTo { get; set; } = "All";
}

public class UpdateCleaningRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string? Config { get; set; }
    public string AppliesTo { get; set; } = "All";
    public bool IsActive { get; set; } = true;
}

public class QualityFlagDto
{
    public Guid Id { get; set; }
    public Guid SampleId { get; set; }
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? FieldPath { get; set; }
    public bool IsResolved { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime DetectedAt { get; set; }
}

public class CleaningRunSummaryDto
{
    public Guid VersionId { get; set; }
    public int SamplesChecked { get; set; }
    public int RulesApplied { get; set; }
    public int FlagsCreated { get; set; }
    public int Cleaned { get; set; }
    public int Rejected { get; set; }
    public Dictionary<string, int> FlagsBySeverity { get; set; } = [];
}
