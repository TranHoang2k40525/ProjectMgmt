using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Comment, Attachment, Watcher của Issue (cùng DeliveryIntelligenceDbContext với IIssueRepository).</summary>
public interface ICollaborationRepository
{
    Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid issueId);

    Task<Comment?> GetCommentAsync(Guid commentId);

    Task AddCommentAsync(Comment comment);

    Task<IReadOnlyList<Attachment>> GetAttachmentsAsync(Guid issueId);

    Task<Attachment?> GetAttachmentAsync(Guid attachmentId);

    Task AddAttachmentAsync(Attachment attachment);

    void RemoveAttachment(Attachment attachment);

    Task<IReadOnlyList<IssueWatcher>> GetWatchersAsync(Guid issueId);

    Task<IssueWatcher?> GetWatcherAsync(Guid issueId, Guid userId);

    Task AddWatcherAsync(IssueWatcher watcher);

    void RemoveWatcher(IssueWatcher watcher);

    Task SaveChangesAsync();
}
