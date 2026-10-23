using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>
/// Label và IssueLink của module DeliveryIntelligence (C). Đọc: thành viên Project. Gắn nhãn/tạo nhãn/liên kết: "issue.update".
/// Sửa/xóa nhãn của Project: "project.settings.manage".
/// </summary>
[Route("api")]
[Route("api/v1")]
public class IssueLabelLinkController(
    IIssueLabelLinkService labels,
    IIssueTrackingService issues,
    ProjectAccess access) : ApiControllerBase
{
    [HttpGet("projects/{projectId:guid}/labels")]
    public async Task<ActionResult<IReadOnlyList<LabelDto>>> ProjectLabels(Guid projectId, CancellationToken cancellationToken)
    {
        if (!await access.AllowsAsync(projectId)) return Forbidden();
        return Ok(await labels.GetProjectLabelsAsync(projectId, cancellationToken));
    }

    [HttpPost("projects/{projectId:guid}/labels")]
    public async Task<ActionResult<LabelDto>> CreateLabel(Guid projectId, SaveLabelDto request, CancellationToken cancellationToken)
    {
        if (!await access.AllowsAsync(projectId, "issue.update")) return Forbidden();
        return FromResult(await labels.CreateLabelAsync(projectId, request, cancellationToken));
    }

    [HttpPut("labels/{id:guid}")]
    public async Task<ActionResult<LabelDto>> UpdateLabel(Guid id, SaveLabelDto request, CancellationToken cancellationToken)
    {
        var projectId = await labels.GetLabelProjectIdAsync(id, cancellationToken);
        if (projectId.IsFailure) return FromResult(Result<LabelDto>.Failure(projectId.Error));
        if (!await access.AllowsAsync(projectId.Value, "project.settings.manage")) return Forbidden();
        return FromResult(await labels.UpdateLabelAsync(id, request, cancellationToken));
    }

    [HttpDelete("labels/{id:guid}")]
    public async Task<ActionResult> DeleteLabel(Guid id, CancellationToken cancellationToken)
    {
        var projectId = await labels.GetLabelProjectIdAsync(id, cancellationToken);
        if (projectId.IsFailure) return FromResult(projectId).Result!;
        if (!await access.AllowsAsync(projectId.Value, "project.settings.manage")) return Forbidden();
        var result = await labels.DeleteLabelAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : FromResult(result);
    }

    [HttpGet("issues/{id:guid}/labels")]
    public async Task<ActionResult<IReadOnlyList<LabelDto>>> IssueLabels(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync(id, "project.read", cancellationToken) ?? FromResult(await labels.GetIssueLabelsAsync(id, cancellationToken));

    [HttpPost("issues/{id:guid}/labels/{labelId:guid}")]
    public async Task<ActionResult<IReadOnlyList<LabelDto>>> AddLabel(Guid id, Guid labelId, CancellationToken cancellationToken) =>
        await DenyAsync(id, "issue.update", cancellationToken) ?? FromResult(await labels.AddLabelToIssueAsync(id, labelId, access.UserId, cancellationToken));

    [HttpDelete("issues/{id:guid}/labels/{labelId:guid}")]
    public async Task<ActionResult<IReadOnlyList<LabelDto>>> RemoveLabel(Guid id, Guid labelId, CancellationToken cancellationToken) =>
        await DenyAsync(id, "issue.update", cancellationToken) ?? FromResult(await labels.RemoveLabelFromIssueAsync(id, labelId, access.UserId, cancellationToken));

    [HttpGet("issues/{id:guid}/links")]
    public async Task<ActionResult<IReadOnlyList<IssueLinkDto>>> Links(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync(id, "project.read", cancellationToken) ?? FromResult(await labels.GetLinksAsync(id, cancellationToken));

    [HttpPost("issues/{id:guid}/links")]
    public async Task<ActionResult<IssueLinkDto>> CreateLink(Guid id, CreateIssueLinkDto request, CancellationToken cancellationToken)
    {
        var denied = await DenyAsync(id, "issue.update", cancellationToken);
        if (denied is not null) return denied;
        if (request.LinkType is "Blocks" or "IsBlockedBy")
        {
            var existing = await labels.GetLinksAsync(request.TargetIssueId, cancellationToken);
            if (existing.IsSuccess && existing.Value.Any(l => l.TargetIssueId == id && (l.LinkType == "Blocks" || l.LinkType == "IsBlockedBy")))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = "Không được phép tạo vòng lặp chặn giữa hai công việc.",
                    Status = StatusCodes.Status409Conflict,
                    Extensions = { ["code"] = "ISSUE_LINK_BLOCKING_CYCLE" }
                });
            }
        }
        return FromResult(await labels.CreateLinkAsync(id, request, access.UserId, cancellationToken));
    }

    [HttpDelete("issue-links/{linkId:guid}")]
    public async Task<ActionResult> DeleteLink(Guid linkId, CancellationToken cancellationToken)
    {
        var issueId = await labels.GetLinkSourceIssueIdAsync(linkId, cancellationToken);
        if (issueId.IsFailure) return FromResult(issueId).Result!;
        var denied = await DenyAsync(issueId.Value, "issue.update", cancellationToken);
        if (denied is not null) return denied;
        var result = await labels.DeleteLinkAsync(linkId, access.UserId, cancellationToken);
        return result.IsSuccess ? NoContent() : FromResult(result);
    }

    private async Task<ActionResult?> DenyAsync(Guid issueId, string permission, CancellationToken cancellationToken)
    {
        var issue = await issues.GetAsync(issueId, cancellationToken);
        if (issue.IsFailure) return FromResult(Result.Failure(issue.Error));
        return await access.AllowsAsync(issue.Value.ProjectId, permission) ? null : Forbidden();
    }

    private ObjectResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
    {
        Title = "Forbidden",
        Detail = "Bạn không có quyền thực hiện thao tác này.",
        Status = StatusCodes.Status403Forbidden,
        Extensions = { ["code"] = "PROJECT_PERMISSION_DENIED" }
    });
}
