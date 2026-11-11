using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-176: đổi cha theo cấp IssueType và xếp RankOrder bằng trung bình cộng.</summary>
public class IssueHierarchyServiceTests
{
    private readonly InMemoryIssueRepository _repository = new();
    private readonly IssueHierarchyService _service;

    public IssueHierarchyServiceTests()
    {
        _service = new IssueHierarchyService(
            _repository,
            new IssueHierarchyLoader(_repository, new SharedProjectLookupService()),
            new FixedTimeProvider(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)));
    }

    private Issue Get(Guid id) => _repository.Issues.Single(issue => issue.Id == id);

    [Fact]
    public async Task MovingTaskUnderEpicSetsEpicId()
    {
        var result = await _service.ChangeParentAsync(SharedTestData.BacklogTaskIssueId,
            new ChangeIssueParentDto { ParentId = SharedTestData.EpicIssueId }, SharedTestData.OwnerUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SharedTestData.EpicIssueId, Get(SharedTestData.BacklogTaskIssueId).ParentId);
        Assert.Equal(SharedTestData.EpicIssueId, Get(SharedTestData.BacklogTaskIssueId).EpicId);
        Assert.Equal("ParentChanged", Assert.Single(_repository.Activities).Action);
    }

    [Fact]
    public async Task MovingSubtaskToAnotherParentRecomputesEpic()
    {
        var result = await _service.ChangeParentAsync(SharedTestData.SubtaskIssueId,
            new ChangeIssueParentDto { ParentId = SharedTestData.BacklogTaskIssueId }, SharedTestData.OwnerUserId);

        Assert.True(result.IsSuccess);
        var subtask = Get(SharedTestData.SubtaskIssueId);
        Assert.Equal(SharedTestData.BacklogTaskIssueId, subtask.ParentId);
        Assert.Null(subtask.EpicId);
    }

    [Fact]
    public async Task DetachingStoryFromEpicClearsEpicOnDescendants()
    {
        var result = await _service.ChangeParentAsync(SharedTestData.StoryIssueId,
            new ChangeIssueParentDto { ParentId = null }, SharedTestData.OwnerUserId);

        Assert.True(result.IsSuccess);
        Assert.Null(Get(SharedTestData.StoryIssueId).ParentId);
        Assert.Null(Get(SharedTestData.StoryIssueId).EpicId);
        Assert.Null(Get(SharedTestData.SubtaskIssueId).EpicId);
    }

    [Fact]
    public async Task InvalidParentsAreRejectedWithoutWriting()
    {
        Assert.Equal("ISSUE_SUBTASK_PARENT_REQUIRED", (await _service.ChangeParentAsync(SharedTestData.SubtaskIssueId,
            new ChangeIssueParentDto(), SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_EPIC_PARENT_NOT_ALLOWED", (await _service.ChangeParentAsync(SharedTestData.EpicIssueId,
            new ChangeIssueParentDto { ParentId = SharedTestData.StoryIssueId }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_PARENT_LEVEL_INVALID", (await _service.ChangeParentAsync(SharedTestData.BacklogTaskIssueId,
            new ChangeIssueParentDto { ParentId = SharedTestData.StoryIssueId }, SharedTestData.OwnerUserId)).Error.Code);
        // Issue của Project khác không nằm trong danh mục loại của DEMO nên coi như không tìm thấy cha.
        Assert.Equal("ISSUE_PARENT_NOT_FOUND", (await _service.ChangeParentAsync(SharedTestData.StoryIssueId,
            new ChangeIssueParentDto { ParentId = SharedTestData.OtherProjectIssueId }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task RankBetweenTwoIssuesUsesAverage()
    {
        var result = await _service.RankAsync(SharedTestData.BugIssueId, new RankIssueDto
        {
            PreviousIssueId = SharedTestData.EpicIssueId,
            NextIssueId = SharedTestData.StoryIssueId
        }, SharedTestData.OwnerUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1.5m, result.Value.RankOrder);
        Assert.False(result.Value.Rebalanced);
        var order = (await _repository.GetRankedAsync(SharedTestData.ProjectId)).Select(i => i.IssueNumber);
        Assert.Equal([1, 5, 2, 3, 4], order);
    }

    [Fact]
    public async Task RankAtEdgesUsesNeighborPlusOrMinusOne()
    {
        var top = await _service.RankAsync(SharedTestData.BugIssueId,
            new RankIssueDto { NextIssueId = SharedTestData.EpicIssueId }, SharedTestData.OwnerUserId);
        Assert.Equal(0, top.Value.RankOrder);

        var bottom = await _service.RankAsync(SharedTestData.EpicIssueId,
            new RankIssueDto { PreviousIssueId = SharedTestData.BacklogTaskIssueId }, SharedTestData.OwnerUserId);
        Assert.Equal(5, bottom.Value.RankOrder);
    }

    [Fact]
    public async Task TooSmallGapTriggersRebalance()
    {
        Get(SharedTestData.StoryIssueId).RankOrder = 1.0000000001m;

        var result = await _service.RankAsync(SharedTestData.BugIssueId, new RankIssueDto
        {
            PreviousIssueId = SharedTestData.EpicIssueId,
            NextIssueId = SharedTestData.StoryIssueId
        }, SharedTestData.OwnerUserId);

        Assert.True(result.Value.Rebalanced);
        Assert.Equal(1.5m, result.Value.RankOrder);
        Assert.Equal([1, 5, 2, 3, 4], (await _repository.GetRankedAsync(SharedTestData.ProjectId)).Select(i => i.IssueNumber));
    }

    [Fact]
    public async Task RankRejectsMissingCrossProjectAndStaleNeighbors()
    {
        Assert.Equal("ISSUE_RANK_NEIGHBOR_REQUIRED",
            (await _service.RankAsync(SharedTestData.BugIssueId, new RankIssueDto(), SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_RANK_NEIGHBOR_INVALID", (await _service.RankAsync(SharedTestData.BugIssueId,
            new RankIssueDto { PreviousIssueId = SharedTestData.OtherProjectIssueId }, SharedTestData.OwnerUserId)).Error.Code);
        var stale = await _service.RankAsync(SharedTestData.BugIssueId, new RankIssueDto
        {
            PreviousIssueId = SharedTestData.StoryIssueId,
            NextIssueId = SharedTestData.EpicIssueId
        }, SharedTestData.OwnerUserId);
        Assert.Equal(ErrorType.Conflict, stale.Error.Type);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public void RankingMathHandlesEdgesAndPrecision()
    {
        Assert.Equal(2.5m, IssueRanking.Between(2, 3));
        Assert.Null(IssueRanking.Between(1m, 1.0000000005m));
        Assert.Equal(1.000000001m, IssueRanking.Between(1m, 1.000000002m));
        Assert.Throws<ArgumentException>(() => IssueRanking.Between(null, null));
    }
}
