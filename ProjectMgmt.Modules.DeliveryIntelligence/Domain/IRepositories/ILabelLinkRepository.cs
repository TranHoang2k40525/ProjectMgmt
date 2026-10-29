using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

public interface ILabelLinkRepository
{
    Task<IReadOnlyList<Label>> GetProjectLabelsAsync(Guid projectId);

    Task<Label?> GetLabelAsync(Guid labelId);

    Task<bool> LabelNameExistsAsync(Guid projectId, string name, Guid? exceptLabelId);

    Task AddLabelAsync(Label label);

    void RemoveLabel(Label label);

    Task<IReadOnlyList<Label>> GetIssueLabelsAsync(Guid issueId);

    Task<IssueLabel?> GetIssueLabelAsync(Guid issueId, Guid labelId);

    Task AddIssueLabelAsync(IssueLabel issueLabel);

    void RemoveIssueLabel(IssueLabel issueLabel);

    /// <summary>Các liên kết đi ra từ Issue (mỗi quan hệ có bản ghi ngược ở Issue kia).</summary>
    Task<IReadOnlyList<IssueLink>> GetOutgoingLinksAsync(Guid issueId);

    Task<IssueLink?> GetLinkAsync(Guid linkId);

    Task<IssueLink?> FindLinkAsync(Guid sourceIssueId, Guid targetIssueId, string linkType);

    Task AddLinkAsync(IssueLink link);

    void RemoveLink(IssueLink link);

    Task SaveChangesAsync();
}
