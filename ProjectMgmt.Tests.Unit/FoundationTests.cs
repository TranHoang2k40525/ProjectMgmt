using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.BuildingBlocks.Security;
using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.Tests.Fakes;
using Xunit;

namespace ProjectMgmt.Tests.Unit;

public sealed class FoundationTests
{
    [Fact]
    public void ResultSuccessExposesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void ResultFailureDoesNotExposeValue()
    {
        var result = Result.Failure<int>(Error.NotFound("sample.missing", "Missing."));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Theory]
    [InlineData("issue.create", true)]
    [InlineData("ai.breakdown.request", true)]
    [InlineData("Issue.Create", false)]
    [InlineData("issue", false)]
    [InlineData("issue..create", false)]
    public void PermissionCodeConventionIsEnforced(string code, bool expected)
    {
        Assert.Equal(expected, PermissionCode.IsValid(code));
    }

    [Fact]
    public async Task FakeIssueServiceIsDeterministicAndRecordsRequests()
    {
        var service = new FakeIssueService();
        var request = new CreateIssueDto(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            "Foundation contract test");

        var first = await service.CreateIssueAsync(request);
        var second = await service.CreateIssueAsync(request);

        Assert.Equal("FAKE-1", first.Value.IssueKey);
        Assert.Equal("FAKE-2", second.Value.IssueKey);
        Assert.Equal(2, service.Requests.Count);
    }
}
