using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace ProjectMgmt.Tests.Support;

public class InMemoryCollaborationRepository : ICollaborationRepository
{
    public List<Comment> Comments { get; } = [];
    public List<Attachment> Attachments { get; } = [];
    public List<IssueWatcher> Watchers { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid issueId)
    {
        IReadOnlyList<Comment> result = Comments.Where(c => c.IssueId == issueId).OrderBy(c => c.CreatedAt).ToList();
        return Task.FromResult(result);
    }

    public Task<Comment?> GetCommentAsync(Guid commentId) =>
        Task.FromResult(Comments.SingleOrDefault(c => c.Id == commentId));

    public Task AddCommentAsync(Comment comment)
    {
        Comments.Add(comment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Attachment>> GetAttachmentsAsync(Guid issueId)
    {
        IReadOnlyList<Attachment> result = Attachments.Where(a => a.IssueId == issueId).OrderBy(a => a.CreatedAt).ToList();
        return Task.FromResult(result);
    }

    public Task<Attachment?> GetAttachmentAsync(Guid attachmentId) =>
        Task.FromResult(Attachments.SingleOrDefault(a => a.Id == attachmentId));

    public Task AddAttachmentAsync(Attachment attachment)
    {
        Attachments.Add(attachment);
        return Task.CompletedTask;
    }

    public void RemoveAttachment(Attachment attachment) => Attachments.Remove(attachment);

    public Task<IReadOnlyList<IssueWatcher>> GetWatchersAsync(Guid issueId)
    {
        IReadOnlyList<IssueWatcher> result = Watchers.Where(w => w.IssueId == issueId).ToList();
        return Task.FromResult(result);
    }

    public Task<IssueWatcher?> GetWatcherAsync(Guid issueId, Guid userId) =>
        Task.FromResult(Watchers.SingleOrDefault(w => w.IssueId == issueId && w.UserId == userId));

    public Task AddWatcherAsync(IssueWatcher watcher)
    {
        Watchers.Add(watcher);
        return Task.CompletedTask;
    }

    public void RemoveWatcher(IssueWatcher watcher) => Watchers.Remove(watcher);

    public Task SaveChangesAsync()
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

public class InMemoryAttachmentStorage : IAttachmentStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task SaveAsync(string storedName, Stream content)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        Files.Add(storedName, buffer.ToArray());
    }

    public Stream? OpenRead(string storedName) =>
        Files.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes, writable: false) : null;

    public void Delete(string storedName) => Files.Remove(storedName);
}
