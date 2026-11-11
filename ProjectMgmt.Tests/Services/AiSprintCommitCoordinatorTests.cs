namespace ProjectMgmt.Tests.Services;

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

        Assert.True(response.Success);
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
}

