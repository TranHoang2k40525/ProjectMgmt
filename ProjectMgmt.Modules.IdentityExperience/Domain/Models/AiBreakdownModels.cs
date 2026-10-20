using IdentityExperience.Domain.Entities;

namespace IdentityExperience.Domain.Models;

public class AiGenerationDetails
{
    public AiGenerationLog Generation { get; set; } = new();
    public AiModel? Model { get; set; }
    public AiPromptTemplate? PromptTemplate { get; set; }
    public List<AiSuggestedTask> Suggestions { get; set; } = [];
}

public enum AiFeedbackWriteStatus
{
    Updated,
    GenerationNotFound,
    SuggestionNotFound,
    GenerationAlreadyApplied
}

public enum AiPromptWriteStatus
{
    Created,
    Activated,
    NotFound,
    Conflict
}

public class AiPromptWriteResult
{
    public AiPromptWriteStatus Status { get; set; }
    public AiPromptTemplate? PromptTemplate { get; set; }
}
