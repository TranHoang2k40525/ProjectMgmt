using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure.Repositories;

public class LabelLinkRepository : ILabelLinkRepository
{
    private readonly DeliveryIntelligenceDbContext _context;

    public LabelLinkRepository(DeliveryIntelligenceDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Label>> GetProjectLabelsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _context.Labels
            .AsNoTracking()
            .Where(label => label.ProjectId == projectId)
            .OrderBy(label => label.Name)
            .ToListAsync(cancellationToken);

    public Task<Label?> GetLabelAsync(Guid labelId, CancellationToken cancellationToken = default) =>
        _context.Labels.SingleOrDefaultAsync(label => label.Id == labelId, cancellationToken);

    // Collation utf8mb4_0900_ai_ci nên so sánh tên không phân biệt hoa thường/dấu, khớp UQ_Label_Name.
    public Task<bool> LabelNameExistsAsync(Guid projectId, string name, Guid? exceptLabelId, CancellationToken cancellationToken = default) =>
        _context.Labels.AnyAsync(
            label => label.ProjectId == projectId && label.Name == name && (exceptLabelId == null || label.Id != exceptLabelId),
            cancellationToken);

    public async Task AddLabelAsync(Label label, CancellationToken cancellationToken = default) =>
        await _context.Labels.AddAsync(label, cancellationToken);

    public void RemoveLabel(Label label) => _context.Labels.Remove(label);

    public async Task<IReadOnlyList<Label>> GetIssueLabelsAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await (from link in _context.IssueLabels.AsNoTracking()
               join label in _context.Labels.AsNoTracking() on link.LabelId equals label.Id
               where link.IssueId == issueId
               orderby label.Name
               select label).ToListAsync(cancellationToken);

    public Task<IssueLabel?> GetIssueLabelAsync(Guid issueId, Guid labelId, CancellationToken cancellationToken = default) =>
        _context.IssueLabels.SingleOrDefaultAsync(link => link.IssueId == issueId && link.LabelId == labelId, cancellationToken);

    public async Task AddIssueLabelAsync(IssueLabel issueLabel, CancellationToken cancellationToken = default) =>
        await _context.IssueLabels.AddAsync(issueLabel, cancellationToken);

    public void RemoveIssueLabel(IssueLabel issueLabel) => _context.IssueLabels.Remove(issueLabel);

    public async Task<IReadOnlyList<IssueLink>> GetOutgoingLinksAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await _context.IssueLinks
            .AsNoTracking()
            .Where(link => link.SourceIssueId == issueId)
            .OrderBy(link => link.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<IssueLink?> GetLinkAsync(Guid linkId, CancellationToken cancellationToken = default) =>
        _context.IssueLinks.SingleOrDefaultAsync(link => link.Id == linkId, cancellationToken);

    public Task<IssueLink?> FindLinkAsync(Guid sourceIssueId, Guid targetIssueId, string linkType, CancellationToken cancellationToken = default) =>
        _context.IssueLinks.SingleOrDefaultAsync(
            link => link.SourceIssueId == sourceIssueId && link.TargetIssueId == targetIssueId && link.LinkType == linkType,
            cancellationToken);

    public async Task AddLinkAsync(IssueLink link, CancellationToken cancellationToken = default) =>
        await _context.IssueLinks.AddAsync(link, cancellationToken);

    public void RemoveLink(IssueLink link) => _context.IssueLinks.Remove(link);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
