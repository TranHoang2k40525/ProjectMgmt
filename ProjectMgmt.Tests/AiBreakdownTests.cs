using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Application.Services;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;
using Xunit;

namespace ProjectMgmt.Tests;

public class AiBreakdownTests
{
    private static readonly DateTime TestNowUtc = new(2026, 9, 30, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FakeInferenceReturnsDeterministicStructuredTasks()
    {
        var inference = new FakeAiBreakdownInference();

        var first = await inference.GenerateAsync(new AiBreakdownDto
        {
            StoryTitle = "Đăng nhập OTP",
            ProjectContext = "ASP.NET Core"
        });
        var second = await inference.GenerateAsync(new AiBreakdownDto
        {
            StoryTitle = "Đăng nhập OTP",
            ProjectContext = "ASP.NET Core"
        });

        Assert.Equal(3, first.Count);
        Assert.Equal(first.Select(item => item.Title), second.Select(item => item.Title));
        Assert.All(first, item => Assert.NotEmpty(item.AcceptanceCriteria!));
    }

    [Fact]
    public async Task GeneratePersistsRealAuditAndSuggestionsAroundFakeInference()
    {
        var actorUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        var repository = new FakeAiBreakdownRepository();
        var notifications = new CapturingNotificationServices();
        var service = CreateService(
            repository,
            new FakeAiIssueSourceReader
            {
                Source = new AiIssueSource
                {
                    IssueId = issueId,
                    ProjectId = projectId,
                    Title = "Story thật trong DB"
                }
            },
            notifications);

        var result = await service.GenerateAsync(actorUserId, new AiBreakdownDto
        {
            IssueId = issueId,
            StoryTitle = "Tiêu đề không đáng tin từ client",
            ProjectContext = "Modular Monolith"
        });

        Assert.True(result.Success);
        Assert.Equal("Completed", result.Status);
        Assert.NotNull(repository.CreatedGeneration);
        Assert.Equal("Processing", repository.CreatedGeneration.Status);
        Assert.True(repository.CompleteCalled);
        Assert.Equal(3, repository.CompletedSuggestions.Count);
        Assert.Contains("Story thật trong DB", repository.CompletedSuggestions[0].OriginalSummary);
        Assert.Equal(NotificationTypes.AiBreakdownCompleted, notifications.Published!.Type);
    }

    [Fact]
    public async Task FeedbackCalculatesEditRatioOnServer()
    {
        var actorUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        var repository = new FakeAiBreakdownRepository
        {
            GenerationDetails = new AiGenerationDetails
            {
                Generation = new AiGenerationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = actorUserId,
                    ProjectId = projectId,
                    Status = "Completed"
                },
                Suggestions =
                [
                    new AiSuggestedTask
                    {
                        Id = suggestionId,
                        OriginalSummary = "Tạo API đăng nhập",
                        OriginalDescription = "Mô tả ban đầu",
                        UserAction = "Pending"
                    }
                ]
            }
        };
        var service = CreateService(repository, new FakeAiIssueSourceReader());

        var result = await service.SaveFeedbackAsync(
            actorUserId,
            repository.GenerationDetails.Generation.Id,
            new AiBreakdownDto
            {
                SuggestedTaskId = suggestionId,
                UserAction = "Edited",
                FinalSummary = "Tạo API đăng nhập an toàn",
                FinalDescription = "Mô tả đã sửa",
                EditDistanceRatio = 0m
            });

        Assert.True(result.Success);
        Assert.True(repository.SavedEditDistanceRatio > 0m);
        Assert.Equal(repository.SavedEditDistanceRatio, result.EditDistanceRatio);
    }

    [Fact]
    public async Task ApplyDelegatesSelectedStoredSuggestionsWhenPermissionIsGranted()
    {
        var actorUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var suggestedTaskId = Guid.NewGuid();
        var repository = new FakeAiBreakdownRepository
        {
            GenerationDetails = new AiGenerationDetails
            {
                Generation = new AiGenerationLog
                {
                    Id = generationId,
                    UserId = actorUserId,
                    ProjectId = projectId,
                    IssueId = Guid.NewGuid(),
                    Status = "Completed"
                }
            }
        };
        var coordinator = new FakeAiBreakdownApplyCoordinator();
        var service = CreateService(
            repository,
            new FakeAiIssueSourceReader(),
            applyCoordinator: coordinator);

        var result = await service.ApplyAsync(actorUserId, generationId, new AiBreakdownDto
        {
            ParentIssueId = repository.GenerationDetails.Generation.IssueId,
            SelectedSubTasks =
            [
                new AiBreakdownDto { SuggestedTaskId = suggestedTaskId }
            ]
        });

        Assert.True(result.Success);
        Assert.Equal(generationId, coordinator.GenerationId);
        Assert.Equal(actorUserId, coordinator.ActorUserId);
        Assert.Equal(suggestedTaskId, coordinator.SelectedSubTasks![0].SuggestedTaskId);
    }

