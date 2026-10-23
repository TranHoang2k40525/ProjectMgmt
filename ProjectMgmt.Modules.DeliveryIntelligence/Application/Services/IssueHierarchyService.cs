using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.Services;

public class IssueHierarchyService : IIssueHierarchyService
{
    private readonly IIssueRepository _issues;
    private readonly IssueHierarchyLoader _hierarchy;
    private readonly TimeProvider _clock;

    public IssueHierarchyService(IIssueRepository issues, IssueHierarchyLoader hierarchy, TimeProvider clock)
    {
        _issues = issues;
        _hierarchy = hierarchy;
        _clock = clock;
    }

    public async Task<Result<IssueDetailDto>> ChangeParentAsync(
        Guid issueId,
        ChangeIssueParentDto request,
        Guid actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issue = await _issues.GetByIdAsync(issueId);
        if (issue is null)
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.NotFound);
        }

        var types = await _hierarchy.GetTypesAsync(issue.ProjectId);
        if (!types.TryGetValue(issue.IssueTypeId, out var type))
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.TypeInvalid);
        }

        // Gỡ cha: nếu cha cũ chính là Epic thì gỡ luôn Epic; nếu chỉ có liên kết EpicId (không có cha) thì giữ nguyên.
        var keepEpic = request.ParentId is null && issue.ParentId is null ? issue.EpicId : null;
        var placement = await _hierarchy.PlaceAsync(issue.Id, issue.ProjectId, type, request.ParentId, keepEpic, types);
        if (placement.Violation is not null)
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.FromRule(placement.Violation));
        }

        if (placement.ParentId == issue.ParentId && placement.EpicId == issue.EpicId)
        {
            return Result.Success(IssueMapper.ToDetail(issue));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        await _issues.AddActivityAsync(
            IssueActivities.Create(issue.Id, actorUserId, IssueActivities.ParentChanged, now,
                nameof(Issue.ParentId), issue.ParentId, placement.ParentId));
        issue.ParentId = placement.ParentId;
        issue.EpicId = placement.EpicId;
        issue.UpdatedAt = now;
        await PropagateEpicAsync(issue, now);
        await _issues.SaveChangesAsync();
        return Result.Success(IssueMapper.ToDetail(issue));
    }

    public async Task<Result<IssueRankDto>> RankAsync(
        Guid issueId,
        RankIssueDto request,
        Guid actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PreviousIssueId is null && request.NextIssueId is null)
        {
            return Result<IssueRankDto>.Failure(Error.Validation("ISSUE_RANK_NEIGHBOR_REQUIRED", "Cần Issue đứng trước hoặc sau."));
        }

        if (request.PreviousIssueId == issueId || request.NextIssueId == issueId || request.PreviousIssueId == request.NextIssueId)
        {
            return Result<IssueRankDto>.Failure(Error.Validation("ISSUE_RANK_NEIGHBOR_INVALID", "Issue kề không hợp lệ."));
        }

        var issue = await _issues.GetByIdAsync(issueId);
        if (issue is null)
        {
            return Result<IssueRankDto>.Failure(IssueErrors.NotFound);
        }

        var previous = await NeighborAsync(request.PreviousIssueId, issue.ProjectId);
        var next = await NeighborAsync(request.NextIssueId, issue.ProjectId);
        if ((request.PreviousIssueId.HasValue && previous is null) || (request.NextIssueId.HasValue && next is null))
        {
            return Result<IssueRankDto>.Failure(Error.Validation("ISSUE_RANK_NEIGHBOR_INVALID", "Issue kề không thuộc cùng Project."));
        }

        if (previous is not null && next is not null && previous.RankOrder >= next.RankOrder)
        {
            return Result<IssueRankDto>.Failure(Error.Conflict("ISSUE_RANK_ORDER_STALE", "Thứ tự đã thay đổi, hãy tải lại danh sách."));
        }

        var rebalanced = false;
        var rank = IssueRanking.Between(previous?.RankOrder, next?.RankOrder);
        if (rank is null)
        {
            await RebalanceTrackedAsync(issue.ProjectId);
            rebalanced = true;
            rank = IssueRanking.Between(previous?.RankOrder, next?.RankOrder);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        await _issues.AddActivityAsync(
            IssueActivities.Create(issue.Id, actorUserId, IssueActivities.Ranked, now,
                nameof(Issue.RankOrder), issue.RankOrder, rank));
        issue.RankOrder = rank!.Value;
        issue.UpdatedAt = now;
        await _issues.SaveChangesAsync();
        return Result.Success(new IssueRankDto { IssueId = issue.Id, RankOrder = issue.RankOrder, Rebalanced = rebalanced });
    }

    public async Task<Result<int>> RebalanceAsync(Guid projectId)
    {
        var count = await RebalanceTrackedAsync(projectId);
        await _issues.SaveChangesAsync();
        return Result.Success(count);
    }

    private async Task<int> RebalanceTrackedAsync(Guid projectId)
    {
        var ranked = await _issues.GetRankedAsync(projectId);
        for (var position = 0; position < ranked.Count; position++)
        {
            ranked[position].RankOrder = IssueRanking.Rebalanced(position);
        }

        return ranked.Count;
    }

    private async Task<Issue?> NeighborAsync(Guid? issueId, Guid projectId)
    {
        if (!issueId.HasValue)
        {
            return null;
        }

        var neighbor = await _issues.GetByIdAsync(issueId.Value);
        return neighbor?.ProjectId == projectId ? neighbor : null;
    }

    /// <summary>Con cháu kế thừa EpicId của Issue vừa đổi cha.</summary>
    private async Task PropagateEpicAsync(Issue root, DateTime now)
    {
        var pending = new Queue<Issue>([root]);
        var visited = new HashSet<Guid> { root.Id };
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var child in await _issues.GetChildrenAsync(current.Id))
            {
                if (!visited.Add(child.Id))
                {
                    continue;
                }

                if (child.EpicId != root.EpicId)
                {
                    child.EpicId = root.EpicId;
                    child.UpdatedAt = now;
                }

                pending.Enqueue(child);
            }
        }
    }

    public async Task<Result<IReadOnlyList<IssueListItemDto>>> GetSubtasksAsync(
        Guid parentIssueId)
    {
        var parent = await _issues.GetByIdAsync(parentIssueId);
        if (parent is null)
        {
            return Result<IReadOnlyList<IssueListItemDto>>.Failure(IssueErrors.NotFound);
        }

        var children = await _issues.GetChildrenAsync(parentIssueId);
        var dtos = children.Select(c => IssueMapper.ToListItem(c)).ToList();
        return Result.Success<IReadOnlyList<IssueListItemDto>>(dtos);
    }

    public async Task<Result<IReadOnlyList<IssueListItemDto>>> GetEpicChildrenAsync(
        Guid epicId)
    {
        var epic = await _issues.GetByIdAsync(epicId);
        if (epic is null)
        {
            return Result<IReadOnlyList<IssueListItemDto>>.Failure(IssueErrors.NotFound);
        }

        var byEpic = await _issues.GetByEpicAsync(epicId);
        var byParent = await _issues.GetChildrenAsync(epicId);
        var dtos = byEpic.Concat(byParent)
            .Where(i => !i.IsDeleted)
            .DistinctBy(i => i.Id)
            .OrderBy(i => i.RankOrder)
            .ThenBy(i => i.IssueNumber)
            .Select(i => IssueMapper.ToListItem(i))
            .ToList();

        return Result.Success<IReadOnlyList<IssueListItemDto>>(dtos);
    }
}
