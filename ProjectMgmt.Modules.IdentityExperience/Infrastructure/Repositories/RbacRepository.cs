using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IdentityExperience.Infrastructure.Repository;

public class RbacRepository : IRbacRepository
{
    private const string ProjectManagerRoleName = "ProjectManager";
    private readonly IdentityExperienceDbContext _context;

    public RbacRepository(IdentityExperienceDbContext context)
    {
        _context = context;
    }

    public Task<List<Role>> GetRolesAsync(string? scope)
    {
        var query = _context.Roles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(scope))
        {
            var normalizedScope = scope.Trim();
            query = query.Where(role => role.Scope == normalizedScope);
        }

        return query.OrderBy(role => role.Scope).ThenBy(role => role.Name).ToListAsync();
    }

    public Task<List<Permission>> GetPermissionsAsync()
    {
        return _context.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Grouping)
            .ThenBy(permission => permission.Code)
            .ToListAsync();
    }

    public async Task<RbacWriteResult> CreateRoleAsync(Role role, List<Guid> permissionIds)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var distinctPermissionIds = permissionIds.Distinct().ToList();
            var permissionCount = await _context.Permissions
                .CountAsync(permission => distinctPermissionIds.Contains(permission.Id));
            if (permissionCount != distinctPermissionIds.Count)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.PermissionNotFound);
            }

            try
            {
                await _context.Roles.AddAsync(role);
                await _context.RolePermissions.AddRangeAsync(
                    distinctPermissionIds.Select(permissionId => new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionId
                    }));
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return new RbacWriteResult
                {
                    Status = RbacWriteStatus.Created,
                    Role = role,
                    UpdatedCount = distinctPermissionIds.Count
                };
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is MySqlException { Number: 1062 })
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Result(RbacWriteStatus.Conflict);
            }
        });
    }

    public async Task<RbacWriteResult> UpdateRolePermissionsAsync(
        Guid roleId,
        List<Guid> permissionIds)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedRoles = await _context.Roles
                .FromSqlInterpolated($"SELECT * FROM `Role` WHERE `Id` = {roleId} FOR UPDATE")
                .ToListAsync();
            var role = lockedRoles.SingleOrDefault();
            if (role is null)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.RoleNotFound);
            }

            var distinctPermissionIds = permissionIds.Distinct().ToList();
            var permissionCount = await _context.Permissions
                .CountAsync(permission => distinctPermissionIds.Contains(permission.Id));
            if (permissionCount != distinctPermissionIds.Count)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.PermissionNotFound);
            }

            var currentPermissions = await _context.RolePermissions
                .Where(item => item.RoleId == roleId)
                .ToListAsync();
            _context.RolePermissions.RemoveRange(currentPermissions);
            await _context.RolePermissions.AddRangeAsync(
                distinctPermissionIds.Select(permissionId => new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId
                }));

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new RbacWriteResult
            {
                Status = RbacWriteStatus.Updated,
                Role = role,
                UpdatedCount = distinctPermissionIds.Count
            };
        });
    }

    public Task<List<ScopedRoleDetails>> GetUserRolesAsync(
        Guid userId,
        string? scopeType,
        Guid? scopeId)
    {
        var query = from userRole in _context.UserRoles.AsNoTracking()
                    join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                    where userRole.UserId == userId
                    select new ScopedRoleDetails
                    {
                        UserRole = userRole,
                        Role = role
                    };
        if (!string.IsNullOrWhiteSpace(scopeType))
        {
            var normalizedScopeType = scopeType.Trim();
            query = query.Where(details => details.UserRole.ScopeType == normalizedScopeType);
        }

        if (scopeId.HasValue)
        {
            query = query.Where(details => details.UserRole.ScopeId == scopeId);
        }

        return query
            .OrderBy(details => details.UserRole.ScopeType)
            .ThenBy(details => details.Role.Name)
            .ToListAsync();
    }

    public async Task<RbacWriteResult> GrantRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var userExists = await _context.Users.AnyAsync(user => user.Id == userId);
            if (!userExists)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.UserNotFound);
            }

            var role = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == roleId);
            if (role is null)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.RoleNotFound);
            }

            if (!ScopeMatches(role, scopeType, scopeId))
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.ScopeMismatch);
            }

            var exists = await _context.UserRoles.AnyAsync(item =>
                item.UserId == userId
                && item.RoleId == roleId
                && item.ScopeType == scopeType
                && item.ScopeId == scopeId);
            if (exists)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.DuplicateRole);
            }

            var userRole = NewUserRole(userId, roleId, scopeType, scopeId, grantedBy, createdAtUtc);
            try
            {
                await _context.UserRoles.AddAsync(userRole);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return new RbacWriteResult
                {
                    Status = RbacWriteStatus.Created,
                    Role = role,
                    UserRole = userRole
                };
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is MySqlException { Number: 1062 })
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Result(RbacWriteStatus.DuplicateRole);
            }
        });
    }

    public async Task<RbacWriteResult> GrantRoleForProvisioningAsync(
        Guid userId,
        string roleName,
        string scopeType,
        Guid scopeId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        var userExists = await _context.Users.AnyAsync(user => user.Id == userId && user.IsActive);
        if (!userExists)
        {
            return Result(RbacWriteStatus.UserNotFound);
        }

        var role = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Name == roleName && item.Scope == scopeType);
        if (role is null)
        {
            return Result(RbacWriteStatus.RoleNotFound);
        }

        var exists = await _context.UserRoles.AnyAsync(item =>
            item.UserId == userId
            && item.RoleId == role.Id
            && item.ScopeType == scopeType
            && item.ScopeId == scopeId);
        if (exists)
        {
            return Result(RbacWriteStatus.DuplicateRole);
        }

        var userRole = NewUserRole(
            userId,
            role.Id,
            scopeType,
            scopeId,
            grantedBy,
            createdAtUtc);
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();
        return new RbacWriteResult
        {
            Status = RbacWriteStatus.Created,
            Role = role,
            UserRole = userRole
        };
    }

    public async Task<RbacWriteResult> RemoveUserRoleAsync(Guid userId, Guid userRoleId)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var preview = await _context.UserRoles.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == userRoleId && item.UserId == userId);
            if (preview is null)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.UserRoleNotFound);
            }

            List<UserRole> lockedScopeAssignments;
            UserRole? assignment;
            if (preview.ScopeType == "Project" && preview.ScopeId.HasValue)
            {
                lockedScopeAssignments = await LockProjectAssignmentsAsync(preview.ScopeId.Value);
                assignment = lockedScopeAssignments.SingleOrDefault(item => item.Id == userRoleId);
            }
            else
            {
                lockedScopeAssignments = [];
                var lockedAssignments = await _context.UserRoles
                    .FromSqlInterpolated($"SELECT * FROM `UserRole` WHERE `Id` = {userRoleId} FOR UPDATE")
                    .ToListAsync();
                assignment = lockedAssignments.SingleOrDefault();
            }

            if (assignment is null)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.UserRoleNotFound);
            }

            var role = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == assignment.RoleId);
            if (role?.Name == ProjectManagerRoleName
                && assignment.ScopeType == "Project"
                && assignment.ScopeId.HasValue
                && lockedScopeAssignments
                    .Where(item => item.RoleId == role.Id)
                    .Select(item => item.UserId)
                    .Distinct()
                    .Count() <= 1)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.LastProjectManager);
            }

            _context.UserRoles.Remove(assignment);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result(RbacWriteStatus.Removed);
        });
    }

    public async Task<List<ProjectMemberDetails>> GetProjectMembersAsync(Guid projectId)
    {
        var rows = await (from userRole in _context.UserRoles.AsNoTracking()
                          join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                          join user in _context.Users.AsNoTracking() on userRole.UserId equals user.Id
                          join profile in _context.UserProfiles.AsNoTracking()
                              on user.Id equals profile.UserId into profiles
                          from profile in profiles.DefaultIfEmpty()
                          where userRole.ScopeType == "Project" && userRole.ScopeId == projectId
                          select new ProjectMemberRow
                          {
                              User = user,
                              Profile = profile,
                              UserRole = userRole,
                              Role = role
                          })
            .ToListAsync();

        return rows
            .GroupBy(row => row.User.Id)
            .Select(group => new ProjectMemberDetails
            {
                User = group.First().User,
                Profile = group.First().Profile,
                Roles = group
                    .OrderBy(row => row.Role.Name)
                    .Select(row => new ScopedRoleDetails
                    {
                        UserRole = row.UserRole,
                        Role = row.Role
                    })
                    .ToList()
            })
            .OrderBy(member => member.Profile?.DisplayName ?? member.User.Email)
            .ToList();
    }

    public async Task<RbacWriteResult> AddProjectMemberAsync(
        Guid userId,
        Guid roleId,
        Guid projectId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var userExists = await _context.Users.AnyAsync(user => user.Id == userId && user.IsActive);
            if (!userExists)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.UserNotFound);
            }

            var role = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == roleId && item.Scope == "Project");
            if (role is null)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.RoleNotFound);
            }

            var projectAssignments = await LockProjectAssignmentsAsync(projectId);
            var isMember = projectAssignments.Any(item => item.UserId == userId);
            if (isMember)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.MemberAlreadyExists);
            }

            var userRole = NewUserRole(
                userId,
                roleId,
                "Project",
                projectId,
                grantedBy,
                createdAtUtc);
            await _context.UserRoles.AddAsync(userRole);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new RbacWriteResult
            {
                Status = RbacWriteStatus.Created,
                Role = role,
                UserRole = userRole
            };
        });
    }

    public async Task<RbacWriteResult> ChangeProjectMemberRoleAsync(
        Guid userId,
        Guid newRoleId,
        Guid projectId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var newRole = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(role => role.Id == newRoleId && role.Scope == "Project");
            if (newRole is null)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.RoleNotFound);
            }

            var projectAssignments = await LockProjectAssignmentsAsync(projectId);
            var assignments = projectAssignments.Where(item => item.UserId == userId).ToList();
            if (assignments.Count == 0)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.MemberNotFound);
            }

            var currentRoleIds = assignments.Select(item => item.RoleId).ToList();
            var projectManagerRoleId = await _context.Roles.AsNoTracking()
                .Where(role => role.Name == ProjectManagerRoleName)
                .Select(role => role.Id)
                .FirstOrDefaultAsync();
            var removesProjectManager = newRole.Name != ProjectManagerRoleName
                && currentRoleIds.Contains(projectManagerRoleId);
            if (removesProjectManager
                && projectAssignments
                    .Where(item => item.RoleId == projectManagerRoleId)
                    .Select(item => item.UserId)
                    .Distinct()
                    .Count() <= 1)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.LastProjectManager);
            }

            _context.UserRoles.RemoveRange(assignments);
            var replacement = NewUserRole(
                userId,
                newRoleId,
                "Project",
                projectId,
                grantedBy,
                createdAtUtc);
            await _context.UserRoles.AddAsync(replacement);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new RbacWriteResult
            {
                Status = RbacWriteStatus.Updated,
                Role = newRole,
                UserRole = replacement
            };
        });
    }

    public async Task<RbacWriteResult> RemoveProjectMemberAsync(Guid userId, Guid projectId)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var projectAssignments = await LockProjectAssignmentsAsync(projectId);
            var assignments = projectAssignments.Where(item => item.UserId == userId).ToList();
            if (assignments.Count == 0)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.MemberNotFound);
            }

            var roleIds = assignments.Select(item => item.RoleId).ToList();
            var projectManagerRoleId = await _context.Roles.AsNoTracking()
                .Where(role => role.Name == ProjectManagerRoleName)
                .Select(role => role.Id)
                .FirstOrDefaultAsync();
            var isProjectManager = roleIds.Contains(projectManagerRoleId);
            if (isProjectManager
                && projectAssignments
                    .Where(item => item.RoleId == projectManagerRoleId)
                    .Select(item => item.UserId)
                    .Distinct()
                    .Count() <= 1)
            {
                await transaction.RollbackAsync();
                return Result(RbacWriteStatus.LastProjectManager);
            }

            _context.UserRoles.RemoveRange(assignments);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result(RbacWriteStatus.Removed);
        });
    }

    public Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        string scopeType,
        Guid? scopeId,
        Guid? organizationId)
    {
        return (from userRole in _context.UserRoles.AsNoTracking()
                join rolePermission in _context.RolePermissions.AsNoTracking()
                    on userRole.RoleId equals rolePermission.RoleId
                join permission in _context.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                where userRole.UserId == userId
                      && permission.Code == permissionCode
                      && ((userRole.ScopeType == "System" && userRole.ScopeId == null)
                          || (userRole.ScopeType == scopeType && userRole.ScopeId == scopeId)
                          || (scopeType == "Project"
                              && organizationId.HasValue
                              && userRole.ScopeType == "Organization"
                              && userRole.ScopeId == organizationId))
                select userRole.Id)
            .AnyAsync();
    }

    public Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
    {
        return _context.UserRoles.AsNoTracking().AnyAsync(item =>
            item.UserId == userId
            && item.ScopeType == "Project"
            && item.ScopeId == projectId);
    }

    private Task<List<UserRole>> LockProjectAssignmentsAsync(Guid projectId)
    {
        return _context.UserRoles
            .FromSqlInterpolated($"""
                SELECT * FROM `UserRole`
                WHERE `ScopeType` = 'Project' AND `ScopeId` = {projectId}
                FOR UPDATE
                """)
            .ToListAsync();
    }

    private static bool ScopeMatches(Role role, string scopeType, Guid? scopeId)
    {
        return role.Scope == scopeType
            && (scopeType == "System" ? scopeId is null : scopeId.HasValue);
    }

    private static UserRole NewUserRole(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        return new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            GrantedBy = grantedBy,
            CreatedAt = createdAtUtc
        };
    }

    private static RbacWriteResult Result(RbacWriteStatus status)
    {
        return new RbacWriteResult { Status = status };
    }

    private class ProjectMemberRow
    {
        public User User { get; set; } = new();
        public UserProfile? Profile { get; set; }
        public UserRole UserRole { get; set; } = new();
        public Role Role { get; set; } = new();
    }
}