    private static AiBreakdownServices CreateService(
        FakeAiBreakdownRepository repository,
        FakeAiIssueSourceReader sourceReader,
        CapturingNotificationServices? notifications = null,
        FakeAiBreakdownApplyCoordinator? applyCoordinator = null)
    {
        return new AiBreakdownServices(
            repository,
            new FakeAiBreakdownInference(),
            sourceReader,
            applyCoordinator ?? new FakeAiBreakdownApplyCoordinator(),
            new AllowAllPermissionEvaluator(),
            new FakeProjectLookupService(),
            notifications ?? new CapturingNotificationServices(),
            new FixedTimeProvider(TestNowUtc));
    }
}

public class FakeAiBreakdownRepository : IAiBreakdownRepository
{
    public AiGenerationLog? CreatedGeneration { get; private set; }
    public bool CompleteCalled { get; private set; }
    public List<AiSuggestedTask> CompletedSuggestions { get; private set; } = [];
    public AiGenerationDetails? GenerationDetails { get; set; }
    public decimal SavedEditDistanceRatio { get; private set; }

    public Task<AiModel?> GetActiveModelAsync(string taskType)
    {
        return Task.FromResult<AiModel?>(null);
    }

    public Task<AiPromptTemplate?> GetActivePromptAsync(string taskType)
    {
        return Task.FromResult<AiPromptTemplate?>(null);
    }

    public Task<bool> AddGenerationAsync(AiGenerationLog generation)
    {
        CreatedGeneration = generation;
        return Task.FromResult(true);
    }

    public Task<bool> CompleteGenerationAsync(
        Guid generationId,
        string rawResponseJson,
        string parsedJson,
        List<AiSuggestedTask> suggestions,
        int latencyMs,
        DateTime completedAtUtc)
    {
        CompleteCalled = true;
        CompletedSuggestions = suggestions;
        return Task.FromResult(true);
    }

    public Task MarkGenerationFailedAsync(
        Guid generationId,
        string errorCode,
        string errorMessage,
        DateTime completedAtUtc)
    {
        return Task.CompletedTask;
    }

    public Task<AiGenerationDetails?> GetGenerationAsync(Guid generationId)
    {
        return Task.FromResult(GenerationDetails);
    }

    public Task<AiFeedbackWriteStatus> SaveFeedbackAsync(
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
        DateTime reviewedAtUtc)
    {
        SavedEditDistanceRatio = editDistanceRatio;
        return Task.FromResult(AiFeedbackWriteStatus.Updated);
    }

    public Task<List<AiPromptTemplate>> GetPromptTemplatesAsync(string? taskType)
    {
        return Task.FromResult(new List<AiPromptTemplate>());
    }

    public Task<AiPromptWriteResult> CreatePromptTemplateAsync(AiPromptTemplate promptTemplate)
    {
        return Task.FromResult(new AiPromptWriteResult
        {
            Status = AiPromptWriteStatus.Created,
            PromptTemplate = promptTemplate
        });
    }

    public Task<AiPromptWriteResult> ActivatePromptTemplateAsync(Guid promptId)
    {
        return Task.FromResult(new AiPromptWriteResult
        {
            Status = AiPromptWriteStatus.Activated,
            PromptTemplate = new AiPromptTemplate { Id = promptId }
        });
    }
}

public class FakeAiIssueSourceReader : IAiIssueSourceReader
{
    public AiIssueSource? Source { get; set; }

    public Task<AiIssueSource?> GetIssueSourceAsync(Guid issueId)
    {
        return Task.FromResult(Source);
    }
}

public class FakeAiBreakdownApplyCoordinator : IAiBreakdownApplyCoordinator
{
    public Guid GenerationId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public List<AiBreakdownDto>? SelectedSubTasks { get; private set; }

    public Task<AiBreakdownDto> ApplyAsync(
        Guid generationId,
        Guid actorUserId,
        List<AiBreakdownDto>? selectedSubTasks)
    {
        GenerationId = generationId;
        ActorUserId = actorUserId;
        SelectedSubTasks = selectedSubTasks;
        return Task.FromResult(new AiBreakdownDto
        {
            Success = true,
            GenerationId = generationId,
            Applied = true
        });
    }
}

public class AllowAllPermissionEvaluator : IPermissionEvaluator
{
    public Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        string scopeType,
        Guid? scopeId,
        Guid? organizationId = null)
    {
        return Task.FromResult(true);
    }

    public Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
    {
        return Task.FromResult(true);
    }
}

public class FakeProjectLookupService : IProjectLookupService
{
    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectIssueTypeDto>>([]);
    }

    public Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectStatusDto>>([]);
    }

    public Task<ProjectScopeInfo?> GetProjectScopeAsync(Guid projectId)
    {
        return Task.FromResult<ProjectScopeInfo?>(new ProjectScopeInfo
        {
            ProjectId = projectId,
            OrganizationId = Guid.NewGuid(),
            IsArchived = false
        });
    }
}
