using FluentValidation;

namespace DeliveryIntelligence.Application.Dto;

internal static class IssueFieldRules
{
    public const int TitleMaxLength = 500;
    public const int DescriptionMaxLength = 100_000;
    public const decimal StoryPointsMax = 9999.99m;
    public const int EstimateMinutesMax = 1_000_000;
    public static readonly DateOnly DueDateMin = new(2000, 1, 1);
    public static readonly DateOnly DueDateMax = new(2100, 12, 31);

    public static void Title<T>(IRuleBuilder<T, string> rule) => rule
        .Must(title => !string.IsNullOrWhiteSpace(title)).WithErrorCode("ISSUE_TITLE_REQUIRED").WithMessage("Tiêu đề không được để trống.")
        .Must(title => title is null || title.Trim().Length <= TitleMaxLength).WithErrorCode("ISSUE_TITLE_TOO_LONG").WithMessage($"Tiêu đề tối đa {TitleMaxLength} ký tự.");

    public static void Description<T>(IRuleBuilder<T, string?> rule) => rule
        .MaximumLength(DescriptionMaxLength).WithErrorCode("ISSUE_DESCRIPTION_TOO_LONG").WithMessage($"Mô tả tối đa {DescriptionMaxLength} ký tự.");

    public static void StoryPoints<T>(IRuleBuilder<T, decimal?> rule) => rule
        .InclusiveBetween(0, StoryPointsMax).WithErrorCode("ISSUE_STORY_POINTS_INVALID").WithMessage("Story points phải từ 0 đến 9999.99.")
        .PrecisionScale(6, 2, true).WithErrorCode("ISSUE_STORY_POINTS_INVALID").WithMessage("Story points tối đa 2 chữ số thập phân.");

    public static void Estimate<T>(IRuleBuilder<T, int?> rule) => rule
        .InclusiveBetween(0, EstimateMinutesMax).WithErrorCode("ISSUE_ESTIMATE_INVALID").WithMessage("Thời gian ước lượng (phút) không hợp lệ.");

    public static void DueDate<T>(IRuleBuilder<T, DateOnly?> rule) => rule
        .Must(date => date is null || (date >= DueDateMin && date <= DueDateMax)).WithErrorCode("ISSUE_DUE_DATE_INVALID").WithMessage("Hạn hoàn thành không hợp lệ.");

    public static void OptionalId<T>(IRuleBuilder<T, Guid?> rule, string code) => rule
        .Must(id => id is null || id != Guid.Empty).WithErrorCode(code).WithMessage("Mã tham chiếu không hợp lệ.");
}

public class CreateIssueDtoValidator : AbstractValidator<CreateIssueDto>
{
    public CreateIssueDtoValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty().WithErrorCode("ISSUE_PROJECT_REQUIRED").WithMessage("Thiếu Project.");
        RuleFor(x => x.IssueTypeId).NotEmpty().WithErrorCode("ISSUE_TYPE_REQUIRED").WithMessage("Thiếu loại Issue.");
        IssueFieldRules.Title(RuleFor(x => x.Title));
        IssueFieldRules.Description(RuleFor(x => x.Description));
        IssueFieldRules.StoryPoints(RuleFor(x => x.StoryPoints));
        IssueFieldRules.Estimate(RuleFor(x => x.OriginalEstimateMinutes));
        IssueFieldRules.DueDate(RuleFor(x => x.DueDate));
        IssueFieldRules.OptionalId(RuleFor(x => x.PriorityId), "ISSUE_PRIORITY_INVALID");
        IssueFieldRules.OptionalId(RuleFor(x => x.AssigneeId), "ISSUE_ASSIGNEE_INVALID");
        IssueFieldRules.OptionalId(RuleFor(x => x.ParentId), "ISSUE_PARENT_INVALID");
        IssueFieldRules.OptionalId(RuleFor(x => x.EpicId), "ISSUE_EPIC_LINK_INVALID");
        IssueFieldRules.OptionalId(RuleFor(x => x.SprintId), "ISSUE_SPRINT_INVALID");
    }
}

public class UpdateIssueDtoValidator : AbstractValidator<UpdateIssueDto>
{
    public UpdateIssueDtoValidator()
    {
        IssueFieldRules.Title(RuleFor(x => x.Title));
        IssueFieldRules.Description(RuleFor(x => x.Description));
        IssueFieldRules.StoryPoints(RuleFor(x => x.StoryPoints));
        IssueFieldRules.Estimate(RuleFor(x => x.OriginalEstimateMinutes));
        IssueFieldRules.DueDate(RuleFor(x => x.DueDate));
        IssueFieldRules.OptionalId(RuleFor(x => x.PriorityId), "ISSUE_PRIORITY_INVALID");
        IssueFieldRules.OptionalId(RuleFor(x => x.AssigneeId), "ISSUE_ASSIGNEE_INVALID");
    }
}

public class IssueListQueryDtoValidator : AbstractValidator<IssueListQueryDto>
{
    public IssueListQueryDtoValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("ISSUE_PAGE_INVALID").WithMessage("Trang phải từ 1 trở lên.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("ISSUE_PAGE_SIZE_INVALID").WithMessage("Số dòng mỗi trang từ 1 đến 100.");
        RuleFor(x => x.SearchText).MaximumLength(200).WithErrorCode("ISSUE_SEARCH_TOO_LONG").WithMessage("Từ khóa tìm kiếm tối đa 200 ký tự.");
        RuleFor(x => x.SprintId).Null().When(x => x.BacklogOnly).WithErrorCode("ISSUE_FILTER_CONFLICT").WithMessage("Không thể lọc đồng thời Backlog và Sprint.");
    }
}
