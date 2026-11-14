namespace ProjectMgmt.Solution.Services.Ai;

using ProjectMgmt.Core.Ai;

public record AiUsageRecord(
    string SessionId,
    Guid UserId,
    Guid ProjectId,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    long DurationMs,
    bool Success,
    DateTime TimestampUtc,
    string? OperationName = null);

public record AiSessionSummary(
    string SessionId,
    Guid ProjectId,
    Guid CreatedByUserId,
    string CreatedByName,
    string SprintGoal,
    int TaskCount,
    decimal TotalStoryPoints,
    string Model,
    DateTime CreatedAtUtc,
    string? Title = null,
    int TurnCount = 0,
    int MemoryCount = 0,
    DateTime? UpdatedAtUtc = null,
    string? LastIntent = null);

public record AiTokenUsageSummary(
    int TotalTokens,
    int PromptTokens,
    int CompletionTokens,
    int TotalCalls,
    int SuccessfulCalls,
    double SuccessRate,
    double EstimatedCostUsd,
    Dictionary<string, int> ModelBreakdown,
    List<AiUsageRecord> RecentHistory);

public record AiQuotaCheckResult(
    bool Allowed,
    string? ErrorCode = null,
    string? Message = null,
    int RequestsInLastMinute = 0,
    int MaxRequestsPerMinute = 0,
    long UserDailyTokensUsed = 0,
    long UserDailyTokenLimit = 0,
    long ProjectDailyTokensUsed = 0,
    long ProjectDailyTokenLimit = 0);

public interface IAiTokenUsageTracker
{
    void RecordUsage(AiUsageRecord record);
    AiTokenUsageSummary GetUserUsageSummary(Guid userId);
    AiTokenUsageSummary GetProjectUsageSummary(Guid projectId);
    void RecordSession(AiSessionSummary session);
    List<AiSessionSummary> GetProjectSessions(Guid projectId, int limit = 10);
    AiQuotaCheckResult CheckPreExecutionQuota(Guid userId, Guid projectId, AiQuotaOptions? quotaOverride = null);
}

