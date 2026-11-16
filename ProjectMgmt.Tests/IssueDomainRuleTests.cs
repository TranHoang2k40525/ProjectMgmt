using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

public class IssueDomainRuleTests
{
    private static readonly IssueTypeLevel Epic = SharedTestData.TypeLevel(SharedTestData.EpicTypeId);
    private static readonly IssueTypeLevel Story = SharedTestData.TypeLevel(SharedTestData.StoryTypeId);
    private static readonly IssueTypeLevel Task = SharedTestData.TypeLevel(SharedTestData.TaskTypeId);
    private static readonly IssueTypeLevel Subtask = SharedTestData.TypeLevel(SharedTestData.SubtaskTypeId);

    private static readonly IssueHierarchyNode EpicNode =
        new(SharedTestData.EpicIssueId, SharedTestData.ProjectId, Epic, null, null);

    private static readonly IssueHierarchyNode StoryNode =
        new(SharedTestData.StoryIssueId, SharedTestData.ProjectId, Story, SharedTestData.EpicIssueId, SharedTestData.EpicIssueId);

    [Fact]
    public void SubtaskRequiresParent()
    {
        var violation = IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.ProjectId, Subtask, null, []);
        Assert.Equal("ISSUE_SUBTASK_PARENT_REQUIRED", violation?.Code);
    }

    [Fact]
    public void StandardIssuesAndEpicsMayStandAlone()
    {
        Assert.Null(IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.ProjectId, Epic, null, []));
        Assert.Null(IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.ProjectId, Task, null, []));
    }

    [Fact]
    public void StoryMayBelongToEpicAndSubtaskToStory()
    {
        Assert.Null(IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.ProjectId, Story, EpicNode, []));
        Assert.Null(IssueHierarchyRules.ValidateParent(
            Guid.NewGuid(), SharedTestData.ProjectId, Subtask, StoryNode, [SharedTestData.EpicIssueId]));
    }

    [Fact]
    public void EpicCannotHaveParent()
    {
        var violation = IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.ProjectId, Epic, StoryNode, []);
        Assert.Equal("ISSUE_EPIC_PARENT_NOT_ALLOWED", violation?.Code);
    }

    [Fact]
    public void ParentMustHaveHigherLevel()
    {
        var subtaskNode = new IssueHierarchyNode(SharedTestData.SubtaskIssueId, SharedTestData.ProjectId, Subtask, StoryNode.Id, EpicNode.Id);
        Assert.Equal("ISSUE_PARENT_LEVEL_INVALID",
            IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.ProjectId, Task, StoryNode, [])?.Code);
        Assert.Equal("ISSUE_PARENT_LEVEL_INVALID",
            IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.ProjectId, Subtask, subtaskNode, [])?.Code);
    }

    [Fact]
    public void SelfParentCrossProjectAndCyclesAreRejected()
    {
        Assert.Equal("ISSUE_PARENT_SELF",
            IssueHierarchyRules.ValidateParent(StoryNode.Id, SharedTestData.ProjectId, Story, StoryNode, [])?.Code);
        Assert.Equal("ISSUE_PARENT_PROJECT_MISMATCH",
            IssueHierarchyRules.ValidateParent(Guid.NewGuid(), SharedTestData.OtherProjectId, Story, EpicNode, [])?.Code);

        var movingId = Guid.NewGuid();
        Assert.Equal("ISSUE_PARENT_CYCLE",
            IssueHierarchyRules.ValidateParent(movingId, SharedTestData.ProjectId, Subtask, StoryNode, [EpicNode.Id, movingId])?.Code);
    }

    [Fact]
    public void EpicIdIsDenormalizedFromParent()
    {
        Assert.Null(IssueHierarchyRules.ResolveEpicId(null));
        Assert.Equal(EpicNode.Id, IssueHierarchyRules.ResolveEpicId(EpicNode));
        Assert.Equal(EpicNode.Id, IssueHierarchyRules.ResolveEpicId(StoryNode));
    }

    [Fact]
    public void EpicLinkMustTargetEpicInSameProject()
    {
        Assert.Null(IssueHierarchyRules.ValidateEpic(Guid.NewGuid(), SharedTestData.ProjectId, Task, EpicNode));
        Assert.Equal("ISSUE_EPIC_LINK_INVALID",
            IssueHierarchyRules.ValidateEpic(Guid.NewGuid(), SharedTestData.ProjectId, Task, StoryNode)?.Code);
        Assert.Equal("ISSUE_EPIC_LINK_INVALID",
            IssueHierarchyRules.ValidateEpic(Guid.NewGuid(), SharedTestData.OtherProjectId, Task, EpicNode)?.Code);
        Assert.Equal("ISSUE_EPIC_LINK_INVALID",
            IssueHierarchyRules.ValidateEpic(Guid.NewGuid(), SharedTestData.ProjectId, Epic, EpicNode)?.Code);
    }

    [Theory]
    [InlineData("DEMO-12", "DEMO", 12)]
    [InlineData(" crm-7 ", "CRM", 7)]
    [InlineData("A1-1", "A1", 1)]
    public void IssueKeyParsesValidKeys(string text, string projectKey, int number)
    {
        Assert.True(IssueKey.TryParse(text, out var parsedKey, out var parsedNumber));
        Assert.Equal(projectKey, parsedKey);
        Assert.Equal(number, parsedNumber);
        Assert.Equal($"{projectKey}-{number}", IssueKey.Format(parsedKey, parsedNumber));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DEMO")]
    [InlineData("DEMO-")]
    [InlineData("DEMO-0")]
    [InlineData("DEMO--1")]
    [InlineData("1DEMO-3")]
    [InlineData("DEMO-1.5")]
    [InlineData("c0de0000-0000-0000-0007-000000000001")]
    public void IssueKeyRejectsInvalidText(string? text)
    {
        Assert.False(IssueKey.TryParse(text, out _, out _));
    }
}
