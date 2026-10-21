using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>
/// API quản trị DataOps (module C, SCRUMAI-213). Dataset không thuộc Project nào nên mọi endpoint — kể cả đọc, vì mẫu có thể
/// chứa nội dung Story thật — cần quyền hệ thống "ai.dataset.manage".
/// </summary>
[Route("api/v1/ai-data")]
public class DataOpsController(
    IDatasetService datasets,
    IDataCleaningService cleaning,
    IDatasetReviewService review,
    IPermissionEvaluator permissions,
    ICurrentUserContext currentUser) : ApiControllerBase
{
    private const string ManagePermission = "ai.dataset.manage";

    private Guid UserId => currentUser.UserId ?? Guid.Empty;

    [HttpGet("datasets")]
    public async Task<ActionResult<IReadOnlyList<DatasetDto>>> Datasets(CancellationToken cancellationToken) =>
        await DenyAsync() ?? Ok(await datasets.ListDatasetsAsync(cancellationToken));

    [HttpPost("datasets")]
    public async Task<ActionResult<DatasetDto>> CreateDataset(CreateDatasetDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.CreateDatasetAsync(request, UserId, cancellationToken));

    [HttpGet("datasets/{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<DatasetVersionDto>>> Versions(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.ListVersionsAsync(id, cancellationToken));

    [HttpPost("datasets/{id:guid}/versions")]
    public async Task<ActionResult<DatasetVersionDto>> CreateVersion(Guid id, CreateDatasetVersionDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.CreateVersionAsync(id, request, cancellationToken));

    [HttpGet("versions/{id:guid}")]
    public async Task<ActionResult<DatasetVersionDto>> Version(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.GetVersionAsync(id, cancellationToken));

    [HttpGet("versions/{id:guid}/samples")]
    public async Task<ActionResult<PagedResultDto<DatasetSampleDto>>> Samples(Guid id, [FromQuery] SampleQueryDto query, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.ListSamplesAsync(id, query, cancellationToken));

    [HttpPost("versions/{id:guid}/samples")]
    public async Task<ActionResult<DatasetSampleDto>> AddSample(Guid id, SaveDatasetSampleDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.AddSampleAsync(id, request, cancellationToken));

    [HttpGet("samples/{id:guid}")]
    public async Task<ActionResult<DatasetSampleDto>> Sample(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.GetSampleAsync(id, cancellationToken));

    [HttpPut("samples/{id:guid}")]
    public async Task<ActionResult<DatasetSampleDto>> UpdateSample(Guid id, SaveDatasetSampleDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await datasets.UpdateSampleAsync(id, request, cancellationToken));

    [HttpGet("samples/{id:guid}/flags")]
    public async Task<ActionResult<IReadOnlyList<QualityFlagDto>>> SampleFlags(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await cleaning.GetSampleFlagsAsync(id, cancellationToken));

    [HttpPost("samples/{id:guid}/approve")]
    public async Task<ActionResult<DatasetSampleDto>> Approve(Guid id, ApproveSampleDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await review.ApproveAsync(id, request, UserId, cancellationToken));

    [HttpPost("samples/{id:guid}/reject")]
    public async Task<ActionResult<DatasetSampleDto>> Reject(Guid id, RejectSampleDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await review.RejectAsync(id, request, UserId, cancellationToken));

    [HttpGet("versions/{id:guid}/flags")]
    public async Task<ActionResult<IReadOnlyList<QualityFlagDto>>> VersionFlags(Guid id, bool unresolvedOnly = true, CancellationToken cancellationToken = default) =>
        await DenyAsync() ?? FromResult(await review.GetVersionFlagsAsync(id, unresolvedOnly, cancellationToken));

    [HttpPost("flags/{id:guid}/resolve")]
    public async Task<ActionResult<QualityFlagDto>> ResolveFlag(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await cleaning.ResolveFlagAsync(id, UserId, cancellationToken));

    [HttpPost("versions/{id:guid}/clean")]
    public async Task<ActionResult<CleaningRunSummaryDto>> Clean(Guid id, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await cleaning.RunAsync(id, cancellationToken));

    [HttpPost("versions/{id:guid}/freeze")]
    public async Task<ActionResult<DatasetVersionDto>> Freeze(Guid id, FreezeVersionDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await review.FreezeAsync(id, request, cancellationToken));

    [HttpGet("cleaning-rules")]
    public async Task<ActionResult<IReadOnlyList<CleaningRuleDto>>> Rules(CancellationToken cancellationToken) =>
        await DenyAsync() ?? Ok(await cleaning.ListRulesAsync(cancellationToken));

    [HttpPost("cleaning-rules")]
    public async Task<ActionResult<CleaningRuleDto>> CreateRule(CreateCleaningRuleDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await cleaning.CreateRuleAsync(request, cancellationToken));

    [HttpPut("cleaning-rules/{id:guid}")]
    public async Task<ActionResult<CleaningRuleDto>> UpdateRule(Guid id, UpdateCleaningRuleDto request, CancellationToken cancellationToken) =>
        await DenyAsync() ?? FromResult(await cleaning.UpdateRuleAsync(id, request, cancellationToken));

    private async Task<ActionResult?> DenyAsync()
    {
        if (UserId != Guid.Empty && await permissions.HasPermissionAsync(UserId, ManagePermission, "System", null))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Forbidden",
            Detail = "Cần quyền quản trị dữ liệu AI (ai.dataset.manage).",
            Status = StatusCodes.Status403Forbidden,
            Extensions = { ["code"] = "AI_DATASET_PERMISSION_DENIED" }
        });
    }
}
