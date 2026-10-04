using System.Text.RegularExpressions;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;

namespace IdentityExperience.Application.Services;

public class RoleServices : IRoleServices
{
    private static readonly HashSet<string> Scopes = ["System", "Organization", "Project"];
    private static readonly Regex RoleNamePattern = new(
        "^[A-Za-z][A-Za-z0-9_-]{1,79}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IRbacRepository _rbacRepository;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationServices? _notificationServices;

    public RoleServices(
        IRbacRepository rbacRepository,
        TimeProvider timeProvider,
        INotificationServices? notificationServices = null)
    {
        _rbacRepository = rbacRepository;
        _timeProvider = timeProvider;
        _notificationServices = notificationServices;
    }

    public async Task<RoleDto> GetRolesAsync(string? scope)
    {
        if (scope is not null && !Scopes.Contains(scope.Trim()))
        {
            return Failure("RBAC_SCOPE_INVALID", "Phạm vi vai trò không hợp lệ.");
        }

        var roles = await _rbacRepository.GetRolesAsync(scope);
        return new RoleDto
        {
            Success = true,
            Items = roles.Select(MapRole).ToList()
        };
    }

    public async Task<RoleDto> GetPermissionsAsync()
    {
        var permissions = await _rbacRepository.GetPermissionsAsync();
        return new RoleDto
        {
            Success = true,
            Items = permissions.Select(MapPermission).ToList()
        };
    }

    public async Task<RoleDto> CreateRoleAsync(RoleDto request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var scope = request.Scope?.Trim() ?? string.Empty;
        var description = OptionalText(request.Description);

        if (!RoleNamePattern.IsMatch(name))
        {
            return Failure(
                "RBAC_ROLE_NAME_INVALID",
                "Tên vai trò phải dài 2-80 ký tự, bắt đầu bằng chữ và chỉ chứa chữ, số, gạch ngang hoặc gạch dưới.");
        }

        if (scope is not ("Organization" or "Project"))
        {
            return Failure("RBAC_SCOPE_INVALID", "Chỉ được tạo vai trò Organization hoặc Project.");
        }

        if (description?.Length > 255)
        {
            return Failure("RBAC_DESCRIPTION_TOO_LONG", "Mô tả không được vượt quá 255 ký tự.");
        }

        if (request.PermissionIds is null || request.PermissionIds.Count > 100)
        {
            return Failure("RBAC_PERMISSIONS_INVALID", "Danh sách quyền không hợp lệ.");
        }

        var result = await _rbacRepository.CreateRoleAsync(
            new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
                Scope = scope,
                IsSystem = false,
                Description = description
            },
            request.PermissionIds);
        return result.Status switch
        {
            RbacWriteStatus.Created => new RoleDto
            {
                Success = true,
                Message = "Tạo vai trò thành công.",
                RoleId = result.Role!.Id,
                Name = result.Role.Name,
                Scope = result.Role.Scope,
                UpdatedPermissionCount = result.UpdatedCount
            },
            RbacWriteStatus.PermissionNotFound =>
                Failure("RBAC_PERMISSION_NOT_FOUND", "Có quyền không tồn tại."),
            _ => Failure("RBAC_ROLE_NAME_EXISTS", "Tên vai trò đã tồn tại.")
        };
    }

    public async Task<RoleDto> UpdateRolePermissionsAsync(
        Guid roleId,
        List<Guid>? permissionIds)
    {
        if (roleId == Guid.Empty || permissionIds is null || permissionIds.Count > 100)
        {
            return Failure("RBAC_PERMISSIONS_INVALID", "Vai trò hoặc danh sách quyền không hợp lệ.");
        }

        var result = await _rbacRepository.UpdateRolePermissionsAsync(roleId, permissionIds);
        return result.Status switch
        {
            RbacWriteStatus.Updated => new RoleDto
            {
                Success = true,
                Message = "Cập nhật quyền của vai trò thành công.",
                RoleId = roleId,
                UpdatedPermissionCount = result.UpdatedCount
            },
            RbacWriteStatus.RoleNotFound => Failure("RBAC_ROLE_NOT_FOUND", "Không tìm thấy vai trò."),
            _ => Failure("RBAC_PERMISSION_NOT_FOUND", "Có quyền không tồn tại.")
        };
    }

