using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;

namespace DeliveryIntelligence.Application.Services;

public static class IssueMapper
{
    public static IssueDetailDto ToDetail(Issue issue, string? projectKey = null) => new()
    {
        Id = issue.Id,
        ProjectId = issue.ProjectId,
        IssueNumber = issue.IssueNumber,
        IssueKey = KeyOf(projectKey, issue.IssueNumber),
        IssueTypeId = issue.IssueTypeId,
        StatusId = issue.StatusId,
        PriorityId = issue.PriorityId,
        SprintId = issue.SprintId,
        ParentId = issue.ParentId,
        EpicId = issue.EpicId,
        AssigneeId = issue.AssigneeId,
        ReporterId = issue.ReporterId,
        Title = issue.Title,
        Description = issue.Description,
        StoryPoints = issue.StoryPoints,
        OriginalEstimateMinutes = issue.OriginalEstimateMinutes,
        TimeSpentMinutes = issue.TimeSpentMinutes,
        RankOrder = issue.RankOrder,
        DueDate = issue.DueDate,
        StartedAt = issue.StartedAt,
        ResolvedAt = issue.ResolvedAt,
        IsAiGenerated = issue.IsAiGenerated,
        AiGenerationLogId = issue.AiGenerationLogId,
        IsAiAssigned = issue.IsAiAssigned,
        CreatedAt = issue.CreatedAt,
        UpdatedAt = issue.UpdatedAt
    };

    public static IssueListItemDto ToListItem(Issue issue, string? projectKey = null) => new()
    {
        Id = issue.Id,
        ProjectId = issue.ProjectId,
        IssueNumber = issue.IssueNumber,
        IssueKey = KeyOf(projectKey, issue.IssueNumber),
        Title = issue.Title,
        IssueTypeId = issue.IssueTypeId,
        StatusId = issue.StatusId,
        PriorityId = issue.PriorityId,
        AssigneeId = issue.AssigneeId,
        SprintId = issue.SprintId,
        ParentId = issue.ParentId,
        EpicId = issue.EpicId,
        StoryPoints = issue.StoryPoints,
        RankOrder = issue.RankOrder,
        DueDate = issue.DueDate,
        UpdatedAt = issue.UpdatedAt
    };

    /// <summary>Tạo entity từ request; IssueNumber, StatusId, ReporterId và RankOrder do use case tạo Issue gán.</summary>
    public static Issue ToNewEntity(CreateIssueDto request, Guid issueId, DateTime createdAtUtc) => new()
    {
        Id = issueId,
        ProjectId = request.ProjectId,
        IssueTypeId = request.IssueTypeId,
        Title = request.Title.Trim(),
        Description = request.Description,
        PriorityId = request.PriorityId,
        AssigneeId = request.AssigneeId,
        ParentId = request.ParentId,
        EpicId = request.EpicId,
        SprintId = request.SprintId,
        StoryPoints = request.StoryPoints,
        OriginalEstimateMinutes = request.OriginalEstimateMinutes,
        DueDate = request.DueDate,
        CreatedAt = createdAtUtc
    };

    public static void ApplyUpdate(Issue issue, UpdateIssueDto request, DateTime updatedAtUtc)
    {
        issue.Title = request.Title.Trim();
        issue.Description = request.Description;
        issue.PriorityId = request.PriorityId;
        issue.AssigneeId = request.AssigneeId;
        issue.StoryPoints = request.StoryPoints;
        issue.OriginalEstimateMinutes = request.OriginalEstimateMinutes;
        issue.DueDate = request.DueDate;
        issue.UpdatedAt = updatedAtUtc;
    }

    public static IssueListFilter ToFilter(Guid projectId, IssueListQueryDto query) => new()
    {
        ProjectId = projectId,
        SprintId = query.SprintId,
        BacklogOnly = query.BacklogOnly,
        StatusId = query.StatusId,
        AssigneeId = query.AssigneeId,
        IssueTypeId = query.IssueTypeId,
        ParentId = query.ParentId,
        EpicId = query.EpicId,
        SearchText = query.SearchText,
        Page = query.Page,
        PageSize = query.PageSize
    };

    private static string? KeyOf(string? projectKey, int issueNumber) =>
        string.IsNullOrWhiteSpace(projectKey) || issueNumber <= 0 ? null : IssueKey.Format(projectKey, issueNumber);
}
