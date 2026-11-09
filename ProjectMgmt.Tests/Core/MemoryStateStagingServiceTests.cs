using ProjectMgmt.Core.Staging;
using Xunit;

namespace ProjectMgmt.Tests.Core;

public class MemoryStateStagingServiceTests
{
    private sealed class SampleProposal
    {
        public string SprintGoal { get; set; } = string.Empty;
        public List<string> Tasks { get; set; } = new();
    }

    [Fact]
    public async Task SaveAndGetProposal_StoresPayloadCorrectly()
    {
        var staging = new MemoryStateStagingService();
        var sessionId = Guid.NewGuid().ToString("N");
        var proposal = new SampleProposal
        {
            SprintGoal = "Triển khai xác thực đa lớp",
            Tasks = new List<string> { "Task 1", "Task 2" }
        };

        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromMinutes(10));
        var retrieved = await staging.GetProposalAsync<SampleProposal>(sessionId);

        Assert.NotNull(retrieved);
        Assert.Equal("Triển khai xác thực đa lớp", retrieved.SprintGoal);
        Assert.Equal(2, retrieved.Tasks.Count);
        Assert.True(await staging.ExistsAsync(sessionId));
    }

    [Fact]
    public async Task GetProposal_WhenExpired_ReturnsNullAndEvicts()
    {
        var timeProvider = new TestTimeProvider();
        var staging = new MemoryStateStagingService(timeProvider);
        var sessionId = "session_expiring";
        var proposal = new SampleProposal { SprintGoal = "Expired goal" };

        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromMinutes(5));

        // Advance beyond TTL
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        var retrieved = await staging.GetProposalAsync<SampleProposal>(sessionId);

        Assert.Null(retrieved);
        Assert.False(await staging.ExistsAsync(sessionId));
    }

    [Fact]
    public async Task RemoveProposal_RemovesStagedPayload()
    {
        var staging = new MemoryStateStagingService();
        var sessionId = "session_remove";
        var proposal = new SampleProposal { SprintGoal = "To be removed" };

        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromMinutes(10));
        Assert.True(await staging.ExistsAsync(sessionId));

        var removed = await staging.RemoveProposalAsync(sessionId);

        Assert.True(removed);
        Assert.False(await staging.ExistsAsync(sessionId));
        Assert.Null(await staging.GetProposalAsync<SampleProposal>(sessionId));
    }

    [Fact]
    public async Task Immutability_ModifyingOriginalObjectDoesNotMutateStagedCopy()
    {
        var staging = new MemoryStateStagingService();
        var sessionId = "session_immutability";
        var proposal = new SampleProposal
        {
            SprintGoal = "Original Goal",
            Tasks = new List<string> { "Task 1" }
        };

        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromMinutes(10));

        // Mutate local reference
        proposal.SprintGoal = "Mutated Local Goal";
        proposal.Tasks.Add("Injected Task");

        var retrieved = await staging.GetProposalAsync<SampleProposal>(sessionId);

        Assert.NotNull(retrieved);
        Assert.Equal("Original Goal", retrieved.SprintGoal);
        Assert.Single(retrieved.Tasks);
    }

    [Fact]
    public async Task SaveProposal_NullSessionOrPayload_ThrowsArgumentException()
    {
        var staging = new MemoryStateStagingService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            staging.SaveProposalAsync(string.Empty, new SampleProposal(), TimeSpan.FromMinutes(5)));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            staging.SaveProposalAsync<SampleProposal>("valid_id", null!, TimeSpan.FromMinutes(5)));
    }
}
