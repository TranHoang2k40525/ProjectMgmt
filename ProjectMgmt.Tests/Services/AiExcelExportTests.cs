using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Domain.Entities;
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
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

namespace ProjectMgmt.Tests.Services;

public class AiExcelExportTests
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

    private sealed record SeededWorkspace(
        TestPlanningDbContext PlanningDb,
        TestDeliveryDbContext DeliveryDb,
        TestIdentityDbContext IdentityDb,
        Guid OrgId,
        Guid ProjectId,
        Guid ActorId,
        Guid MemberAnId,
        Guid Sprint1Id,
        Guid Sprint2EmptyId,
        Guid Sprint3Id);

    private static async Task<SeededWorkspace> SeedWorkspaceAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Exp_Plan_{suffix}").Options);
        var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Exp_Del_{suffix}").Options);
        var identityDb = new TestIdentityDbContext(new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Exp_Id_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var memberAnId = Guid.NewGuid();

        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Hệ thống QLDA",
            ProjectKey = "QLDA",
            IssueCounter = 10,
            LeadUserId = actorId
        });

        var storyTypeId = Guid.NewGuid();
        var taskTypeId = Guid.NewGuid();
        var bugTypeId = Guid.NewGuid();
        var subtaskTypeId = Guid.NewGuid();
        planningDb.IssueTypes.AddRange(
            new IssueType { Id = storyTypeId, ProjectId = projectId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = taskTypeId, ProjectId = projectId, Name = "Task", OrderIndex = 2 },
            new IssueType { Id = bugTypeId, ProjectId = projectId, Name = "Bug", OrderIndex = 3 },
            new IssueType { Id = subtaskTypeId, ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 4 }
        );

        var todoStatusId = Guid.NewGuid();
        var inProgStatusId = Guid.NewGuid();
        var doneStatusId = Guid.NewGuid();
        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = todoStatusId, ProjectId = projectId, Name = "To Do", Category = "ToDo", ColorHex = "#64748B", IsInitial = true, OrderIndex = 1 },
            new WorkflowStatus { Id = inProgStatusId, ProjectId = projectId, Name = "In Progress", Category = "InProgress", ColorHex = "#2563EB", IsActiveWork = true, OrderIndex = 2 },
            new WorkflowStatus { Id = doneStatusId, ProjectId = projectId, Name = "Done", Category = "Done", ColorHex = "#16A34A", IsDone = true, OrderIndex = 3 }
        );

        var urgentPrioId = Guid.NewGuid();
        var highPrioId = Guid.NewGuid();
        var mediumPrioId = Guid.NewGuid();
        planningDb.Priorities.AddRange(
            new Priority { Id = urgentPrioId, ProjectId = projectId, Name = "Urgent", Level = 1, ColorHex = "#DC2626" },
            new Priority { Id = highPrioId, ProjectId = projectId, Name = "High", Level = 2, ColorHex = "#F59E0B" },
            new Priority { Id = mediumPrioId, ProjectId = projectId, Name = "Medium", Level = 3, ColorHex = "#3B82F6" }
        );

        var sprint1Id = Guid.NewGuid();
        var sprint2EmptyId = Guid.NewGuid();
        var sprint3Id = Guid.NewGuid();
        planningDb.Sprints.AddRange(
            new Sprint { Id = sprint1Id, ProjectId = projectId, Name = "Sprint 1", Status = "Completed", OrderIndex = 1 },
            new Sprint { Id = sprint2EmptyId, ProjectId = projectId, Name = "Sprint 2", Status = "Planned", OrderIndex = 2 },
            new Sprint { Id = sprint3Id, ProjectId = projectId, Name = "Sprint 3", Status = "Active", OrderIndex = 3 }
        );

        var roleId = Guid.NewGuid();
        identityDb.Roles.Add(new Role { Id = roleId, Name = "Developer", Scope = "Project" });
        identityDb.Users.AddRange(
            new User { Id = actorId, Email = "lead@example.com", NormalizedEmail = "LEAD@EXAMPLE.COM", PasswordHash = "H", SecurityStamp = Guid.NewGuid(), IsActive = true, IsEmailVerified = true },
            new User { Id = memberAnId, Email = "an.nv@example.com", NormalizedEmail = "AN.NV@EXAMPLE.COM", PasswordHash = "H", SecurityStamp = Guid.NewGuid(), IsActive = true, IsEmailVerified = true }
        );
        identityDb.UserProfiles.AddRange(
            new UserProfile { Id = Guid.NewGuid(), UserId = actorId, DisplayName = "Trần Quản Lý", JobTitle = "Project Lead" },
            new UserProfile { Id = Guid.NewGuid(), UserId = memberAnId, DisplayName = "Nguyễn Văn An", JobTitle = "Backend Dev" }
        );
        identityDb.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = memberAnId, RoleId = roleId, ScopeType = "Project", ScopeId = projectId });

        var qlda1Id = Guid.NewGuid();
        var qlda2Id = Guid.NewGuid();
        var qlda3SubId = Guid.NewGuid();
        var qlda4BugId = Guid.NewGuid();
        var qlda5BacklogId = Guid.NewGuid();

        deliveryDb.Issues.AddRange(
            new Issue
            {
                Id = qlda1Id,
                ProjectId = projectId,
                IssueNumber = 1,
                SprintId = sprint3Id,
                StatusId = inProgStatusId,
                IssueTypeId = storyTypeId,
                PriorityId = highPrioId,
                AssigneeId = memberAnId,
                Title = "Xây dựng API Xuất Báo Cáo",
                Description = "Cho phép xuất file bảng sạch theo phạm vi.",
                StoryPoints = 5,
                RankOrder = 100
            },
            new Issue
            {
                Id = qlda3SubId,
                ProjectId = projectId,
                IssueNumber = 3,
                ParentId = qlda1Id,
                SprintId = sprint3Id,
                StatusId = todoStatusId,
                IssueTypeId = subtaskTypeId,
                PriorityId = highPrioId,
                AssigneeId = memberAnId,
                Title = "[BE] Tạo bộ lọc dữ liệu Sprint",
                StoryPoints = 2,
                RankOrder = 101
            },
            new Issue
            {
                Id = qlda2Id,
                ProjectId = projectId,
                IssueNumber = 2,
                SprintId = sprint1Id,
                StatusId = doneStatusId,
                IssueTypeId = taskTypeId,
                PriorityId = mediumPrioId,
                Title = "Khởi tạo hạ tầng cơ sở dữ liệu",
                StoryPoints = 3,
                RankOrder = 50
            },
            new Issue
            {
                Id = qlda4BugId,
                ProjectId = projectId,
                IssueNumber = 4,
                SprintId = sprint3Id,
                StatusId = inProgStatusId,
                IssueTypeId = bugTypeId,
                PriorityId = urgentPrioId,
                AssigneeId = memberAnId,
                IsBlocked = true,
                BlockReason = "Chờ mở cổng kết nối Sandbox",
                Title = "Lỗi sai lệch tổng điểm Sprint",
                StoryPoints = 2,
                RankOrder = 102
            },
            new Issue
            {
                Id = qlda5BacklogId,
                ProjectId = projectId,
                IssueNumber = 5,
                SprintId = null,
                StatusId = todoStatusId,
                IssueTypeId = storyTypeId,
                PriorityId = mediumPrioId,
                Title = "Tối ưu hóa trải nghiệm tìm kiếm tài liệu",
                StoryPoints = 5,
                RankOrder = 200
            }
        );

        await planningDb.SaveChangesAsync();
        await identityDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();

        return new SeededWorkspace(planningDb, deliveryDb, identityDb, orgId, projectId, actorId, memberAnId, sprint1Id, sprint2EmptyId, sprint3Id);
    }

    private static AiSprintDecompositionService CreateService(CapturingAiClient aiClient, SeededWorkspace ws)
        => new(
            aiClient,
            new NoOpVectorSearch(),
            new NoOpTraceService(),
            ws.PlanningDb,
            new SpecHubTools(ws.PlanningDb, new NoOpSpecHubStorage(), NullLogger<SpecHubTools>.Instance),
            new BacklogTools(ws.DeliveryDb, ws.PlanningDb),
            new SprintTools(ws.PlanningDb, ws.DeliveryDb),
            new TeamTools(ws.PlanningDb, ws.IdentityDb),
            new PromptTemplateService(NullLogger<PromptTemplateService>.Instance),
            NullLogger<AiSprintDecompositionService>.Instance);

    private static async Task<AiSprintProposalDto> RunPromptAsync(
        AiSprintDecompositionService service,
        SeededWorkspace ws,
        string sessionId,
        string prompt,
        AiSprintProposalDto? existing = null)
    {
        AiSprintProposalDto? result = null;
        await service.ExecutePipelineAsync(
            sessionId: sessionId,
            scope: new ProjectScopeContext(ws.OrgId, ws.ProjectId, ws.ActorId),
            targetSprintId: null,
            extractedDocumentContent: string.Empty,
            userPrompt: prompt,
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { result = p; return Task.CompletedTask; },
            existingProposal: existing);

        Assert.NotNull(result);
        return result!;
    }

    [Fact]
    public async Task ExportExcel_SingleSprintAndSprintStatus_ShouldResolveExactScope()
    {
        var ws = await SeedWorkspaceAsync();
        await using var _p = ws.PlanningDb;
        await using var _d = ws.DeliveryDb;
        await using var _i = ws.IdentityDb;

        var aiClient = new CapturingAiClient(_ => "{}");
        var service = CreateService(aiClient, ws);

        // 1. Yêu cầu chỉ xuất Sprint 3
        var sprint3Result = await RunPromptAsync(service, ws, "exp_s3", "Xuất file Excel Sprint 3 giúp tôi");
        Assert.Equal("export_excel", sprint3Result.Intent);
        Assert.Equal("export_excel", sprint3Result.TriggerAction);
        Assert.NotNull(sprint3Result.ExportSpec);
        Assert.Equal("single_sprint", sprint3Result.ExportSpec!.Scope);
        Assert.Equal([ws.Sprint3Id], sprint3Result.ExportSpec.SprintIds);
        Assert.Equal(3, sprint3Result.ExportSpec.MatchedItemCount); // QLDA-1, QLDA-3, QLDA-4
        Assert.DoesNotContain("jira", sprint3Result.ExportSpec.SuggestedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(aiClient.CapturedPrompts);

        // 2. Yêu cầu xuất Sprint đang chạy -> khớp Sprint 3
        var activeResult = await RunPromptAsync(service, ws, "exp_active", "Xuất Excel sprint đang chạy");
        Assert.Equal("export_excel", activeResult.TriggerAction);
        Assert.Equal("single_sprint", activeResult.ExportSpec!.Scope);
        Assert.Equal([ws.Sprint3Id], activeResult.ExportSpec.SprintIds);

        // 3. Yêu cầu xuất Sprint đã hoàn thành -> khớp Sprint 1
        var completedResult = await RunPromptAsync(service, ws, "exp_done", "Xuất các sprint đã hoàn thành ra excel");
        Assert.Equal("export_excel", completedResult.TriggerAction);
        Assert.Equal("single_sprint", completedResult.ExportSpec!.Scope);
        Assert.Equal([ws.Sprint1Id], completedResult.ExportSpec.SprintIds);
        Assert.Equal(1, completedResult.ExportSpec.MatchedItemCount);
    }

    [Fact]
    public async Task ExportExcel_BacklogOnly_SingleTask_And_TaskDetail_ShouldResolveExactScope()
    {
        var ws = await SeedWorkspaceAsync();
        await using var _p = ws.PlanningDb;
        await using var _d = ws.DeliveryDb;
        await using var _i = ws.IdentityDb;

        var aiClient = new CapturingAiClient(_ => "{}");
        var service = CreateService(aiClient, ws);

        // 1. Chỉ xuất kho Product Backlog
        var backlogRes = await RunPromptAsync(service, ws, "exp_bl", "Xuất mỗi kho Product Backlog ra Excel thôi");
        Assert.Equal("export_excel", backlogRes.TriggerAction);
        Assert.Equal("backlog_only", backlogRes.ExportSpec!.Scope);
        Assert.Equal(1, backlogRes.ExportSpec.MatchedItemCount); // QLDA-5

        // 2. Chỉ xuất 1 task
        var singleTaskRes = await RunPromptAsync(service, ws, "exp_one", "Xuất chỉ 1 task QLDA-2 ra file Excel");
        Assert.Equal("export_excel", singleTaskRes.TriggerAction);
        Assert.Equal("single_task", singleTaskRes.ExportSpec!.Scope);
        Assert.Equal(["QLDA-2"], singleTaskRes.ExportSpec.IssueKeys);

        // 3. Xuất nội dung chi tiết 1 task
        var detailRes = await RunPromptAsync(service, ws, "exp_detail", "Xuất toàn bộ nội dung chi tiết của task QLDA-1 ra Excel");
        Assert.Equal("export_excel", detailRes.TriggerAction);
        Assert.Equal("task_detail", detailRes.ExportSpec!.Scope);
        Assert.Equal(["QLDA-1"], detailRes.ExportSpec.IssueKeys);
    }

    [Fact]
    public async Task ExportExcel_FilteredTasks_And_DeicticTurnHistory_ShouldPersistPerTurnSpec()
    {
        var ws = await SeedWorkspaceAsync();
        await using var _p = ws.PlanningDb;
        await using var _d = ws.DeliveryDb;
        await using var _i = ws.IdentityDb;

        var aiClient = new CapturingAiClient(_ => "{}");
        var service = CreateService(aiClient, ws);

        // Lượt 1: Lọc đa tiêu chí (Bug + bị chặn + Nguyễn Văn An)
        var filteredRes = await RunPromptAsync(service, ws, "exp_multi", "Xuất các Bug đang bị chặn của Nguyễn Văn An ra Excel");
        Assert.Equal("export_excel", filteredRes.TriggerAction);
        Assert.Equal("filtered_tasks", filteredRes.ExportSpec!.Scope);
        Assert.True(filteredRes.ExportSpec.BlockedOnly);
        Assert.Contains("Nguyễn Văn An", filteredRes.ExportSpec.AssigneeNames);
        Assert.Contains("Bug", filteredRes.ExportSpec.IssueTypes);
        Assert.Equal(1, filteredRes.ExportSpec.MatchedItemCount);

        // Lượt 2: User nhắc QLDA-2 trong lượt hỏi, sau đó hỏi quy chiếu "Xuất nội dung chi tiết task đó"
        filteredRes.ConversationHistory.Add(new AiConversationTurnDto
        {
            Role = "assistant",
            Content = "Công việc QLDA-2 (Khởi tạo hạ tầng cơ sở dữ liệu) thuộc Sprint 1 đã Done."
        });

        var deicticRes = await RunPromptAsync(service, ws, "exp_multi", "Xuất nội dung chi tiết task đó ra Excel", filteredRes);
        Assert.Equal("export_excel", deicticRes.TriggerAction);
        Assert.Equal("task_detail", deicticRes.ExportSpec!.Scope);
        Assert.Equal(["QLDA-2"], deicticRes.ExportSpec.IssueKeys);

        // Kiểm tra ExportSpec được lưu tách biệt theo từng turn trong ConversationHistory (không ghi đè turn cũ)
        var exportSpecsInHistory = deicticRes.ConversationHistory
            .Where(t => t.ExportSpec != null)
            .Select(t => t.ExportSpec!.Scope)
            .ToList();
        Assert.Equal(["filtered_tasks", "task_detail"], exportSpecsInHistory);
    }

    [Fact]
    public async Task ExportExcel_AiProposalAndSingleDraftTask_ShouldResolveWithoutOverwritingDraft()
    {
        var ws = await SeedWorkspaceAsync();
        await using var _p = ws.PlanningDb;
        await using var _d = ws.DeliveryDb;
        await using var _i = ws.IdentityDb;

        var aiClient = new CapturingAiClient(_ => "{}");
        var service = CreateService(aiClient, ws);

        var draft = new AiSprintProposalDto
        {
            SessionId = "exp_draft",
            ProjectId = ws.ProjectId,
            SprintGoal = "Bảng đề xuất nháp Sprint 4",
            TotalEstimatedHours = 12,
            TotalStoryPoints = 5,
            Tasks =
            [
                new ProposedTaskDto
                {
                    Id = "task_01",
                    Title = "Tích hợp thanh toán QR",
                    Description = "Kết nối cổng thanh toán VietQR.",
                    Type = "Story",
                    Priority = "High",
                    Status = "To Do",
                    StoryPoints = 5,
                    EstimatedHours = 12
                }
            ]
        };

        var res = await RunPromptAsync(service, ws, "exp_draft", "Xuất nội dung chi tiết task đầu tiên trong bảng đề xuất AI ra Excel", draft);
        Assert.Equal("export_excel", res.Intent);
        Assert.Equal("export_excel", res.TriggerAction);
        Assert.Equal("task_detail", res.ExportSpec!.Scope);
        Assert.Equal(["task_01"], res.ExportSpec.IssueKeys);
        Assert.Equal("ai_proposal", res.ExportSpec.SearchKeyword);
        Assert.Single(res.Tasks);
    }

    [Fact]
    public async Task ExportExcel_NonExistentEntitiesOrEmptyNarrowScope_ShouldRefuseExportHonestly()
    {
        var ws = await SeedWorkspaceAsync();
        await using var _p = ws.PlanningDb;
        await using var _d = ws.DeliveryDb;
        await using var _i = ws.IdentityDb;

        var aiClient = new CapturingAiClient(_ => "{}");
        var service = CreateService(aiClient, ws);

        // Sprint không tồn tại
        var noSprint = await RunPromptAsync(service, ws, "exp_none_1", "Xuất file Excel Sprint 99 giúp tôi");
        Assert.Equal("export_excel", noSprint.Intent);
        Assert.Equal("none", noSprint.TriggerAction);
        Assert.Null(noSprint.ExportSpec);
        Assert.Contains("Sprint 99", noSprint.AssistantReply);

        // Sprint có thật nhưng trống 0 công việc (Sprint 2)
        var emptySprint = await RunPromptAsync(service, ws, "exp_none_2", "Xuất Sprint 2 ra Excel");
        Assert.Equal("none", emptySprint.TriggerAction);
        Assert.Null(emptySprint.ExportSpec);
        Assert.Contains("0 công việc", emptySprint.AssistantReply);

        // Mã task không tồn tại
        var noTask = await RunPromptAsync(service, ws, "exp_none_3", "Xuất chi tiết task QLDA-999 ra file excel");
        Assert.Equal("none", noTask.TriggerAction);
        Assert.Null(noTask.ExportSpec);
        Assert.Contains("QLDA-999", noTask.AssistantReply);

        // Thành viên không thuộc dự án
        var noMember = await RunPromptAsync(service, ws, "exp_none_4", "Xuất công việc của nhân viên Người Lạ Không Tồn Tại ra Excel");
        Assert.Equal("none", noMember.TriggerAction);
        Assert.Null(noMember.ExportSpec);
    }

    [Fact]
    public async Task ClassifyIntent_ShouldNotMisclassifyCreateTaskHowToOrLongSrsExportPhrases()
    {
        var ws = await SeedWorkspaceAsync();
        await using var _p = ws.PlanningDb;
        await using var _d = ws.DeliveryDb;
        await using var _i = ws.IdentityDb;

        var aiClient = new CapturingAiClient(p =>
        {
            var lastMsg = p.Messages.LastOrDefault()?.Content ?? string.Empty;
            if (lastMsg.Contains("Làm sao", StringComparison.OrdinalIgnoreCase))
            {
                return "Bạn có thể bấm nút Xuất Excel trên thanh công cụ.";
            }

            if (lastMsg.Contains("PDF", StringComparison.OrdinalIgnoreCase))
            {
                return "Hệ thống hiện hỗ trợ xuất tệp Excel (.xlsx) chuẩn ProjectMgmt theo phạm vi Sprint/Backlog.";
            }

            return JsonSerializer.Serialize(new
            {
                assistantReply = "Đã phân rã task theo tài liệu.",
                recommendedTarget = "sprint",
                sprintGoal = "Triển khai xuất báo cáo",
                tasks = new[]
                {
                    new { id = "t1", title = "Phát triển API xuất Excel", description = "Export", type = "Task", priority = "High", storyPoints = 3, estimatedHours = 6 }
                }
            });
        });
        var service = CreateService(aiClient, ws);

        // 1. Tạo task xây dựng chức năng xuất Excel -> không được kích hoạt tải file
        var buildTask = await RunPromptAsync(service, ws, "cls_1", "Tạo task xuất Excel theo Sprint 3 cho dev thực hiện");
        Assert.Equal("sprint_plan", buildTask.Intent);
        Assert.Equal("none", buildTask.TriggerAction);

        // 2. Câu hỏi cách làm -> project_qa
        var howTo = await RunPromptAsync(service, ws, "cls_2", "Làm sao để xuất file Excel trên hệ thống?");
        Assert.Equal("project_qa", howTo.Intent);
        Assert.Equal("none", howTo.TriggerAction);

        // 3. Đề nghị xuất định dạng không hỗ trợ (PDF) -> project_qa giải thích thật, không lén xuất Excel
        var askPdf = await RunPromptAsync(service, ws, "cls_3", "Xuất báo cáo Sprint 3 ra file PDF giúp tôi");
        Assert.Equal("project_qa", askPdf.Intent);
        Assert.Equal("none", askPdf.TriggerAction);
        Assert.Contains("Excel", askPdf.AssistantReply);

        // 4. Tài liệu SRS dài (>500 ký tự) nhắc tới từ "xuất file excel" nhưng prompt lập kế hoạch Sprint -> vẫn phải phân rã Sprint
        AiSprintProposalDto? srsResult = null;
        var longSrs = new string('A', 460) + " Hệ thống cần hỗ trợ tính năng xuất file excel cho báo cáo cuối kỳ.";
        await service.ExecutePipelineAsync(
            sessionId: "cls_4",
            scope: new ProjectScopeContext(ws.OrgId, ws.ProjectId, ws.ActorId),
            targetSprintId: null,
            extractedDocumentContent: longSrs,
            userPrompt: "Phân tích tài liệu dự án và lập kế hoạch Sprint tiếp theo.",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { srsResult = p; return Task.CompletedTask; });

        Assert.NotNull(srsResult);
        Assert.Equal("sprint_plan", srsResult!.Intent);
        Assert.Equal("none", srsResult.TriggerAction);
    }
}
