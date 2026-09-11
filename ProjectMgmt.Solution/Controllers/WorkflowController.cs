using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/workflow")]
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
        [FromBody] UpdateWorkflowTransitionRequestDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _service.UpdateWorkflowTransitionAsync(transitionId, request, cancellationToken));

    [HttpDelete("transitions/{transitionId:guid}")]
    public async Task<IActionResult> DeleteTransition(
        Guid transitionId,
        CancellationToken cancellationToken) =>
        FromResult(await _service.DeleteWorkflowTransitionAsync(transitionId, cancellationToken));
}
