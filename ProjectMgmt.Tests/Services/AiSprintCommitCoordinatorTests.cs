namespace ProjectMgmt.Tests.Services;

using System.Text.Json;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

public class AiSprintCommitCoordinatorTests
{
    private sealed class TestPlanningDbContext(DbContextOptions<PlanningDbContext> options) : PlanningDbContext(options)
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

    private sealed class FakeUserLookup : IUserLookupService
    {
        public Guid MemberId { get; } = Guid.NewGuid();
        public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync() =>
            Task.FromResult<IReadOnlyList<UserDisplayInfo>>(new[]
            {
                new UserDisplayInfo { UserId = MemberId, DisplayName = "Hoàng Team 04 (Backend Dev)", Email = "hoangteam04@gmail.com" }
            });

        public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds) => GetAllActiveDisplayInfoAsync();
        public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId) =>
            Task.FromResult<UserDisplayInfo?>(new UserDisplayInfo { UserId = userId, DisplayName = "Hoàng Team 04 (Backend Dev)", Email = "hoangteam04@gmail.com" });
    }

    [Fact]
    public async Task ApplySprintProposalAsync_CreatesNextPlannedSprint_Epics_Tasks_Subtasks_And_Criteria()
    {
        var planningOpts = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var deliveryOpts = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var planningDb = new TestPlanningDbContext(planningOpts);
        await using var deliveryDb = new TestDeliveryDbContext(deliveryOpts);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Quản lý dự án",
            ProjectKey = "QLDA",
            LeadUserId = actorId,
            IssueCounter = 5
        });

        planningDb.IssueTypes.AddRange(
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Epic", IsSubtask = false, OrderIndex = 0 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Story", IsSubtask = false, OrderIndex = 1 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 2 }
        );

        planningDb.WorkflowStatuses.Add(new WorkflowStatus
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "To Do",
            Category = "ToDo",
            IsInitial = true,
            OrderIndex = 0
        });

        planningDb.Priorities.Add(new Priority
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "High",
            Level = 2
        });

        await planningDb.SaveChangesAsync();

        var staging = new MemoryStateStagingService();
        var userLookup = new FakeUserLookup();
        var coordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            staging,
            userLookup,
            NullLogger<AiSprintCommitCoordinator>.Instance);

        var sessionId = "sprint_ai_test_commit";
        var request = new AiApplySprintProposalRequest
        {
            SessionId = sessionId,
            ProjectId = projectId,
            SprintGoal = "Hoàn thiện hệ thống AI Agent 16 bước và kiểm thử Playwright E2E",
            Tasks =
            [
                new ProposedTaskDto
                {
                    Id = "task_01",
                    EpicTitle = "AI Agent Architecture",
                    Title = "Xây dựng SSE Streaming & MCP Server",
                    Description = "Triển khai truyền sự kiện thời gian thực.",
                    Type = "Story",
                    Priority = "High",
                    StoryPoints = 5,
                    EstimatedHours = 16,
                    AssigneeName = "Hoàng Team 04 (Backend Dev)",
                    AcceptanceCriteria = ["Hỗ trợ SSE không buffer", "Bảo vệ ProjectScopeGuard"],
                    Subtasks =
                    [
                        new ProposedSubTaskDto { Id = "sub_01", Title = "Cài đặt Controller SSE", Role = "Backend", EstimatedHours = 8 },
                        new ProposedSubTaskDto { Id = "sub_02", Title = "Viết Unit Tests", Role = "QA", EstimatedHours = 8 }
                    ]
                }
            ]
        };

        var scope = new ProjectScopeContext(orgId, projectId, actorId);
        var response = await coordinator.ApplySprintProposalAsync(request, scope);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.Message}");
        Assert.Equal("QLDA Sprint 1", response.SprintName);
        Assert.Equal("Hoàn thiện hệ thống AI Agent 16 bước và kiểm thử Playwright E2E", response.SprintGoal);
        Assert.Equal(1, response.CreatedTaskCount);
        Assert.Equal(2, response.CreatedSubtaskCount);

        var createdSprint = await planningDb.Sprints.SingleAsync(s => s.ProjectId == projectId);
        Assert.Equal("Planned", createdSprint.Status);
        Assert.Equal(request.SprintGoal, createdSprint.Goal);

        var allIssues = await deliveryDb.Issues.Where(i => i.ProjectId == projectId).ToListAsync();
        // 1 Epic + 1 Story + 2 Subtasks = 4 issues
        Assert.Equal(4, allIssues.Count);

        var criteria = await deliveryDb.AcceptanceCriteria.ToListAsync();
        Assert.Equal(2, criteria.Count);
    }

    private sealed class FakeSkillCatalogReader(IReadOnlyList<SkillCatalogItem> items) : ISkillCatalogReader
    {
        public Task<IReadOnlyList<SkillCatalogItem>> GetSkillsAsync()
            => Task.FromResult(items);
    }

    [Fact]
    public async Task ApplySprintProposalAsync_Persists_SpecLinks_Skills_SubtaskRoles_AiMetadata_And_NormalizedAssignee()
    {
        var planningOpts = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var deliveryOpts = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var planningDb = new TestPlanningDbContext(planningOpts);
        await using var deliveryDb = new TestDeliveryDbContext(deliveryOpts);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var existingSprintId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var secondDocId = Guid.NewGuid();
        var backendSkillId = Guid.NewGuid();
        var signalRSkillId = Guid.NewGuid();

        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Quản lý dự án",
            ProjectKey = "QLDA",
            LeadUserId = actorId,
            IssueCounter = 10
        });

        planningDb.Sprints.Add(new Sprint
        {
            Id = existingSprintId,
            ProjectId = projectId,
            Name = "QLDA Sprint 3",
            Goal = "Giữ nguyên mục tiêu hiện tại của Sprint",
            Status = "Planned",
            OrderIndex = 1
        });

        planningDb.ProjectDocuments.AddRange(
            new ProjectDocument
            {
                Id = docId,
                ProjectId = projectId,
                Name = "mcp-server-guide.md",
                Title = "MCP Server Guide",
                RelativePath = "docs/mcp-server-guide.md",
                IsDirectory = false
            },
            new ProjectDocument
            {
                Id = secondDocId,
                ProjectId = projectId,
                Name = "01-kien-truc-tong-quan-va-use-case.md",
                Title = "Kiến trúc tổng quan",
                RelativePath = "docs/01-kien-truc-tong-quan-va-use-case.md",
                IsDirectory = false
            });

        planningDb.IssueTypes.AddRange(
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Story", IsSubtask = false, OrderIndex = 0 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 1 }
        );

        planningDb.WorkflowStatuses.Add(new WorkflowStatus
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "To Do",
            Category = "ToDo",
            IsInitial = true,
            OrderIndex = 0
        });

        planningDb.Priorities.Add(new Priority
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Highest",
            Level = 1
        });

        await planningDb.SaveChangesAsync();

        var staging = new MemoryStateStagingService();
        var userLookup = new FakeUserLookup();
        var skillReader = new FakeSkillCatalogReader(
        [
            new SkillCatalogItem(backendSkillId, "ASP.NET Core", "Backend", true),
            new SkillCatalogItem(signalRSkillId, "SignalR", "Realtime", true)
        ]);

        var coordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            staging,
            userLookup,
            NullLogger<AiSprintCommitCoordinator>.Instance,
            skillReader);

        var request = new AiApplySprintProposalRequest
        {
            SessionId = "session_enrich_test",
            ProjectId = projectId,
            TargetSprintId = existingSprintId,
            SprintGoal = string.Empty,
            Tasks =
            [
                new ProposedTaskDto
                {
                    Id = "task_enrich_01",
                    Title = "Thiết kế Real-time Notification",
                    Description = "Đẩy thông báo thời gian thực cho thành viên.",
                    Type = "Story",
                    Priority = "Urgent",
                    StoryPoints = 5,
                    EstimatedHours = 12,
                    AssigneeName = "Hoàng Team 04",
                    DocumentId = docId,
                    DocumentCitations =
                    [
                        "mcp-server-guide.md - Mục 2",
                        "01-kien-truc-tong-quan-va-use-case.md - Mục 1.2"
                    ],
                    Tags = ["realtime", "#backend"],
                    SuggestedSkills = ["ASP.NET Core", "SignalR", "Architecture"],
                    AiRationale = "Ưu tiên xử lý cảnh báo thời gian thực theo Spec.",
                    AcceptanceCriteria = ["Client nhận sự kiện dưới 500ms"],
                    Subtasks =
                    [
                        new ProposedSubTaskDto
                        {
                            Id = "sub_enrich_01",
                            Title = "Cấu hình SignalR Hub",
                            Role = "Backend",
                            StoryPoints = 2,
                            EstimatedHours = 6,
                            AssigneeName = "Hoàng Team 04 (Backend Dev)",
                            AcceptanceCriteria = ["Hub xác thực JWT chuẩn"]
                        }
                    ]
                }
            ]
        };

        var scope = new ProjectScopeContext(orgId, projectId, actorId);
        var response = await coordinator.ApplySprintProposalAsync(request, scope);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.Message}");
        var sprint = await planningDb.Sprints.SingleAsync(s => s.Id == existingSprintId);
        Assert.Equal("Giữ nguyên mục tiêu hiện tại của Sprint", sprint.Goal);

        var parentIssue = await deliveryDb.Issues.SingleAsync(i => i.ParentId == null);
        Assert.Equal(userLookup.MemberId, parentIssue.AssigneeId);

        var subIssue = await deliveryDb.Issues.SingleAsync(i => i.ParentId == parentIssue.Id);
        Assert.Equal(userLookup.MemberId, subIssue.AssigneeId);
        Assert.Equal(2, subIssue.StoryPoints);

        var specLinks = await planningDb.SpecStoryLinks.ToListAsync();
        Assert.Equal(4, specLinks.Count); // 2 docs linked to parent + 2 inherited docs linked to subtask

        var requiredSkills = await deliveryDb.IssueRequiredSkills.Where(rs => rs.IssueId == parentIssue.Id).ToListAsync();
        Assert.Equal(2, requiredSkills.Count);
        Assert.All(requiredSkills, rs => Assert.Equal("AI", rs.Source));

        var labels = await deliveryDb.Labels.Select(l => l.Name).ToListAsync();
        Assert.Contains("realtime", labels);
        Assert.Contains("backend", labels, StringComparer.OrdinalIgnoreCase);

        var parentLog = await deliveryDb.ActivityLogs.SingleAsync(l => l.IssueId == parentIssue.Id && l.Source == "AI");
        using var parentJson = JsonDocument.Parse(parentLog.Detail!);
        Assert.Equal("Ưu tiên xử lý cảnh báo thời gian thực theo Spec.", parentJson.RootElement.GetProperty("aiRationale").GetString());
        Assert.Equal(3, parentJson.RootElement.GetProperty("suggestedSkills").GetArrayLength());

        var subLog = await deliveryDb.ActivityLogs.SingleAsync(l => l.IssueId == subIssue.Id && l.Source == "AI");
        using var subJson = JsonDocument.Parse(subLog.Detail!);
        Assert.Equal("Backend", subJson.RootElement.GetProperty("subtaskRole").GetString());
    }
}

