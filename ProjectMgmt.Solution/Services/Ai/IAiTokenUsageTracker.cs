namespace ProjectMgmt.Solution.Services.Ai;

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
    DateTime CreatedAtUtc);

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

public interface IAiTokenUsageTracker
{
    void RecordUsage(AiUsageRecord record);
    AiTokenUsageSummary GetUserUsageSummary(Guid userId);
    AiTokenUsageSummary GetProjectUsageSummary(Guid projectId);
    void RecordSession(AiSessionSummary session);
    List<AiSessionSummary> GetProjectSessions(Guid projectId, int limit = 10);
}

