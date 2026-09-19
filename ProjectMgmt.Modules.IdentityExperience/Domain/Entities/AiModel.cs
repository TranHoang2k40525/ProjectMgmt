namespace IdentityExperience.Domain.Entities;

public class AiModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string? BaseModelCode { get; set; }
    public string? AdapterPath { get; set; }
    public Guid? TrainingRunId { get; set; }
    public bool IsActive { get; set; }
    public int? ContextWindow { get; set; }
    public string? DefaultParams { get; set; }
    public DateTime CreatedAt { get; set; }
}
