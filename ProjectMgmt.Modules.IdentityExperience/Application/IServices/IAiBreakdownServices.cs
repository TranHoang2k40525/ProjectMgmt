using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface IAiBreakdownServices
{
    Task<AiBreakdownDto> GenerateAsync(Guid actorUserId, AiBreakdownDto request);
    Task<AiBreakdownDto> GetAsync(Guid actorUserId, Guid generationId);
    Task<AiBreakdownDto> SaveFeedbackAsync(
        Guid actorUserId,
        Guid generationId,
        AiBreakdownDto request);
    Task<AiBreakdownDto> ApplyAsync(
        Guid actorUserId,
        Guid generationId,
        AiBreakdownDto request);
    Task<AiPromptTemplateDto> GetPromptTemplatesAsync(string? taskType);
    Task<AiPromptTemplateDto> CreatePromptTemplateAsync(
        Guid actorUserId,
        AiPromptTemplateDto request);
    Task<AiPromptTemplateDto> ActivatePromptTemplateAsync(Guid actorUserId, Guid promptId);
}

public interface IAiBreakdownInference
{
    Task<List<AiBreakdownDto>> GenerateAsync(AiBreakdownDto source);
}

public interface IAiIssueSourceReader
{
    Task<AiIssueSource?> GetIssueSourceAsync(Guid issueId);
}

public interface IAiBreakdownApplyCoordinator
{
    Task<AiBreakdownDto> ApplyAsync(
        Guid generationId,
        Guid actorUserId,
        List<AiBreakdownDto>? selectedSubTasks);
}

public class AiIssueSource
{
    public Guid IssueId { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
}
