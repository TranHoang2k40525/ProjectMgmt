using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/boards")]
[Route("api/v1/boards")]
[TypeFilter(typeof(ProjectMgmt.Solution.Security.ProjectAuthorizationFilter))]
public class BoardsController : ApiControllerBase
{
    private readonly IProjectManagementService _service;

    public BoardsController(IProjectManagementService service)
    {
        _service = service;
    }

    [HttpGet("{boardId:guid}/columns")]
    public async Task<ActionResult<IReadOnlyList<BoardColumnDto>>> GetColumns(
        Guid boardId) =>
        FromResult(await _service.GetBoardColumnsAsync(boardId));

    [HttpPost("{boardId:guid}/columns")]
    public async Task<ActionResult<BoardColumnDto>> CreateColumn(
        Guid boardId,
        [FromBody] CreateBoardColumnRequestDto request) =>
        FromResult(await _service.CreateBoardColumnAsync(boardId, request));

    [HttpPut("{boardId:guid}/columns/reorder")]
    public async Task<IActionResult> ReorderColumns(
        Guid boardId,
        [FromBody] ReorderBoardColumnsRequestDto request) =>
        FromResult(await _service.ReorderBoardColumnsAsync(boardId, request));

    [HttpDelete("columns/{columnId:guid}")]
    public async Task<IActionResult> DeleteColumn(
        Guid columnId) =>
        FromResult(await _service.DeleteBoardColumnAsync(columnId));

    [HttpPut("columns/{columnId:guid}")]
    public async Task<ActionResult<BoardColumnDto>> UpdateColumn(
        Guid columnId,
        [FromBody] UpdateBoardColumnRequestDto request) =>
        FromResult(await _service.UpdateBoardColumnAsync(columnId, request));
}
