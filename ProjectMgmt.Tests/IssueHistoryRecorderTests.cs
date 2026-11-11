using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-194: IssueStatusHistory (DurationSeconds) và IssueAssignmentHistory (nguồn, snapshot points).</summary>
public class IssueHistoryRecorderTests
{
    private readonly InMemoryIssueRepository _issues = new();
    private readonly IssueHistoryRecorder _recorder;

    public IssueHistoryRecorderTests()
    {
        _recorder = new IssueHistoryRecorder(_issues);
    }

    private Issue Story => _issues.Issues.Single(i => i.Id == SharedTestData.StoryIssueId);

    [Fact]
    public async Task DurationCountsFromCreationThenFromPreviousChange()
    {
        var created = Story.CreatedAt;
        await _recorder.RecordStatusAsync(Story, SharedTestData.ToDoStatusId, "ToDo", SharedTestData.InProgressStatusId, "InProgress",
            SharedTestData.DeveloperUserId, created.AddHours(2));
        await _recorder.RecordStatusAsync(Story, SharedTestData.InProgressStatusId, "InProgress", SharedTestData.InReviewStatusId, "InProgress",
            SharedTestData.DeveloperUserId, created.AddHours(2).AddMinutes(30));

        Assert.Equal([7200L, 1800L], _issues.StatusHistory.Select(h => h.DurationSeconds!.Value));
        Assert.Equal("InProgress", _issues.StatusHistory[0].ToCategory);
    }

    [Fact]
    public async Task InitialStatusHasNoDuration()
    {
        await _recorder.RecordStatusAsync(Story, null, null, SharedTestData.BacklogStatusId, "ToDo", SharedTestData.OwnerUserId, Story.CreatedAt);
        var history = Assert.Single(_issues.StatusHistory);
        Assert.Null(history.FromStatusId);
        Assert.Null(history.DurationSeconds);
    }

    [Fact]
    public async Task AssignmentSourceSnapshotAndNoOp()
    {
        var at = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        await _recorder.RecordAssignmentAsync(Story, SharedTestData.DeveloperUserId, SharedTestData.OwnerUserId, SharedTestData.ViewerUserId, at);
        await _recorder.RecordAssignmentAsync(Story, SharedTestData.OwnerUserId, SharedTestData.DeveloperUserId, SharedTestData.DeveloperUserId, at);
        await _recorder.RecordAssignmentAsync(Story, SharedTestData.DeveloperUserId, SharedTestData.DeveloperUserId, SharedTestData.OwnerUserId, at);

        Assert.Equal(["Manual", "SelfPick"], _issues.AssignmentHistory.Select(h => h.AssignmentSource));
        Assert.All(_issues.AssignmentHistory, h => Assert.Equal(5, h.StoryPointsAtTime));
    }

    [Fact]
    public async Task AiAssignmentRequiresCandidateReference()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _recorder.RecordAssignmentAsync(
            Story, null, SharedTestData.DeveloperUserId, SharedTestData.OwnerUserId, DateTime.UtcNow, IssueHistoryRecorder.AiSuggested));

        var candidate = Guid.NewGuid();
        await _recorder.RecordAssignmentAsync(Story, null, SharedTestData.DeveloperUserId, SharedTestData.OwnerUserId, DateTime.UtcNow,
            IssueHistoryRecorder.AiSuggested, candidate, "Điểm phù hợp cao nhất");
        Assert.Equal(candidate, Assert.Single(_issues.AssignmentHistory).AiCandidateId);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _recorder.RecordAssignmentAsync(
            Story, null, SharedTestData.OwnerUserId, SharedTestData.OwnerUserId, DateTime.UtcNow, "Robot"));
    }

    [Fact]
    public async Task UpdatingAssigneeThroughServiceWritesHistory()
    {
        var service = new IssueTrackingService(_issues, new SharedPermissionEvaluator(), TimeProvider.System, _recorder,
            new CreateIssueDtoValidator(), new UpdateIssueDtoValidator(), new IssueListQueryDtoValidator());

        await service.UpdateAsync(SharedTestData.StoryIssueId,
            new UpdateIssueDto { Title = Story.Title, PriorityId = Story.PriorityId, StoryPoints = Story.StoryPoints, AssigneeId = SharedTestData.OwnerUserId },
            SharedTestData.OwnerUserId);

        var history = Assert.Single(_issues.AssignmentHistory);
        Assert.Equal((SharedTestData.DeveloperUserId, SharedTestData.OwnerUserId, "SelfPick"),
            (history.FromAssigneeId!.Value, history.ToAssigneeId!.Value, history.AssignmentSource));
    }
}
