using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Common;
using ProjectMgmt.IssueTracking.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/issues")]
public class IssuesController : ApiControllerBase
{
    private readonly IIssueService _issueService;
    private readonly IIssueReadService _issueReads;
    private readonly IIssueSprintService _issueSprints;

    public IssuesController(
        IIssueService issueService,
        IIssueReadService issueReads,
        IIssueSprintService issueSprints)
    {
        _issueService = issueService;
        _issueReads = issueReads;
        _issueSprints = issueSprints;
    }

    [HttpPost]
    [ProducesResponseType<CreateIssueResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateIssueResultDto>> Create(
        CreateIssueDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _issueService.CreateIssueAsync(request, cancellationToken));

    [HttpGet("{issueId:guid}")]
    public async Task<ActionResult<IssueDto>> GetById(Guid issueId, CancellationToken cancellationToken) =>
        FromResult(await _issueReads.GetByIdAsync(issueId, cancellationToken));

    [HttpGet("by-sprint/{sprintId:guid}")]
    public Task<PagedResult<IssueDto>> GetBySprint(
        Guid sprintId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        _issueReads.GetBySprintAsync(sprintId, new PageRequest(pageNumber, pageSize), cancellationToken);

    [HttpGet("by-assignee/{assigneeId:guid}/open")]
    public Task<PagedResult<IssueDto>> GetOpenByAssignee(
        Guid assigneeId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        _issueReads.GetOpenByAssigneeAsync(assigneeId, new PageRequest(pageNumber, pageSize), cancellationToken);

    [HttpPut("sprint/{sprintId:guid}")]
    public async Task<IActionResult> MoveToSprint(
        Guid sprintId,
        [FromBody] IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken) =>
        FromResult(await _issueSprints.MoveToSprintAsync(issueIds, sprintId, cancellationToken));

    [HttpDelete("sprint")]
    public async Task<IActionResult> ReturnToBacklog(
        [FromBody] IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken) =>
        FromResult(await _issueSprints.ReturnToBacklogAsync(issueIds, cancellationToken));
}
