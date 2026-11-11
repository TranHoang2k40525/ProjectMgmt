using System.Net.Sockets;
using ProjectMgmt.Core.Resilience;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.Core.Tracing;
using ProjectMgmt.Core.VectorSearch;
using Xunit;

namespace ProjectMgmt.Tests.Core;

/// <summary>
/// Empirical Adversarial Test Suite by Challenger 2 for Milestone M1.
/// Rigorously tests CircuitBreakerPolicy state transitions, concurrency,
/// timeout semantics, and ensures ResilientVectorSearchService and ResilientTraceService
/// never leak unhandled exceptions under diverse external service outages.
/// </summary>
public class ChallengerResilienceTests
{
    #region CircuitBreakerPolicy State Transitions & Edge Cases

    [Fact]
    public void CircuitBreaker_FullStateLifecycle_Closed_Open_HalfOpen_Closed()
    {
        var timeProvider = new TestTimeProvider();
        var cb = new CircuitBreakerPolicy(
            serviceName: "LifecycleService",
            failureThreshold: 3,
            breakDuration: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        // 1. Initial state: Closed
        Assert.Equal(CircuitState.Closed, cb.State);
        Assert.True(cb.AllowExecution());

        // 2. Threshold - 1 failures: Still Closed
        cb.RecordFailure(new SocketException(10061));
        cb.RecordFailure(new TimeoutException("Redis timeout"));
        Assert.Equal(CircuitState.Closed, cb.State);
        Assert.True(cb.AllowExecution());

        // 3. 3rd failure: Trips to Open
        cb.RecordFailure(new HttpRequestException("503 Service Unavailable"));
        Assert.Equal(CircuitState.Open, cb.State);
        Assert.False(cb.AllowExecution());

        // 4. During break duration: Stays Open, rejects execution
        timeProvider.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(CircuitState.Open, cb.State);
        Assert.False(cb.AllowExecution());

        // 5. Cooldown expires: Transitions to HalfOpen
        timeProvider.Advance(TimeSpan.FromSeconds(16));
        Assert.Equal(CircuitState.HalfOpen, cb.State);
        Assert.True(cb.AllowExecution());

        // 6. Successful probe in HalfOpen: Resets to Closed
        cb.RecordSuccess();
        Assert.Equal(CircuitState.Closed, cb.State);
        Assert.True(cb.AllowExecution());

        // 7. Verify failure counter was reset: 1 failure should not trip breaker
        cb.RecordFailure(new SocketException(10061));
        Assert.Equal(CircuitState.Closed, cb.State);
    }

    [Fact]
    public void CircuitBreaker_HalfOpen_ProbeFails_TripsImmediatelyBackToOpen()
    {
        var timeProvider = new TestTimeProvider();
        var cb = new CircuitBreakerPolicy(
            serviceName: "HalfOpenFailService",
            failureThreshold: 3,
            breakDuration: TimeSpan.FromSeconds(20),
            timeProvider: timeProvider);

        // Trip to Open
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitState.Open, cb.State);

        // Advance past cooldown to HalfOpen
        timeProvider.Advance(TimeSpan.FromSeconds(21));
        Assert.Equal(CircuitState.HalfOpen, cb.State);

        // Probe fails! Must trip back to Open immediately without needing 3 failures
        cb.RecordFailure(new SocketException(10061));
        Assert.Equal(CircuitState.Open, cb.State);
        Assert.False(cb.AllowExecution());
    }

    [Fact]
    public void CircuitBreaker_SuccessWhileClosed_ResetsConsecutiveFailureCount()
    {
        var cb = new CircuitBreakerPolicy("ResetCountService", failureThreshold: 3);

        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitState.Closed, cb.State);

        // Success resets counter
        cb.RecordSuccess();

