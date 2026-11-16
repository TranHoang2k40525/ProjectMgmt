using System.Text.RegularExpressions;
using FluentValidation;

namespace DeliveryIntelligence.Application.Dto;

public class LabelDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
}

public class SaveLabelDto
{
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
}

public class CreateIssueLinkDto
{
    public Guid TargetIssueId { get; set; }

    /// <summary>Blocks | IsBlockedBy | Relates | Duplicates | IsDuplicatedBy (nhìn từ Issue hiện tại).</summary>
    public string LinkType { get; set; } = string.Empty;
}

public class IssueLinkDto
{
    public Guid Id { get; set; }
    public string LinkType { get; set; } = string.Empty;
    public Guid TargetIssueId { get; set; }
    public int TargetIssueNumber { get; set; }
    public string TargetTitle { get; set; } = string.Empty;
    public Guid TargetStatusId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static partial class LabelRules
{
    public const string DefaultColor = "#DFE1E6";

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    public static partial Regex ColorPattern();
}

public class SaveLabelDtoValidator : AbstractValidator<SaveLabelDto>
{
    public SaveLabelDtoValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("LABEL_NAME_REQUIRED").WithMessage("Tên nhãn không được để trống.")
            .Must(name => name is null || name.Trim().Length <= 60).WithErrorCode("LABEL_NAME_TOO_LONG").WithMessage("Tên nhãn tối đa 60 ký tự.");
        RuleFor(x => x.ColorHex)
            .Must(color => color is null || LabelRules.ColorPattern().IsMatch(color)).WithErrorCode("LABEL_COLOR_INVALID").WithMessage("Màu phải có dạng #RRGGBB.");
    }
}
