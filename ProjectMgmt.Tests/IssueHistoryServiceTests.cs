using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-195: dữ liệu cho timeline — tên người, tên trạng thái, phân trang.</summary>
public class IssueHistoryServiceTests
{
    private static readonly DateTime At = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryIssueRepository _issues = new();
    private readonly IssueHistoryService _service;

    public IssueHistoryServiceTests()
    {
        _service = new IssueHistoryService(_issues, new SharedUserLookupService(), new SharedProjectLookupService());
    }

    [Fact]
    public async Task ActivityIsNewestFirstPagedAndResolvesAssigneeNames()
    {
        for (var i = 0; i < 3; i++)
        {
            _issues.Activities.Add(IssueActivities.Create(SharedTestData.StoryIssueId, SharedTestData.OwnerUserId, "Updated", At.AddMinutes(i), "Title", $"v{i}", $"v{i + 1}"));
        }

        _issues.Activities.Add(IssueActivities.Create(SharedTestData.StoryIssueId, SharedTestData.OwnerUserId, "Updated", At.AddHours(1),
            "AssigneeId", SharedTestData.DeveloperUserId, SharedTestData.ViewerUserId));

        var first = await _service.GetActivityAsync(SharedTestData.StoryIssueId, 1, 2);
        Assert.Equal(4, first.Value.TotalCount);
        var newest = first.Value.Items[0];
        Assert.Equal(("Demo Developer", "Demo Viewer", "Demo Owner"), (newest.OldValue, newest.NewValue, newest.ActorName));
        Assert.Equal("v2", first.Value.Items[1].OldValue);
        Assert.Equal(["v1", "v0"], (await _service.GetActivityAsync(SharedTestData.StoryIssueId, 2, 2)).Value.Items.Select(a => a.OldValue));
        Assert.Equal("HISTORY_PAGE_INVALID", (await _service.GetActivityAsync(SharedTestData.StoryIssueId, 1, 500)).Error.Code);
    }

    [Fact]
    public async Task StatusAndAssignmentHistoryCarryDisplayNames()
    {
        _issues.StatusHistory.Add(new IssueStatusHistory { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, FromStatusId = SharedTestData.ToDoStatusId,
            ToStatusId = SharedTestData.InProgressStatusId, FromCategory = "ToDo", ToCategory = "InProgress", ChangedBy = SharedTestData.DeveloperUserId, DurationSeconds = 60, ChangedAt = At });
        _issues.AssignmentHistory.Add(new IssueAssignmentHistory { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, ToAssigneeId = SharedTestData.DeveloperUserId,
            AssignedBy = SharedTestData.OwnerUserId, AssignmentSource = "Manual", AssignedAt = At });

        var status = Assert.Single((await _service.GetStatusHistoryAsync(SharedTestData.StoryIssueId)).Value);
        Assert.Equal(("To Do", "In Progress", "Demo Developer"), (status.FromStatusName, status.ToStatusName, status.ChangedByName));
        var assignment = Assert.Single((await _service.GetAssignmentHistoryAsync(SharedTestData.StoryIssueId)).Value);
        Assert.Equal((null, "Demo Developer", "Demo Owner"), (assignment.FromAssigneeName, assignment.ToAssigneeName, assignment.AssignedByName));
        Assert.Equal("ISSUE_NOT_FOUND", (await _service.GetStatusHistoryAsync(Guid.NewGuid())).Error.Code);
    }
}
