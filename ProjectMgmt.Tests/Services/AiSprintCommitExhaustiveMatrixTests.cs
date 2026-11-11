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

public class AiSprintCommitExhaustiveMatrixTests
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

    private sealed class MockStagingService : IStateStagingService
    {
        public Task SaveProposalAsync<T>(string sessionId, T proposal, TimeSpan ttl, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<T?> GetProposalAsync<T>(string sessionId, CancellationToken ct = default)
            => Task.FromResult<T?>(default);

        public Task<bool> RemoveProposalAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class MockUserLookupService : IUserLookupService
    {
        private readonly List<UserDisplayInfo> _users;

        public MockUserLookupService(List<UserDisplayInfo> users)
        {
            _users = users;
        }

        public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync()
            => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(_users);

        public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds)
        {
            var list = _users.Where(u => userIds.Contains(u.UserId)).ToList();
            return Task.FromResult<IReadOnlyList<UserDisplayInfo>>(list);
        }

        public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId)
            => Task.FromResult(_users.FirstOrDefault(u => u.UserId == userId));
    }

    public static IEnumerable<object[]> GetSprintCommitPermutations()
    {
        var taskCounts = new[] { 1, 2, 3, 5, 8 };
        var subtaskCounts = new[] { 0, 1, 2, 3 };
        var sprintResolutionModes = new[] { "CreateNewSprint", "UseExistingPlannedSprint", "UseExplicitTargetSprint", "IgnoreCompletedSprintCreateNew" };
        var epicModes = new[] { "NoEpic", "MatchExistingEpicByTitle", "AutoCreateNewEpicByTitle" };

        foreach (var tc in taskCounts)
        {
            foreach (var sc in subtaskCounts)
            {
                foreach (var sMode in sprintResolutionModes)
                {
                    foreach (var eMode in epicModes)
                    {
                        yield return new object[] { tc, sc, sMode, eMode };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSprintCommitPermutations))]
    public async Task ApplySprintProposalAsync_ShouldCommitTasksSubtasksEpicsAndSprintGoalCorrectly(
        int taskCount,
        int subtaskCount,
        string sprintResolutionMode,
        string epicMode)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(
            new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Commit_Plan_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(
            new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Commit_Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var devUserId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            OrgId = orgId,
            ProjectKey = "QLDA",
            Name = "Quản lý dự án",
            IssueCounter = 10,
            IsDeleted = false,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        planningDb.Projects.Add(project);

        var epicType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Epic", IsSubtask = false, OrderIndex = 0 };
        var storyType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Story", IsSubtask = false, OrderIndex = 1 };
        var taskType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Task", IsSubtask = false, OrderIndex = 2 };
        var bugType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Bug", IsSubtask = false, OrderIndex = 3 };
        var subType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 4 };
        planningDb.IssueTypes.AddRange(epicType, storyType, taskType, bugType, subType);

        var todoStatus = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "To Do", Category = "todo", IsInitial = true, OrderIndex = 0 };
        var inProgStatus = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "In Progress", Category = "in_progress", IsInitial = false, OrderIndex = 1 };
        planningDb.WorkflowStatuses.AddRange(todoStatus, inProgStatus);

        planningDb.Priorities.AddRange(
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Highest", Level = 1, ColorHex = "#EF4444" },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "High", Level = 2, ColorHex = "#F97316" },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Medium", Level = 3, ColorHex = "#3B82F6" },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Low", Level = 4, ColorHex = "#10B981" });

        Guid? requestedSprintId = null;
        if (sprintResolutionMode == "UseExistingPlannedSprint")
        {
            planningDb.Sprints.Add(new Sprint
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = "QLDA Sprint Planned",
                Goal = "Old Goal",
                Status = "Planned",
                OrderIndex = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (sprintResolutionMode == "UseExplicitTargetSprint")
        {
            var explicitSprintId = Guid.NewGuid();
            requestedSprintId = explicitSprintId;
            planningDb.Sprints.Add(new Sprint
            {
                Id = explicitSprintId,
                ProjectId = projectId,
                Name = "QLDA Explicit Sprint",
                Goal = "Old Explicit Goal",
                Status = "Active",
                OrderIndex = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (sprintResolutionMode == "IgnoreCompletedSprintCreateNew")
        {
            var completedSprintId = Guid.NewGuid();
            requestedSprintId = completedSprintId;
            planningDb.Sprints.Add(new Sprint
            {
                Id = completedSprintId,
                ProjectId = projectId,
                Name = "QLDA Completed Sprint",
                Goal = "Completed Goal",
                Status = "Completed",
                OrderIndex = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await planningDb.SaveChangesAsync();

        if (epicMode == "MatchExistingEpicByTitle")
        {
            deliveryDb.Issues.Add(new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                IssueNumber = 1,
                IssueTypeId = epicType.Id,
                StatusId = todoStatus.Id,
                ReporterId = actorId,
                Title = "Epic AI Core",
                IsAiGenerated = false,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await deliveryDb.SaveChangesAsync();
        }

        var userLookup = new MockUserLookupService(new List<UserDisplayInfo>
        {
            new()
            {
                UserId = devUserId,
                DisplayName = "Kỹ sư AI 01",
                Email = "hoangteam01@gmail.com",
                AvatarUrl = null
            }
        });

        var coordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            new MockStagingService(),
            userLookup,
            NullLogger<AiSprintCommitCoordinator>.Instance);

        var scope = new ProjectScopeContext(orgId, projectId, actorId);

        var types = new[] { "Story", "Task", "Bug" };
        var priorities = new[] { "Urgent", "High", "Medium", "Low" };

        var request = new AiApplySprintProposalRequest
        {
            SessionId = $"sess_{suffix}",
            TargetSprintId = requestedSprintId,
            SprintGoal = "Hoàn thiện nền tảng AI Agent Quản lý Dự án chuẩn Scrum",
            Tasks = Enumerable.Range(1, taskCount).Select(idx => new ProposedTaskDto
            {
                Id = $"task_{idx:D2}",
                Title = $"Công việc AI đề xuất số {idx}",
                Description = $"Chi tiết kỹ thuật cho công việc {idx}",
                Type = types[idx % types.Length],
                Priority = priorities[idx % priorities.Length],
                StoryPoints = (idx % 5) + 1,
                EstimatedHours = ((idx % 4) + 1) * 4,
                AssigneeName = idx % 2 == 0 ? "Kỹ sư AI 01" : null,
                AssigneeUserId = idx % 2 == 1 ? devUserId : null,
                EpicTitle = epicMode switch
                {
                    "MatchExistingEpicByTitle" => "Epic AI Core",
                    "AutoCreateNewEpicByTitle" => "Epic Mới Tự Động",
                    _ => null
                },
                AcceptanceCriteria = new List<string>
                {
                    $"Tiêu chí nghiệm thu A cho task {idx}",
                    $"Tiêu chí nghiệm thu B cho task {idx}"
                },
                Subtasks = Enumerable.Range(1, subtaskCount).Select(sIdx => new ProposedSubTaskDto
                {
                    Id = $"sub_{idx}_{sIdx}",
                    Title = $"Subtask {idx}.{sIdx}",
                    Role = sIdx % 2 == 0 ? "QA" : "Backend",
                    EstimatedHours = 2,
                    AssigneeUserId = devUserId
                }).ToList()
            }).ToList()
        };

        var response = await coordinator.ApplySprintProposalAsync(request, scope);

        Assert.True(response.Success);
        Assert.Equal(taskCount, response.CreatedTaskCount);
        Assert.Equal(taskCount * subtaskCount, response.CreatedSubtaskCount);
        Assert.Equal("Hoàn thiện nền tảng AI Agent Quản lý Dự án chuẩn Scrum", response.SprintGoal);

        var allIssues = await deliveryDb.Issues.Where(i => i.ProjectId == projectId).ToListAsync();
        Assert.All(allIssues.Where(i => i.IsAiGenerated), i =>
        {
            Assert.True(i.AiGenerationLogId.HasValue);
            Assert.NotEqual(Guid.Empty, i.AiGenerationLogId!.Value);
        });

        var criteriaCount = await deliveryDb.AcceptanceCriteria.CountAsync();
        Assert.Equal(taskCount * 2, criteriaCount);
    }

    public static IEnumerable<object[]> GetSprintCommitValidationAndEdgeCases()
    {
        var scenarios = new[]
        {
            "EmptySprintGoal",
            "WhitespaceSprintGoal",
            "EmptyTaskList",
            "ProjectNotFound",
            "ArchivedProject",
            "DeletedProject",
            "CrossOrgUnauthorized",
            "MissingProjectWorkflowConfig",
            "NegativeStoryPointsAndHoursClamping",
            "LongAcceptanceCriteriaTruncation"
        };

        for (var iteration = 1; iteration <= 6; iteration++)
        {
            foreach (var scenario in scenarios)
            {
                yield return new object[] { scenario, iteration };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSprintCommitValidationAndEdgeCases))]
    public async Task ApplySprintProposalAsync_ShouldEnforceGuardrailsAndValidationRules(
        string scenario,
        int iteration)
    {
        var suffix = $"{Guid.NewGuid():N}_{iteration}";
        await using var planningDb = new TestPlanningDbContext(
            new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Val_Plan_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(
            new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Val_Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        if (scenario != "ProjectNotFound")
        {
            planningDb.Projects.Add(new Project
            {
                Id = projectId,
                OrgId = orgId,
                ProjectKey = "QLDA",
                Name = "Dự án Kiểm thử",
                IsArchived = scenario == "ArchivedProject",
                IsDeleted = scenario == "DeletedProject",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            if (scenario != "MissingProjectWorkflowConfig")
            {
                planningDb.IssueTypes.Add(new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Task", IsSubtask = false, OrderIndex = 1 });
                planningDb.WorkflowStatuses.Add(new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "To Do", Category = "todo", IsInitial = true, OrderIndex = 0 });
            }

            await planningDb.SaveChangesAsync();
        }

        var coordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            new MockStagingService(),
            new MockUserLookupService(new List<UserDisplayInfo>()),
            NullLogger<AiSprintCommitCoordinator>.Instance);

        var scope = new ProjectScopeContext(
            scenario == "CrossOrgUnauthorized" ? Guid.NewGuid() : orgId,
            projectId,
            actorId);

        var request = new AiApplySprintProposalRequest
        {
            SessionId = $"sess_{suffix}",
            SprintGoal = scenario switch
            {
                "EmptySprintGoal" => "",
                "WhitespaceSprintGoal" => "   ",
                _ => "Mục tiêu hợp lệ"
            },
            Tasks = scenario == "EmptyTaskList"
                ? new List<ProposedTaskDto>()
                : new List<ProposedTaskDto>
                {
                    new()
                    {
                        Id = "task_01",
                        Title = "Task kiểm thử biên",
                        StoryPoints = scenario == "NegativeStoryPointsAndHoursClamping" ? -10 : 5,
                        EstimatedHours = scenario == "NegativeStoryPointsAndHoursClamping" ? -25 : 8,
                        AcceptanceCriteria = new List<string>
                        {
                            scenario == "LongAcceptanceCriteriaTruncation" ? new string('A', 2500) : "Tiêu chí chuẩn"
                        }
                    }
                }
        };

        if (scenario == "CrossOrgUnauthorized")
        {
            await Assert.ThrowsAsync<ProjectScopeException>(() => coordinator.ApplySprintProposalAsync(request, scope));
            return;
        }

        var res = await coordinator.ApplySprintProposalAsync(request, scope);

        switch (scenario)
        {
            case "EmptySprintGoal":
            case "WhitespaceSprintGoal":
                Assert.False(res.Success);
                Assert.Equal("INVALID_SPRINT_GOAL", res.ErrorCode);
                break;

            case "EmptyTaskList":
                Assert.False(res.Success);
                Assert.Equal("EMPTY_TASK_LIST", res.ErrorCode);
                break;

            case "ProjectNotFound":
            case "ArchivedProject":
            case "DeletedProject":
                Assert.False(res.Success);
                Assert.Equal("PROJECT_NOT_FOUND", res.ErrorCode);
                break;

            case "CrossOrgUnauthorized":
                Assert.False(res.Success);
                break;

            case "MissingProjectWorkflowConfig":
                Assert.False(res.Success);
                Assert.Equal("PROJECT_CONFIG_MISSING", res.ErrorCode);
                break;

            case "NegativeStoryPointsAndHoursClamping":
                Assert.True(res.Success);
                var issue = await deliveryDb.Issues.FirstAsync(i => i.ProjectId == projectId);
                Assert.Equal(0m, issue.StoryPoints);
                Assert.Equal(0m, issue.EstimatedHours);
                break;

            case "LongAcceptanceCriteriaTruncation":
                Assert.True(res.Success);
                var ac = await deliveryDb.AcceptanceCriteria.FirstAsync();
                Assert.Equal(2000, ac.Content.Length);
                break;
        }
    }
}
