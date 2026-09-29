using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Application.Services;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Xunit;

namespace ProjectMgmt.Tests;

public class IdentityRbacTests
{
    private static readonly DateTime TestNowUtc = new(2026, 9, 30, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RoleServiceRejectsSelfEscalationBeforeRepositoryWrite()
    {
        var repository = new FakeRbacRepository();
        var service = new RoleServices(repository, new FixedTimeProvider(TestNowUtc));
        var actorUserId = Guid.NewGuid();

        var result = await service.AssignUserRoleAsync(actorUserId, actorUserId, new RoleDto
        {
            RoleId = Guid.NewGuid(),
            ScopeType = "Project",
            ScopeId = Guid.NewGuid()
        });

        Assert.False(result.Success);
        Assert.Equal("RBAC_SELF_ESCALATION_FORBIDDEN", result.ErrorCode);
        Assert.False(repository.GrantCalled);
    }

    [Fact]
    public async Task RoleServiceSurfacesLastProjectManagerInvariant()
    {
        var repository = new FakeRbacRepository
        {
            ChangeResult = new RbacWriteResult
            {
                Status = RbacWriteStatus.LastProjectManager
            }
        };
        var service = new RoleServices(repository, new FixedTimeProvider(TestNowUtc));

        var result = await service.ChangeProjectMemberRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("RBAC_LAST_PROJECT_MANAGER", result.ErrorCode);
    }

    [Fact]
    public async Task PermissionEvaluatorPassesProjectAndOrganizationScopeToRepository()
    {
        var repository = new FakeRbacRepository { PermissionAllowed = true };
        var evaluator = new PermissionEvaluator(repository);
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var allowed = await evaluator.HasPermissionAsync(
            userId,
            "member.read",
            "Project",
            projectId,
            organizationId);

        Assert.True(allowed);
        Assert.Equal(userId, repository.PermissionUserId);
        Assert.Equal("member.read", repository.PermissionCode);
        Assert.Equal(projectId, repository.PermissionScopeId);
        Assert.Equal(organizationId, repository.PermissionOrganizationId);
    }

    [Fact]
    public async Task PublicRoleCreationCannotCreateSystemRole()
    {
        var service = new RoleServices(
            new FakeRbacRepository(),
            new FixedTimeProvider(TestNowUtc));

        var result = await service.CreateRoleAsync(new RoleDto
        {
            Name = "SecondAdmin",
            Scope = "System",
            PermissionIds = []
        });

        Assert.False(result.Success);
        Assert.Equal("RBAC_SCOPE_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task AddingProjectMemberPublishesInvitationAfterRoleWrite()
    {
        var repository = new FakeRbacRepository();
        var notifications = new CapturingNotificationServices();
        var service = new RoleServices(
            repository,
            new FixedTimeProvider(TestNowUtc),
            notifications);
        var projectId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();

        var result = await service.AddProjectMemberAsync(
            actorUserId,
            projectId,
            memberUserId,
            Guid.NewGuid());

        Assert.True(result.Success);
        Assert.NotNull(notifications.Published);
        Assert.Equal(NotificationTypes.ProjectInvitation, notifications.Published.Type);
        Assert.Equal(memberUserId, notifications.Published.UserId);
        Assert.Equal(projectId, notifications.Published.ProjectId);
        Assert.Equal(actorUserId, notifications.Published.ActorId);
        Assert.True(notifications.Published.SendEmail);
    }
}

public class CapturingNotificationServices : INotificationServices
{
    public NotificationDto? Published { get; private set; }

    public Task<NotificationDto> GetInboxAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize)
    {
        return Task.FromResult(new NotificationDto { Success = true });
    }

    public Task<NotificationDto> GetUnreadCountAsync(Guid userId)
    {
        return Task.FromResult(new NotificationDto { Success = true });
    }

    public Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId)
    {
        return Task.FromResult(new NotificationDto { Success = true });
    }

    public Task<NotificationDto> MarkAllReadAsync(Guid userId)
    {
        return Task.FromResult(new NotificationDto { Success = true });
    }

    public Task<bool> PublishAsync(NotificationDto notification)
    {
        Published = notification;
        return Task.FromResult(true);
    }
}

public class FakeRbacRepository : IRbacRepository
{
    public bool GrantCalled { get; private set; }
    public bool PermissionAllowed { get; set; }
    public Guid? PermissionUserId { get; private set; }
    public string? PermissionCode { get; private set; }
    public Guid? PermissionScopeId { get; private set; }
    public Guid? PermissionOrganizationId { get; private set; }
    public RbacWriteResult ChangeResult { get; set; } = new()
    {
        Status = RbacWriteStatus.Updated,
        Role = new Role { Id = Guid.NewGuid(), Name = "Developer", Scope = "Project" },
        UserRole = new UserRole { Id = Guid.NewGuid(), ScopeType = "Project" }
    };

