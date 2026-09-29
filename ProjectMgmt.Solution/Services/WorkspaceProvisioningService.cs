using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planning.Infrastructure;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using MySqlConnector;

namespace ProjectMgmt.Solution.Services;

public interface IWorkspaceProvisioningService
{
    Task<Result<OrganizationDto>> CreateOrganizationAsync(
        CreateOrganizationDto request,
        Guid ownerUserId);

    Task<Result<ProjectDto>> CreateProjectAsync(
        CreateProjectRequestDto request,
        Guid creatorUserId);
}

public class WorkspaceProvisioningService : IWorkspaceProvisioningService
{
    private readonly PlanningDbContext _planningContext;
    private readonly IdentityExperienceDbContext _identityContext;
    private readonly IProjectManagementService _projectManagement;
    private readonly IRoleAssignmentService _roleAssignment;
    private readonly TimeProvider _timeProvider;

    public WorkspaceProvisioningService(
        PlanningDbContext planningContext,
        IdentityExperienceDbContext identityContext,
        IProjectManagementService projectManagement,
        IRoleAssignmentService roleAssignment,
        TimeProvider timeProvider)
    {
        _planningContext = planningContext;
        _identityContext = identityContext;
        _projectManagement = projectManagement;
        _roleAssignment = roleAssignment;
        _timeProvider = timeProvider;
    }

    public async Task<Result<OrganizationDto>> CreateOrganizationAsync(
        CreateOrganizationDto request,
        Guid ownerUserId)
    {
        try
        {
            return await ExecuteAsync(async () =>
            {
                var result = await _projectManagement.CreateOrganizationAsync(request, ownerUserId);
                if (result.IsFailure)
                {
                    return result;
                }

                var granted = await _roleAssignment.GrantRoleForProvisioningAsync(
                    ownerUserId,
                    "OrgOwner",
                    "Organization",
                    result.Value.Id,
                    ownerUserId,
                    _timeProvider.GetUtcNow().UtcDateTime);
                return granted
                    ? result
                    : Result<OrganizationDto>.Failure(Error.Failure(
                        "ORG_OWNER_ASSIGNMENT_FAILED",
                        "Không thể cấp vai trò chủ sở hữu tổ chức."));
            });
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlException { Number: 1062 })
        {
            return Result<OrganizationDto>.Failure(Error.Conflict(
                "ORGANIZATION_SLUG_EXISTS",
                "Đường dẫn tổ chức đã tồn tại."));
        }
    }

    public async Task<Result<ProjectDto>> CreateProjectAsync(
        CreateProjectRequestDto request,
        Guid creatorUserId)
    {
        try
        {
            return await ExecuteAsync(async () =>
            {
                var result = await _projectManagement.CreateProjectAsync(request, creatorUserId);
                if (result.IsFailure)
                {
                    return result;
                }

                var granted = await _roleAssignment.GrantRoleForProvisioningAsync(
                    creatorUserId,
                    "ProjectManager",
                    "Project",
                    result.Value.Id,
                    creatorUserId,
                    _timeProvider.GetUtcNow().UtcDateTime);
                return granted
                    ? result
                    : Result<ProjectDto>.Failure(Error.Failure(
                        "PROJECT_MANAGER_ASSIGNMENT_FAILED",
                        "Không thể cấp vai trò Project Manager cho người tạo dự án."));
            });
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlException { Number: 1062 })
        {
            return Result<ProjectDto>.Failure(Error.Conflict(
                "PROJECT_KEY_EXISTS",
                "Mã dự án đã tồn tại trong tổ chức."));
        }
    }

    private Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> operation)
    {
        var planningConnection = _planningContext.Database.GetDbConnection();
        var identityConnection = _identityContext.Database.GetDbConnection();
        if (!ReferenceEquals(planningConnection, identityConnection))
        {
            throw new InvalidOperationException(
                "Planning và IdentityExperience phải dùng cùng DbConnection trong provisioning transaction.");
        }

        var strategy = _planningContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _planningContext.Database.BeginTransactionAsync();
            await using var identityTransaction = await _identityContext.Database.UseTransactionAsync(
                transaction.GetDbTransaction());

            var result = await operation();
            if (result.IsFailure)
            {
                await transaction.RollbackAsync();
                return result;
            }

            await transaction.CommitAsync();
            return result;
        });
    }
}
