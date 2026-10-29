using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

/// <summary>
/// Acceptance Criteria và kỹ năng yêu cầu (module C). Đọc: thành viên Project; ghi: "issue.update".
/// Route cũ POST /issues/{id}/criteria và PUT /criteria/{id} trong WorkItemsController gọi cùng service.
/// </summary>
[Route("api")]
[Route("api/v1")]
public class IssueRequirementController(
    IIssueRequirementService requirements,
    IIssueTrackingService issues,
    ProjectAccess access) : ApiControllerBase
{
    [HttpGet("issues/{id:guid}/acceptance-criteria")]
    public async Task<ActionResult<IReadOnlyList<AcceptanceCriterionDto>>> Criteria(Guid id) =>
        await DenyAsync(id, "project.read") ?? FromResult(await requirements.GetCriteriaAsync(id));

    [HttpPost("issues/{id:guid}/acceptance-criteria")]
    public async Task<ActionResult<AcceptanceCriterionDto>> AddCriterion(Guid id, SaveCriterionDto request) =>
        await DenyAsync(id, "issue.update") ?? FromResult(await requirements.AddCriterionAsync(id, request, access.UserId));

    [HttpPut("issues/{id:guid}/acceptance-criteria/order")]
    public async Task<ActionResult<IReadOnlyList<AcceptanceCriterionDto>>> ReorderCriteria(Guid id, ReorderCriteriaDto request) =>
        await DenyAsync(id, "issue.update") ?? FromResult(await requirements.ReorderCriteriaAsync(id, request));

    [HttpPut("acceptance-criteria/{criterionId:guid}")]
    public async Task<ActionResult<AcceptanceCriterionDto>> UpdateCriterion(Guid criterionId, SaveCriterionDto request)
    {
        var issueId = await requirements.GetCriterionIssueIdAsync(criterionId);
        if (issueId.IsFailure) return FromResult(Result<AcceptanceCriterionDto>.Failure(issueId.Error));
        return await DenyAsync(issueId.Value, "issue.update")
            ?? FromResult(await requirements.UpdateCriterionAsync(criterionId, request, access.UserId));
    }

    [HttpDelete("acceptance-criteria/{criterionId:guid}")]
    public async Task<ActionResult> DeleteCriterion(Guid criterionId)
    {
        var issueId = await requirements.GetCriterionIssueIdAsync(criterionId);
        if (issueId.IsFailure) return FromResult(issueId).Result!;
        var denied = await DenyAsync(issueId.Value, "issue.update");
        if (denied is not null) return denied;
        var result = await requirements.DeleteCriterionAsync(criterionId, access.UserId);
        return result.IsSuccess ? NoContent() : FromResult(result);
    }

    [HttpGet("issues/{id:guid}/required-skills")]
    public async Task<ActionResult<IReadOnlyList<RequiredSkillDto>>> RequiredSkills(Guid id) =>
        await DenyAsync(id, "project.read") ?? FromResult(await requirements.GetRequiredSkillsAsync(id));

    [HttpPost("issues/{id:guid}/required-skills")]
    public async Task<ActionResult<RequiredSkillDto>> AddRequiredSkill(Guid id, SaveRequiredSkillDto request) =>
        await DenyAsync(id, "issue.update") ?? FromResult(await requirements.AddRequiredSkillAsync(id, request, access.UserId));

    [HttpPut("issue-required-skills/{requiredSkillId:guid}")]
    public async Task<ActionResult<RequiredSkillDto>> UpdateRequiredSkill(Guid requiredSkillId, SaveRequiredSkillDto request)
    {
        var issueId = await requirements.GetRequiredSkillIssueIdAsync(requiredSkillId);
        if (issueId.IsFailure) return FromResult(Result<RequiredSkillDto>.Failure(issueId.Error));
        return await DenyAsync(issueId.Value, "issue.update")
            ?? FromResult(await requirements.UpdateRequiredSkillAsync(requiredSkillId, request));
    }

    [HttpDelete("issue-required-skills/{requiredSkillId:guid}")]
    public async Task<ActionResult> DeleteRequiredSkill(Guid requiredSkillId)
    {
        var issueId = await requirements.GetRequiredSkillIssueIdAsync(requiredSkillId);
        if (issueId.IsFailure) return FromResult(issueId).Result!;
        var denied = await DenyAsync(issueId.Value, "issue.update");
        if (denied is not null) return denied;
        var result = await requirements.DeleteRequiredSkillAsync(requiredSkillId, access.UserId);
        return result.IsSuccess ? NoContent() : FromResult(result);
    }

    private async Task<ActionResult?> DenyAsync(Guid issueId, string permission)
    {
        var issue = await issues.GetAsync(issueId);
        if (issue.IsFailure) return FromResult(Result.Failure(issue.Error));
        return await access.AllowsAsync(issue.Value.ProjectId, permission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Không có quyền truy cập",
                Detail = "Bạn không có quyền thực hiện thao tác này.",
                Status = StatusCodes.Status403Forbidden,
                Extensions = { ["code"] = "PROJECT_PERMISSION_DENIED" }
            });
    }
}
