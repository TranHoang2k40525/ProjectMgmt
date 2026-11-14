using System.Text.Json;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Infrastructure;
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
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

namespace ProjectMgmt.Tests.Services;

public class AiConversationalAssistantTests
{
    private sealed class TestPlanningDbContext(DbContextOptions<PlanningDbContext> options) : PlanningDbContext(options) { }
    private sealed class TestDeliveryDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options) : DeliveryIntelligenceDbContext(options) { }
    private sealed class TestIdentityDbContext(DbContextOptions<IdentityExperienceDbContext> options) : IdentityExperienceDbContext(options) { }

    private sealed class CapturingAiClient(Func<AiPrompt, string> responseFactory) : IAiModelClient
    {
        public string ProviderName => "CapturingTestAi";
        public List<AiPrompt> CapturedPrompts { get; } = [];

        public Task<AiCompletionResult> GenerateAsync(AiPrompt prompt, IProjectScopeContext scope, CancellationToken cancellationToken = default)
        {
            CapturedPrompts.Add(prompt);
            return Task.FromResult(AiCompletionResult.Success(responseFactory(prompt)));
        }

        public async Task StreamGenerateAsync(AiPrompt prompt, IProjectScopeContext scope, Func<AiStreamChunk, Task> onChunk, CancellationToken cancellationToken = default)
        {
            CapturedPrompts.Add(prompt);
            await onChunk(new AiStreamChunk { ChunkType = AiStreamChunkType.Content, DeltaContent = responseFactory(prompt) });
            await onChunk(new AiStreamChunk { ChunkType = AiStreamChunkType.Done, FinishReason = "stop" });
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

    private sealed class NoOpUserLookupService : IUserLookupService
    {
        public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync() => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());
        public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds) => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());
        public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId) => Task.FromResult<UserDisplayInfo?>(null);
    }

    private static async Task<(TestPlanningDbContext PlanningDb, TestDeliveryDbContext DeliveryDb, TestIdentityDbContext IdentityDb, Guid OrgId, Guid ProjectId, Guid ActorId)> SeedProjectAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Conv_Plan_{suffix}").Options);
        var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Conv_Del_{suffix}").Options);
        var identityDb = new TestIdentityDbContext(new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Conv_Id_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        planningDb.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Hệ thống QLDA", ProjectKey = "QLDA", IssueCounter = 5, LeadUserId = actorId });
        planningDb.IssueTypes.AddRange(
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Task", OrderIndex = 2 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Bug", OrderIndex = 3 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Epic", OrderIndex = 4 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 5 }
        );
        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "To Do", Category = "ToDo", IsInitial = true, OrderIndex = 1 },
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "In Progress", Category = "InProgress", IsActiveWork = true, OrderIndex = 2 },
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Done", Category = "Done", IsDone = true, OrderIndex = 3 }
        );
        planningDb.Priorities.AddRange(
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Urgent", Level = 1, ColorHex = "#DC2626" },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "High", Level = 2, ColorHex = "#F59E0B" },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Medium", Level = 3, ColorHex = "#3B82F6" }
        );

        await planningDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();
        await identityDb.SaveChangesAsync();
        return (planningDb, deliveryDb, identityDb, orgId, projectId, actorId);
    }

    private static AiSprintDecompositionService CreateService(
        CapturingAiClient aiClient,
        TestPlanningDbContext planningDb,
        TestDeliveryDbContext deliveryDb,
        TestIdentityDbContext identityDb)
        => new(
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

    [Fact]
    public async Task ExecutePipelineAsync_OutOfScopeRequest_ShouldRefuseAndPreserveExistingTasks()
    {
        var (planningDb, deliveryDb, identityDb, orgId, projectId, actorId) = await SeedProjectAsync();
        await using var _p = planningDb;
        await using var _d = deliveryDb;
        await using var _i = identityDb;

        var aiClient = new CapturingAiClient(_ => "{}");
        var service = CreateService(aiClient, planningDb, deliveryDb, identityDb);
        var scope = new ProjectScopeContext(orgId, projectId, actorId);

        var existing = new AiSprintProposalDto
        {
            SessionId = "sprint_ai_existing",
            ProjectId = projectId,
            SprintGoal = "Xây dựng module Báo cáo",
            TotalStoryPoints = 5,
            TotalEstimatedHours = 12,
            Tasks =
            [
                new ProposedTaskDto { Id = "t1", Title = "Xây dựng API thống kê", Type = "Task", Priority = "High", Status = "To Do", StoryPoints = 5, EstimatedHours = 12 }
            ]
        };

        AiSprintProposalDto? result = null;
        await service.ExecutePipelineAsync(
            sessionId: "sprint_ai_existing",
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: string.Empty,
            userPrompt: "Thời tiết Hà Nội hôm nay thế nào? Kể chuyện cười đi.",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { result = p; return Task.CompletedTask; },
            existingProposal: existing);

        Assert.NotNull(result);
        Assert.Equal("out_of_scope_refusal", result.Intent);
        Assert.Equal("none", result.TriggerAction);
        Assert.Contains("ngoài phạm vi", result.AssistantReply, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.Tasks);
        Assert.Equal("Xây dựng API thống kê", result.Tasks[0].Title);
        Assert.Equal(2, result.ConversationHistory.Count);
        Assert.Empty(aiClient.CapturedPrompts); // Refused deterministically without wasting LLM tokens
    }

    [Fact]
    public async Task ExecutePipelineAsync_ExcelExportVsBuildExcelTask_ShouldClassifyCorrectly()
    {
        var (planningDb, deliveryDb, identityDb, orgId, projectId, actorId) = await SeedProjectAsync();
        await using var _p = planningDb;
        await using var _d = deliveryDb;
        await using var _i = identityDb;

        var aiClient = new CapturingAiClient(_ => JsonSerializer.Serialize(new
        {
            assistantReply = "Đã tạo task xây dựng tính năng xuất Excel theo yêu cầu.",
            recommendedTarget = "sprint",
            sprintGoal = "Triển khai tính năng xuất báo cáo Excel",
            tasks = new[]
            {
                new { id = "t1", title = "Xây dựng API xuất báo cáo Excel", description = "Export .xlsx", type = "Task", priority = "High", storyPoints = 3, estimatedHours = 8 }
            }
        }));
        var service = CreateService(aiClient, planningDb, deliveryDb, identityDb);
        var scope = new ProjectScopeContext(orgId, projectId, actorId);

        // 1. Export command triggers export_excel immediately without regenerating tasks
        AiSprintProposalDto? exportResult = null;
        await service.ExecutePipelineAsync(
            sessionId: "sprint_ai_excel",
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: string.Empty,
            userPrompt: "Xuất file excel cho dự án này giúp tôi",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { exportResult = p; return Task.CompletedTask; });

        Assert.NotNull(exportResult);
        Assert.Equal("export_excel", exportResult.Intent);
        Assert.Equal("export_excel", exportResult.TriggerAction);
        Assert.Empty(aiClient.CapturedPrompts);

        // 2. Request to build an Excel feature triggers normal Sprint task decomposition
        AiSprintProposalDto? featureResult = null;
        await service.ExecutePipelineAsync(
            sessionId: "sprint_ai_excel_feature",
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: string.Empty,
            userPrompt: "Tạo task xây dựng tính năng xuất báo cáo Excel cho Sprint tới",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { featureResult = p; return Task.CompletedTask; });

        Assert.NotNull(featureResult);
        Assert.Equal("sprint_plan", featureResult.Intent);
        Assert.Equal("none", featureResult.TriggerAction);
        Assert.Single(featureResult.Tasks);
        Assert.Single(aiClient.CapturedPrompts);
    }

    [Fact]
    public async Task ExecutePipelineAsync_StatefulMultiTurnConversation_ShouldRetainRedisMemoryAndTriggerBacklogPush()
    {
        var (planningDb, deliveryDb, identityDb, orgId, projectId, actorId) = await SeedProjectAsync();
        await using var _p = planningDb;
        await using var _d = deliveryDb;
        await using var _i = identityDb;

        var turnCallCount = 0;
        var aiClient = new CapturingAiClient(_ =>
        {
            turnCallCount++;
            return JsonSerializer.Serialize(new
            {
                assistantReply = turnCallCount == 1
                    ? "Tôi đã lập 2 công việc cho kho Backlog."
                    : "Tôi đã cập nhật độ ưu tiên lên Urgent theo yêu cầu của bạn.",
                recommendedTarget = "backlog",
                sprintGoal = "Bổ sung hạng mục Audit Log vào kho Backlog",
                tasks = new[]
                {
                    new
                    {
                        id = "task_01",
                        title = "Thiết kế bảng và service lưu Audit Log",
                        description = "Ghi nhận nhật ký thay đổi công việc",
                        type = "Task",
                        priority = turnCallCount == 1 ? "High" : "Urgent",
                        storyPoints = 5,
                        estimatedHours = 12,
                        subtasks = new[]
                        {
                            new { id = "sub_01", title = "Migration bảng AuditLogs", role = "Backend", estimatedHours = 4, storyPoints = 2 }
                        }
                    }
                }
            });
        });

        var staging = new MemoryStateStagingService();
        var service = CreateService(aiClient, planningDb, deliveryDb, identityDb);
        var scope = new ProjectScopeContext(orgId, projectId, actorId);
        const string sessionId = "sprint_ai_multi_turn";

        // Turn 1: Plan into Backlog
        AiSprintProposalDto? turn1 = null;
        await service.ExecutePipelineAsync(
            sessionId: sessionId,
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: "Cần thêm module Audit Log để truy vết thao tác.",
            userPrompt: "Lập công việc rồi đẩy vào kho backlog giúp tôi",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: async p => { await staging.SaveProposalAsync(sessionId, p, TimeSpan.FromHours(1)); turn1 = p; });

        Assert.NotNull(turn1);
        Assert.Equal("backlog_plan", turn1.Intent);
        Assert.Equal("backlog", turn1.RecommendedTarget);
        Assert.Equal(2, turn1.ConversationHistory.Count);

        // Turn 2: Conversational refinement reading prior Redis session
        var stagedAfterT1 = await staging.GetProposalAsync<AiSprintProposalDto>(sessionId);
        AiSprintProposalDto? turn2 = null;
        await service.ExecutePipelineAsync(
            sessionId: sessionId,
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: string.Empty,
            userPrompt: "Đổi độ ưu tiên task đầu tiên thành Urgent giúp tôi",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: async p => { await staging.SaveProposalAsync(sessionId, p, TimeSpan.FromHours(1)); turn2 = p; },
            existingProposal: stagedAfterT1);

        Assert.NotNull(turn2);
        Assert.Equal("refine_proposal", turn2.Intent);
        Assert.Equal("backlog", turn2.RecommendedTarget);
        Assert.Equal("Urgent", Assert.Single(turn2.Tasks).Priority);
        Assert.Equal(4, turn2.ConversationHistory.Count);

        // Prompt 2 must have received the Turn 1 conversation history & staged tasks from Redis
        var turn2PromptText = string.Join("\n", aiClient.CapturedPrompts[1].Messages.Select(m => m.Content));
        Assert.Contains("LỊCH SỬ ĐOẠN HỘI THOẠI TRƯỚC ĐÓ", turn2PromptText);
        Assert.Contains("Thiết kế bảng và service lưu Audit Log", turn2PromptText);

        // Turn 3: Direct conversational command to push staged proposal into Product Backlog
        var stagedAfterT2 = await staging.GetProposalAsync<AiSprintProposalDto>(sessionId);
        AiSprintProposalDto? turn3 = null;
        await service.ExecutePipelineAsync(
            sessionId: sessionId,
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: string.Empty,
            userPrompt: "Ok đẩy vào kho backlog ngay",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { turn3 = p; return Task.CompletedTask; },
            existingProposal: stagedAfterT2);

        Assert.NotNull(turn3);
        Assert.Equal("push_backlog", turn3.Intent);
        Assert.Equal("push_backlog", turn3.TriggerAction);
        Assert.Equal(6, turn3.ConversationHistory.Count);
    }

    [Fact]
    public async Task ApplySprintProposalAsync_WithPushToBacklog_ShouldCreateBacklogIssuesWithNullSprintIdAndNoSprint()
    {
        var (planningDb, deliveryDb, _, orgId, projectId, actorId) = await SeedProjectAsync();
        await using var _p = planningDb;
        await using var _d = deliveryDb;

        var coordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            new MemoryStateStagingService(),
            new NoOpUserLookupService(),
            NullLogger<AiSprintCommitCoordinator>.Instance);

        var scope = new ProjectScopeContext(orgId, projectId, actorId);
        var request = new AiApplySprintProposalRequest
        {
            SessionId = "sprint_ai_backlog_commit",
            ProjectId = projectId,
            PushToBacklog = true,
            SprintGoal = string.Empty, // Empty goal is valid when pushing into Product Backlog
            Tasks =
            [
                new ProposedTaskDto
                {
                    Id = "task_bl_1",
                    Title = "Xây dựng API xuất báo cáo tiến độ PDF",
                    Description = "Cho phép PM xuất báo cáo Sprint ra PDF",
                    Type = "Story",
                    Priority = "High",
                    Status = "To Do",
                    StoryPoints = 5,
                    EstimatedHours = 12,
                    AcceptanceCriteria = ["File PDF hiển thị đầy đủ bảng Sprint"],
                    Subtasks =
                    [
                        new ProposedSubTaskDto
                        {
                            Id = "sub_bl_1",
                            Title = "[Backend] Endpoint xuất PDF",
                            Role = "Backend",
                            Priority = "High",
                            Status = "To Do",
                            StoryPoints = 2,
                            EstimatedHours = 6,
                            AcceptanceCriteria = ["HTTP 200 application/pdf"]
                        }
                    ]
                }
            ]
        };

        var response = await coordinator.ApplySprintProposalAsync(request, scope);

        Assert.True(response.Success);
        Assert.Equal(Guid.Empty, response.SprintId);
        Assert.Equal("Product Backlog", response.SprintName);
        Assert.Equal(1, response.CreatedTaskCount);
        Assert.Equal(1, response.CreatedSubtaskCount);
        Assert.Empty(await planningDb.Sprints.ToListAsync());

        var createdIssues = await deliveryDb.Issues.Where(i => i.ProjectId == projectId).ToListAsync();
        Assert.Equal(2, createdIssues.Count);
        Assert.All(createdIssues, issue => Assert.Null(issue.SprintId));
    }
}
