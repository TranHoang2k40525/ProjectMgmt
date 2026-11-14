namespace ProjectMgmt.Solution.Controllers;

using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.Security;
using ProjectMgmt.Solution.Services.Ai;

[ApiController]
[Authorize]
[Route("api/ai/agent")]
public class AiAgentController : ControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAiSprintDecompositionService _decompositionService;
    private readonly IAiDocumentTextExtractor _textExtractor;
    private readonly IStateStagingService _stagingService;
    private readonly PlanningDbContext _planningDb;
    private readonly ProjectAccess _projectAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserLookupService _userLookup;
    private readonly ILogger<AiAgentController> _logger;
    private readonly IAiSprintCommitCoordinator? _commitCoordinator;
    private readonly IAiTokenUsageTracker? _tokenTracker;
    private readonly IAiConversationSessionStore? _sessionStore;

    public AiAgentController(
        IAiSprintDecompositionService decompositionService,
        IAiDocumentTextExtractor textExtractor,
        IStateStagingService stagingService,
        PlanningDbContext planningDb,
        ProjectAccess projectAccess,
        ICurrentUserContext currentUser,
        IUserLookupService userLookup,
        ILogger<AiAgentController> logger,
        IAiSprintCommitCoordinator? commitCoordinator = null,
        IAiTokenUsageTracker? tokenTracker = null,
        IAiConversationSessionStore? sessionStore = null)
    {
        _decompositionService = decompositionService;
        _textExtractor = textExtractor;
        _stagingService = stagingService;
        _planningDb = planningDb;
        _projectAccess = projectAccess;
        _currentUser = currentUser;
        _userLookup = userLookup;
        _logger = logger;
        _commitCoordinator = commitCoordinator;
        _tokenTracker = tokenTracker;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Initiates real-time Server-Sent Events (SSE) streaming for AI sprint decomposition.
    /// Emits progress steps (15%, 35%, 55%, 75%, 100%), tool calls, reasoning thoughts, and staged proposal.
    /// </summary>
    [HttpPost("decompose/stream")]
    [Consumes("multipart/form-data")]
    public async Task DecomposeStream([FromForm] AiDecomposeStreamRequest request)
    {
        var ct = HttpContext.RequestAborted;
        var stopwatch = Stopwatch.StartNew();

        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (request.ProjectId == Guid.Empty)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Connection"] = "keep-alive";

        var bufferingFeature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
        bufferingFeature?.DisableBuffering();

        async Task EmitEventAsync<T>(string eventType, T data)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                var json = JsonSerializer.Serialize(data, SseJsonOptions);
                var payload = $"event: {eventType}\ndata: {json}\n\n";
                var bytes = Encoding.UTF8.GetBytes(payload);
                await Response.Body.WriteAsync(bytes, 0, bytes.Length, ct);
                await Response.Body.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Disconnection during write/flush
            }
            catch (Exception) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                // Socket abort
            }
        }

        // Hội thoại có trạng thái: tái sử dụng phiên Redis cũ nếu client gửi kèm SessionId hợp lệ
        var requestedSessionId = request.SessionId?.Trim();
        var sessionId = !string.IsNullOrWhiteSpace(requestedSessionId) && requestedSessionId.StartsWith("sprint_ai_", StringComparison.Ordinal)
            ? requestedSessionId
            : $"sprint_ai_{Guid.NewGuid():N}";
        AiSprintProposalDto? existingProposal = null;

        try
        {
            if (request.ProjectId == Guid.Empty)
            {
                await EmitEventAsync(AiSseEventTypes.Error, new AiErrorPayload("INVALID_PROJECT_ID", "Mã dự án không hợp lệ."));
                return;
            }

            var project = await _planningDb.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId && !p.IsDeleted, ct);

            if (project == null)
            {
                await EmitEventAsync(AiSseEventTypes.Error, new AiErrorPayload("PROJECT_NOT_FOUND", "Dự án không tồn tại hoặc đã bị xóa."));
                return;
            }

            if (!await HasAiWriteAccessAsync(request.ProjectId, project.LeadUserId, userId.Value))
            {
                await EmitEventAsync(AiSseEventTypes.Error, new AiErrorPayload("FORBIDDEN", "Bạn không có quyền sử dụng Trợ Lý AI để phân tách hoặc khởi tạo công việc trong dự án này."));
                return;
            }

            var scope = new ProjectScopeContext(project.OrgId, request.ProjectId, userId.Value);
            ProjectScopeGuard.Validate(scope, project.OrgId, request.ProjectId);

            if (!string.IsNullOrWhiteSpace(requestedSessionId) && requestedSessionId.StartsWith("sprint_ai_", StringComparison.Ordinal))
            {
                existingProposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(sessionId, ct);
                // Khóa cứng phiên theo (OrganizationId, ProjectId, UserId) để không đọc nhầm ngữ cảnh
                var redisScopeMismatch = existingProposal != null &&
                    ((existingProposal.ProjectId != Guid.Empty && existingProposal.ProjectId != request.ProjectId) ||
                     (existingProposal.OrganizationId.HasValue && existingProposal.OrganizationId.Value != Guid.Empty && existingProposal.OrganizationId.Value != project.OrgId) ||
                     (existingProposal.UserId.HasValue && existingProposal.UserId.Value != Guid.Empty && existingProposal.UserId.Value != userId.Value));

                if (redisScopeMismatch)
                {
                    existingProposal = null;
                    sessionId = $"sprint_ai_{Guid.NewGuid():N}";
                }
                else if (existingProposal == null && _sessionStore != null)
                {
                    // Redis miss / hết TTL -> khôi phục từ MySQL nếu đúng chủ sở hữu
                    var (dbProposal, isForbidden) = await _sessionStore.GetProposalWithOwnershipCheckAsync(
                        sessionId,
                        userId.Value,
                        project.OrgId,
                        ct);

                    if (isForbidden || (dbProposal != null && dbProposal.ProjectId != request.ProjectId))
                    {
                        existingProposal = null;
                        sessionId = $"sprint_ai_{Guid.NewGuid():N}";
                    }
                    else if (dbProposal != null)
                    {
                        existingProposal = dbProposal;
                        await _stagingService.SaveProposalAsync(sessionId, existingProposal, TimeSpan.FromHours(2), ct);
                    }
                }
            }

            var userDisplay = await _userLookup.GetDisplayInfoAsync(userId.Value);
            var actorName = userDisplay?.DisplayName ?? "Thành viên Scrum";

            // Step 0: Emit session_init
            await EmitEventAsync(AiSseEventTypes.SessionInit, new AiSessionInitPayload(
                sessionId,
                request.ProjectId,
                actorName,
                DateTime.UtcNow));

            // Step 1: Tiếp nhận & Phân tích tài liệu (15%)
            await EmitEventAsync(AiSseEventTypes.StepProgress, new AiStepProgressPayload(
                1, 5, "INGEST_DOCUMENTS", "Tiếp nhận & Phân tích tài liệu", 15, "Đang trích xuất nội dung văn bản từ tệp và ghi chú..."));

            string extractedContent;
            try
            {
                var inputText = !string.IsNullOrWhiteSpace(request.Text)
                    ? request.Text
                    : request.Prompt;
                extractedContent = await _textExtractor.ExtractAsync(request.File, inputText, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi trích xuất tài liệu tại Step 1");
                await EmitEventAsync(AiSseEventTypes.Error, new AiErrorPayload("DOCUMENT_EXTRACTION_FAILED", ex.Message, 1));
                return;
            }

            // Steps 2 through 5: Orchestrated Decomposition Pipeline
            await _decompositionService.ExecutePipelineAsync(
                sessionId: sessionId,
                scope: scope,
                targetSprintId: request.TargetSprintId,
                extractedDocumentContent: extractedContent,
                userPrompt: request.Prompt,
                onStepProgress: async (stepIdx, stepName, labelVi, percent, detail) =>
                {
                    await EmitEventAsync(AiSseEventTypes.StepProgress, new AiStepProgressPayload(
                        stepIdx, 5, stepName, labelVi, percent, detail));
                },
                onToolCall: async (toolName, toolArgs, summaryVi, durMs) =>
                {
                    var (actionVerb, targetLabel, detailPreview) = BuildToolTraceMetadata(toolName, toolArgs, summaryVi);
                    await EmitEventAsync(AiSseEventTypes.ToolCall, new AiToolCallPayload(
                        toolName, toolArgs, summaryVi, durMs, actionVerb, targetLabel, detailPreview));
                },
                onThoughtDelta: async delta =>
                {
                    await EmitEventAsync(AiSseEventTypes.ThoughtStream, new AiThoughtDeltaPayload(
                        delta, delta.Length));
                },
                onProposalReady: async proposal =>
                {
                    proposal.OrganizationId = scope.OrganizationId;
                    proposal.ProjectId = scope.ProjectId;
                    proposal.UserId = scope.UserId;
                    await _stagingService.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2), ct);
                    await EmitEventAsync(AiSseEventTypes.ProposalReady, proposal);
                },
                model: request.Model,
                cancellationToken: ct,
                existingProposal: existingProposal);

            stopwatch.Stop();
            var stagedProposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(sessionId, ct);
            var taskCount = stagedProposal?.Tasks.Count ?? 0;
            var storyPoints = stagedProposal?.TotalStoryPoints ?? 0;
            var effectiveModel = request.Model ?? "Gemini 3.6 Flash";

            if (_sessionStore != null && stagedProposal != null)
            {
                await _sessionStore.SaveSessionStateAsync(scope, actorName, effectiveModel, stagedProposal, ct);
            }

            if (_tokenTracker != null)
            {
                var inputLen = (extractedContent?.Length ?? 0) + (request.Prompt?.Length ?? 0);
                var estPromptTokens = Math.Max(250, inputLen / 4);
                var estCompletionTokens = Math.Max(400, taskCount * 140);
                _tokenTracker.RecordUsage(new AiUsageRecord(
                    SessionId: sessionId,
                    UserId: userId.Value,
                    ProjectId: request.ProjectId,
                    Model: effectiveModel,
                    PromptTokens: estPromptTokens,
                    CompletionTokens: estCompletionTokens,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    Success: true,
                    TimestampUtc: DateTime.UtcNow,
                    OperationName: "DecomposeSprint"));

                if (stagedProposal != null)
                {
                    _tokenTracker.RecordSession(new AiSessionSummary(
                        SessionId: sessionId,
                        ProjectId: request.ProjectId,
                        CreatedByUserId: userId.Value,
                        CreatedByName: actorName,
                        SprintGoal: stagedProposal.SprintGoal,
                        TaskCount: taskCount,
                        TotalStoryPoints: storyPoints,
                        Model: effectiveModel,
                        CreatedAtUtc: DateTime.UtcNow,
                        Title: AiIndexedMemoryEngine.BuildSessionTitle(stagedProposal),
                        TurnCount: stagedProposal.ConversationHistory?.Count ?? 0,
                        MemoryCount: stagedProposal.IndexedMemories?.Count ?? 0,
                        UpdatedAtUtc: DateTime.UtcNow,
                        LastIntent: stagedProposal.Intent));
                }
            }

            await EmitEventAsync(AiSseEventTypes.Completed, new AiCompletedPayload(
                sessionId,
                stopwatch.ElapsedMilliseconds,
                taskCount,
                storyPoints));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Phiên streaming SSE {SessionId} đã bị người dùng hủy bỏ.", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi nghiêm trọng trong luồng xử lý AI Agent decompose stream {SessionId}", sessionId);
            await EmitEventAsync(AiSseEventTypes.Error, new AiErrorPayload("INTERNAL_AGENT_ERROR", "Đã xảy ra lỗi hệ thống trong quá trình phân tách AI. Vui lòng thử lại."));
        }
    }

    /// <summary>
    /// Retrieves a currently staged proposal by sessionId, optionally slicing conversation history to the latest tailLimit turns.
    /// </summary>
    [HttpGet("proposal/{sessionId}")]
    public async Task<IActionResult> GetStagedProposal(string sessionId, [FromQuery] int? tailLimit = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { code = "INVALID_SESSION_ID", message = "Mã phiên không được để trống." });
        }

        var ct = HttpContext.RequestAborted;
        var userId = _currentUser.UserId;
        var (proposal, isForbidden) = await LoadScopedProposalAsync(sessionId, userId, ct);
        if (isForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại AI này thuộc về người dùng hoặc tổ chức khác." });
        }

        if (proposal == null)
        {
            return NotFound(new { code = "PROPOSAL_EXPIRED", message = "Phiên đề xuất không tồn tại hoặc đã hết hạn." });
        }

        if (!await _projectAccess.AllowsAsync(proposal.ProjectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền truy cập." });
        }

        if (proposal.OrganizationId.HasValue && proposal.OrganizationId.Value != Guid.Empty)
        {
            var project = await _planningDb.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == proposal.ProjectId && !p.IsDeleted, ct);
            if (project == null || project.OrgId != proposal.OrganizationId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN_ORG_SCOPE", message = "Phiên hội thoại không thuộc tổ chức của dự án này." });
            }
        }

        return Ok(BuildScopedProposalView(proposal, tailLimit));
    }

    /// <summary>
    /// Tải phân trang ngược các lượt hội thoại cũ hơn theo kiểu Messenger (cursor beforeIndex + limit).
    /// </summary>
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetSessionMessages(
        string sessionId,
        [FromQuery] Guid projectId,
        [FromQuery] int? beforeIndex = null,
        [FromQuery] int limit = 12)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { code = "INVALID_SESSION_ID", message = "Mã phiên không được để trống." });
        }

        if (projectId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_PROJECT_ID", message = "Thiếu mã dự án hợp lệ." });
        }

        if (!await _projectAccess.AllowsAsync(projectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền truy cập dự án." });
        }

        var ct = HttpContext.RequestAborted;
        var userId = _currentUser.UserId;
        var (proposal, isForbidden) = await LoadScopedProposalAsync(sessionId, userId, ct);
        if (isForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại AI này thuộc về người dùng hoặc tổ chức khác." });
        }

        if (proposal == null)
        {
            return NotFound(new { code = "SESSION_NOT_FOUND", message = "Phiên hội thoại không tồn tại hoặc đã hết hạn." });
        }

        if (proposal.ProjectId != projectId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN_PROJECT_SCOPE", message = "Phiên hội thoại không thuộc dự án hiện tại." });
        }

        if (proposal.OrganizationId.HasValue && proposal.OrganizationId.Value != Guid.Empty)
        {
            var project = await _planningDb.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
            if (project == null || project.OrgId != proposal.OrganizationId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN_ORG_SCOPE", message = "Phiên hội thoại không thuộc tổ chức của dự án này." });
            }
        }

        var history = proposal.ConversationHistory ?? [];
        var totalCount = history.Count;
        var take = Math.Clamp(limit, 1, 50);
        var endExclusive = beforeIndex.HasValue ? Math.Clamp(beforeIndex.Value, 0, totalCount) : totalCount;
        var startIndex = Math.Max(0, endExclusive - take);
        var count = Math.Max(0, endExclusive - startIndex);
        var slice = count > 0 ? history.GetRange(startIndex, count) : [];

        return Ok(new AiConversationMessagesPageDto
        {
            SessionId = proposal.SessionId,
            TotalCount = totalCount,
            OldestLoadedIndex = count > 0 ? startIndex : (totalCount > 0 ? Math.Min(endExclusive, totalCount - 1) : null),
            HasOlderMessages = startIndex > 0,
            Messages = slice
        });
    }

    private static AiSprintProposalDto BuildScopedProposalView(AiSprintProposalDto source, int? tailLimit)
    {
        var history = source.ConversationHistory ?? [];
        var totalTurns = history.Count;

        List<AiConversationTurnDto> slicedHistory;
        int? oldestIdx;
        bool hasOlder;

        if (tailLimit is > 0)
        {
            var cappedLimit = Math.Clamp(tailLimit.Value, 1, 50);
            var skip = Math.Max(0, totalTurns - cappedLimit);
            slicedHistory = history.Skip(skip).ToList();
            oldestIdx = totalTurns > 0 ? skip : null;
            hasOlder = skip > 0;
        }
        else
        {
            slicedHistory = history.ToList();
            oldestIdx = totalTurns > 0 ? 0 : null;
            hasOlder = false;
        }

        return new AiSprintProposalDto
        {
            SessionId = source.SessionId,
            OrganizationId = source.OrganizationId,
            ProjectId = source.ProjectId,
            UserId = source.UserId,
            TargetSprintId = source.TargetSprintId,
            SprintGoal = source.SprintGoal,
            Rationale = source.Rationale,
            Intent = source.Intent,
            AssistantReply = source.AssistantReply,
            RecommendedTarget = source.RecommendedTarget,
            TriggerAction = source.TriggerAction,
            ConversationHistory = slicedHistory,
            IndexedMemories = source.IndexedMemories,
            Tags = source.Tags,
            TotalEstimatedHours = source.TotalEstimatedHours,
            TotalStoryPoints = source.TotalStoryPoints,
            WorkflowName = source.WorkflowName,
            InitialStatus = source.InitialStatus,
            AppliedWorkflowStatuses = source.AppliedWorkflowStatuses,
            AppliedPriorities = source.AppliedPriorities,
            AppliedIssueTypes = source.AppliedIssueTypes,
            AppliedLabels = source.AppliedLabels,
            Tasks = source.Tasks,
            TotalConversationTurns = totalTurns,
            HasOlderMessages = hasOlder,
            OldestLoadedTurnIndex = oldestIdx,
            CreatedAtUtc = source.CreatedAtUtc
        };
    }

    /// <summary>
    /// Regenerates a single task based on user refinement instructions without altering other tasks.
    /// </summary>
    [HttpPost("regenerate-task")]
    public async Task<IActionResult> RegenerateTask([FromBody] AiRegenerateTaskRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ct = HttpContext.RequestAborted;

        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Yêu cầu đăng nhập." });
        }

        var (proposal, isForbidden) = await LoadScopedProposalAsync(request.SessionId, userId, ct);
        if (isForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại AI này thuộc về thành viên hoặc tổ chức khác." });
        }

        if (proposal == null)
        {
            return NotFound(new { code = "PROPOSAL_EXPIRED", message = "Phiên đề xuất đã hết hạn. Vui lòng thực hiện phân tách lại." });
        }

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proposal.ProjectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return NotFound(new { code = "PROJECT_NOT_FOUND", message = "Dự án liên quan không tồn tại hoặc đã bị xóa." });
        }

        if (IsScopeMismatch(proposal, userId.Value, project.OrgId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại không khớp phạm vi người dùng hoặc tổ chức." });
        }

        if (!await HasAiWriteAccessAsync(proposal.ProjectId, project.LeadUserId, userId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Bạn không có quyền tinh chỉnh đề xuất công việc trên dự án này." });
        }

        if (InputGuardrail.ContainsPromptInjectionOrExfiltration(request.Feedback, out _) ||
            InputGuardrail.ContainsUnsafeScript(request.Feedback))
        {
            return BadRequest(new
            {
                code = "SECURITY_GUARDRAIL_BLOCKED",
                message = "Yêu cầu bị từ chối bởi bộ kiểm chuẩn an toàn: không cho phép ghi đè chỉ dẫn bảo mật, truy xuất bí mật hoặc chèn mã độc."
            });
        }

        var targetTask = proposal.Tasks?.FirstOrDefault(t => t.Id == request.TaskId);
        if (targetTask == null)
        {
            return NotFound(new { code = "TASK_NOT_FOUND", message = $"Không tìm thấy Task với Id '{request.TaskId}' trong đề xuất hiện tại." });
        }

        var scope = new ProjectScopeContext(project.OrgId, proposal.ProjectId, userId.Value);
        ProjectScopeGuard.Validate(scope, project.OrgId, proposal.ProjectId);

        try
        {
            var updatedTask = await _decompositionService.RegenerateSingleTaskAsync(
                proposal,
                request.TaskId,
                request.Feedback,
                scope,
                ct);

            await _stagingService.SaveProposalAsync(request.SessionId, proposal, TimeSpan.FromHours(2), ct);
            await PersistUpdatedSessionAsync(scope, proposal, ct);

            return Ok(new { updatedTask });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Thao tác làm lại Task không hợp lệ: {Message}", ex.Message);
            return NotFound(new { code = "TASK_NOT_FOUND", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tinh chỉnh lại Task {TaskId} trong phiên {SessionId}", request.TaskId, request.SessionId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "REGENERATE_FAILED", message = "Không thể làm lại task do lỗi hệ thống AI. Vui lòng thử lại." });
        }
    }

    /// <summary>
    /// Regenerates the entire sprint proposal based on user refinement instructions.
    /// </summary>
    [HttpPost("regenerate-all")]
    public async Task<IActionResult> RegenerateAll([FromBody] AiRegenerateAllRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ct = HttpContext.RequestAborted;
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Yêu cầu đăng nhập." });
        }

        var (proposal, isForbidden) = await LoadScopedProposalAsync(request.SessionId, userId, ct);
        if (isForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại AI này thuộc về thành viên hoặc tổ chức khác." });
        }

        if (proposal == null)
        {
            return NotFound(new { code = "PROPOSAL_EXPIRED", message = "Phiên đề xuất đã hết hạn. Vui lòng thực hiện phân tách lại." });
        }

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proposal.ProjectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return NotFound(new { code = "PROJECT_NOT_FOUND", message = "Dự án liên quan không tồn tại hoặc đã bị xóa." });
        }

        if (!await HasAiWriteAccessAsync(proposal.ProjectId, project.LeadUserId, userId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Bạn không có quyền tinh chỉnh đề xuất công việc trên dự án này." });
        }

        if (InputGuardrail.ContainsPromptInjectionOrExfiltration(request.Feedback, out _) ||
            InputGuardrail.ContainsUnsafeScript(request.Feedback))
        {
            return BadRequest(new
            {
                code = "SECURITY_GUARDRAIL_BLOCKED",
                message = "Yêu cầu bị từ chối bởi bộ kiểm chuẩn an toàn: không cho phép ghi đè chỉ dẫn bảo mật, truy xuất bí mật hoặc chèn mã độc."
            });
        }

        if (IsScopeMismatch(proposal, userId.Value, project.OrgId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại không khớp phạm vi người dùng hoặc tổ chức." });
        }

        var scope = new ProjectScopeContext(project.OrgId, proposal.ProjectId, userId.Value);
        ProjectScopeGuard.Validate(scope, project.OrgId, proposal.ProjectId);

        var updatedProposal = await _decompositionService.RegenerateFullProposalAsync(
            proposal,
            request.Feedback,
            scope,
            ct);

        updatedProposal.OrganizationId ??= project.OrgId;
        updatedProposal.UserId ??= userId.Value;

        await _stagingService.SaveProposalAsync(request.SessionId, updatedProposal, TimeSpan.FromHours(2), ct);
        await PersistUpdatedSessionAsync(scope, updatedProposal, ct);

        return Ok(updatedProposal);
    }

    /// <summary>
    /// Updates the staged sprint proposal (human-in-the-loop inline edits).
    /// </summary>
    [HttpPut("proposal/{sessionId}")]
    public async Task<IActionResult> UpdateStagedProposal(string sessionId, [FromBody] AiUpdateProposalRequest request)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { code = "INVALID_SESSION_ID", message = "Mã phiên không được để trống." });
        }

        var ct = HttpContext.RequestAborted;
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Yêu cầu đăng nhập." });
        }

        var (proposal, isForbidden) = await LoadScopedProposalAsync(sessionId, userId, ct);
        if (isForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại AI này thuộc về thành viên hoặc tổ chức khác." });
        }

        if (proposal == null)
        {
            return NotFound(new { code = "PROPOSAL_EXPIRED", message = "Phiên đề xuất không tồn tại hoặc đã hết hạn." });
        }

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proposal.ProjectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return NotFound(new { code = "PROJECT_NOT_FOUND", message = "Dự án liên quan không tồn tại hoặc đã bị xóa." });
        }

        if (!await HasAiWriteAccessAsync(proposal.ProjectId, project.LeadUserId, userId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Bạn không có quyền chỉnh sửa đề xuất AI của dự án này." });
        }

        if (IsScopeMismatch(proposal, userId.Value, project.OrgId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại không khớp phạm vi tổ chức hoặc người dùng." });
        }

        if (request.SprintGoal != null)
        {
            proposal.SprintGoal = InputGuardrail.StripUnsafeHtmlOrScript(request.SprintGoal);
        }
        if (request.Rationale != null)
        {
            proposal.Rationale = InputGuardrail.StripUnsafeHtmlOrScript(request.Rationale);
        }
        if (request.TargetSprintId.HasValue)
        {
            if (request.TargetSprintId.Value != Guid.Empty)
            {
                var sprintExists = await _planningDb.Sprints.AsNoTracking()
                    .AnyAsync(s => s.Id == request.TargetSprintId.Value && s.ProjectId == proposal.ProjectId && s.Status != "Completed", ct);
                if (!sprintExists)
                {
                    return BadRequest(new { code = "INVALID_TARGET_SPRINT", message = "Sprint đích không tồn tại trong dự án này hoặc đã hoàn thành." });
                }
            }
            proposal.TargetSprintId = request.TargetSprintId;
        }

        if (request.Tasks != null)
        {
            var sanitizedTasks = new List<ProposedTaskDto>();
            foreach (var rawTask in request.Tasks)
            {
                var cleanTitle = InputGuardrail.StripUnsafeHtmlOrScript(rawTask.Title);
                if (string.IsNullOrWhiteSpace(cleanTitle)) continue;

                rawTask.Title = cleanTitle.Length > 500 ? cleanTitle[..500] : cleanTitle;
                rawTask.Description = InputGuardrail.StripUnsafeHtmlOrScript(rawTask.Description);
                rawTask.EpicTitle = InputGuardrail.StripUnsafeHtmlOrScript(rawTask.EpicTitle);
                rawTask.AiRationale = InputGuardrail.StripUnsafeHtmlOrScript(rawTask.AiRationale);
                rawTask.StoryPoints = Math.Clamp(rawTask.StoryPoints <= 0 ? 1m : rawTask.StoryPoints, 1m, 13m);
                rawTask.EstimatedHours = Math.Clamp(rawTask.EstimatedHours <= 0 ? rawTask.StoryPoints * 3m : rawTask.EstimatedHours, 0.5m, 120m);
                rawTask.RemainingHours = Math.Clamp(rawTask.RemainingHours <= 0 ? rawTask.EstimatedHours : rawTask.RemainingHours, 0m, 120m);
                rawTask.AcceptanceCriteria = (rawTask.AcceptanceCriteria ?? [])
                    .Select(InputGuardrail.StripUnsafeHtmlOrScript)
                    .Where(ac => !string.IsNullOrWhiteSpace(ac))
                    .ToList();

                if (rawTask.Subtasks != null)
                {
                    var cleanSubs = new List<ProposedSubTaskDto>();
                    foreach (var sub in rawTask.Subtasks)
                    {
                        var cleanSubTitle = InputGuardrail.StripUnsafeHtmlOrScript(sub.Title);
                        if (string.IsNullOrWhiteSpace(cleanSubTitle)) continue;

                        sub.Title = cleanSubTitle.Length > 500 ? cleanSubTitle[..500] : cleanSubTitle;
                        sub.Description = InputGuardrail.StripUnsafeHtmlOrScript(sub.Description);
                        sub.StoryPoints = Math.Clamp(sub.StoryPoints <= 0 ? 1 : sub.StoryPoints, 1, 5);
                        sub.EstimatedHours = Math.Clamp(sub.EstimatedHours <= 0 ? 4m : sub.EstimatedHours, 0.5m, 80m);
                        sub.AcceptanceCriteria = (sub.AcceptanceCriteria ?? [])
                            .Select(InputGuardrail.StripUnsafeHtmlOrScript)
                            .Where(ac => !string.IsNullOrWhiteSpace(ac))
                            .ToList();
                        cleanSubs.Add(sub);
                    }
                    rawTask.Subtasks = cleanSubs;
                }

                sanitizedTasks.Add(rawTask);
            }

            proposal.Tasks = sanitizedTasks;
            proposal.TotalStoryPoints = proposal.Tasks.Sum(t => t.StoryPoints);
            proposal.TotalEstimatedHours = proposal.Tasks.Sum(t => t.EstimatedHours);
        }

        await _stagingService.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2), ct);
        {
            var scope = new ProjectScopeContext(project.OrgId, proposal.ProjectId, userId.Value);
            await PersistUpdatedSessionAsync(scope, proposal, ct);
        }
        return Ok(proposal);
    }

    /// <summary>
    /// Atomically commits the sprint proposal (Sprint Goal, Epics, Tasks, Subtasks, Acceptance Criteria) into the next planned Sprint.
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyProposal([FromBody] AiApplySprintProposalRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ct = HttpContext.RequestAborted;
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Yêu cầu đăng nhập." });
        }

        AiSprintProposalDto? stagedProposal = null;
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            var (loadedProposal, isForbidden) = await LoadScopedProposalAsync(request.SessionId, userId, ct);
            if (isForbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại AI này thuộc về thành viên hoặc tổ chức khác." });
            }
            stagedProposal = loadedProposal;
        }

        var projectId = request.ProjectId ?? stagedProposal?.ProjectId ?? Guid.Empty;
        if (projectId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_PROJECT_ID", message = "Không xác định được dự án cần áp dụng Sprint." });
        }

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return NotFound(new { code = "PROJECT_NOT_FOUND", message = "Dự án không tồn tại hoặc đã bị xóa." });
        }

        if (stagedProposal != null && IsScopeMismatch(stagedProposal, userId.Value, project.OrgId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Phiên hội thoại không khớp phạm vi người dùng hoặc tổ chức." });
        }

        var canCommitSprint = await _projectAccess.AllowsAsync(projectId, "sprint.create")
            || await _projectAccess.AllowsAsync(projectId, "project.update")
            || project.LeadUserId == userId.Value;

        var canPushBacklog = canCommitSprint || await _projectAccess.AllowsAsync(projectId, "issue.create");

        if (request.PushToBacklog ? !canPushBacklog : !canCommitSprint)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = request.PushToBacklog ? "FORBIDDEN_BACKLOG_PUSH" : "FORBIDDEN_SPRINT_COMMIT",
                message = request.PushToBacklog
                    ? "Bạn không có quyền tạo công việc trong kho Backlog của dự án này."
                    : "Chỉ Giám đốc dự án, Chủ tổ chức hoặc Scrum Master mới có quyền áp dụng kế hoạch vào Sprint."
            });
        }

        if (_commitCoordinator == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "SERVICE_UNAVAILABLE", message = "Dịch vụ commit Sprint chưa sẵn sàng." });
        }

        var lastAction = stagedProposal?.ConversationHistory?.LastOrDefault()?.ActionType;
        if (stagedProposal != null &&
            stagedProposal.Tasks.Count == 0 &&
            (lastAction is "apply_sprint_done" or "push_backlog_done"))
        {
            return Conflict(new
            {
                code = "ALREADY_COMMITTED",
                message = "Đề xuất của phiên này đã được áp dụng vào dự án trước đó. Hãy yêu cầu AI tạo đề xuất mới để tránh trùng lặp."
            });
        }

        if ((request.Tasks == null || request.Tasks.Count == 0) && stagedProposal != null)
        {
            request.Tasks = stagedProposal.Tasks;
        }
        if (string.IsNullOrWhiteSpace(request.SprintGoal) && stagedProposal != null)
        {
            request.SprintGoal = stagedProposal.SprintGoal;
        }
        if (!request.TargetSprintId.HasValue && stagedProposal?.TargetSprintId != null)
        {
            request.TargetSprintId = stagedProposal.TargetSprintId;
        }
        request.ProjectId = projectId;

        var scope = new ProjectScopeContext(project.OrgId, projectId, userId.Value);
        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        var result = await _commitCoordinator.ApplySprintProposalAsync(request, scope, ct);
        if (!result.Success)
        {
            return BadRequest(new { code = result.ErrorCode ?? "COMMIT_FAILED", message = result.Message });
        }

        // Đồng bộ lại trạng thái phiên hội thoại trên Redis để không lưu trùng nếu người dùng tiếp tục trò chuyện hoặc mở lại phiên
        if (stagedProposal != null && !string.IsNullOrWhiteSpace(request.SessionId))
        {
            stagedProposal.Tasks = [];
            stagedProposal.TotalStoryPoints = 0;
            stagedProposal.TotalEstimatedHours = 0;
            stagedProposal.TriggerAction = "none";
            stagedProposal.ConversationHistory ??= [];
            stagedProposal.ConversationHistory.Add(new AiConversationTurnDto
            {
                Id = $"assistant_commit_{DateTime.UtcNow.Ticks}",
                Role = "assistant",
                Content = result.Message,
                Intent = request.PushToBacklog ? "push_backlog" : "apply_sprint",
                ActionType = request.PushToBacklog ? "push_backlog_done" : "apply_sprint_done",
                TaskCount = result.CreatedTaskCount,
                TotalStoryPoints = result.TotalStoryPoints,
                TimestampUtc = DateTime.UtcNow
            });
            await _stagingService.SaveProposalAsync(request.SessionId, stagedProposal, TimeSpan.FromHours(2), ct);
            await PersistUpdatedSessionAsync(scope, stagedProposal, ct);
        }

        return Ok(result);
    }

    /// <summary>
    /// Clears transient staging cache and resets AI session memory.
    /// </summary>
    [HttpPost("clear-cache")]
    public async Task<IActionResult> ClearCache([FromBody] AiClearCacheRequest request)
    {
        var ct = HttpContext.RequestAborted;
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            await _stagingService.RemoveProposalAsync(request.SessionId, ct);
        }
        return Ok(new { success = true, message = "Đã xóa sạch bộ nhớ đệm cache và làm mới tri thức phiên làm việc." });
    }

    /// <summary>
    /// Retrieves token consumption statistics and KPIs for user / project.
    /// </summary>
    [HttpGet("token-usage")]
    public IActionResult GetTokenUsage([FromQuery] Guid? projectId = null)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Yêu cầu đăng nhập." });
        }

        if (_tokenTracker == null)
        {
            return Ok(new AiTokenUsageSummary(0, 0, 0, 0, 0, 100.0, 0.0, new Dictionary<string, int>(), new List<AiUsageRecord>()));
        }

        var summary = projectId.HasValue
            ? _tokenTracker.GetProjectUsageSummary(projectId.Value)
            : _tokenTracker.GetUserUsageSummary(userId.Value);

        return Ok(summary);
    }

    /// <summary>
    /// Retrieves persisted AI conversation sessions strictly scoped to (OrganizationId, ProjectId, UserId).
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetProjectSessions([FromQuery] Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_PROJECT_ID", message = "Mã dự án không hợp lệ." });
        }

        var ct = HttpContext.RequestAborted;
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Yêu cầu đăng nhập." });
        }

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return NotFound(new { code = "PROJECT_NOT_FOUND", message = "Dự án không tồn tại hoặc đã bị xóa." });
        }

        if (!await _projectAccess.AllowsAsync(projectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền truy cập." });
        }

        var scope = new ProjectScopeContext(project.OrgId, projectId, userId.Value);
        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        if (_sessionStore != null)
        {
            var dbSessions = await _sessionStore.ListUserProjectSessionsAsync(scope, 25, ct);
            return Ok(dbSessions);
        }

        var fallbackSessions = (_tokenTracker?.GetProjectSessions(projectId, 25) ?? new List<AiSessionSummary>())
            .Where(s => s.CreatedByUserId == userId.Value)
            .ToList();
        return Ok(fallbackSessions);
    }

    /// <summary>
    /// Permanently deletes an older AI conversation session owned by the current user inside the project & organization.
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId, [FromQuery] Guid projectId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { code = "INVALID_SESSION_ID", message = "Mã phiên không được để trống." });
        }

        if (projectId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_PROJECT_ID", message = "Mã dự án không hợp lệ." });
        }

        var ct = HttpContext.RequestAborted;
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Yêu cầu đăng nhập." });
        }

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return NotFound(new { code = "PROJECT_NOT_FOUND", message = "Dự án không tồn tại hoặc đã bị xóa." });
        }

        if (!await HasAiWriteAccessAsync(projectId, project.LeadUserId, userId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Chỉ thành viên có quyền chỉnh sửa công việc hoặc quản lý dự án mới được xóa phiên AI." });
        }

        var scope = new ProjectScopeContext(project.OrgId, projectId, userId.Value);
        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        var redisProposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(sessionId, ct);
        if (redisProposal != null && IsScopeMismatch(redisProposal, userId.Value, project.OrgId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN_SESSION_OWNER", message = "Không có quyền xóa phiên làm việc của thành viên hoặc tổ chức khác." });
        }

        var foundInDb = false;
        if (_sessionStore != null)
        {
            var (found, forbidden) = await _sessionStore.DeleteSessionAsync(sessionId, scope, ct);
            if (forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN_SESSION_OWNER", message = "Không có quyền xóa phiên làm việc của thành viên hoặc tổ chức khác." });
            }
            foundInDb = found;
        }

        if (!foundInDb && redisProposal == null)
        {
            return NotFound(new { code = "SESSION_NOT_FOUND", message = "Không tìm thấy phiên hội thoại trong dự án này." });
        }

        await _stagingService.RemoveProposalAsync(sessionId, ct);
        return Ok(new { success = true, message = "Đã xóa phiên hội thoại và bộ nhớ chỉ mục liên quan." });
    }

    private async Task<(AiSprintProposalDto? Proposal, bool Forbidden)> LoadScopedProposalAsync(
        string sessionId,
        Guid? userId,
        CancellationToken ct)
    {
        var proposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(sessionId, ct);
        if (proposal != null)
        {
            if (proposal.UserId.HasValue && proposal.UserId.Value != Guid.Empty &&
                userId.HasValue && proposal.UserId.Value != userId.Value)
            {
                return (null, true);
            }
            return (proposal, false);
        }

        if (_sessionStore != null && userId.HasValue && userId.Value != Guid.Empty)
        {
            var (dbProposal, forbidden) = await _sessionStore.GetProposalWithOwnershipCheckAsync(
                sessionId, userId.Value, Guid.Empty, ct);
            if (dbProposal != null)
            {
                await _stagingService.SaveProposalAsync(sessionId, dbProposal, TimeSpan.FromHours(2), ct);
            }
            return (dbProposal, forbidden);
        }

        return (null, false);
    }

    private static bool IsScopeMismatch(AiSprintProposalDto proposal, Guid currentUserId, Guid projectOrgId)
    {
        if (proposal.UserId.HasValue && proposal.UserId.Value != Guid.Empty && proposal.UserId.Value != currentUserId)
        {
            return true;
        }
        if (proposal.OrganizationId.HasValue && proposal.OrganizationId.Value != Guid.Empty && proposal.OrganizationId.Value != projectOrgId)
        {
            return true;
        }
        return false;
    }

    private async Task<bool> HasAiWriteAccessAsync(Guid projectId, Guid? leadUserId, Guid userId)
    {
        if (leadUserId.HasValue && leadUserId.Value == userId)
        {
            return true;
        }

        return await _projectAccess.AllowsAsync(projectId, "issue.create")
            || await _projectAccess.AllowsAsync(projectId, "sprint.create")
            || await _projectAccess.AllowsAsync(projectId, "project.update");
    }

    private async Task PersistUpdatedSessionAsync(IProjectScopeContext scope, AiSprintProposalDto proposal, CancellationToken ct)
    {
        if (_sessionStore == null) return;
        await _sessionStore.SaveSessionStateAsync(scope, string.Empty, string.Empty, proposal, ct);
    }

    private static (string ActionVerb, string TargetLabel, string? DetailPreview) BuildToolTraceMetadata(
        string toolName,
        object? toolArgs,
        string summaryVi)
    {
        var key = (toolName ?? string.Empty).Trim().ToLowerInvariant();
        var (verb, target) = key switch
        {
            "intent_router" => ("Phân tích", "Ngữ cảnh hội thoại & Ý định"),
            "memory_recall" => ("Trỏ trí nhớ", "Chỉ mục ghi nhớ phiên (#mem-X)"),
            "project_get_active_config" => ("Truy vấn MCP", "Cấu hình Active (Workflow, Priority, Type, Label)"),
            "backlog_get_pending_issues" => ("Truy vấn MCP", "Kho Backlog tồn đọng"),
            "spechub_get_latest_docs" => ("Truy vấn MCP", "Tài liệu Đặc tả (Spec Hub)"),
            "sprint_get_active_velocity" => ("Truy vấn MCP", "Sprint & Năng lực thực thi"),
            "team_get_member_workloads" => ("Truy vấn MCP", "Thành viên & Tải công việc"),
            "gemini_reasoner" => ("Suy luận AI", "Mô hình Trợ lý AI"),
            "domain_guardrail_audit" => ("Kiểm chuẩn", "Bộ kiểm chuẩn & Ngữ cảnh dự án"),
            _ => ("Thực thi", string.IsNullOrWhiteSpace(toolName) ? "Công cụ hệ thống" : toolName)
        };

        string? detailPreview = null;
        if (toolArgs != null)
        {
            try
            {
                var rawArgs = JsonSerializer.Serialize(toolArgs, SseJsonOptions);
                detailPreview = rawArgs.Length > 260 ? $"{rawArgs[..260]}..." : rawArgs;
            }
            catch
            {
                detailPreview = null;
            }
        }

        return (verb, target, string.IsNullOrWhiteSpace(detailPreview) ? summaryVi : $"{summaryVi} · Tham số: {detailPreview}");
    }
}