        // 2 more failures should NOT trip breaker
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitState.Closed, cb.State);

        // 3rd consecutive failure trips it
        cb.RecordFailure();
        Assert.Equal(CircuitState.Open, cb.State);
    }

    [Fact]
    public async Task CircuitBreaker_WhenOpen_RejectsPrimaryImmediatelyWithoutInvokingIt()
    {
        var cb = new CircuitBreakerPolicy("FastRejectService", failureThreshold: 1);
        cb.RecordFailure(); // Trips to Open

        var primaryInvoked = false;
        var fallbackInvoked = false;

        var result = await cb.ExecuteWithFallbackAsync(
            primaryAction: _ =>
            {
                primaryInvoked = true;
                return Task.FromResult("primary");
            },
            fallbackAction: _ =>
            {
                fallbackInvoked = true;
                return Task.FromResult("fallback");
            });

        Assert.False(primaryInvoked, "Primary action must NOT be invoked when circuit is Open.");
        Assert.True(fallbackInvoked, "Fallback action MUST be invoked when circuit is Open.");
        Assert.Equal("fallback", result);
    }

    [Fact]
    public async Task CircuitBreaker_ExecutionTimeout_FailsAndExecutesFallback()
    {
        var cb = new CircuitBreakerPolicy(
            serviceName: "TimeoutService",
            failureThreshold: 1,
            executionTimeout: TimeSpan.FromMilliseconds(50));

        var fallbackExecuted = false;

        var result = await cb.ExecuteWithFallbackAsync(
            primaryAction: async token =>
            {
                // Simulate a hanging external network call respecting cancellation token
                await Task.Delay( TimeSpan.FromMilliseconds(500), token);
                return "hanging_result";
            },
            fallbackAction: _ =>
            {
                fallbackExecuted = true;
                return Task.FromResult("timeout_fallback");
            });

        Assert.True(fallbackExecuted);
        Assert.Equal("timeout_fallback", result);
        Assert.Equal(CircuitState.Open, cb.State); // 1 failure tripped threshold of 1
    }

    [Fact]
    public async Task CircuitBreaker_CallerCancellation_RethrowsAndDoesNotTripBreaker()
    {
        var cb = new CircuitBreakerPolicy("CallerCancelService", failureThreshold: 1);

        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel(); // Caller cancels upfront

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await cb.ExecuteWithFallbackAsync(
                primaryAction: token =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult("never");
                },
                fallbackAction: _ => Task.FromResult("fallback"),
                ct: callerCts.Token);
        });

        // Breaker must NOT record caller cancellation as a service failure!
        Assert.Equal(CircuitState.Closed, cb.State);
    }

    [Fact]
    public async Task CircuitBreaker_ConcurrentStressHarness_ThreadSafety()
    {
        var cb = new CircuitBreakerPolicy(
            serviceName: "StressService",
            failureThreshold: 5,
            breakDuration: TimeSpan.FromMilliseconds(50));

        var totalOperations = 100;
        var tasks = new Task[totalOperations];
        var fallbackCount = 0;
        var successCount = 0;

        for (int i = 0; i < totalOperations; i++)
        {
            var iteration = i;
            tasks[i] = Task.Run(async () =>
            {
                var result = await cb.ExecuteWithFallbackAsync(
                    primaryAction: _ =>
                    {
                        if (iteration % 3 == 0)
                        {
                            throw new SocketException(10061);
                        }
                        return Task.FromResult("OK");
                    },
                    fallbackAction: _ => Task.FromResult("FALLBACK"));

                if (result == "OK")
                {
                    Interlocked.Increment(ref successCount);
                }
                else
                {
                    Interlocked.Increment(ref fallbackCount);
                }
            });
        }

        await Task.WhenAll(tasks);

        Assert.Equal(totalOperations, successCount + fallbackCount);
        // State must be either Closed or Open without corrupting
        Assert.True(cb.State == CircuitState.Closed || cb.State == CircuitState.Open || cb.State == CircuitState.HalfOpen);
    }

    #endregion

    #region ResilientVectorSearchService Empirical Outage Tests

    [Fact]
    public async Task VectorSearch_SimulateSocketException_FallsBackSafelyWithoutThrowing()
    {
        var failingQdrant = new OutageVectorSearchProvider(new SocketException(10061));
        var keywordFallback = new MockKeywordFallbackProvider();
        var service = new ResilientVectorSearchService(
            primaryProvider: failingQdrant,
            keywordFallbackProvider: keywordFallback,
            qdrantEnabled: true);

        var results = await service.SearchRelevantSpecsAsync(Guid.NewGuid(), "Quản lý Sprint");

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal("Spec Đăng nhập", results[0].Title);
    }

    [Fact]
    public async Task VectorSearch_SimulateTaskCanceledException_FallsBackSafelyWithoutThrowing()
    {
        var failingQdrant = new OutageVectorSearchProvider(new TaskCanceledException("Qdrant HTTP timeout"));
        var keywordFallback = new MockKeywordFallbackProvider();
        var service = new ResilientVectorSearchService(
            primaryProvider: failingQdrant,
            keywordFallbackProvider: keywordFallback,
            qdrantEnabled: true);

        var results = await service.SearchRelevantSpecsAsync(Guid.NewGuid(), "Task breakdown");

        Assert.NotNull(results);
        Assert.Single(results);
    }

    [Fact]
    public async Task VectorSearch_DoubleOutage_QdrantAndSqlFallbackBothFail_ReturnsEmptyListWithoutThrowing()
    {
        var failingQdrant = new OutageVectorSearchProvider(new SocketException(10061));
        var failingKeyword = new FailingKeywordFallbackProvider(new InvalidOperationException("MySQL connection pool exhausted"));
        var service = new ResilientVectorSearchService(
            primaryProvider: failingQdrant,
            keywordFallbackProvider: failingKeyword,
            qdrantEnabled: true);

        // Both primary and fallback crash - must safely return empty list
        var results = await service.SearchRelevantSpecsAsync(Guid.NewGuid(), "Critical spec");

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task VectorSearch_UpsertSpecChunk_WhenQdrantOffline_ReturnsFalseWithoutThrowing()
    {
        var failingQdrant = new OutageVectorSearchProvider(new SocketException(10061));
        var service = new ResilientVectorSearchService(
            primaryProvider: failingQdrant,
            qdrantEnabled: true);

        var success = await service.UpsertSpecChunkAsync(
            Guid.NewGuid(),
            "chunk-1",
            "Title",
            "Content",
            "path/to/spec.md");

        Assert.False(success);
    }

    #endregion

    #region ResilientTraceService Empirical Outage Tests

    [Fact]
    public async Task TraceService_SimulateSocketException_NeverThrows()
    {
        var failingExporter = new OutageTraceExporter(new SocketException(10061));
        var service = new ResilientTraceService(
            externalExporter: failingExporter,
            langfuseEnabled: true);

        // StartTraceAsync must return traceId without throwing
        var traceId = await service.StartTraceAsync("trace-err-1", Guid.NewGuid(), Guid.NewGuid(), "Session");
        Assert.Equal("trace-err-1", traceId);

        // RecordSpanAsync must not throw
        await service.RecordSpanAsync(new AiTraceSpan
        {
            TraceId = traceId,
            SpanId = "span-1",
            Name = "span",
            SpanType = "custom"
        });

        // RecordGenerationAsync must not throw
        await service.RecordGenerationAsync(traceId, "gemini-2.5-flash", "prompt", "output", 10, 20, 100);

        // RecordToolCallAsync must not throw
        await service.RecordToolCallAsync(traceId, "SpecHubTools", "{}", "{}", 50, true);

        // RecordFeedbackAsync must not throw
        await service.RecordFeedbackAsync(traceId, "thumbs_up", 1.0m);
    }

    [Fact]
    public async Task TraceService_SimulateTaskCanceledException_NeverThrows()
    {
        var failingExporter = new OutageTraceExporter(new TaskCanceledException("Langfuse request timed out"));
        var service = new ResilientTraceService(
            externalExporter: failingExporter,
            langfuseEnabled: true);

        var traceId = await service.StartTraceAsync("trace-cancel", Guid.NewGuid(), Guid.NewGuid(), "Session");
        Assert.Equal("trace-cancel", traceId);

        await service.RecordGenerationAsync(traceId, "claude-3-7-sonnet", "prompt", "output", 15, 25, 200);
    }

    [Fact]
    public async Task TraceService_RapidConcurrentTraceCalls_UnderContinuousFailure_RemainsStable()
    {
        var failingExporter = new OutageTraceExporter(new SocketException(10061));
        var service = new ResilientTraceService(
            externalExporter: failingExporter,
            langfuseEnabled: true);

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                var tid = $"trace-{idx}";
                await service.StartTraceAsync(tid, Guid.NewGuid(), Guid.NewGuid(), $"Session-{idx}");
                await service.RecordGenerationAsync(tid, "local-model", "p", "c", 5, 5, 10);
            }));
        }

        // Must all complete without a single unhandled exception
        await Task.WhenAll(tasks);
    }

    #endregion

    #region ResilientStateStagingService Empirical Outage Tests

    [Fact]
    public async Task StagingService_SimulateRedisConnectionException_FallsBackSafelyToMemory()
    {
        var memory = new MemoryStateStagingService();
        var failingRedis = new OutageStagingProvider(new SocketException(10061));
        var service = new ResilientStateStagingService(
            memoryFallback: memory,
            primaryProvider: failingRedis,
            redisEnabled: true);

        var sessionId = "session_redis_down";
        var payload = new TestProposal { SprintGoal = "Goal", TaskCount = 5 };

        // SaveProposalAsync must not throw
        await service.SaveProposalAsync(sessionId, payload, TimeSpan.FromMinutes(5));

        // GetProposalAsync must return from memory fallback
        var retrieved = await service.GetProposalAsync<TestProposal>(sessionId);
        Assert.NotNull(retrieved);
        Assert.Equal("Goal", retrieved.SprintGoal);
        Assert.Equal(5, retrieved.TaskCount);

        // ExistsAsync must return true from memory fallback
        var exists = await service.ExistsAsync(sessionId);
        Assert.True(exists);

        // RemoveProposalAsync must clean up memory fallback without throwing
        var removed = await service.RemoveProposalAsync(sessionId);
        Assert.True(removed);

        // ExistsAsync should now be false
        Assert.False(await service.ExistsAsync(sessionId));
    }

    #endregion
}

