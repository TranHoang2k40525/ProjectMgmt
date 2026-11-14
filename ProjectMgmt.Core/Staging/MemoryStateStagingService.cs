using System.Collections.Concurrent;
using System.Text.Json;

namespace ProjectMgmt.Core.Staging;

/// <summary>
/// Pure in-memory state staging engine with thread-safe ConcurrentDictionary,
/// automatic TTL expiration, and JSON immutability guarantees.
/// </summary>
public sealed class MemoryStateStagingService : IStateStagingService
{
    private sealed record StagedEntry(string JsonPayload, Type TargetType, DateTimeOffset ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, StagedEntry> _store = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public MemoryStateStagingService(TimeProvider? timeProvider = null, JsonSerializerOptions? jsonOptions = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    public Task SaveProposalAsync<T>(string sessionId, T proposal, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(proposal);

        var expiresAtUtc = _timeProvider.GetUtcNow().Add(ttl);
        var json = JsonSerializer.Serialize(proposal, _jsonOptions);
        var entry = new StagedEntry(json, typeof(T), expiresAtUtc);

        _store[sessionId] = entry;

        // Lightweight periodic eviction when dictionary grows
        if (_store.Count > 500)
        {
            CleanupExpiredEntries();
        }

        return Task.CompletedTask;
    }

    public Task<T?> GetProposalAsync<T>(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_store.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult<T?>(default);
        }

        if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
        {
            _store.TryRemove(sessionId, out _);
            return Task.FromResult<T?>(default);
        }

        var result = JsonSerializer.Deserialize<T>(entry.JsonPayload, _jsonOptions);
        return Task.FromResult(result);
    }

    public Task<bool> RemoveProposalAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var removed = _store.TryRemove(sessionId, out _);
        return Task.FromResult(removed);
    }

    public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_store.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult(false);
        }

        if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
        {
            _store.TryRemove(sessionId, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public int GetActiveCount()
    {
        CleanupExpiredEntries();
        return _store.Count;
    }

    private void CleanupExpiredEntries()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var kvp in _store)
        {
            if (now >= kvp.Value.ExpiresAtUtc)
            {
                _store.TryRemove(kvp.Key, out _);
            }
        }
    }
}
