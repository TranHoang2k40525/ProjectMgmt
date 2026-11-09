using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Resilience;

namespace ProjectMgmt.Core.Staging;

/// <summary>
/// Resilient composite staging service.
/// Uses external provider (Redis) when enabled and available; seamlessly falls back
/// to MemoryStateStagingService with circuit breaking to avoid latency spikes.
/// </summary>
public sealed class ResilientStateStagingService : IStateStagingService
{
    private readonly IExternalStagingProvider? _primaryProvider;
    private readonly MemoryStateStagingService _memoryFallback;
    private readonly CircuitBreakerPolicy _circuitBreaker;
    private readonly ILogger<ResilientStateStagingService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _redisEnabled;

    public ResilientStateStagingService(
        MemoryStateStagingService memoryFallback,
        IExternalStagingProvider? primaryProvider = null,
        CircuitBreakerPolicy? circuitBreaker = null,
        bool redisEnabled = false,
        TimeSpan? timeout = null,
        ILogger<ResilientStateStagingService>? logger = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        _memoryFallback = memoryFallback ?? throw new ArgumentNullException(nameof(memoryFallback));
        _primaryProvider = primaryProvider;
        _redisEnabled = redisEnabled && primaryProvider != null;
        _logger = logger ?? NullLogger<ResilientStateStagingService>.Instance;
        _circuitBreaker = circuitBreaker ?? new CircuitBreakerPolicy(
            serviceName: "RedisStaging",
            failureThreshold: 3,
            breakDuration: TimeSpan.FromSeconds(60),
            executionTimeout: timeout ?? TimeSpan.FromMilliseconds(2000));
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    public async Task SaveProposalAsync<T>(string sessionId, T proposal, TimeSpan ttl, CancellationToken ct = default)
    {
        if (!_redisEnabled || _primaryProvider == null)
        {
            await _memoryFallback.SaveProposalAsync(sessionId, proposal, ttl, ct).ConfigureAwait(false);
            return;
        }

        var json = JsonSerializer.Serialize(proposal, _jsonOptions);

        await _circuitBreaker.ExecuteWithFallbackAsync(
            primaryAction: async token =>
            {
                await _primaryProvider.SetAsync(sessionId, json, ttl, token).ConfigureAwait(false);
            },
            fallbackAction: async token =>
            {
                await _memoryFallback.SaveProposalAsync(sessionId, proposal, ttl, token).ConfigureAwait(false);
            },
            ct: ct,
            onFallback: (reason, ex) =>
            {
                _logger.LogWarning(ex, "[ResilientStaging] {Reason} SessionId={SessionId}", reason, sessionId);
            }).ConfigureAwait(false);
    }

    public async Task<T?> GetProposalAsync<T>(string sessionId, CancellationToken ct = default)
    {
        if (!_redisEnabled || _primaryProvider == null)
        {
            return await _memoryFallback.GetProposalAsync<T>(sessionId, ct).ConfigureAwait(false);
        }

        return await _circuitBreaker.ExecuteWithFallbackAsync<T?>(
            primaryAction: async token =>
            {
                var json = await _primaryProvider.GetAsync(sessionId, token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(json))
                {
                    // Fallback to memory in case the session was saved during a previous fallback period
                    return await _memoryFallback.GetProposalAsync<T>(sessionId, token).ConfigureAwait(false);
                }
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            },
            fallbackAction: async token =>
            {
                return await _memoryFallback.GetProposalAsync<T>(sessionId, token).ConfigureAwait(false);
            },
            ct: ct,
            onFallback: (reason, ex) =>
            {
                _logger.LogWarning(ex, "[ResilientStaging] {Reason} SessionId={SessionId}", reason, sessionId);
            }).ConfigureAwait(false);
    }

    public async Task<bool> RemoveProposalAsync(string sessionId, CancellationToken ct = default)
    {
        var memoryRemoved = await _memoryFallback.RemoveProposalAsync(sessionId, ct).ConfigureAwait(false);

        if (_redisEnabled && _primaryProvider != null && _circuitBreaker.AllowExecution())
        {
            try
            {
                var redisRemoved = await _primaryProvider.RemoveAsync(sessionId, ct).ConfigureAwait(false);
                return memoryRemoved || redisRemoved;
            }
            catch (Exception ex)
            {
                _circuitBreaker.RecordFailure(ex);
                _logger.LogWarning(ex, "[ResilientStaging] Failed to remove proposal from Redis. SessionId={SessionId}", sessionId);
            }
        }

        return memoryRemoved;
    }

    public async Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        if (_redisEnabled && _primaryProvider != null && _circuitBreaker.AllowExecution())
        {
            try
            {
                var existsInRedis = await _primaryProvider.ExistsAsync(sessionId, ct).ConfigureAwait(false);
                if (existsInRedis) return true;
            }
            catch (Exception ex)
            {
                _circuitBreaker.RecordFailure(ex);
                _logger.LogWarning(ex, "[ResilientStaging] Redis Exists check failed. SessionId={SessionId}", sessionId);
            }
        }

        return await _memoryFallback.ExistsAsync(sessionId, ct).ConfigureAwait(false);
    }
}
