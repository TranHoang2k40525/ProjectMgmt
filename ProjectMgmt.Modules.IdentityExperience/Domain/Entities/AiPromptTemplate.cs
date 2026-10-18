namespace IdentityExperience.Domain.Entities;

public class AiPromptTemplate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string? UserTemplate { get; set; }
    public string? JsonSchema { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
