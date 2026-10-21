namespace DeliveryIntelligence.Application.Dto;

public class CreateIssueDto
{
    public Guid ProjectId { get; set; }
    public Guid IssueTypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public Guid? SprintId { get; set; }
    public decimal? StoryPoints { get; set; }
    public int? OriginalEstimateMinutes { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? RankOrder { get; set; }
}

public class UpdateIssueDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? AssigneeId { get; set; }
    public decimal? StoryPoints { get; set; }
    public int? OriginalEstimateMinutes { get; set; }
    public DateOnly? DueDate { get; set; }
}

public class IssueDetailDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int IssueNumber { get; set; }
    public string? IssueKey { get; set; }
    public Guid IssueTypeId { get; set; }
    public Guid StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid ReporterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? StoryPoints { get; set; }
    public int? OriginalEstimateMinutes { get; set; }
    public int TimeSpentMinutes { get; set; }
    public decimal RankOrder { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsAiGenerated { get; set; }
    public Guid? AiGenerationLogId { get; set; }
    public bool IsAiAssigned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class IssueListItemDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int IssueNumber { get; set; }
    public string? IssueKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid IssueTypeId { get; set; }
    public Guid StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public decimal? StoryPoints { get; set; }
    public decimal RankOrder { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class IssueListQueryDto
{
    public Guid? SprintId { get; set; }
    public bool BacklogOnly { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? IssueTypeId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public string? SearchText { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ChangeIssueParentDto
{
    /// <summary>null = gỡ khỏi cha (không áp dụng cho Sub-task).</summary>
    public Guid? ParentId { get; set; }
}

public class RankIssueDto
{
    /// <summary>Issue đứng ngay trước vị trí mới (null = đầu danh sách).</summary>
    public Guid? PreviousIssueId { get; set; }

    /// <summary>Issue đứng ngay sau vị trí mới (null = cuối danh sách).</summary>
    public Guid? NextIssueId { get; set; }
}

public class IssueRankDto
{
    public Guid IssueId { get; set; }
    public decimal RankOrder { get; set; }
    public bool Rebalanced { get; set; }
}

public class TransitionIssueDto
{
    public Guid TargetStatusId { get; set; }

    /// <summary>Trạng thái FE đang thấy; khác trạng thái hiện tại thì trả 409 để tải lại thay vì ghi đè.</summary>
    public Guid? ExpectedStatusId { get; set; }

    /// <summary>Bình luận kèm theo lần chuyển (tùy chọn).</summary>
    public string? Comment { get; set; }
}

public class IssueTransitionResultDto
{
    public Guid IssueId { get; set; }
    public Guid PreviousStatusId { get; set; }
    public Guid NewStatusId { get; set; }
    public bool Changed { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
