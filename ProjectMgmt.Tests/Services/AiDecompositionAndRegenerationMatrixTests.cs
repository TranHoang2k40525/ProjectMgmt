namespace ProjectMgmt.Tests.Services;

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using Planning.Infrastructure.Services;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Tracing;
using ProjectMgmt.Core.VectorSearch;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

public class AiDecompositionAndRegenerationMatrixTests
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

    private sealed class ConfigurableMockAiClient : IAiModelClient
    {
        private readonly string _streamOutput;
        private readonly string _generateOutput;
        private readonly bool _throwOnStream;
        private readonly bool _throwOnGenerate;

        public ConfigurableMockAiClient(
            string streamOutput,
            string generateOutput,
            bool throwOnStream = false,
            bool throwOnGenerate = false)
        {
            _streamOutput = streamOutput;
            _generateOutput = generateOutput;
            _throwOnStream = throwOnStream;
            _throwOnGenerate = throwOnGenerate;
        }

        public string ProviderName => "TestMatrixProvider";

        public Task<AiCompletionResult> GenerateAsync(AiPrompt prompt, IProjectScopeContext scope, CancellationToken ct = default)
        {
            if (_throwOnGenerate)
            {
                throw new InvalidOperationException("Simulated LLM generation failure");
            }

            return Task.FromResult(new AiCompletionResult
            {
                IsSuccess = !string.IsNullOrWhiteSpace(_generateOutput),
                Content = _generateOutput
            });
        }

        public async Task StreamGenerateAsync(AiPrompt prompt, IProjectScopeContext scope, Func<AiStreamChunk, Task> onChunk, CancellationToken ct = default)
        {
            if (_throwOnStream)
            {
                throw new InvalidOperationException("Simulated LLM stream failure");
            }

            await onChunk(new AiStreamChunk
            {
                ChunkType = AiStreamChunkType.Thought,
                DeltaThought = "Đang phân tích ngữ cảnh dự án, lịch sử Sprint và hồ sơ kỹ năng..."
            });

            if (!string.IsNullOrEmpty(_streamOutput))
            {
                await onChunk(new AiStreamChunk
                {
                    ChunkType = AiStreamChunkType.Content,
                    DeltaContent = _streamOutput
                });
            }
        }
    }

    private sealed class NoOpVectorSearch : IVectorSearchService
    {
        public Task<IReadOnlyList<VectorSearchResult>> SearchRelevantSpecsAsync(
            Guid projectId,
            string query,
            int topK = 5,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<VectorSearchResult>>(Array.Empty<VectorSearchResult>());

        public Task<bool> UpsertSpecChunkAsync(
            Guid projectId,
            string chunkId,
            string title,
            string content,
            string sourcePath,
            IReadOnlyDictionary<string, string>? metadata = null,
            CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class NoOpTraceService : IAiTraceService
    {
        public Task<string> StartTraceAsync(
            string traceId,
            Guid projectId,
            Guid userId,
            string sessionName,
            IReadOnlyDictionary<string, string>? tags = null,
            CancellationToken ct = default)
            => Task.FromResult(traceId);

        public Task RecordSpanAsync(AiTraceSpan span, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordGenerationAsync(
            string traceId,
            string modelName,
            string prompt,
            string completion,
            int promptTokens,
            int completionTokens,
            long durationMs,
            IReadOnlyDictionary<string, object?>? metadata = null,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordToolCallAsync(
            string traceId,
            string toolName,
            string inputJson,
            string outputJson,
            long durationMs,
            bool success,
            string? errorMessage = null,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordFeedbackAsync(
            string traceId,
            string feedbackType,
            decimal score,
            string? comment = null,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpSpecHubStorageService : ISpecHubStorageService
    {
        public string GetProjectRootPath(string organizationName, string projectName) => string.Empty;
        public Task<string> EnsureProjectFolderAsync(string organizationName, string projectName, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<string> CreateFolderAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<string> WriteTextFileAsync(string organizationName, string projectName, string relativePath, string content, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<(string RelativePath, long FileSize)> SaveUploadedFileAsync(string organizationName, string projectName, string relativePath, Stream stream, CancellationToken cancellationToken = default) => Task.FromResult((relativePath, 0L));
        public Task<string> ReadTextFileAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult("Nội dung đặc tả mẫu");
        public Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult<(Stream, string, string)>((new MemoryStream(), "text/plain", "spec.md"));
        public Task<DocxPreviewDto> ExtractDocxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(new DocxPreviewDto("spec.docx", "<p>Spec</p>", "Spec"));
        public Task<XlsxPreviewDto> ExtractXlsxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(new XlsxPreviewDto("spec.xlsx", new List<XlsxSheetDto>()));
        public Task DeleteAsync(string organizationName, string projectName, string relativePath, bool isDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RenameAsync(string organizationName, string projectName, string oldRelativePath, string newRelativePath, bool isDirectory, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public static IEnumerable<object[]> GetPipelineExecutionPermutations()
    {
        var llmOutputModes = new[] { "CleanJson", "MarkdownJsonFence", "ProseWrappedJson", "MalformedJsonFallback", "StreamExceptionFallback" };
        var teamSizes = new[] { 0, 1, 3, 6 };
        var promptModes = new[]
        {
            "NormalPrompt",
            "NullPrompt",
            "EmptyWhitespacePrompt",
            "PromptWithSecretKeyToRedact_AIzaSyDummyKey12345678901234567890",
            "PromptWithInjectionAttempt_IgnorePreviousInstructions"
        };

        foreach (var mode in llmOutputModes)
        {
            foreach (var teamSize in teamSizes)
            {
                foreach (var promptMode in promptModes)
                {
                    yield return new object[] { mode, teamSize, promptMode };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetPipelineExecutionPermutations))]
    public async Task ExecutePipelineAsync_ShouldHandleAllOutputFormatsTeamSizesAndPrompts(
        string llmOutputMode,
        int teamSize,
        string promptMode)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(
            new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Pipe_Plan_{suffix}").Options);
        await using var identityDb = new TestIdentityDbContext(
            new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Pipe_Id_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(
            new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Pipe_Del_{suffix}").Options);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        planningDb.Projects.Add(new Project
        {
            Id = projectId,
            OrgId = orgId,
            Name = "Quản lý dự án",
            ProjectKey = "QLDA"
        });

        var devRoleId = Guid.NewGuid();
        identityDb.Roles.Add(new Role { Id = devRoleId, Name = "Developer", Scope = "Project" });

        for (var i = 0; i < teamSize; i++)
        {
            var uid = Guid.NewGuid();
            identityDb.Users.Add(new User
            {
                Id = uid,
                Email = $"dev{i}@test.local",
                NormalizedEmail = $"DEV{i}@TEST.LOCAL",
                PasswordHash = "HASH",
                SecurityStamp = Guid.NewGuid(),
                IsActive = true,
                IsEmailVerified = true
            });
            identityDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = uid,
                DisplayName = $"Kỹ sư {i + 1:D2}",
                SeniorityLevel = "Senior",
                UpdatedAt = DateTime.UtcNow
            });
            identityDb.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = uid,
                RoleId = devRoleId,
                ScopeType = "Project",
                ScopeId = projectId
            });
        }
        await planningDb.SaveChangesAsync();
        await identityDb.SaveChangesAsync();

        var sampleJson = JsonSerializer.Serialize(new AiSprintProposalDto
        {
            SprintGoal = "Xây dựng phân hệ Quản lý Sprint thông minh",
            Rationale = "Phù hợp năng lực đội ngũ",
            Tasks = new List<ProposedTaskDto>
            {
                new()
                {
                    Title = "Thiết kế API Phân tách",
                    Description = "Xây dựng SSE endpoint",
                    Type = "Story",
                    Priority = "High",
                    StoryPoints = 5,
                    EstimatedHours = 12,
                    AssigneeName = teamSize > 0 ? "Kỹ sư 01" : null,
                    AcceptanceCriteria = new List<string> { "Chuẩn SSE", "Bảo mật JWT" },
                    Subtasks = new List<ProposedSubTaskDto>
                    {
                        new() { Title = "Code Controller", Role = "Backend", EstimatedHours = 6 }
                    }
                },
                new()
                {
                    Title = "Thiết kế UI Modal",
                    Description = "Xây dựng giao diện Angular",
                    Type = "Task",
                    Priority = "Medium",
                    StoryPoints = 3,
                    EstimatedHours = 8
                }
            }
        });

        var rawStream = llmOutputMode switch
        {
            "CleanJson" => sampleJson,
            "MarkdownJsonFence" => $"```json\n{sampleJson}\n```",
            "ProseWrappedJson" => $"Dưới đây là kết quả phân tích của AI:\n{sampleJson}\nChúc dự án thành công!",
            "MalformedJsonFallback" => "{ invalid json: [ unclosed ",
            _ => string.Empty
        };

        var aiClient = new ConfigurableMockAiClient(
            streamOutput: rawStream,
            generateOutput: sampleJson,
            throwOnStream: llmOutputMode == "StreamExceptionFallback");

        var vectorSearch = new NoOpVectorSearch();
        var traceService = new NoOpTraceService();
        var specTools = new SpecHubTools(planningDb, new NoOpSpecHubStorageService(), NullLogger<SpecHubTools>.Instance);
        var backlogTools = new BacklogTools(deliveryDb, planningDb);
        var sprintTools = new SprintTools(planningDb, deliveryDb);
        var teamTools = new TeamTools(planningDb, identityDb, deliveryDb);

        var service = new AiSprintDecompositionService(
            aiClient,
            vectorSearch,
            traceService,
            planningDb,
            specTools,
            backlogTools,
            sprintTools,
            teamTools,
            NullLogger<AiSprintDecompositionService>.Instance);

        var scope = new ProjectScopeContext(orgId, projectId, actorId);

        var stepEvents = new List<int>();
        var toolCalls = new List<string>();
        var thoughts = new List<string>();
        AiSprintProposalDto? finalProposal = null;

        string? userPrompt = promptMode switch
        {
            "NullPrompt" => null,
            "EmptyWhitespacePrompt" => "   ",
            _ => promptMode
        };

        await service.ExecutePipelineAsync(
            sessionId: $"sess_{suffix}",
            scope: scope,
            targetSprintId: null,
            extractedDocumentContent: "Tài liệu đặc tả hệ thống quản lý dự án chuẩn Scrum.",
            userPrompt: userPrompt,
            onStepProgress: (step, code, title, pct, detail) =>
            {
                stepEvents.Add(step);
                return Task.CompletedTask;
            },
            onToolCall: (tool, args, summary, ms) =>
            {
                toolCalls.Add(tool);
                return Task.CompletedTask;
            },
            onThoughtDelta: delta =>
            {
                thoughts.Add(delta);
                return Task.CompletedTask;
            },
            onProposalReady: proposal =>
            {
                finalProposal = proposal;
                return Task.CompletedTask;
            });

        Assert.Contains(2, stepEvents);
        Assert.Contains(3, stepEvents);
        Assert.Contains(4, stepEvents);
        Assert.Contains(5, stepEvents);
        Assert.Contains("spec_hub_search", toolCalls);
        Assert.Contains("sprint_get_history_and_velocity", toolCalls);
        Assert.Contains("team_get_dev_capacities", toolCalls);
        Assert.Contains("backlog_get_stories", toolCalls);

        Assert.NotNull(finalProposal);
        Assert.Equal(projectId, finalProposal!.ProjectId);
        Assert.NotEmpty(finalProposal.Tasks);
        Assert.Equal(finalProposal.Tasks.Sum(t => t.StoryPoints), finalProposal.TotalStoryPoints);
        Assert.Equal(finalProposal.Tasks.Sum(t => t.EstimatedHours), finalProposal.TotalEstimatedHours);

        if (teamSize > 0)
        {
            Assert.All(finalProposal.Tasks, t =>
            {
                Assert.True(t.AssigneeUserId.HasValue);
                Assert.False(string.IsNullOrWhiteSpace(t.AssigneeName));
            });
        }
    }

    public static IEnumerable<object[]> GetSingleTaskRegenerationPermutations()
    {
        var taskCounts = new[] { 5, 6, 7, 8, 10 };
        var targetPositions = new[] { 0, 1, 2, 3, 4 };
        var regenerationModes = new[] { "ValidAiJson", "MarkdownAiJson", "MalformedAiJsonFallback", "AiExceptionFallback" };

        foreach (var count in taskCounts)
        {
            foreach (var pos in targetPositions)
            {
                foreach (var mode in regenerationModes)
                {
                    yield return new object[] { count, pos, mode };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetSingleTaskRegenerationPermutations))]
    public async Task RegenerateSingleTaskAsync_ShouldUpdateTargetTaskOnlyAndKeepOtherTasksUntouched(
        int taskCount,
        int targetIndex,
        string regenerationMode)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(
            new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"Regen1_Plan_{suffix}").Options);
        await using var identityDb = new TestIdentityDbContext(
            new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"Regen1_Id_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(
            new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"Regen1_Del_{suffix}").Options);

        var proposal = new AiSprintProposalDto
        {
            SessionId = $"sess_{suffix}",
            ProjectId = Guid.NewGuid(),
            SprintGoal = "Mục tiêu Sprint ban đầu",
            Tasks = Enumerable.Range(1, taskCount).Select(i => new ProposedTaskDto
            {
                Id = $"task_{i:D2}",
                Title = $"Task gốc số {i}",
                Description = $"Mô tả gốc của task {i}",
                StoryPoints = 3,
                EstimatedHours = 6
            }).ToList()
        };

        var targetTaskId = proposal.Tasks[targetIndex].Id;
        var updatedSingleTaskJson = JsonSerializer.Serialize(new ProposedTaskDto
        {
            Id = targetTaskId,
            Title = $"Task {targetIndex + 1} ĐÃ LÀM LẠI",
            Description = "Mô tả mới sau khi AI tinh chỉnh",
            StoryPoints = 8,
            EstimatedHours = 16,
            AcceptanceCriteria = new List<string> { "AC mới 1", "AC mới 2" }
        });

        var genOutput = regenerationMode switch
        {
            "ValidAiJson" => updatedSingleTaskJson,
            "MarkdownAiJson" => $"```json\n{updatedSingleTaskJson}\n```",
            "MalformedAiJsonFallback" => "not-a-json-object",
            _ => string.Empty
        };

        var aiClient = new ConfigurableMockAiClient(
            streamOutput: string.Empty,
            generateOutput: genOutput,
            throwOnGenerate: regenerationMode == "AiExceptionFallback");

        var vectorSearch = new NoOpVectorSearch();
        var service = new AiSprintDecompositionService(
            aiClient,
            vectorSearch,
            new NoOpTraceService(),
            planningDb,
            new SpecHubTools(planningDb, new NoOpSpecHubStorageService(), NullLogger<SpecHubTools>.Instance),
            new BacklogTools(deliveryDb, planningDb),
            new SprintTools(planningDb, deliveryDb),
            new TeamTools(planningDb, identityDb, deliveryDb),
            NullLogger<AiSprintDecompositionService>.Instance);

        var scope = new ProjectScopeContext(Guid.NewGuid(), proposal.ProjectId, Guid.NewGuid());

        var result = await service.RegenerateSingleTaskAsync(
            proposal,
            targetTaskId,
            "Chia nhỏ chi tiết hơn và tăng story points",
            scope);

        Assert.Equal(targetTaskId, result.Id);

        for (var i = 0; i < taskCount; i++)
        {
            if (i == targetIndex) continue;
            Assert.Equal($"Task gốc số {i + 1}", proposal.Tasks[i].Title);
            Assert.Equal($"Mô tả gốc của task {i + 1}", proposal.Tasks[i].Description);
            Assert.Equal(3, proposal.Tasks[i].StoryPoints);
        }

        if (regenerationMode is "ValidAiJson" or "MarkdownAiJson")
        {
            Assert.Equal($"Task {targetIndex + 1} ĐÃ LÀM LẠI", result.Title);
            Assert.Equal(8, result.StoryPoints);
        }
        else if (regenerationMode == "MalformedAiJsonFallback")
        {
            Assert.Contains("Đã ghi nhận yêu cầu", result.Description);
        }
        else
        {
            Assert.Contains("Chia nhỏ chi tiết hơn", result.Description);
        }
    }

    public static IEnumerable<object[]> GetFullProposalRegenerationPermutations()
    {
        var llmModes = new[] { "ValidAiJson", "MarkdownAiJson", "MalformedFallback", "ExceptionFallback" };
        var feedbacks = new[]
        {
            "Tập trung vào hiệu năng và bảo mật",
            "Giảm tổng Story Points xuống dưới 20 SP",
            "Bổ sung các subtask kiểm thử tự động cho QA",
            "Ưu tiên hoàn thành các API Backend trước",
            "Phân bổ đều công việc cho các thành viên Senior"
        };
        var hasTargetSprint = new[] { true, false, true };

        for (var s = 0; s < hasTargetSprint.Length; s++)
        {
            foreach (var mode in llmModes)
            {
                foreach (var fb in feedbacks)
                {
                    yield return new object[] { mode, fb, hasTargetSprint[s], s };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetFullProposalRegenerationPermutations))]
    public async Task RegenerateFullProposalAsync_ShouldRebuildProposalAccordingToFeedback(
        string llmMode,
        string feedback,
        bool hasTargetSprint,
        int variantIndex)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var planningDb = new TestPlanningDbContext(
            new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase($"RegenAll_Plan_{suffix}").Options);
        await using var identityDb = new TestIdentityDbContext(
            new DbContextOptionsBuilder<IdentityExperienceDbContext>().UseInMemoryDatabase($"RegenAll_Id_{suffix}").Options);
        await using var deliveryDb = new TestDeliveryDbContext(
            new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase($"RegenAll_Del_{suffix}").Options);

        var projectId = Guid.NewGuid();
        Guid? targetSprintId = hasTargetSprint ? Guid.NewGuid() : null;

        var proposal = new AiSprintProposalDto
        {
            SessionId = $"sess_{suffix}_{variantIndex}",
            ProjectId = projectId,
            TargetSprintId = targetSprintId,
            SprintGoal = "Sprint Goal Gốc",
            Tasks = new List<ProposedTaskDto>
            {
                new() { Id = "task_01", Title = "Task 1", Description = "Desc 1", StoryPoints = 5, EstimatedHours = 10 }
            }
        };

        var regeneratedDto = new AiSprintProposalDto
        {
            SprintGoal = $"Sprint Goal Mới: {feedback}",
            Tasks = new List<ProposedTaskDto>
            {
                new() { Id = "task_01", Title = "Task Tái cấu trúc 1", Description = feedback, StoryPoints = 3, EstimatedHours = 8 },
                new() { Id = "task_02", Title = "Task Tái cấu trúc 2", Description = feedback, StoryPoints = 5, EstimatedHours = 12 }
            }
        };

        var genOutput = llmMode switch
        {
            "ValidAiJson" => JsonSerializer.Serialize(regeneratedDto),
            "MarkdownAiJson" => $"```json\n{JsonSerializer.Serialize(regeneratedDto)}\n```",
            "MalformedFallback" => "{ broken_json",
            _ => string.Empty
        };

        var aiClient = new ConfigurableMockAiClient(
            streamOutput: string.Empty,
            generateOutput: genOutput,
            throwOnGenerate: llmMode == "ExceptionFallback");

        var vectorSearch = new NoOpVectorSearch();
        var service = new AiSprintDecompositionService(
            aiClient,
            vectorSearch,
            new NoOpTraceService(),
            planningDb,
            new SpecHubTools(planningDb, new NoOpSpecHubStorageService(), NullLogger<SpecHubTools>.Instance),
            new BacklogTools(deliveryDb, planningDb),
            new SprintTools(planningDb, deliveryDb),
            new TeamTools(planningDb, identityDb, deliveryDb),
            NullLogger<AiSprintDecompositionService>.Instance);

        var scope = new ProjectScopeContext(Guid.NewGuid(), projectId, Guid.NewGuid());

        var updated = await service.RegenerateFullProposalAsync(proposal, feedback, scope);
        Assert.NotNull(updated);
        Assert.Equal(projectId, updated.ProjectId);
        Assert.NotEmpty(updated.Tasks);
    }

    public static IEnumerable<object[]> GetDocumentExtractorPermutations()
    {
        var extensions = new[] { ".txt", ".md", ".json", ".csv", ".docx" };
        var contentLengths = new[] { 1, 5, 20, 50 };
        var pastedModes = new[] { "None", "WithPastedNote", "WhitespaceOnlyPasted" };

        foreach (var ext in extensions)
        {
            foreach (var len in contentLengths)
            {
                foreach (var pasted in pastedModes)
                {
                    yield return new object[] { ext, len, pasted };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetDocumentExtractorPermutations))]
    public async Task AiDocumentTextExtractor_ShouldExtractAllSupportedFormatsAndCombineNotes(
        string extension,
        int paragraphCount,
        string pastedMode)
    {
        var extractor = new AiDocumentTextExtractor(NullLogger<AiDocumentTextExtractor>.Instance);
        var textLines = string.Join("\n", Enumerable.Range(1, paragraphCount).Select(i => $"Yêu cầu nghiệp vụ thứ {i}: Xây dựng chức năng phân rã Sprint."));

        byte[] fileBytes;
        if (extension == ".docx")
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("word/document.xml");
                await using var entryStream = entry.Open();
                var xml = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    <w:p><w:r><w:t>{textLines}</w:t></w:r></w:p>
  </w:body>
</w:document>";
                var xmlBytes = Encoding.UTF8.GetBytes(xml);
                await entryStream.WriteAsync(xmlBytes);
            }
            fileBytes = ms.ToArray();
        }
        else
        {
            fileBytes = Encoding.UTF8.GetBytes(textLines);
        }

        await using var fileStream = new MemoryStream(fileBytes);
        IFormFile formFile = new FormFile(fileStream, 0, fileBytes.Length, "file", $"spec_document{extension}");

        string? pasted = pastedMode switch
        {
            "WithPastedNote" => "Ghi chú bổ sung từ Product Owner: Ưu tiên bảo mật",
            "WhitespaceOnlyPasted" => "   ",
            _ => null
        };

        var extracted = await extractor.ExtractAsync(formFile, pasted);
        Assert.Contains("Yêu cầu nghiệp vụ thứ 1", extracted);
        if (pastedMode == "WithPastedNote")
        {
            Assert.Contains("Ghi chú bổ sung từ Product Owner", extracted);
        }
    }
}
