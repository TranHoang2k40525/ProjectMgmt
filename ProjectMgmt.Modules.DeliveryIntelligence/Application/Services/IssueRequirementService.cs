using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using FluentValidation;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.IssueTracking.Contracts;

namespace DeliveryIntelligence.Application.Services;

public class IssueRequirementService : IIssueRequirementService, IIssueSkillService
{
    public const int MaxCriteriaPerIssue = 50;
    public const int MaxSkillsPerIssue = 20;
    private const string Manual = "Manual";
    private const string Ai = "AI";

    private readonly IIssueRepository _issues;
    private readonly IRequirementRepository _requirements;
    private readonly ISkillCatalogReader _skills;
    private readonly IUserLookupService _users;
    private readonly IValidator<SaveCriterionDto> _criterionValidator;
    private readonly IValidator<SaveRequiredSkillDto> _skillValidator;
    private readonly TimeProvider _clock;

    public IssueRequirementService(
        IIssueRepository issues,
        IRequirementRepository requirements,
        ISkillCatalogReader skills,
        IUserLookupService users,
        IValidator<SaveCriterionDto> criterionValidator,
        IValidator<SaveRequiredSkillDto> skillValidator,
        TimeProvider clock)
    {
        _issues = issues;
        _requirements = requirements;
        _skills = skills;
        _users = users;
        _criterionValidator = criterionValidator;
        _skillValidator = skillValidator;
        _clock = clock;
    }

    // ---------- Acceptance Criteria ----------

    public async Task<Result<IReadOnlyList<AcceptanceCriterionDto>>> GetCriteriaAsync(Guid issueId)
    {
        if (await _issues.GetByIdAsync(issueId) is null)
        {
            return Result<IReadOnlyList<AcceptanceCriterionDto>>.Failure(IssueErrors.NotFound);
        }

        return Result.Success(await CriteriaDtosAsync(issueId));
    }

