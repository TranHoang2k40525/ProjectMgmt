using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>Lịch sử Issue (module C). Chỉ đọc; thành viên Project mới xem được.</summary>
[Route("api/issues/{id:guid}")]
[Route("api/v1/issues/{id:guid}")]
public class IssueHistoryController(
    IIssueHistoryService history,
    IIssueTrackingService issues,
    ProjectAccess access) : ApiControllerBase
{
    [HttpGet("history")]
    [HttpGet("activity-log")]
    public async Task<ActionResult<PagedResultDto<ActivityDto>>> Activity(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        await DenyAsync(id, cancellationToken) ?? FromResult(await history.GetActivityAsync(id, page, pageSize, cancellationToken));

    [HttpGet("status-history")]
    public async Task<ActionResult<IReadOnlyList<StatusChangeDto>>> Statuses(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync(id, cancellationToken) ?? FromResult(await history.GetStatusHistoryAsync(id, cancellationToken));

    [HttpGet("assignment-history")]
    public async Task<ActionResult<IReadOnlyList<AssignmentChangeDto>>> Assignments(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync(id, cancellationToken) ?? FromResult(await history.GetAssignmentHistoryAsync(id, cancellationToken));

    private async Task<ActionResult?> DenyAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var issue = await issues.GetAsync(issueId, cancellationToken);
        if (issue.IsFailure) return FromResult(Result.Failure(issue.Error));
        return await access.AllowsAsync(issue.Value.ProjectId)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Forbidden",
                Detail = "Bạn không có quyền thực hiện thao tác này.",
                Status = StatusCodes.Status403Forbidden,
                Extensions = { ["code"] = "PROJECT_PERMISSION_DENIED" }
            });
    }
}
