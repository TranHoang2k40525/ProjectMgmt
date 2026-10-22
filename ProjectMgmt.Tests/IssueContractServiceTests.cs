using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-177: contract IIssueReadService/IIssueSprintService bản thật trên repository của C.</summary>
public class IssueContractServiceTests
{
    private readonly InMemoryIssueRepository _repository = new();
    private readonly IssueContractService _service;

    public IssueContractServiceTests()
    {
        _service = new IssueContractService(_repository, new FixedTimeProvider(new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task ReadsSprintAndOpenWorkAsDtos()
    {
        var sprint = await _service.GetBySprintAsync(SharedTestData.ActiveSprintId);
        Assert.Equal([2, 3, 5], sprint.Select(i => i.IssueNumber));

        _repository.Issues.Single(i => i.Id == SharedTestData.SubtaskIssueId).ResolvedAt = DateTime.UtcNow;
        var open = await _service.GetOpenByAssigneeAsync(SharedTestData.DeveloperUserId);
        Assert.Equal(SharedTestData.StoryIssueId, Assert.Single(open).Id);

        Assert.Null(await _service.GetByIdAsync(Guid.NewGuid()));
        Assert.Equal("Tạo endpoint gửi OTP", (await _service.GetByIdAsync(SharedTestData.SubtaskIssueId))?.Title);
    }

    [Fact]
    public async Task ReadsHistoryInChronologicalOrder()
    {
        var at = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
        _repository.StatusHistory.Add(new IssueStatusHistory { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, ToStatusId = SharedTestData.InProgressStatusId, ToCategory = "InProgress", ChangedAt = at.AddHours(1) });
        _repository.StatusHistory.Add(new IssueStatusHistory { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, ToStatusId = SharedTestData.ToDoStatusId, ToCategory = "ToDo", ChangedAt = at });
        _repository.AssignmentHistory.Add(new IssueAssignmentHistory { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, ToAssigneeId = SharedTestData.DeveloperUserId, AssignmentSource = "Manual", AssignedAt = at });

        var statuses = await _service.GetStatusHistoryAsync(SharedTestData.StoryIssueId);
        Assert.Equal(["ToDo", "InProgress"], statuses.Select(s => s.ToCategory));
        Assert.Equal("Manual", Assert.Single(await _service.GetAssignmentHistoryAsync(SharedTestData.StoryIssueId)).AssignmentSource);
    }

    [Fact]
    public async Task MoveToSprintCarriesSubtasksAndLogsOnlyChanges()
    {
        var sprintId = Guid.NewGuid();
        var moved = await _service.MoveToSprintAsync(SharedTestData.ProjectId,
            [SharedTestData.StoryIssueId, SharedTestData.BacklogTaskIssueId], sprintId, SharedTestData.OwnerUserId);

        Assert.Equal(3, moved.Value);
        Assert.All(_repository.Issues.Where(i => i.Id == SharedTestData.StoryIssueId || i.Id == SharedTestData.SubtaskIssueId || i.Id == SharedTestData.BacklogTaskIssueId),
            issue => Assert.Equal(sprintId, issue.SprintId));
        Assert.Equal(3, _repository.Activities.Count(a => a.Action == "SprintChanged"));

        var again = await _service.MoveToSprintAsync(SharedTestData.ProjectId, [SharedTestData.StoryIssueId], sprintId, SharedTestData.OwnerUserId);
        Assert.Equal(0, again.Value);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task ReturnToBacklogClearsSprint()
    {
        var result = await _service.ReturnToBacklogAsync(SharedTestData.ProjectId, [SharedTestData.BugIssueId], SharedTestData.OwnerUserId);
        Assert.Equal(1, result.Value);
        Assert.Null(_repository.Issues.Single(i => i.Id == SharedTestData.BugIssueId).SprintId);
    }

    [Fact]
    public async Task SprintMovesRejectForeignEmptyOrUnknownIssues()
    {
        Assert.Equal("ISSUE_NOT_IN_PROJECT", (await _service.MoveToSprintAsync(SharedTestData.ProjectId,
            [SharedTestData.OtherProjectIssueId], Guid.NewGuid(), SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_NOT_IN_PROJECT", (await _service.ReturnToBacklogAsync(SharedTestData.ProjectId,
            [Guid.NewGuid()], SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_BATCH_SIZE_INVALID", (await _service.ReturnToBacklogAsync(SharedTestData.ProjectId,
            [], SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_SPRINT_INVALID", (await _service.MoveToSprintAsync(SharedTestData.ProjectId,
            [SharedTestData.StoryIssueId], Guid.Empty, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal(0, _repository.SaveCount);
    }
}
