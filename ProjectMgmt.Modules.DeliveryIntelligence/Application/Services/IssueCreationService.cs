using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.IRepositories;
using FluentValidation;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace DeliveryIntelligence.Application.Services;

/// <summary>
/// Tạo Issue (SCRUMAI-175): kiểm Project/loại/thành viên qua contract, lấy trạng thái khởi tạo từ Planning,
/// xin IssueNumber từ IIssueNumberGenerator, xếp RankOrder cuối Backlog và ghi ActivityLog "Created".
/// </summary>
public class IssueCreationService : IIssueCreationService
{
    private readonly IIssueRepository _issues;
    private readonly IProjectLookupService _projects;
    private readonly IPermissionEvaluator _permissions;
    private readonly IIssueNumberGenerator _numbers;
    private readonly IssueHierarchyLoader _hierarchy;
    private readonly IValidator<CreateIssueDto> _validator;
    private readonly TimeProvider _clock;

    public IssueCreationService(
        IIssueRepository issues,
        IProjectLookupService projects,
        IPermissionEvaluator permissions,
        IIssueNumberGenerator numbers,
        IssueHierarchyLoader hierarchy,
        IValidator<CreateIssueDto> validator,
        TimeProvider clock)
    {
        _issues = issues;
        _projects = projects;
        _permissions = permissions;
        _numbers = numbers;
        _hierarchy = hierarchy;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<IssueDetailDto>> CreateAsync(
        CreateIssueDto request,
        Guid reporterUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _validator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<IssueDetailDto>.Failure(IssueTrackingService.ToError(validation));
        }

        var scope = await _projects.GetProjectScopeAsync(request.ProjectId);
        if (scope is null || scope.IsArchived)
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.ProjectNotFound);
        }

        if (!await _permissions.IsProjectMemberAsync(reporterUserId, request.ProjectId))
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.ReporterNotMember);
        }

        if (request.AssigneeId.HasValue && !await _permissions.IsProjectMemberAsync(request.AssigneeId.Value, request.ProjectId))
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.AssigneeNotMember);
        }

        var types = await _hierarchy.GetTypesAsync(request.ProjectId, cancellationToken);
        if (!types.TryGetValue(request.IssueTypeId, out var type))
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.TypeInvalid);
        }

        var issueId = Guid.NewGuid();
        var placement = await _hierarchy.PlaceAsync(
            issueId, request.ProjectId, type, request.ParentId, request.EpicId, types, cancellationToken);
        if (placement.Violation is not null)
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.FromRule(placement.Violation));
        }

        var statuses = await _projects.GetStatusesAsync(request.ProjectId, cancellationToken);
        var initial = statuses.Where(status => status.IsInitial).OrderBy(status => status.OrderIndex).FirstOrDefault();
        if (initial is null)
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.InitialStatusMissing);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var issue = IssueMapper.ToNewEntity(request, issueId, now);
        issue.ParentId = placement.ParentId;
        issue.EpicId = placement.EpicId;
        issue.StatusId = initial.Id;
        issue.ReporterId = reporterUserId;
        issue.RankOrder = request.RankOrder ?? (await _issues.GetMaxRankAsync(request.ProjectId, cancellationToken) ?? 0) + 1;
        issue.IssueNumber = await _numbers.ReserveNextAsync(request.ProjectId, cancellationToken);

        await _issues.AddAsync(issue, cancellationToken);
        await _issues.AddActivityAsync(
            IssueActivities.Create(issue.Id, reporterUserId, IssueActivities.Created, now),
            cancellationToken);
        await _issues.SaveChangesAsync(cancellationToken);
        return Result.Success(IssueMapper.ToDetail(issue));
    }
}
