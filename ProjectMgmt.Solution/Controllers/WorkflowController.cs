using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/workflow")]
[Route("api/v1/workflow")]
[TypeFilter(typeof(ProjectMgmt.Solution.Security.ProjectAuthorizationFilter))]
public class WorkflowController : ApiControllerBase
{
    private readonly IProjectManagementService _service;

    public WorkflowController(IProjectManagementService service)
    {
        _service = service;
    }

    [HttpPut("transitions/{transitionId:guid}")]
    public async Task<ActionResult<WorkflowTransitionItemDto>> UpdateTransition(
        Guid transitionId,
        [FromBody] UpdateWorkflowTransitionRequestDto request) =>
        FromResult(await _service.UpdateWorkflowTransitionAsync(transitionId, request));

    [HttpDelete("transitions/{transitionId:guid}")]
    public async Task<IActionResult> DeleteTransition(
        Guid transitionId) =>
        FromResult(await _service.DeleteWorkflowTransitionAsync(transitionId));
}