    public Task<List<Role>> GetRolesAsync(string? scope)
    {
        return Task.FromResult(new List<Role>());
    }

    public Task<List<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult(new List<Permission>());
    }

    public Task<RbacWriteResult> CreateRoleAsync(Role role, List<Guid> permissionIds)
    {
        return Task.FromResult(new RbacWriteResult
        {
            Status = RbacWriteStatus.Created,
            Role = role,
            UpdatedCount = permissionIds.Count
        });
    }

    public Task<RbacWriteResult> UpdateRolePermissionsAsync(Guid roleId, List<Guid> permissionIds)
    {
        return Task.FromResult(new RbacWriteResult
        {
            Status = RbacWriteStatus.Updated,
            Role = new Role { Id = roleId },
            UpdatedCount = permissionIds.Count
        });
    }

    public Task<List<ScopedRoleDetails>> GetUserRolesAsync(
        Guid userId,
        string? scopeType,
        Guid? scopeId)
    {
        return Task.FromResult(new List<ScopedRoleDetails>());
    }

    public Task<RbacWriteResult> GrantRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        GrantCalled = true;
        return Task.FromResult(new RbacWriteResult
        {
            Status = RbacWriteStatus.Created,
            Role = new Role { Id = roleId, Name = "Developer", Scope = scopeType },
            UserRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = roleId,
                ScopeType = scopeType,
                ScopeId = scopeId,
                CreatedAt = createdAtUtc
            }
        });
    }

    public Task<RbacWriteResult> GrantRoleForProvisioningAsync(
        Guid userId,
        string roleName,
        string scopeType,
        Guid scopeId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        return GrantRoleAsync(
            userId,
            Guid.NewGuid(),
            scopeType,
            scopeId,
            grantedBy,
            createdAtUtc);
    }

    public Task<RbacWriteResult> RemoveUserRoleAsync(Guid userId, Guid userRoleId)
    {
        return Task.FromResult(new RbacWriteResult { Status = RbacWriteStatus.Removed });
    }

    public Task<List<ProjectMemberDetails>> GetProjectMembersAsync(Guid projectId)
    {
        return Task.FromResult(new List<ProjectMemberDetails>());
    }

    public Task<RbacWriteResult> AddProjectMemberAsync(
        Guid userId,
        Guid roleId,
        Guid projectId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        return GrantRoleAsync(userId, roleId, "Project", projectId, grantedBy, createdAtUtc);
    }

    public Task<RbacWriteResult> ChangeProjectMemberRoleAsync(
        Guid userId,
        Guid newRoleId,
        Guid projectId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        return Task.FromResult(ChangeResult);
    }

    public Task<RbacWriteResult> RemoveProjectMemberAsync(Guid userId, Guid projectId)
    {
        return Task.FromResult(new RbacWriteResult { Status = RbacWriteStatus.Removed });
    }

    public Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        string scopeType,
        Guid? scopeId,
        Guid? organizationId)
    {
        PermissionUserId = userId;
        PermissionCode = permissionCode;
        PermissionScopeId = scopeId;
        PermissionOrganizationId = organizationId;
        return Task.FromResult(PermissionAllowed);
    }

    public Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
    {
        return Task.FromResult(true);
    }
}
