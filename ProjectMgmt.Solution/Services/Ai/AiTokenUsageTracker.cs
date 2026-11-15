namespace ProjectMgmt.Solution.Services.Ai;

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.Ai;

public sealed class AiTokenUsageTracker : IAiTokenUsageTracker
{
    private readonly ConcurrentBag<AiUsageRecord> _usageRecords = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<AiSessionSummary>> _projectSessions = new();
    private readonly ConcurrentDictionary<Guid, Queue<DateTime>> _userRequestWindow = new();
    private readonly AiQuotaOptions _defaultQuota;

    public AiTokenUsageTracker(IOptions<AiOptions>? aiOptions = null)
    {
        _defaultQuota = aiOptions?.Value?.Quota ?? new AiQuotaOptions();
    }

    public AiQuotaCheckResult CheckPreExecutionQuota(Guid userId, Guid projectId, AiQuotaOptions? quotaOverride = null)
    {
        var quota = quotaOverride ?? _defaultQuota;
        if (!quota.Enabled)
        {
            return new AiQuotaCheckResult(true);
        }

        var nowUtc = DateTime.UtcNow;
        var cutoffWindow = nowUtc.AddMinutes(-1);
        var utcToday = nowUtc.Date;

        var userDailyTokens = _usageRecords
            .Where(r => r.UserId == userId && r.TimestampUtc.Date == utcToday)
            .Sum(r => (long)r.PromptTokens + r.CompletionTokens);

        if (quota.DailyTokenLimitPerUser > 0 && userDailyTokens >= quota.DailyTokenLimitPerUser)
        {
            return new AiQuotaCheckResult(
                Allowed: false,
                ErrorCode: "QUOTA_EXCEEDED",
                Message: $"Bạn đã đạt giới hạn sử dụng Token AI trong ngày ({userDailyTokens:N0}/{quota.DailyTokenLimitPerUser:N0} tokens). Vui lòng thử lại vào ngày mai hoặc liên hệ quản trị viên.",
                UserDailyTokensUsed: userDailyTokens,
                UserDailyTokenLimit: quota.DailyTokenLimitPerUser);
        }

        var projectDailyTokens = _usageRecords
            .Where(r => r.ProjectId == projectId && r.TimestampUtc.Date == utcToday)
            .Sum(r => (long)r.PromptTokens + r.CompletionTokens);

        if (quota.DailyTokenLimitPerProject > 0 && projectDailyTokens >= quota.DailyTokenLimitPerProject)
        {
            return new AiQuotaCheckResult(
                Allowed: false,
                ErrorCode: "QUOTA_EXCEEDED",
                Message: $"Dự án đã đạt hạn mức Token AI trong ngày ({projectDailyTokens:N0}/{quota.DailyTokenLimitPerProject:N0} tokens). Vui lòng thử lại sau hoặc nâng hạn mức dự án.",
                UserDailyTokensUsed: userDailyTokens,
                UserDailyTokenLimit: quota.DailyTokenLimitPerUser,
                ProjectDailyTokensUsed: projectDailyTokens,
                ProjectDailyTokenLimit: quota.DailyTokenLimitPerProject);
        }

        int requestsInLastMinute = 0;
        if (quota.MaxRequestsPerMinutePerUser > 0 && userId != Guid.Empty)
        {
            var queue = _userRequestWindow.GetOrAdd(userId, _ => new Queue<DateTime>());
            lock (queue)
            {
                while (queue.Count > 0 && queue.Peek() < cutoffWindow)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= quota.MaxRequestsPerMinutePerUser)
                {
                    return new AiQuotaCheckResult(
                        Allowed: false,
                        ErrorCode: "RATE_LIMIT_EXCEEDED",
                        Message: $"Bạn đang gửi yêu cầu AI quá nhanh (tối đa {quota.MaxRequestsPerMinutePerUser} lượt/phút). Vui lòng chờ giây lát rồi thử lại.",
                        RequestsInLastMinute: queue.Count,
                        MaxRequestsPerMinute: quota.MaxRequestsPerMinutePerUser,
                        UserDailyTokensUsed: userDailyTokens,
                        UserDailyTokenLimit: quota.DailyTokenLimitPerUser,
                        ProjectDailyTokensUsed: projectDailyTokens,
                        ProjectDailyTokenLimit: quota.DailyTokenLimitPerProject);
                }

                queue.Enqueue(nowUtc);
                requestsInLastMinute = queue.Count;
            }
        }

        return new AiQuotaCheckResult(
            Allowed: true,
            RequestsInLastMinute: requestsInLastMinute,
            MaxRequestsPerMinute: quota.MaxRequestsPerMinutePerUser,
            UserDailyTokensUsed: userDailyTokens,
            UserDailyTokenLimit: quota.DailyTokenLimitPerUser,
            ProjectDailyTokensUsed: projectDailyTokens,
            ProjectDailyTokenLimit: quota.DailyTokenLimitPerProject);
    }

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
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Model) ? "Gemini 3.6 Flash" : r.Model)
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

