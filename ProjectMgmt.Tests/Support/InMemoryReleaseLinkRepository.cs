using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace ProjectMgmt.Tests.Support;

public class InMemoryReleaseLinkRepository : IReleaseLinkRepository
{
    public List<IssueComponentLink> Components { get; } = [];
    public List<IssueVersionLink> Versions { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<IssueComponentLink>> GetComponentLinksAsync(Guid issueId) =>
        Task.FromResult<IReadOnlyList<IssueComponentLink>>(Components.Where(l => l.IssueId == issueId).ToList());

    public Task AddComponentLinkAsync(IssueComponentLink link)
    {
        Components.Add(link);
        return Task.CompletedTask;
    }

    public void RemoveComponentLink(IssueComponentLink link) => Components.Remove(link);

    public Task<IReadOnlyList<IssueVersionLink>> GetVersionLinksAsync(Guid issueId) =>
        Task.FromResult<IReadOnlyList<IssueVersionLink>>(Versions.Where(l => l.IssueId == issueId).ToList());

    public Task AddVersionLinkAsync(IssueVersionLink link)
    {
        Versions.Add(link);
        return Task.CompletedTask;
    }

    public void RemoveVersionLink(IssueVersionLink link) => Versions.Remove(link);

    public Task SaveChangesAsync()
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
