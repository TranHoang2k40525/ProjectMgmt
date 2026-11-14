using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure.Repositories;

public class ReleaseLinkRepository : IReleaseLinkRepository
{
    private readonly DeliveryIntelligenceDbContext _context;

    public ReleaseLinkRepository(DeliveryIntelligenceDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<IssueComponentLink>> GetComponentLinksAsync(Guid issueId) =>
        await _context.IssueComponentLinks.Where(link => link.IssueId == issueId).ToListAsync();

    public async Task AddComponentLinkAsync(IssueComponentLink link) =>
        await _context.IssueComponentLinks.AddAsync(link);

    public void RemoveComponentLink(IssueComponentLink link) => _context.IssueComponentLinks.Remove(link);

    public async Task<IReadOnlyList<IssueVersionLink>> GetVersionLinksAsync(Guid issueId) =>
        await _context.IssueVersionLinks.Where(link => link.IssueId == issueId).ToListAsync();

    public async Task AddVersionLinkAsync(IssueVersionLink link) =>
        await _context.IssueVersionLinks.AddAsync(link);

    public void RemoveVersionLink(IssueVersionLink link) => _context.IssueVersionLinks.Remove(link);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
