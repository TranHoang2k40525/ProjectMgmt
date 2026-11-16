using ProjectMgmt.Core.Resilience;
using Xunit;

namespace ProjectMgmt.Tests.Core;

public class CircuitBreakerPolicyTests
{
    [Fact]
    public void InitialState_IsClosed_AndAllowsExecution()
    {
        var cb = new CircuitBreakerPolicy("TestService", failureThreshold: 3);

        Assert.Equal(CircuitState.Closed, cb.State);
        Assert.True(cb.AllowExecution());
    }

    [Fact]
    public void FailuresBelowThreshold_RemainClosed()
    {
        var cb = new CircuitBreakerPolicy("TestService", failureThreshold: 3);

        cb.RecordFailure();
        cb.RecordFailure();

        Assert.Equal(CircuitState.Closed, cb.State);
        Assert.True(cb.AllowExecution());
    }

    [Fact]
    public void TripsToOpen_WhenFailureThresholdReached()
    {
        var cb = new CircuitBreakerPolicy("TestService", failureThreshold: 3);

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();

        Assert.Equal(CircuitState.Open, cb.State);
        Assert.False(cb.AllowExecution());
    }

    [Fact]
    public async Task ExecuteWithFallback_WhenOpen_DirectlyExecutesFallback()
    {
        var cb = new CircuitBreakerPolicy("TestService", failureThreshold: 1);
        cb.RecordFailure(); // Trips to Open

        var primaryExecuted = false;
        var fallbackExecuted = false;

        var result = await cb.ExecuteWithFallbackAsync(
            primaryAction: _ =>
            {
                primaryExecuted = true;
                return Task.FromResult("primary");
            },
            fallbackAction: _ =>
            {
                fallbackExecuted = true;
                return Task.FromResult("fallback");
            });

        Assert.False(primaryExecuted);
        Assert.True(fallbackExecuted);
        Assert.Equal("fallback", result);
    }

    [Fact]
    public async Task ExecuteWithFallback_WhenPrimaryFails_RecordsFailureAndExecutesFallback()
    {
        var cb = new CircuitBreakerPolicy("TestService", failureThreshold: 3);

        var result = await cb.ExecuteWithFallbackAsync(
            primaryAction: _ => throw new HttpRequestException("Server error"),
            fallbackAction: _ => Task.FromResult("fallback_value"));

        Assert.Equal("fallback_value", result);
        Assert.Equal(CircuitState.Closed, cb.State); // 1 failure recorded, threshold is 3
    }

    [Fact]
    public void TransitionsToHalfOpen_AfterBreakDurationExpires()
    {
        var timeProvider = new TestTimeProvider();
        var cb = new CircuitBreakerPolicy(
            "TestService",
            failureThreshold: 2,
            breakDuration: TimeSpan.FromSeconds(30),
            timeProvider: timeProvider);

        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitState.Open, cb.State);

        // Advance before break duration
        timeProvider.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(CircuitState.Open, cb.State);

        // Advance past break duration
        timeProvider.Advance(TimeSpan.FromSeconds(16));
        Assert.Equal(CircuitState.HalfOpen, cb.State);
        Assert.True(cb.AllowExecution());
    }

    [Fact]
    public void Success_ResetsStateToClosed_AndClearsFailureCount()
    {
        var cb = new CircuitBreakerPolicy("TestService", failureThreshold: 3);

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess();

        // One more failure should not trip breaker since count was reset
        cb.RecordFailure();
        Assert.Equal(CircuitState.Closed, cb.State);
    }
}
