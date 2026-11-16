using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using Planning.Infrastructure.Services;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.Core.Tracing;
using ProjectMgmt.Core.VectorSearch;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Controllers;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;
using ProjectMgmt.Solution.Security;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

namespace ProjectMgmt.Tests.Services;

public class AiTaskQuickActionTests
{
    private sealed class TestPlanningDbContext(DbContextOptions<PlanningDbContext> options) : PlanningDbContext(options) { }
    private sealed class TestDeliveryDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options) : DeliveryIntelligenceDbContext(options) { }
    private sealed class TestIdentityDbContext(DbContextOptions<IdentityExperienceDbContext> options) : IdentityExperienceDbContext(options) { }

    private sealed class ScriptedAiClient(Func<AiPrompt, string> responder) : IAiModelClient
    {
        public string ProviderName => "ScriptedTestAi";
        public int CallCount { get; private set; }
        public List<AiPrompt> CapturedPrompts { get; } = [];

        public Task<AiCompletionResult> GenerateAsync(AiPrompt prompt, IProjectScopeContext scope, CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedPrompts.Add(prompt);
            return Task.FromResult(AiCompletionResult.Success(responder(prompt), model: "gemini-2.5-flash"));
        }

        public Task StreamGenerateAsync(AiPrompt prompt, IProjectScopeContext scope, Func<AiStreamChunk, Task> onChunk, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpSpecHubStorage : ISpecHubStorageService
    {
        public string GetProjectRootPath(string organizationName, string projectName) => string.Empty;
        public Task<string> EnsureProjectFolderAsync(string organizationName, string projectName, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<string> CreateFolderAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<string> WriteTextFileAsync(string organizationName, string projectName, string relativePath, string content, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<(string RelativePath, long FileSize)> SaveUploadedFileAsync(string organizationName, string projectName, string relativePath, Stream stream, CancellationToken cancellationToken = default) => Task.FromResult((relativePath, 0L));
        public Task<string> ReadTextFileAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult<(Stream, string, string)>((new MemoryStream(), "text/plain", "spec.md"));
        public Task<DocxPreviewDto> ExtractDocxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(new DocxPreviewDto("spec.docx", "<p>Spec</p>", "Spec"));
        public Task<XlsxPreviewDto> ExtractXlsxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(new XlsxPreviewDto("spec.xlsx", []));
        public Task DeleteAsync(string organizationName, string projectName, string relativePath, bool isDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RenameAsync(string organizationName, string projectName, string oldRelativePath, string newRelativePath, bool isDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpVectorSearch : IVectorSearchService
    {
        public Task<IReadOnlyList<VectorSearchResult>> SearchRelevantSpecsAsync(Guid projectId, string query, int topK = 5, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<VectorSearchResult>>(Array.Empty<VectorSearchResult>());
        public Task<bool> UpsertSpecChunkAsync(Guid projectId, string chunkId, string title, string content, string sourcePath, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class NoOpTraceService : IAiTraceService
    {
        public Task<string> StartTraceAsync(string traceId, Guid projectId, Guid userId, string sessionName, IReadOnlyDictionary<string, string>? tags = null, CancellationToken ct = default) => Task.FromResult(traceId);
        public Task RecordSpanAsync(AiTraceSpan span, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordGenerationAsync(string traceId, string modelName, string prompt, string completion, int promptTokens, int completionTokens, long durationMs, IReadOnlyDictionary<string, object?>? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordToolCallAsync(string traceId, string toolName, string inputJson, string outputJson, long durationMs, bool success, string? errorMessage = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordFeedbackAsync(string traceId, string feedbackType, decimal score, string? comment = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SeededUserLookupService(IReadOnlyList<UserDisplayInfo> users) : IUserLookupService
    {
        public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync() => Task.FromResult(users);
        public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds)
            => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(users.Where(u => userIds.Contains(u.UserId)).ToList());
        public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId)
            => Task.FromResult(users.FirstOrDefault(u => u.UserId == userId));
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
        public Guid? UserId { get; set; }
    }

    private sealed class FakePermissionEvaluator : IPermissionEvaluator
    {
        public HashSet<string> AllowedPermissions { get; set; } =
            ["project.read", "issue.create", "issue.update", "issue.assign", "sprint.create"];

        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, string scopeType, Guid? scopeId, Guid? organizationId = null)
            => Task.FromResult(AllowedPermissions.Contains(permissionCode));

        public Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
            => Task.FromResult(AllowedPermissions.Count > 0);
    }

    private sealed class JsonMemoryStateStagingService : IStateStagingService
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.Ordinal);
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public Task SaveProposalAsync<T>(string sessionId, T proposal, CancellationToken ct = default)
            => SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2), ct);

        public Task SaveProposalAsync<T>(string sessionId, T proposal, TimeSpan ttl, CancellationToken ct = default)
        {
            _store[sessionId] = JsonSerializer.Serialize(proposal, JsonOpts);
            return Task.CompletedTask;
        }

        public Task<T?> GetProposalAsync<T>(string sessionId, CancellationToken ct = default)
        {
            if (!_store.TryGetValue(sessionId, out var json)) return Task.FromResult<T?>(default);
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, JsonOpts));
        }

        public Task<bool> RemoveProposalAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult(_store.Remove(sessionId));

        public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult(_store.ContainsKey(sessionId));
    }

    private sealed class FakeMembersOnlyRoleServices(IReadOnlyCollection<Guid> memberIds) : IRoleServices
    {
        public Task<RoleDto> GetProjectMembersAsync(Guid projectId) => Task.FromResult(new RoleDto
        {
            Success = true,
            // Giống hành vi thật của RoleServices.GetProjectMembersAsync: chỉ điền Members và để Items = null
            Members = memberIds.Select(id => new RoleDto { UserId = id }).ToList(),
            Items = null
        });

        public Task<RoleDto> GetRolesAsync(string? scope) => throw new NotImplementedException();
        public Task<RoleDto> GetPermissionsAsync() => throw new NotImplementedException();
        public Task<RoleDto> CreateRoleAsync(RoleDto request) => throw new NotImplementedException();
        public Task<RoleDto> UpdateRolePermissionsAsync(Guid roleId, List<Guid>? permissionIds) => throw new NotImplementedException();
        public Task<RoleDto> GetUserRolesAsync(Guid userId, string? scopeType, Guid? scopeId) => throw new NotImplementedException();
        public Task<RoleDto> AssignUserRoleAsync(Guid actorUserId, Guid userId, RoleDto request) => throw new NotImplementedException();
        public Task<RoleDto> RemoveUserRoleAsync(Guid actorUserId, Guid userId, Guid userRoleId) => throw new NotImplementedException();
        public Task<RoleDto> AddProjectMemberAsync(Guid actorUserId, Guid projectId, Guid? userId, Guid? roleId) => throw new NotImplementedException();
        public Task<RoleDto> ChangeProjectMemberRoleAsync(Guid actorUserId, Guid projectId, Guid userId, Guid? newRoleId) => throw new NotImplementedException();
        public Task<RoleDto> RemoveProjectMemberAsync(Guid actorUserId, Guid projectId, Guid userId) => throw new NotImplementedException();
        public Task<RoleDto> InviteProjectMemberByEmailAsync(Guid actorUserId, Guid projectId, string email, string roleName) => throw new NotImplementedException();
        public Task<RoleDto> BatchInviteProjectMembersAsync(Guid actorUserId, Guid projectId, string emails, string? names, string roleName) => throw new NotImplementedException();
        public Task<RoleDto> RespondProjectInvitationAsync(Guid actorUserId, Guid projectId, Guid notificationId, bool accept) => throw new NotImplementedException();
    }

    private sealed class TestFixture
    {
        public required TestPlanningDbContext PlanningDb { get; init; }
        public required TestDeliveryDbContext DeliveryDb { get; init; }
        public required TestIdentityDbContext IdentityDb { get; init; }
        public required Guid OrgId { get; init; }
        public required Guid ProjectId { get; init; }
        public required Guid LeadUserId { get; init; }
        public required Guid DevBackendId { get; init; }
        public required Guid DevFrontendId { get; init; }
        public required Guid SprintActiveId { get; init; }
        public required Guid SprintCompletedId { get; init; }
        public required Guid StatusTodoId { get; init; }
        public required Guid StoryTypeId { get; init; }
        public required Guid SubtaskTypeId { get; init; }
        public required Guid PriorityMediumId { get; init; }
        public required Guid StoryIssueId { get; init; }
        public required Guid CompletedSprintIssueId { get; init; }
        public required Guid ExistingChildIssueId { get; init; }
        public required ScriptedAiClient AiClient { get; init; }
        public required JsonMemoryStateStagingService Staging { get; init; }
        public required FakeCurrentUserContext CurrentUser { get; init; }
        public required FakePermissionEvaluator Permissions { get; init; }
        public required AiSprintCommitCoordinator CommitCoordinator { get; init; }
        public required AiTaskQuickActionService QuickActionService { get; init; }
        public required AiAgentController Controller { get; init; }
    }

    private static async Task<TestFixture> CreateFixtureAsync(
        Func<AiPrompt, string> aiResponder,
        bool useMembersOnlyRoleServices = false)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Quick_Plan_{suffix}").Options);
        var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Quick_Del_{suffix}").Options);
        var identityDb = new TestIdentityDbContext(new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Quick_Id_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadUserId = Guid.NewGuid();
        var devBackendId = Guid.NewGuid();
        var devFrontendId = Guid.NewGuid();
        var sprintActiveId = Guid.NewGuid();
        var sprintCompletedId = Guid.NewGuid();
        var todoId = Guid.NewGuid();
        var storyTypeId = Guid.NewGuid();
        var subtaskTypeId = Guid.NewGuid();
        var mediumPriorityId = Guid.NewGuid();

        var devRoleId = Guid.NewGuid();
        identityDb.Roles.Add(new Role { Id = devRoleId, Name = "Developer", Scope = "Project", IsActive = true });
        identityDb.Users.AddRange(
            new User { Id = leadUserId, Email = "lead@example.com", NormalizedEmail = "LEAD@EXAMPLE.COM", IsActive = true },
            new User { Id = devBackendId, Email = "anh@example.com", NormalizedEmail = "ANH@EXAMPLE.COM", IsActive = true },
            new User { Id = devFrontendId, Email = "nam@example.com", NormalizedEmail = "NAM@EXAMPLE.COM", IsActive = true }
        );
        identityDb.UserProfiles.AddRange(
            new UserProfile { Id = Guid.NewGuid(), UserId = leadUserId, DisplayName = "Hoàng Hải Hậu", JobTitle = "Project Director", SeniorityLevel = "Principal", YearsOfExperience = 6m },
            new UserProfile { Id = Guid.NewGuid(), UserId = devBackendId, DisplayName = "Phạm Đức Anh", JobTitle = "Backend Dev", SeniorityLevel = "Senior", YearsOfExperience = 4m },
            new UserProfile { Id = Guid.NewGuid(), UserId = devFrontendId, DisplayName = "Vũ Hoàng Nam", JobTitle = "Frontend Dev", SeniorityLevel = "Mid", YearsOfExperience = 2.5m }
        );
        identityDb.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), UserId = devBackendId, RoleId = devRoleId, ScopeType = "Project", ScopeId = projectId },
            new UserRole { Id = Guid.NewGuid(), UserId = devFrontendId, RoleId = devRoleId, ScopeType = "Project", ScopeId = projectId }
        );
        await identityDb.SaveChangesAsync();

        planningDb.Organizations.Add(new Organization { Id = orgId, Name = "Hoang Corp" });
        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Quản lý dự án",
            ProjectKey = "QLDA",
            IssueCounter = 12,
            LeadUserId = leadUserId
        });
        planningDb.Sprints.AddRange(
            new Sprint { Id = sprintCompletedId, ProjectId = projectId, Name = "QLDA Sprint 1", Goal = "Cơ bản", Status = "Completed", OrderIndex = 1 },
            new Sprint { Id = sprintActiveId, ProjectId = projectId, Name = "QLDA Sprint 4", Goal = "AI & MCP", Status = "Active", OrderIndex = 4 }
        );
        planningDb.IssueTypes.AddRange(
            new IssueType { Id = storyTypeId, ProjectId = projectId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = subtaskTypeId, ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 2 }
        );
        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = todoId, ProjectId = projectId, Name = "Chưa làm", Category = "ToDo", IsInitial = true, OrderIndex = 1 },
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Hoàn tất", Category = "Done", IsDone = true, OrderIndex = 2 }
        );
        planningDb.Priorities.Add(new Priority { Id = mediumPriorityId, ProjectId = projectId, Name = "Medium", Level = 3, ColorHex = "#3B82F6" });
        planningDb.ProjectDocuments.Add(new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "spec-ai-mcp.md",
            Title = "Đặc tả MCP & Redis",
            Content = "Hệ thống sử dụng C# backend API và Redis staging cho Trợ Lý AI."
        });
        await planningDb.SaveChangesAsync();

        var storyIssueId = Guid.NewGuid();
        var completedSprintIssueId = Guid.NewGuid();
        var existingChildIssueId = Guid.NewGuid();
        var extraLoadIssueId = Guid.NewGuid();

        deliveryDb.Issues.AddRange(
            new Issue
            {
                Id = storyIssueId,
                ProjectId = projectId,
                IssueNumber = 10,
                SprintId = sprintActiveId,
                ReporterId = leadUserId,
                StatusId = todoId,
                IssueTypeId = storyTypeId,
                PriorityId = mediumPriorityId,
                Title = "Xây dựng API đồng bộ MCP & cache Redis",
                Description = "Cài đặt endpoint REST và lưu cache Redis TTL 2h",
                StoryPoints = 5,
                EstimatedHours = 12,
                RankOrder = 1000m
            },
            new Issue
            {
                Id = completedSprintIssueId,
                ProjectId = projectId,
                IssueNumber = 11,
                SprintId = sprintCompletedId,
                ReporterId = leadUserId,
                StatusId = todoId,
                IssueTypeId = storyTypeId,
                PriorityId = mediumPriorityId,
                Title = "Task thuộc Sprint đã đóng",
                StoryPoints = 3,
                RankOrder = 2000m
            },
            new Issue
            {
                Id = existingChildIssueId,
                ProjectId = projectId,
                IssueNumber = 12,
                SprintId = sprintActiveId,
                ParentId = storyIssueId,
                ReporterId = leadUserId,
                StatusId = todoId,
                IssueTypeId = subtaskTypeId,
                PriorityId = mediumPriorityId,
                Title = "Thiết kế DTO Redis",
                StoryPoints = 1,
                RankOrder = 3000m
            },
            new Issue
            {
                Id = extraLoadIssueId,
                ProjectId = projectId,
                IssueNumber = 13,
                SprintId = sprintActiveId,
                ReporterId = leadUserId,
                AssigneeId = devFrontendId,
                StatusId = todoId,
                IssueTypeId = storyTypeId,
                PriorityId = mediumPriorityId,
                Title = "Tối ưu bảng Kanban UI",
                StoryPoints = 5,
                EstimatedHours = 20,
                RemainingHours = 20,
                RankOrder = 4000m
            }
        );
        await deliveryDb.SaveChangesAsync();

        var userDisplays = new List<UserDisplayInfo>
        {
            new() { UserId = leadUserId, DisplayName = "Hoàng Hải Hậu", Email = "lead@example.com" },
            new() { UserId = devBackendId, DisplayName = "Phạm Đức Anh", Email = "anh@example.com" },
            new() { UserId = devFrontendId, DisplayName = "Vũ Hoàng Nam", Email = "nam@example.com" }
        };

        var aiClient = new ScriptedAiClient(aiResponder);
        var staging = new JsonMemoryStateStagingService();
        var userLookup = new SeededUserLookupService(userDisplays);
        var currentUser = new FakeCurrentUserContext { UserId = leadUserId };
        var permissions = new FakePermissionEvaluator();
        var projectAccess = new ProjectAccess(planningDb, permissions, currentUser);
        var teamTools = new TeamTools(planningDb, identityDb, deliveryDb);
        var specHubTools = new SpecHubTools(planningDb, new NoOpSpecHubStorage(), NullLogger<SpecHubTools>.Instance);

        var decompService = new AiSprintDecompositionService(
            aiClient,
            new NoOpVectorSearch(),
            new NoOpTraceService(),
            planningDb,
            specHubTools,
            new BacklogTools(deliveryDb, planningDb),
            new SprintTools(planningDb, deliveryDb),
            teamTools,
            new PromptTemplateService(NullLogger<PromptTemplateService>.Instance),
            NullLogger<AiSprintDecompositionService>.Instance);

        var roleServices = useMembersOnlyRoleServices
            ? new FakeMembersOnlyRoleServices([devBackendId, devFrontendId])
            : null;

        var commitCoordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            staging,
            userLookup,
            NullLogger<AiSprintCommitCoordinator>.Instance,
            roleServices: roleServices);

        var quickActionService = new AiTaskQuickActionService(
            aiClient,
            planningDb,
            deliveryDb,
            staging,
            teamTools,
            specHubTools,
            tokenTracker: null,
            logger: NullLogger<AiTaskQuickActionService>.Instance);

        var controller = new AiAgentController(
            decompService,
            new AiDocumentTextExtractor(NullLogger<AiDocumentTextExtractor>.Instance),
            staging,
            planningDb,
            projectAccess,
            currentUser,
            userLookup,
            NullLogger<AiAgentController>.Instance,
            commitCoordinator: commitCoordinator,
            quickActionService: quickActionService);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } }
        };

        return new TestFixture
        {
            PlanningDb = planningDb,
            DeliveryDb = deliveryDb,
            IdentityDb = identityDb,
            OrgId = orgId,
            ProjectId = projectId,
            LeadUserId = leadUserId,
            DevBackendId = devBackendId,
            DevFrontendId = devFrontendId,
            SprintActiveId = sprintActiveId,
            SprintCompletedId = sprintCompletedId,
            StatusTodoId = todoId,
            StoryTypeId = storyTypeId,
            SubtaskTypeId = subtaskTypeId,
            PriorityMediumId = mediumPriorityId,
            StoryIssueId = storyIssueId,
            CompletedSprintIssueId = completedSprintIssueId,
            ExistingChildIssueId = existingChildIssueId,
            AiClient = aiClient,
            Staging = staging,
            CurrentUser = currentUser,
            Permissions = permissions,
            CommitCoordinator = commitCoordinator,
            QuickActionService = quickActionService,
            Controller = controller
        };
    }

    [Fact]
    public async Task SubtaskBreakdown_StagesOnlyOnRedis_AndCommitsOnlyAfterConfirm()
    {
        var fx = await CreateFixtureAsync(_ => JsonSerializer.Serialize(new
        {
            rationale = "Phân rã Backend & QA theo đúng ngữ cảnh MCP TeamTools.",
            subtasks = new[]
            {
                new
                {
                    title = "Cài đặt endpoint đồng bộ Redis TTL",
                    role = "Backend",
                    description = "Viết service staging Redis & endpoint REST",
                    estimatedHours = 5,
                    storyPoints = 2,
                    priority = "Medium",
                    assigneeName = "Phạm Đức Anh",
                    acceptanceCriteria = new[] { "Key Redis có TTL 2h" },
                    suggestedSkills = new[] { "C#" }
                },
                new
                {
                    title = "Viết integration test kiểm chứng Redis staging",
                    role = "QA",
                    description = "Kiểm thử không ghi DB trước confirm",
                    estimatedHours = 3,
                    storyPoints = 1,
                    priority = "Medium",
                    assigneeName = "Vũ Hoàng Nam",
                    acceptanceCriteria = new[] { "100% pass" },
                    suggestedSkills = new[] { "xUnit" }
                }
            }
        }));

        var initialIssueCount = await fx.DeliveryDb.Issues.CountAsync();

        var runResult = await fx.Controller.RunQuickAction(new AiQuickActionRunRequest
        {
            ProjectId = fx.ProjectId,
            IssueId = fx.StoryIssueId,
            ActionType = "subtask_breakdown"
        });

        var okRun = Assert.IsType<OkObjectResult>(runResult);
        var plan = Assert.IsType<AiQuickActionPlanDto>(okRun.Value);

        Assert.True(plan.StagedInRedis);
        Assert.True(plan.UsedLlm);
        Assert.Equal("pending", plan.Status);
        Assert.Equal("subtask_breakdown", plan.ActionType);
        Assert.Equal(2, plan.ProposedSubtasks.Count);
        Assert.Equal(fx.DevBackendId, plan.ProposedSubtasks[0].SuggestedAssigneeUserId);
        Assert.True(await fx.Staging.ExistsAsync(plan.ActionId));
        Assert.Contains("Phạm Đức Anh", fx.AiClient.CapturedPrompts[0].Messages[0].Content, StringComparison.Ordinal);

        // Tuyệt đối KHÔNG ghi DB trước khi người dùng bấm Xác nhận áp dụng
        Assert.Equal(initialIssueCount, await fx.DeliveryDb.Issues.CountAsync());
        Assert.Empty(await fx.DeliveryDb.ActivityLogs.ToListAsync());

        // Bấm Xác nhận áp dụng chỉ với Sub-task đầu tiên (đã sửa tên)
        var confirmResult = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "apply",
            SelectedSubtasks =
            [
                new AiQuickSelectedSubtaskInput
                {
                    TempId = plan.ProposedSubtasks[0].TempId,
                    Title = "Cài đặt API đồng bộ Redis đã rà soát"
                }
            ]
        });

        var okConfirm = Assert.IsType<OkObjectResult>(confirmResult);
        var confirmDto = Assert.IsType<AiQuickActionConfirmResponse>(okConfirm.Value);
        Assert.True(confirmDto.Success);
        Assert.Equal(1, confirmDto.CreatedSubtaskCount);
        Assert.Single(confirmDto.CreatedIssues);

        Assert.Equal(initialIssueCount + 1, await fx.DeliveryDb.Issues.CountAsync());
        var createdSub = await fx.DeliveryDb.Issues.FirstOrDefaultAsync(i => i.Id == confirmDto.CreatedIssues[0].IssueId);
        Assert.NotNull(createdSub);
        Assert.Equal(fx.StoryIssueId, createdSub!.ParentId);
        Assert.Equal(fx.SprintActiveId, createdSub.SprintId);
        Assert.Equal(fx.DevBackendId, createdSub.AssigneeId);
        Assert.True(createdSub.IsAiGenerated);
        Assert.Equal("[Backend] Cài đặt API đồng bộ Redis đã rà soát", createdSub.Title);

        var logs = await fx.DeliveryDb.ActivityLogs.Where(l => l.IssueId == createdSub.Id).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("AI", logs[0].Source);
        Assert.Equal("AiQuickSubtaskCreated", logs[0].Action);

        var updatedStaged = await fx.Staging.GetProposalAsync<AiQuickActionPlanDto>(plan.ActionId);
        Assert.Equal("executed", updatedStaged!.Status);

        // Chống Confirm lặp lần 2 -> 409 Conflict
        var replay = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "apply"
        });
        Assert.IsType<ConflictObjectResult>(replay);
    }

    [Fact]
    public async Task SubtaskBreakdown_RefusesExistingSubtask_AndCompletedSprintIssue()
    {
        var fx = await CreateFixtureAsync(_ => "{}");

        var subOfSub = await fx.Controller.RunQuickAction(new AiQuickActionRunRequest
        {
            ProjectId = fx.ProjectId,
            IssueId = fx.ExistingChildIssueId,
            ActionType = "subtask_breakdown"
        });
        Assert.IsType<BadRequestObjectResult>(subOfSub);

        var completedRes = await fx.Controller.RunQuickAction(new AiQuickActionRunRequest
        {
            ProjectId = fx.ProjectId,
            IssueId = fx.CompletedSprintIssueId,
            ActionType = "subtask_breakdown"
        });
        Assert.IsType<BadRequestObjectResult>(completedRes);
    }

    [Fact]
    public async Task AssigneeSuggestion_StagesRankedCandidatesInRedis_AndRecordsDecisionHistoryOnConfirm()
    {
        ScriptedAiClient? holderClient = null;
        var fx = await CreateFixtureAsync(_ => string.Empty);

        // Inject responding JSON knowing actual DevBackendId
        holderClient = new ScriptedAiClient(_ => JsonSerializer.Serialize(new
        {
            rationale = "Phạm Đức Anh khớp chuyên môn Backend và đang rảnh 40h.",
            candidates = new[]
            {
                new
                {
                    userId = fx.DevBackendId.ToString(),
                    displayName = "Phạm Đức Anh",
                    totalScore = 94.0,
                    loadBalanceScore = 95.0,
                    skillMatchScore = 96.0,
                    historyScore = 88.0,
                    capacityScore = 95.0,
                    reason = "Senior Backend Dev, đang không vướng task và còn 40h khả dụng."
                },
                new
                {
                    userId = fx.DevFrontendId.ToString(),
                    displayName = "Vũ Hoàng Nam",
                    totalScore = 62.5,
                    loadBalanceScore = 58.0,
                    skillMatchScore = 55.0,
                    historyScore = 70.0,
                    capacityScore = 65.0,
                    reason = "Chuyên môn Frontend và đang xử lý 1 story 20h trong Sprint."
                }
            }
        }));

        var service = new AiTaskQuickActionService(
            holderClient,
            fx.PlanningDb,
            fx.DeliveryDb,
            fx.Staging,
            new TeamTools(fx.PlanningDb, fx.IdentityDb, fx.DeliveryDb),
            new SpecHubTools(fx.PlanningDb, new NoOpSpecHubStorage(), NullLogger<SpecHubTools>.Instance));

        var scope = new ProjectScopeContext(fx.OrgId, fx.ProjectId, fx.LeadUserId);
        var plan = await service.RunQuickActionAsync(new AiQuickActionRunRequest
        {
            ProjectId = fx.ProjectId,
            IssueId = fx.StoryIssueId,
            ActionType = "assignee_suggestion"
        }, scope);

        Assert.True(plan.StagedInRedis);
        Assert.Equal(fx.DevBackendId, plan.RecommendedUserId);
        Assert.True(plan.AssigneeCandidates.Count >= 2);
        Assert.Equal("Phạm Đức Anh", plan.AssigneeCandidates[0].DisplayName);

        // Trước khi xác nhận: DB tuyệt đối chưa đổi Assignee hay tạo AiAssignmentRun
        var storyBefore = await fx.DeliveryDb.Issues.AsNoTracking().FirstAsync(i => i.Id == fx.StoryIssueId);
        Assert.Null(storyBefore.AssigneeId);
        Assert.False(storyBefore.IsAiAssigned);
        Assert.Empty(await fx.PlanningDb.AiAssignmentRuns.ToListAsync());
        Assert.Empty(await fx.DeliveryDb.IssueAssignmentHistories.ToListAsync());

        // Người dùng bấm Xác nhận áp dụng ứng viên #1 (Accepted)
        var confirmRes = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "apply",
            SelectedAssigneeUserId = fx.DevBackendId
        });

        var okConfirm = Assert.IsType<OkObjectResult>(confirmRes);
        var confirmDto = Assert.IsType<AiQuickActionConfirmResponse>(okConfirm.Value);
        Assert.True(confirmDto.Success);
        Assert.Equal(fx.DevBackendId, confirmDto.AssignedUserId);

        var storyAfter = await fx.DeliveryDb.Issues.AsNoTracking().FirstAsync(i => i.Id == fx.StoryIssueId);
        Assert.Equal(fx.DevBackendId, storyAfter.AssigneeId);
        Assert.True(storyAfter.IsAiAssigned);

        var run = Assert.Single(await fx.PlanningDb.AiAssignmentRuns.ToListAsync());
        Assert.Equal("Completed", run.Status);
        Assert.Equal("Hybrid", run.Strategy);
        Assert.Equal("Manual", run.TriggerSource);

        var decision = Assert.Single(await fx.PlanningDb.AiAssignmentDecisions.ToListAsync());
        Assert.Equal("Accepted", decision.Outcome);
        Assert.Equal(fx.DevBackendId, decision.FinalUserId);

        var history = Assert.Single(await fx.DeliveryDb.IssueAssignmentHistories.ToListAsync());
        Assert.Equal("AiSuggested", history.AssignmentSource);
        Assert.NotNull(history.AiCandidateId);
        Assert.Equal(fx.DevBackendId, history.ToAssigneeId);

        var actLog = Assert.Single(await fx.DeliveryDb.ActivityLogs.ToListAsync());
        Assert.Equal("AiQuickAssigneeApplied", actLog.Action);
        Assert.Equal("AI", actLog.Source);
    }

    [Fact]
    public async Task ConfirmQuickAction_EnforcesRbacCrossUser_AndCancelFlow()
    {
        var fx = await CreateFixtureAsync(_ => "{}");

        // Dùng DevFrontend chạy quick action assignee_suggestion (AI sẽ fallback đánh giá từ dữ liệu thật -> khuyến nghị Phạm Đức Anh còn 40h rảnh)
        fx.CurrentUser.UserId = fx.DevFrontendId;
        fx.Permissions.AllowedPermissions = ["project.read", "issue.update"]; // KHÔNG có issue.assign

        var runRes = await fx.Controller.RunQuickAction(new AiQuickActionRunRequest
        {
            ProjectId = fx.ProjectId,
            IssueId = fx.StoryIssueId,
            ActionType = "assignee_suggestion"
        });
        var plan = Assert.IsType<AiQuickActionPlanDto>(Assert.IsType<OkObjectResult>(runRes).Value);

        // 1. Cross-user cố xác nhận ActionId của người khác -> 403 FORBIDDEN_SESSION_SCOPE
        fx.CurrentUser.UserId = fx.DevBackendId;
        var crossRes = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "apply",
            SelectedAssigneeUserId = fx.DevBackendId
        });
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(crossRes).StatusCode);

        // 2. Chính chủ (chỉ có issue.update, không có issue.assign) cố gán người khác -> 403 FORBIDDEN_ASSIGN
        fx.CurrentUser.UserId = fx.DevFrontendId;
        var otherAssignRes = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "apply",
            SelectedAssigneeUserId = fx.DevBackendId
        });
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(otherAssignRes).StatusCode);

        // 3. Hủy bỏ -> trạng thái Redis sang cancelled, Issue vẫn chưa có AssigneeId, apply lại ra 409 Conflict
        var cancelRes = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "cancel"
        });
        Assert.IsType<OkObjectResult>(cancelRes);
        var issueStillUnassigned = await fx.DeliveryDb.Issues.AsNoTracking().FirstAsync(i => i.Id == fx.StoryIssueId);
        Assert.Null(issueStillUnassigned.AssigneeId);

        var replayAfterCancel = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "apply",
            SelectedAssigneeUserId = fx.DevFrontendId
        });
        Assert.IsType<ConflictObjectResult>(replayAfterCancel);
    }

    [Fact]
    public void NormalizeRole_ShouldRespectWordBoundaries_NotFalseMatchUiInsideCircuit()
    {
        Assert.Equal("Backend", AiTaskQuickActionService.NormalizeRole("Backend", "Phát triển middleware Circuit Breaker cơ chế Half-Open thử nghiệm"));
        Assert.Equal("Backend", AiTaskQuickActionService.NormalizeRole(null, "Phát triển middleware Circuit Breaker cơ chế Half-Open thử nghiệm"));
        Assert.Equal("Backend", AiTaskQuickActionService.NormalizeRole("Developer", "Xây dựng cơ chế Circuit Breaker chịu lỗi cho dịch vụ"));
        Assert.Equal("Frontend", AiTaskQuickActionService.NormalizeRole(null, "Thiết kế giao diện UI trang Backlog"));
        Assert.Equal("Frontend", AiTaskQuickActionService.NormalizeRole("Frontend", "Xây dựng UI/UX dashboard giám sát Circuit Breaker"));
        Assert.Equal("QA", AiTaskQuickActionService.NormalizeRole(null, "Viết integration test cho module thanh toán"));
    }

    [Fact]
    public async Task QuickSubtaskAndSprintCommit_WithRoleServicesPopulatingMembersOnly_ShouldPreserveNonLeadAssignees()
    {
        var fx = await CreateFixtureAsync(_ => JsonSerializer.Serialize(new
        {
            rationale = "Phân rã Circuit Breaker middleware cho Backend.",
            subtasks = new[]
            {
                new
                {
                    title = "Phát triển middleware Circuit Breaker cơ chế Half-Open thử nghiệm",
                    role = "Backend",
                    description = "Xây dựng middleware chống lỗi dây chuyền và phục hồi Half-Open",
                    estimatedHours = 4,
                    storyPoints = 2,
                    priority = "Medium",
                    assigneeName = "Phạm Đức Anh",
                    acceptanceCriteria = new[] { "Chuyển trạng thái Closed -> Open -> Half-Open chính xác" },
                    suggestedSkills = new[] { "C#" }
                }
            }
        }), useMembersOnlyRoleServices: true);

        var runResult = await fx.Controller.RunQuickAction(new AiQuickActionRunRequest
        {
            ProjectId = fx.ProjectId,
            IssueId = fx.StoryIssueId,
            ActionType = "subtask_breakdown"
        });

        var plan = Assert.IsType<AiQuickActionPlanDto>(Assert.IsType<OkObjectResult>(runResult).Value);
        var proposed = Assert.Single(plan.ProposedSubtasks);
        Assert.Equal("Backend", proposed.Role);
        Assert.Equal(fx.DevBackendId, proposed.SuggestedAssigneeUserId);

        var confirmResult = await fx.Controller.ConfirmQuickAction(new AiQuickActionConfirmRequest
        {
            ActionId = plan.ActionId,
            ProjectId = fx.ProjectId,
            Mode = "apply",
            SelectedSubtasks =
            [
                new AiQuickSelectedSubtaskInput
                {
                    TempId = proposed.TempId,
                    Title = proposed.Title
                }
            ]
        });

        var confirmDto = Assert.IsType<AiQuickActionConfirmResponse>(Assert.IsType<OkObjectResult>(confirmResult).Value);
        Assert.True(confirmDto.Success);
        var createdSub = await fx.DeliveryDb.Issues.AsNoTracking().FirstAsync(i => i.Id == confirmDto.CreatedIssues[0].IssueId);
        Assert.StartsWith("[Backend]", createdSub.Title, StringComparison.Ordinal);
        Assert.Equal(fx.DevBackendId, createdSub.AssigneeId);

        var sprintApplyResponse = await fx.CommitCoordinator.ApplySprintProposalAsync(new AiApplySprintProposalRequest
        {
            SessionId = $"sprint_ai_mem_{Guid.NewGuid():N}",
            ProjectId = fx.ProjectId,
            PushToBacklog = true,
            SprintGoal = "Kiểm tra giữ nguyên Assignee khi Members được điền",
            Tasks =
            [
                new ProposedTaskDto
                {
                    Id = "t_mem_1",
                    Title = "Xây dựng component UI màn hình lọc",
                    StoryPoints = 3,
                    AssigneeUserId = fx.DevFrontendId,
                    AssigneeName = "Vũ Hoàng Nam"
                }
            ]
        }, new ProjectScopeContext(fx.OrgId, fx.ProjectId, fx.LeadUserId));

        Assert.True(sprintApplyResponse.Success);
        var createdBacklogTaskId = Assert.Single(sprintApplyResponse.CreatedIssues).IssueId;
        var createdBacklogTask = await fx.DeliveryDb.Issues.AsNoTracking().FirstAsync(i => i.Id == createdBacklogTaskId);
        Assert.Equal(fx.DevFrontendId, createdBacklogTask.AssigneeId);
    }
}

