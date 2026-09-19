namespace DeliveryIntelligence.Domain.Entities;

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
