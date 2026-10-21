using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.UseCases;

/// <summary>
/// Mốc thời gian theo nhóm trạng thái (WorkflowStatus.Category: ToDo | InProgress | Done):
/// StartedAt = lần đầu vào InProgress (giữ nguyên về sau); ResolvedAt = lúc vào Done, bị xóa khi mở lại.
/// </summary>
public static class IssueStatusRules
{
    public const string ToDo = "ToDo";
    public const string InProgress = "InProgress";
    public const string Done = "Done";

    public static void Apply(Issue issue, string targetCategory, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (string.Equals(targetCategory, InProgress, StringComparison.OrdinalIgnoreCase))
        {
            issue.StartedAt ??= nowUtc;
        }

        if (string.Equals(targetCategory, Done, StringComparison.OrdinalIgnoreCase))
        {
            issue.StartedAt ??= nowUtc;
            issue.ResolvedAt ??= nowUtc;
        }
        else
        {
            issue.ResolvedAt = null;
        }
    }
}
