using FluentValidation;

namespace DeliveryIntelligence.Application.Dto;

public class AcceptanceCriterionDto
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public Guid? MetBy { get; set; }
    public string? MetByName { get; set; }
    public DateTime? MetAt { get; set; }
    public int OrderIndex { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? AiGenerationLogId { get; set; }
    public bool WasEditedAfterAi { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveCriterionDto
{
    public string Content { get; set; } = string.Empty;
    public bool IsMet { get; set; }
}

public class ReorderCriteriaDto
{
    public List<Guid> OrderedIds { get; set; } = [];
}

public class RequiredSkillDto
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int MinLevel { get; set; }
    public decimal Weight { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class SaveRequiredSkillDto
{
    public Guid SkillId { get; set; }
    public int MinLevel { get; set; } = 1;
    public decimal Weight { get; set; } = 1m;
}

public class SaveCriterionDtoValidator : AbstractValidator<SaveCriterionDto>
{
    public const int ContentMaxLength = 4000;

    public SaveCriterionDtoValidator()
    {
        RuleFor(x => x.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content)).WithErrorCode("CRITERIA_CONTENT_REQUIRED").WithMessage("Nhập tiêu chí nghiệm thu.")
            .MaximumLength(ContentMaxLength).WithErrorCode("CRITERIA_CONTENT_TOO_LONG").WithMessage($"Tiêu chí tối đa {ContentMaxLength} ký tự.");
    }
}

public class SaveRequiredSkillDtoValidator : AbstractValidator<SaveRequiredSkillDto>
{
    public SaveRequiredSkillDtoValidator()
    {
        RuleFor(x => x.SkillId).NotEmpty().WithErrorCode("REQUIRED_SKILL_SKILL_REQUIRED").WithMessage("Chọn kỹ năng.");
        RuleFor(x => x.MinLevel).InclusiveBetween(1, 5).WithErrorCode("REQUIRED_SKILL_LEVEL_INVALID").WithMessage("Mức tối thiểu từ 1 đến 5.");
        RuleFor(x => x.Weight)
            .GreaterThan(0).WithErrorCode("REQUIRED_SKILL_WEIGHT_INVALID").WithMessage("Trọng số phải lớn hơn 0 và nhỏ hơn 10.")
            .LessThanOrEqualTo(9.999m).WithErrorCode("REQUIRED_SKILL_WEIGHT_INVALID").WithMessage("Trọng số phải lớn hơn 0 và nhỏ hơn 10.")
            .PrecisionScale(4, 3, true).WithErrorCode("REQUIRED_SKILL_WEIGHT_INVALID").WithMessage("Trọng số tối đa 3 chữ số thập phân.");
    }
}
