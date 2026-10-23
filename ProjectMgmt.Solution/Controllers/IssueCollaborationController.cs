using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>
/// Comment / Attachment / Watcher của module DeliveryIntelligence (C). Đọc cần là thành viên Project;
/// ghi bình luận/tệp cần "issue.update"; xóa bình luận của người khác cần "issue.delete".
/// POST /issues/{id}/comments, POST /issues/{id}/attachments, GET/DELETE /attachments/{id} vẫn ở WorkItemsController (URL cũ) nhưng gọi cùng service.
/// </summary>
[Route("api")]
[Route("api/v1")]
public class IssueCollaborationController(
    IIssueCollaborationService collaboration,
    IIssueTrackingService issues,
    ProjectAccess access) : ApiControllerBase
{
    [HttpGet("issues/{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> Comments(Guid id, CancellationToken cancellationToken)
    {
        var denied = await DenyForIssueAsync(id, "project.read", cancellationToken);
        return denied ?? FromResult(await collaboration.GetCommentsAsync(id, cancellationToken));
    }

    [HttpPost("comments/{id:guid}/replies")]
    public async Task<ActionResult<CommentDto>> Reply(Guid id, CreateCommentDto request, CancellationToken cancellationToken)
    {
        var denied = await DenyForCommentAsync(id, "issue.update", cancellationToken);
        return denied ?? FromResult(await collaboration.ReplyAsync(id, request, access.UserId, cancellationToken));
    }

    [HttpPut("comments/{id:guid}")]
    public async Task<ActionResult<CommentDto>> UpdateComment(Guid id, UpdateCommentDto request, CancellationToken cancellationToken)
    {
        var denied = await DenyForCommentAsync(id, "issue.update", cancellationToken);
        return denied ?? FromResult(await collaboration.UpdateCommentAsync(id, request, access.UserId, cancellationToken));
    }

    [HttpDelete("comments/{id:guid}")]
    public async Task<ActionResult> DeleteComment(Guid id, CancellationToken cancellationToken)
    {
        var issueId = await collaboration.GetCommentIssueIdAsync(id, cancellationToken);
        if (issueId.IsFailure) return FromResult(issueId).Result!;
        var projectId = await ProjectOfAsync(issueId.Value, cancellationToken);
        if (projectId is null || !await access.AllowsAsync(projectId.Value, "issue.update")) return Forbidden();
        var canModerate = await access.AllowsAsync(projectId.Value, "issue.delete");
        var result = await collaboration.DeleteCommentAsync(id, access.UserId, canModerate, cancellationToken);
        return result.IsSuccess ? NoContent() : FromResult(result);
    }

    [HttpGet("issues/{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> Attachments(Guid id, CancellationToken cancellationToken)
    {
        var denied = await DenyForIssueAsync(id, "project.read", cancellationToken);
        return denied ?? FromResult(await collaboration.GetAttachmentsAsync(id, cancellationToken));
    }

    [HttpGet("attachments/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var issueId = await collaboration.GetAttachmentIssueIdAsync(id, cancellationToken);
        if (issueId.IsFailure) return FromResult(issueId).Result!;
        var denied = await DenyForIssueAsync(issueId.Value, "project.read", cancellationToken);
        if (denied is not null) return denied;
        var file = await collaboration.OpenAttachmentAsync(id, cancellationToken);
        if (file.IsFailure) return FromResult(file).Result!;
        return File(file.Value.Content, file.Value.Metadata.ContentType, file.Value.Metadata.FileName);
    }

    [HttpGet("issues/{id:guid}/watchers")]
    public async Task<ActionResult<IReadOnlyList<WatcherDto>>> Watchers(Guid id, CancellationToken cancellationToken)
    {
        var denied = await DenyForIssueAsync(id, "project.read", cancellationToken);
        return denied ?? FromResult(await collaboration.GetWatchersAsync(id, cancellationToken));
    }

    [HttpPost("issues/{id:guid}/watchers")]
    [HttpPost("issues/{id:guid}/watchers/me")]
    public async Task<ActionResult<WatchStateDto>> Watch(Guid id, CancellationToken cancellationToken)
    {
        var denied = await DenyForIssueAsync(id, "project.read", cancellationToken);
        return denied ?? FromResult(await collaboration.WatchAsync(id, access.UserId, cancellationToken));
    }

    [HttpDelete("issues/{id:guid}/watchers/me")]
    public async Task<ActionResult<WatchStateDto>> Unwatch(Guid id, CancellationToken cancellationToken)
    {
        var denied = await DenyForIssueAsync(id, "project.read", cancellationToken);
        return denied ?? FromResult(await collaboration.UnwatchAsync(id, access.UserId, cancellationToken));
    }

    private async Task<ActionResult?> DenyForCommentAsync(Guid commentId, string permission, CancellationToken cancellationToken)
    {
        var issueId = await collaboration.GetCommentIssueIdAsync(commentId, cancellationToken);
        return issueId.IsFailure ? FromResult(issueId).Result : await DenyForIssueAsync(issueId.Value, permission, cancellationToken);
    }

    private async Task<ActionResult?> DenyForIssueAsync(Guid issueId, string permission, CancellationToken cancellationToken)
    {
        var projectId = await ProjectOfAsync(issueId, cancellationToken);
        if (projectId is null) return FromResult(Result.Failure(Error.NotFound("ISSUE_NOT_FOUND", "Không tìm thấy Issue.")));
        return await access.AllowsAsync(projectId.Value, permission) ? null : Forbidden();
    }

    private async Task<Guid?> ProjectOfAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var issue = await issues.GetAsync(issueId, cancellationToken);
        return issue.IsSuccess ? issue.Value.ProjectId : null;
    }

    private ObjectResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
    {
        Title = "Forbidden",
        Detail = "Bạn không có quyền thực hiện thao tác này.",
        Status = StatusCodes.Status403Forbidden,
        Extensions = { ["code"] = "PROJECT_PERMISSION_DENIED" }
    });
}
