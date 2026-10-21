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
