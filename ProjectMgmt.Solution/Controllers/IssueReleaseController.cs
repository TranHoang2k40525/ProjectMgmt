using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Application.Services;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.ProjectManagement.Contracts;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>
/// Component / Fix &amp; Affects Version của Issue (module C). Trả 501 khi Host chưa có IProjectReleaseLookupService
/// thật của Planning; khi B đăng ký contract đó thì các endpoint tự hoạt động.
/// </summary>
[Route("api/issues/{id:guid}")]
[Route("api/v1/issues/{id:guid}")]
public class IssueReleaseController(
    IIssueTrackingService issues,
    ProjectAccess access,
    IServiceProvider services) : ApiControllerBase
{
    [HttpGet("components")]
    [HttpGet("versions")]
    [HttpGet("release-links")]
    public Task<ActionResult<IssueReleaseLinksDto>> Get(Guid id) =>
        Run(id, "project.read", release => release.GetLinksAsync(id));

    [HttpPost("components")]
    public Task<ActionResult<IssueReleaseLinksDto>> AddComponent(Guid id, AddIssueComponentDto request) =>
        Run(id, "issue.update", release => release.AddComponentAsync(id, request.ComponentId, access.UserId));

    [HttpDelete("components/{componentId:guid}")]
    public Task<ActionResult<IssueReleaseLinksDto>> RemoveComponent(Guid id, Guid componentId) =>
        Run(id, "issue.update", release => release.RemoveComponentAsync(id, componentId, access.UserId));

    [HttpPost("versions")]
    public Task<ActionResult<IssueReleaseLinksDto>> AddVersion(Guid id, AddIssueVersionDto request) =>
        Run(id, "issue.update", release => release.AddVersionAsync(id, request, access.UserId));

    [HttpDelete("versions/{versionId:guid}")]
    public Task<ActionResult<IssueReleaseLinksDto>> RemoveVersion(Guid id, Guid versionId, [FromQuery] string linkType) =>
        Run(id, "issue.update", release => release.RemoveVersionAsync(id, versionId, linkType, access.UserId));

    private async Task<ActionResult<IssueReleaseLinksDto>> Run(
        Guid issueId,
        string permission,
        Func<IIssueReleaseService, Task<Result<IssueReleaseLinksDto>>> action)
    {
        if (services.GetService<IProjectReleaseLookupService>() is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Title = "Chưa hỗ trợ",
                Detail = "Chưa có danh sách Component và Version của dự án.",
                Status = StatusCodes.Status501NotImplemented,
                Extensions = { ["code"] = "RELEASE_LOOKUP_UNAVAILABLE" }
            });
        }

        var issue = await issues.GetAsync(issueId);
        if (issue.IsFailure) return FromResult(Result<IssueReleaseLinksDto>.Failure(issue.Error));
        if (!await access.AllowsAsync(issue.Value.ProjectId, permission))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Không có quyền truy cập",
                Detail = "Bạn không có quyền thực hiện thao tác này.",
                Status = StatusCodes.Status403Forbidden,
                Extensions = { ["code"] = "PROJECT_PERMISSION_DENIED" }
            });
        }

        var release = services.GetService<IIssueReleaseService>() ?? ActivatorUtilities.CreateInstance<IssueReleaseService>(services);
        return FromResult(await action(release));
    }
}