public class TestProposal
{
    public string SprintGoal { get; set; } = string.Empty;
    public int TaskCount { get; set; }
}

public class OutageVectorSearchProvider : IExternalVectorSearchProvider
{
    private readonly Exception _outageException;
    public string ProviderName => "OutageQdrant";

    public OutageVectorSearchProvider(Exception outageException)
    {
        _outageException = outageException;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(Guid projectId, string query, int topK, CancellationToken ct) =>
        throw _outageException;

    public Task<bool> UpsertChunkAsync(Guid projectId, string chunkId, string title, string content, string sourcePath, IReadOnlyDictionary<string, string>? metadata, CancellationToken ct) =>
        throw _outageException;
}

public class FailingKeywordFallbackProvider : ISpecKeywordFallbackProvider
{
    private readonly Exception _fallbackException;

    public FailingKeywordFallbackProvider(Exception fallbackException)
    {
        _fallbackException = fallbackException;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchKeywordAsync(Guid projectId, string query, int topK, CancellationToken ct) =>
        throw _fallbackException;
}

public class OutageTraceExporter : IExternalTraceExporter
{
    private readonly Exception _outageException;
    public string ExporterName => "OutageLangfuse";

    public OutageTraceExporter(Exception outageException)
    {
        _outageException = outageException;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    public Task ExportTraceAsync(AiTraceSession session, CancellationToken ct) =>
        throw _outageException;

    public Task ExportSpanAsync(AiTraceSpan span, CancellationToken ct) =>
        throw _outageException;
}

public class OutageStagingProvider : IExternalStagingProvider
{
    private readonly Exception _outageException;
    public string ProviderName => "OutageRedis";

    public OutageStagingProvider(Exception outageException)
    {
        _outageException = outageException;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    public Task SetAsync(string key, string serializedValue, TimeSpan ttl, CancellationToken ct) =>
        throw _outageException;

    public Task<string?> GetAsync(string key, CancellationToken ct) =>
        throw _outageException;

    public Task<bool> RemoveAsync(string key, CancellationToken ct) =>
        throw _outageException;

    public Task<bool> ExistsAsync(string key, CancellationToken ct) =>
        throw _outageException;
}
