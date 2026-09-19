using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/organizations")]
public class OrganizationsController : ApiControllerBase
{
    private readonly IProjectManagementService _service;

    public OrganizationsController(IProjectManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetAll(CancellationToken cancellationToken) =>
        FromResult(await _service.GetOrganizationsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OrganizationDto>> Create(
        [FromBody] CreateOrganizationDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _service.CreateOrganizationAsync(request, cancellationToken));
}
