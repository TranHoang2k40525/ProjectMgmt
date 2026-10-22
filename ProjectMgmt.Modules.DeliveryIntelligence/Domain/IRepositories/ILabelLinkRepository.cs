using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

public interface ILabelLinkRepository
{
    Task<IReadOnlyList<Label>> GetProjectLabelsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Label?> GetLabelAsync(Guid labelId, CancellationToken cancellationToken = default);

    Task<bool> LabelNameExistsAsync(Guid projectId, string name, Guid? exceptLabelId, CancellationToken cancellationToken = default);

    Task AddLabelAsync(Label label, CancellationToken cancellationToken = default);

    void RemoveLabel(Label label);

    Task<IReadOnlyList<Label>> GetIssueLabelsAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<IssueLabel?> GetIssueLabelAsync(Guid issueId, Guid labelId, CancellationToken cancellationToken = default);

    Task AddIssueLabelAsync(IssueLabel issueLabel, CancellationToken cancellationToken = default);

    void RemoveIssueLabel(IssueLabel issueLabel);

    /// <summary>Các liên kết đi ra từ Issue (mỗi quan hệ có bản ghi ngược ở Issue kia).</summary>
    Task<IReadOnlyList<IssueLink>> GetOutgoingLinksAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<IssueLink?> GetLinkAsync(Guid linkId, CancellationToken cancellationToken = default);

    Task<IssueLink?> FindLinkAsync(Guid sourceIssueId, Guid targetIssueId, string linkType, CancellationToken cancellationToken = default);

    Task AddLinkAsync(IssueLink link, CancellationToken cancellationToken = default);

    void RemoveLink(IssueLink link);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
