namespace ProjectMgmt.Core.Staging;

/// <summary>
/// Provider interface for distributed cache backends (e.g., Redis).
/// Implemented in Solution or Infrastructure layer.
/// </summary>
public interface IExternalStagingProvider
{
    string ProviderName { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task SetAsync(string key, string serializedValue, TimeSpan ttl, CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<bool> RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
