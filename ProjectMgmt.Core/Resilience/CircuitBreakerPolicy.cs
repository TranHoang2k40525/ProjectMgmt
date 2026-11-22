namespace ProjectMgmt.Core.Resilience;

public enum CircuitState
{
    Closed = 0,
    Open = 1,
    HalfOpen = 2
}

/// <summary>
/// Thread-safe circuit breaker policy for external services (Redis, Qdrant, Langfuse).
/// Automatically detects failures, trips to Open state, and allows self-healing probes.
/// </summary>
public sealed class CircuitBreakerPolicy
{
    private readonly object _syncLock = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly TimeSpan _executionTimeout;
    private readonly TimeProvider _timeProvider;

    private int _consecutiveFailures;
    private DateTimeOffset _lastFailureTimeUtc;
    private DateTimeOffset _breakExpiryTimeUtc;
    private CircuitState _state = CircuitState.Closed;

    public string ServiceName { get; }
    public CircuitState State
    {
        get
        {
            lock (_syncLock)
            {
                UpdateStateIfNeeded();
                return _state;
            }
        }
    }

    public CircuitBreakerPolicy(
        string serviceName,
        int failureThreshold = 3,
        TimeSpan? breakDuration = null,
        TimeSpan? executionTimeout = null,
        TimeProvider? timeProvider = null)
    {
        ServiceName = string.IsNullOrWhiteSpace(serviceName) ? "ExternalService" : serviceName;
        _failureThreshold = Math.Max(1, failureThreshold);
        _breakDuration = breakDuration ?? TimeSpan.FromSeconds(60);
        _executionTimeout = executionTimeout ?? TimeSpan.FromMilliseconds(2000);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool AllowExecution()
    {
        lock (_syncLock)
        {
            UpdateStateIfNeeded();
            return _state != CircuitState.Open;
        }
    }

    public void RecordSuccess()
    {
        lock (_syncLock)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

    public void RecordFailure(Exception? ex = null)
    {
        lock (_syncLock)
        {
            _consecutiveFailures++;
            _lastFailureTimeUtc = _timeProvider.GetUtcNow();

            if (_consecutiveFailures >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _breakExpiryTimeUtc = _lastFailureTimeUtc + _breakDuration;
            }
        }
    }

    private void UpdateStateIfNeeded()
    {
        if (_state == CircuitState.Open && _timeProvider.GetUtcNow() >= _breakExpiryTimeUtc)
        {
            _state = CircuitState.HalfOpen;
        }
    }

    /// <summary>
    /// Executes the primary action with timeout. If the circuit is open or primary fails, executes fallback.
    /// </summary>
    public async Task<T> ExecuteWithFallbackAsync<T>(
        Func<CancellationToken, Task<T>> primaryAction,
        Func<CancellationToken, Task<T>> fallbackAction,
        Action<string, Exception?>? onFallback = null,
        CancellationToken ct = default)
    {
        if (!AllowExecution())
        {
            onFallback?.Invoke($"Circuit breaker for {ServiceName} is OPEN. Directly executing fallback.", null);
            return await fallbackAction(ct).ConfigureAwait(false);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_executionTimeout);

        try
        {
            var result = await primaryAction(timeoutCts.Token).ConfigureAwait(false);
            RecordSuccess();
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            RecordFailure(ex);
            onFallback?.Invoke($"Primary action for {ServiceName} failed ({ex.GetType().Name}: {ex.Message}). Executing fallback.", ex);
            return await fallbackAction(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Non-generic overload for void/Task operations.
    /// </summary>
    public async Task ExecuteWithFallbackAsync(
        Func<CancellationToken, Task> primaryAction,
        Func<CancellationToken, Task> fallbackAction,
        Action<string, Exception?>? onFallback = null,
        CancellationToken ct = default)
    {
        if (!AllowExecution())
        {
            onFallback?.Invoke($"Circuit breaker for {ServiceName} is OPEN. Directly executing fallback.", null);
            await fallbackAction(ct).ConfigureAwait(false);
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_executionTimeout);

        try
        {
            await primaryAction(timeoutCts.Token).ConfigureAwait(false);
            RecordSuccess();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            RecordFailure(ex);
            onFallback?.Invoke($"Primary action for {ServiceName} failed ({ex.GetType().Name}: {ex.Message}). Executing fallback.", ex);
            await fallbackAction(ct).ConfigureAwait(false);
        }
    }
}
