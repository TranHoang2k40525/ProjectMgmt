using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace DeliveryIntelligence.Application.Services;

public class IssueHistoryService : IIssueHistoryService
{
    public const int MaxPageSize = 100;
    private readonly IIssueRepository _issues;
    private readonly IUserLookupService _users;
    private readonly IProjectLookupService _projects;

    public IssueHistoryService(IIssueRepository issues, IUserLookupService users, IProjectLookupService projects)
    {
        _issues = issues;
        _users = users;
        _projects = projects;
    }

    public async Task<Result<PagedResultDto<ActivityDto>>> GetActivityAsync(
        Guid issueId,
        int page,
        int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return Result<PagedResultDto<ActivityDto>>.Failure(Error.Validation("HISTORY_PAGE_INVALID", $"Trang từ 1, mỗi trang 1–{MaxPageSize} dòng."));
        }

        if (await _issues.GetByIdAsync(issueId) is null)
        {
            return Result<PagedResultDto<ActivityDto>>.Failure(IssueErrors.NotFound);
        }

        var (items, total) = await _issues.GetActivitiesAsync(issueId, page, pageSize);
        var userIds = items.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value)
            .Concat(items.Where(a => a.FieldName == nameof(Issue.AssigneeId)).SelectMany(a => ParseIds(a.OldValue, a.NewValue)));
        var names = await NamesAsync(userIds);

        return Result.Success(new PagedResultDto<ActivityDto>
        {
            Items = items.Select(activity => new ActivityDto
            {
                Id = activity.Id,
                Action = activity.Action,
                FieldName = activity.FieldName,
                // Người xử lý lưu dạng Id: trả về tên để timeline đọc được.
                OldValue = activity.FieldName == nameof(Issue.AssigneeId) ? NameOf(activity.OldValue, names) : activity.OldValue,
                NewValue = activity.FieldName == nameof(Issue.AssigneeId) ? NameOf(activity.NewValue, names) : activity.NewValue,
                ActorId = activity.UserId,
                ActorName = activity.UserId.HasValue ? names.GetValueOrDefault(activity.UserId.Value, string.Empty) : string.Empty,
                Source = activity.Source,
                CreatedAt = activity.CreatedAt
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<IReadOnlyList<StatusChangeDto>>> GetStatusHistoryAsync(Guid issueId)
    {
        var issue = await _issues.GetByIdAsync(issueId);
        if (issue is null)
        {
            return Result<IReadOnlyList<StatusChangeDto>>.Failure(IssueErrors.NotFound);
        }

        var history = await _issues.GetStatusHistoryAsync(issueId);
        var statuses = (await _projects.GetStatusesAsync(issue.ProjectId)).ToDictionary(s => s.Id, s => s.Name);
        var names = await NamesAsync(history.Where(h => h.ChangedBy.HasValue).Select(h => h.ChangedBy!.Value));
        IReadOnlyList<StatusChangeDto> result = history.Select(h => new StatusChangeDto
        {
            Id = h.Id,
            FromStatusId = h.FromStatusId,
            FromStatusName = h.FromStatusId.HasValue ? statuses.GetValueOrDefault(h.FromStatusId.Value) : null,
            ToStatusId = h.ToStatusId,
            ToStatusName = statuses.GetValueOrDefault(h.ToStatusId, string.Empty),
            FromCategory = h.FromCategory,
            ToCategory = h.ToCategory,
            ChangedBy = h.ChangedBy,
            ChangedByName = h.ChangedBy.HasValue ? names.GetValueOrDefault(h.ChangedBy.Value, string.Empty) : string.Empty,
            DurationSeconds = h.DurationSeconds,
            ChangedAt = h.ChangedAt
        }).ToList();
        return Result.Success(result);
    }

    public async Task<Result<IReadOnlyList<AssignmentChangeDto>>> GetAssignmentHistoryAsync(Guid issueId)
    {
        if (await _issues.GetByIdAsync(issueId) is null)
        {
            return Result<IReadOnlyList<AssignmentChangeDto>>.Failure(IssueErrors.NotFound);
        }

        var history = await _issues.GetAssignmentHistoryAsync(issueId);
        var names = await NamesAsync(history.SelectMany(h => new[] { h.FromAssigneeId, h.ToAssigneeId, h.AssignedBy })
            .Where(id => id.HasValue).Select(id => id!.Value));
        IReadOnlyList<AssignmentChangeDto> result = history.Select(h => new AssignmentChangeDto
        {
            Id = h.Id,
            FromAssigneeId = h.FromAssigneeId,
            FromAssigneeName = h.FromAssigneeId.HasValue ? names.GetValueOrDefault(h.FromAssigneeId.Value) : null,
            ToAssigneeId = h.ToAssigneeId,
            ToAssigneeName = h.ToAssigneeId.HasValue ? names.GetValueOrDefault(h.ToAssigneeId.Value) : null,
            AssignedBy = h.AssignedBy,
            AssignedByName = h.AssignedBy.HasValue ? names.GetValueOrDefault(h.AssignedBy.Value, string.Empty) : string.Empty,
            AssignmentSource = h.AssignmentSource,
            StoryPointsAtTime = h.StoryPointsAtTime,
            Reason = h.Reason,
            AssignedAt = h.AssignedAt
        }).ToList();
        return Result.Success(result);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return (await _users.GetDisplayInfoAsync(ids)).ToDictionary(user => user.UserId, user => user.DisplayName);
    }

    private static IEnumerable<Guid> ParseIds(params string?[] values) =>
        values.Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null).Where(id => id.HasValue).Select(id => id!.Value);

    private static string? NameOf(string? value, Dictionary<Guid, string> names) =>
        Guid.TryParse(value, out var id) && names.TryGetValue(id, out var name) ? name : value;
}
