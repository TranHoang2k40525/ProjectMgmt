using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-184: bình luận (có reply, @mention), tệp đính kèm và người theo dõi Issue. Quyền theo Project do Host kiểm.</summary>
public interface IIssueCollaborationService
{
    Task<Result<IReadOnlyList<CommentDto>>> GetCommentsAsync(Guid issueId);

    Task<Result<CommentDto>> AddCommentAsync(Guid issueId, CreateCommentDto request, Guid actorUserId);

    /// <summary>Trả lời luôn gắn vào bình luận gốc (một cấp), kể cả khi trả lời một reply.</summary>
    Task<Result<CommentDto>> ReplyAsync(Guid commentId, CreateCommentDto request, Guid actorUserId);

    Task<Result<CommentDto>> UpdateCommentAsync(Guid commentId, UpdateCommentDto request, Guid actorUserId);

    /// <summary>Xóa mềm; chỉ tác giả hoặc người có quyền điều phối (<paramref name="canModerate"/>).</summary>
    Task<Result> DeleteCommentAsync(Guid commentId, Guid actorUserId, bool canModerate);

    Task<Result<Guid>> GetCommentIssueIdAsync(Guid commentId);

    Task<Result<IReadOnlyList<AttachmentDto>>> GetAttachmentsAsync(Guid issueId);

    Task<Result<AttachmentDto>> UploadAttachmentAsync(
        Guid issueId,
        string fileName,
        long length,
        Stream content,
        Guid actorUserId);

    Task<Result<AttachmentFile>> OpenAttachmentAsync(Guid attachmentId);

    Task<Result<Guid>> GetAttachmentIssueIdAsync(Guid attachmentId);

    Task<Result> DeleteAttachmentAsync(Guid attachmentId, Guid actorUserId);

    Task<Result<IReadOnlyList<WatcherDto>>> GetWatchersAsync(Guid issueId);

    Task<Result<WatchStateDto>> WatchAsync(Guid issueId, Guid userId);

    Task<Result<WatchStateDto>> UnwatchAsync(Guid issueId, Guid userId);
}

public record AttachmentFile(AttachmentDto Metadata, Stream Content);

/// <summary>Kho lưu file đính kèm (ngoài webroot). Tên lưu do hệ thống sinh, không dùng tên file người dùng làm đường dẫn.</summary>
public interface IAttachmentStorage
{
    Task SaveAsync(string storedName, Stream content);

    Stream? OpenRead(string storedName);

    void Delete(string storedName);
}

public class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".pdf", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".docx", ".xlsx", ".zip"];
}
