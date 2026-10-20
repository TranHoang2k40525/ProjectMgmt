using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planning.Infrastructure;

namespace ProjectMgmt.Solution.Services;

public class AiBreakdownApplyCoordinator : IAiBreakdownApplyCoordinator
{
    private static readonly Action<ILogger, Guid, Exception?> LogApplyFailure =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2020, "AiBreakdownApplyFailed"),
            "Không thể áp dụng AI generation {GenerationId} thành Issue thật.");

    private readonly IdentityExperienceDbContext _identityContext;
    private readonly DeliveryIntelligenceDbContext _deliveryContext;
    private readonly PlanningDbContext _planningContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AiBreakdownApplyCoordinator> _logger;

    public AiBreakdownApplyCoordinator(
        IdentityExperienceDbContext identityContext,
        DeliveryIntelligenceDbContext deliveryContext,
        PlanningDbContext planningContext,
        TimeProvider timeProvider,
        ILogger<AiBreakdownApplyCoordinator> logger)
    {
        _identityContext = identityContext;
        _deliveryContext = deliveryContext;
        _planningContext = planningContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AiBreakdownDto> ApplyAsync(
        Guid generationId,
        Guid actorUserId,
        List<AiBreakdownDto>? selectedSubTasks)
    {
        try
        {
            return await ExecuteInSharedTransactionAsync(async () =>
            {
                var generationRows = await _identityContext.AiGenerationLogs
                    .FromSqlInterpolated($"SELECT * FROM `AiGenerationLog` WHERE `Id` = {generationId} FOR UPDATE")
                    .ToListAsync();
                var generation = generationRows.SingleOrDefault();
                if (generation is null)
                {
                    return Failure("AI_BREAKDOWN_NOT_FOUND", "Không tìm thấy lần phân rã AI.");
                }

                var suggestionRows = await _identityContext.AiSuggestedTasks
                    .FromSqlInterpolated($"SELECT * FROM `AiSuggestedTask` WHERE `AiGenerationLogId` = {generationId} ORDER BY `OrderIndex` FOR UPDATE")
                    .ToListAsync();
                if (generation.AppliedAt.HasValue)
                {
                    return await BuildAlreadyAppliedResultAsync(generation, suggestionRows);
                }

                if (generation.Status != "Completed")
                {
                    return Failure("AI_BREAKDOWN_NOT_COMPLETED", "Chỉ có thể áp dụng lần phân rã đã hoàn tất.");
                }

                if (selectedSubTasks is null || selectedSubTasks.Count is < 1 or > 50)
                {
                    return Failure("AI_BREAKDOWN_SELECTION_REQUIRED", "Hãy chọn từ 1 đến 50 gợi ý để áp dụng.");
                }

                var selectedById = new Dictionary<Guid, AiBreakdownDto>();
                foreach (var selected in selectedSubTasks)
                {
                    if (!selected.SuggestedTaskId.HasValue
                        || !selectedById.TryAdd(selected.SuggestedTaskId.Value, selected))
                    {
                        return Failure("AI_BREAKDOWN_SELECTION_INVALID", "Danh sách gợi ý được chọn không hợp lệ hoặc bị trùng.");
                    }
                }

                var suggestionsById = suggestionRows.ToDictionary(suggestion => suggestion.Id);
                if (selectedById.Keys.Any(id => !suggestionsById.ContainsKey(id)))
                {
                    return Failure("AI_BREAKDOWN_SELECTION_INVALID", "Có gợi ý không thuộc lần phân rã này.");
                }

                var projectRows = await _planningContext.Projects
                    .FromSqlInterpolated($"SELECT * FROM `Project` WHERE `Id` = {generation.ProjectId} FOR UPDATE")
                    .ToListAsync();
                var project = projectRows.SingleOrDefault();
                if (project is null || project.IsDeleted || project.IsArchived)
                {
                    return Failure("AI_BREAKDOWN_PROJECT_UNAVAILABLE", "Dự án không còn khả dụng.");
                }

                var parentRows = await _deliveryContext.Issues
                    .FromSqlInterpolated($"SELECT * FROM `Issue` WHERE `Id` = {generation.IssueId} FOR UPDATE")
                    .ToListAsync();
                var parent = parentRows.SingleOrDefault();
                if (parent is null || parent.IsDeleted || parent.ProjectId != generation.ProjectId)
                {
                    return Failure("AI_BREAKDOWN_PARENT_UNAVAILABLE", "Issue cha không còn khả dụng.");
                }

                var issueType = await _planningContext.IssueTypes
                    .Where(type => type.ProjectId == generation.ProjectId && type.IsSubtask)
                    .OrderBy(type => type.OrderIndex)
                    .FirstOrDefaultAsync();
                var initialStatus = await _planningContext.WorkflowStatuses
                    .Where(status => status.ProjectId == generation.ProjectId && status.IsInitial)
                    .OrderBy(status => status.OrderIndex)
                    .FirstOrDefaultAsync();
                if (issueType is null || initialStatus is null)
                {
                    return Failure(
                        "AI_BREAKDOWN_PROJECT_CONFIG_MISSING",
                        "Dự án thiếu loại Subtask hoặc trạng thái khởi tạo.");
                }

                var rank = await _deliveryContext.Issues
                    .Where(issue => issue.ProjectId == generation.ProjectId)
                    .MaxAsync(issue => (decimal?)issue.RankOrder) ?? 0m;
                var maximumIssueNumber = await _deliveryContext.Issues
                    .Where(issue => issue.ProjectId == generation.ProjectId)
                    .MaxAsync(issue => (int?)issue.IssueNumber) ?? 0;
                project.IssueCounter = Math.Max(project.IssueCounter, maximumIssueNumber);
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var createdDtos = new List<AiBreakdownDto>();
                var newIssues = new List<Issue>();
                var newCriteria = new List<AcceptanceCriteria>();
                var activityLogs = new List<ActivityLog>();

                foreach (var selectedEntry in selectedById)
                {
                    var suggestion = suggestionsById[selectedEntry.Key];
                    var selected = selectedEntry.Value;
                    if (suggestion.UserAction == "Rejected" || suggestion.CreatedIssueId.HasValue)
                    {
                        return Failure(
                            "AI_BREAKDOWN_SUGGESTION_UNAVAILABLE",
                            "Không thể áp dụng gợi ý đã từ chối hoặc đã tạo Issue.");
                    }

                    var title = OptionalText(
                        selected.FinalSummary
                        ?? selected.Title
                        ?? suggestion.FinalSummary
                        ?? suggestion.OriginalSummary);
                    var description = OptionalText(
                        selected.FinalDescription
                        ?? selected.Description
                        ?? suggestion.FinalDescription
                        ?? suggestion.OriginalDescription);
                    if (title is null || title.Length > 500 || description?.Length > 20_000)
                    {
                        return Failure("AI_BREAKDOWN_TASK_INVALID", "Tiêu đề hoặc mô tả sub-task không hợp lệ.");
                    }

                    var points = selected.FinalEstimatePoints
                        ?? selected.EstimatePoints
                        ?? suggestion.FinalEstimatePoints
                        ?? suggestion.OriginalEstimatePoints;
                    if (points is < 0 or > 9999)
                    {
                        return Failure("AI_BREAKDOWN_ESTIMATE_INVALID", "Ước lượng sub-task không hợp lệ.");
                    }

                    var estimatedHours = selected.EstimatedHours ?? points * 2;
                    if (estimatedHours is < 0 or > 100_000)
                    {
                        return Failure("AI_BREAKDOWN_HOURS_INVALID", "Số giờ ước lượng không hợp lệ.");
                    }

                    var criteria = NormalizeCriteria(
                        selected.FinalAcceptanceCriteria
                        ?? selected.AcceptanceCriteria
                        ?? DeserializeList(suggestion.FinalAcceptanceCriteria)
                        ?? DeserializeList(suggestion.OriginalAcceptanceCriteria));
                    if (criteria.Any(item => item.Length > 2000))
                    {
                        return Failure("AI_BREAKDOWN_CRITERIA_INVALID", "Mỗi tiêu chí nghiệm thu tối đa 2000 ký tự.");
                    }

                    if (project.IssueCounter == int.MaxValue)
                    {
                        return Failure("AI_BREAKDOWN_ISSUE_COUNTER_EXHAUSTED", "Bộ đếm Issue của dự án đã hết phạm vi.");
                    }

                    project.IssueCounter++;
                    rank += 1000m;
                    var issueId = Guid.NewGuid();
                    var issue = new Issue
                    {
                        Id = issueId,
                        ProjectId = generation.ProjectId,
                        IssueNumber = project.IssueCounter,
                        ParentId = parent.Id,
                        EpicId = parent.EpicId,
                        ReporterId = actorUserId,
                        StatusId = initialStatus.Id,
                        IssueTypeId = issueType.Id,
                        Title = title,
                        Description = description,
                        StoryPoints = points,
                        OriginalEstimateMinutes = estimatedHours.HasValue
                            ? checked((int)Math.Round(estimatedHours.Value * 60m, MidpointRounding.AwayFromZero))
                            : null,
                        TimeSpentMinutes = 0,
                        RankOrder = rank,
                        IsAiGenerated = true,
                        AiGenerationLogId = generation.Id,
                        IsAiAssigned = false,
                        IsDeleted = false,
                        CreatedAt = now
                    };
                    newIssues.Add(issue);

                    for (var index = 0; index < criteria.Count; index++)
                    {
                        newCriteria.Add(new AcceptanceCriteria
                        {
                            Id = Guid.NewGuid(),
                            IssueId = issueId,
                            Content = criteria[index],
                            IsMet = false,
                            OrderIndex = index,
                            Source = "AI",
                            AiGenerationLogId = generation.Id,
                            WasEditedAfterAi = selected.FinalAcceptanceCriteria is not null
                                || selected.AcceptanceCriteria is not null,
                            CreatedAt = now
                        });
                    }

                    activityLogs.Add(new ActivityLog
                    {
                        Id = Guid.NewGuid(),
                        IssueId = issueId,
                        UserId = actorUserId,
                        Action = "AiBreakdownApplied",
                        Detail = JsonSerializer.Serialize(new
                        {
                            generationId = generation.Id,
                            suggestedTaskId = suggestion.Id,
                            parentIssueId = parent.Id
                        }),
                        Source = "AI",
                        CreatedAt = now
                    });

                    var edited = title != suggestion.OriginalSummary
                        || description != suggestion.OriginalDescription
                        || points != suggestion.OriginalEstimatePoints;
                    suggestion.FinalSummary = title;
                    suggestion.FinalDescription = description;
                    suggestion.FinalAcceptanceCriteria = criteria.Count == 0
                        ? null
                        : JsonSerializer.Serialize(criteria);
                    suggestion.FinalEstimatePoints = points;
                    suggestion.UserAction = edited ? "Edited" : "Kept";
                    suggestion.EditDistanceRatio = edited ? 1m : 0m;
                    suggestion.CreatedIssueId = issueId;
                    suggestion.ReviewedBy = actorUserId;
                    suggestion.ReviewedAt = now;

                    createdDtos.Add(new AiBreakdownDto
                    {
                        SuggestedTaskId = suggestion.Id,
                        CreatedIssueId = issueId,
                        IssueKey = $"{project.ProjectKey}-{project.IssueCounter}",
                        Title = title
                    });
                }

                generation.AppliedAt = now;
                generation.AppliedCount = createdDtos.Count;
                await _deliveryContext.Issues.AddRangeAsync(newIssues);
                await _deliveryContext.AcceptanceCriteria.AddRangeAsync(newCriteria);
                await _deliveryContext.ActivityLogs.AddRangeAsync(activityLogs);
                await _planningContext.SaveChangesAsync();
                await _deliveryContext.SaveChangesAsync();
                await _identityContext.SaveChangesAsync();

                return new AiBreakdownDto
                {
                    Success = true,
                    Message = "Đã tạo sub-task thật từ các gợi ý AI.",
                    GenerationId = generation.Id,
                    IssueId = generation.IssueId,
                    ParentIssueId = generation.IssueId,
                    ProjectId = generation.ProjectId,
                    Applied = true,
                    AppliedCount = createdDtos.Count,
                    CreatedSubTasks = createdDtos
                };
            });
        }
        catch (Exception exception)
        {
            LogApplyFailure(_logger, generationId, exception);
            _identityContext.ChangeTracker.Clear();
            _deliveryContext.ChangeTracker.Clear();
            _planningContext.ChangeTracker.Clear();
            return Failure(
                "AI_BREAKDOWN_APPLY_FAILED",
                "Không thể áp dụng gợi ý thành Issue thật. Không có dữ liệu dở dang được lưu.");
        }
    }

    private async Task<AiBreakdownDto> ExecuteInSharedTransactionAsync(
        Func<Task<AiBreakdownDto>> operation)
    {
        var identityConnection = _identityContext.Database.GetDbConnection();
        var deliveryConnection = _deliveryContext.Database.GetDbConnection();
        var planningConnection = _planningContext.Database.GetDbConnection();
        if (!ReferenceEquals(identityConnection, deliveryConnection)
            || !ReferenceEquals(identityConnection, planningConnection))
        {
            throw new InvalidOperationException(
                "IdentityExperience, Planning và DeliveryIntelligence phải dùng cùng DbConnection khi AI apply.");
        }

        var strategy = _planningContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _planningContext.Database.BeginTransactionAsync();
            await using var identityTransaction = await _identityContext.Database.UseTransactionAsync(
                transaction.GetDbTransaction());
            await using var deliveryTransaction = await _deliveryContext.Database.UseTransactionAsync(
                transaction.GetDbTransaction());

            var result = await operation();
            if (!result.Success)
            {
                await transaction.RollbackAsync();
                _identityContext.ChangeTracker.Clear();
                _deliveryContext.ChangeTracker.Clear();
                _planningContext.ChangeTracker.Clear();
                return result;
            }

            await transaction.CommitAsync();
            return result;
        });
    }

    private async Task<AiBreakdownDto> BuildAlreadyAppliedResultAsync(
        AiGenerationLog generation,
        List<AiSuggestedTask> suggestions)
    {
        var createdIds = suggestions
            .Where(suggestion => suggestion.CreatedIssueId.HasValue)
            .Select(suggestion => suggestion.CreatedIssueId!.Value)
            .ToList();
        var issues = await _deliveryContext.Issues
            .AsNoTracking()
            .Where(issue => createdIds.Contains(issue.Id))
            .ToListAsync();
        var issueMap = issues.ToDictionary(issue => issue.Id);
        var projectKey = await _planningContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == generation.ProjectId)
            .Select(project => project.ProjectKey)
            .FirstOrDefaultAsync() ?? "ISSUE";

        return new AiBreakdownDto
        {
            Success = true,
            Message = "Lần phân rã này đã được áp dụng trước đó; hệ thống không tạo Issue trùng.",
            GenerationId = generation.Id,
            IssueId = generation.IssueId,
            ParentIssueId = generation.IssueId,
            ProjectId = generation.ProjectId,
            Applied = true,
            AppliedCount = generation.AppliedCount,
            CreatedSubTasks = suggestions
                .Where(suggestion => suggestion.CreatedIssueId.HasValue)
                .Select(suggestion =>
                {
                    issueMap.TryGetValue(suggestion.CreatedIssueId!.Value, out var issue);
                    return new AiBreakdownDto
                    {
                        SuggestedTaskId = suggestion.Id,
                        CreatedIssueId = suggestion.CreatedIssueId,
                        IssueKey = issue is null ? null : $"{projectKey}-{issue.IssueNumber}",
                        Title = issue?.Title ?? suggestion.FinalSummary ?? suggestion.OriginalSummary
                    };
                })
                .ToList()
        };
    }

    private static List<string> NormalizeCriteria(List<string>? criteria)
    {
        return criteria?
            .Select(OptionalText)
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .Take(50)
            .ToList() ?? [];
    }

    private static List<string>? DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? OptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static AiBreakdownDto Failure(string errorCode, string message)
    {
        return new AiBreakdownDto
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}
