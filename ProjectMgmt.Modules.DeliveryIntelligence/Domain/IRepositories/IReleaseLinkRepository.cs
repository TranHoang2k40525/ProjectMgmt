using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Liên kết Issue ↔ Component / Version (Id component/version là XMOD sang Planning).</summary>
public interface IReleaseLinkRepository
{
    Task<IReadOnlyList<IssueComponentLink>> GetComponentLinksAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task AddComponentLinkAsync(IssueComponentLink link, CancellationToken cancellationToken = default);

    void RemoveComponentLink(IssueComponentLink link);

    Task<IReadOnlyList<IssueVersionLink>> GetVersionLinksAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task AddVersionLinkAsync(IssueVersionLink link, CancellationToken cancellationToken = default);

    void RemoveVersionLink(IssueVersionLink link);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
