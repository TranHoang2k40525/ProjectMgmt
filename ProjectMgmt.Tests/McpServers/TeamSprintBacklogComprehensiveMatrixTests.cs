namespace ProjectMgmt.Tests.McpServers;

using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.McpServers;
using Xunit;

public class TeamSprintBacklogComprehensiveMatrixTests
{
    private sealed class TestPlanningDbContext(DbContextOptions<PlanningDbContext> options) : PlanningDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class TestIdentityDbContext(DbContextOptions<IdentityExperienceDbContext> options) : IdentityExperienceDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class TestDeliveryDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options) : DeliveryIntelligenceDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }

    public static IEnumerable<object[]> GetTeamProfilePrivacyMatrix()
    {
        var roles = new[] { "ProjectManager", "ScrumMaster", "Developer", "QA", "Viewer" };
        var seniorities = new[] { "Intern", "Junior", "Middle", "Senior", "Lead", "Principal" };
        var categories = new[]
        {
            ("CSHARP_DOTNET", "C# / .NET 9 Web API", "Backend"),
            ("ANGULAR_TS", "Angular 19 & TypeScript", "Frontend"),
            ("MYSQL_DB", "MySQL & SQL Optimization", "Database"),
            ("PLAYWRIGHT_QA", "Playwright E2E Testing", "QA"),
            ("API_SECURITY", "OWASP API Security & RBAC", "Security")
        };

        int index = 0;
        foreach (var role in roles)
        {
            foreach (var seniority in seniorities)
            {
                foreach (var (code, skillName, category) in categories)
                {
                    index++;
                    sbyte proficiency = (sbyte)((index % 5) + 1);
                    decimal yearsExp = 1.0m + (index % 10);
                    decimal availableHours = 20m + ((index % 5) * 8m);
                    int completedTasks = index % 6;
                    int activeTasks = index % 3;

                    yield return
                    [
                        role,
                        seniority,
                        code,
                        skillName,
                        category,
                        proficiency,
                        yearsExp,
                        availableHours,
                        completedTasks,
                        activeTasks
                    ];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetTeamProfilePrivacyMatrix))]
    public async Task TeamTools_GetDevCapacities_ReturnsSafeProfileSkillsAndHistory_WithoutAnySensitiveFields(
        string roleName,
        string seniority,
        string skillCode,
        string skillName,
        string skillCategory,
        sbyte proficiency,
        decimal yearsExp,
        decimal availableHours,
        int completedTasksCount,
        int activeTasksCount)
    {
        var planningOpts = new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var identityOpts = new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var deliveryOpts = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var planningDb = new TestPlanningDbContext(planningOpts);
        await using var identityDb = new TestIdentityDbContext(identityOpts);
        await using var deliveryDb = new TestDeliveryDbContext(deliveryOpts);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activeSprintId = Guid.NewGuid();
        var pastSprintId = Guid.NewGuid();
        var doneStatusId = Guid.NewGuid();
        var inProgressStatusId = Guid.NewGuid();

        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Quản lý dự án",
            ProjectKey = "QLDA",
            LeadUserId = userId
        });

        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = doneStatusId, ProjectId = projectId, Name = "Done", Category = "done", IsDone = true },
            new WorkflowStatus { Id = inProgressStatusId, ProjectId = projectId, Name = "In Progress", Category = "in_progress", IsActiveWork = true }
        );

        planningDb.Sprints.AddRange(
            new Sprint { Id = pastSprintId, ProjectId = projectId, Name = "Sprint 1", Status = "Completed" },
            new Sprint { Id = activeSprintId, ProjectId = projectId, Name = "Sprint 2", Status = "Active" }
        );

        planningDb.SprintMemberCapacities.Add(new SprintMemberCapacity
        {
            Id = Guid.NewGuid(),
            SprintId = activeSprintId,
            UserId = userId,
            AvailableHours = availableHours,
            CapacityPoints = 10m,
            Note = $"Chuyên môn {skillCategory}"
        });

        await planningDb.SaveChangesAsync();

        var sensitivePasswordHash = "$2a$12$SENSITIVE_HASH_DO_NOT_LEAK_999999";
        var sensitivePhone = "0987654321";
        var sensitiveEmail = "private_secret_member@internal.corp";
        var fullName = $"Kỹ sư {seniority} ({roleName})";

        identityDb.Users.Add(new User
        {
            Id = userId,
            Email = sensitiveEmail,
            NormalizedEmail = sensitiveEmail.ToUpperInvariant(),
            PasswordHash = sensitivePasswordHash,
            SecurityStamp = Guid.NewGuid(),
            IsActive = true,
            IsEmailVerified = true
        });

        identityDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = fullName,
            JobTitle = $"Chuyên gia {skillCategory}",
            SeniorityLevel = seniority,
            YearsOfExperience = yearsExp,
            PhoneNumber = sensitivePhone,
            Bio = $"Kinh nghiệm thực chiến {skillName}"
        });

        var roleId = Guid.NewGuid();
        identityDb.Roles.Add(new Role { Id = roleId, Name = roleName, Scope = "Project" });
        identityDb.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            ScopeType = "Project",
            ScopeId = projectId
        });

        var skillId = Guid.NewGuid();
        identityDb.SkillCatalogs.Add(new SkillCatalog
        {
            Id = skillId,
            Code = skillCode,
            Name = skillName,
            Category = skillCategory,
            IsActive = true
        });

        identityDb.UserSkills.Add(new UserSkill
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SkillId = skillId,
            ProficiencyLevel = proficiency,
            YearsOfExperience = yearsExp,
            IsSelfDeclared = true
        });

        await identityDb.SaveChangesAsync();

        for (int i = 0; i < completedTasksCount; i++)
        {
            deliveryDb.Issues.Add(new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SprintId = pastSprintId,
                IssueNumber = i + 1,
                AssigneeId = userId,
                ReporterId = userId,
                StatusId = doneStatusId,
                IssueTypeId = Guid.NewGuid(),
                Title = $"Hoàn thành hạng mục {skillCode} #{i + 1}",
                StoryPoints = 3m,
                EstimatedHours = 8m,
                RemainingHours = 0m,
                ResolvedAt = DateTime.UtcNow.AddDays(-5)
            });
        }

        for (int i = 0; i < activeTasksCount; i++)
        {
            deliveryDb.Issues.Add(new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SprintId = activeSprintId,
                IssueNumber = 100 + i,
                AssigneeId = userId,
                ReporterId = userId,
                StatusId = inProgressStatusId,
                IssueTypeId = Guid.NewGuid(),
                Title = $"Đang thực hiện {skillCode} #{i + 1}",
                StoryPoints = 2m,
                EstimatedHours = 6m,
                RemainingHours = 4m,
                ResolvedAt = null
            });
        }

        await deliveryDb.SaveChangesAsync();

        var teamTools = new TeamTools(planningDb, identityDb, deliveryDb);
        var scope = new ProjectScopeContext(orgId, projectId, userId);

        var json = await teamTools.GetDevCapacitiesAsync(projectId, scope);

        // 1. Privacy assertions: NEVER leak password hash, phone, email, or security stamp
        Assert.DoesNotContain(sensitivePasswordHash, json);
        Assert.DoesNotContain("PasswordHash", json);
        Assert.DoesNotContain("SecurityStamp", json);
        Assert.DoesNotContain(sensitivePhone, json);
        Assert.DoesNotContain(sensitiveEmail, json);

        // 2. Profile, skill & historical performance assertions
        using var doc = JsonDocument.Parse(json);
        var capacities = doc.RootElement.GetProperty("Capacities");
        Assert.Equal(1, capacities.GetArrayLength());

        var member = capacities[0];
        Assert.Equal(fullName, member.GetProperty("DisplayName").GetString());
        Assert.Equal(fullName, member.GetProperty("FullName").GetString());
        Assert.Equal(seniority, member.GetProperty("SeniorityLevel").GetString());
        Assert.Equal(yearsExp, member.GetProperty("YearsOfExperience").GetDecimal());
        Assert.Equal(availableHours, member.GetProperty("AvailableHours").GetDecimal());

        var expectedRemaining = Math.Max(0m, availableHours - (activeTasksCount * 4m));
        Assert.Equal(expectedRemaining, member.GetProperty("RemainingAvailableHours").GetDecimal());

        var skillProfiles = member.GetProperty("SkillProfiles");
        Assert.Equal(1, skillProfiles.GetArrayLength());
        Assert.Equal(skillCode, skillProfiles[0].GetProperty("SkillCode").GetString());
        Assert.Equal(skillName, skillProfiles[0].GetProperty("SkillName").GetString());
        Assert.Equal(proficiency, skillProfiles[0].GetProperty("ProficiencyLevel").GetInt32());

        var perf = member.GetProperty("HistoricalPerformance");
        Assert.Equal(completedTasksCount, perf.GetProperty("CompletedTasksCount").GetInt32());
        Assert.Equal(completedTasksCount * 3m, perf.GetProperty("CompletedStoryPoints").GetDecimal());
        Assert.Equal(activeTasksCount, perf.GetProperty("ActiveSprintInProgressCount").GetInt32());
        Assert.Equal(activeTasksCount * 2m, perf.GetProperty("ActiveSprintCommittedPoints").GetDecimal());
    }

    public static IEnumerable<object[]> GetSprintVelocityPermutations()
    {
        var completedSprintCounts = new[] { 0, 1, 2, 3, 4, 5 };
        var pointsPerTaskOptions = new[] { 1m, 2m, 3m, 5m, 8m, 13m };
        var hasActiveSprintOptions = new[] { false, true };
        var statusCasings = new[] { "Completed", "completed" };

        foreach (var sprintCount in completedSprintCounts)
        {
            foreach (var pts in pointsPerTaskOptions)
            {
                foreach (var hasActive in hasActiveSprintOptions)
                {
                    foreach (var statusCase in statusCasings)
                    {
                        yield return [sprintCount, pts, hasActive, statusCase];
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSprintVelocityPermutations))]
    public async Task SprintTools_GetHistoryAndVelocity_ComputesAccurateVelocityAndSprintHistory(
        int completedSprintCount,
        decimal pointsPerTask,
        bool hasActiveSprint,
        string statusCase)
    {
        var planningOpts = new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var deliveryOpts = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var planningDb = new TestPlanningDbContext(planningOpts);
        await using var deliveryDb = new TestDeliveryDbContext(deliveryOpts);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var doneStatusId = Guid.NewGuid();
        var todoStatusId = Guid.NewGuid();

        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Quản lý dự án",
            ProjectKey = "QLDA",
            LeadUserId = userId
        });

        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = doneStatusId, ProjectId = projectId, Name = "Done", Category = "done", IsDone = true },
            new WorkflowStatus { Id = todoStatusId, ProjectId = projectId, Name = "To Do", Category = "todo", IsInitial = true }
        );

        int issueNum = 1;
        for (int s = 0; s < completedSprintCount; s++)
        {
            var spId = Guid.NewGuid();
            planningDb.Sprints.Add(new Sprint
            {
                Id = spId,
                ProjectId = projectId,
                Name = $"QLDA Sprint {s + 1}",
                Goal = $"Mục tiêu Sprint {s + 1}",
                Status = statusCase,
                OrderIndex = s + 1,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30 + s * 5)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-25 + s * 5))
            });

            // 2 completed tasks per sprint
            for (int t = 0; t < 2; t++)
            {
                deliveryDb.Issues.Add(new Issue
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    SprintId = spId,
                    IssueNumber = issueNum++,
                    ReporterId = userId,
                    AssigneeId = userId,
                    StatusId = doneStatusId,
                    IssueTypeId = Guid.NewGuid(),
                    Title = $"Task hoàn thành {issueNum}",
                    StoryPoints = pointsPerTask,
                    EstimatedHours = pointsPerTask * 3m,
                    ResolvedAt = DateTime.UtcNow.AddDays(-10)
                });
            }
        }

        if (hasActiveSprint)
        {
            var activeId = Guid.NewGuid();
            planningDb.Sprints.Add(new Sprint
            {
                Id = activeId,
                ProjectId = projectId,
                Name = "QLDA Active Sprint",
                Goal = "Active Goal",
                Status = statusCase == "Completed" ? "Active" : "active",
                OrderIndex = 99
            });

            deliveryDb.Issues.Add(new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SprintId = activeId,
                IssueNumber = issueNum++,
                ReporterId = userId,
                StatusId = todoStatusId,
                IssueTypeId = Guid.NewGuid(),
                Title = "Active Task",
                StoryPoints = pointsPerTask
            });
        }

        await planningDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();

        var sprintTools = new SprintTools(planningDb, deliveryDb);
        var scope = new ProjectScopeContext(orgId, projectId, userId);

        var json = await sprintTools.GetHistoryAndVelocityAsync(projectId, scope);
        using var doc = JsonDocument.Parse(json);

        var recentSprints = doc.RootElement.GetProperty("RecentSprints");
        Assert.Equal(Math.Min(5, completedSprintCount), recentSprints.GetArrayLength());

        if (completedSprintCount > 0)
        {
            var expectedVelocity = (double)(pointsPerTask * 2m);
            Assert.Equal(expectedVelocity, doc.RootElement.GetProperty("AverageVelocity").GetDouble());
        }

        var activeProp = doc.RootElement.GetProperty("ActiveSprint");
        if (hasActiveSprint)
        {
            Assert.Equal(JsonValueKind.Object, activeProp.ValueKind);
            Assert.Equal(pointsPerTask, activeProp.GetProperty("CommittedPoints").GetDecimal());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, activeProp.ValueKind);
        }
    }

    public static IEnumerable<object[]> GetBacklogPermutations()
    {
        var keys = new[] { "QLDA", "ERP", "CRM", "CORE", "AI" };
        var backlogCounts = new[] { 0, 1, 3, 8, 15 };
        var criteriaCounts = new[] { 0, 1, 3, 5 };

        foreach (var key in keys)
        {
            foreach (var count in backlogCounts)
            {
                foreach (var acCount in criteriaCounts)
                {
                    yield return [key, count, acCount];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetBacklogPermutations))]
    public async Task BacklogTools_GetStories_ReturnsOnlyUnassignedNonDeletedBacklogStoriesWithOrderedCriteria(
        string projectKey,
        int backlogCount,
        int criteriaPerStory)
    {
        var planningOpts = new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var deliveryOpts = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var planningDb = new TestPlanningDbContext(planningOpts);
        await using var deliveryDb = new TestDeliveryDbContext(deliveryOpts);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = $"Dự án {projectKey}",
            ProjectKey = projectKey,
            LeadUserId = userId
        });
        await planningDb.SaveChangesAsync();

        for (int i = 0; i < backlogCount; i++)
        {
            var issueId = Guid.NewGuid();
            deliveryDb.Issues.Add(new Issue
            {
                Id = issueId,
                ProjectId = projectId,
                SprintId = null,
                IssueNumber = i + 1,
                ReporterId = userId,
                StatusId = Guid.NewGuid(),
                IssueTypeId = Guid.NewGuid(),
                Title = $"Backlog Story #{i + 1}",
                StoryPoints = 5m,
                RankOrder = (i + 1) * 10m,
                IsDeleted = false
            });

            for (int c = 0; c < criteriaPerStory; c++)
            {
                deliveryDb.AcceptanceCriteria.Add(new AcceptanceCriteria
                {
                    Id = Guid.NewGuid(),
                    IssueId = issueId,
                    Content = $"Tiêu chí #{c + 1}",
                    OrderIndex = c,
                    Source = "Manual"
                });
            }
        }

        // Add 1 deleted issue and 1 sprint-assigned issue that must be excluded
        deliveryDb.Issues.AddRange(
            new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SprintId = null,
                IssueNumber = 998,
                ReporterId = userId,
                StatusId = Guid.NewGuid(),
                IssueTypeId = Guid.NewGuid(),
                Title = "Deleted Story",
                IsDeleted = true
            },
            new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SprintId = Guid.NewGuid(),
                IssueNumber = 999,
                ReporterId = userId,
                StatusId = Guid.NewGuid(),
                IssueTypeId = Guid.NewGuid(),
                Title = "Already In Sprint Story",
                IsDeleted = false
            }
        );

        await deliveryDb.SaveChangesAsync();

        var backlogTools = new BacklogTools(deliveryDb, planningDb);
        var scope = new ProjectScopeContext(orgId, projectId, userId);

        var json = await backlogTools.GetStoriesAsync(projectId, scope);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(backlogCount, doc.RootElement.GetArrayLength());

        if (backlogCount > 0)
        {
            var first = doc.RootElement[0];
            Assert.Equal($"{projectKey}-1", first.GetProperty("IssueKey").GetString());
            Assert.Equal(criteriaPerStory, first.GetProperty("AcceptanceCriteria").GetArrayLength());
        }
    }
}

