namespace ProjectMgmt.Core.Staging;

/// <summary>
/// Contract for staging transient AI decomposition plans, task refinement sessions, and proposals.
/// Supports both in-memory and distributed cache backends transparently.
/// </summary>
public interface IStateStagingService
{
    /// <summary>
    /// Saves a proposal payload for a session with a specified TTL.
    /// </summary>
    Task SaveProposalAsync<T>(string sessionId, T proposal, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a staged proposal. Returns default/null if not found or expired.
    /// </summary>
    Task<T?> GetProposalAsync<T>(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Removes a staged proposal from storage (e.g. after sprint apply or discard).
    /// </summary>
    Task<bool> RemoveProposalAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether an unexpired proposal exists for the session ID.
    /// </summary>
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
}