    public async Task<Result<AcceptanceCriterionDto>> AddCriterionAsync(
        Guid issueId,
        SaveCriterionDto request,
        Guid actorUserId)
    {
        var invalid = Validate(_criterionValidator, request);
        if (invalid is not null)
        {
            return Result<AcceptanceCriterionDto>.Failure(invalid);
        }

        if (await _issues.GetByIdAsync(issueId) is null)
        {
            return Result<AcceptanceCriterionDto>.Failure(IssueErrors.NotFound);
        }

        var existing = await _requirements.GetCriteriaAsync(issueId);
        if (existing.Count >= MaxCriteriaPerIssue)
        {
            return Result<AcceptanceCriterionDto>.Failure(RequirementErrors.TooManyCriteria);
        }

        var now = Now();
        var criterion = new AcceptanceCriteria
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            Content = request.Content.Trim(),
            IsMet = request.IsMet,
            MetBy = request.IsMet ? actorUserId : null,
            MetAt = request.IsMet ? now : null,
            OrderIndex = existing.Count == 0 ? 0 : existing.Max(c => c.OrderIndex) + 1,
            Source = Manual,
            CreatedAt = now
        };
        await _requirements.AddCriterionAsync(criterion);
        await _issues.AddActivityAsync(
            IssueActivities.Create(issueId, actorUserId, IssueActivities.CriteriaAdded, now, "AcceptanceCriteria", newValue: criterion.Content));
        await _requirements.SaveChangesAsync();
        return Result.Success(await ToDtoAsync(criterion));
    }

    public async Task<Result<AcceptanceCriterionDto>> UpdateCriterionAsync(
        Guid criterionId,
        SaveCriterionDto request,
        Guid actorUserId)
    {
        var invalid = Validate(_criterionValidator, request);
        if (invalid is not null)
        {
            return Result<AcceptanceCriterionDto>.Failure(invalid);
        }

        var criterion = await _requirements.GetCriterionAsync(criterionId);
        if (criterion is null || await _issues.GetByIdAsync(criterion.IssueId) is null)
        {
            return Result<AcceptanceCriterionDto>.Failure(RequirementErrors.CriterionNotFound);
        }

        var now = Now();
        var content = request.Content.Trim();
        var changed = false;
        if (content != criterion.Content)
        {
            criterion.Content = content;
            criterion.WasEditedAfterAi |= criterion.Source == Ai;
            changed = true;
        }

        if (request.IsMet != criterion.IsMet)
        {
            criterion.IsMet = request.IsMet;
            criterion.MetBy = request.IsMet ? actorUserId : null;
            criterion.MetAt = request.IsMet ? now : null;
            await _issues.AddActivityAsync(
                IssueActivities.Create(criterion.IssueId, actorUserId, request.IsMet ? IssueActivities.CriteriaMet : IssueActivities.CriteriaUnmet,
                    now, "AcceptanceCriteria", newValue: criterion.Content));
            changed = true;
        }

        if (changed)
        {
            criterion.UpdatedAt = now;
            await _requirements.SaveChangesAsync();
        }

        return Result.Success(await ToDtoAsync(criterion));
    }

    public async Task<Result> DeleteCriterionAsync(Guid criterionId, Guid actorUserId)
    {
        var criterion = await _requirements.GetCriterionAsync(criterionId);
        if (criterion is null)
        {
            return Result.Failure(RequirementErrors.CriterionNotFound);
        }

        _requirements.RemoveCriterion(criterion);
        await _issues.AddActivityAsync(
            IssueActivities.Create(criterion.IssueId, actorUserId, IssueActivities.CriteriaRemoved, Now(), "AcceptanceCriteria", oldValue: criterion.Content));
        await _requirements.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<AcceptanceCriterionDto>>> ReorderCriteriaAsync(
        Guid issueId,
        ReorderCriteriaDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var criteria = await _requirements.GetCriteriaAsync(issueId);
        var requested = request.OrderedIds.Distinct().ToList();
        if (requested.Count != request.OrderedIds.Count
            || requested.Count != criteria.Count
            || criteria.Any(criterion => !requested.Contains(criterion.Id)))
        {
            return Result<IReadOnlyList<AcceptanceCriterionDto>>.Failure(RequirementErrors.ReorderMismatch);
        }

        var byId = criteria.ToDictionary(criterion => criterion.Id);
        for (var index = 0; index < requested.Count; index++)
        {
            byId[requested[index]].OrderIndex = index;
        }

        await _requirements.SaveChangesAsync();
        return Result.Success(await CriteriaDtosAsync(issueId));
    }

    public async Task<Result<Guid>> GetCriterionIssueIdAsync(Guid criterionId)
    {
        var criterion = await _requirements.GetCriterionAsync(criterionId);
        return criterion is null ? Result<Guid>.Failure(RequirementErrors.CriterionNotFound) : Result.Success(criterion.IssueId);
    }

    // ---------- Required skills ----------

    public async Task<Result<IReadOnlyList<RequiredSkillDto>>> GetRequiredSkillsAsync(Guid issueId)
    {
        if (await _issues.GetByIdAsync(issueId) is null)
        {
            return Result<IReadOnlyList<RequiredSkillDto>>.Failure(IssueErrors.NotFound);
        }

        var skills = await _requirements.GetRequiredSkillsAsync([issueId]);
        var names = await SkillNamesAsync();
        IReadOnlyList<RequiredSkillDto> result = skills.Select(skill => ToDto(skill, names)).ToList();
        return Result.Success(result);
    }

    public async Task<Result<RequiredSkillDto>> AddRequiredSkillAsync(
        Guid issueId,
        SaveRequiredSkillDto request,
        Guid actorUserId)
    {
        var invalid = Validate(_skillValidator, request);
        if (invalid is not null)
        {
            return Result<RequiredSkillDto>.Failure(invalid);
        }

        if (await _issues.GetByIdAsync(issueId) is null)
        {
            return Result<RequiredSkillDto>.Failure(IssueErrors.NotFound);
        }

        var catalog = (await _skills.GetSkillsAsync()).ToDictionary(skill => skill.Id);
        if (!catalog.TryGetValue(request.SkillId, out var skillInfo) || !skillInfo.IsActive)
        {
            return Result<RequiredSkillDto>.Failure(RequirementErrors.SkillUnknown);
        }

        var existing = await _requirements.GetRequiredSkillsAsync([issueId]);
        if (existing.Any(skill => skill.SkillId == request.SkillId))
        {
            return Result<RequiredSkillDto>.Failure(RequirementErrors.SkillDuplicate);
        }

        if (existing.Count >= MaxSkillsPerIssue)
        {
            return Result<RequiredSkillDto>.Failure(RequirementErrors.TooManySkills);
        }

        var required = new IssueRequiredSkill
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            SkillId = request.SkillId,
            MinLevel = (sbyte)request.MinLevel,
            Weight = request.Weight,
            Source = Manual
        };
        await _requirements.AddRequiredSkillAsync(required);
        await _issues.AddActivityAsync(
            IssueActivities.Create(issueId, actorUserId, IssueActivities.SkillRequired, Now(), "RequiredSkill", newValue: skillInfo.Name));
        await _requirements.SaveChangesAsync();
        return Result.Success(ToDto(required, catalog.ToDictionary(pair => pair.Key, pair => pair.Value.Name)));
    }

    public async Task<Result<RequiredSkillDto>> UpdateRequiredSkillAsync(
        Guid requiredSkillId,
        SaveRequiredSkillDto request)
    {
        var invalid = Validate(_skillValidator, request);
        if (invalid is not null)
        {
            return Result<RequiredSkillDto>.Failure(invalid);
        }

        var required = await _requirements.GetRequiredSkillAsync(requiredSkillId);
        if (required is null)
        {
            return Result<RequiredSkillDto>.Failure(RequirementErrors.RequiredSkillNotFound);
        }

        if (request.SkillId != required.SkillId)
        {
            return Result<RequiredSkillDto>.Failure(RequirementErrors.SkillImmutable);
        }

        required.MinLevel = (sbyte)request.MinLevel;
        required.Weight = request.Weight;
        await _requirements.SaveChangesAsync();
        return Result.Success(ToDto(required, await SkillNamesAsync()));
    }

    public async Task<Result> DeleteRequiredSkillAsync(Guid requiredSkillId, Guid actorUserId)
    {
        var required = await _requirements.GetRequiredSkillAsync(requiredSkillId);
        if (required is null)
        {
            return Result.Failure(RequirementErrors.RequiredSkillNotFound);
        }

        var names = await SkillNamesAsync();
        _requirements.RemoveRequiredSkill(required);
        await _issues.AddActivityAsync(
            IssueActivities.Create(required.IssueId, actorUserId, IssueActivities.SkillUnrequired, Now(), "RequiredSkill",
                oldValue: names.GetValueOrDefault(required.SkillId, required.SkillId.ToString())));
        await _requirements.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<Guid>> GetRequiredSkillIssueIdAsync(Guid requiredSkillId)
    {
        var required = await _requirements.GetRequiredSkillAsync(requiredSkillId);
        return required is null ? Result<Guid>.Failure(RequirementErrors.RequiredSkillNotFound) : Result.Success(required.IssueId);
    }

    /// <summary>Contract cho AI phân công (B): kỹ năng yêu cầu của nhiều Issue trong một lần đọc.</summary>
    async Task<IReadOnlyList<IssueRequiredSkillDto>> IIssueSkillService.GetRequiredSkillsAsync(
        IEnumerable<Guid> issueIds) =>
        (await _requirements.GetRequiredSkillsAsync(issueIds))
            .Select(skill => new IssueRequiredSkillDto(skill.IssueId, skill.SkillId, skill.MinLevel, skill.Weight, skill.Source))
            .ToList();

    // ---------- Helpers ----------

    private async Task<IReadOnlyList<AcceptanceCriterionDto>> CriteriaDtosAsync(Guid issueId)
    {
        var criteria = await _requirements.GetCriteriaAsync(issueId);
        var names = await NamesAsync(criteria.Where(c => c.MetBy.HasValue).Select(c => c.MetBy!.Value));
        return criteria.Select(c => ToDto(c, names)).ToList();
    }

    private async Task<AcceptanceCriterionDto> ToDtoAsync(AcceptanceCriteria criterion) =>
        ToDto(criterion, criterion.MetBy.HasValue ? await NamesAsync([criterion.MetBy.Value]) : []);

    private static AcceptanceCriterionDto ToDto(AcceptanceCriteria c, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = c.Id,
        IssueId = c.IssueId,
        Content = c.Content,
        IsMet = c.IsMet,
        MetBy = c.MetBy,
        MetByName = c.MetBy.HasValue ? names.GetValueOrDefault(c.MetBy.Value) : null,
        MetAt = c.MetAt,
        OrderIndex = c.OrderIndex,
        Source = c.Source,
        AiGenerationLogId = c.AiGenerationLogId,
        WasEditedAfterAi = c.WasEditedAfterAi,
        CreatedAt = c.CreatedAt
    };

    private static RequiredSkillDto ToDto(IssueRequiredSkill skill, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = skill.Id,
        IssueId = skill.IssueId,
        SkillId = skill.SkillId,
        SkillName = names.GetValueOrDefault(skill.SkillId, string.Empty),
        MinLevel = skill.MinLevel,
        Weight = skill.Weight,
        Source = skill.Source
    };

    private async Task<Dictionary<Guid, string>> SkillNamesAsync() =>
        (await _skills.GetSkillsAsync()).ToDictionary(skill => skill.Id, skill => skill.Name);

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return ids.Count == 0 ? [] : (await _users.GetDisplayInfoAsync(ids)).ToDictionary(u => u.UserId, u => u.DisplayName);
    }

    private static Error? Validate<T>(IValidator<T> validator, T request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = validator.Validate(request);
        return validation.IsValid ? null : IssueTrackingService.ToError(validation);
    }

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;
}

