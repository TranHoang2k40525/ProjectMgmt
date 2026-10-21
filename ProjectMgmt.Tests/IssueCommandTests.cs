using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-175: tạo/sửa/xóa mềm Issue qua module C với contract fake.</summary>
public class IssueCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly InMemoryIssueRepository _repository = new();
    private readonly SharedPermissionEvaluator _permissions = new();
    private readonly SharedIssueNumberGenerator _numbers = new();
    private readonly FixedTimeProvider _clock = new(Now.UtcDateTime);
    private readonly IssueTrackingService _tracking;
    private readonly IssueCreationService _creation;

    public IssueCommandTests()
    {
        _tracking = new IssueTrackingService(_repository, _permissions, _clock, new IssueHistoryRecorder(_repository),
            new CreateIssueDtoValidator(), new UpdateIssueDtoValidator(), new IssueListQueryDtoValidator());
        var projects = new SharedProjectLookupService();
        _creation = new IssueCreationService(_repository, projects, _permissions, _numbers,
            new IssueHierarchyLoader(_repository, projects), new CreateIssueDtoValidator(), new IssueHistoryRecorder(_repository), _clock);
    }

    [Fact]
    public async Task CreateAssignsNextNumberInitialStatusRankAndLogsActivity()
    {
        var result = await _creation.CreateAsync(NewTask(), SharedTestData.DeveloperUserId);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Equal(6, result.Value.IssueNumber);
        Assert.Equal(SharedTestData.BacklogStatusId, result.Value.StatusId);
        Assert.Equal(SharedTestData.DeveloperUserId, result.Value.ReporterId);
        Assert.Equal(6, result.Value.RankOrder);
        Assert.Equal("Viết API đăng xuất", result.Value.Title);
        var activity = Assert.Single(_repository.Activities);
        Assert.Equal("Created", activity.Action);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task CreateSubtaskDenormalizesEpicFromParent()
    {
        var request = NewTask();
        request.IssueTypeId = SharedTestData.SubtaskTypeId;
        request.ParentId = SharedTestData.StoryIssueId;

        var result = await _creation.CreateAsync(request, SharedTestData.OwnerUserId);

        Assert.Equal(SharedTestData.StoryIssueId, result.Value.ParentId);
        Assert.Equal(SharedTestData.EpicIssueId, result.Value.EpicId);
    }

    [Fact]
    public async Task CreateRejectsOutsidersWrongTypeAndBadHierarchyWithoutWriting()
    {
        Assert.Equal("ISSUE_REPORTER_NOT_MEMBER",
            (await _creation.CreateAsync(NewTask(), SharedTestData.OutsiderUserId)).Error.Code);

        var outsiderAssignee = NewTask();
        outsiderAssignee.AssigneeId = SharedTestData.OutsiderUserId;
        Assert.Equal("ISSUE_ASSIGNEE_NOT_MEMBER",
            (await _creation.CreateAsync(outsiderAssignee, SharedTestData.OwnerUserId)).Error.Code);

        var foreignType = NewTask();
        foreignType.IssueTypeId = SharedTestData.OtherTaskTypeId;
        Assert.Equal("ISSUE_TYPE_INVALID", (await _creation.CreateAsync(foreignType, SharedTestData.OwnerUserId)).Error.Code);

        var orphanSubtask = NewTask();
        orphanSubtask.IssueTypeId = SharedTestData.SubtaskTypeId;
        Assert.Equal("ISSUE_SUBTASK_PARENT_REQUIRED",
            (await _creation.CreateAsync(orphanSubtask, SharedTestData.OwnerUserId)).Error.Code);

        var unknownProject = NewTask();
        unknownProject.ProjectId = Guid.NewGuid();
        Assert.Equal(ErrorType.NotFound, (await _creation.CreateAsync(unknownProject, SharedTestData.OwnerUserId)).Error.Type);

        Assert.Equal(0, _repository.SaveCount);
        Assert.Empty(_repository.Activities);
    }

    [Fact]
    public async Task UpdateChangesFieldsAndLogsOnlyRealChanges()
    {
        var result = await _tracking.UpdateAsync(SharedTestData.StoryIssueId, new UpdateIssueDto
        {
            Title = "Người dùng có thể đặt lại mật khẩu",
            PriorityId = SharedTestData.MediumPriorityId,
            AssigneeId = SharedTestData.OwnerUserId,
            StoryPoints = 8,
            DueDate = new DateOnly(2026, 10, 31)
        }, SharedTestData.OwnerUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value.StoryPoints);
        Assert.Equal(Now.UtcDateTime, result.Value.UpdatedAt);
        Assert.Equal(["AssigneeId", "StoryPoints", "DueDate"], _repository.Activities.Select(a => a.FieldName));
        var points = _repository.Activities.Single(a => a.FieldName == "StoryPoints");
        Assert.Equal(("5", "8"), (points.OldValue, points.NewValue));
    }

    [Fact]
    public async Task UpdateWithoutChangesDoesNotWrite()
    {
        var story = _repository.Issues.Single(i => i.Id == SharedTestData.StoryIssueId);
        var result = await _tracking.UpdateAsync(story.Id, new UpdateIssueDto
        {
            Title = story.Title,
            PriorityId = story.PriorityId,
            AssigneeId = story.AssigneeId,
            StoryPoints = story.StoryPoints
        }, SharedTestData.OwnerUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task UpdateRejectsNonMemberAssigneeAndMissingIssue()
    {
        var outsider = await _tracking.UpdateAsync(SharedTestData.StoryIssueId,
            new UpdateIssueDto { Title = "x", AssigneeId = SharedTestData.OutsiderUserId }, SharedTestData.OwnerUserId);
        Assert.Equal("ISSUE_ASSIGNEE_NOT_MEMBER", outsider.Error.Code);

        var missing = await _tracking.UpdateAsync(Guid.NewGuid(), new UpdateIssueDto { Title = "x" }, SharedTestData.OwnerUserId);
        Assert.Equal("ISSUE_NOT_FOUND", missing.Error.Code);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task DeleteIsSoftCascadesToDescendantsAndUnlinksEpic()
    {
        var result = await _tracking.DeleteAsync(SharedTestData.EpicIssueId, SharedTestData.OwnerUserId);

        Assert.True(result.IsSuccess);
        var byId = _repository.Issues.ToDictionary(i => i.Id);
        Assert.True(byId[SharedTestData.EpicIssueId].IsDeleted);
        Assert.True(byId[SharedTestData.StoryIssueId].IsDeleted);
        Assert.True(byId[SharedTestData.SubtaskIssueId].IsDeleted);
        Assert.False(byId[SharedTestData.BacklogTaskIssueId].IsDeleted);
        Assert.Null(byId[SharedTestData.StoryIssueId].EpicId);
        Assert.Equal(3, _repository.Activities.Count(a => a.Action == "Deleted"));
        Assert.Contains(_repository.Activities, a => a.Detail != null && a.Detail.Contains(SharedTestData.EpicIssueId.ToString(), StringComparison.Ordinal));

        Assert.Equal("ISSUE_NOT_FOUND", (await _tracking.DeleteAsync(SharedTestData.EpicIssueId, SharedTestData.OwnerUserId)).Error.Code);
    }

    private static CreateIssueDto NewTask() => new()
    {
        ProjectId = SharedTestData.ProjectId,
        IssueTypeId = SharedTestData.TaskTypeId,
        Title = " Viết API đăng xuất ",
        AssigneeId = SharedTestData.DeveloperUserId,
        StoryPoints = 3
    };
}
