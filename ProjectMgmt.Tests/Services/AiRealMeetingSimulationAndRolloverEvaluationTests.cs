namespace ProjectMgmt.Tests.Services;

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
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

public class AiRealMeetingSimulationAndRolloverEvaluationTests
{
    private sealed class TestPlanningDbContext(DbContextOptions<PlanningDbContext> options) : PlanningDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => base.OnModelCreating(modelBuilder);
    }

    private sealed class TestIdentityDbContext(DbContextOptions<IdentityExperienceDbContext> options) : IdentityExperienceDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => base.OnModelCreating(modelBuilder);
    }

    private sealed class TestDeliveryDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options) : DeliveryIntelligenceDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => base.OnModelCreating(modelBuilder);
    }

    private sealed class MockAiClientForSimulations : IAiModelClient
    {
        private readonly Func<AiPrompt, string> _responseGenerator;

        public MockAiClientForSimulations(Func<AiPrompt, string> responseGenerator)
        {
            _responseGenerator = responseGenerator;
        }

        public string ProviderName => "SimulationClient";

        public Task<AiCompletionResult> GenerateAsync(AiPrompt prompt, IProjectScopeContext scope, CancellationToken ct = default)
        {
            var content = _responseGenerator(prompt);
            return Task.FromResult(new AiCompletionResult { IsSuccess = true, Content = content });
        }

        public async Task StreamGenerateAsync(AiPrompt prompt, IProjectScopeContext scope, Func<AiStreamChunk, Task> onChunk, CancellationToken ct = default)
        {
            await onChunk(new AiStreamChunk
            {
                ChunkType = AiStreamChunkType.Thought,
                DeltaThought = "Đang phân tích biên bản cuộc họp, đối chiếu tài liệu SRS và đánh giá năng lực đội ngũ..."
            });

            var content = _responseGenerator(prompt);
            await onChunk(new AiStreamChunk
            {
                ChunkType = AiStreamChunkType.Content,
                DeltaContent = content
            });
        }
    }

    private sealed class InMemoryPromptTemplateService : IPromptTemplateService
    {
        public Task<string> GetTemplateAsync(string templateName, CancellationToken ct = default)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProjectMgmt.Solution", "Prompts", $"{templateName}.md");
            if (File.Exists(path)) return File.ReadAllTextAsync(path, ct);

            var fallback = Path.Combine(Directory.GetCurrentDirectory(), "Prompts", $"{templateName}.md");
            if (File.Exists(fallback)) return File.ReadAllTextAsync(fallback, ct);

            return Task.FromResult(string.Empty);
        }

        public async Task<string> RenderTemplateAsync(string templateName, IDictionary<string, string> variables, CancellationToken ct = default)
        {
            var raw = await GetTemplateAsync(templateName, ct);
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            foreach (var (k, v) in variables)
            {
                raw = raw.Replace($"{{{{{k}}}}}", v ?? string.Empty);
            }
            return raw;
        }
    }

    private sealed class DummySpecHubStorageService : ISpecHubStorageService
    {
        public string GetProjectRootPath(string organizationName, string projectName) => string.Empty;
        public Task<string> EnsureProjectFolderAsync(string organizationName, string projectName, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<string> CreateFolderAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<string> WriteTextFileAsync(string organizationName, string projectName, string relativePath, string content, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<(string RelativePath, long FileSize)> SaveUploadedFileAsync(string organizationName, string projectName, string relativePath, Stream stream, CancellationToken cancellationToken = default) => Task.FromResult((relativePath, 0L));
        public Task<string> ReadTextFileAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult("SRS Nội dung đặc tả kỹ thuật");
        public Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult<(Stream, string, string)>((new MemoryStream(), "text/plain", "spec.md"));
        public Task<DocxPreviewDto> ExtractDocxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(new DocxPreviewDto("spec.docx", "<p>Spec</p>", "Spec"));
        public Task<XlsxPreviewDto> ExtractXlsxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(new XlsxPreviewDto("spec.xlsx", new List<XlsxSheetDto>()));
        public Task DeleteAsync(string organizationName, string projectName, string relativePath, bool isDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RenameAsync(string organizationName, string projectName, string oldRelativePath, string newRelativePath, bool isDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DummyVectorSearchService : IVectorSearchService
    {
        public Task<IReadOnlyList<VectorSearchResult>> SearchRelevantSpecsAsync(Guid projectId, string query, int topK = 5, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<VectorSearchResult>>(Array.Empty<VectorSearchResult>());

        public Task<bool> UpsertSpecChunkAsync(Guid projectId, string chunkId, string title, string content, string sourcePath, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class DummyTraceService : IAiTraceService
    {
        public Task<string> StartTraceAsync(string traceId, Guid projectId, Guid userId, string sessionName, IReadOnlyDictionary<string, string>? tags = null, CancellationToken ct = default) => Task.FromResult(traceId);
        public Task RecordSpanAsync(AiTraceSpan span, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordGenerationAsync(string traceId, string modelName, string prompt, string completion, int promptTokens, int completionTokens, long durationMs, IReadOnlyDictionary<string, object?>? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordToolCallAsync(string traceId, string toolName, string inputJson, string outputJson, long durationMs, bool success, string? errorMessage = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordFeedbackAsync(string traceId, string feedbackType, decimal score, string? comment = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class DummyUserLookupService : IUserLookupService
    {
        public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync()
            => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());

        public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds)
            => Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());

        public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId)
            => Task.FromResult<UserDisplayInfo?>(null);
    }

    [Fact]
    public async Task Scenario1_SprintPlanningMeeting_RealtimeKanban_ShouldGenerateComprehensiveActionablePlan()
    {
        // -------------------------------------------------------------
        // GIẢ LẬP VAI TRÒ & BIÊN BẢN HỌP:
        // - PO (Product Owner): "Cần hoàn thiện bảng Kanban realtime trong Sprint này để khách hàng demo."
        // - Tech Lead: "Backend dùng SignalR Hub, MySQL 8.0, cấu hình JWT WebSocket. Frontend dùng Angular Signal."
        // - Scrum Master: "Có 3 thành viên: Hoàng Hải Hậu (Frontend), Vũ Hoàng Nam (Backend), Đặng Phương Thảo (QA)."
        // -------------------------------------------------------------
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Plan_{suffix}").Options);
        await using var identityDb = new TestIdentityDbContext(new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Id_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        planningDb.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Quản lý dự án", ProjectKey = "QLDA", LeadUserId = leadId });
        planningDb.IssueTypes.AddRange(
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Story", OrderIndex = 1 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Task", OrderIndex = 2 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Bug", OrderIndex = 3 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Subtask", IsSubtask = true, OrderIndex = 4 }
        );
        planningDb.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "To Do", IsInitial = true, OrderIndex = 1 },
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "In Progress", OrderIndex = 2 },
            new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Done", OrderIndex = 3 }
        );
        planningDb.Priorities.AddRange(
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Highest", Level = 1 },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "High", Level = 2 },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Medium", Level = 3 },
            new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Low", Level = 4 }
        );
        planningDb.ProjectDocuments.Add(new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "SRS-Quan-Ly-Sprint-Va-Kanban-Realtime.md",
            Title = "Đặc tả Kanban Realtime",
            Extension = ".md",
            IsDirectory = false
        });

        // 3 thành viên theo vai trò kỹ thuật
        var devFeId = Guid.NewGuid();
        var devBeId = Guid.NewGuid();
        var qaId = Guid.NewGuid();

        identityDb.Users.AddRange(
            new User { Id = devFeId, Email = "hau.fe@huce.edu.vn", NormalizedEmail = "HAU.FE@HUCE.EDU.VN", PasswordHash = "H", SecurityStamp = Guid.NewGuid(), IsActive = true, IsEmailVerified = true },
            new User { Id = devBeId, Email = "nam.be@huce.edu.vn", NormalizedEmail = "NAM.BE@HUCE.EDU.VN", PasswordHash = "H", SecurityStamp = Guid.NewGuid(), IsActive = true, IsEmailVerified = true },
            new User { Id = qaId, Email = "thao.qa@huce.edu.vn", NormalizedEmail = "THAO.QA@HUCE.EDU.VN", PasswordHash = "H", SecurityStamp = Guid.NewGuid(), IsActive = true, IsEmailVerified = true }
        );
        identityDb.UserProfiles.AddRange(
            new UserProfile { Id = Guid.NewGuid(), UserId = devFeId, DisplayName = "Hoàng Hải Hậu", JobTitle = "Frontend Developer", Bio = "Angular, TypeScript, Signals" },
            new UserProfile { Id = Guid.NewGuid(), UserId = devBeId, DisplayName = "Vũ Hoàng Nam", JobTitle = "Backend Developer", Bio = ".NET 10, SignalR, MySQL" },
            new UserProfile { Id = Guid.NewGuid(), UserId = qaId, DisplayName = "Đặng Phương Thảo", JobTitle = "QA Engineer", Bio = "Playwright, E2E Testing" }
        );

        await planningDb.SaveChangesAsync();
        await identityDb.SaveChangesAsync();

        // Mô phỏng phản hồi AI chuẩn mực (không template, không giới hạn cứng, bám sát biên bản họp)
        var simulatedProposalJson = JsonSerializer.Serialize(new
        {
            sprintGoal = "Triển khai hoàn tất Bảng Kanban thời gian thực qua SignalR và kiểm thử E2E Playwright",
            rationale = "Phân bổ theo chuyên môn kỹ thuật Frontend (Angular Signals), Backend (SignalR Hub) và QA (E2E Playwright).",
            tags = new[] { "#SprintPlan", "#RealtimeKanban", "#SignalR" },
            tasks = new object[]
            {
                new
                {
                    id = "task_01",
                    epicTitle = "Realtime Kanban Core",
                    title = "Xây dựng SignalR Hub và tích hợp WebSocket đồng bộ trạng thái thẻ",
                    description = "Thiết kế SignalR Hub cho bảng Kanban, định nghĩa các Channel theo ProjectId và xử lý bắt sự kiện chuyển cột.",
                    type = "Story",
                    priority = "High",
                    storyPoints = 5,
                    estimatedHours = 16,
                    assigneeName = "Vũ Hoàng Nam",
                    tags = new[] { "#Backend", "#SignalR" },
                    acceptanceCriteria = new[]
                    {
                        "Clients nhận được thông báo sự kiện đổi cột thẻ trong dưới 200ms",
                        "Xác thực token JWT qua Query String cho kết nối WebSocket"
                    },
                    documentCitations = new[] { "SRS-Quan-Ly-Sprint-Va-Kanban-Realtime.md - Mục 2.2" },
                    aiRationale = "Vũ Hoàng Nam phụ trách Backend có chuyên môn sâu về .NET và SignalR.",
                    subtasks = new object[]
                    {
                        new
                        {
                            id = "task_01_1",
                            title = "Định nghĩa KanbanHub và đăng ký Dependency Injection",
                            role = "Backend",
                            estimatedHours = 8,
                            storyPoints = 2,
                            priority = "High",
                            assigneeName = "Vũ Hoàng Nam",
                            description = "Cấu hình Program.cs và xây dựng Hub logic.",
                            acceptanceCriteria = new[] { "Hub kết nối thành công và phân nhóm Client theo ProjectId" }
                        },
                        new
                        {
                            id = "task_01_2",
                            title = "Unit tests luồng phát sự kiện SignalR",
                            role = "QA",
                            estimatedHours = 8,
                            storyPoints = 1,
                            priority = "Medium",
                            assigneeName = "Đặng Phương Thảo",
                            description = "Viết kịch bản kiểm thử giả lập nhiều Client đồng thời kết nối Hub.",
                            acceptanceCriteria = new[] { "Kiểm tra không bị rò rỉ kết nối socket khi client disconnect" }
                        }
                    }
                },
                new
                {
                    id = "task_02",
                    epicTitle = "Kanban User Experience",
                    title = "Phát triển giao diện bảng Kanban kéo thả với Angular CDK và Signal",
                    description = "Xây dựng component bảng Kanban, quản lý trạng thái cột bằng Angular Signals và bắt sự kiện Drag & Drop.",
                    type = "Story",
                    priority = "High",
                    storyPoints = 5,
                    estimatedHours = 16,
                    assigneeName = "Hoàng Hải Hậu",
                    tags = new[] { "#Frontend", "#AngularSignals" },
                    acceptanceCriteria = new[]
                    {
                        "Thao tác kéo thả mượt mà 60fps không bị giật lag",
                        "Tự động cập nhật vị trí thẻ khi nhận tín hiệu từ SignalR"
                    },
                    documentCitations = new[] { "SRS-Quan-Ly-Sprint-Va-Kanban-Realtime.md - Mục 3.1" },
                    aiRationale = "Hoàng Hải Hậu phụ trách Frontend chuyên môn sâu về Angular và UI/UX.",
                    subtasks = new object[]
                    {
                        new
                        {
                            id = "task_02_1",
                            title = "Triển khai Angular CDK DragDrop và Signal Store",
                            role = "Frontend",
                            estimatedHours = 10,
                            storyPoints = 2,
                            priority = "High",
                            assigneeName = "Hoàng Hải Hậu",
                            description = "Lập trình DragDrop directive và kết nối với Service.",
                            acceptanceCriteria = new[] { "Kéo thả thành công giữa 4 cột trạng thái" }
                        },
                        new
                        {
                            id = "task_02_2",
                            title = "Kịch bản E2E Playwright kiểm thử kéo thả thẻ Kanban",
                            role = "QA",
                            estimatedHours = 6,
                            storyPoints = 1,
                            priority = "Medium",
                            assigneeName = "Đặng Phương Thảo",
                            description = "Tự động hóa kịch bản người dùng kéo thẻ từ To Do sang In Progress.",
                            acceptanceCriteria = new[] { "Kịch bản test Playwright pass 100% trong chế độ headless" }
                        }
                    }
                },
                new
                {
                    id = "task_03",
                    epicTitle = "Quality Assurance & Performance",
                    title = "Kiểm thử hiệu năng và bảo mật Zero-Secret-Leakage cho SignalR Hub",
                    description = "Rà soát toàn diện việc bảo vệ thông tin mật và kiểm thử tải kết nối WebSocket đồng thời.",
                    type = "Task",
                    priority = "Medium",
                    storyPoints = 3,
                    estimatedHours = 8,
                    assigneeName = "Đặng Phương Thảo",
                    tags = new[] { "#QA", "#Security" },
                    acceptanceCriteria = new[]
                    {
                        "Hệ thống chịu tải 200 kết nối đồng thời không tăng đột biến bộ nhớ",
                        "Không ghi nhận secret/token trong logs"
                    },
                    documentCitations = new[] { "SRS-Quan-Ly-Sprint-Va-Kanban-Realtime.md - Mục 3.3" },
                    aiRationale = "Đặng Phương Thảo đảm bảo chất lượng và an toàn thông tin sản phẩm.",
                    subtasks = Array.Empty<object>()
                }
            }
        });

        var aiClient = new MockAiClientForSimulations(_ => simulatedProposalJson);
        var promptTemplates = new InMemoryPromptTemplateService();
        var specHubTools = new SpecHubTools(planningDb, new DummySpecHubStorageService(), NullLogger<SpecHubTools>.Instance);
        var backlogTools = new BacklogTools(deliveryDb, planningDb);
        var sprintTools = new SprintTools(planningDb, deliveryDb);
        var teamTools = new TeamTools(planningDb, identityDb);
        var vectorSearch = new DummyVectorSearchService();
        var traceService = new DummyTraceService();

        var service = new AiSprintDecompositionService(
            aiClient,
            vectorSearch,
            traceService,
            planningDb,
            specHubTools,
            backlogTools,
            sprintTools,
            teamTools,
            promptTemplates,
            NullLogger<AiSprintDecompositionService>.Instance
        );

        var scope = new ProjectScopeContext(orgId, projectId, leadId);
        AiSprintProposalDto? generatedProposal = null;

        await service.ExecutePipelineAsync(
            sessionId: "sim_session_01",
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: "Biên bản họp Sprint Planning: PO yêu cầu triển khai bảng Kanban Realtime qua SignalR cho dự án QLDA.",
            userPrompt: "Tập trung vào tính năng realtime và kiểm thử tự động Playwright",
            onStepProgress: (_, _, _, _, _) => Task.CompletedTask,
            onToolCall: (_, _, _, _) => Task.CompletedTask,
            onThoughtDelta: _ => Task.CompletedTask,
            onProposalReady: p => { generatedProposal = p; return Task.CompletedTask; },
            model: null,
            cancellationToken: default
        );

        // ==============================================================
        // ĐÁNH GIÁ CHẤT LƯỢNG THỰC CHIẾN (EVALUATION CRITERIA):
        // ==============================================================
        Assert.NotNull(generatedProposal);
        Assert.Equal("Triển khai hoàn tất Bảng Kanban thời gian thực qua SignalR và kiểm thử E2E Playwright", generatedProposal.SprintGoal);
        Assert.Equal(3, generatedProposal.Tasks.Count);

        // 1. Kiểm tra không bị ép một loại điểm (Fibonacci đa dạng: 5, 5, 3)
        var spList = generatedProposal.Tasks.Select(t => t.StoryPoints).ToList();
        Assert.Contains(5, spList);
        Assert.Contains(3, spList);
        Assert.Equal(13, generatedProposal.TotalStoryPoints);

        // 2. Kiểm tra phân công đúng kỹ năng chuyên môn
        var beTask = generatedProposal.Tasks.First(t => t.Id == "task_01");
        Assert.Equal("Vũ Hoàng Nam", beTask.AssigneeName);
        Assert.Contains("SRS-Quan-Ly-Sprint-Va-Kanban-Realtime.md - Mục 2.2", beTask.DocumentCitations);

        var feTask = generatedProposal.Tasks.First(t => t.Id == "task_02");
        Assert.Equal("Hoàng Hải Hậu", feTask.AssigneeName);

        var qaTask = generatedProposal.Tasks.First(t => t.Id == "task_03");
        Assert.Equal("Đặng Phương Thảo", qaTask.AssigneeName);
        Assert.Empty(qaTask.Subtasks); // Task 3 SP độc lập không bị ép sinh subtask giả tạo

        // 3. Kiểm tra Subtask đầy đủ chuyên môn và tiêu chí nghiệm thu
        Assert.Equal(2, beTask.Subtasks.Count);
        Assert.All(beTask.Subtasks, s =>
        {
            Assert.NotEmpty(s.Title);
            Assert.NotEmpty(s.AcceptanceCriteria);
            Assert.True(s.EstimatedHours > 0);
        });

        // 4. Kiểm tra loại bỏ tính nhựa AI: Tiêu đề không chứa "# GHI CHÚ / YÊU CẦU BỔ SUNG"
        Assert.DoesNotContain("GHI CHÚ", beTask.Title);
        Assert.DoesNotContain("YÊU CẦU BỔ SUNG", beTask.Title);
    }

    [Fact]
    public async Task Scenario2_SprintRolloverAndOverdueTaskHandling_ShouldCorrectlyPartitionAndApply()
    {
        // -------------------------------------------------------------
        // KỊCH BẢN 2: SPRINT REVIEW & ROLLOVER
        // Sprint 1 kết thúc với 3 hạng mục dở dang:
        // 1. BUG nghiêm trọng: "Lỗi crash app khi kéo thả thẻ Kanban không có assignee" (Quá hạn 1 ngày).
        // 2. TASK đang làm dở ('In Progress'): "Xây dựng Angular Service kết nối WebSocket" (TimeSpent = 120 phút).
        // 3. TASK chưa làm ('To Do') nhưng ĐÃ QUÁ HẠN: "Tạo tài liệu hướng dẫn sử dụng PDF" (TimeSpent = 0, DueDate quá hạn 5 ngày).
        //
        // MONG ĐỢI CHUẨN SCRUM & QUYẾT ĐỊNH AGENT:
        // - BUG nghiêm trọng -> Rollover sang Sprint 2 với mức ưu tiên 'Highest'.
        // - TASK đang làm dở -> Rollover sang Sprint 2 để tiếp tục hoàn thiện.
        // - TASK quá hạn chưa làm -> Chuyển về Product Backlog (SprintId = null) để PO thẩm định lại.
        // -------------------------------------------------------------
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Plan_{suffix}").Options);
        await using var identityDb = new TestIdentityDbContext(new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Id_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var project = new Project { Id = projectId, OrgId = orgId, Name = "Quản lý dự án", ProjectKey = "QLDA", IssueCounter = 100, LeadUserId = leadId };
        planningDb.Projects.Add(project);

        var storyType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Story", OrderIndex = 1 };
        var taskType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Task", OrderIndex = 2 };
        var bugType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Bug", OrderIndex = 3 };
        var subtaskType = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Subtask", IsSubtask = true, OrderIndex = 4 };
        planningDb.IssueTypes.AddRange(storyType, taskType, bugType, subtaskType);

        var todoStatus = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "To Do", IsInitial = true, OrderIndex = 1 };
        var inProgressStatus = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "In Progress", OrderIndex = 2 };
        var doneStatus = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Done", OrderIndex = 3 };
        planningDb.WorkflowStatuses.AddRange(todoStatus, inProgressStatus, doneStatus);

        var highestPrio = new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Highest", Level = 1 };
        var highPrio = new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "High", Level = 2 };
        var medPrio = new Priority { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Medium", Level = 3 };
        planningDb.Priorities.AddRange(highestPrio, highPrio, medPrio);

        var sprint1 = new Sprint
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "QLDA Sprint 1",
            Goal = "Khởi tạo nền tảng dự án",
            Status = "Completed",
            OrderIndex = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-14)
        };
        var sprint2 = new Sprint
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "QLDA Sprint 2",
            Goal = "Tính năng Realtime Kanban",
            Status = "Planned",
            OrderIndex = 2,
            CreatedAt = DateTime.UtcNow
        };
        planningDb.Sprints.AddRange(sprint1, sprint2);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Bug nghiêm trọng từ Sprint 1 (quá hạn)
        var bugIssue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            IssueNumber = 101,
            SprintId = sprint1.Id,
            IssueTypeId = bugType.Id,
            PriorityId = medPrio.Id,
            StatusId = todoStatus.Id,
            Title = "Bug: Crash ứng dụng khi kéo thả thẻ trống assignee",
            DueDate = today.AddDays(-1),
            TimeSpentMinutes = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        // 2. Task đang làm dở từ Sprint 1 (WIP)
        var wipIssue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            IssueNumber = 102,
            SprintId = sprint1.Id,
            IssueTypeId = taskType.Id,
            PriorityId = highPrio.Id,
            StatusId = inProgressStatus.Id,
            Title = "Task: Lập trình Angular Service kết nối WebSocket",
            DueDate = today.AddDays(2),
            TimeSpentMinutes = 120,
            CreatedAt = DateTime.UtcNow.AddDays(-4),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        // 3. Task thường chưa làm ('To Do') nhưng ĐÃ QUÁ HẠN 5 ngày
        var overdueTodoIssue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            IssueNumber = 103,
            SprintId = sprint1.Id,
            IssueTypeId = taskType.Id,
            PriorityId = medPrio.Id,
            StatusId = todoStatus.Id,
            Title = "Task: Tạo tài liệu hướng dẫn sử dụng PDF",
            DueDate = today.AddDays(-5),
            TimeSpentMinutes = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        deliveryDb.Issues.AddRange(bugIssue, wipIssue, overdueTodoIssue);
        await planningDb.SaveChangesAsync();
        await deliveryDb.SaveChangesAsync();

        // Áp dụng Đề xuất Sprint 2 qua AiSprintCommitCoordinator
        var stagingService = new MemoryStateStagingService();
        var userLookup = new DummyUserLookupService();
        var commitCoordinator = new AiSprintCommitCoordinator(
            planningDb,
            deliveryDb,
            stagingService,
            userLookup,
            NullLogger<AiSprintCommitCoordinator>.Instance
        );

        var scope = new ProjectScopeContext(orgId, projectId, leadId);
        var applyRequest = new AiApplySprintProposalRequest
        {
            SessionId = Guid.NewGuid().ToString("N"),
            TargetSprintId = sprint2.Id,
            SprintGoal = "Hoàn thành Realtime Kanban và giải quyết dứt điểm tồn đọng Sprint 1",
            Tasks = new List<ProposedTaskDto>
            {
                new()
                {
                    Id = "new_task_01",
                    Title = "Xây dựng giao diện bộ lọc thẻ thời gian thực",
                    Type = "Story",
                    Priority = "High",
                    StoryPoints = 3,
                    EstimatedHours = 8
                }
            }
        };

        var response = await commitCoordinator.ApplySprintProposalAsync(applyRequest, scope);

        // ==============================================================
        // KIỂM TRA KẾT QUẢ KẾ THỪA & PHÂN LOẠI ROLLOVER:
        // ==============================================================
        Assert.True(response.Success);
        Assert.Equal(1, response.CreatedTaskCount);
        Assert.Equal(2, response.RolloverTaskCount);      // 1 Bug + 1 WIP task
        Assert.Equal(1, response.BacklogReturnedCount);    // 1 Overdue Todo task chuyển về Backlog

        // Kiểm tra trong DB:
        var updatedBug = await deliveryDb.Issues.FindAsync(bugIssue.Id);
        var updatedWip = await deliveryDb.Issues.FindAsync(wipIssue.Id);
        var updatedOverdue = await deliveryDb.Issues.FindAsync(overdueTodoIssue.Id);

        Assert.NotNull(updatedBug);
        Assert.Equal(sprint2.Id, updatedBug.SprintId); // Đã chuyển sang Sprint 2
        Assert.Equal(highestPrio.Id, updatedBug.PriorityId); // Bug quá hạn được nâng lên Highest priority!

        Assert.NotNull(updatedWip);
        Assert.Equal(sprint2.Id, updatedWip.SprintId); // WIP task đã chuyển sang Sprint 2 để hoàn thiện

        Assert.NotNull(updatedOverdue);
        Assert.Null(updatedOverdue.SprintId); // Task quá hạn chưa làm đã được chuyển về Product Backlog!
    }
}
