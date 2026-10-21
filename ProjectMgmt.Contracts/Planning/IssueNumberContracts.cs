namespace ProjectMgmt.ProjectManagement.Contracts;

/// <summary>
/// Planning (B) cấp IssueNumber nguyên tử theo Project (Project.IssueCounter). DeliveryIntelligence (C) là consumer.
/// Chưa có implementation thật trong Host: C chỉ dùng fake trong test cho tới khi B bàn giao.
/// </summary>
public interface IIssueNumberGenerator
{
    Task<int> ReserveNextAsync(Guid projectId, CancellationToken cancellationToken = default);
}
