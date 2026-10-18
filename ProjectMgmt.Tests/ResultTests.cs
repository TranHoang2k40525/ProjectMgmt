using ProjectMgmt.BuildingBlocks.Results;
using Xunit;

namespace ProjectMgmt.Tests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success("test value");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("test value", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        var error = Error.Validation("INVALID_KEY", "Key is invalid");
        var result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }
}
