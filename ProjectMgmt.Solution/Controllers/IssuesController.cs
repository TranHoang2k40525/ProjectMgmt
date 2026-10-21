using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>
/// API Issue của module DeliveryIntelligence (C). Quyền theo Project kiểm qua ProjectAccess trước khi gọi use case.
/// Các route cũ GET/PUT/POST trong WorkItemsController vẫn giữ nguyên cho Backlog/Board.
/// </summary>
[Route("api/v1")]
public class IssuesController(
    IIssueTrackingService issues,
    IIssueHierarchyService hierarchy,
    ProjectAccess access) : ApiControllerBase
{
    [HttpGet("projects/{projectId:guid}/issues")]
    public async Task<ActionResult<PagedResultDto<IssueListItemDto>>> List(
        Guid projectId,
        [FromQuery] IssueListQueryDto query,
        CancellationToken cancellationToken)
    {
        if (!await access.AllowsAsync(projectId)) return Forbidden();
        return FromResult(await issues.ListAsync(projectId, query, cancellationToken));
    }

    /// <summary>Dữ liệu lõi của Issue do module C sở hữu (id, số, các tham chiếu XMOD, ước lượng, hạn…).</summary>
    [HttpGet("issues/{id:guid}/core")]
    public async Task<ActionResult<IssueDetailDto>> GetCore(Guid id, CancellationToken cancellationToken)
    {
        var result = await issues.GetAsync(id, cancellationToken);
        if (result.IsFailure) return FromResult(result);
        if (!await access.AllowsAsync(result.Value.ProjectId)) return Forbidden();
        return Ok(result.Value);
    }

    /// <summary>Quyền hiệu lực của người dùng hiện tại trên Issue (để FE ẩn/khóa thao tác; backend vẫn kiểm lại mỗi request).</summary>
    [HttpGet("issues/{id:guid}/permissions")]
    public async Task<ActionResult<IssuePermissionsDto>> Permissions(Guid id, CancellationToken cancellationToken)
    {
        var current = await issues.GetAsync(id, cancellationToken);
        if (current.IsFailure) return FromResult(Result<IssuePermissionsDto>.Failure(current.Error));
        var projectId = current.Value.ProjectId;
        if (!await access.AllowsAsync(projectId)) return Forbidden();
        return Ok(new IssuePermissionsDto
        {
            CanUpdate = await access.AllowsAsync(projectId, "issue.update"),
            CanDelete = await access.AllowsAsync(projectId, "issue.delete"),
            CanTransition = await access.AllowsAsync(projectId, "issue.transition"),
            CanAssign = await access.AllowsAsync(projectId, "issue.assign"),
            CanRank = await access.AllowsAsync(projectId, "backlog.rank"),
            CanManageLabels = await access.AllowsAsync(projectId, "project.settings.manage")
        });
    }

    [HttpPatch("issues/{id:guid}")]
    public async Task<ActionResult<IssueDetailDto>> Update(Guid id, UpdateIssueDto request, CancellationToken cancellationToken)
    {
        var current = await issues.GetAsync(id, cancellationToken);
        if (current.IsFailure) return FromResult(current);
        if (!await access.AllowsAsync(current.Value.ProjectId, "issue.update")) return Forbidden();
        if (request.AssigneeId != current.Value.AssigneeId
            && request.AssigneeId != access.UserId
            && !await access.AllowsAsync(current.Value.ProjectId, "issue.assign")) return Forbidden();
        return FromResult(await issues.UpdateAsync(id, request, access.UserId, cancellationToken));
    }

    [HttpPut("issues/{id:guid}/parent")]
    public async Task<ActionResult<IssueDetailDto>> ChangeParent(
        Guid id,
        ChangeIssueParentDto request,
        CancellationToken cancellationToken)
    {
        var current = await issues.GetAsync(id, cancellationToken);
        if (current.IsFailure) return FromResult(current);
        if (!await access.AllowsAsync(current.Value.ProjectId, "issue.update")) return Forbidden();
        return FromResult(await hierarchy.ChangeParentAsync(id, request, access.UserId, cancellationToken));
    }

    [HttpPut("issues/{id:guid}/rank")]
    public async Task<ActionResult<IssueRankDto>> Rank(Guid id, RankIssueDto request, CancellationToken cancellationToken)
    {
        var current = await issues.GetAsync(id, cancellationToken);
        if (current.IsFailure) return FromResult(Result<IssueRankDto>.Failure(current.Error));
        if (!await access.AllowsAsync(current.Value.ProjectId, "backlog.rank")) return Forbidden();
        return FromResult(await hierarchy.RankAsync(id, request, access.UserId, cancellationToken));
    }

    /// <summary>
    /// Chuyển trạng thái theo workflow. Trả 501 khi Host chưa có IWorkflowValidationService thật của Planning
    /// (luồng cũ PUT /issues/{id} vẫn đổi được trạng thái).
    /// </summary>
    [HttpPost("issues/{id:guid}/transition")]
    public async Task<ActionResult<IssueTransitionResultDto>> Transition(
        Guid id,
        TransitionIssueDto request,
        [FromServices] IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (services.GetService<ProjectMgmt.ProjectManagement.Contracts.IWorkflowValidationService>() is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Title = "Not Implemented",
                Detail = "Chưa có dịch vụ kiểm workflow của Planning; dùng luồng cập nhật Issue hiện tại để đổi trạng thái.",
                Status = StatusCodes.Status501NotImplemented,
                Extensions = { ["code"] = "WORKFLOW_VALIDATION_UNAVAILABLE" }
            });
        }

        var transitions = services.GetService<IIssueTransitionService>()
            ?? ActivatorUtilities.CreateInstance<DeliveryIntelligence.Application.Services.IssueTransitionService>(services);
        var current = await issues.GetAsync(id, cancellationToken);
        if (current.IsFailure) return FromResult(Result<IssueTransitionResultDto>.Failure(current.Error));
        if (!await access.AllowsAsync(current.Value.ProjectId, "issue.transition")) return Forbidden();
        // Quyền riêng được kiểm cho đường chuyển xuất phát từ trạng thái đang thấy; nếu trạng thái đổi trước khi ghi thì trả 409.
        request.ExpectedStatusId ??= current.Value.StatusId;
        var check = await transitions.CheckAsync(id, request.TargetStatusId, cancellationToken);
        if (check.IsSuccess && !string.IsNullOrWhiteSpace(check.Value.RequiredPermissionCode)
            && !await access.AllowsAsync(current.Value.ProjectId, check.Value.RequiredPermissionCode)) return Forbidden();
        return FromResult(await transitions.TransitionAsync(id, request, access.UserId, cancellationToken));
    }

    private ObjectResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
    {
        Title = "Forbidden",
        Detail = "Bạn không có quyền thực hiện thao tác này.",
        Status = StatusCodes.Status403Forbidden,
        Extensions = { ["code"] = "PROJECT_PERMISSION_DENIED" }
    });
}
