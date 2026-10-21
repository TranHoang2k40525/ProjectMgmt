using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.IRepositories;
using FluentValidation;
using FluentValidation.Results;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.Services;

public class IssueTrackingService : IIssueTrackingService
{
    private readonly IIssueRepository _issues;
    private readonly IValidator<CreateIssueDto> _createValidator;
    private readonly IValidator<UpdateIssueDto> _updateValidator;
    private readonly IValidator<IssueListQueryDto> _listValidator;

    public IssueTrackingService(
        IIssueRepository issues,
        IValidator<CreateIssueDto> createValidator,
        IValidator<UpdateIssueDto> updateValidator,
        IValidator<IssueListQueryDto> listValidator)
    {
        _issues = issues;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _listValidator = listValidator;
    }

    public async Task<Result<IssueDetailDto>> GetAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        return issue is null
            ? Result<IssueDetailDto>.Failure(IssueErrors.NotFound)
            : Result.Success(IssueMapper.ToDetail(issue));
    }

    public async Task<Result<PagedResultDto<IssueListItemDto>>> ListAsync(
        Guid projectId,
        IssueListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (projectId == Guid.Empty)
        {
            return Result<PagedResultDto<IssueListItemDto>>.Failure(
                Error.Validation("ISSUE_PROJECT_REQUIRED", "Thiếu Project."));
        }

        var validation = _listValidator.Validate(query);
        if (!validation.IsValid)
        {
            return Result<PagedResultDto<IssueListItemDto>>.Failure(ToError(validation));
        }

        var page = await _issues.ListAsync(IssueMapper.ToFilter(projectId, query), cancellationToken);
        return Result.Success(new PagedResultDto<IssueListItemDto>
        {
            Items = page.Items.Select(issue => IssueMapper.ToListItem(issue)).ToList(),
            TotalCount = page.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    public Result ValidateCreate(CreateIssueDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        return validation.IsValid ? Result.Success() : Result.Failure(ToError(validation));
    }

    public Result ValidateUpdate(UpdateIssueDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _updateValidator.Validate(request);
        return validation.IsValid ? Result.Success() : Result.Failure(ToError(validation));
    }

    private static Error ToError(ValidationResult validation)
    {
        var first = validation.Errors[0];
        return Error.Validation(first.ErrorCode, first.ErrorMessage);
    }
}

public static class IssueErrors
{
    public static readonly Error NotFound = Error.NotFound("ISSUE_NOT_FOUND", "Không tìm thấy Issue.");
}
