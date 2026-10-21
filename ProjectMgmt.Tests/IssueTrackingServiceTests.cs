using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

public class IssueTrackingServiceTests
{
    private readonly InMemoryIssueRepository _repository = new();
    private readonly IssueTrackingService _service;

    public IssueTrackingServiceTests()
    {
        _service = new IssueTrackingService(
            _repository,
            new CreateIssueDtoValidator(),
            new UpdateIssueDtoValidator(),
            new IssueListQueryDtoValidator());
    }

    [Fact]
    public async Task GetReturnsDetailAndHidesDeletedIssues()
    {
        var found = await _service.GetAsync(SharedTestData.StoryIssueId);
        Assert.True(found.IsSuccess);
        Assert.Equal(2, found.Value.IssueNumber);
        Assert.Equal(SharedTestData.EpicIssueId, found.Value.EpicId);
        Assert.Null(found.Value.IssueKey);

        _repository.Issues.Single(i => i.Id == SharedTestData.StoryIssueId).IsDeleted = true;
        var deleted = await _service.GetAsync(SharedTestData.StoryIssueId);
        Assert.Equal("ISSUE_NOT_FOUND", deleted.Error.Code);
        Assert.Equal(ErrorType.NotFound, deleted.Error.Type);
    }

    [Fact]
    public async Task ListIsScopedToProjectOrderedByRankAndPaged()
    {
        var first = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { Page = 1, PageSize = 2 });
        var second = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { Page = 2, PageSize = 2 });

        Assert.Equal(5, first.Value.TotalCount);
        Assert.Equal([1, 2], first.Value.Items.Select(i => i.IssueNumber));
        Assert.Equal([3, 4], second.Value.Items.Select(i => i.IssueNumber));
        Assert.DoesNotContain(first.Value.Items.Concat(second.Value.Items), i => i.ProjectId != SharedTestData.ProjectId);
    }

    [Fact]
    public async Task ListFiltersBacklogSprintAssigneeAndSearch()
    {
        var backlog = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { BacklogOnly = true });
        Assert.Equal([1, 4], backlog.Value.Items.Select(i => i.IssueNumber));

        var sprint = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { SprintId = SharedTestData.ActiveSprintId });
        Assert.Equal([2, 3, 5], sprint.Value.Items.Select(i => i.IssueNumber));

        var mine = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { AssigneeId = SharedTestData.DeveloperUserId });
        Assert.Equal([2, 3], mine.Value.Items.Select(i => i.IssueNumber));

        var byText = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { SearchText = "mật khẩu" });
        Assert.Equal(SharedTestData.StoryIssueId, Assert.Single(byText.Value.Items).Id);

        var byNumber = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { SearchText = "5" });
        Assert.Equal(SharedTestData.BugIssueId, Assert.Single(byNumber.Value.Items).Id);
    }

    [Theory]
    [InlineData(0, 20, "ISSUE_PAGE_INVALID")]
    [InlineData(1, 0, "ISSUE_PAGE_SIZE_INVALID")]
    [InlineData(1, 101, "ISSUE_PAGE_SIZE_INVALID")]
    public async Task ListRejectsInvalidPaging(int page, int pageSize, string code)
    {
        var result = await _service.ListAsync(SharedTestData.ProjectId, new IssueListQueryDto { Page = page, PageSize = pageSize });
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task ListRejectsConflictingBacklogAndSprintFilters()
    {
        var result = await _service.ListAsync(SharedTestData.ProjectId,
            new IssueListQueryDto { BacklogOnly = true, SprintId = SharedTestData.ActiveSprintId });
        Assert.Equal("ISSUE_FILTER_CONFLICT", result.Error.Code);
        Assert.Equal("ISSUE_PROJECT_REQUIRED", (await _service.ListAsync(Guid.Empty, new IssueListQueryDto())).Error.Code);
    }

    [Fact]
    public void CreateValidationAcceptsMinimalIssue()
    {
        var result = _service.ValidateCreate(ValidCreate());
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("   ", null, null, "ISSUE_TITLE_REQUIRED")]
    [InlineData("ok", -1.0, null, "ISSUE_STORY_POINTS_INVALID")]
    [InlineData("ok", 10000.0, null, "ISSUE_STORY_POINTS_INVALID")]
    [InlineData("ok", 1.255, null, "ISSUE_STORY_POINTS_INVALID")]
    [InlineData("ok", null, -5, "ISSUE_ESTIMATE_INVALID")]
    public void CreateValidationRejectsBadFields(string title, double? points, int? estimate, string code)
    {
        var request = ValidCreate();
        request.Title = title;
        request.StoryPoints = points.HasValue ? (decimal)points.Value : null;
        request.OriginalEstimateMinutes = estimate;
        Assert.Equal(code, _service.ValidateCreate(request).Error.Code);
    }

    [Fact]
    public void CreateValidationRejectsLongTitleEmptyIdsAndAbsurdDueDate()
    {
        var longTitle = ValidCreate();
        longTitle.Title = new string('x', 501);
        Assert.Equal("ISSUE_TITLE_TOO_LONG", _service.ValidateCreate(longTitle).Error.Code);

        var noProject = ValidCreate();
        noProject.ProjectId = Guid.Empty;
        Assert.Equal("ISSUE_PROJECT_REQUIRED", _service.ValidateCreate(noProject).Error.Code);

        var emptyParent = ValidCreate();
        emptyParent.ParentId = Guid.Empty;
        Assert.Equal("ISSUE_PARENT_INVALID", _service.ValidateCreate(emptyParent).Error.Code);

        var oldDue = ValidCreate();
        oldDue.DueDate = new DateOnly(1999, 12, 31);
        Assert.Equal("ISSUE_DUE_DATE_INVALID", _service.ValidateCreate(oldDue).Error.Code);
    }

    [Fact]
    public void UpdateValidationUsesSameFieldRules()
    {
        Assert.True(_service.ValidateUpdate(new UpdateIssueDto { Title = "Đổi tên", StoryPoints = 0.5m }).IsSuccess);
        Assert.Equal("ISSUE_TITLE_REQUIRED", _service.ValidateUpdate(new UpdateIssueDto { Title = "" }).Error.Code);
        Assert.Equal("ISSUE_ASSIGNEE_INVALID",
            _service.ValidateUpdate(new UpdateIssueDto { Title = "x", AssigneeId = Guid.Empty }).Error.Code);
    }

    [Fact]
    public void MapperRoundTripsCreateAndUpdate()
    {
        var created = IssueMapper.ToNewEntity(ValidCreate(), Guid.NewGuid(), SharedTestData.SeededAtUtc);
        Assert.Equal("Tạo màn hình đăng nhập", created.Title);
        Assert.Equal(SharedTestData.TaskTypeId, created.IssueTypeId);
        Assert.Equal(0, created.IssueNumber);

        IssueMapper.ApplyUpdate(created, new UpdateIssueDto { Title = "  Đổi tên  ", StoryPoints = 3 }, SharedTestData.SeededAtUtc.AddDays(1));
        var detail = IssueMapper.ToDetail(created, SharedTestData.ProjectKey);
        Assert.Equal("Đổi tên", detail.Title);
        Assert.Equal(3, detail.StoryPoints);
        Assert.Null(detail.IssueKey);
        Assert.Equal(SharedTestData.SeededAtUtc.AddDays(1), detail.UpdatedAt);

        var seeded = SharedTestData.CreateIssues()[1];
        Assert.Equal("DEMO-2", IssueMapper.ToListItem(seeded, SharedTestData.ProjectKey).IssueKey);
    }

    private static CreateIssueDto ValidCreate() => new()
    {
        ProjectId = SharedTestData.ProjectId,
        IssueTypeId = SharedTestData.TaskTypeId,
        Title = "  Tạo màn hình đăng nhập ",
        StoryPoints = 2,
        DueDate = new DateOnly(2026, 10, 30)
    };
}
