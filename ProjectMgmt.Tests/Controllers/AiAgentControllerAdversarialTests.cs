namespace ProjectMgmt.Tests.Controllers;

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Controllers;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.Security;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

/// <summary>
/// Challenger 2 Adversarial Stress Suite for AiAgentController and SSE Streaming Pipeline.
/// Empirical verification of:
/// - Unauthenticated & forbidden access
/// - Missing & soft-deleted projects
/// - Client disconnect & cancellation token handling
/// - Strict 5-step SSE progress sequence (15%, 35%, 55%, 75%, 100%) and payload schemas
/// - Proposal staging, retrieval, and single-task regeneration isolation
/// - Rapid concurrent requests
/// </summary>
public class AiAgentControllerAdversarialTests
{
    #region Test Doubles & Fixtures

    public sealed class TestPlanningDbContext : PlanningDbContext
    {
        public TestPlanningDbContext(DbContextOptions<PlanningDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Organization>(b => b.HasKey(o => o.Id));
            modelBuilder.Entity<Project>(b => b.HasKey(p => p.Id));
        }
    }

    public sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
        public Guid? UserId { get; set; } = Guid.NewGuid();
    }

    public sealed class FakeUserLookupService : IUserLookupService
    {
        public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync()
        {
            return Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());
        }

        public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds)
        {
            return Task.FromResult<IReadOnlyList<UserDisplayInfo>>(Array.Empty<UserDisplayInfo>());
        }

        public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId)
        {
            return Task.FromResult<UserDisplayInfo?>(new UserDisplayInfo
            {
                UserId = userId,
                DisplayName = "Hoang Scrum Master",
                Email = "hoang@scrum.local"
            });
        }
    }

    public sealed class FakePermissionEvaluator : IPermissionEvaluator
    {
        public bool AllowProjectRead { get; set; } = true;

        public Task<bool> HasPermissionAsync(
            Guid userId,
            string permissionCode,
            string scopeType,
            Guid? scopeId,
            Guid? organizationId = null)
        {
            return Task.FromResult(AllowProjectRead);
        }

        public Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
        {
            return Task.FromResult(AllowProjectRead);
        }
    }

    public sealed class FakeAiSprintDecompositionService : IAiSprintDecompositionService
    {
        public bool ShouldThrow { get; set; }
        public Func<CancellationToken, Task>? OnExecutePipeline { get; set; }

        public async Task ExecutePipelineAsync(
            string sessionId,
            IProjectScopeContext scope,
            Guid? targetSprintId,
            string extractedDocumentContent,
            string? userPrompt,
            Func<int, string, string, int, string, Task> onStepProgress,
            Func<string, object?, string, long?, Task> onToolCall,
            Func<string, Task> onThoughtDelta,
            Func<AiSprintProposalDto, Task> onProposalReady,
            string? model = null,
            CancellationToken cancellationToken = default,
            AiSprintProposalDto? existingProposal = null)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulation error inside decomposition pipeline.");
            }

            if (OnExecutePipeline != null)
            {
                await OnExecutePipeline(cancellationToken);
            }

            // Emits Steps 2 through 5
            await onStepProgress(2, "QUERY_CONTEXT", "Truy vấn Ngữ cảnh & Lịch sử Sprint", 35, "Đang đối chiếu tài liệu đặc tả...");
            await onToolCall("spec_hub_search", new { query = "SRS" }, "Tìm kiếm tài liệu đặc tả", 15);
            await onThoughtDelta("Phân tích cấu trúc hệ thống...");

            await onStepProgress(3, "EVALUATE_CAPACITY", "Đánh giá Năng lực Đội ngũ & Kỹ năng", 55, "Đang thống kê số lượng thành viên...");
            await onToolCall("team_get_dev_capacities", new { }, "Đánh giá năng lực nhóm", 12);

            await onStepProgress(4, "DECOMPOSE_SPRINT", "Phân rã Sprint Goal, Epics & Tasks", 75, "Mô hình AI đang suy luận cấu trúc Sprint...");
            await onThoughtDelta("Tạo danh sách các task và subtask...");

            await onStepProgress(5, "FINALIZE_PROPOSAL", "Đề xuất Tiêu chí Nghiệm thu & Hoàn tất", 100, "Đang chuẩn hóa tiêu chí nghiệm thu...");

            var proposal = new AiSprintProposalDto
            {
                SessionId = sessionId,
                ProjectId = scope.ProjectId,
                TargetSprintId = targetSprintId,
                SprintGoal = "Xây dựng hệ thống streaming real-time và bảo mật Scrum",
                Rationale = "Tối ưu hóa năng lực đội ngũ và hoàn thành đúng hạn",
                TotalStoryPoints = 13,
                TotalEstimatedHours = 40,
                Tasks = new List<ProposedTaskDto>
                {
                    new()
                    {
                        Id = "task_01",
                        EpicTitle = "Core Streaming",
                        Title = "Cài đặt SSE Controller",
                        Description = "Stream dữ liệu tiến trình theo thời gian thực",
                        Type = "Story",
                        Priority = "High",
                        StoryPoints = 5,
                        EstimatedHours = 16,
                        AcceptanceCriteria = new List<string> { "Hỗ trợ text/event-stream", "Không buffer dữ liệu" },
                        DocumentCitations = new List<string> { "Đặc tả M2" },
                        AiRationale = "Task quan trọng nhất của Milestone 2",
                        Subtasks = new List<ProposedSubTaskDto>
                        {
                            new() { Id = "sub_01", Title = "DisableBuffering", Role = "Backend", EstimatedHours = 8 }
                        }
                    },
                    new()
                    {
                        Id = "task_02",
                        EpicTitle = "Core Streaming",
                        Title = "Kiểm thử Stress SSE Controller",
                        Description = "Viết test adversarial harness",
                        Type = "Task",
                        Priority = "High",
                        StoryPoints = 8,
                        EstimatedHours = 24,
                        AcceptanceCriteria = new List<string> { "100% test pass" }
                    }
                }
            };

            await onProposalReady(proposal);
        }

        public Task<ProposedTaskDto> RegenerateSingleTaskAsync(
            AiSprintProposalDto proposal,
            string taskId,
            string userFeedback,
            IProjectScopeContext scope,
            CancellationToken cancellationToken = default)
        {
            var existingTask = proposal.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (existingTask == null)
            {
                throw new InvalidOperationException($"Không tìm thấy Task với Id '{taskId}' trong đề xuất hiện tại.");
            }

            existingTask.Title += " (Refined)";
            existingTask.Description += $"\n[Feedback: {userFeedback}]";
            return Task.FromResult(existingTask);
        }
    }

    private static (AiAgentController Controller, TestPlanningDbContext Db, FakePermissionEvaluator Permissions, FakeCurrentUserContext CurrentUser, IStateStagingService Staging, FakeAiSprintDecompositionService DecompService, DefaultHttpContext HttpContext, MemoryStream ResponseBody)
        CreateTestHarness(Guid? userId = null, bool allowRead = true)
    {
        var dbOptions = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TestPlanningDbContext(dbOptions);

        var currentUser = new FakeCurrentUserContext { UserId = userId ?? Guid.NewGuid() };
        var permissions = new FakePermissionEvaluator { AllowProjectRead = allowRead };
        var projectAccess = new ProjectAccess(db, permissions, currentUser);
        var userLookup = new FakeUserLookupService();
        var staging = new MemoryStateStagingService();
        var extractor = new AiDocumentTextExtractor(NullLogger<AiDocumentTextExtractor>.Instance);
        var decompService = new FakeAiSprintDecompositionService();

        var controller = new AiAgentController(
            decompService,
            extractor,
            staging,
            db,
            projectAccess,
            currentUser,
            userLookup,
            NullLogger<AiAgentController>.Instance);

        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, db, permissions, currentUser, staging, decompService, httpContext, responseBody);
    }

    private static async Task<Project> SeedProjectAsync(TestPlanningDbContext db, Guid? projectId = null, Guid? orgId = null, bool isDeleted = false)
    {
        var proj = new Project
        {
            Id = projectId ?? Guid.NewGuid(),
            OrgId = orgId ?? Guid.NewGuid(),
            Name = "Scrum Test Project",
            ProjectKey = "SCRUM",
            IsDeleted = isDeleted,
            LeadUserId = Guid.NewGuid()
        };
        db.Projects.Add(proj);
        await db.SaveChangesAsync();
        return proj;
    }

    private static List<(string EventType, string JsonData)> ParseSseEvents(string sseOutput)
    {
        var events = new List<(string EventType, string JsonData)>();
        var blocks = sseOutput.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            string? eventType = null;
            string? jsonData = null;

            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    eventType = line["event: ".Length..].Trim();
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    jsonData = line["data: ".Length..].Trim();
                }
            }

            if (!string.IsNullOrEmpty(eventType) && !string.IsNullOrEmpty(jsonData))
            {
                events.Add((eventType, jsonData));
            }
        }

        return events;
    }

    #endregion

    #region 1. Security & Authentication Challenges

    [Fact]
    public async Task DecomposeStream_UnauthenticatedUser_Returns401Immediately()
    {
        var (controller, db, _, currentUser, _, _, httpContext, _) = CreateTestHarness();
        currentUser.UserId = null; // Unauthenticated

        var request = new AiDecomposeStreamRequest
        {
            ProjectId = Guid.NewGuid(),
            Text = "Yêu cầu phân tách Sprint"
        };

        await controller.DecomposeStream(request);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task DecomposeStream_EmptyProjectId_Returns400BadRequest()
    {
        var (controller, _, _, _, _, _, httpContext, _) = CreateTestHarness();

        var request = new AiDecomposeStreamRequest
        {
            ProjectId = Guid.Empty,
            Text = "Đặc tả với ProjectId rỗng"
        };

        await controller.DecomposeStream(request);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task DecomposeStream_NonExistentProjectId_EmitsProjectNotFoundErrorEvent()
    {
        var (controller, _, _, _, _, _, httpContext, responseBody) = CreateTestHarness();
        var nonExistentProjectId = Guid.NewGuid();

        var request = new AiDecomposeStreamRequest
        {
            ProjectId = nonExistentProjectId,
            Text = "Nội dung đặc tả hợp lệ"
        };

        await controller.DecomposeStream(request);

        var output = Encoding.UTF8.GetString(responseBody.ToArray());
        var sseEvents = ParseSseEvents(output);

        Assert.Single(sseEvents);
        Assert.Equal(AiSseEventTypes.Error, sseEvents[0].EventType);
        var payload = JsonSerializer.Deserialize<AiErrorPayload>(sseEvents[0].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(payload);
        Assert.Equal("PROJECT_NOT_FOUND", payload.Code);
        Assert.Contains("không tồn tại", payload.Message);
    }

    [Fact]
    public async Task DecomposeStream_SoftDeletedProject_EmitsProjectNotFoundErrorEvent()
    {
        var (controller, db, _, _, _, _, _, responseBody) = CreateTestHarness();
        var deletedProject = await SeedProjectAsync(db, isDeleted: true);

        var request = new AiDecomposeStreamRequest
        {
            ProjectId = deletedProject.Id,
            Text = "Nội dung đặc tả"
        };

        await controller.DecomposeStream(request);

        var output = Encoding.UTF8.GetString(responseBody.ToArray());
        var sseEvents = ParseSseEvents(output);

        Assert.Single(sseEvents);
        Assert.Equal(AiSseEventTypes.Error, sseEvents[0].EventType);
        var payload = JsonSerializer.Deserialize<AiErrorPayload>(sseEvents[0].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(payload);
        Assert.Equal("PROJECT_NOT_FOUND", payload.Code);
    }

    [Fact]
    public async Task DecomposeStream_UserLacksProjectReadPermission_EmitsForbiddenErrorEvent()
    {
        var (controller, db, permissions, _, _, _, _, responseBody) = CreateTestHarness();
        var project = await SeedProjectAsync(db);
        permissions.AllowProjectRead = false; // Forbidden

        var request = new AiDecomposeStreamRequest
        {
            ProjectId = project.Id,
            Text = "Đặc tả hệ thống"
        };

        await controller.DecomposeStream(request);

        var output = Encoding.UTF8.GetString(responseBody.ToArray());
        var sseEvents = ParseSseEvents(output);

        Assert.Single(sseEvents);
        Assert.Equal(AiSseEventTypes.Error, sseEvents[0].EventType);
        var payload = JsonSerializer.Deserialize<AiErrorPayload>(sseEvents[0].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(payload);
        Assert.Equal("FORBIDDEN", payload.Code);
        Assert.Contains("không có quyền", payload.Message);
    }

    #endregion

    #region 2. Strict 5-Step SSE Streaming Sequence & Payload Verification

    [Fact]
    public async Task DecomposeStream_ValidExecution_EmitsStrictFiveStepSequenceAndHeaders()
    {
        var (controller, db, _, _, staging, _, httpContext, responseBody) = CreateTestHarness();
        var project = await SeedProjectAsync(db);

        var request = new AiDecomposeStreamRequest
        {
            ProjectId = project.Id,
            TargetSprintId = Guid.NewGuid(),
            Prompt = "Lập kế hoạch tối ưu năng lực",
            Text = "Đặc tả yêu cầu phần mềm chi tiết cho Sprint M2."
        };

        await controller.DecomposeStream(request);

        // Verify Anti-Buffering & SSE HTTP Headers
        Assert.Equal("text/event-stream; charset=utf-8", httpContext.Response.ContentType);
        Assert.Equal("no-cache, no-transform", httpContext.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("no", httpContext.Response.Headers["X-Accel-Buffering"].ToString());

        var output = Encoding.UTF8.GetString(responseBody.ToArray());
        var sseEvents = ParseSseEvents(output);

        // Required event sequence:
        // 1. session_init
        // 2. step_progress (Step 1, 15%)
        // 3. step_progress (Step 2, 35%)
        // 4. tool_call
        // 5. thought_stream
        // 6. step_progress (Step 3, 55%)
        // 7. tool_call
        // 8. step_progress (Step 4, 75%)
        // 9. thought_stream
        // 10. step_progress (Step 5, 100%)
        // 11. proposal_ready
        // 12. completed

        var sessionInit = sseEvents.FirstOrDefault(e => e.EventType == AiSseEventTypes.SessionInit);
        Assert.NotNull(sessionInit.EventType);
        var initPayload = JsonSerializer.Deserialize<AiSessionInitPayload>(sessionInit.JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(initPayload);
        Assert.StartsWith("sprint_ai_", initPayload.SessionId);
        Assert.Equal(project.Id, initPayload.ProjectId);
        Assert.Equal("Hoang Scrum Master", initPayload.ActorName);

        // Verify all 5 steps in order with exact required percentages
        var progressSteps = sseEvents.Where(e => e.EventType == AiSseEventTypes.StepProgress).ToList();
        Assert.Equal(5, progressSteps.Count);

        var step1 = JsonSerializer.Deserialize<AiStepProgressPayload>(progressSteps[0].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(1, step1!.StepIndex);
        Assert.Equal(5, step1.TotalSteps);
        Assert.Equal("INGEST_DOCUMENTS", step1.StepName);
        Assert.Equal(15, step1.Percent);

        var step2 = JsonSerializer.Deserialize<AiStepProgressPayload>(progressSteps[1].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(2, step2!.StepIndex);
        Assert.Equal(5, step2.TotalSteps);
        Assert.Equal("QUERY_CONTEXT", step2.StepName);
        Assert.Equal(35, step2.Percent);

        var step3 = JsonSerializer.Deserialize<AiStepProgressPayload>(progressSteps[2].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(3, step3!.StepIndex);
        Assert.Equal(5, step3.TotalSteps);
        Assert.Equal("EVALUATE_CAPACITY", step3.StepName);
        Assert.Equal(55, step3.Percent);

        var step4 = JsonSerializer.Deserialize<AiStepProgressPayload>(progressSteps[3].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(4, step4!.StepIndex);
        Assert.Equal(5, step4.TotalSteps);
        Assert.Equal("DECOMPOSE_SPRINT", step4.StepName);
        Assert.Equal(75, step4.Percent);

        var step5 = JsonSerializer.Deserialize<AiStepProgressPayload>(progressSteps[4].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(5, step5!.StepIndex);
        Assert.Equal(5, step5.TotalSteps);
        Assert.Equal("FINALIZE_PROPOSAL", step5.StepName);
        Assert.Equal(100, step5.Percent);

        // Verify proposal_ready event
        var proposalReady = sseEvents.FirstOrDefault(e => e.EventType == AiSseEventTypes.ProposalReady);
        Assert.NotNull(proposalReady.EventType);
        var proposal = JsonSerializer.Deserialize<AiSprintProposalDto>(proposalReady.JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(proposal);
        Assert.Equal(2, proposal.Tasks.Count);
        Assert.Equal("task_01", proposal.Tasks[0].Id);
        Assert.Single(proposal.Tasks[0].Subtasks);

        // Verify completed event
        var completed = sseEvents.LastOrDefault(e => e.EventType == AiSseEventTypes.Completed);
        Assert.NotNull(completed.EventType);
        var completedPayload = JsonSerializer.Deserialize<AiCompletedPayload>(completed.JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(completedPayload);
        Assert.Equal(initPayload.SessionId, completedPayload.SessionId);
        Assert.Equal(2, completedPayload.TaskCount);
        Assert.Equal(13, completedPayload.TotalStoryPoints);

        // Verify proposal was staged in IStateStagingService with 2-hour TTL
        var staged = await staging.GetProposalAsync<AiSprintProposalDto>(initPayload.SessionId);
        Assert.NotNull(staged);
        Assert.Equal(proposal.SprintGoal, staged.SprintGoal);
    }

    [Fact]
    public async Task DecomposeStream_DocumentExtractionFailure_EmitsErrorAtStep1AndTerminates()
    {
        var (controller, db, _, _, _, _, _, responseBody) = CreateTestHarness();
        var project = await SeedProjectAsync(db);

        // Empty file and empty text triggers ArgumentException in text extractor
        var request = new AiDecomposeStreamRequest
        {
            ProjectId = project.Id,
            Text = "   " // empty text
        };

        await controller.DecomposeStream(request);

        var output = Encoding.UTF8.GetString(responseBody.ToArray());
        var sseEvents = ParseSseEvents(output);

        // Should emit session_init, step 1 (INGEST_DOCUMENTS), and then error (DOCUMENT_EXTRACTION_FAILED)
        Assert.Equal(3, sseEvents.Count);
        Assert.Equal(AiSseEventTypes.SessionInit, sseEvents[0].EventType);
        Assert.Equal(AiSseEventTypes.StepProgress, sseEvents[1].EventType);
        Assert.Equal(AiSseEventTypes.Error, sseEvents[2].EventType);

        var error = JsonSerializer.Deserialize<AiErrorPayload>(sseEvents[2].JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(error);
        Assert.Equal("DOCUMENT_EXTRACTION_FAILED", error.Code);
        Assert.Equal(1, error.StepIndex);
    }

    #endregion

    #region 3. Client Disconnect & Cancellation Token Handling

    [Fact]
    public async Task DecomposeStream_ClientDisconnectMidStream_CatchesOperationCanceledCleanlyWithoutCrash()
    {
        var (controller, db, _, _, _, decompService, httpContext, _) = CreateTestHarness();
        var project = await SeedProjectAsync(db);

        using var cts = new CancellationTokenSource();
        httpContext.RequestAborted = cts.Token;

        decompService.OnExecutePipeline = async ct =>
        {
            // Simulate client aborting connection midway
            cts.Cancel();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        };

        var request = new AiDecomposeStreamRequest
        {
            ProjectId = project.Id,
            Text = "Đặc tả kiểm thử disconnect"
        };

        // Should complete cleanly without throwing unhandled OperationCanceledException
        var exception = await Record.ExceptionAsync(() => controller.DecomposeStream(request));
        Assert.Null(exception);
    }

    #endregion

    #region 4. Proposal Retrieval Endpoint (/proposal/{sessionId})

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStagedProposal_NullOrWhitespaceSessionId_Returns400BadRequest(string sessionId)
    {
        var (controller, _, _, _, _, _, _, _) = CreateTestHarness();

        var result = await controller.GetStagedProposal(sessionId);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task GetStagedProposal_NonExistentSessionId_Returns404NotFound()
    {
        var (controller, _, _, _, _, _, _, _) = CreateTestHarness();

        var result = await controller.GetStagedProposal("non_existent_session_123");

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task GetStagedProposal_UserLacksReadAccess_Returns403Forbidden()
    {
        var (controller, db, permissions, _, staging, _, _, _) = CreateTestHarness();
        var project = await SeedProjectAsync(db);
        const string sessionId = "staged_session_forbidden";

        var proposal = new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = project.Id,
            SprintGoal = "Confidential Goal"
        };
        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(1));

        permissions.AllowProjectRead = false; // Forbidden

        var result = await controller.GetStagedProposal(sessionId);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }

    [Fact]
    public async Task GetStagedProposal_ValidAuthorizedSession_Returns200WithProposal()
    {
        var (controller, db, _, _, staging, _, _, _) = CreateTestHarness();
        var project = await SeedProjectAsync(db);
        const string sessionId = "staged_session_valid";

        var proposal = new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = project.Id,
            SprintGoal = "Valid Sprint Goal",
            Tasks = new List<ProposedTaskDto>
            {
                new() { Id = "task_01", Title = "Task 1" }
            }
        };
        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2));

        var result = await controller.GetStagedProposal(sessionId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<AiSprintProposalDto>(okResult.Value);
        Assert.Equal(sessionId, returned.SessionId);
        Assert.Equal("Valid Sprint Goal", returned.SprintGoal);
    }

    #endregion

    #region 5. Single-Task Regeneration Endpoint (/regenerate-task)

    [Fact]
    public async Task RegenerateTask_UnauthenticatedUser_Returns401Unauthorized()
    {
        var (controller, _, _, currentUser, _, _, _, _) = CreateTestHarness();
        currentUser.UserId = null;

        var request = new AiRegenerateTaskRequest
        {
            SessionId = "some_session",
            TaskId = "task_01",
            Feedback = "Refine task"
        };

        var result = await controller.RegenerateTask(request);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task RegenerateTask_SoftDeletedProject_Returns404NotFound()
    {
        var (controller, db, _, _, staging, _, _, _) = CreateTestHarness();
        var deletedProject = await SeedProjectAsync(db, isDeleted: true);
        const string sessionId = "regen_session_deleted_proj";

        var proposal = new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = deletedProject.Id,
            SprintGoal = "Sprint Goal",
            Tasks = new List<ProposedTaskDto>
            {
                new() { Id = "task_01", Title = "Task 1" }
            }
        };
        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2));

        var request = new AiRegenerateTaskRequest
        {
            SessionId = sessionId,
            TaskId = "task_01",
            Feedback = "Refine task"
        };

        var result = await controller.RegenerateTask(request);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task RegenerateTask_MissingProject_Returns404NotFound()
    {
        var (controller, _, _, _, staging, _, _, _) = CreateTestHarness();
        var nonExistentProjectId = Guid.NewGuid();
        const string sessionId = "regen_session_missing_proj";

        var proposal = new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = nonExistentProjectId,
            SprintGoal = "Sprint Goal",
            Tasks = new List<ProposedTaskDto>
            {
                new() { Id = "task_01", Title = "Task 1" }
            }
        };
        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2));

        var request = new AiRegenerateTaskRequest
        {
            SessionId = sessionId,
            TaskId = "task_01",
            Feedback = "Refine task"
        };

        var result = await controller.RegenerateTask(request);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task RegenerateTask_NonExistentSessionId_Returns404NotFound()
    {
        var (controller, _, _, _, _, _, _, _) = CreateTestHarness();

        var request = new AiRegenerateTaskRequest
        {
            SessionId = "expired_or_invalid_session",
            TaskId = "task_01",
            Feedback = "Thêm tiêu chí nghiệm thu bảo mật"
        };

        var result = await controller.RegenerateTask(request);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task RegenerateTask_NonExistentTaskIdInProposal_Returns404NotFound()
    {
        var (controller, db, _, _, staging, _, _, _) = CreateTestHarness();
        var project = await SeedProjectAsync(db);
        const string sessionId = "regen_task_missing_session";

        var proposal = new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = project.Id,
            SprintGoal = "Sprint Goal",
            Tasks = new List<ProposedTaskDto>
            {
                new() { Id = "task_existing_01", Title = "Existing Task" }
            }
        };
        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2));

        var request = new AiRegenerateTaskRequest
        {
            SessionId = sessionId,
            TaskId = "task_non_existent_99",
            Feedback = "Làm lại task không tồn tại"
        };

        var result = await controller.RegenerateTask(request);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task RegenerateTask_ValidSessionAndTask_UpdatesOnlyTargetTaskAndKeepsOthersIntact()
    {
        var (controller, db, _, _, staging, _, _, _) = CreateTestHarness();
        var project = await SeedProjectAsync(db);
        const string sessionId = "regen_session_valid";

        var originalTask1 = new ProposedTaskDto
        {
            Id = "task_01",
            Title = "Task 1",
            Description = "Desc 1",
            StoryPoints = 3
        };
        var originalTask2 = new ProposedTaskDto
        {
            Id = "task_02",
            Title = "Task 2 Original",
            Description = "Desc 2 Original",
            StoryPoints = 5
        };

        var proposal = new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = project.Id,
            SprintGoal = "Sprint Goal",
            Tasks = new List<ProposedTaskDto> { originalTask1, originalTask2 }
        };
        await staging.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2));

        var request = new AiRegenerateTaskRequest
        {
            SessionId = sessionId,
            TaskId = "task_01",
            Feedback = "Bổ sung kiểm thử tự động với Playwright"
        };

        var result = await controller.RegenerateTask(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify in staging that task_01 is updated, but task_02 is completely untouched
        var updatedProposal = await staging.GetProposalAsync<AiSprintProposalDto>(sessionId);
        Assert.NotNull(updatedProposal);
        Assert.Equal(2, updatedProposal.Tasks.Count);

        var updatedTask1 = updatedProposal.Tasks.First(t => t.Id == "task_01");
        Assert.Contains("(Refined)", updatedTask1.Title);
        Assert.Contains("Bổ sung kiểm thử tự động với Playwright", updatedTask1.Description);

        var untouchedTask2 = updatedProposal.Tasks.First(t => t.Id == "task_02");
        Assert.Equal("Task 2 Original", untouchedTask2.Title);
        Assert.Equal("Desc 2 Original", untouchedTask2.Description);
        Assert.Equal(5, untouchedTask2.StoryPoints);
    }

    #endregion

    #region 6. Concurrency & High Volume Rapid Requests

    [Fact]
    public async Task DecomposeStream_RapidConcurrentRequests_MaintainsSessionIsolationWithoutPollution()
    {
        var (controller, db, _, _, staging, _, _, _) = CreateTestHarness();
        var project = await SeedProjectAsync(db);

        const int concurrency = 5;
        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            var httpContext = new DefaultHttpContext();
            var responseBody = new MemoryStream();
            httpContext.Response.Body = responseBody;

            // Clone controller context per request
            var subController = new AiAgentController(
                controller.GetType().GetField("_decompositionService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controller) as IAiSprintDecompositionService ?? throw new InvalidOperationException(),
                controller.GetType().GetField("_textExtractor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controller) as IAiDocumentTextExtractor ?? throw new InvalidOperationException(),
                staging,
                db,
                controller.GetType().GetField("_projectAccess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controller) as ProjectAccess ?? throw new InvalidOperationException(),
                controller.GetType().GetField("_currentUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controller) as ICurrentUserContext ?? throw new InvalidOperationException(),
                controller.GetType().GetField("_userLookup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controller) as IUserLookupService ?? throw new InvalidOperationException(),
                NullLogger<AiAgentController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var request = new AiDecomposeStreamRequest
            {
                ProjectId = project.Id,
                Text = $"Đặc tả đồng thời luồng số {i}"
            };

            await subController.DecomposeStream(request);

            var output = Encoding.UTF8.GetString(responseBody.ToArray());
            var events = ParseSseEvents(output);
            var init = events.FirstOrDefault(e => e.EventType == AiSseEventTypes.SessionInit);
            Assert.NotNull(init.EventType);
            var payload = JsonSerializer.Deserialize<AiSessionInitPayload>(init.JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return payload!.SessionId;
        }).ToList();

        var sessionIds = await Task.WhenAll(tasks);

        // Every concurrent session must have a unique ID
        Assert.Equal(concurrency, sessionIds.Distinct().Count());

        // Every session must exist independently in staging
        foreach (var sid in sessionIds)
        {
            var staged = await staging.GetProposalAsync<AiSprintProposalDto>(sid);
            Assert.NotNull(staged);
            Assert.Equal(sid, staged.SessionId);
        }
    }

    #endregion
}
