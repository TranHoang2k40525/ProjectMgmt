namespace ProjectMgmt.Solution.Services;

using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure;

public static class TestAccountSeeder
{
    public const string DefaultPassword = "@123456xX";

    private sealed record SeedAccountSpec(
        string Email,
        string DisplayName,
        string JobTitle,
        string Department,
        string OrgRole,
        string ProjectRoleName,
        string[] SkillCodes,
        bool IsSystemAdmin = false);

    private static readonly SeedAccountSpec[] Accounts =
    [
        new(
            "hoangteam01@gmail.com",
            "Hoàng Team 01 (Admin)",
            "Quản trị viên Hệ thống",
            "Ban Công nghệ & Bảo mật",
            "Admin",
            "ProjectManager",
            ["CSHARP_DOTNET", "MYSQL_DB"],
            IsSystemAdmin: true),
        new(
            "hoangteam02@gmail.com",
            "Hoàng Team 02 (Giám đốc DA)",
            "Giám đốc Dự án / Product Owner",
            "Ban Quản lý Dự án",
            "Admin",
            "ProjectManager",
            ["CSHARP_DOTNET", "ANGULAR_TS"]),
        new(
            "hoangteam03@gmail.com",
            "Hoàng Team 03 (Scrum Master)",
            "Chuyên gia Scrum Master",
            "Phòng Điều phối Agile",
            "Member",
            "ScrumMaster",
            ["CSHARP_DOTNET", "ANGULAR_TS"]),
        new(
            "hoangteam04@gmail.com",
            "Hoàng Team 04 (Backend Dev)",
            "Kỹ sư Backend .NET",
            "Phòng Kỹ thuật Phần mềm",
            "Member",
            "Developer",
            ["CSHARP_DOTNET", "MYSQL_DB"]),
        new(
            "hoangteam05@gmail.com",
            "Hoàng Team 05 (Frontend Dev)",
            "Kỹ sư Frontend Angular",
            "Phòng Kỹ thuật Phần mềm",
            "Member",
            "Developer",
            ["ANGULAR_TS"]),
        new(
            "hoangteam06@gmail.com",
            "Hoàng Team 06 (QA Tester)",
            "Kỹ sư Kiểm thử Chất lượng (QA)",
            "Phòng Đảm bảo Chất lượng",
            "Member",
            "QA",
            ["ANGULAR_TS", "MYSQL_DB"]),
        new(
            "hoangteam07@gmail.com",
            "Hoàng Team 07 (Viewer)",
            "Chuyên viên Quan sát & Nghiệm thu",
            "Khối Nghiệp vụ Khách hàng",
            "Member",
            "Viewer",
            ["MYSQL_DB"])
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var loggerFactory = services.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("TestAccountSeeder");

        try
        {
            var identityDb = services.GetRequiredService<IdentityExperienceDbContext>();
            var planningDb = services.GetRequiredService<PlanningDbContext>();
            var passwordService = services.GetRequiredService<IPasswordService>();

            var now = DateTime.UtcNow;
            var passwordHash = passwordService.Hash(DefaultPassword);

            // Locate all matching projects ("Quản lý dự án" / "QLDA") across organizations (including HUCE)
            var targetProjects = await planningDb.Projects.AsNoTracking()
                .Where(p => !p.IsDeleted && (p.Name.Contains("Quản lý dự án") || p.ProjectKey == "QLDA"))
                .OrderByDescending(p => p.Name == "Quản lý dự án")
                .ThenBy(p => p.CreatedAt)
                .ToListAsync();

            if (targetProjects.Count == 0)
            {
                var fallbackProj = await planningDb.Projects.AsNoTracking().Where(p => !p.IsDeleted).FirstOrDefaultAsync();
                if (fallbackProj != null)
                {
                    targetProjects.Add(fallbackProj);
                }
            }

            if (targetProjects.Count == 0)
            {
                return;
            }

            var targetProject = targetProjects[0];
            var orgId = targetProject.OrgId;
            var projectId = targetProject.Id;
            var inviterUserId = targetProject.LeadUserId;

            // Ensure Project & System Roles exist
            var allRoles = await identityDb.Roles.ToListAsync();

            Role EnsureProjectRole(string roleName, string description)
            {
                var role = allRoles.FirstOrDefault(r =>
                    r.Scope == "Project" && r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
                if (role == null)
                {
                    role = new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName,
                        Scope = "Project",
                        Description = description,
                        IsSystem = true
                    };
                    identityDb.Roles.Add(role);
                    allRoles.Add(role);
                }
                return role;
            }

            var pmRole = EnsureProjectRole("ProjectManager", "Quản lý dự án / Giám đốc dự án");
            var smRole = EnsureProjectRole("ScrumMaster", "Điều phối Scrum & Sprint");
            var devRole = EnsureProjectRole("Developer", "Kỹ sư phát triển phần mềm");
            var qaRole = EnsureProjectRole("QA", "Kỹ sư kiểm thử chất lượng");
            var viewerRole = EnsureProjectRole("Viewer", "Người xem chỉ đọc");
            await identityDb.SaveChangesAsync();

            // Ensure ScrumMaster has sprint/issue permissions like ProjectManager
            var allPerms = await identityDb.Permissions.AsNoTracking().ToListAsync();
            var existingRolePerms = await identityDb.RolePermissions.AsNoTracking().ToListAsync();

            void GrantPermissionsToRole(Guid roleId, Func<Permission, bool> predicate)
            {
                foreach (var perm in allPerms.Where(predicate))
                {
                    if (!existingRolePerms.Any(rp => rp.RoleId == roleId && rp.PermissionId == perm.Id))
                    {
                        var rp = new RolePermission { RoleId = roleId, PermissionId = perm.Id };
                        identityDb.RolePermissions.Add(rp);
                        existingRolePerms.Add(rp);
                    }
                }
            }

            GrantPermissionsToRole(pmRole.Id, _ => true);
            GrantPermissionsToRole(smRole.Id, p =>
                p.Code.StartsWith("project.", StringComparison.OrdinalIgnoreCase) ||
                p.Code.StartsWith("sprint.", StringComparison.OrdinalIgnoreCase) ||
                p.Code.StartsWith("issue.", StringComparison.OrdinalIgnoreCase) ||
                p.Code.StartsWith("backlog.", StringComparison.OrdinalIgnoreCase) ||
                p.Code.StartsWith("member.", StringComparison.OrdinalIgnoreCase) ||
                p.Code.StartsWith("workflow.", StringComparison.OrdinalIgnoreCase));
            GrantPermissionsToRole(devRole.Id, p =>
                p.Code is "project.read" or "issue.create" or "issue.update" or "issue.transition" or "backlog.rank");
            GrantPermissionsToRole(qaRole.Id, p =>
                p.Code is "project.read" or "issue.create" or "issue.update" or "issue.transition");
            GrantPermissionsToRole(viewerRole.Id, p =>
                p.Code is "project.read");

            await identityDb.SaveChangesAsync();

            var sysAdminRole = allRoles.FirstOrDefault(r => r.Scope == "System" || r.Name == "Admin" || r.Name == "SystemAdmin");
            var orgMemberRole = allRoles.FirstOrDefault(r => r.Scope == "Organization");
            var skills = await identityDb.SkillCatalogs.AsNoTracking().ToListAsync();

            foreach (var spec in Accounts)
            {
                var normalizedEmail = spec.Email.ToUpperInvariant();
                var user = await identityDb.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
                if (user == null)
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = spec.Email,
                        NormalizedEmail = normalizedEmail,
                        PasswordHash = passwordHash,
                        IsEmailVerified = true,
                        IsActive = true,
                        SecurityStamp = Guid.NewGuid(),
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    identityDb.Users.Add(user);
                    await identityDb.SaveChangesAsync();
                }
                else
                {
                    user.IsEmailVerified = true;
                    user.IsActive = true;
                    user.PasswordHash = passwordHash;
                    if (user.SecurityStamp == Guid.Empty) user.SecurityStamp = Guid.NewGuid();
                    await identityDb.SaveChangesAsync();
                }

                // Ensure UserProfile
                var profile = await identityDb.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (profile == null)
                {
                    profile = new UserProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        DisplayName = spec.DisplayName,
                        JobTitle = spec.JobTitle,
                        SeniorityLevel = spec.Department,
                        Timezone = "Asia/Ho_Chi_Minh",
                        YearsOfExperience = 4.0m,
                        Bio = $"Thành viên đội dự án Scrum ({spec.JobTitle})",
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    identityDb.UserProfiles.Add(profile);
                }
                else
                {
                    profile.DisplayName = spec.DisplayName;
                    profile.JobTitle = spec.JobTitle;
                    profile.SeniorityLevel = spec.Department;
                }

                // Ensure UserSkills
                var existingUserSkills = await identityDb.UserSkills
                    .Where(us => us.UserId == user.Id)
                    .Select(us => us.SkillId)
                    .ToListAsync();

                foreach (var code in spec.SkillCodes)
                {
                    var skillObj = skills.FirstOrDefault(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                    if (skillObj != null && !existingUserSkills.Contains(skillObj.Id))
                    {
                        identityDb.UserSkills.Add(new UserSkill
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            SkillId = skillObj.Id,
                            ProficiencyLevel = 4,
                            YearsOfExperience = 3.0m,
                            IsSelfDeclared = true,
                            UpdatedAt = now
                        });
                    }
                }

                // System Admin role if applicable
                if (spec.IsSystemAdmin && sysAdminRole != null)
                {
                    var hasSys = await identityDb.UserRoles.AnyAsync(ur =>
                        ur.UserId == user.Id && ur.RoleId == sysAdminRole.Id && ur.ScopeType == "System");
                    if (!hasSys)
                    {
                        identityDb.UserRoles.Add(new UserRole
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            RoleId = sysAdminRole.Id,
                            ScopeType = "System",
                            ScopeId = null,
                            GrantedBy = inviterUserId,
                            CreatedAt = now
                        });
                    }
                }

                foreach (var proj in targetProjects)
                {
                    // Organization Role & Membership
                    if (orgMemberRole != null)
                    {
                        var hasOrgRole = await identityDb.UserRoles.AnyAsync(ur =>
                            ur.UserId == user.Id && ur.ScopeType == "Organization" && ur.ScopeId == proj.OrgId);
                        if (!hasOrgRole)
                        {
                            identityDb.UserRoles.Add(new UserRole
                            {
                                Id = Guid.NewGuid(),
                                UserId = user.Id,
                                RoleId = orgMemberRole.Id,
                                ScopeType = "Organization",
                                ScopeId = proj.OrgId,
                                GrantedBy = proj.LeadUserId,
                                CreatedAt = now
                            });
                        }
                    }

                    try
                    {
                        await planningDb.Database.ExecuteSqlInterpolatedAsync($@"
                            INSERT INTO OrganizationMember (Id, OrgId, UserId, Role, JoinedAt)
                            VALUES ({Guid.NewGuid().ToString()}, {proj.OrgId.ToString()}, {user.Id.ToString()}, {spec.OrgRole}, UTC_TIMESTAMP(6))
                            ON DUPLICATE KEY UPDATE Role = VALUES(Role);");
                    }
                    catch
                    {
                        // Ignore if OrganizationMember table not present in test context
                    }

                    // Project Role assignment
                    var targetRole = allRoles.FirstOrDefault(r =>
                        r.Scope == "Project" && r.Name.Equals(spec.ProjectRoleName, StringComparison.OrdinalIgnoreCase)) ?? devRole;

                    var existingProjRoles = await identityDb.UserRoles
                        .Where(ur => ur.UserId == user.Id && ur.ScopeType == "Project" && ur.ScopeId == proj.Id)
                        .ToListAsync();

                    if (existingProjRoles.Count == 0)
                    {
                        identityDb.UserRoles.Add(new UserRole
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            RoleId = targetRole.Id,
                            ScopeType = "Project",
                            ScopeId = proj.Id,
                            GrantedBy = proj.LeadUserId,
                            CreatedAt = now
                        });
                    }
                    else if (existingProjRoles[0].RoleId != targetRole.Id)
                    {
                        existingProjRoles[0].RoleId = targetRole.Id;
                    }

                    // In-App ProjectInvitation Notification so the user can view & respond to invitation on the app
                    var hasInviteNotif = await identityDb.Notifications.AnyAsync(n =>
                        n.UserId == user.Id && n.ProjectId == proj.Id && n.Type == "ProjectInvitation");

                    if (!hasInviteNotif)
                    {
                        identityDb.Notifications.Add(new Notification
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            Type = "ProjectInvitation",
                            Title = $"Lời mời tham gia dự án {proj.Name}",
                            Content = $"Bạn đã được mời tham gia dự án \"{proj.Name}\" với vai trò {targetRole.Name}. Nhấn Chấp nhận trên thông báo để xác nhận.",
                            EntityType = "Project",
                            EntityId = proj.Id,
                            ProjectId = proj.Id,
                            ActorId = proj.LeadUserId,
                            IsRead = false,
                            CreatedAt = now
                        });
                    }

                    await identityDb.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Không thể khởi tạo dữ liệu tài khoản thử nghiệm hoangteam01..07");
        }
    }
}
