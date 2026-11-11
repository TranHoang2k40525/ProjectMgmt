namespace ProjectMgmt.Solution.Services;

using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planning.Domain.Entities;
using Planning.Infrastructure;

public static class TestAccountSeeder
{
    public const string DefaultPassword = "@123456xX";

    private sealed record SkillCatalogSeed(string Code, string Name, string Category);

    private sealed record UserSkillSeed(string Code, sbyte ProficiencyLevel, decimal YearsOfExperience);

    private sealed record SeedAccountSpec(
        string Email,
        string DisplayName,
        string JobTitle,
        string SeniorityLevel,
        decimal YearsOfExperience,
        string Bio,
        string OrgRole,
        string ProjectRoleName,
        decimal CapacityPoints,
        decimal AvailableHours,
        UserSkillSeed[] Skills,
        bool IsSystemAdmin = false);

    private static readonly SkillCatalogSeed[] SkillCatalogSeeds =
    [
        new("CSHARP_DOTNET", "C# / .NET 9 Web API", "Backend"),
        new("EF_CORE", "Entity Framework Core & LINQ", "Backend"),
        new("MYSQL_DB", "MySQL & Tối ưu Truy vấn SQL", "Database"),
        new("REDIS_CACHE", "Redis Caching & Distributed State", "Infrastructure"),
        new("ANGULAR_TS", "Angular 19, TypeScript & Signals", "Frontend"),
        new("UI_TAILWIND", "TailwindCSS, Responsive UI & GSAP", "Frontend"),
        new("SIGNALR_SSE", "SignalR & Server-Sent Events (SSE)", "Realtime"),
        new("PLAYWRIGHT_QA", "Playwright E2E & Kiểm thử Tự động", "QA"),
        new("API_SECURITY", "Bảo mật OWASP, JWT & Phân quyền RBAC", "Security"),
        new("SCRUM_AGILE", "Điều phối Scrum, Quản trị Sprint & Backlog", "Management"),
        new("SYS_ARCH", "Kiến trúc Hệ thống, Microservices & MCP", "Architecture")
    ];