    public async Task<RoleDto> GetUserRolesAsync(
        Guid userId,
        string? scopeType,
        Guid? scopeId)
    {
        if (userId == Guid.Empty
            || (scopeType is not null && !Scopes.Contains(scopeType.Trim())))
        {
            return Failure("RBAC_SCOPE_INVALID", "Người dùng hoặc phạm vi không hợp lệ.");
        }

        var roles = await _rbacRepository.GetUserRolesAsync(userId, scopeType, scopeId);
        return new RoleDto
        {
            Success = true,
            UserId = userId,
            Roles = roles.Select(MapScopedRole).ToList()
        };
    }

    public async Task<RoleDto> AssignUserRoleAsync(
        Guid actorUserId,
        Guid userId,
        RoleDto request)
    {
        if (actorUserId == userId)
        {
            return Failure("RBAC_SELF_ESCALATION_FORBIDDEN", "Không được tự gán vai trò cho chính mình.");
        }

        if (!request.RoleId.HasValue
            || !request.ScopeId.HasValue
            || request.ScopeType is not ("Organization" or "Project"))
        {
            return Failure("RBAC_ASSIGNMENT_INVALID", "Thông tin gán vai trò không hợp lệ.");
        }

        var result = await _rbacRepository.GrantRoleAsync(
            userId,
            request.RoleId.Value,
            request.ScopeType,
            request.ScopeId,
            actorUserId,
            _timeProvider.GetUtcNow().UtcDateTime);
        return MapAssignmentResult(result);
    }

    public async Task<RoleDto> RemoveUserRoleAsync(
        Guid actorUserId,
        Guid userId,
        Guid userRoleId)
    {
        if (actorUserId == userId)
        {
            return Failure("RBAC_SELF_CHANGE_FORBIDDEN", "Không được tự gỡ vai trò của chính mình.");
        }

        var result = await _rbacRepository.RemoveUserRoleAsync(userId, userRoleId);
        return result.Status switch
        {
            RbacWriteStatus.Removed => new RoleDto
            {
                Success = true,
                Message = "Đã gỡ vai trò khỏi người dùng.",
                UserId = userId,
                UserRoleId = userRoleId,
                Removed = true
            },
            RbacWriteStatus.LastProjectManager => LastManagerFailure(),
            _ => Failure("RBAC_USER_ROLE_NOT_FOUND", "Không tìm thấy vai trò đã gán.")
        };
    }

    public async Task<RoleDto> GetProjectMembersAsync(Guid projectId)
    {
        var members = await _rbacRepository.GetProjectMembersAsync(projectId);
        return new RoleDto
        {
            Success = true,
            ScopeType = "Project",
            ScopeId = projectId,
            Members = members.Select(MapMember).ToList()
        };
    }

