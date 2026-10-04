using IdentityExperience.Application.IServices;
using IdentityExperience.Application.Services;
using Xunit;

namespace ProjectMgmt.Tests;

public class ForYouTests
{
    [Fact]
    public async Task ForYouBuildsAttentionFromRealWorkCounters()
    {
        var reader = new FakeUserWorkReader
        {
            Overview = new UserWorkOverview
            {
                ActiveCount = 4,
                OverdueCount = 1,
                DueSoonCount = 2,
                AssignedIssues =
                [
                    new UserWorkItem
                    {
                        IssueId = Guid.NewGuid(),
                        ProjectId = Guid.NewGuid(),
                        IssueKey = "SCRUM-8",
                        Title = "Hoàn thiện đăng nhập"
                    }
                ]
            }
        };
        var service = new ForYouServices(reader);

        var result = await service.GetAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Single(result.AssignedIssues!);
        Assert.Equal(3, result.AttentionFocus!.Count);
        Assert.Contains(result.AttentionFocus, item => item.Contains("quá hạn", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForYouShowsCalmStateWhenNoWorkNeedsAttention()
    {
        var service = new ForYouServices(new FakeUserWorkReader());

        var result = await service.GetAsync(Guid.NewGuid());

        Assert.True(result.Success);
        var attention = Assert.Single(result.AttentionFocus!);
        Assert.Contains("không có công việc khẩn cấp", attention, StringComparison.OrdinalIgnoreCase);
    }
}

public class FakeUserWorkReader : IUserWorkReader
{
    public UserWorkOverview Overview { get; set; } = new();

    public Task<UserWorkOverview> GetOverviewAsync(Guid userId)
    {
        return Task.FromResult(Overview);
    }
}
