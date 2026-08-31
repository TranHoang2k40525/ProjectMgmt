using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Solution.Api;

[Route("api/projects")]
public sealed class ProjectsController(IProjectLookupService projects) : ApiControllerBase
{
    [HttpGet("{projectId:guid}/issue-types")]
    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypes(
        Guid projectId,
        CancellationToken cancellationToken) =>
        projects.GetIssueTypesAsync(projectId, cancellationToken);

    [HttpGet("{projectId:guid}/statuses")]
    public Task<IReadOnlyList<ProjectStatusDto>> GetStatuses(
        Guid projectId,
        CancellationToken cancellationToken) =>
        projects.GetStatusesAsync(projectId, cancellationToken);
}
