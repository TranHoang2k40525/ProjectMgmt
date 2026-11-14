using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.Core.Tracing;
using ProjectMgmt.Core.VectorSearch;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using Planning.Infrastructure.Services;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

namespace ProjectMgmt.Tests.Services;

public class AiActiveConfigAndBacklogMoveTests
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
            await onChunk(new AiStreamChunk
            {
                ChunkType = AiStreamChunkType.Content,
                DeltaContent = responseFactory(prompt)
            });
            await onChunk(new AiStreamChunk
            {
                ChunkType = AiStreamChunkType.Done,
                FinishReason = "stop"
            });
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
        public Task<XlsxPreviewDto> ExtractXlsxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(new XlsxPreviewDto("spec.xlsx", new List<XlsxSheetDto>()));
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

    [Fact]
    public async Task ProjectActiveConfigResolver_And_McpTool_ShouldReturnAppliedConfigOnly_CustomVsSystemDefaultPriorities()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Cfg_Plan_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Cfg_Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectCustomId = Guid.NewGuid();
        var projectDefaultPrioId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        planningDb.Projects.AddRange(
            new Project { Id = projectCustomId, OrgId = orgId, Name = "Custom Workflow Project", ProjectKey = "CWF", LeadUserId = actorId },
            new Project { Id = projectDefaultPrioId, OrgId = orgId, Name = "Default Prio Project", ProjectKey = "DPP", LeadUserId = actorId }
        );

        // System global priorities (ProjectId == null)
        planningDb.Priorities.AddRange(
            new Priority { Id = Guid.NewGuid(), ProjectId = null, Name = "Highest", Level = 1, ColorHex = "#E5493A", IconKey = "priority_high" },
            new Priority { Id = Guid.NewGuid(), ProjectId = null, Name = "Medium", Level = 3, ColorHex = "#E97F33", IconKey = "drag_handle" },
            new Priority { Id = Guid.NewGuid(), ProjectId = null, Name = "Lowest", Level = 5, ColorHex = "#2D8738", IconKey = "arrow_downward" }
        );

        // Project-specific applied priorities for projectCustomId ONLY
        planningDb.Priorities.AddRange(
            new Priority { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Blocker", Level = 1, ColorHex = "#DC2626", IconKey = "block" },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Normal", Level = 2, ColorHex = "#2563EB", IconKey = "remove" }
        );

        var stOpen = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Open", Category = "ToDo", ColorHex = "#42526E", IsInitial = true, OrderIndex = 1 };
        var stDev = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "In Dev", Category = "InProgress", ColorHex = "#0052CC", IsActiveWork = true, OrderIndex = 2 };
        var stRel = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Released", Category = "Done", ColorHex = "#00875A", IsDone = true, OrderIndex = 3 };
        planningDb.WorkflowStatuses.AddRange(stOpen, stDev, stRel);

        planningDb.WorkflowTransitions.Add(new WorkflowTransition
        {
            Id = Guid.NewGuid(),
            ProjectId = projectCustomId,
            FromStatusId = stOpen.Id,
            ToStatusId = stDev.Id,
            Name = "Start Coding"
        });

        var board = new Board { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Bảng DevOps Custom", Type = "Scrum", IsDefault = true };
        planningDb.Boards.Add(board);
        planningDb.BoardColumns.AddRange(
            new BoardColumn { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Open", StatusId = stOpen.Id, OrderIndex = 1 },
            new BoardColumn { Id = Guid.NewGuid(), BoardId = board.Id, Name = "In Dev", StatusId = stDev.Id, OrderIndex = 2, WipLimit = 5 },
            new BoardColumn { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Released", StatusId = stRel.Id, OrderIndex = 3 }
        );

        planningDb.IssueTypes.AddRange(
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Task", OrderIndex = 2 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Sub-task", IsSubtask = true, OrderIndex = 3 }
        );

        deliveryDb.Labels.AddRange(
            new Label { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "Security", ColorHex = "#DC2626" },
            new Label { Id = Guid.NewGuid(), ProjectId = projectCustomId, Name = "SignalR", ColorHex = "#0052CC" }
        );

        await planningDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();

        var backlogTools = new BacklogTools(deliveryDb, planningDb);
        var customJson = await backlogTools.GetActiveConfigAsync(projectCustomId, new ProjectScopeContext(orgId, projectCustomId, actorId));
        using var doc = JsonDocument.Parse(customJson);

        var workflowElem = doc.RootElement.GetProperty("Workflow");
        Assert.Equal("DevOps Custom", workflowElem.GetProperty("Name").GetString());
        Assert.Equal("Open", workflowElem.GetProperty("InitialStatus").GetString());

        var priosElem = doc.RootElement.GetProperty("Priorities");
        Assert.True(priosElem.GetProperty("IsCustomProjectConfiguration").GetBoolean());

        var prios = priosElem.GetProperty("Items").EnumerateArray().Select(x => x.GetProperty("Name").GetString()).ToList();
        Assert.Equal(["Blocker", "Normal"], prios);
        Assert.DoesNotContain("Highest", prios);
        Assert.DoesNotContain("Medium", prios);

        var statuses = workflowElem.GetProperty("Statuses").EnumerateArray().Select(x => x.GetProperty("Name").GetString()).ToList();
        Assert.Equal(["Open", "In Dev", "Released"], statuses);

        var transitions = workflowElem.GetProperty("Transitions").EnumerateArray().ToList();
        Assert.Single(transitions);
        Assert.Equal("Open", transitions[0].GetProperty("FromStatus").GetString());
        Assert.Equal("In Dev", transitions[0].GetProperty("ToStatus").GetString());

        var labels = doc.RootElement.GetProperty("Labels").EnumerateArray().Select(x => x.GetProperty("Name").GetString()).ToList();
        Assert.Equal(["Security", "SignalR"], labels);

        // Project without custom priorities falls back cleanly to system defaults
        var defaultConfig = await ProjectActiveConfigResolver.ResolveAsync(planningDb, deliveryDb, projectDefaultPrioId);
        Assert.False(defaultConfig.HasCustomPriorities);
        Assert.Equal(["Highest", "Medium", "Lowest"], defaultConfig.Priorities.Select(p => p.Name).ToList());
    }

    [Fact]
    public async Task ExecutePipelineAsync_WithNonStandardWorkflowAndBacklogStories_ShouldInjectActiveConfigAndDefaultToCustomInitialStatus()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Decomp_Plan_{suffix}").Options);
        await using var identityDb = new TestIdentityDbContext(new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Decomp_Id_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Decomp_Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var storyTypeId = Guid.NewGuid();
        var taskTypeId = Guid.NewGuid();
        var subtaskTypeId = Guid.NewGuid();
        var openStatusId = Guid.NewGuid();
        var inDevStatusId = Guid.NewGuid();
        var qaStatusId = Guid.NewGuid();
        var releasedStatusId = Guid.NewGuid();
        var blockerPrioId = Guid.NewGuid();
        var majorPrioId = Guid.NewGuid();
        var realtimeLabelId = Guid.NewGuid();

        planningDb.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Hệ thống QLDA", ProjectKey = "QLDA", IssueCounter = 50, LeadUserId = leadId });
        planningDb.IssueTypes.AddRange(
            new IssueType { Id = storyTypeId, ProjectId = projectId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = taskTypeId, ProjectId = projectId, Name = "Task", OrderIndex = 2 },
            new IssueType { Id = subtaskTypeId, ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 3 }
        );
        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = openStatusId, ProjectId = projectId, Name = "Open", Category = "ToDo", ColorHex = "#42526E", IsInitial = true, OrderIndex = 1 },
            new WorkflowStatus { Id = inDevStatusId, ProjectId = projectId, Name = "In Dev", Category = "InProgress", ColorHex = "#0052CC", IsActiveWork = true, OrderIndex = 2 },
            new WorkflowStatus { Id = qaStatusId, ProjectId = projectId, Name = "QA Testing", Category = "InProgress", ColorHex = "#6554C0", OrderIndex = 3 },
            new WorkflowStatus { Id = releasedStatusId, ProjectId = projectId, Name = "Released", Category = "Done", ColorHex = "#00875A", IsDone = true, OrderIndex = 4 }
        );
        planningDb.WorkflowTransitions.Add(new WorkflowTransition
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            FromStatusId = openStatusId,
            ToStatusId = inDevStatusId,
            Name = "Start Dev"
        });
        planningDb.Priorities.AddRange(
            new Priority { Id = blockerPrioId, ProjectId = projectId, Name = "Blocker", Level = 1, ColorHex = "#DC2626" },
            new Priority { Id = majorPrioId, ProjectId = projectId, Name = "Major", Level = 2, ColorHex = "#F59E0B" },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Normal", Level = 3, ColorHex = "#3B82F6" }
        );

        var backlogStoryId = Guid.NewGuid();
        deliveryDb.Labels.Add(new Label { Id = realtimeLabelId, ProjectId = projectId, Name = "Realtime", ColorHex = "#0052CC" });
        deliveryDb.Issues.Add(new Issue
        {
            Id = backlogStoryId,
            ProjectId = projectId,
            IssueNumber = 42,
            SprintId = null,
            StatusId = openStatusId,
            IssueTypeId = storyTypeId,
            PriorityId = majorPrioId,
            Title = "Cho phép PO kéo thẻ Kanban giữa các cột realtime",
            Description = "Nhu cầu người dùng từ Product Backlog",
            StoryPoints = 5,
            EstimatedHours = 12,
            IsDeleted = false
        });
        deliveryDb.IssueLabels.Add(new IssueLabel { IssueId = backlogStoryId, LabelId = realtimeLabelId });

        identityDb.Users.Add(new User { Id = leadId, Email = "dev@huce.edu.vn", NormalizedEmail = "DEV@HUCE.EDU.VN", PasswordHash = "H", SecurityStamp = Guid.NewGuid(), IsActive = true, IsEmailVerified = true });
        identityDb.UserProfiles.Add(new UserProfile { Id = Guid.NewGuid(), UserId = leadId, DisplayName = "Nguyễn Văn A", JobTitle = "Backend Dev", Bio = "DotNet" });

        await planningDb.SaveChangesAsync();
        await identityDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();

        // AI omits "status" to verify parser initializes to activeConfig.InitialStatusName ("Open"), never "To Do"
        var aiJson = JsonSerializer.Serialize(new
        {
            sprintGoal = "Đưa User Story QLDA-42 vào Sprint theo quy trình Open -> In Dev -> QA Testing -> Released",
            rationale = "Kéo Story QLDA-42 từ kho Backlog và gắn nhãn Realtime.",
            tasks = new[]
            {
                new
                {
                    id = "task_01",
                    sourceBacklogIssueId = backlogStoryId.ToString(),
                    sourceBacklogIssueKey = "QLDA-42",
                    epicTitle = "Realtime Kanban",
                    title = "Cho phép PO kéo thẻ Kanban giữa các cột realtime",
                    description = "Lấy từ Product Backlog QLDA-42 để triển khai",
                    type = "Story",
                    priority = "Blocker",
                    storyPoints = 5,
                    estimatedHours = 12,
                    assigneeName = "Nguyễn Văn A",
                    tags = new[] { "Realtime" },
                    acceptanceCriteria = new[] { "Sự kiện đổi cột phát dưới 200ms" },
                    subtasks = new[]
                    {
                        new { id = "sub_01", title = "[Backend] SignalR Hub", description = "Tạo Hub realtime", priority = "Blocker", storyPoints = 3, estimatedHours = 6, assigneeName = "Nguyễn Văn A", acceptanceCriteria = new[] { "Hub pass test" } }
                    }
                }
            }
        });

        var aiClient = new CapturingAiClient(_ => aiJson);
        var backlogTools = new BacklogTools(deliveryDb, planningDb);
        var scope = new ProjectScopeContext(orgId, projectId, leadId);

        // Verify enriched BacklogTools.GetStoriesAsync output first
        var storiesJson = await backlogTools.GetStoriesAsync(projectId, scope);
        using (var storiesDoc = JsonDocument.Parse(storiesJson))
        {
            var storyElem = storiesDoc.RootElement.EnumerateArray().Single();
            Assert.Equal("QLDA-42", storyElem.GetProperty("IssueKey").GetString());
            Assert.Equal("Story", storyElem.GetProperty("IssueType").GetString());
            Assert.True(storyElem.GetProperty("IsUserStory").GetBoolean());
            Assert.Equal("Major", storyElem.GetProperty("Priority").GetString());
            Assert.Contains("Realtime", storyElem.GetProperty("Labels").EnumerateArray().Select(x => x.GetString()));
        }

        var service = new AiSprintDecompositionService(
            aiClient,
            new NoOpVectorSearch(),
            new NoOpTraceService(),
            planningDb,
            new SpecHubTools(planningDb, new NoOpSpecHubStorage(), NullLogger<SpecHubTools>.Instance),
            backlogTools,
            new SprintTools(planningDb, deliveryDb),
            new TeamTools(planningDb, identityDb),
            new PromptTemplateService(NullLogger<PromptTemplateService>.Instance),
            NullLogger<AiSprintDecompositionService>.Instance);

        var invokedTools = new List<string>();
        AiSprintProposalDto? proposal = null;

        await service.ExecutePipelineAsync(
            sessionId: "cfg_session_01",
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: "Biên bản họp chọn Story QLDA-42 vào Sprint.",
            userPrompt: "Kéo Story từ Backlog vào Sprint",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (tool, _, _, _) => { invokedTools.Add(tool); return Task.CompletedTask; },
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { proposal = p; return Task.CompletedTask; });

        Assert.Contains("project_get_active_config", invokedTools);
        Assert.NotEmpty(aiClient.CapturedPrompts);

        var promptText = aiClient.CapturedPrompts[0].SystemInstruction + "\n" +
                         string.Join("\n", aiClient.CapturedPrompts[0].Messages.Select(m => m.Content));
        Assert.Contains("In Dev", promptText);
        Assert.Contains("QA Testing", promptText);
        Assert.Contains("Released", promptText);
        Assert.Contains("Blocker", promptText);
        Assert.Contains("Realtime", promptText);

        Assert.NotNull(proposal);
        Assert.Equal("Open", proposal.InitialStatus);
        Assert.Equal(["Open", "In Dev", "QA Testing", "Released"], proposal.AppliedWorkflowStatuses);
        Assert.Equal(["Blocker", "Major", "Normal"], proposal.AppliedPriorities);
        Assert.Contains("Realtime", proposal.AppliedLabels);

        var task = Assert.Single(proposal.Tasks);
        Assert.Equal("Open", task.Status);
        Assert.Equal("Blocker", task.Priority);
        Assert.Equal("QLDA-42", task.SourceBacklogIssueKey);
        Assert.Equal(backlogStoryId, task.SourceBacklogIssueId);
        Assert.Equal("Open", Assert.Single(task.Subtasks).Status);
    }

    [Fact]
    public async Task ApplySprintProposalAsync_ShouldMoveBacklogStoryWithoutDuplicating_PreservePriorityUrgent_SaveLabelsAndSubtaskCriteria()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Commit_Plan_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Commit_Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var storyTypeId = Guid.NewGuid();
        var taskTypeId = Guid.NewGuid();
        var epicTypeId = Guid.NewGuid();
        var subtaskTypeId = Guid.NewGuid();
        var openStatusId = Guid.NewGuid();
        var releasedStatusId = Guid.NewGuid();
        var urgentPrioId = Guid.NewGuid();
        var normalPrioId = Guid.NewGuid();
        var backendLabelId = Guid.NewGuid();

        planningDb.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "QLDA", ProjectKey = "QLDA", IssueCounter = 12, LeadUserId = actorId });
        planningDb.IssueTypes.AddRange(
            new IssueType { Id = storyTypeId, ProjectId = projectId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = taskTypeId, ProjectId = projectId, Name = "Task", OrderIndex = 2 },
            new IssueType { Id = epicTypeId, ProjectId = projectId, Name = "Epic", OrderIndex = 3 },
            new IssueType { Id = subtaskTypeId, ProjectId = projectId, Name = "Sub-task", IsSubtask = true, OrderIndex = 4 }
        );
        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = openStatusId, ProjectId = projectId, Name = "Open", Category = "ToDo", IsInitial = true, OrderIndex = 1 },
            new WorkflowStatus { Id = releasedStatusId, ProjectId = projectId, Name = "Released", Category = "Done", IsDone = true, OrderIndex = 2 }
        );
        // Custom project priority uses "Urgent" (Level 1), must NEVER be rewritten to "Highest"
        planningDb.Priorities.AddRange(
            new Priority { Id = urgentPrioId, ProjectId = projectId, Name = "Urgent", Level = 1, ColorHex = "#DC2626" },
            new Priority { Id = normalPrioId, ProjectId = projectId, Name = "Normal", Level = 2, ColorHex = "#3B82F6" }
        );

        var targetSprintId = Guid.NewGuid();
        var prevCompletedSprintId = Guid.NewGuid();
        planningDb.Sprints.AddRange(
            new Sprint { Id = prevCompletedSprintId, ProjectId = projectId, Name = "QLDA Sprint 1", Status = "Active", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new Sprint { Id = targetSprintId, ProjectId = projectId, Name = "QLDA Sprint 2", Status = "Planned" }
        );

        var backlogStoryId = Guid.NewGuid();
        var preExistingBacklogSubtaskId = Guid.NewGuid();
        var doneIssueInPrevSprintId = Guid.NewGuid();

        deliveryDb.Labels.Add(new Label { Id = backendLabelId, ProjectId = projectId, Name = "Backend", ColorHex = "#2563EB" });
        deliveryDb.Issues.AddRange(
            new Issue
            {
                Id = backlogStoryId,
                ProjectId = projectId,
                IssueNumber = 10,
                SprintId = null, // Currently in Product Backlog
                StatusId = openStatusId,
                IssueTypeId = storyTypeId,
                PriorityId = normalPrioId,
                Title = "Tối ưu truy vấn Kanban theo Sprint",
                StoryPoints = 5,
                IsDeleted = false
            },
            new Issue
            {
                Id = preExistingBacklogSubtaskId,
                ProjectId = projectId,
                IssueNumber = 11,
                ParentId = backlogStoryId,
                SprintId = null,
                StatusId = openStatusId,
                IssueTypeId = subtaskTypeId,
                Title = "[DB] Đánh chỉ mục StatusId và SprintId",
                StoryPoints = 2,
                IsDeleted = false
            },
            new Issue
            {
                Id = doneIssueInPrevSprintId,
                ProjectId = projectId,
                IssueNumber = 12,
                SprintId = prevCompletedSprintId,
                StatusId = releasedStatusId, // Custom IsDone status: must NOT rollover!
                IssueTypeId = taskTypeId,
                PriorityId = normalPrioId,
                Title = "Hoàn tất cấu hình CI/CD",
                StoryPoints = 3,
                IsDeleted = false
            }
        );

        await planningDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();

        var coordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            new MemoryStateStagingService(),
            new NoOpUserLookupService(),
            NullLogger<AiSprintCommitCoordinator>.Instance);

        var scope = new ProjectScopeContext(orgId, projectId, actorId);
        var request = new AiApplySprintProposalRequest
        {
            SessionId = "commit_backlog_move_session",
            ProjectId = projectId,
            TargetSprintId = targetSprintId,
            SprintGoal = "Chuyển Story từ Backlog vào Sprint 2 và hoàn thiện API",
            Tasks =
            [
                new ProposedTaskDto
                {
                    Id = "task_move_01",
                    SourceBacklogIssueId = backlogStoryId,
                    SourceBacklogIssueKey = "QLDA-10",
                    EpicTitle = "Hiệu năng Backlog & Kanban",
                    Title = "Tối ưu truy vấn Kanban theo Sprint",
                    Description = "Nâng cấp từ Story Product Backlog để phục vụ Sprint 2",
                    Type = "Story",
                    Priority = "Urgent",
                    Status = "Open",
                    StoryPoints = 5,
                    EstimatedHours = 14,
                    Tags = ["Backend", "SignalR"],
                    AcceptanceCriteria = ["Thời gian tải bảng dưới 150ms"],
                    Subtasks =
                    [
                        new ProposedSubTaskDto
                        {
                            Id = "sub_new_01",
                            Title = "[Backend] Tối ưu Projection EF Core",
                            Role = "Backend",
                            Description = "Sử dụng AsNoTracking và projection DTO thay vì Include toàn bảng.",
                            Priority = "Urgent",
                            Status = "Open",
                            StoryPoints = 2,
                            EstimatedHours = 6,
                            AcceptanceCriteria = ["Không phát sinh N+1 query", "Unit test query pass"]
                        }
                    ]
                }
            ]
        };

        var response = await coordinator.ApplySprintProposalAsync(request, scope);

        Assert.True(response.Success);
        Assert.Equal(1, response.MovedFromBacklogCount);

        // Backlog must NOT retain duplicate copy of QLDA-10 or QLDA-11
        Assert.Empty(await deliveryDb.Issues.Where(i => i.ProjectId == projectId && i.SprintId == null && i.IssueTypeId != epicTypeId).ToListAsync());

        var movedStory = await deliveryDb.Issues.SingleAsync(i => i.Id == backlogStoryId);
        Assert.Equal(targetSprintId, movedStory.SprintId);
        Assert.Equal(urgentPrioId, movedStory.PriorityId); // "Urgent" preserved!
        Assert.Equal(openStatusId, movedStory.StatusId);

        var movedExistingSub = await deliveryDb.Issues.SingleAsync(i => i.Id == preExistingBacklogSubtaskId);
        Assert.Equal(targetSprintId, movedExistingSub.SprintId);

        // Labels: existing "Backend" reused + "SignalR" newly created and linked
        var projectLabels = await deliveryDb.Labels.Where(l => l.ProjectId == projectId).ToListAsync();
        Assert.Equal(2, projectLabels.Count);
        var linkedLabelIds = await deliveryDb.IssueLabels.Where(il => il.IssueId == backlogStoryId).Select(il => il.LabelId).ToListAsync();
        Assert.Equal(2, linkedLabelIds.Count);

        // New subtask preserves its own Description and AcceptanceCriteria
        var createdSub = await deliveryDb.Issues.SingleAsync(i => i.ParentId == backlogStoryId && i.Id != preExistingBacklogSubtaskId);
        Assert.Equal("Sử dụng AsNoTracking và projection DTO thay vì Include toàn bảng.", createdSub.Description);
        var subCriteria = await deliveryDb.AcceptanceCriteria.Where(ac => ac.IssueId == createdSub.Id).Select(ac => ac.Content).ToListAsync();
        Assert.Equal(["Không phát sinh N+1 query", "Unit test query pass"], subCriteria);

        // Custom IsDone ("Released") issue in completed sprint was NOT rolled over into Sprint 2
        var donePrevIssue = await deliveryDb.Issues.SingleAsync(i => i.Id == doneIssueInPrevSprintId);
        Assert.Equal(prevCompletedSprintId, donePrevIssue.SprintId);
    }
}
