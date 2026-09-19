using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/boards")]
public class BoardsController : ApiControllerBase
{
    private readonly IProjectManagementService _service;

    public BoardsController(IProjectManagementService service)
    {
        _service = service;
    }

    [HttpGet("{boardId:guid}/columns")]
    public async Task<ActionResult<IReadOnlyList<BoardColumnDto>>> GetColumns(
        Guid boardId,
        CancellationToken cancellationToken) =>
        FromResult(await _service.GetBoardColumnsAsync(boardId, cancellationToken));

    [HttpPost("{boardId:guid}/columns")]
    public async Task<ActionResult<BoardColumnDto>> CreateColumn(
        Guid boardId,
        [FromBody] CreateBoardColumnRequestDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _service.CreateBoardColumnAsync(boardId, request, cancellationToken));

    [HttpPut("{boardId:guid}/columns/reorder")]
    public async Task<IActionResult> ReorderColumns(
        Guid boardId,
        [FromBody] ReorderBoardColumnsRequestDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _service.ReorderBoardColumnsAsync(boardId, request, cancellationToken));

    [HttpDelete("columns/{columnId:guid}")]
    public async Task<IActionResult> DeleteColumn(
        Guid columnId,
        CancellationToken cancellationToken) =>
        FromResult(await _service.DeleteBoardColumnAsync(columnId, cancellationToken));
}
