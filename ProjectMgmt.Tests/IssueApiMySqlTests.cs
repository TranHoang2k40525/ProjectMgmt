using System.Net;
using System.Net.Http.Json;
using DeliveryIntelligence.Application.Dto;
using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>Database riêng cho test API vì các test này ghi dữ liệu (không dùng chung collection seed).</summary>
public class DeliveryApiFixture : IAsyncLifetime
{
    public MySqlTestDatabase Database { get; } = new();
    public DeliveryApiFactory? Factory { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        if (MySqlTestDatabase.IsConfigured)
        {
            Factory = new DeliveryApiFactory(Database);
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

/// <summary>
/// Các test dùng chung một database tạm và có ghi dữ liệu, nên mỗi test chỉ kiểm phần dữ liệu không bị test khác thay đổi.
/// Kiểm API Issue của C qua Host thật: xác thực, RBAC theo Project, MySQL (SCRUMAI-175/176/177).
/// </summary>
public class IssueApiMySqlTests(DeliveryApiFixture fixture) : IClassFixture<DeliveryApiFixture>
{
    private static readonly string DemoIssues = $"/api/v1/projects/{SharedTestData.ProjectId}/issues";

    private HttpClient As(Guid? userId) => fixture.Factory!.CreateClientFor(userId);

    [MySqlFact]
    public async Task ListRequiresAuthenticationAndProjectMembership()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await As(null).GetAsync(DemoIssues)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.OutsiderUserId).GetAsync(DemoIssues)).StatusCode);

        var page = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<PagedResultDto<IssueListItemDto>>(
            $"{DemoIssues}?backlogOnly=true&pageSize=1");
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(SharedTestData.EpicIssueId, Assert.Single(page.Items).Id);
    }

    [MySqlFact]
    public async Task InvalidQueryReturnsProblemDetailsWithCode()
    {
        var response = await As(SharedTestData.OwnerUserId).GetAsync($"{DemoIssues}?pageSize=500");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ISSUE_PAGE_SIZE_INVALID", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [MySqlFact]
    public async Task PatchUpdatesIssueForEditorsOnly()
    {
        var url = $"/api/v1/issues/{SharedTestData.BacklogTaskIssueId}";
        var request = new UpdateIssueDto { Title = "Tài liệu đăng nhập (đã sửa)", StoryPoints = 2, DueDate = new DateOnly(2026, 11, 15) };

        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.ViewerUserId).PatchAsJsonAsync(url, request)).StatusCode);

        var response = await As(SharedTestData.DeveloperUserId).PatchAsJsonAsync(url, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var core = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<IssueDetailDto>($"{url}/core");
        Assert.Equal("Tài liệu đăng nhập (đã sửa)", core!.Title);
        Assert.Equal(new DateOnly(2026, 11, 15), core.DueDate);

        await using var delivery = fixture.Database.CreateDeliveryContext();
        Assert.True(await delivery.ActivityLogs.AnyAsync(a => a.IssueId == SharedTestData.BacklogTaskIssueId && a.FieldName == "DueDate"));
    }

    [MySqlFact]
    public async Task RankAndParentEndpointsApplyRulesAndPermissions()
    {
        var rank = new RankIssueDto { PreviousIssueId = SharedTestData.EpicIssueId, NextIssueId = SharedTestData.StoryIssueId };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(SharedTestData.DeveloperUserId).PutAsJsonAsync($"/api/v1/issues/{SharedTestData.BugIssueId}/rank", rank)).StatusCode);

        var ranked = await As(SharedTestData.OwnerUserId).PutAsJsonAsync($"/api/v1/issues/{SharedTestData.BugIssueId}/rank", rank);
        Assert.Equal(1.5m, (await ranked.Content.ReadFromJsonAsync<IssueRankDto>())!.RankOrder);

        var badParent = await As(SharedTestData.OwnerUserId).PutAsJsonAsync(
            $"/api/v1/issues/{SharedTestData.EpicIssueId}/parent", new ChangeIssueParentDto { ParentId = SharedTestData.StoryIssueId });
        Assert.Equal(HttpStatusCode.BadRequest, badParent.StatusCode);
        Assert.Contains("ISSUE_EPIC_PARENT_NOT_ALLOWED", await badParent.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [MySqlFact]
    public async Task DeleteThroughExistingRouteIsSoftAndCascades()
    {
        var other = fixture.Database.CreateDeliveryContext();
        await using (other)
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await As(SharedTestData.DeveloperUserId).DeleteAsync($"/api/v1/issues/{SharedTestData.StoryIssueId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,
                (await As(SharedTestData.OwnerUserId).DeleteAsync($"/api/v1/issues/{SharedTestData.StoryIssueId}")).StatusCode);

            var rows = await other.Issues.AsNoTracking()
                .Where(i => i.Id == SharedTestData.StoryIssueId || i.Id == SharedTestData.SubtaskIssueId)
                .ToListAsync();
            Assert.All(rows, row => Assert.True(row.IsDeleted));
            Assert.Equal(HttpStatusCode.NotFound,
                (await As(SharedTestData.OwnerUserId).GetAsync($"/api/v1/issues/{SharedTestData.StoryIssueId}/core")).StatusCode);
        }
    }
}