    public async Task<RoleDto> AddProjectMemberAsync(
        Guid actorUserId,
        Guid projectId,
        Guid? userId,
        Guid? roleId)
    {
        if (!userId.HasValue || !roleId.HasValue)
        {
            return Failure("RBAC_MEMBER_INPUT_INVALID", "Người dùng và vai trò là bắt buộc.");
        }

        if (actorUserId == userId.Value)
        {
            return Failure("RBAC_SELF_ESCALATION_FORBIDDEN", "Không được tự thêm hoặc nâng vai trò của chính mình.");
        }

        var result = await _rbacRepository.AddProjectMemberAsync(
            userId.Value,
            roleId.Value,
            projectId,
            actorUserId,
            _timeProvider.GetUtcNow().UtcDateTime);
        var response = result.Status switch
        {
            RbacWriteStatus.Created => AssignmentSuccess(result, "Đã thêm thành viên vào dự án."),
            RbacWriteStatus.UserNotFound => Failure("RBAC_USER_NOT_FOUND", "Không tìm thấy người dùng hoạt động."),
            RbacWriteStatus.RoleNotFound => Failure("RBAC_PROJECT_ROLE_NOT_FOUND", "Vai trò dự án không tồn tại."),
            _ => Failure("RBAC_MEMBER_ALREADY_EXISTS", "Người dùng đã là thành viên dự án.")
        };

        if (response.Success)
        {
            await PublishProjectNotificationAsync(
                userId.Value,
                actorUserId,
                projectId,
                NotificationTypes.ProjectInvitation,
                "Bạn đã được thêm vào dự án",
                $"Bạn đã được thêm vào dự án với vai trò {response.RoleName}.",
                response.RoleName);
        }

        return response;
    }

    public async Task<RoleDto> ChangeProjectMemberRoleAsync(
        Guid actorUserId,
        Guid projectId,
        Guid userId,
        Guid? newRoleId)
    {
        if (actorUserId == userId)
        {
            return Failure("RBAC_SELF_CHANGE_FORBIDDEN", "Không được tự thay đổi vai trò của chính mình.");
        }

        if (!newRoleId.HasValue)
        {
            return Failure("RBAC_PROJECT_ROLE_REQUIRED", "Vai trò mới là bắt buộc.");
        }

        var result = await _rbacRepository.ChangeProjectMemberRoleAsync(
            userId,
            newRoleId.Value,
            projectId,
            actorUserId,
            _timeProvider.GetUtcNow().UtcDateTime);
        var response = result.Status switch
        {
            RbacWriteStatus.Updated => new RoleDto
            {
                Success = true,
                Message = "Đã thay đổi vai trò thành viên.",
                UserId = userId,
                RoleId = result.Role!.Id,
                NewRoleId = result.Role.Id,
                RoleName = result.Role.Name,
                NewRoleName = result.Role.Name,
                UserRoleId = result.UserRole!.Id
            },
            RbacWriteStatus.LastProjectManager => LastManagerFailure(),
            RbacWriteStatus.MemberNotFound => Failure("RBAC_MEMBER_NOT_FOUND", "Không tìm thấy thành viên dự án."),
            _ => Failure("RBAC_PROJECT_ROLE_NOT_FOUND", "Vai trò dự án không tồn tại.")
        };

        if (response.Success)
        {
            await PublishProjectNotificationAsync(
                userId,
                actorUserId,
                projectId,
                NotificationTypes.ProjectRoleChanged,
                "Vai trò dự án đã thay đổi",
                $"Vai trò mới của bạn là {response.NewRoleName}.",
                response.NewRoleName);
        }

        return response;
    }

    public async Task<RoleDto> RemoveProjectMemberAsync(
        Guid actorUserId,
        Guid projectId,
        Guid userId)
    {
        if (actorUserId == userId)
        {
            return Failure("RBAC_SELF_CHANGE_FORBIDDEN", "Không được tự gỡ mình khỏi dự án qua API quản trị.");
        }

        var result = await _rbacRepository.RemoveProjectMemberAsync(userId, projectId);
        var response = result.Status switch
        {
            RbacWriteStatus.Removed => new RoleDto
            {
                Success = true,
                Message = "Đã gỡ thành viên khỏi dự án.",
                UserId = userId,
                ScopeId = projectId,
                Removed = true
            },
            RbacWriteStatus.LastProjectManager => LastManagerFailure(),
            _ => Failure("RBAC_MEMBER_NOT_FOUND", "Không tìm thấy thành viên dự án.")
        };

        if (response.Success)
        {
            await PublishProjectNotificationAsync(
                userId,
                actorUserId,
                projectId,
                NotificationTypes.ProjectRoleRevoked,
                "Quyền truy cập dự án đã được thu hồi",
                "Bạn đã được gỡ khỏi dự án.",
                null);
        }

        return response;
    }

