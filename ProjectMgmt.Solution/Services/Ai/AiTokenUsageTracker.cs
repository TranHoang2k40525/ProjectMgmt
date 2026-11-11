namespace ProjectMgmt.Solution.Services.Ai;

using System.Collections.Concurrent;

public sealed class AiTokenUsageTracker : IAiTokenUsageTracker
{
    private readonly ConcurrentBag<AiUsageRecord> _usageRecords = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<AiSessionSummary>> _projectSessions = new();

    public void RecordUsage(AiUsageRecord record)
    {
        _usageRecords.Add(record);
    }

    public void RecordSession(AiSessionSummary session)
    {
        var bag = _projectSessions.GetOrAdd(session.ProjectId, _ => new ConcurrentBag<AiSessionSummary>());
        bag.Add(session);
    }

    public AiTokenUsageSummary GetUserUsageSummary(Guid userId)
    {
        var userRecords = _usageRecords
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.TimestampUtc)
            .ToList();

        return BuildSummary(userRecords);
    }

    public AiTokenUsageSummary GetProjectUsageSummary(Guid projectId)
    {
        var projectRecords = _usageRecords
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.TimestampUtc)
            .ToList();

        return BuildSummary(projectRecords);
    }

    public List<AiSessionSummary> GetProjectSessions(Guid projectId, int limit = 10)
    {
        if (!_projectSessions.TryGetValue(projectId, out var bag))
        {
            return [];
        }

        return bag
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(limit)
            .ToList();
    }

    private static AiTokenUsageSummary BuildSummary(List<AiUsageRecord> records)
    {
        var prompt = records.Sum(r => r.PromptTokens);
        var completion = records.Sum(r => r.CompletionTokens);
        var total = prompt + completion;
        var totalCalls = records.Count;
        var successCalls = records.Count(r => r.Success);
        var successRate = totalCalls > 0 ? Math.Round((double)successCalls / totalCalls * 100, 1) : 100.0;

        // Pricing approx: Gemini Flash ~$0.075 / 1M prompt tokens, ~$0.30 / 1M completion tokens
        var cost = (prompt * 0.0000001) + (completion * 0.0000004);

        var modelBreakdown = records
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Model) ? "Gemini 2.5 Flash" : r.Model)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.PromptTokens + r.CompletionTokens));

        return new AiTokenUsageSummary(
            TotalTokens: total,
            PromptTokens: prompt,
            CompletionTokens: completion,
            TotalCalls: totalCalls,
            SuccessfulCalls: successCalls,
            SuccessRate: successRate,
            EstimatedCostUsd: Math.Round(cost, 4),
            ModelBreakdown: modelBreakdown,
            RecentHistory: records.Take(15).ToList());
    }
}
