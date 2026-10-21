using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-184: bình luận (có reply, @mention), tệp đính kèm và người theo dõi Issue. Quyền theo Project do Host kiểm.</summary>
public interface IIssueCollaborationService
{
    Task<Result<IReadOnlyList<CommentDto>>> GetCommentsAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<CommentDto>> AddCommentAsync(Guid issueId, CreateCommentDto request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Trả lời luôn gắn vào bình luận gốc (một cấp), kể cả khi trả lời một reply.</summary>
    Task<Result<CommentDto>> ReplyAsync(Guid commentId, CreateCommentDto request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<CommentDto>> UpdateCommentAsync(Guid commentId, UpdateCommentDto request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Xóa mềm; chỉ tác giả hoặc người có quyền điều phối (<paramref name="canModerate"/>).</summary>
    Task<Result> DeleteCommentAsync(Guid commentId, Guid actorUserId, bool canModerate, CancellationToken cancellationToken = default);

    Task<Result<Guid>> GetCommentIssueIdAsync(Guid commentId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AttachmentDto>>> GetAttachmentsAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<AttachmentDto>> UploadAttachmentAsync(
        Guid issueId,
        string fileName,
        long length,
        Stream content,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<AttachmentFile>> OpenAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> GetAttachmentIssueIdAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    Task<Result> DeleteAttachmentAsync(Guid attachmentId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WatcherDto>>> GetWatchersAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<WatchStateDto>> WatchAsync(Guid issueId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<WatchStateDto>> UnwatchAsync(Guid issueId, Guid userId, CancellationToken cancellationToken = default);
}

public record AttachmentFile(AttachmentDto Metadata, Stream Content);

/// <summary>Kho lưu file đính kèm (ngoài webroot). Tên lưu do hệ thống sinh, không dùng tên file người dùng làm đường dẫn.</summary>
public interface IAttachmentStorage
{
    Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken = default);

    Stream? OpenRead(string storedName);

    void Delete(string storedName);
}

public class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".pdf", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".docx", ".xlsx", ".zip"];
}
