using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>
/// API quản trị DataOps (module C, SCRUMAI-213). Dataset không thuộc Project nào nên mọi endpoint — kể cả đọc, vì mẫu có thể
/// chứa nội dung Story thật — cần quyền hệ thống "ai.dataset.manage".
/// </summary>
[Route("api/ai-data")]
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
    public async Task<ActionResult<IReadOnlyList<DatasetDto>>> Datasets() =>
        await DenyAsync() ?? Ok(await datasets.ListDatasetsAsync());

    [HttpPost("datasets")]
    public async Task<ActionResult<DatasetDto>> CreateDataset(CreateDatasetDto request) =>
        await DenyAsync() ?? FromResult(await datasets.CreateDatasetAsync(request, UserId));

    [HttpGet("datasets/{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<DatasetVersionDto>>> Versions(Guid id) =>
        await DenyAsync() ?? FromResult(await datasets.ListVersionsAsync(id));

    [HttpPost("datasets/{id:guid}/versions")]
    public async Task<ActionResult<DatasetVersionDto>> CreateVersion(Guid id, CreateDatasetVersionDto request) =>
        await DenyAsync() ?? FromResult(await datasets.CreateVersionAsync(id, request));

    [HttpGet("versions/{id:guid}")]
    public async Task<ActionResult<DatasetVersionDto>> Version(Guid id) =>
        await DenyAsync() ?? FromResult(await datasets.GetVersionAsync(id));

    [HttpGet("versions/{id:guid}/samples")]
    public async Task<ActionResult<PagedResultDto<DatasetSampleDto>>> Samples(Guid id, [FromQuery] SampleQueryDto query) =>
        await DenyAsync() ?? FromResult(await datasets.ListSamplesAsync(id, query));

    [HttpPost("versions/{id:guid}/samples")]
    public async Task<ActionResult<DatasetSampleDto>> AddSample(Guid id, SaveDatasetSampleDto request) =>
        await DenyAsync() ?? FromResult(await datasets.AddSampleAsync(id, request));

    [HttpGet("samples/{id:guid}")]
    public async Task<ActionResult<DatasetSampleDto>> Sample(Guid id) =>
        await DenyAsync() ?? FromResult(await datasets.GetSampleAsync(id));

    [HttpPut("samples/{id:guid}")]
    public async Task<ActionResult<DatasetSampleDto>> UpdateSample(Guid id, SaveDatasetSampleDto request) =>
        await DenyAsync() ?? FromResult(await datasets.UpdateSampleAsync(id, request));

    [HttpGet("samples/{id:guid}/flags")]
    public async Task<ActionResult<IReadOnlyList<QualityFlagDto>>> SampleFlags(Guid id) =>
        await DenyAsync() ?? FromResult(await cleaning.GetSampleFlagsAsync(id));

    [HttpPost("samples/{id:guid}/approve")]
    public async Task<ActionResult<DatasetSampleDto>> Approve(Guid id, ApproveSampleDto request) =>
        await DenyAsync() ?? FromResult(await review.ApproveAsync(id, request, UserId));

    [HttpPost("samples/{id:guid}/reject")]
    public async Task<ActionResult<DatasetSampleDto>> Reject(Guid id, RejectSampleDto request) =>
        await DenyAsync() ?? FromResult(await review.RejectAsync(id, request, UserId));

    [HttpGet("versions/{id:guid}/flags")]
    public async Task<ActionResult<IReadOnlyList<QualityFlagDto>>> VersionFlags(Guid id, bool unresolvedOnly = true) =>
        await DenyAsync() ?? FromResult(await review.GetVersionFlagsAsync(id, unresolvedOnly));

    [HttpPost("flags/{id:guid}/resolve")]
    public async Task<ActionResult<QualityFlagDto>> ResolveFlag(Guid id) =>
        await DenyAsync() ?? FromResult(await cleaning.ResolveFlagAsync(id, UserId));

    [HttpPost("versions/{id:guid}/clean")]
    public async Task<ActionResult<CleaningRunSummaryDto>> Clean(Guid id) =>
        await DenyAsync() ?? FromResult(await cleaning.RunAsync(id));

    [HttpPost("versions/{id:guid}/freeze")]
    public async Task<ActionResult<DatasetVersionDto>> Freeze(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FreezeVersionDto? request) =>
        await DenyAsync() ?? FromResult(await review.FreezeAsync(id, request ?? new FreezeVersionDto()));

    [HttpGet("cleaning-rules")]
    public async Task<ActionResult<IReadOnlyList<CleaningRuleDto>>> Rules() =>
        await DenyAsync() ?? Ok(await cleaning.ListRulesAsync());

    [HttpPost("cleaning-rules")]
    public async Task<ActionResult<CleaningRuleDto>> CreateRule(CreateCleaningRuleDto request) =>
        await DenyAsync() ?? FromResult(await cleaning.CreateRuleAsync(request));

    [HttpPut("cleaning-rules/{id:guid}")]
    public async Task<ActionResult<CleaningRuleDto>> UpdateRule(Guid id, UpdateCleaningRuleDto request) =>
        await DenyAsync() ?? FromResult(await cleaning.UpdateRuleAsync(id, request));

    private async Task<ActionResult?> DenyAsync()
    {
        if (UserId != Guid.Empty && await permissions.HasPermissionAsync(UserId, ManagePermission, "System", null))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Không có quyền truy cập",
            Detail = "Cần quyền quản trị dữ liệu AI (ai.dataset.manage).",
            Status = StatusCodes.Status403Forbidden,
            Extensions = { ["code"] = "AI_DATASET_PERMISSION_DENIED" }
        });
    }
}
