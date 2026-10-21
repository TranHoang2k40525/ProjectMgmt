using System.Text.Json;
using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using FluentValidation;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;

namespace DeliveryIntelligence.Application.Services;

public class IssueCollaborationService : IIssueCollaborationService
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".zip"] = "application/zip"
    };

    private readonly IIssueRepository _issues;
    private readonly ICollaborationRepository _collaboration;
    private readonly IUserLookupService _users;
    private readonly IPermissionEvaluator _permissions;
    private readonly IAttachmentStorage _storage;
    private readonly AttachmentOptions _attachmentOptions;
    private readonly IValidator<CreateCommentDto> _createCommentValidator;
    private readonly IValidator<UpdateCommentDto> _updateCommentValidator;
    private readonly TimeProvider _clock;

    public IssueCollaborationService(
        IIssueRepository issues,
        ICollaborationRepository collaboration,
        IUserLookupService users,
        IPermissionEvaluator permissions,
        IAttachmentStorage storage,
        AttachmentOptions attachmentOptions,
        IValidator<CreateCommentDto> createCommentValidator,
        IValidator<UpdateCommentDto> updateCommentValidator,
        TimeProvider clock)
    {
        _issues = issues;
        _collaboration = collaboration;
        _users = users;
        _permissions = permissions;
        _storage = storage;
        _attachmentOptions = attachmentOptions;
        _createCommentValidator = createCommentValidator;
        _updateCommentValidator = updateCommentValidator;
        _clock = clock;
    }

    // ---------- Comments ----------

    public async Task<Result<IReadOnlyList<CommentDto>>> GetCommentsAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<IReadOnlyList<CommentDto>>.Failure(IssueErrors.NotFound);
        }

        var comments = await _collaboration.GetCommentsAsync(issueId, cancellationToken);
        var parentsWithReplies = comments
            .Where(comment => comment.ParentCommentId.HasValue && !comment.IsDeleted)
            .Select(comment => comment.ParentCommentId!.Value)
            .ToHashSet();
        var visible = comments.Where(comment => !comment.IsDeleted || parentsWithReplies.Contains(comment.Id)).ToList();
        var names = await NamesAsync(visible.Select(comment => comment.UserId), cancellationToken);
        IReadOnlyList<CommentDto> result = visible.Select(comment => ToDto(comment, names)).ToList();
        return Result.Success(result);
    }

    public async Task<Result<CommentDto>> AddCommentAsync(
        Guid issueId,
        CreateCommentDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createCommentValidator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<CommentDto>.Failure(IssueTrackingService.ToError(validation));
        }

        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        if (issue is null)
        {
            return Result<CommentDto>.Failure(IssueErrors.NotFound);
        }

        Guid? parentId = null;
        if (request.ParentCommentId.HasValue)
        {
            var parent = await _collaboration.GetCommentAsync(request.ParentCommentId.Value, cancellationToken);
            if (parent is null || parent.IssueId != issueId || parent.IsDeleted)
            {
                return Result<CommentDto>.Failure(CollaborationErrors.ParentCommentInvalid);
            }

            parentId = parent.ParentCommentId ?? parent.Id;
        }

        var mentions = new List<Guid>();
        foreach (var userId in request.MentionedUserIds.Distinct())
        {
            if (await _permissions.IsProjectMemberAsync(userId, issue.ProjectId))
            {
                mentions.Add(userId);
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            UserId = actorUserId,
            ParentCommentId = parentId,
            Content = request.Content.Trim(),
            MentionedUserIds = mentions.Count == 0 ? null : JsonSerializer.Serialize(mentions),
            CreatedAt = now
        };
        await _collaboration.AddCommentAsync(comment, cancellationToken);
        await _issues.AddActivityAsync(
            IssueActivities.Create(issueId, actorUserId, IssueActivities.CommentAdded, now, detail: new { commentId = comment.Id }),
            cancellationToken);
        if (await _collaboration.GetWatcherAsync(issueId, actorUserId, cancellationToken) is null)
        {
            await _collaboration.AddWatcherAsync(new IssueWatcher { IssueId = issueId, UserId = actorUserId, CreatedAt = now }, cancellationToken);
        }

        await _collaboration.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(comment, await NamesAsync([actorUserId], cancellationToken)));
    }

    public async Task<Result<CommentDto>> ReplyAsync(
        Guid commentId,
        CreateCommentDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parent = await _collaboration.GetCommentAsync(commentId, cancellationToken);
        if (parent is null || parent.IsDeleted)
        {
            return Result<CommentDto>.Failure(CollaborationErrors.CommentNotFound);
        }

        request.ParentCommentId = parent.Id;
        return await AddCommentAsync(parent.IssueId, request, actorUserId, cancellationToken);
    }

    public async Task<Result<CommentDto>> UpdateCommentAsync(
        Guid commentId,
        UpdateCommentDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _updateCommentValidator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<CommentDto>.Failure(IssueTrackingService.ToError(validation));
        }

        var comment = await _collaboration.GetCommentAsync(commentId, cancellationToken);
        if (comment is null || comment.IsDeleted)
        {
            return Result<CommentDto>.Failure(CollaborationErrors.CommentNotFound);
        }

        if (comment.UserId != actorUserId)
        {
            return Result<CommentDto>.Failure(CollaborationErrors.NotCommentAuthor);
        }

        var content = request.Content.Trim();
        if (content != comment.Content)
        {
            comment.Content = content;
            comment.IsEdited = true;
            comment.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _collaboration.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(ToDto(comment, await NamesAsync([comment.UserId], cancellationToken)));
    }

    public async Task<Result> DeleteCommentAsync(
        Guid commentId,
        Guid actorUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        var comment = await _collaboration.GetCommentAsync(commentId, cancellationToken);
        if (comment is null || comment.IsDeleted)
        {
            return Result.Failure(CollaborationErrors.CommentNotFound);
        }

        if (comment.UserId != actorUserId && !canModerate)
        {
            return Result.Failure(CollaborationErrors.NotCommentAuthor);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        comment.IsDeleted = true;
        comment.UpdatedAt = now;
        await _issues.AddActivityAsync(
            IssueActivities.Create(comment.IssueId, actorUserId, IssueActivities.CommentDeleted, now, detail: new { commentId }),
            cancellationToken);
        await _collaboration.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> GetCommentIssueIdAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var comment = await _collaboration.GetCommentAsync(commentId, cancellationToken);
        return comment is null || comment.IsDeleted
            ? Result<Guid>.Failure(CollaborationErrors.CommentNotFound)
            : Result.Success(comment.IssueId);
    }

    // ---------- Attachments ----------

    public async Task<Result<IReadOnlyList<AttachmentDto>>> GetAttachmentsAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<IReadOnlyList<AttachmentDto>>.Failure(IssueErrors.NotFound);
        }

        var attachments = await _collaboration.GetAttachmentsAsync(issueId, cancellationToken);
        var names = await NamesAsync(attachments.Select(attachment => attachment.UploadedBy), cancellationToken);
        IReadOnlyList<AttachmentDto> result = attachments.Select(attachment => ToDto(attachment, names)).ToList();
        return Result.Success(result);
    }

    public async Task<Result<AttachmentDto>> UploadAttachmentAsync(
        Guid issueId,
        string fileName,
        long length,
        Stream content,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (length <= 0 || length > _attachmentOptions.MaxFileSizeBytes
            || !_attachmentOptions.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            var allowed = string.Join(", ", _attachmentOptions.AllowedExtensions);
            return Result<AttachmentDto>.Failure(Error.Validation(
                "ATTACHMENT_FILE_INVALID",
                $"Chỉ nhận tệp {allowed}, tối đa {_attachmentOptions.MaxFileSizeBytes / (1024 * 1024)} MB."));
        }

        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<AttachmentDto>.Failure(IssueErrors.NotFound);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            UploadedBy = actorUserId,
            FileName = SafeFileName(fileName!, extension),
            ContentType = ContentTypes.GetValueOrDefault(extension, "application/octet-stream"),
            FileSizeBytes = length,
            CreatedAt = now
        };
        attachment.StoredPath = attachment.Id.ToString("N") + extension;

        await _storage.SaveAsync(attachment.StoredPath, content, cancellationToken);
        try
        {
            await _collaboration.AddAttachmentAsync(attachment, cancellationToken);
            await _issues.AddActivityAsync(
                IssueActivities.Create(issueId, actorUserId, IssueActivities.AttachmentAdded, now, newValue: attachment.FileName,
                    detail: new { attachmentId = attachment.Id }),
                cancellationToken);
            await _collaboration.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _storage.Delete(attachment.StoredPath);
            throw;
        }

        return Result.Success(ToDto(attachment, await NamesAsync([actorUserId], cancellationToken)));
    }

    public async Task<Result<AttachmentFile>> OpenAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _collaboration.GetAttachmentAsync(attachmentId, cancellationToken);
        if (attachment is null || await _issues.GetByIdAsync(attachment.IssueId, cancellationToken: cancellationToken) is null)
        {
            return Result<AttachmentFile>.Failure(CollaborationErrors.AttachmentNotFound);
        }

        var stream = _storage.OpenRead(attachment.StoredPath);
        return stream is null
            ? Result<AttachmentFile>.Failure(CollaborationErrors.AttachmentNotFound)
            : Result.Success(new AttachmentFile(ToDto(attachment, new Dictionary<Guid, string>()), stream));
    }

    public async Task<Result<Guid>> GetAttachmentIssueIdAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _collaboration.GetAttachmentAsync(attachmentId, cancellationToken);
        return attachment is null
            ? Result<Guid>.Failure(CollaborationErrors.AttachmentNotFound)
            : Result.Success(attachment.IssueId);
    }

    public async Task<Result> DeleteAttachmentAsync(Guid attachmentId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var attachment = await _collaboration.GetAttachmentAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Result.Failure(CollaborationErrors.AttachmentNotFound);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        _collaboration.RemoveAttachment(attachment);
        await _issues.AddActivityAsync(
            IssueActivities.Create(attachment.IssueId, actorUserId, IssueActivities.AttachmentRemoved, now, oldValue: attachment.FileName,
                detail: new { attachmentId }),
            cancellationToken);
        await _collaboration.SaveChangesAsync(cancellationToken);
        _storage.Delete(attachment.StoredPath);
        return Result.Success();
    }

    // ---------- Watchers ----------

    public async Task<Result<IReadOnlyList<WatcherDto>>> GetWatchersAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<IReadOnlyList<WatcherDto>>.Failure(IssueErrors.NotFound);
        }

        var watchers = await _collaboration.GetWatchersAsync(issueId, cancellationToken);
        var names = await NamesAsync(watchers.Select(watcher => watcher.UserId), cancellationToken);
        IReadOnlyList<WatcherDto> result = watchers
            .Select(watcher => new WatcherDto { UserId = watcher.UserId, DisplayName = names.GetValueOrDefault(watcher.UserId, string.Empty), CreatedAt = watcher.CreatedAt })
            .ToList();
        return Result.Success(result);
    }

    public async Task<Result<WatchStateDto>> WatchAsync(Guid issueId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<WatchStateDto>.Failure(IssueErrors.NotFound);
        }

        if (await _collaboration.GetWatcherAsync(issueId, userId, cancellationToken) is null)
        {
            await _collaboration.AddWatcherAsync(
                new IssueWatcher { IssueId = issueId, UserId = userId, CreatedAt = _clock.GetUtcNow().UtcDateTime },
                cancellationToken);
            await _collaboration.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(await WatchStateAsync(issueId, true, cancellationToken));
    }

    public async Task<Result<WatchStateDto>> UnwatchAsync(Guid issueId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<WatchStateDto>.Failure(IssueErrors.NotFound);
        }

        var watcher = await _collaboration.GetWatcherAsync(issueId, userId, cancellationToken);
        if (watcher is not null)
        {
            _collaboration.RemoveWatcher(watcher);
            await _collaboration.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(await WatchStateAsync(issueId, false, cancellationToken));
    }

    private async Task<WatchStateDto> WatchStateAsync(Guid issueId, bool isWatching, CancellationToken cancellationToken) =>
        new() { IsWatching = isWatching, WatcherCount = (await _collaboration.GetWatchersAsync(issueId, cancellationToken)).Count };

    // ---------- Helpers ----------

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var users = await _users.GetDisplayInfoAsync(ids, cancellationToken);
        return users.ToDictionary(user => user.UserId, user => user.DisplayName);
    }

    private static CommentDto ToDto(Comment comment, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = comment.Id,
        IssueId = comment.IssueId,
        UserId = comment.UserId,
        AuthorName = names.GetValueOrDefault(comment.UserId, string.Empty),
        ParentCommentId = comment.ParentCommentId,
        Content = comment.IsDeleted ? string.Empty : comment.Content,
        MentionedUserIds = string.IsNullOrEmpty(comment.MentionedUserIds) || comment.IsDeleted
            ? []
            : JsonSerializer.Deserialize<List<Guid>>(comment.MentionedUserIds) ?? [],
        IsEdited = comment.IsEdited,
        IsDeleted = comment.IsDeleted,
        CreatedAt = comment.CreatedAt,
        UpdatedAt = comment.UpdatedAt
    };

    private static AttachmentDto ToDto(Attachment attachment, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = attachment.Id,
        IssueId = attachment.IssueId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSizeBytes = attachment.FileSizeBytes,
        UploadedBy = attachment.UploadedBy,
        UploadedByName = names.GetValueOrDefault(attachment.UploadedBy, string.Empty),
        CreatedAt = attachment.CreatedAt
    };

    /// <summary>Chỉ giữ tên hiển thị (bỏ thư mục, ký tự điều khiển), tối đa 255 ký tự vẫn giữ phần mở rộng.</summary>
    internal static string SafeFileName(string fileName, string extension)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(ch => !char.IsControl(ch) && Array.IndexOf(Path.GetInvalidFileNameChars(), ch) < 0).ToArray()).Trim();
        if (name.Length == 0 || name == extension)
        {
            name = "file" + extension;
        }

        return name.Length <= 255 ? name : name[..(255 - extension.Length)] + extension;
    }
}

public static class CollaborationErrors
{
    public static readonly Error CommentNotFound = Error.NotFound("COMMENT_NOT_FOUND", "Không tìm thấy bình luận.");
    public static readonly Error ParentCommentInvalid = Error.Validation("COMMENT_PARENT_INVALID", "Bình luận được trả lời không hợp lệ.");
    public static readonly Error NotCommentAuthor = Error.Forbidden("COMMENT_NOT_AUTHOR", "Chỉ tác giả mới được sửa hoặc xóa bình luận này.");
    public static readonly Error AttachmentNotFound = Error.NotFound("ATTACHMENT_NOT_FOUND", "Không tìm thấy tệp đính kèm.");
}
