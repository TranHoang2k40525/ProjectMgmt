using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure.Repositories;

public class CollaborationRepository : ICollaborationRepository
{
    private readonly DeliveryIntelligenceDbContext _context;

    public CollaborationRepository(DeliveryIntelligenceDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await _context.Comments
            .AsNoTracking()
            .Where(comment => comment.IssueId == issueId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Comment?> GetCommentAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        _context.Comments.SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public async Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default) =>
        await _context.Comments.AddAsync(comment, cancellationToken);

    public async Task<IReadOnlyList<Attachment>> GetAttachmentsAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await _context.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.IssueId == issueId)
            .OrderBy(attachment => attachment.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Attachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
        _context.Attachments.SingleOrDefaultAsync(attachment => attachment.Id == attachmentId, cancellationToken);

    public async Task AddAttachmentAsync(Attachment attachment, CancellationToken cancellationToken = default) =>
        await _context.Attachments.AddAsync(attachment, cancellationToken);

    public void RemoveAttachment(Attachment attachment) => _context.Attachments.Remove(attachment);

    public async Task<IReadOnlyList<IssueWatcher>> GetWatchersAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await _context.IssueWatchers
            .AsNoTracking()
            .Where(watcher => watcher.IssueId == issueId)
            .OrderBy(watcher => watcher.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<IssueWatcher?> GetWatcherAsync(Guid issueId, Guid userId, CancellationToken cancellationToken = default) =>
        _context.IssueWatchers.SingleOrDefaultAsync(watcher => watcher.IssueId == issueId && watcher.UserId == userId, cancellationToken);

    public async Task AddWatcherAsync(IssueWatcher watcher, CancellationToken cancellationToken = default) =>
        await _context.IssueWatchers.AddAsync(watcher, cancellationToken);

    public void RemoveWatcher(IssueWatcher watcher) => _context.IssueWatchers.Remove(watcher);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
