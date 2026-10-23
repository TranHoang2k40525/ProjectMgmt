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

    public async Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid issueId) =>
        await _context.Comments
            .AsNoTracking()
            .Where(comment => comment.IssueId == issueId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync();

    public Task<Comment?> GetCommentAsync(Guid commentId) =>
        _context.Comments.SingleOrDefaultAsync(comment => comment.Id == commentId);

    public async Task AddCommentAsync(Comment comment) =>
        await _context.Comments.AddAsync(comment);

    public async Task<IReadOnlyList<Attachment>> GetAttachmentsAsync(Guid issueId) =>
        await _context.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.IssueId == issueId)
            .OrderBy(attachment => attachment.CreatedAt)
            .ToListAsync();

    public Task<Attachment?> GetAttachmentAsync(Guid attachmentId) =>
        _context.Attachments.SingleOrDefaultAsync(attachment => attachment.Id == attachmentId);

    public async Task AddAttachmentAsync(Attachment attachment) =>
        await _context.Attachments.AddAsync(attachment);

    public void RemoveAttachment(Attachment attachment) => _context.Attachments.Remove(attachment);

    public async Task<IReadOnlyList<IssueWatcher>> GetWatchersAsync(Guid issueId) =>
        await _context.IssueWatchers
            .AsNoTracking()
            .Where(watcher => watcher.IssueId == issueId)
            .OrderBy(watcher => watcher.CreatedAt)
            .ToListAsync();

    public Task<IssueWatcher?> GetWatcherAsync(Guid issueId, Guid userId) =>
        _context.IssueWatchers.SingleOrDefaultAsync(watcher => watcher.IssueId == issueId && watcher.UserId == userId);

    public async Task AddWatcherAsync(IssueWatcher watcher) =>
        await _context.IssueWatchers.AddAsync(watcher);

    public void RemoveWatcher(IssueWatcher watcher) => _context.IssueWatchers.Remove(watcher);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
