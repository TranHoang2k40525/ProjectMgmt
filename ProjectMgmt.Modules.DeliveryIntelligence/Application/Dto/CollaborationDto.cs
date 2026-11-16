using FluentValidation;

namespace DeliveryIntelligence.Application.Dto;

public class CreateCommentDto
{
    public string Content { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }

    /// <summary>Người được @mention do FE gửi; chỉ thành viên Project mới được lưu.</summary>
    public List<Guid> MentionedUserIds { get; set; } = [];
}

public class UpdateCommentDto
{
    public string Content { get; set; } = string.Empty;
}

public class CommentDto
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }

    /// <summary>Rỗng khi bình luận đã xóa nhưng còn trả lời (giữ chỗ cho luồng reply).</summary>
    public string Content { get; set; } = string.Empty;
    public IReadOnlyList<Guid> MentionedUserIds { get; set; } = [];
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AttachmentDto
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public Guid UploadedBy { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WatcherDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WatchStateDto
{
    public bool IsWatching { get; set; }
    public int WatcherCount { get; set; }
}

public class CreateCommentDtoValidator : AbstractValidator<CreateCommentDto>
{
    public const int ContentMaxLength = 20_000;
    public const int MaxMentions = 20;

    public CreateCommentDtoValidator()
    {
        RuleFor(x => x.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content)).WithErrorCode("COMMENT_CONTENT_REQUIRED").WithMessage("Bình luận không được để trống.")
            .MaximumLength(ContentMaxLength).WithErrorCode("COMMENT_CONTENT_TOO_LONG").WithMessage($"Bình luận tối đa {ContentMaxLength} ký tự.");
        RuleFor(x => x.MentionedUserIds)
            .Must(ids => ids.Count <= MaxMentions).WithErrorCode("COMMENT_MENTIONS_TOO_MANY").WithMessage($"Tối đa {MaxMentions} người được nhắc tới.");
        RuleFor(x => x.ParentCommentId)
            .Must(id => id is null || id != Guid.Empty).WithErrorCode("COMMENT_PARENT_INVALID").WithMessage("Bình luận gốc không hợp lệ.");
    }
}

public class UpdateCommentDtoValidator : AbstractValidator<UpdateCommentDto>
{
    public UpdateCommentDtoValidator()
    {
        RuleFor(x => x.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content)).WithErrorCode("COMMENT_CONTENT_REQUIRED").WithMessage("Bình luận không được để trống.")
            .MaximumLength(CreateCommentDtoValidator.ContentMaxLength).WithErrorCode("COMMENT_CONTENT_TOO_LONG").WithMessage("Bình luận quá dài.");
    }
}