    private Task<bool> PublishProjectNotificationAsync(
        Guid userId,
        Guid actorUserId,
        Guid projectId,
        string type,
        string title,
        string content,
        string? roleName)
    {
        if (_notificationServices is null)
        {
            return Task.FromResult(false);
        }

        return _notificationServices.PublishAsync(new NotificationDto
        {
            UserId = userId,
            Type = type,
            Title = title,
            Content = content,
            EntityType = "Project",
            EntityId = projectId,
            ProjectId = projectId,
            ActorId = actorUserId,
            RoleName = roleName,
            SendEmail = true
        });
    }

    private static RoleDto MapRole(Role role)
    {
        return new RoleDto
        {
            RoleId = role.Id,
            Name = role.Name,
            Scope = role.Scope,
            IsSystem = role.IsSystem,
            Description = role.Description
        };
    }

    private static RoleDto MapPermission(Permission permission)
    {
        return new RoleDto
        {
            PermissionId = permission.Id,
            Code = permission.Code,
            Grouping = permission.Grouping,
            Description = permission.Description
        };
    }

    private static RoleDto MapScopedRole(ScopedRoleDetails details)
    {
        return new RoleDto
        {
            UserRoleId = details.UserRole.Id,
            UserId = details.UserRole.UserId,
            RoleId = details.Role.Id,
            RoleName = details.Role.Name,
            ScopeType = details.UserRole.ScopeType,
            ScopeId = details.UserRole.ScopeId,
            GrantedBy = details.UserRole.GrantedBy,
            GrantedAt = details.UserRole.CreatedAt
        };
    }

    private static RoleDto MapMember(ProjectMemberDetails member)
    {
        var primaryRole = member.Roles.FirstOrDefault();
        return new RoleDto
        {
            UserId = member.User.Id,
            FullName = member.Profile?.DisplayName,
            Email = member.User.Email,
            AvatarUrl = member.Profile?.AvatarUrl,
            RoleId = primaryRole?.Role.Id,
            RoleName = primaryRole?.Role.Name,
            GrantedAt = primaryRole?.UserRole.CreatedAt,
            Roles = member.Roles.Select(MapScopedRole).ToList()
        };
    }

    private static RoleDto MapAssignmentResult(RbacWriteResult result)
    {
        return result.Status switch
        {
            RbacWriteStatus.Created => AssignmentSuccess(result, "Gán vai trò thành công."),
            RbacWriteStatus.UserNotFound => Failure("RBAC_USER_NOT_FOUND", "Không tìm thấy người dùng."),
            RbacWriteStatus.RoleNotFound => Failure("RBAC_ROLE_NOT_FOUND", "Không tìm thấy vai trò."),
            RbacWriteStatus.ScopeMismatch => Failure("RBAC_SCOPE_MISMATCH", "Vai trò không thuộc phạm vi yêu cầu."),
            _ => Failure("RBAC_ROLE_ALREADY_ASSIGNED", "Vai trò đã được gán trong phạm vi này.")
        };
    }

    private static RoleDto AssignmentSuccess(RbacWriteResult result, string message)
    {
        return new RoleDto
        {
            Success = true,
            Message = message,
            UserRoleId = result.UserRole!.Id,
            UserId = result.UserRole.UserId,
            RoleId = result.Role!.Id,
            RoleName = result.Role.Name,
            ScopeType = result.UserRole.ScopeType,
            ScopeId = result.UserRole.ScopeId,
            GrantedAt = result.UserRole.CreatedAt
        };
    }

    private static RoleDto LastManagerFailure()
    {
        return Failure(
            "RBAC_LAST_PROJECT_MANAGER",
            "Dự án phải luôn còn ít nhất một Project Manager.");
    }

    private static RoleDto Failure(string errorCode, string message)
    {
        return new RoleDto { Success = false, ErrorCode = errorCode, Message = message };
    }

    private static string? OptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
