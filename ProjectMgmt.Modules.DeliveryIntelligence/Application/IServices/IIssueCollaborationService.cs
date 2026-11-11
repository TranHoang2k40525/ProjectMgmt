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

    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB

    /// <summary>
    /// Các định dạng tệp thực thi / nguy cơ mã độc tuyệt đối bị cấm tải lên hệ thống.
    /// </summary>
    public string[] BlockedExtensions { get; set; } =
    [
        ".exe", ".bat", ".cmd", ".sh", ".vbs", ".msi", ".dll", ".com", ".scr", ".pif",
        ".reg", ".ps1", ".vbe", ".jse", ".wsf", ".wsh", ".msc", ".bin", ".hta", ".cpl",
        ".jar", ".apk"
    ];

    public string[] AllowedExtensions { get; set; } =
    [
        // Documents & Office
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".csv", ".rtf", ".odt", ".ods",
        // Images & Graphics
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".ico",
        // Source code (hỗ trợ .cs, .hs, .ts, .js, .py, .go, .rs, .c, .cpp, v.v.)
        ".cs", ".hs", ".ts", ".js", ".py", ".c", ".cpp", ".h", ".hpp", ".java", ".go", ".rs", ".php",
        ".rb", ".html", ".css", ".scss", ".less", ".json", ".xml", ".yaml", ".yml", ".md", ".sql",
        // Archives
        ".zip", ".rar", ".7z", ".tar", ".gz"
    ];
}
