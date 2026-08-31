using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/projects")]
public class ProjectsController : ApiControllerBase
{
    private readonly IProjectLookupService _projects;

    public ProjectsController(IProjectLookupService projects)
    {
        _projects = projects;
    }

    [HttpGet("{projectId:guid}/issue-types")]
    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypes(
        Guid projectId,
        CancellationToken cancellationToken) =>
        _projects.GetIssueTypesAsync(projectId, cancellationToken);

    [HttpGet("{projectId:guid}/statuses")]
    public Task<IReadOnlyList<ProjectStatusDto>> GetStatuses(
        Guid projectId,
        CancellationToken cancellationToken) =>
        _projects.GetStatusesAsync(projectId, cancellationToken);
}