public static class RequirementErrors
{
    public static readonly Error CriterionNotFound = Error.NotFound("CRITERIA_NOT_FOUND", "Không tìm thấy tiêu chí nghiệm thu.");
    public static readonly Error TooManyCriteria = Error.Validation("CRITERIA_LIMIT_REACHED", $"Mỗi Issue tối đa {IssueRequirementService.MaxCriteriaPerIssue} tiêu chí.");
    public static readonly Error ReorderMismatch = Error.Conflict("CRITERIA_REORDER_MISMATCH", "Danh sách tiêu chí đã thay đổi, hãy tải lại rồi sắp xếp.");
    public static readonly Error RequiredSkillNotFound = Error.NotFound("REQUIRED_SKILL_NOT_FOUND", "Không tìm thấy kỹ năng yêu cầu.");
    public static readonly Error SkillUnknown = Error.Validation("REQUIRED_SKILL_UNKNOWN", "Kỹ năng không có trong danh mục hoặc đã ngừng dùng.");
    public static readonly Error SkillDuplicate = Error.Conflict("REQUIRED_SKILL_DUPLICATE", "Issue đã yêu cầu kỹ năng này.");
    public static readonly Error SkillImmutable = Error.Validation("REQUIRED_SKILL_IMMUTABLE", "Không đổi kỹ năng; hãy xóa rồi thêm kỹ năng khác.");
    public static readonly Error TooManySkills = Error.Validation("REQUIRED_SKILL_LIMIT_REACHED", $"Mỗi Issue tối đa {IssueRequirementService.MaxSkillsPerIssue} kỹ năng yêu cầu.");
}
