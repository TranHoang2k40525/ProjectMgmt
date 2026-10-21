using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-193: chuyển trạng thái theo workflow, mốc StartedAt/ResolvedAt, ActivityLog, thông báo sau commit.</summary>
public class IssueTransitionServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryIssueRepository _issues = new();
    private readonly InMemoryCollaborationRepository _collaboration = new();
    private readonly RecordingIssueNotifier _notifier = new();
    private readonly IssueTransitionService _service;

    public IssueTransitionServiceTests()
    {
        _service = new IssueTransitionService(_issues, _collaboration, new SharedProjectLookupService(),
            new SharedWorkflowValidationService(), _notifier, new FixedTimeProvider(Now));
    }

    private Issue Subtask => _issues.Issues.Single(i => i.Id == SharedTestData.SubtaskIssueId);

    private Task<Result<IssueTransitionResultDto>> Move(Guid issueId, Guid to, Guid? expected = null, string? comment = null) =>
        _service.TransitionAsync(issueId, new TransitionIssueDto { TargetStatusId = to, ExpectedStatusId = expected, Comment = comment }, SharedTestData.OwnerUserId);

    [Fact]
    public async Task StartingWorkSetsStartedAtLogsNamesAndNotifiesOthers()
    {
        _collaboration.Watchers.Add(new IssueWatcher { IssueId = SharedTestData.SubtaskIssueId, UserId = SharedTestData.ViewerUserId });

        var result = await Move(SharedTestData.SubtaskIssueId, SharedTestData.InProgressStatusId, SharedTestData.ToDoStatusId, "Bắt đầu làm");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Changed);
        Assert.Equal(Now, Subtask.StartedAt);
        Assert.Null(Subtask.ResolvedAt);
        var activity = Assert.Single(_issues.Activities);
        Assert.Equal(("StatusChanged", "To Do", "In Progress"), (activity.Action, activity.OldValue, activity.NewValue));
        Assert.Equal("Bắt đầu làm", Assert.Single(_collaboration.Comments).Content);
        var sent = Assert.Single(_notifier.Sent);
        Assert.Equal(new[] { SharedTestData.DeveloperUserId, SharedTestData.ViewerUserId }.OrderBy(x => x), sent.RecipientUserIds.OrderBy(x => x));
        Assert.Equal(1, _issues.TransactionCount);
    }

    [Fact]
    public async Task DoneSetsResolvedAtAndReopenClearsItButKeepsStartedAt()
    {
        var started = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);
        var story = _issues.Issues.Single(i => i.Id == SharedTestData.StoryIssueId);
        story.StartedAt = started;

        await Move(story.Id, SharedTestData.InReviewStatusId);
        await Move(story.Id, SharedTestData.DoneStatusId);
        Assert.Equal(Now, story.ResolvedAt);

        await Move(story.Id, SharedTestData.InProgressStatusId);
        Assert.Null(story.ResolvedAt);
        Assert.Equal(started, story.StartedAt);
    }

    [Fact]
    public async Task DisallowedTransitionIsConflictWithReasonAndWritesNothing()
    {
        var result = await Move(SharedTestData.BacklogTaskIssueId, SharedTestData.DoneStatusId);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("WORKFLOW_TRANSITION_NOT_ALLOWED", result.Error.Code);
        Assert.Contains("Backlog", result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, _issues.SaveCount);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task StaleExpectedStatusIsRejected()
    {
        var result = await Move(SharedTestData.SubtaskIssueId, SharedTestData.InProgressStatusId, expected: SharedTestData.BacklogStatusId);
        Assert.Equal("ISSUE_STATUS_STALE", result.Error.Code);
        Assert.Equal(SharedTestData.ToDoStatusId, Subtask.StatusId);
    }

    [Fact]
    public async Task SameStatusIsNoOpWithoutSideEffects()
    {
        var result = await Move(SharedTestData.SubtaskIssueId, SharedTestData.ToDoStatusId);
        Assert.False(result.Value.Changed);
        Assert.Equal(0, _issues.SaveCount);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task ForeignOrEmptyTargetIsValidationError()
    {
        Assert.Equal("ISSUE_STATUS_NOT_IN_PROJECT", (await Move(SharedTestData.SubtaskIssueId, SharedTestData.OtherInitialStatusId)).Error.Code);
        Assert.Equal("ISSUE_TRANSITION_TARGET_REQUIRED", (await Move(SharedTestData.SubtaskIssueId, Guid.Empty)).Error.Code);
        Assert.Equal("ISSUE_NOT_FOUND", (await Move(Guid.NewGuid(), SharedTestData.DoneStatusId)).Error.Code);
    }
}
