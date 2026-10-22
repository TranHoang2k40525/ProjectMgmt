using System.Net;
using System.Net.Http.Json;
using DeliveryIntelligence.Application.Dto;
using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>Host thật + MySQL tạm + fake contract của B chưa có (IWorkflowValidationService, IProjectReleaseLookupService).</summary>
public class TransitionApiFixture : IAsyncLifetime
{
    public MySqlTestDatabase Database { get; } = new();
    public DeliveryApiFactory? Factory { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        if (MySqlTestDatabase.IsConfigured)
        {
            Factory = new DeliveryApiFactory(Database) { RegisterWorkflowFake = true, RegisterReleaseFake = true };
        }
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await Database.DisposeAsync();
    }
}

/// <summary>SCRUMAI-193: endpoint transition, quyền, khóa dòng khi hai request đồng thời.</summary>
public class IssueTransitionApiMySqlTests(TransitionApiFixture fixture) : IClassFixture<TransitionApiFixture>
{
    private HttpClient As(Guid userId) => fixture.Factory!.CreateClientFor(userId);

    private static string Url(Guid issueId) => $"/api/v1/issues/{issueId}/transition";

    [MySqlFact]
    public async Task ViewerCannotTransitionAndDeveloperCan()
    {
        var request = new TransitionIssueDto { TargetStatusId = SharedTestData.ToDoStatusId };
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.ViewerUserId).PostAsJsonAsync(Url(SharedTestData.BacklogTaskIssueId), request)).StatusCode);

        var response = await As(SharedTestData.DeveloperUserId).PostAsJsonAsync(Url(SharedTestData.BacklogTaskIssueId), request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<IssueTransitionResultDto>())!.Changed);

        var notAllowed = await As(SharedTestData.DeveloperUserId).PostAsJsonAsync(Url(SharedTestData.BacklogTaskIssueId),
            new TransitionIssueDto { TargetStatusId = SharedTestData.DoneStatusId });
        Assert.Equal(HttpStatusCode.Conflict, notAllowed.StatusCode);
    }

    [MySqlFact]
    public async Task ConcurrentTransitionsFromSameStatusOnlyOneWins()
    {
        // DEMO-3 đang To Do; hai người cùng bấm "Start Work" với cùng trạng thái mong đợi.
        var request = new TransitionIssueDto { TargetStatusId = SharedTestData.InProgressStatusId, ExpectedStatusId = SharedTestData.ToDoStatusId };
        var responses = await Task.WhenAll(
            As(SharedTestData.OwnerUserId).PostAsJsonAsync(Url(SharedTestData.SubtaskIssueId), request),
            As(SharedTestData.DeveloperUserId).PostAsJsonAsync(Url(SharedTestData.SubtaskIssueId), request));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

        await using var delivery = fixture.Database.CreateDeliveryContext();
        var issue = await delivery.Issues.AsNoTracking().SingleAsync(i => i.Id == SharedTestData.SubtaskIssueId);
        Assert.Equal(SharedTestData.InProgressStatusId, issue.StatusId);
        Assert.NotNull(issue.StartedAt);
        Assert.Equal(1, await delivery.ActivityLogs.CountAsync(a => a.IssueId == SharedTestData.SubtaskIssueId && a.Action == "StatusChanged"));
    }
}
