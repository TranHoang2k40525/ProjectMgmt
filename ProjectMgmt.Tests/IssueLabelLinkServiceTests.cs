using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-185: nhãn theo Project và liên kết hai chiều giữa Issue.</summary>
public class IssueLabelLinkServiceTests
{
    private readonly InMemoryIssueRepository _issues = new();
    private readonly InMemoryLabelLinkRepository _repository = new();
    private readonly IssueLabelLinkService _service;

    public IssueLabelLinkServiceTests()
    {
        _service = new IssueLabelLinkService(_issues, _repository, new SaveLabelDtoValidator(),
            new FixedTimeProvider(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task LabelsAreUniquePerProjectIgnoringCase()
    {
        var created = await _service.CreateLabelAsync(SharedTestData.ProjectId, new SaveLabelDto { Name = " backend ", ColorHex = "#0052cc" });
        Assert.Equal("backend", created.Value.Name);
        Assert.Equal("#0052CC", created.Value.ColorHex);

        Assert.Equal(ErrorType.Conflict, (await _service.CreateLabelAsync(SharedTestData.ProjectId, new SaveLabelDto { Name = "BACKEND" })).Error.Type);
        Assert.True((await _service.CreateLabelAsync(SharedTestData.OtherProjectId, new SaveLabelDto { Name = "backend" })).IsSuccess);
        Assert.Equal("LABEL_COLOR_INVALID", (await _service.CreateLabelAsync(SharedTestData.ProjectId, new SaveLabelDto { Name = "x", ColorHex = "blue" })).Error.Code);
        Assert.Equal("LABEL_NAME_REQUIRED", (await _service.CreateLabelAsync(SharedTestData.ProjectId, new SaveLabelDto { Name = " " })).Error.Code);
        Assert.Equal("#DFE1E6", (await _service.CreateLabelAsync(SharedTestData.ProjectId, new SaveLabelDto { Name = "ui" })).Value.ColorHex);
    }

    [Fact]
    public async Task AddRemoveIssueLabelIsIdempotentAndScopedToProject()
    {
        var backend = (await _service.CreateLabelAsync(SharedTestData.ProjectId, new SaveLabelDto { Name = "backend" })).Value;
        var foreign = (await _service.CreateLabelAsync(SharedTestData.OtherProjectId, new SaveLabelDto { Name = "khác" })).Value;

        Assert.Single((await _service.AddLabelToIssueAsync(SharedTestData.StoryIssueId, backend.Id, SharedTestData.OwnerUserId)).Value);
        Assert.Single((await _service.AddLabelToIssueAsync(SharedTestData.StoryIssueId, backend.Id, SharedTestData.OwnerUserId)).Value);
        Assert.Equal("LABEL_NOT_IN_PROJECT", (await _service.AddLabelToIssueAsync(SharedTestData.StoryIssueId, foreign.Id, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Empty((await _service.RemoveLabelFromIssueAsync(SharedTestData.StoryIssueId, backend.Id, SharedTestData.OwnerUserId)).Value);
        Assert.Equal(["LabelAdded", "LabelRemoved"], _issues.Activities.Select(a => a.Action));
    }

    [Fact]
    public async Task DeletingLabelDetachesItFromIssues()
    {
        var label = (await _service.CreateLabelAsync(SharedTestData.ProjectId, new SaveLabelDto { Name = "tạm" })).Value;
        await _service.AddLabelToIssueAsync(SharedTestData.BugIssueId, label.Id, SharedTestData.OwnerUserId);

        Assert.True((await _service.DeleteLabelAsync(label.Id)).IsSuccess);
        Assert.Empty((await _service.GetIssueLabelsAsync(SharedTestData.BugIssueId)).Value);
        Assert.Equal("LABEL_NOT_FOUND", (await _service.DeleteLabelAsync(label.Id)).Error.Code);
    }

    [Fact]
    public async Task BlocksCreatesInverseAndDeleteRemovesBoth()
    {
        var link = await _service.CreateLinkAsync(SharedTestData.StoryIssueId,
            new CreateIssueLinkDto { TargetIssueId = SharedTestData.BugIssueId, LinkType = "blocks" }, SharedTestData.OwnerUserId);

        Assert.Equal(IssueLinkTypes.Blocks, link.Value.LinkType);
        Assert.Equal(5, link.Value.TargetIssueNumber);
        var inverse = Assert.Single((await _service.GetLinksAsync(SharedTestData.BugIssueId)).Value);
        Assert.Equal(IssueLinkTypes.IsBlockedBy, inverse.LinkType);
        Assert.Equal(SharedTestData.StoryIssueId, inverse.TargetIssueId);

        Assert.Equal(ErrorType.Conflict, (await _service.CreateLinkAsync(SharedTestData.BugIssueId,
            new CreateIssueLinkDto { TargetIssueId = SharedTestData.StoryIssueId, LinkType = "IsBlockedBy" }, SharedTestData.OwnerUserId)).Error.Type);

        Assert.True((await _service.DeleteLinkAsync(inverse.Id, SharedTestData.OwnerUserId)).IsSuccess);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task RelatesIsSymmetricWithoutDuplicates()
    {
        await _service.CreateLinkAsync(SharedTestData.EpicIssueId,
            new CreateIssueLinkDto { TargetIssueId = SharedTestData.BugIssueId, LinkType = "Relates" }, SharedTestData.OwnerUserId);
        Assert.Equal(2, _repository.Links.Count);
        Assert.Equal(ErrorType.Conflict, (await _service.CreateLinkAsync(SharedTestData.BugIssueId,
            new CreateIssueLinkDto { TargetIssueId = SharedTestData.EpicIssueId, LinkType = "Relates" }, SharedTestData.OwnerUserId)).Error.Type);
    }

    [Theory]
    [InlineData("Blocks", true, false, "ISSUE_LINK_SELF")]
    [InlineData("Clones", false, false, "ISSUE_LINK_TYPE_INVALID")]
    [InlineData("Relates", false, true, "ISSUE_LINK_TARGET_INVALID")]
    public async Task InvalidLinksAreRejected(string type, bool self, bool otherProject, string code)
    {
        var target = self ? SharedTestData.StoryIssueId : otherProject ? SharedTestData.OtherProjectIssueId : SharedTestData.BugIssueId;
        var result = await _service.CreateLinkAsync(SharedTestData.StoryIssueId,
            new CreateIssueLinkDto { TargetIssueId = target, LinkType = type }, SharedTestData.OwnerUserId);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_repository.Links);
    }
}
