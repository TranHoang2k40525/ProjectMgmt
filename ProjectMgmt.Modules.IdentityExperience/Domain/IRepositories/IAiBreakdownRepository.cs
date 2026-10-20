using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.Models;

namespace IdentityExperience.Domain.IRepositories;

public interface IAiBreakdownRepository
{
    Task<AiModel?> GetActiveModelAsync(string taskType);
    Task<AiPromptTemplate?> GetActivePromptAsync(string taskType);
    Task<bool> AddGenerationAsync(AiGenerationLog generation);
    Task<bool> CompleteGenerationAsync(
        Guid generationId,
        string rawResponseJson,
        string parsedJson,
        List<AiSuggestedTask> suggestions,
        int latencyMs,
        DateTime completedAtUtc);
    Task MarkGenerationFailedAsync(
        Guid generationId,
        string errorCode,
        string errorMessage,
        DateTime completedAtUtc);
    Task<AiGenerationDetails?> GetGenerationAsync(Guid generationId);
    Task<AiFeedbackWriteStatus> SaveFeedbackAsync(
        Guid generationId,
        Guid suggestedTaskId,
        Guid reviewedBy,
        string userAction,
        string? finalSummary,
        string? finalDescription,
        string? finalAcceptanceCriteria,
        decimal? finalEstimatePoints,
        decimal editDistanceRatio,
        string? rejectReason,
        DateTime reviewedAtUtc);
    Task<List<AiPromptTemplate>> GetPromptTemplatesAsync(string? taskType);
    Task<AiPromptWriteResult> CreatePromptTemplateAsync(AiPromptTemplate promptTemplate);
    Task<AiPromptWriteResult> ActivatePromptTemplateAsync(Guid promptId);
}
