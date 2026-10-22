using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Comment, Attachment, Watcher của Issue (cùng DeliveryIntelligenceDbContext với IIssueRepository).</summary>
public interface ICollaborationRepository
{
    Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Comment?> GetCommentAsync(Guid commentId, CancellationToken cancellationToken = default);

    Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Attachment>> GetAttachmentsAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Attachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    Task AddAttachmentAsync(Attachment attachment, CancellationToken cancellationToken = default);

    void RemoveAttachment(Attachment attachment);

    Task<IReadOnlyList<IssueWatcher>> GetWatchersAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<IssueWatcher?> GetWatcherAsync(Guid issueId, Guid userId, CancellationToken cancellationToken = default);

    Task AddWatcherAsync(IssueWatcher watcher, CancellationToken cancellationToken = default);

    void RemoveWatcher(IssueWatcher watcher);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
