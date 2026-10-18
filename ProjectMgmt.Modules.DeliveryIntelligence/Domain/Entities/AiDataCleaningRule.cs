namespace DeliveryIntelligence.Domain.Entities;

public class AiDataCleaningRule
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Config { get; set; }
    public string AppliesTo { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
