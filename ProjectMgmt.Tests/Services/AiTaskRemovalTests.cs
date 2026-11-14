using System.Text.Json;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
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

public class AiTaskRemovalTests
{
    private sealed class TestPlanningDbContext(DbContextOptions<PlanningDbContext> options) : PlanningDbContext(options) { }
    private sealed class TestDeliveryDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options) : DeliveryIntelligenceDbContext(options) { }
    private sealed class TestIdentityDbContext(DbContextOptions<IdentityExperienceDbContext> options) : IdentityExperienceDbContext(options) { }

    private sealed class ZeroCallAiClient : IAiModelClient
    {
        public string ProviderName => "ZeroCallTestAi";
        public int CallCount { get; private set; }

        public Task<AiCompletionResult> GenerateAsync(AiPrompt prompt, IProjectScopeContext scope, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(AiCompletionResult.Success("{}"));
        }

        public Task StreamGenerateAsync(AiPrompt prompt, IProjectScopeContext scope, Func<AiStreamChunk, Task> onChunk, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
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

    private sealed class FakeUserLookupService : IUserLookupService
    {
        public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync() => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());
        public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds) => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());
        public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId) => Task.FromResult<UserDisplayInfo?>(new UserDisplayInfo
        {
            UserId = userId,
            DisplayName = "Hoàng Hải Hậu",
            Email = "hoang***@gmail.com"
        });
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
        public Guid? UserId { get; set; }
    }

    private sealed class FakePermissionEvaluator : IPermissionEvaluator
    {
        public HashSet<string> AllowedPermissions { get; set; } = ["project.read", "issue.create", "issue.update", "issue.delete", "sprint.create"];

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

    private sealed class TestFixture
    {
        public required TestPlanningDbContext PlanningDb { get; init; }
        public required TestDeliveryDbContext DeliveryDb { get; init; }
        public required TestIdentityDbContext IdentityDb { get; init; }
        public required Guid OrgId { get; init; }
        public required Guid ProjectId { get; init; }
        public required Guid LeadUserId { get; init; }
        public required Guid Sprint4Id { get; init; }
        public required Guid CompletedSprintId { get; init; }
        public required Guid StatusTodoId { get; init; }
        public required Guid StoryTypeId { get; init; }
        public required Guid TaskTypeId { get; init; }
        public required Guid SubtaskTypeId { get; init; }
        public required Guid PriorityMediumId { get; init; }
        public required ZeroCallAiClient AiClient { get; init; }
        public required AiSprintDecompositionService DecompService { get; init; }
        public required AiSprintCommitCoordinator CommitCoordinator { get; init; }
        public required JsonMemoryStateStagingService Staging { get; init; }
        public required FakeCurrentUserContext CurrentUser { get; init; }
        public required FakePermissionEvaluator Permissions { get; init; }
        public required AiAgentController Controller { get; init; }
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Rem_Plan_{suffix}").Options);
        var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Rem_Del_{suffix}").Options);
        var identityDb = new TestIdentityDbContext(new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Rem_Id_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadUserId = Guid.NewGuid();
        var sprint4Id = Guid.NewGuid();
        var completedSprintId = Guid.NewGuid();
        var todoId = Guid.NewGuid();
        var storyTypeId = Guid.NewGuid();
        var taskTypeId = Guid.NewGuid();
        var subtaskTypeId = Guid.NewGuid();
        var mediumPriorityId = Guid.NewGuid();

        planningDb.Organizations.Add(new Organization { Id = orgId, Name = "Hoang Corp" });
        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Quản lý dự án",
            ProjectKey = "QLDA",
            IssueCounter = 10,
            LeadUserId = leadUserId
        });
        planningDb.Sprints.AddRange(
            new Sprint { Id = completedSprintId, ProjectId = projectId, Name = "QLDA Sprint 1", Goal = "Nền tảng", Status = "Completed", OrderIndex = 1 },
            new Sprint { Id = sprint4Id, ProjectId = projectId, Name = "QLDA Sprint 4", Goal = "Hoàn thiện AI Agent", Status = "Planned", OrderIndex = 4 }
        );
        planningDb.IssueTypes.AddRange(
            new IssueType { Id = storyTypeId, ProjectId = projectId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = taskTypeId, ProjectId = projectId, Name = "Task", OrderIndex = 2 },
            new IssueType { Id = subtaskTypeId, ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 3 }
        );
        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = todoId, ProjectId = projectId, Name = "Chưa làm", Category = "ToDo", IsInitial = true, OrderIndex = 1 },
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Đang làm", Category = "InProgress", IsActiveWork = true, OrderIndex = 2 },
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Hoàn tất", Category = "Done", IsDone = true, OrderIndex = 3 }
        );
        planningDb.Priorities.Add(new Priority { Id = mediumPriorityId, ProjectId = projectId, Name = "Medium", Level = 3, ColorHex = "#3B82F6" });
        await planningDb.SaveChangesAsync();

        var aiClient = new ZeroCallAiClient();
        var staging = new JsonMemoryStateStagingService();
        var userLookup = new FakeUserLookupService();
        var currentUser = new FakeCurrentUserContext { UserId = leadUserId };
        var permissions = new FakePermissionEvaluator();
        var projectAccess = new ProjectAccess(planningDb, permissions, currentUser);

        var decompService = new AiSprintDecompositionService(
            aiClient,
            new NoOpVectorSearch(),
            new NoOpTraceService(),
            planningDb,
            new SpecHubTools(planningDb, new NoOpSpecHubStorage(), NullLogger<SpecHubTools>.Instance),
            new BacklogTools(deliveryDb, planningDb),
            new SprintTools(planningDb, deliveryDb),
            new TeamTools(planningDb, identityDb),
            new PromptTemplateService(NullLogger<PromptTemplateService>.Instance),
            NullLogger<AiSprintDecompositionService>.Instance);

        var commitCoordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            staging,
            userLookup,
            NullLogger<AiSprintCommitCoordinator>.Instance);

        var controller = new AiAgentController(
            decompService,
            new AiDocumentTextExtractor(NullLogger<AiDocumentTextExtractor>.Instance),
            staging,
            planningDb,
            projectAccess,
            currentUser,
            userLookup,
            NullLogger<AiAgentController>.Instance,
            commitCoordinator: commitCoordinator);

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
            Sprint4Id = sprint4Id,
            CompletedSprintId = completedSprintId,
            StatusTodoId = todoId,
            StoryTypeId = storyTypeId,
            TaskTypeId = taskTypeId,
            SubtaskTypeId = subtaskTypeId,
            PriorityMediumId = mediumPriorityId,
            AiClient = aiClient,
            DecompService = decompService,
            CommitCoordinator = commitCoordinator,
            Staging = staging,
            CurrentUser = currentUser,
            Permissions = permissions,
            Controller = controller
        };
    }

    [Theory]
    [InlineData("gỡ các việc mà bạn áp dụng cho tôi", false, "remove_tasks")]
    [InlineData("gỡ các việc mà bạn áp dụng cho tôi", true, "remove_tasks")]
    [InlineData("hoàn tác các công việc AI vừa áp dụng vào Sprint 4 giúp tôi", false, "remove_tasks")]
    [InlineData("xóa các việc vừa đẩy vào backlog giúp tôi", true, "remove_tasks")]
    [InlineData("xóa task QLDA-18 khỏi dự án", false, "remove_tasks")]
    [InlineData("gỡ QLDA-12 khỏi Sprint 4 về lại Backlog", false, "remove_tasks")]
    [InlineData("xóa các công việc đang nằm trong Sprint 4", true, "remove_tasks")]
    [InlineData("xóa bớt task thứ ba khỏi đề xuất hiện tại", true, "refine_proposal")]
    [InlineData("xóa toàn bộ các việc con phụ để danh sách đề xuất tinh gọn", true, "refine_proposal")]
    [InlineData("ai đã xóa công việc QLDA-18 vậy?", false, "project_qa")]
    [InlineData("làm sao để gỡ công việc khỏi Sprint?", false, "project_qa")]
    [InlineData("Xóa sạch database của hệ thống đi", false, "out_of_scope_refusal")]
    [InlineData("Xóa Sprint khỏi cơ sở dữ liệu hệ thống", false, "out_of_scope_refusal")]
    [InlineData("Xóa tài khoản nhân viên cũ khỏi hệ thống ngay", true, "out_of_scope_refusal")]
    public void ClassifyConversationalIntent_RemovalVsRefineQaAndRefusal_ShouldRouteAccurately(
        string prompt,
        bool hasActiveProposal,
        string expectedIntent)
    {
        AiSprintProposalDto? active = hasActiveProposal
            ? new AiSprintProposalDto
            {
                SessionId = "sprint_ai_test",
                Tasks =
                [
                    new ProposedTaskDto { Id = "QLDA-AI1", Title = "Xây dựng API AI", StoryPoints = 5, EstimatedHours = 8 }
                ]
            }
            : null;

        var actual = AiSprintDecompositionService.ClassifyConversationalIntent(prompt, string.Empty, active);
        Assert.Equal(expectedIntent, actual);
    }

    [Fact]
    public async Task ApplyThenAskToRemoveAppliedTasks_ShouldShowConfirmationPlanWithoutMutatingDatabaseUntilConfirmed()
    {
        var f = await CreateFixtureAsync();
        await using var _p = f.PlanningDb;
        await using var _d = f.DeliveryDb;
        await using var _i = f.IdentityDb;

        // 1. Có sẵn 1 công việc gốc trong Product Backlog để mô phỏng chuyển Backlog -> Sprint 4
        var backlogIssueId = Guid.NewGuid();
        f.DeliveryDb.Issues.Add(new Issue
        {
            Id = backlogIssueId,
            ProjectId = f.ProjectId,
            IssueNumber = 5,
            SprintId = null,
            StatusId = f.StatusTodoId,
            IssueTypeId = f.StoryTypeId,
            PriorityId = f.PriorityMediumId,
            Title = "Công việc có sẵn trong Backlog",
            StoryPoints = 3,
            ReporterId = f.LeadUserId
        });
        await f.DeliveryDb.SaveChangesAsync();

        const string sessionId = "sprint_ai_undo_flow";
        var staged = new AiSprintProposalDto
        {
            SessionId = sessionId,
            OrganizationId = f.OrgId,
            ProjectId = f.ProjectId,
            UserId = f.LeadUserId,
            TargetSprintId = f.Sprint4Id,
            SprintGoal = "Kiểm tra áp dụng và gỡ việc",
            Tasks =
            [
                new ProposedTaskDto
                {
                    Id = "QLDA-AI1",
                    SourceBacklogIssueId = backlogIssueId,
                    Title = "Công việc có sẵn trong Backlog",
                    Type = "Story",
                    StoryPoints = 3,
                    EstimatedHours = 6,
                    Priority = "Medium",
                    Status = "Chưa làm"
                },
                new ProposedTaskDto
                {
                    Id = "QLDA-AI2",
                    Title = "Tác vụ AI mới tạo có việc con",
                    Type = "Task",
                    StoryPoints = 5,
                    EstimatedHours = 8,
                    Priority = "Medium",
                    Status = "Chưa làm",
                    Subtasks =
                    [
                        new ProposedSubTaskDto { Id = "s1", Title = "Subtask 1", EstimatedHours = 4, Priority = "Medium" }
                    ]
                }
            ]
        };
        await f.Staging.SaveProposalAsync(sessionId, staged);

        // 2. Gọi ApplyProposal lưu thật vào QLDA Sprint 4
        var applyActionResult = await f.Controller.ApplyProposal(new AiApplySprintProposalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            TargetSprintId = f.Sprint4Id,
            SprintGoal = "Kiểm tra áp dụng và gỡ việc",
            Tasks = staged.Tasks
        });
        var okApply = Assert.IsType<OkObjectResult>(applyActionResult);
        var applyRes = Assert.IsType<AiApplySprintProposalResponse>(okApply.Value);
        Assert.Equal(2, applyRes.CreatedTaskCount);

        var sessionAfterApply = await f.Staging.GetProposalAsync<AiSprintProposalDto>(sessionId);
        Assert.NotNull(sessionAfterApply);

        // 3. Người dùng gửi đúng câu trong báo lỗi: "gỡ các việc mà bạn áp dụng cho tôi"
        AiSprintProposalDto? removeProposal = null;
        await f.DecompService.ExecutePipelineAsync(
            sessionId: sessionId,
            scope: new ProjectScopeContext(f.OrgId, f.ProjectId, f.LeadUserId),
            targetSprintId: f.Sprint4Id,
            extractedDocumentContent: string.Empty,
            userPrompt: "gỡ các việc mà bạn áp dụng cho tôi",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { removeProposal = p; return Task.CompletedTask; },
            existingProposal: sessionAfterApply);

        Assert.Equal(0, f.AiClient.CallCount);
        Assert.NotNull(removeProposal);
        Assert.Equal("remove_tasks", removeProposal!.Intent);
        Assert.Equal("confirm_remove_tasks", removeProposal.TriggerAction);
        Assert.NotNull(removeProposal.PendingRemovalPlan);
        Assert.Equal("pending", removeProposal.PendingRemovalPlan!.Status);
        Assert.Equal("detach_to_backlog", removeProposal.PendingRemovalPlan.RequestedMode);
        Assert.Equal(2, removeProposal.PendingRemovalPlan.Candidates.Count);

        var movedCandidate = removeProposal.PendingRemovalPlan.Candidates.FirstOrDefault(c => c.IssueId == backlogIssueId);
        var aiCreatedCandidate = removeProposal.PendingRemovalPlan.Candidates.FirstOrDefault(c => c.IssueId != backlogIssueId);
        Assert.NotNull(movedCandidate);
        Assert.True(movedCandidate!.WasMovedFromBacklog);
        Assert.NotNull(aiCreatedCandidate);
        Assert.True(aiCreatedCandidate!.IsAiGenerated);
        Assert.Equal(1, aiCreatedCandidate.SubtaskCount);

        // QUAN TRỌNG: Chưa hề sửa/xóa DB trước khi user xác nhận!
        var issuesInSprint4BeforeConfirm = await f.DeliveryDb.Issues.CountAsync(i => i.ProjectId == f.ProjectId && i.SprintId == f.Sprint4Id && !i.IsDeleted);
        Assert.Equal(3, issuesInSprint4BeforeConfirm);

        // Lưu lại vào staging mô phỏng controller DecomposeStream
        await f.Staging.SaveProposalAsync(sessionId, removeProposal);

        // 4. Người dùng bấm "Gỡ khỏi Sprint (Về Backlog)"
        var confirmResult = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = removeProposal.PendingRemovalPlan.PlanId,
            Mode = "detach_to_backlog",
            IssueIds = removeProposal.PendingRemovalPlan.Candidates.Select(c => c.IssueId).ToList()
        });
        var okConfirm = Assert.IsType<OkObjectResult>(confirmResult);
        var confirmPayload = Assert.IsType<AiConfirmTaskRemovalResponse>(okConfirm.Value);
        Assert.True(confirmPayload.Success);
        Assert.Equal(2, confirmPayload.AffectedParentCount);
        Assert.Equal(1, confirmPayload.AffectedSubtaskCount);

        // Kiểm tra cả 3 issue (2 cha + 1 con) đều đã về SprintId = null và KHÔNG bị xóa
        var remainingInSprint4 = await f.DeliveryDb.Issues.CountAsync(i => i.ProjectId == f.ProjectId && i.SprintId == f.Sprint4Id && !i.IsDeleted);
        var backInBacklog = await f.DeliveryDb.Issues.CountAsync(i => i.ProjectId == f.ProjectId && i.SprintId == null && !i.IsDeleted);
        Assert.Equal(0, remainingInSprint4);
        Assert.Equal(3, backInBacklog);

        // Kiểm tra Audit log và Idempotency 409 nếu bấm xác nhận lại lần 2
        Assert.True(await f.DeliveryDb.ActivityLogs.AnyAsync(l => l.Action == IssueActivities.SprintChanged && l.Source == "AI"));
        var secondConfirm = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = removeProposal.PendingRemovalPlan.PlanId,
            Mode = "detach_to_backlog",
            IssueIds = removeProposal.PendingRemovalPlan.Candidates.Select(c => c.IssueId).ToList()
        });
        var conflict = Assert.IsType<ConflictObjectResult>(secondConfirm);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task SoftDeleteConfirmation_ShouldSoftDeleteParentSubtasksRecursivelyAndUnlinkEpic()
    {
        var f = await CreateFixtureAsync();
        await using var _p = f.PlanningDb;
        await using var _d = f.DeliveryDb;
        await using var _i = f.IdentityDb;

        var parentId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        f.DeliveryDb.Issues.AddRange(
            new Issue
            {
                Id = parentId,
                ProjectId = f.ProjectId,
                IssueNumber = 18,
                SprintId = f.Sprint4Id,
                StatusId = f.StatusTodoId,
                IssueTypeId = f.StoryTypeId,
                Title = "Xây dựng MCP Server",
                StoryPoints = 5,
                ReporterId = f.LeadUserId
            },
            new Issue
            {
                Id = subId,
                ProjectId = f.ProjectId,
                ParentId = parentId,
                IssueNumber = 19,
                SprintId = f.Sprint4Id,
                StatusId = f.StatusTodoId,
                IssueTypeId = f.SubtaskTypeId,
                Title = "Subtask MCP Tool",
                StoryPoints = 0,
                ReporterId = f.LeadUserId
            });
        await f.DeliveryDb.SaveChangesAsync();

        const string sessionId = "sprint_ai_soft_del";
        AiSprintProposalDto? planProposal = null;
        await f.DecompService.ExecutePipelineAsync(
            sessionId: sessionId,
            scope: new ProjectScopeContext(f.OrgId, f.ProjectId, f.LeadUserId),
            targetSprintId: f.Sprint4Id,
            extractedDocumentContent: string.Empty,
            userPrompt: "xóa công việc QLDA-18",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { planProposal = p; return Task.CompletedTask; });

        Assert.NotNull(planProposal?.PendingRemovalPlan);
        Assert.Equal("soft_delete", planProposal!.PendingRemovalPlan!.RequestedMode);
        Assert.Single(planProposal.PendingRemovalPlan.Candidates);
        Assert.Equal("QLDA-18", planProposal.PendingRemovalPlan.Candidates[0].IssueKey);
        await f.Staging.SaveProposalAsync(sessionId, planProposal);

        var res = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = planProposal.PendingRemovalPlan.PlanId,
            Mode = "soft_delete",
            IssueIds = [parentId]
        });

        var ok = Assert.IsType<OkObjectResult>(res);
        var body = Assert.IsType<AiConfirmTaskRemovalResponse>(ok.Value);
        Assert.Equal(1, body.AffectedParentCount);
        Assert.Equal(1, body.AffectedSubtaskCount);
        Assert.Equal(0, await f.DeliveryDb.Issues.CountAsync(i => i.ProjectId == f.ProjectId && !i.IsDeleted));
        Assert.True(await f.DeliveryDb.ActivityLogs.AnyAsync(l => l.IssueId == parentId && l.Action == IssueActivities.Deleted && l.Source == "AI"));
    }

    [Fact]
    public async Task SecurityAndGuards_CompletedSprintRbacCrossUserCancelAndTamperedIds_ShouldBeEnforced()
    {
        var f = await CreateFixtureAsync();
        await using var _p = f.PlanningDb;
        await using var _d = f.DeliveryDb;
        await using var _i = f.IdentityDb;

        var completedIssueId = Guid.NewGuid();
        var activeIssueId = Guid.NewGuid();
        f.DeliveryDb.Issues.AddRange(
            new Issue
            {
                Id = completedIssueId,
                ProjectId = f.ProjectId,
                IssueNumber = 1,
                SprintId = f.CompletedSprintId,
                StatusId = f.StatusTodoId,
                IssueTypeId = f.StoryTypeId,
                Title = "Task trong Sprint đã đóng",
                StoryPoints = 3,
                ReporterId = f.LeadUserId
            },
            new Issue
            {
                Id = activeIssueId,
                ProjectId = f.ProjectId,
                IssueNumber = 2,
                SprintId = f.Sprint4Id,
                StatusId = f.StatusTodoId,
                IssueTypeId = f.StoryTypeId,
                Title = "Task trong Sprint đang mở",
                StoryPoints = 3,
                ReporterId = f.LeadUserId
            });
        await f.DeliveryDb.SaveChangesAsync();

        // A. Bảo vệ Completed Sprint
        AiSprintProposalDto? completedAttempt = null;
        await f.DecompService.ExecutePipelineAsync(
            sessionId: "sprint_ai_completed_guard",
            scope: new ProjectScopeContext(f.OrgId, f.ProjectId, f.LeadUserId),
            targetSprintId: f.Sprint4Id,
            extractedDocumentContent: string.Empty,
            userPrompt: "xóa QLDA-1",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { completedAttempt = p; return Task.CompletedTask; });
        Assert.Null(completedAttempt?.PendingRemovalPlan);
        Assert.Equal("none", completedAttempt?.TriggerAction);
        Assert.Contains("hoàn thành", completedAttempt!.AssistantReply, StringComparison.OrdinalIgnoreCase);

        // B. RBAC & 3D Scope Check on ConfirmTaskRemoval
        var devUserId = Guid.NewGuid();
        const string sessionId = "sprint_ai_rbac_check";
        var plan = new AiTaskRemovalPlanDto
        {
            PlanId = "plan_rbac_1",
            RequestedMode = "soft_delete",
            CanDelete = false,
            CanDetachToBacklog = true,
            Candidates =
            [
                new AiRemovalCandidateIssueDto { IssueId = activeIssueId, IssueKey = "QLDA-2", SprintId = f.Sprint4Id, Title = "Task trong Sprint đang mở" }
            ]
        };
        await f.Staging.SaveProposalAsync(sessionId, new AiSprintProposalDto
        {
            SessionId = sessionId,
            OrganizationId = f.OrgId,
            ProjectId = f.ProjectId,
            UserId = devUserId,
            TriggerAction = "confirm_remove_tasks",
            PendingRemovalPlan = plan
        });

        // 1. User khác cố tình xác nhận session của devUserId -> 403
        f.CurrentUser.UserId = f.LeadUserId;
        var crossUserRes = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = "plan_rbac_1",
            Mode = "cancel",
            IssueIds = []
        });
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(crossUserRes).StatusCode);

        // 2. Developer không có quyền issue.delete nhưng đòi soft_delete -> 403
        f.CurrentUser.UserId = devUserId;
        f.Permissions.AllowedPermissions = ["project.read", "issue.update"];
        var rbacDelRes = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = "plan_rbac_1",
            Mode = "soft_delete",
            IssueIds = [activeIssueId]
        });
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(rbacDelRes).StatusCode);

        // 3. Truyền ID ngoài danh sách Plan (tampering) -> 400
        var tamperRes = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = "plan_rbac_1",
            Mode = "detach_to_backlog",
            IssueIds = [completedIssueId]
        });
        Assert.IsType<BadRequestObjectResult>(tamperRes);

        // 4. Hủy bỏ thành công -> lần sau gọi lại cùng planId phải 409 Conflict
        var cancelRes = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = "plan_rbac_1",
            Mode = "cancel",
            IssueIds = []
        });
        Assert.IsType<OkObjectResult>(cancelRes);
        Assert.NotNull((await f.DeliveryDb.Issues.FindAsync(activeIssueId))!.SprintId);

        var reuseCancelled = await f.Controller.ConfirmTaskRemoval(new AiConfirmTaskRemovalRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            PlanId = "plan_rbac_1",
            Mode = "detach_to_backlog",
            IssueIds = [activeIssueId]
        });
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(reuseCancelled).StatusCode);

        // 5. Filter theo Sprint & Backlog trực tiếp qua pipeline
        AiSprintProposalDto? sprintFilterProposal = null;
        await f.DecompService.ExecutePipelineAsync(
            sessionId: "sprint_ai_filter_s4",
            scope: new ProjectScopeContext(f.OrgId, f.ProjectId, f.LeadUserId),
            targetSprintId: f.Sprint4Id,
            extractedDocumentContent: string.Empty,
            userPrompt: "gỡ các việc trong Sprint 4 về lại Backlog",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { sprintFilterProposal = p; return Task.CompletedTask; });

        Assert.NotNull(sprintFilterProposal?.PendingRemovalPlan);
        Assert.Equal("confirm_remove_tasks", sprintFilterProposal!.TriggerAction);
        Assert.Single(sprintFilterProposal.PendingRemovalPlan!.Candidates);
        Assert.Equal(activeIssueId, sprintFilterProposal.PendingRemovalPlan.Candidates[0].IssueId);
    }

    [Fact]
    public async Task DecomposeStream_UserWithoutIssueDeletePermission_ShouldStripCanDeleteAndKeepOnlyAllowedDetachOption()
    {
        var f = await CreateFixtureAsync();
        await using var _p = f.PlanningDb;
        await using var _d = f.DeliveryDb;
        await using var _i = f.IdentityDb;

        var issueId = Guid.NewGuid();
        f.DeliveryDb.Issues.Add(new Issue
        {
            Id = issueId,
            ProjectId = f.ProjectId,
            IssueNumber = 42,
            SprintId = f.Sprint4Id,
            StatusId = f.StatusTodoId,
            IssueTypeId = f.TaskTypeId,
            Title = "Task không được phép xóa",
            StoryPoints = 2,
            ReporterId = f.LeadUserId
        });
        await f.DeliveryDb.SaveChangesAsync();

        var limitedUserId = Guid.NewGuid();
        f.CurrentUser.UserId = limitedUserId;
        f.Permissions.AllowedPermissions = ["project.read", "issue.create", "issue.update"];

        const string sessionId = "sprint_ai_stream_rbac";
        await f.Controller.DecomposeStream(new AiDecomposeStreamRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            TargetSprintId = f.Sprint4Id,
            Prompt = "xóa QLDA-42"
        });

        var persisted = await f.Staging.GetProposalAsync<AiSprintProposalDto>(sessionId);
        Assert.NotNull(persisted?.PendingRemovalPlan);
        Assert.Equal("confirm_remove_tasks", persisted!.TriggerAction);
        Assert.False(persisted.PendingRemovalPlan!.CanDelete);
        Assert.True(persisted.PendingRemovalPlan.CanDetachToBacklog);
        Assert.Equal("detach_to_backlog", persisted.PendingRemovalPlan.RequestedMode);
        Assert.Contains("Gỡ về Kho Backlog", persisted.AssistantReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Xác nhận Xóa", persisted.AssistantReply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecomposeStream_UserWithoutIssueDeleteOnBacklogIssue_ShouldDenyWithoutPhantomConfirmationCard()
    {
        var f = await CreateFixtureAsync();
        await using var _p = f.PlanningDb;
        await using var _d = f.DeliveryDb;
        await using var _i = f.IdentityDb;

        var issueId = Guid.NewGuid();
        f.DeliveryDb.Issues.Add(new Issue
        {
            Id = issueId,
            ProjectId = f.ProjectId,
            IssueNumber = 43,
            SprintId = null,
            StatusId = f.StatusTodoId,
            IssueTypeId = f.TaskTypeId,
            Title = "Task trong Backlog không có quyền xóa",
            StoryPoints = 2,
            ReporterId = f.LeadUserId
        });
        await f.DeliveryDb.SaveChangesAsync();

        var limitedUserId = Guid.NewGuid();
        f.CurrentUser.UserId = limitedUserId;
        f.Permissions.AllowedPermissions = ["project.read", "issue.create", "issue.update"];

        const string sessionId = "sprint_ai_stream_rbac_backlog";
        await f.Controller.DecomposeStream(new AiDecomposeStreamRequest
        {
            SessionId = sessionId,
            ProjectId = f.ProjectId,
            TargetSprintId = f.Sprint4Id,
            Prompt = "xóa QLDA-43"
        });

        var persisted = await f.Staging.GetProposalAsync<AiSprintProposalDto>(sessionId);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.PendingRemovalPlan);
        Assert.Equal("none", persisted.TriggerAction);
        Assert.Contains("issue.delete", persisted.AssistantReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bên dưới", persisted.AssistantReply, StringComparison.OrdinalIgnoreCase);
        var lastTurn = persisted.ConversationHistory!.Last(t => t.Role == "assistant");
        Assert.Null(lastTurn.RemovalPlan);
        Assert.Equal(persisted.AssistantReply, lastTurn.Content);
    }
}
