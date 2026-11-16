using System.Collections.Concurrent;
using System.Text.Json;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using Xunit;

namespace ProjectMgmt.Tests.Core;

/// <summary>
/// Challenger 1 Adversarial Test Harness:
/// Empirical stress testing for ProjectScopeGuard and MemoryStateStagingService.
/// </summary>
public class Challenger1AdversarialTests
{
    #region Nested DTOs for Staging Tests

    private sealed class LargePayloadDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string BigText { get; set; } = string.Empty;
        public List<SubtaskItem> Items { get; set; } = new();
    }

    private sealed class SubtaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    private sealed class MismatchedSchemaDto
    {
        public int RequiredNumericField { get; set; }
    }

    private sealed class DynamicScopeContext : IProjectScopeContext
    {
        public Guid OrganizationId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid UserId { get; set; }

        public DynamicScopeContext(Guid orgId, Guid projId, Guid userId)
        {
            OrganizationId = orgId;
            ProjectId = projId;
            UserId = userId;
        }
    }

    #endregion

    #region ProjectScopeGuard Adversarial Challenges

    [Fact]
    public void ProjectScopeGuard_InvertedIds_ThrowsProjectScopeException()
    {
        // Attack: Swap OrgId and ProjectId in target parameters
        var orgId = Guid.NewGuid();
        var projId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var scope = new ProjectScopeContext(orgId, projId, userId);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, projId, orgId));

        Assert.Equal(ProjectScopeException.DefaultErrorCode, ex.ErrorCode);
        Assert.Equal(orgId, ex.CurrentOrgId);
        Assert.Equal(projId, ex.TargetOrgId);
    }

    [Fact]
    public void ProjectScopeGuard_IdenticalOrgAndProjectIds_ValidatesCorrectly()
    {
        // Edge case: In unusual setups, OrgId == ProjectId
        var sharedId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var scope = new ProjectScopeContext(sharedId, sharedId, userId);

        // Matching should pass
        var ex = Record.Exception(() => ProjectScopeGuard.Validate(scope, sharedId, sharedId));
        Assert.Null(ex);

        // Different Org should fail
        var differentOrg = Guid.NewGuid();
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, differentOrg, sharedId));

        // Different Project should fail
        var differentProj = Guid.NewGuid();
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, sharedId, differentProj));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", true, false)]
    [InlineData("D3B07384-D113-46D4-A6A4-B9772F2A1E02", false, false)]
    [InlineData("d3b07384-d113-46d4-a6a4-b9772f2a1e02", false, false)]
    public void ProjectScopeGuard_GuidCaseAndEmptyRepresentations(string guidString, bool isEmpty, bool shouldThrow)
    {
        var parsed = Guid.Parse(guidString);
        Assert.Equal(isEmpty, parsed == Guid.Empty);

        var validOrg = Guid.NewGuid();
        var validProj = Guid.NewGuid();
        var validUser = Guid.NewGuid();

        if (isEmpty)
        {
            var invalidScope = new ProjectScopeContext(parsed, validProj, validUser);
            Assert.Throws<ProjectScopeException>(() => ProjectScopeGuard.Validate(invalidScope, validOrg, validProj));

            var invalidTargetOrgScope = new ProjectScopeContext(validOrg, validProj, validUser);
            Assert.Throws<ProjectScopeException>(() => ProjectScopeGuard.Validate(invalidTargetOrgScope, parsed, validProj));
        }
        else
        {
            // Case insensitivity: Upper and Lower parse to equal 16-byte structs
            var upper = Guid.Parse(guidString.ToUpperInvariant());
            var lower = Guid.Parse(guidString.ToLowerInvariant());
            Assert.Equal(upper, lower);

            var scope = new ProjectScopeContext(upper, validProj, validUser);
            var ex = Record.Exception(() => ProjectScopeGuard.Validate(scope, lower, validProj));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void ProjectScopeGuard_AllEmptyGuidPermutations_AllThrowProjectScopeException()
    {
        var gEmpty = Guid.Empty;
        var gVal = Guid.NewGuid();

        // TargetOrg empty
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(new ProjectScopeContext(gVal, gVal, gVal), gEmpty, gVal));

        // TargetProj empty
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(new ProjectScopeContext(gVal, gVal, gVal), gVal, gEmpty));

        // Both target empty
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(new ProjectScopeContext(gVal, gVal, gVal), gEmpty, gEmpty));

        // Scope Org empty
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(new ProjectScopeContext(gEmpty, gVal, gVal), gVal, gVal));

        // Scope Proj empty
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(new ProjectScopeContext(gVal, gEmpty, gVal), gVal, gVal));

        // Scope User empty
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(new ProjectScopeContext(gVal, gVal, gEmpty), gVal, gVal));

        // ValidateProject with empty target
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.ValidateProject(new ProjectScopeContext(gVal, gVal, gVal), gEmpty));

        // ValidateOrganization with empty target
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.ValidateOrganization(new ProjectScopeContext(gVal, gVal, gVal), gEmpty));
    }

    [Fact]
    public void ProjectScopeGuard_HighConcurrencyStress_ZeroStateCorruption()
    {
        const int threadCount = 20;
        const int iterationsPerThread = 500;
        var exceptions = new ConcurrentBag<Exception>();

        Parallel.For(0, threadCount, _ =>
        {
            for (var i = 0; i < iterationsPerThread; i++)
            {
                var org = Guid.NewGuid();
                var proj = Guid.NewGuid();
                var user = Guid.NewGuid();
                var scope = new ProjectScopeContext(org, proj, user);

                try
                {
                    // 1. Valid call must pass
                    ProjectScopeGuard.Validate(scope, org, proj);
                    ProjectScopeGuard.ValidateProject(scope, proj);
                    ProjectScopeGuard.ValidateOrganization(scope, org);
                    ProjectScopeGuard.EnsureValidScope(scope);

                    // 2. Cross-tenant must throw ProjectScopeException
                    var crossOrg = Guid.NewGuid();
                    var threw = false;
                    try
                    {
                        ProjectScopeGuard.Validate(scope, crossOrg, proj);
                    }
                    catch (ProjectScopeException)
                    {
                        threw = true;
                    }

                    if (!threw)
                    {
                        exceptions.Add(new InvalidOperationException("ProjectScopeGuard failed to reject cross-tenant OrgId"));
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        Assert.Empty(exceptions);
    }

    #endregion

    #region MemoryStateStagingService Adversarial Challenges

    [Fact]
    public async Task MemoryStateStaging_LargePayload_SerializesAndDeserializesAccurately()
    {
        var staging = new MemoryStateStagingService();
        var sessionId = "session_large_payload_" + Guid.NewGuid();

        // Construct 2MB+ payload with 1,000 subtasks
        var items = new List<SubtaskItem>(1000);
        for (var i = 0; i < 1000; i++)
        {
            items.Add(new SubtaskItem
            {
                Id = i,
                Title = $"Adversarial Task {i:D4}",
                Description = new string('x', 500),
                Metadata = new Dictionary<string, string>
                {
                    ["key1"] = "val_" + i,
                    ["key2"] = new string('y', 100)
                }
            });
        }

        var payload = new LargePayloadDto
        {
            SessionId = sessionId,
            BigText = new string('z', 100_000),
            Items = items
        };

        await staging.SaveProposalAsync(sessionId, payload, TimeSpan.FromMinutes(10));

        var exists = await staging.ExistsAsync(sessionId);
        Assert.True(exists);

        var retrieved = await staging.GetProposalAsync<LargePayloadDto>(sessionId);
        Assert.NotNull(retrieved);
        Assert.Equal(sessionId, retrieved.SessionId);
        Assert.Equal(100_000, retrieved.BigText.Length);
        Assert.Equal(1000, retrieved.Items.Count);
        Assert.Equal(500, retrieved.Items[500].Description.Length);
    }

    [Fact]
    public async Task MemoryStateStaging_ExactBoundaryExpiration_EvictsImmediatelyAtBoundary()
    {
        var timeProvider = new TestTimeProvider();
        var staging = new MemoryStateStagingService(timeProvider);
        var sessionId = "session_exact_ttl";

        await staging.SaveProposalAsync(sessionId, "Simple Data", TimeSpan.FromSeconds(30));

        // 1. Advance 29 seconds -> Still unexpired
        timeProvider.Advance(TimeSpan.FromSeconds(29));
        Assert.True(await staging.ExistsAsync(sessionId));
        Assert.Equal("Simple Data", await staging.GetProposalAsync<string>(sessionId));

        // 2. Advance exactly 1 second (total 30s) -> At or past ExpiresAtUtc
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.False(await staging.ExistsAsync(sessionId));
        Assert.Null(await staging.GetProposalAsync<string>(sessionId));
    }

    [Fact]
    public async Task MemoryStateStaging_NegativeOrZeroTtl_ExpiresImmediately()
    {
        var timeProvider = new TestTimeProvider();
        var staging = new MemoryStateStagingService(timeProvider);

        // Zero TTL
        await staging.SaveProposalAsync("zero_ttl", "Immediate Expiration", TimeSpan.Zero);
        Assert.False(await staging.ExistsAsync("zero_ttl"));
        Assert.Null(await staging.GetProposalAsync<string>("zero_ttl"));

        // Negative TTL
        await staging.SaveProposalAsync("neg_ttl", "Past Expiration", TimeSpan.FromSeconds(-10));
        Assert.False(await staging.ExistsAsync("neg_ttl"));
        Assert.Null(await staging.GetProposalAsync<string>("neg_ttl"));
    }

    [Fact]
    public async Task MemoryStateStaging_HighVolume_TriggersPeriodicCleanupWithoutDeadlock()
    {
        var timeProvider = new TestTimeProvider();
        var staging = new MemoryStateStagingService(timeProvider);

        // Store 700 items with short TTL (5s) to cross the 500-item threshold
        for (var i = 0; i < 700; i++)
        {
            await staging.SaveProposalAsync($"expiring_key_{i}", $"payload_{i}", TimeSpan.FromSeconds(5));
        }

        // Advance time to expire all 700 items
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Next write triggers CleanupExpiredEntries because count > 500
        await staging.SaveProposalAsync("fresh_key", "fresh_value", TimeSpan.FromMinutes(10));

        // All expired items should be purged from active count
        var activeCount = staging.GetActiveCount();
        Assert.Equal(1, activeCount);
        Assert.True(await staging.ExistsAsync("fresh_key"));
    }

    [Fact]
    public async Task MemoryStateStaging_HighConcurrencyReadWriteRemoveStress()
    {
        var staging = new MemoryStateStagingService();
        const int concurrentTasks = 20;
        const int opsPerTask = 100;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, concurrentTasks).Select(taskId => Task.Run(async () =>
        {
            for (var i = 0; i < opsPerTask; i++)
            {
                var sessionId = $"concurrent_sess_{taskId}_{i}";
                var payload = new SubtaskItem
                {
                    Id = i,
                    Title = $"Task {taskId}-{i}",
                    Description = "Concurrent stress test"
                };

                try
                {
                    // Save
                    await staging.SaveProposalAsync(sessionId, payload, TimeSpan.FromMinutes(5));

                    // Exists check
                    var exists = await staging.ExistsAsync(sessionId);
                    if (!exists)
                    {
                        exceptions.Add(new InvalidOperationException($"Key {sessionId} was not found immediately after save"));
                    }

                    // Get
                    var retrieved = await staging.GetProposalAsync<SubtaskItem>(sessionId);
                    if (retrieved == null || retrieved.Id != i)
                    {
                        exceptions.Add(new InvalidOperationException($"Retrieved data corrupted for key {sessionId}"));
                    }

                    // Remove
                    var removed = await staging.RemoveProposalAsync(sessionId);
                    if (!removed)
                    {
                        exceptions.Add(new InvalidOperationException($"Failed to remove {sessionId}"));
                    }

                    // Verify gone
                    if (await staging.ExistsAsync(sessionId))
                    {
                        exceptions.Add(new InvalidOperationException($"Key {sessionId} still exists after removal"));
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task MemoryStateStaging_TypeMismatchOnDeserialization_ThrowsJsonException()
    {
        // Attack: Session stored as SubtaskItem, but consumer attempts to deserialize as an incompatible schema
        var staging = new MemoryStateStagingService();
        var sessionId = "session_type_mismatch";

        // Store a JSON object { "Id": 1, "Title": "Task", ... }
        await staging.SaveProposalAsync(sessionId, new List<string> { "a", "b", "c" }, TimeSpan.FromMinutes(5));

        // Deserializing a JSON array ["a", "b", "c"] into MismatchedSchemaDto ({ RequiredNumericField: int })
        // System.Text.Json throws JsonException when token type doesn't match
        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await staging.GetProposalAsync<MismatchedSchemaDto>(sessionId);
        });
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   \t  \n ")]
    [InlineData("")]
    public async Task MemoryStateStaging_InvalidSessionId_ThrowsArgumentException(string invalidSessionId)
    {
        var staging = new MemoryStateStagingService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            staging.SaveProposalAsync(invalidSessionId, "payload", TimeSpan.FromMinutes(1)));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            staging.GetProposalAsync<string>(invalidSessionId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            staging.RemoveProposalAsync(invalidSessionId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            staging.ExistsAsync(invalidSessionId));
    }

    [Fact]
    public async Task MemoryStateStaging_ExtremelyLongSessionId_HandlesGracefully()
    {
        var staging = new MemoryStateStagingService();
        var veryLongSessionId = "sess_" + new string('a', 5000);

        await staging.SaveProposalAsync(veryLongSessionId, "Long Key Data", TimeSpan.FromMinutes(5));

        var exists = await staging.ExistsAsync(veryLongSessionId);
        Assert.True(exists);

        var retrieved = await staging.GetProposalAsync<string>(veryLongSessionId);
        Assert.Equal("Long Key Data", retrieved);

        var removed = await staging.RemoveProposalAsync(veryLongSessionId);
        Assert.True(removed);
    }

    [Fact]
    public async Task MemoryStateStaging_TtlNearMax_DoesNotOverflowOrThrow()
    {
        // Testing extreme large TTL values
        var staging = new MemoryStateStagingService();
        var sessionId = "sess_large_ttl";

        // 100 years TTL
        var largeTtl = TimeSpan.FromDays(365 * 100);
        await staging.SaveProposalAsync(sessionId, "Long living proposal", largeTtl);

        Assert.True(await staging.ExistsAsync(sessionId));
        var retrieved = await staging.GetProposalAsync<string>(sessionId);
        Assert.Equal("Long living proposal", retrieved);
    }

    [Fact]
    public async Task MemoryStateStaging_TimeSpanMaxValue_ThrowsArgumentOutOfRangeException()
    {
        var staging = new MemoryStateStagingService();
        var sessionId = "sess_max_ttl";

        // TimeSpan.MaxValue causes DateTimeOffset.Add to overflow 9999-12-31
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            staging.SaveProposalAsync(sessionId, "Overflow payload", TimeSpan.MaxValue));
    }

    [Fact]
    public void ProjectScopeGuard_CrossFieldTransposition_BothMismatch_ThrowsProjectScopeException()
    {
        var orgId = Guid.NewGuid();
        var projId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var scope = new ProjectScopeContext(orgId, projId, userId);

        // Transpose targetOrg = scope.ProjectId and targetProj = scope.OrgId
        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, projId, orgId));

        Assert.Equal(ProjectScopeException.DefaultErrorCode, ex.ErrorCode);
    }

    [Fact]
    public async Task MemoryStateStaging_ConcurrentOverwritesAndReadsOnSameKey_NoExceptionsLeaked()
    {
        var staging = new MemoryStateStagingService();
        const string sharedSessionId = "shared_hot_key";
        var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var exceptions = new ConcurrentBag<Exception>();

        var writer = Task.Run(async () =>
        {
            var counter = 0;
            while (!cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    await staging.SaveProposalAsync(sharedSessionId, $"data_{counter++}", TimeSpan.FromMilliseconds(5));
                    await Task.Yield();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        var reader = Task.Run(async () =>
        {
            while (!cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Reading concurrent with rapid 5ms expiration and concurrent writes
                    _ = await staging.GetProposalAsync<string>(sharedSessionId);
                    _ = await staging.ExistsAsync(sharedSessionId);
                    _ = staging.GetActiveCount();
                    await Task.Yield();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        var remover = Task.Run(async () =>
        {
            while (!cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    _ = await staging.RemoveProposalAsync(sharedSessionId);
                    await Task.Yield();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        await Task.WhenAll(writer, reader, remover);
        Assert.Empty(exceptions);
    }

    #endregion
}
