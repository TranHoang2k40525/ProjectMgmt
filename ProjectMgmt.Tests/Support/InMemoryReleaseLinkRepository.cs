using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace ProjectMgmt.Tests.Support;

public class InMemoryReleaseLinkRepository : IReleaseLinkRepository
{
    public List<IssueComponentLink> Components { get; } = [];
    public List<IssueVersionLink> Versions { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<IssueComponentLink>> GetComponentLinksAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IssueComponentLink>>(Components.Where(l => l.IssueId == issueId).ToList());

    public Task AddComponentLinkAsync(IssueComponentLink link, CancellationToken cancellationToken = default)
    {
        Components.Add(link);
        return Task.CompletedTask;
    }

    public void RemoveComponentLink(IssueComponentLink link) => Components.Remove(link);

    public Task<IReadOnlyList<IssueVersionLink>> GetVersionLinksAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IssueVersionLink>>(Versions.Where(l => l.IssueId == issueId).ToList());

    public Task AddVersionLinkAsync(IssueVersionLink link, CancellationToken cancellationToken = default)
    {
        Versions.Add(link);
        return Task.CompletedTask;
    }

    public void RemoveVersionLink(IssueVersionLink link) => Versions.Remove(link);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
