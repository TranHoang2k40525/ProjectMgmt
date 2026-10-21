using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace DeliveryIntelligence.Application.Services;

/// <summary>
/// SCRUMAI-194: ghi lịch sử có cấu trúc để báo cáo/AI không phải đọc ActivityLog.
/// Chỉ thêm bản ghi (không sửa); lưu cùng unit of work của thao tác gây ra thay đổi.
/// </summary>
public class IssueHistoryRecorder
{
    public const string Manual = "Manual";
    public const string AiSuggested = "AiSuggested";
    public const string AiAuto = "AiAuto";
    public const string SelfPick = "SelfPick";

    private readonly IIssueRepository _issues;

    public IssueHistoryRecorder(IIssueRepository issues)
    {
        _issues = issues;
    }

    /// <summary>
    /// DurationSeconds = thời gian Issue nằm ở trạng thái trước: tính từ lần đổi gần nhất, hoặc từ lúc tạo nếu chưa có lịch sử.
    /// Bản ghi đầu tiên khi tạo Issue có FromStatusId = null và không có Duration.
    /// </summary>
    public async Task RecordStatusAsync(
        Issue issue,
        Guid? fromStatusId,
        string? fromCategory,
        Guid toStatusId,
        string toCategory,
        Guid? changedBy,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        long? duration = null;
        if (fromStatusId.HasValue)
        {
            var previous = await _issues.GetLastStatusChangeAsync(issue.Id, cancellationToken);
            var since = previous?.ChangedAt ?? issue.CreatedAt;
            duration = Math.Max(0, (long)(changedAtUtc - since).TotalSeconds);
        }

        await _issues.AddStatusHistoryAsync(new IssueStatusHistory
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            FromStatusId = fromStatusId,
            ToStatusId = toStatusId,
            FromCategory = fromCategory,
            ToCategory = toCategory,
            ChangedBy = changedBy,
            DurationSeconds = duration,
            ChangedAt = changedAtUtc
        }, cancellationToken);
    }

    /// <summary>
    /// Ghi đổi người xử lý; nguồn mặc định Manual, hoặc SelfPick khi người thao tác tự nhận việc.
    /// Nguồn AI (AiSuggested/AiAuto) phải kèm AiCandidateId để truy vết đề xuất.
    /// </summary>
    public async Task RecordAssignmentAsync(
        Issue issue,
        Guid? fromAssigneeId,
        Guid? toAssigneeId,
        Guid? assignedBy,
        DateTime assignedAtUtc,
        string? source = null,
        Guid? aiCandidateId = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (fromAssigneeId == toAssigneeId)
        {
            return;
        }

        source ??= toAssigneeId.HasValue && toAssigneeId == assignedBy ? SelfPick : Manual;
        if (source is not (Manual or AiSuggested or AiAuto or SelfPick))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Nguồn phân công không hợp lệ.");
        }

        if (source is AiSuggested or AiAuto && aiCandidateId is null)
        {
            throw new ArgumentException("Phân công từ AI phải có AiCandidateId.", nameof(aiCandidateId));
        }

        await _issues.AddAssignmentHistoryAsync(new IssueAssignmentHistory
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            FromAssigneeId = fromAssigneeId,
            ToAssigneeId = toAssigneeId,
            AssignedBy = assignedBy,
            AssignmentSource = source,
            AiCandidateId = aiCandidateId,
            StoryPointsAtTime = issue.StoryPoints,
            Reason = reason is { Length: > 500 } ? reason[..500] : reason,
            AssignedAt = assignedAtUtc
        }, cancellationToken);
    }
}
