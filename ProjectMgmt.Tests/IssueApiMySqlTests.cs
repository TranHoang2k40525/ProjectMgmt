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
    public async Task CommentsThroughOldAndNewRoutesShareOneStore()
    {
        var epic = SharedTestData.EpicIssueId;
        var posted = await As(SharedTestData.DeveloperUserId).PostAsJsonAsync($"/api/v1/issues/{epic}/comments",
            new { content = "Nhờ @owner xem", mentionedUserIds = new[] { SharedTestData.OwnerUserId, SharedTestData.OutsiderUserId } });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(SharedTestData.ViewerUserId).PostAsJsonAsync($"/api/v1/issues/{epic}/comments", new { content = "x" })).StatusCode);

        var comments = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<List<CommentDto>>($"/api/v1/issues/{epic}/comments");
        var root = Assert.Single(comments!);
        Assert.Equal([SharedTestData.OwnerUserId], root.MentionedUserIds);
        Assert.Equal("Demo Developer", root.AuthorName);

        var reply = await As(SharedTestData.OwnerUserId).PostAsJsonAsync($"/api/v1/comments/{root.Id}/replies", new CreateCommentDto { Content = "Đã xem" });
        Assert.Equal(root.Id, (await reply.Content.ReadFromJsonAsync<CommentDto>())!.ParentCommentId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(SharedTestData.OwnerUserId).PutAsJsonAsync($"/api/v1/comments/{root.Id}", new UpdateCommentDto { Content = "sửa" })).StatusCode);

        var watchers = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<List<WatcherDto>>($"/api/v1/issues/{epic}/watchers");
        Assert.Contains(watchers!, w => w.UserId == SharedTestData.DeveloperUserId);
    }

    [MySqlFact]
    public async Task AttachmentUploadDownloadAndWatchFlow()
    {
        var epic = SharedTestData.EpicIssueId;
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("hello"u8.ToArray());
        form.Add(file, "file", "ghi-chu.txt");
        var upload = await As(SharedTestData.DeveloperUserId).PostAsync($"/api/v1/issues/{epic}/attachments", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var listed = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<List<AttachmentDto>>($"/api/v1/issues/{epic}/attachments");
        var attachment = Assert.Single(listed!, a => a.FileName == "ghi-chu.txt");
        Assert.Equal("text/plain", attachment.ContentType);

        Assert.Equal("hello", await As(SharedTestData.ViewerUserId).GetStringAsync($"/api/v1/attachments/{attachment.Id}/download"));
        Assert.Equal("hello", await As(SharedTestData.ViewerUserId).GetStringAsync($"/api/v1/attachments/{attachment.Id}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.OutsiderUserId).GetAsync($"/api/v1/attachments/{attachment.Id}/download")).StatusCode);

        using var bad = new MultipartFormDataContent { { new ByteArrayContent([1]), "file", "tool.exe" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await As(SharedTestData.OwnerUserId).PostAsync($"/api/v1/issues/{epic}/attachments", bad)).StatusCode);

        var watch = await As(SharedTestData.ViewerUserId).PostAsync($"/api/v1/issues/{epic}/watchers/me", null);
        Assert.True((await watch.Content.ReadFromJsonAsync<WatchStateDto>())!.IsWatching);
        var unwatch = await As(SharedTestData.ViewerUserId).DeleteAsync($"/api/v1/issues/{epic}/watchers/me");
        Assert.False((await unwatch.Content.ReadFromJsonAsync<WatchStateDto>())!.IsWatching);

        Assert.Equal(HttpStatusCode.NoContent, (await As(SharedTestData.OwnerUserId).DeleteAsync($"/api/v1/attachments/{attachment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(SharedTestData.OwnerUserId).GetAsync($"/api/v1/attachments/{attachment.Id}/download")).StatusCode);
    }

    [MySqlFact]
    public async Task LabelsAndLinksPersistWithPermissions()
    {
        var labelsUrl = $"/api/v1/projects/{SharedTestData.ProjectId}/labels";
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.ViewerUserId).PostAsJsonAsync(labelsUrl, new SaveLabelDto { Name = "api" })).StatusCode);
        var created = await As(SharedTestData.DeveloperUserId).PostAsJsonAsync(labelsUrl, new SaveLabelDto { Name = "api", ColorHex = "#36B37E" });
        var label = (await created.Content.ReadFromJsonAsync<LabelDto>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await As(SharedTestData.DeveloperUserId).PostAsJsonAsync(labelsUrl, new SaveLabelDto { Name = "API" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.DeveloperUserId).DeleteAsync($"/api/v1/labels/{label.Id}")).StatusCode);

        var epic = SharedTestData.EpicIssueId;
        var tagged = await As(SharedTestData.DeveloperUserId).PostAsync($"/api/v1/issues/{epic}/labels/{label.Id}", null);
        Assert.Equal("api", Assert.Single((await tagged.Content.ReadFromJsonAsync<List<LabelDto>>())!).Name);

        var link = await As(SharedTestData.DeveloperUserId).PostAsJsonAsync($"/api/v1/issues/{epic}/links",
            new CreateIssueLinkDto { TargetIssueId = SharedTestData.BacklogTaskIssueId, LinkType = "Duplicates" });
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var inverse = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<List<IssueLinkDto>>($"/api/v1/issues/{SharedTestData.BacklogTaskIssueId}/links");
        var back = Assert.Single(inverse!, l => l.TargetIssueId == epic);
        Assert.Equal("IsDuplicatedBy", back.LinkType);

        Assert.Equal(HttpStatusCode.NoContent, (await As(SharedTestData.DeveloperUserId).DeleteAsync($"/api/v1/issue-links/{back.Id}")).StatusCode);
        Assert.Empty((await As(SharedTestData.ViewerUserId).GetFromJsonAsync<List<IssueLinkDto>>($"/api/v1/issues/{epic}/links"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await As(SharedTestData.OwnerUserId).DeleteAsync($"/api/v1/labels/{label.Id}")).StatusCode);
    }

    [MySqlFact]
    public async Task LegacyUpdateRouteRecordsStructuredHistoryAndStartedAt()
    {
        var epic = SharedTestData.EpicIssueId;
        var body = new
        {
            title = "Quản lý tài khoản người dùng",
            statusName = "In Progress",
            issueType = "Epic",
            priority = "Medium",
            storyPoints = 0,
            assigneeId = SharedTestData.DeveloperUserId,
            assigneeName = "Demo Developer",
            labels = Array.Empty<string>()
        };
        var response = await As(SharedTestData.OwnerUserId).PutAsJsonAsync($"/api/v1/issues/{epic}", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var delivery = fixture.Database.CreateDeliveryContext();
        var issue = await delivery.Issues.AsNoTracking().SingleAsync(i => i.Id == epic);
        Assert.NotNull(issue.StartedAt);
        var status = await delivery.IssueStatusHistories.AsNoTracking().SingleAsync(h => h.IssueId == epic);
        Assert.Equal(("ToDo", "InProgress"), (status.FromCategory, status.ToCategory));
        Assert.True(status.DurationSeconds > 0);
        var assignment = await delivery.IssueAssignmentHistories.AsNoTracking().SingleAsync(h => h.IssueId == epic);
        Assert.Equal((SharedTestData.DeveloperUserId, "Manual"), (assignment.ToAssigneeId!.Value, assignment.AssignmentSource));

        var statuses = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<List<StatusChangeDto>>($"/api/v1/issues/{epic}/status-history");
        Assert.Equal("In Progress", Assert.Single(statuses!).ToStatusName);
        var activity = await As(SharedTestData.ViewerUserId).GetFromJsonAsync<PagedResultDto<ActivityDto>>($"/api/v1/issues/{epic}/history?pageSize=5");
        Assert.NotEmpty(activity!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.OutsiderUserId).GetAsync($"/api/v1/issues/{epic}/assignment-history")).StatusCode);
    }

    [MySqlFact]
    public async Task TransitionEndpointReportsMissingWorkflowContract()
    {
        var response = await As(SharedTestData.OwnerUserId).PostAsJsonAsync($"/api/v1/issues/{SharedTestData.BugIssueId}/transition",
            new TransitionIssueDto { TargetStatusId = SharedTestData.InProgressStatusId });
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Contains("WORKFLOW_VALIDATION_UNAVAILABLE", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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
