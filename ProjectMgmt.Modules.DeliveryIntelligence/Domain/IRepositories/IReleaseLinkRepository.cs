using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Liên kết Issue ↔ Component / Version (Id component/version là XMOD sang Planning).</summary>
public interface IReleaseLinkRepository
{
    Task<IReadOnlyList<IssueComponentLink>> GetComponentLinksAsync(Guid issueId);

    Task AddComponentLinkAsync(IssueComponentLink link);

    void RemoveComponentLink(IssueComponentLink link);

    Task<IReadOnlyList<IssueVersionLink>> GetVersionLinksAsync(Guid issueId);

    Task AddVersionLinkAsync(IssueVersionLink link);

    void RemoveVersionLink(IssueVersionLink link);

    Task SaveChangesAsync();
}
