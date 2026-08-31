namespace ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;

public class AiModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = "Ollama";
    public string TaskType { get; set; } = string.Empty;
    public string? BaseModelCode { get; set; }
    public string? AdapterPath { get; set; }
    public Guid? TrainingRunId { get; set; }
    public bool IsActive { get; set; }
    public int? ContextWindow { get; set; }
    public string? DefaultParams { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AiPromptTemplate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string TaskType { get; set; } = string.Empty;
    public string Language { get; set; } = "vi";
    public string SystemPrompt { get; set; } = string.Empty;
    public string? UserTemplate { get; set; }
    public string? JsonSchema { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