    private static readonly SeedAccountSpec[] Accounts =
    [
        new(
            "hoangteam01@gmail.com",
            "Trần Minh Hoàng (Hoàng Team 01 - Admin)",
            "Kỹ sư Trưởng Hạ tầng & Bảo mật Hệ thống",
            "Lead",
            6.5m,
            "Chuyên gia hạ tầng hệ thống, bảo mật xác thực JWT/RBAC, cấu hình Redis, Qdrant và kiểm toán an toàn thông tin.",
            "Admin",
            "ProjectManager",
            8.0m,
            36.0m,
            [
                new("SYS_ARCH", 5, 6.0m),
                new("API_SECURITY", 5, 6.0m),
                new("REDIS_CACHE", 5, 5.0m),
                new("CSHARP_DOTNET", 4, 5.0m),
                new("MYSQL_DB", 4, 5.0m)
            ],
            IsSystemAdmin: true),
        new(
            "hoangteam02@gmail.com",
            "Nguyễn Quốc Khánh (Hoàng Team 02 - Giám đốc DA)",
            "Giám đốc Dự án / Product Owner",
            "Lead",
            7.0m,
            "Phụ trách đặc tả yêu cầu nghiệp vụ (SRS/BRD), quản lý lộ trình sản phẩm, phân bổ nguồn lực và tối ưu giá trị chuyển giao.",
            "Admin",
            "ProjectManager",
            6.0m,
            30.0m,
            [
                new("SCRUM_AGILE", 5, 7.0m),
                new("SYS_ARCH", 4, 5.0m),
                new("CSHARP_DOTNET", 4, 4.0m),
                new("ANGULAR_TS", 3, 3.0m)
            ]),
        new(
            "hoangteam03@gmail.com",
            "Lê Thu Hà (Hoàng Team 03 - Scrum Master)",
            "Chuyên gia Điều phối Scrum Master",
            "Senior",
            5.0m,
            "Điều phối lập kế hoạch Sprint, theo dõi vận tốc đội ngũ (Velocity), gỡ bỏ điểm nghẽn (Blocker) và chuẩn hóa tiêu chí nghiệm thu.",
            "Member",
            "ScrumMaster",
            6.0m,
            32.0m,
            [
                new("SCRUM_AGILE", 5, 5.0m),
                new("PLAYWRIGHT_QA", 4, 3.5m),
                new("ANGULAR_TS", 3, 2.5m)
            ]),
        new(
            "hoangteam04@gmail.com",
            "Phạm Đức Anh (Hoàng Team 04 - Backend Dev)",
            "Kỹ sư Phát triển Backend .NET Cấp cao",
            "Senior",
            5.0m,
            "Chuyên sâu xây dựng RESTful API, MCP Server, tối ưu hóa truy vấn MySQL, Entity Framework Core và luồng dữ liệu thời gian thực SSE/SignalR.",
            "Member",
            "Developer",
            10.0m,
            40.0m,
            [
                new("CSHARP_DOTNET", 5, 5.0m),
                new("EF_CORE", 5, 4.5m),
                new("MYSQL_DB", 5, 4.5m),
                new("REDIS_CACHE", 4, 3.5m),
                new("SIGNALR_SSE", 5, 4.0m),
                new("API_SECURITY", 4, 3.5m)
            ]),
        new(
            "hoangteam05@gmail.com",
            "Vũ Hoàng Nam (Hoàng Team 05 - Frontend Dev)",
            "Kỹ sư Phát triển Frontend Angular Cấp cao",
            "Senior",
            4.5m,
            "Chuyên thiết kế giao diện Angular Signals, tối ưu trải nghiệm người dùng đa màn hình, bảng kéo thả Kanban/Scrum và biểu đồ báo cáo.",
            "Member",
            "Developer",
            10.0m,
            40.0m,
            [
                new("ANGULAR_TS", 5, 4.5m),
                new("UI_TAILWIND", 5, 4.5m),
                new("SIGNALR_SSE", 4, 3.5m)
            ]),
        new(
            "hoangteam06@gmail.com",
            "Đặng Phương Thảo (Hoàng Team 06 - QA Tester)",
            "Kỹ sư Kiểm thử Chất lượng & Tự động hóa (QA)",
            "Senior",
            4.0m,
            "Xây dựng kịch bản kiểm thử tự động Playwright E2E, kiểm thử bảo mật API, nghiệm thu tiêu chí AC và kiểm soát lỗi hồi quy.",
            "Member",
            "QA",
            8.0m,
            40.0m,
            [
                new("PLAYWRIGHT_QA", 5, 4.0m),
                new("API_SECURITY", 4, 3.5m),
                new("MYSQL_DB", 4, 3.0m),
                new("ANGULAR_TS", 3, 2.5m)
            ]),
        new(
            "hoangteam07@gmail.com",
            "Bùi Quang Huy (Hoàng Team 07 - Viewer)",
            "Chuyên viên Phân tích & Nghiệm thu Nghiệp vụ",
            "Middle",
            3.5m,
            "Theo dõi tiến độ dự án, đối chiếu kết quả bàn giao Sprint với tài liệu đặc tả yêu cầu của khách hàng.",
            "Member",
            "Viewer",
            4.0m,
            20.0m,
            [
                new("SCRUM_AGILE", 4, 3.5m),
                new("MYSQL_DB", 3, 2.5m)
            ])
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var loggerFactory = services.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("TestAccountSeeder");

        try
        {
            var identityDb = services.GetRequiredService<IdentityExperienceDbContext>();
            var planningDb = services.GetRequiredService<PlanningDbContext>();
            var deliveryDb = services.GetRequiredService<DeliveryIntelligenceDbContext>();
            var passwordService = services.GetRequiredService<IPasswordService>();

            var now = DateTime.UtcNow;
            var passwordHash = passwordService.Hash(DefaultPassword);

            // 1. Seed SkillCatalog
            var existingSkills = await identityDb.SkillCatalogs.ToListAsync();
            foreach (var seed in SkillCatalogSeeds)
            {
                var existing = existingSkills.FirstOrDefault(s => s.Code.Equals(seed.Code, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = new SkillCatalog
                    {
                        Id = Guid.NewGuid(),
                        Code = seed.Code,
                        Name = seed.Name,
                        Category = seed.Category,
                        IsActive = true
                    };
                    identityDb.SkillCatalogs.Add(existing);
                    existingSkills.Add(existing);
                }
                else
                {
                    existing.Name = seed.Name;
                    existing.Category = seed.Category;
                    existing.IsActive = true;
                }
            }
            await identityDb.SaveChangesAsync();

            // 2. Locate all "Quản lý dự án" / "QLDA" projects
            var targetProjects = await planningDb.Projects
                .Where(p => !p.IsDeleted && (p.Name.Contains("Quản lý dự án") || p.ProjectKey == "QLDA"))
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            if (targetProjects.Count == 0)
            {
                var fallback = await planningDb.Projects.Where(p => !p.IsDeleted).FirstOrDefaultAsync();
                if (fallback != null) targetProjects.Add(fallback);
            }

            if (targetProjects.Count == 0)
            {
                return;
            }

            // 3. Ensure Project & System Roles exist
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

            // Also enrich Project Owner ("Hoàng Hải Hậu") profile & skills
            var ownerEmail = "HOANGHAIHAU989@GMAIL.COM";
            var ownerUser = await identityDb.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == ownerEmail);
            if (ownerUser != null)
            {
                await UpsertUserProfileAndSkillsAsync(
                    identityDb,
                    ownerUser.Id,
                    "Hoàng Hải Hậu",
                    "Giám đốc Dự án & Kiến trúc sư Trưởng",
                    "Principal",
                    8.0m,
                    "Chịu trách nhiệm định hướng kiến trúc tổng thể, bảo mật hệ thống và phê duyệt mục tiêu Sprint.",
                    [
                        new("SYS_ARCH", 5, 8.0m),
                        new("CSHARP_DOTNET", 5, 7.0m),
                        new("ANGULAR_TS", 5, 6.0m),
                        new("SCRUM_AGILE", 5, 7.0m),
                        new("API_SECURITY", 5, 6.0m)
                    ],
                    existingSkills,
                    now);
            }

            // 4. Create/Update hoangteam01..07 accounts, profiles, skills, and project memberships
            var userByEmail = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            if (ownerUser != null)
            {
                userByEmail["hoanghaihau989@gmail.com"] = ownerUser.Id;
            }

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

                userByEmail[spec.Email] = user.Id;

                await UpsertUserProfileAndSkillsAsync(
                    identityDb,
                    user.Id,
                    spec.DisplayName,
                    spec.JobTitle,
                    spec.SeniorityLevel,
                    spec.YearsOfExperience,
                    spec.Bio,
                    spec.Skills,
                    existingSkills,
                    now);

                var primaryInviterId = targetProjects[0].LeadUserId;

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
                            GrantedBy = primaryInviterId,
                            CreatedAt = now
                        });
                    }
                }

                foreach (var targetProject in targetProjects)
                {
                    var orgId = targetProject.OrgId;
                    var projectId = targetProject.Id;
                    var inviterUserId = targetProject.LeadUserId;

                    if (orgMemberRole != null)
                    {
                        var hasOrgRole = await identityDb.UserRoles.AnyAsync(ur =>
                            ur.UserId == user.Id && ur.ScopeType == "Organization" && ur.ScopeId == orgId);
                        if (!hasOrgRole)
                        {
                            identityDb.UserRoles.Add(new UserRole
                            {
                                Id = Guid.NewGuid(),
                                UserId = user.Id,
                                RoleId = orgMemberRole.Id,
                                ScopeType = "Organization",
                                ScopeId = orgId,
                                GrantedBy = inviterUserId,
                                CreatedAt = now
                            });
                        }
                    }

                    try
                    {
                        await planningDb.Database.ExecuteSqlInterpolatedAsync($@"
                            INSERT INTO OrganizationMember (Id, OrgId, UserId, Role, JoinedAt)
                            VALUES ({Guid.NewGuid().ToString()}, {orgId.ToString()}, {user.Id.ToString()}, {spec.OrgRole}, UTC_TIMESTAMP(6))
                            ON DUPLICATE KEY UPDATE Role = VALUES(Role);");
                    }
                    catch
                    {
                        // Ignore if OrganizationMember table not present in test context
                    }

                    var targetRole = allRoles.FirstOrDefault(r =>
                        r.Scope == "Project" && r.Name.Equals(spec.ProjectRoleName, StringComparison.OrdinalIgnoreCase)) ?? devRole;

                    var existingProjRoles = await identityDb.UserRoles
                        .Where(ur => ur.UserId == user.Id && ur.ScopeType == "Project" && ur.ScopeId == projectId)
                        .ToListAsync();

                    if (existingProjRoles.Count == 0)
                    {
                        identityDb.UserRoles.Add(new UserRole
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            RoleId = targetRole.Id,
                            ScopeType = "Project",
                            ScopeId = projectId,
                            GrantedBy = inviterUserId,
                            CreatedAt = now
                        });
                    }
                    else if (existingProjRoles[0].RoleId != targetRole.Id)
                    {
                        existingProjRoles[0].RoleId = targetRole.Id;
                    }

                    var hasInviteNotif = await identityDb.Notifications.AnyAsync(n =>
                        n.UserId == user.Id && n.ProjectId == projectId && n.Type == "ProjectInvitation");

                    if (!hasInviteNotif)
                    {
                        identityDb.Notifications.Add(new Notification
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            Type = "ProjectInvitation",
                            Title = $"Lời mời tham gia dự án {targetProject.Name}",
                            Content = $"Bạn đã được mời tham gia dự án \"{targetProject.Name}\" với vai trò {targetRole.Name}. Nhấn Chấp nhận trên thông báo để xác nhận.",
                            EntityType = "Project",
                            EntityId = projectId,
                            ProjectId = projectId,
                            ActorId = inviterUserId,
                            IsRead = false,
                            CreatedAt = now
                        });
                    }
                }

                await identityDb.SaveChangesAsync();
            }

            // 5. Seed Historical Sprints, Active Sprint, Member Capacities & Product Backlog for each target project
            foreach (var project in targetProjects)
            {
                await SeedProjectSprintsAndBacklogAsync(planningDb, deliveryDb, project, userByEmail, now);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Không thể khởi tạo dữ liệu thử nghiệm cho dự án và tài khoản hoangteam01..07");
        }
    }

    private static async Task UpsertUserProfileAndSkillsAsync(
        IdentityExperienceDbContext identityDb,
        Guid userId,
        string displayName,
        string jobTitle,
        string seniorityLevel,
        decimal yearsOfExperience,
        string bio,
        UserSkillSeed[] skillSeeds,
        List<SkillCatalog> catalog,
        DateTime now)
    {
        var profile = await identityDb.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = displayName,
                JobTitle = jobTitle,
                SeniorityLevel = seniorityLevel,
                Timezone = "Asia/Ho_Chi_Minh",
                YearsOfExperience = yearsOfExperience,
                Bio = bio,
                CreatedAt = now,
                UpdatedAt = now
            };
            identityDb.UserProfiles.Add(profile);
        }
        else
        {
            profile.DisplayName = displayName;
            profile.JobTitle = jobTitle;
            profile.SeniorityLevel = seniorityLevel;
            profile.YearsOfExperience = yearsOfExperience;
            profile.Bio = bio;
            profile.UpdatedAt = now;
        }

        var existingSkills = await identityDb.UserSkills
            .Where(us => us.UserId == userId)
            .ToListAsync();

        foreach (var seed in skillSeeds)
        {
            var skillObj = catalog.FirstOrDefault(s => s.Code.Equals(seed.Code, StringComparison.OrdinalIgnoreCase));
            if (skillObj == null) continue;

            var existingUserSkill = existingSkills.FirstOrDefault(us => us.SkillId == skillObj.Id);
            if (existingUserSkill == null)
            {
                identityDb.UserSkills.Add(new UserSkill
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SkillId = skillObj.Id,
                    ProficiencyLevel = seed.ProficiencyLevel,
                    YearsOfExperience = seed.YearsOfExperience,
                    IsSelfDeclared = true,
                    UpdatedAt = now
                });
            }
            else
            {
                existingUserSkill.ProficiencyLevel = seed.ProficiencyLevel;
                existingUserSkill.YearsOfExperience = seed.YearsOfExperience;
                existingUserSkill.UpdatedAt = now;
            }
        }

        await identityDb.SaveChangesAsync();
    }

    private static async Task SeedProjectSprintsAndBacklogAsync(
        PlanningDbContext planningDb,
        DeliveryIntelligenceDbContext deliveryDb,
        Project project,
        Dictionary<string, Guid> userByEmail,
        DateTime now)
    {
        var issueTypes = await planningDb.IssueTypes.AsNoTracking()
            .Where(t => t.ProjectId == project.Id)
            .ToListAsync();
        var statuses = await planningDb.WorkflowStatuses.AsNoTracking()
            .Where(s => s.ProjectId == project.Id)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync();
        var priorities = await planningDb.Priorities.AsNoTracking()
            .Where(p => p.ProjectId == project.Id || p.ProjectId == null)
            .ToListAsync();

        if (issueTypes.Count == 0 || statuses.Count == 0)
        {
            return;
        }

        var epicType = issueTypes.FirstOrDefault(t => t.Name.Equals("Epic", StringComparison.OrdinalIgnoreCase)) ?? issueTypes[0];
        var storyType = issueTypes.FirstOrDefault(t => t.Name.Equals("Story", StringComparison.OrdinalIgnoreCase)) ?? issueTypes[0];
        var taskType = issueTypes.FirstOrDefault(t => t.Name.Equals("Task", StringComparison.OrdinalIgnoreCase)) ?? storyType;
        var bugType = issueTypes.FirstOrDefault(t => t.Name.Equals("Bug", StringComparison.OrdinalIgnoreCase)) ?? taskType;

        var todoStatus = statuses.FirstOrDefault(s => s.IsInitial || s.Category == "todo" || s.Name.Contains("To Do", StringComparison.OrdinalIgnoreCase)) ?? statuses[0];
        var inProgressStatus = statuses.FirstOrDefault(s => s.Category == "in_progress" || s.Name.Contains("Progress", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Đang", StringComparison.OrdinalIgnoreCase)) ?? todoStatus;
        var doneStatus = statuses.LastOrDefault(s => s.Category == "done" || s.Name.Contains("Done", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Hoàn", StringComparison.OrdinalIgnoreCase)) ?? statuses[^1];

        Guid? ResolvePriority(string name) =>
            priorities.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Id
            ?? priorities.FirstOrDefault()?.Id;

        var adminId = userByEmail.GetValueOrDefault("hoangteam01@gmail.com", project.LeadUserId);
        var directorId = userByEmail.GetValueOrDefault("hoangteam02@gmail.com", project.LeadUserId);
        var smId = userByEmail.GetValueOrDefault("hoangteam03@gmail.com", project.LeadUserId);
        var backendDevId = userByEmail.GetValueOrDefault("hoangteam04@gmail.com", project.LeadUserId);
        var frontendDevId = userByEmail.GetValueOrDefault("hoangteam05@gmail.com", project.LeadUserId);
        var qaId = userByEmail.GetValueOrDefault("hoangteam06@gmail.com", project.LeadUserId);

        // Load existing sprints & issues in project
        var existingSprints = await planningDb.Sprints
            .Where(s => s.ProjectId == project.Id)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync();

        var existingIssues = await deliveryDb.Issues
            .Where(i => i.ProjectId == project.Id && !i.IsDeleted)
            .ToListAsync();

        var maxNumInDb = await deliveryDb.Issues
            .Where(i => i.ProjectId == project.Id)
            .Select(i => (int?)i.IssueNumber)
            .MaxAsync() ?? 0;

        var nextIssueNum = Math.Max(maxNumInDb, existingIssues.Count > 0 ? existingIssues.Max(i => i.IssueNumber) : 0) + 1;
        var nextRank = existingIssues.Count > 0 ? existingIssues.Max(i => i.RankOrder) + 100m : 100m;

        // A. Ensure 4 Epics exist
        Guid EnsureEpic(string title, string description)
        {
            var existing = existingIssues.FirstOrDefault(i =>
                i.IssueTypeId == epicType.Id && i.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing.Id;

            var epic = new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                IssueNumber = nextIssueNum++,
                ReporterId = project.LeadUserId,
                AssigneeId = directorId,
                StatusId = inProgressStatus.Id,
                IssueTypeId = epicType.Id,
                PriorityId = ResolvePriority("High"),
                Title = title,
                Description = description,
                StoryPoints = 0,
                RankOrder = nextRank,
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now
            };
            nextRank += 100m;
            deliveryDb.Issues.Add(epic);
            existingIssues.Add(epic);
            return epic.Id;
        }

        var epicAuthId = EnsureEpic(
            "Nền tảng Xác thực & Phân quyền Scrum RBAC",
            "Xây dựng cơ chế xác thực JWT, phân quyền Scrum từ trên xuống (Org Owner -> Project Director -> Scrum Master -> Developer/QA -> Viewer) và bảo vệ an toàn API.");
        var epicWorkspaceId = EnsureEpic(
            "Không gian Quản lý Dự án, Bảng Công việc & Spec Hub",
            "Quản lý quy trình động (Workflow Template), bảng kéo thả công việc, kho tài liệu đặc tả Spec Hub và thông báo thời gian thực.");
        var epicAiId = EnsureEpic(
            "Hệ thống AI Agent, MCP Server & Phân tách Sprint",
            "Tích hợp mô hình AI đa nhà cung cấp, bộ công cụ MCP Server và luồng phân tách Sprint thời gian thực qua Server-Sent Events (SSE).");
        var epicAnalyticsId = EnsureEpic(
            "Báo cáo Vận tốc, Biểu đồ Burndown & Kiểm thử Tự động",
            "Thống kê vận tốc đội ngũ (Velocity), biểu đồ Burndown/Burnup, cảnh báo Blocker và tự động hóa kiểm thử Playwright E2E.");

        await deliveryDb.SaveChangesAsync();

        // B. Ensure Completed Sprint 1 & Completed Sprint 2 + Active Sprint
        async Task<Sprint> EnsureHistoricalSprintAsync(
            string sprintName,
            string goal,
            int daysAgoStart,
            int daysAgoEnd,
            int orderIndex)
        {
            var sp = existingSprints.FirstOrDefault(s => s.Name.Equals(sprintName, StringComparison.OrdinalIgnoreCase));
            if (sp == null)
            {
                sp = new Sprint
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = sprintName,
                    Goal = goal,
                    Status = "Completed",
                    StartDate = DateOnly.FromDateTime(now.AddDays(-daysAgoStart)),
                    EndDate = DateOnly.FromDateTime(now.AddDays(-daysAgoEnd)),
                    ActualStartAt = now.AddDays(-daysAgoStart),
                    ActualCompleteAt = now.AddDays(-daysAgoEnd),
                    OrderIndex = orderIndex,
                    CreatedAt = now.AddDays(-daysAgoStart),
                    UpdatedAt = now.AddDays(-daysAgoEnd)
                };
                planningDb.Sprints.Add(sp);
                existingSprints.Add(sp);
                await planningDb.SaveChangesAsync();
            }
            else if (string.IsNullOrWhiteSpace(sp.Goal))
            {
                sp.Goal = goal;
                await planningDb.SaveChangesAsync();
            }

            await EnsureSprintCapacitiesAsync(planningDb, sp.Id, userByEmail);
            return sp;
        }

        var histSprint1 = await EnsureHistoricalSprintAsync(
            "QLDA Sprint 1 - Nền tảng Kiến trúc & Phân quyền RBAC",
            "Hoàn thiện cơ chế xác thực JWT, phân quyền Scrum từ trên xuống và cấu trúc cơ sở dữ liệu nền tảng.",
            28,
            15,
            1);

        var histSprint2 = await EnsureHistoricalSprintAsync(
            "QLDA Sprint 2 - Quản lý Không gian Dự án, Bảng Công việc & Spec Hub",
            "Xây dựng hoàn chỉnh bảng kéo thả công việc theo quy trình động, kho tài liệu đặc tả Spec Hub và đồng bộ thông báo thời gian thực.",
            14,
            1,
            2);

        // Ensure Active Sprint exists and has member capacities
        var activeSprint = existingSprints.FirstOrDefault(s => s.Status == "Active" || s.Status == "active");
        if (activeSprint == null)
        {
            var plannedCandidate = existingSprints.FirstOrDefault(s => s.Name == "QLDA Sprint 1" && s.Status == "Planned");
            if (plannedCandidate != null)
            {
                plannedCandidate.Name = "QLDA Sprint 3 - Tích hợp Thời gian thực & Tối ưu Hiệu năng";
                plannedCandidate.Goal = "Triển khai đồng bộ bình luận thời gian thực qua SignalR, nhắc tên @mention thành viên và tối ưu hóa tốc độ tải Backlog.";
                plannedCandidate.Status = "Active";
                plannedCandidate.StartDate = DateOnly.FromDateTime(now.AddDays(-3));
                plannedCandidate.EndDate = DateOnly.FromDateTime(now.AddDays(11));
                plannedCandidate.ActualStartAt = now.AddDays(-3);
                activeSprint = plannedCandidate;
            }
            else
            {
                activeSprint = new Sprint
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = "QLDA Sprint 3 - Tích hợp Thời gian thực & Tối ưu Hiệu năng",
                    Goal = "Triển khai đồng bộ bình luận thời gian thực qua SignalR, nhắc tên @mention thành viên và tối ưu hóa tốc độ tải Backlog.",
                    Status = "Active",
                    StartDate = DateOnly.FromDateTime(now.AddDays(-3)),
                    EndDate = DateOnly.FromDateTime(now.AddDays(11)),
                    ActualStartAt = now.AddDays(-3),
                    OrderIndex = 3,
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now
                };
                planningDb.Sprints.Add(activeSprint);
                existingSprints.Add(activeSprint);
            }
            await planningDb.SaveChangesAsync();
        }
        else if (string.IsNullOrWhiteSpace(activeSprint.Goal))
        {
            activeSprint.Goal = "Triển khai đồng bộ bình luận thời gian thực qua SignalR, nhắc tên @mention thành viên và tối ưu hóa tốc độ tải Backlog.";
            await planningDb.SaveChangesAsync();
        }

        await EnsureSprintCapacitiesAsync(planningDb, activeSprint.Id, userByEmail);

        // Helper to seed an issue with acceptance criteria if not already present by title
        void EnsureSeededIssue(
            Guid? sprintId,
            Guid epicId,
            Guid issueTypeId,
            Guid statusId,
            string priorityName,
            Guid assigneeId,
            string title,
            string description,
            decimal storyPoints,
            decimal estimatedHours,
            decimal remainingHours,
            DateTime? resolvedAt,
            string[] acceptanceCriteria)
        {
            if (existingIssues.Any(i => i.Title.Equals(title, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                IssueNumber = nextIssueNum++,
                SprintId = sprintId,
                EpicId = epicId,
                AssigneeId = assigneeId,
                ReporterId = project.LeadUserId,
                StatusId = statusId,
                IssueTypeId = issueTypeId,
                PriorityId = ResolvePriority(priorityName),
                Title = title,
                Description = description,
                StoryPoints = storyPoints,
                EstimatedHours = estimatedHours,
                RemainingHours = remainingHours,
                OriginalEstimateMinutes = (int)Math.Round(estimatedHours * 60m),
                TimeSpentMinutes = resolvedAt.HasValue ? (int)Math.Round(estimatedHours * 60m) : (int)Math.Round((estimatedHours - remainingHours) * 60m),
                RankOrder = nextRank,
                StartedAt = resolvedAt?.AddDays(-3) ?? (statusId == inProgressStatus.Id ? now.AddDays(-2) : null),
                ResolvedAt = resolvedAt,
                CreatedAt = resolvedAt?.AddDays(-5) ?? now.AddDays(-4),
                UpdatedAt = resolvedAt ?? now
            };
            nextRank += 100m;
            deliveryDb.Issues.Add(issue);
            existingIssues.Add(issue);

            for (var idx = 0; idx < acceptanceCriteria.Length; idx++)
            {
                deliveryDb.AcceptanceCriteria.Add(new AcceptanceCriteria
                {
                    Id = Guid.NewGuid(),
                    IssueId = issue.Id,
                    Content = acceptanceCriteria[idx],
                    IsMet = resolvedAt.HasValue,
                    OrderIndex = idx,
                    Source = "Manual",
                    CreatedAt = issue.CreatedAt
                });
            }
        }

        // C. Seed Completed Tasks in Historical Sprint 1 (Total: 21 SP)
        EnsureSeededIssue(
            histSprint1.Id,
            epicAuthId,
            storyType.Id,
            doneStatus.Id,
            "High",
            backendDevId,
            "Xây dựng dịch vụ xác thực JWT, làm mới Refresh Token và kiểm soát phiên đăng nhập",
            "Cài đặt luồng đăng nhập an toàn với thuật toán băm mật khẩu Argon2/BCrypt, cấp phát Access Token & Refresh Token và thu hồi phiên khi đăng xuất.",
            5m,
            16m,
            0m,
            now.AddDays(-20),
            [
                "API /api/auth/login trả về JWT hợp lệ kèm danh sách quyền hạn người dùng",
                "Mật khẩu được băm một chiều an toàn, tuyệt đối không lưu plaintext",
                "Tự động thu hồi Refresh Token cũ khi cấp phát phiên mới"
            ]);

        EnsureSeededIssue(
            histSprint1.Id,
            epicAuthId,
            storyType.Id,
            doneStatus.Id,
            "Highest",
            adminId,
            "Thiết kế hệ thống phân quyền Scrum RBAC đa tầng và Middleware bảo vệ API",
            "Áp dụng mô hình phân quyền từ trên xuống (Org Owner -> Project Director -> Scrum Master -> Developer/QA -> Viewer) và chặn truy cập vượt cấp.",
            8m,
            24m,
            0m,
            now.AddDays(-18),
            [
                "Vai trò Giám đốc dự án (LeadUserId) bất biến, không bị hạ cấp hoặc xóa",
                "Developer, QA và Viewer bị chặn truy cập cấu hình dự án qua projectManagerGuard",
                "Kiểm tra phạm vi dự án (ProjectScopeGuard) trên toàn bộ API"
            ]);

        EnsureSeededIssue(
            histSprint1.Id,
            epicAuthId,
            storyType.Id,
            doneStatus.Id,
            "High",
            frontendDevId,
            "Phát triển giao diện quản lý thành viên, phân quyền dự án và thanh điều hướng thích ứng",
            "Xây dựng tab Thành viên & Phân quyền trong Cài đặt dự án và thanh điều hướng co dãn mượt trên màn hình thu nhỏ.",
            5m,
            16m,
            0m,
            now.AddDays(-16),
            [
                "Hiển thị rõ huy hiệu vai trò Scrum bằng icon Google Material Symbols Outlined",
                "Không bị tràn lề hoặc che khuất khi chia đôi màn hình (split-screen)",
                "Cập nhật quyền thành viên tức thì không cần tải lại trang"
            ]);

        EnsureSeededIssue(
            histSprint1.Id,
            epicAuthId,
            taskType.Id,
            doneStatus.Id,
            "Medium",
            qaId,
            "Viết bộ kiểm thử tự động xác thực phân quyền chống leo thang đặc quyền (Anti-Privilege Escalation)",
            "Kiểm thử tự động xUnit và Playwright đảm bảo cấp dưới không thể thao tác hoặc xem cấu hình của cấp trên.",
            3m,
            10m,
            0m,
            now.AddDays(-15),
            [
                "100% kịch bản kiểm thử leo thang đặc quyền trả về 403 Forbidden",
                "Kiểm thử đăng nhập và phân quyền trên 7 vai trò thành viên dự án",
                "Báo cáo kiểm thử tự động đạt 0 lỗi"
            ]);

        // D. Seed Completed Tasks in Historical Sprint 2 (Total: 26 SP)
        EnsureSeededIssue(
            histSprint2.Id,
            epicWorkspaceId,
            storyType.Id,
            doneStatus.Id,
            "High",
            backendDevId,
            "Phát triển API quản lý quy trình động (Workflow Template) và đồng bộ cột Board",
            "Cho phép dự án áp dụng mẫu quy trình Scrum, Kanban hoặc quy trình tùy chỉnh và tự động ánh xạ các bước trạng thái lên Board.",
            8m,
            24m,
            0m,
            now.AddDays(-8),
            [
                "Áp dụng mẫu quy trình không gây lỗi xung đột khóa duy nhất (409 Conflict)",
                "Tự động cập nhật tên bảng và bộ lọc theo đúng mẫu quy trình đang áp dụng",
                "Bảo toàn trạng thái của các công việc hiện có khi chuyển đổi quy trình"
            ]);

        EnsureSeededIssue(
            histSprint2.Id,
            epicWorkspaceId,
            storyType.Id,
            doneStatus.Id,
            "High",
            frontendDevId,
            "Xây dựng giao diện Bảng công việc (Board) kéo thả mượt mà và bộ lọc đa tiêu chí",
            "Thiết kế bảng kéo thả thẻ việc với hiệu ứng chuyển động mượt, hỗ trợ lọc theo người thực hiện, loại công việc và trạng thái.",
            8m,
            24m,
            0m,
            now.AddDays(-5),
            [
                "Kéo thả thẻ việc giữa các cột cập nhật trạng thái tức thì",
                "Hiển thị đúng ảnh đại diện, Story Points và nhãn Epic trên từng thẻ việc",
                "Tương thích hoàn toàn với chế độ thu nhỏ màn hình"
            ]);

        EnsureSeededIssue(
            histSprint2.Id,
            epicWorkspaceId,
            storyType.Id,
            doneStatus.Id,
            "High",
            backendDevId,
            "Tích hợp kho lưu trữ tài liệu đặc tả Spec Hub hỗ trợ xem và trích xuất nội dung .docx, .pdf, .md",
            "Xây dựng phân hệ Spec Hub quản lý cây thư mục tài liệu đặc tả dự án và bóc tách văn bản phục vụ truy vấn ngữ cảnh cho AI Agent.",
            5m,
            16m,
            0m,
            now.AddDays(-3),
            [
                "Hỗ trợ tải lên và đọc nội dung các định dạng .docx, .pdf, .xlsx, .md, .txt",
                "Phân đoạn văn bản thông minh và hỗ trợ tìm kiếm theo từ khóa/vector",
                "Đảm bảo cách ly dữ liệu tài liệu theo từng ProjectId"
            ]);

        EnsureSeededIssue(
            histSprint2.Id,
            epicWorkspaceId,
            taskType.Id,
            doneStatus.Id,
            "Medium",
            qaId,
            "Kiểm thử E2E luồng chuyển trạng thái công việc và áp dụng mẫu quy trình trên trình duyệt",
            "Thực thi bộ kịch bản kiểm thử Playwright xác minh tính đồng nhất giữa cấu hình Workflow và hiển thị trên màn hình Board/Backlog.",
            3m,
            10m,
            0m,
            now.AddDays(-2),
            [
                "Xác nhận đổi tên menu và tiêu đề bảng theo đúng tên quy trình đã chọn",
                "Kiểm tra kéo thả công việc tuân thủ đúng luật chuyển trạng thái (Transition Rules)",
                "Chụp ảnh minh chứng tự động trên trình duyệt Chrome"
            ]);

        EnsureSeededIssue(
            histSprint2.Id,
            epicWorkspaceId,
            taskType.Id,
            doneStatus.Id,
            "Medium",
            smId,
            "Chuẩn hóa quy trình đánh giá độ sẵn sàng công việc (Definition of Ready) trước Sprint",
            "Thiết lập bộ tiêu chuẩn đánh giá công việc đủ điều kiện đưa vào Sprint (có mô tả, Story Points, người phụ trách và tiêu chí nghiệm thu).",
            2m,
            6m,
            0m,
            now.AddDays(-1),
            [
                "Hiển thị nhãn Ready rõ ràng trên danh sách Backlog cho các công việc đạt chuẩn",
                "Thống kê tổng Story Points của Sprint trước khi bấm Bắt đầu Sprint",
                "Hướng dẫn đội ngũ ước lượng điểm Story Points theo dãy Fibonacci"
            ]);

        // E. Seed Active Sprint Tasks (so AI sees realistic current workload per member)
        EnsureSeededIssue(
            activeSprint.Id,
            epicWorkspaceId,
            storyType.Id,
            doneStatus.Id,
            "High",
            backendDevId,
            "Triển khai SignalR Hub đồng bộ bình luận công việc thời gian thực không dùng toast gây nhiễu",
            "Cập nhật bình luận mới trực tiếp vào khung thảo luận của công việc đang mở mà không bật popup thông báo che màn hình.",
            5m,
            16m,
            0m,
            now.AddDays(-1),
            [
                "Bình luận mới hiển thị tức thì trên mọi trình duyệt đang mở công việc",
                "Không hiển thị toast popup nổi khi có bình luận thông thường",
                "Hỗ trợ nhắc tên @mention thành viên kèm avatar chuẩn xác"
            ]);

        EnsureSeededIssue(
            activeSprint.Id,
            epicAiId,
            storyType.Id,
            inProgressStatus.Id,
            "High",
            backendDevId,
            "Hoàn thiện bộ công cụ MCP Server truy xuất Spec Hub, Backlog, Velocity và Năng lực đội ngũ",
            "Xây dựng các MCP Tool chuẩn hóa trong thư mục McpServers kết nối với Controller qua luồng SSE.",
            5m,
            16m,
            6m,
            null,
            [
                "Cung cấp đầy đủ 7 công cụ MCP với JSON Schema tường minh",
                "Kiểm tra nghiêm ngặt ProjectScopeGuard trước mọi truy vấn cơ sở dữ liệu",
                "Loại bỏ 100% mật khẩu và thông tin nhạy cảm khỏi dữ liệu trả về cho AI"
            ]);

        EnsureSeededIssue(
            activeSprint.Id,
            epicAiId,
            storyType.Id,
            inProgressStatus.Id,
            "High",
            frontendDevId,
            "Tối ưu hóa giao diện Drawer chi tiết công việc và Modal AI Phân tách trên màn hình Backlog",
            "Thiết kế giao diện tương tác trực quan hiển thị tiến trình suy luận 5 bước của Agent, bảng công việc đề xuất và Drawer chỉnh sửa chi tiết.",
            5m,
            16m,
            6m,
            null,
            [
                "Hiển thị trực tiếp tiến trình SSE, phần trăm hoàn thành và các chip MCP Tool",
                "Cho phép chỉnh sửa trực tiếp Sprint Goal, từng công việc, việc con và tiêu chí nghiệm thu",
                "Hỗ trợ yêu cầu AI làm lại riêng 1 công việc mà không ảnh hưởng các công việc khác"
            ]);

        EnsureSeededIssue(
            activeSprint.Id,
            epicAnalyticsId,
            taskType.Id,
            todoStatus.Id,
            "Medium",
            qaId,
            "Xây dựng bộ dữ liệu kiểm thử hiệu năng và kiểm toán bảo mật Zero-Secret-Leakage",
            "Thiết lập quy trình quét tự động toàn bộ mã nguồn và tệp cấu hình đảm bảo không rò rỉ khóa API, mật khẩu hay địa chỉ IP.",
            3m,
            8m,
            8m,
            null,
            [
                "Tệp appsettings.json chỉ chứa giá trị mẫu placeholder an toàn",
                "Tự động chặn commit nếu phát hiện chuỗi khóa nhạy cảm trong git diff",
                "Vượt qua toàn bộ hơn 400 bài kiểm thử đơn vị và đối kháng (Adversarial Tests)"
            ]);

        // F. Seed Rich Product Backlog Items (SprintId = null) for AI Agent to evaluate & plan
        EnsureSeededIssue(
            null,
            epicAnalyticsId,
            storyType.Id,
            todoStatus.Id,
            "High",
            backendDevId,
            "Xây dựng bảng điều khiển thống kê Vận tốc (Velocity Chart) và biểu đồ Burndown theo thời gian thực",
            "Phát triển API tổng hợp dữ liệu điểm Story Points kế hoạch so với thực tế hoàn thành qua từng Sprint và hiển thị biểu đồ trực quan cho Scrum Master.",
            5m,
            16m,
            16m,
            null,
            [
                "Biểu đồ Velocity so sánh điểm cam kết (Committed SP) và điểm hoàn thành (Completed SP) của 5 Sprint gần nhất",
                "Biểu đồ Burndown theo dõi số giờ và điểm còn lại theo từng ngày trong Sprint đang chạy",
                "Cho phép lọc báo cáo theo từng thành viên hoặc theo nhãn Epic"
            ]);

        EnsureSeededIssue(
            null,
            epicWorkspaceId,
            storyType.Id,
            todoStatus.Id,
            "High",
            frontendDevId,
            "Phát triển bộ lọc thông minh và tìm kiếm nhanh (Ctrl+K) trên Backlog, Board và Spec Hub",
            "Cho phép người dùng tìm kiếm tức thì theo mã công việc (QLDA-xx), tiêu đề, người phụ trách hoặc nội dung tài liệu đặc tả.",
            5m,
            16m,
            16m,
            null,
            [
                "Mở hộp thoại tìm kiếm nhanh bằng tổ hợp phím Ctrl+K từ mọi màn hình",
                "Phân nhóm kết quả rõ ràng theo Công việc, Sprint và Tài liệu Spec Hub",
                "Thời gian phản hồi kết quả tìm kiếm dưới 200ms"
            ]);

        EnsureSeededIssue(
            null,
            epicAnalyticsId,
            storyType.Id,
            todoStatus.Id,
            "Medium",
            backendDevId,
            "Tích hợp xuất báo cáo tổng kết Sprint và danh sách Backlog ra tệp Excel (.xlsx) chuẩn doanh nghiệp",
            "Cung cấp tính năng xuất dữ liệu công việc, tiến độ thành viên và chỉ số Sprint ra bảng tính Excel định dạng chuyên nghiệp phục vụ báo cáo tuần.",
            3m,
            10m,
            10m,
            null,
            [
                "Tệp Excel xuất ra có đầy đủ các cột: Mã, Tiêu đề, Loại, Epic, Ưu tiên, SP, Giờ ước tính, Trạng thái, Người thực hiện",
                "Tự động tổng hợp dòng thống kê tổng Story Points và tỷ lệ hoàn thành ở cuối bảng",
                "Hỗ trợ tiếng Việt Unicode chuẩn không lỗi phông chữ"
            ]);

        EnsureSeededIssue(
            null,
            epicAnalyticsId,
            storyType.Id,
            todoStatus.Id,
            "High",
            smId,
            "Cảnh báo tự động các công việc bị nghẽn (Blocker) quá 24 giờ trong bảng theo dõi Daily Scrum",
            "Làm nổi bật các công việc đang gắn cờ IsBlocked và gửi thông báo trong ứng dụng cho Scrum Master và Giám đốc dự án để kịp thời tháo gỡ.",
            5m,
            14m,
            14m,
            null,
            [
                "Hiển thị cảnh báo trực quan kèm nguyên nhân nghẽn (BlockReason) và thời gian bị nghẽn",
                "Gửi thông báo trong ứng dụng cho Scrum Master khi công việc bị nghẽn quá 24 giờ",
                "Cho phép gỡ cờ Blocker và ghi nhận lịch sử giải quyết ngay trên Drawer công việc"
            ]);

        EnsureSeededIssue(
            null,
            epicWorkspaceId,
            taskType.Id,
            todoStatus.Id,
            "Medium",
            backendDevId,
            "Tối ưu hóa chỉ mục (Index) cơ sở dữ liệu MySQL cho truy vấn lịch sử hoạt động và danh sách Backlog",
            "Rà soát kế hoạch thực thi truy vấn (EXPLAIN) và bổ sung composite index cho bảng Issue, IssueActivityLog và Notification.",
            3m,
            8m,
            8m,
            null,
            [
                "Giảm 60% thời gian truy vấn danh sách Backlog khi dự án có trên 1.000 công việc",
                "Loại bỏ hoàn toàn hiện tượng N+1 query khi tải danh sách công việc kèm tiêu chí nghiệm thu",
                "Đảm bảo không làm ảnh hưởng đến các ràng buộc toàn vẹn dữ liệu hiện có"
            ]);

        EnsureSeededIssue(
            null,
            epicAnalyticsId,
            storyType.Id,
            todoStatus.Id,
            "Medium",
            frontendDevId,
            "Xây dựng giao diện cấu hình năng lực thành viên (Capacity Planning) theo từng chu kỳ Sprint",
            "Cho phép Scrum Master điều chỉnh số giờ khả dụng (AvailableHours) và điểm năng lực (CapacityPoints) của từng thành viên khi có lịch nghỉ phép.",
            5m,
            16m,
            16m,
            null,
            [
                "Hiển thị bảng danh sách thành viên kèm kỹ năng chuyên môn và tải công việc hiện tại",
                "Cảnh báo tự động bằng màu sắc khi tổng giờ công việc gán cho thành viên vượt quá giờ khả dụng",
                "Cung cấp dữ liệu trực tiếp cho công cụ MCP team_get_dev_capacities của AI Agent"
            ]);

        EnsureSeededIssue(
            null,
            epicWorkspaceId,
            bugType.Id,
            todoStatus.Id,
            "High",
            frontendDevId,
            "Khắc phục hiện tượng lệch múi giờ khi hiển thị hạn chót (Due Date) và thời gian hoàn thành Sprint",
            "Đồng bộ chuẩn múi giờ Asia/Ho_Chi_Minh khi chuyển đổi giữa dữ liệu UTC từ máy chủ và bộ chọn ngày trên giao diện Angular.",
            2m,
            6m,
            6m,
            null,
            [
                "Hạn chót công việc không bị lùi 1 ngày khi lưu ở múi giờ GMT+7",
                "Thời gian hoàn thành Sprint hiển thị chính xác theo giờ địa phương của người dùng",
                "Bổ sung kiểm thử đơn vị cho bộ chuyển đổi ngày tháng"
            ]);

        EnsureSeededIssue(
            null,
            epicAuthId,
            taskType.Id,
            todoStatus.Id,
            "High",
            qaId,
            "Kiểm thử bảo mật toàn diện OWASP API Top 10 và kiểm toán chống rò rỉ dữ liệu nhạy cảm trên MCP",
            "Thực hiện kiểm thử xâm nhập (Penetration Testing) trên các endpoint AI Agent và MCP Server nhằm đảm bảo an toàn tuyệt đối dữ liệu dự án.",
            3m,
            10m,
            10m,
            null,
            [
                "Chặn 100% các mẫu tấn công Prompt Injection và truy cập chéo dự án (Cross-Tenant)",
                "Xác minh API hồ sơ thành viên không bao giờ trả về mật khẩu, mã băm hay khóa bảo mật",
                "Đạt chứng nhận 100% bài kiểm thử bảo mật tự động trong bộ ProjectMgmt.Tests"
            ]);

        project.IssueCounter = Math.Max(project.IssueCounter, nextIssueNum - 1);
        await planningDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();
    }

    private static async Task EnsureSprintCapacitiesAsync(
        PlanningDbContext planningDb,
        Guid sprintId,
        Dictionary<string, Guid> userByEmail)
    {
        var existingCaps = await planningDb.SprintMemberCapacities
            .Where(c => c.SprintId == sprintId)
            .ToListAsync();

        foreach (var spec in Accounts)
        {
            if (!userByEmail.TryGetValue(spec.Email, out var userId)) continue;

            var cap = existingCaps.FirstOrDefault(c => c.UserId == userId);
            if (cap == null)
            {
                planningDb.SprintMemberCapacities.Add(new SprintMemberCapacity
                {
                    Id = Guid.NewGuid(),
                    SprintId = sprintId,
                    UserId = userId,
                    CapacityPoints = spec.CapacityPoints,
                    AvailableHours = spec.AvailableHours,
                    Note = $"{spec.JobTitle} ({spec.SeniorityLevel})"
                });
            }
            else
            {
                cap.CapacityPoints = spec.CapacityPoints;
                cap.AvailableHours = spec.AvailableHours;
                cap.Note = $"{spec.JobTitle} ({spec.SeniorityLevel})";
            }
        }

        await planningDb.SaveChangesAsync();
    }
}
