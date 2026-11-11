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
        IAiTokenUsageTracker? tokenTracker = null)
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

        var sessionId = $"sprint_ai_{Guid.NewGuid():N}";

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

            if (!await _projectAccess.AllowsAsync(request.ProjectId, "project.read"))
            {
                await EmitEventAsync(AiSseEventTypes.Error, new AiErrorPayload("FORBIDDEN", "Bạn không có quyền truy cập vào dự án này."));
                return;
            }

            var scope = new ProjectScopeContext(project.OrgId, request.ProjectId, userId.Value);
            ProjectScopeGuard.Validate(scope, project.OrgId, request.ProjectId);

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
                    await EmitEventAsync(AiSseEventTypes.ToolCall, new AiToolCallPayload(
                        toolName, toolArgs, summaryVi, durMs));
                },
                onThoughtDelta: async delta =>
                {
                    await EmitEventAsync(AiSseEventTypes.ThoughtStream, new AiThoughtDeltaPayload(
                        delta, delta.Length));
                },
                onProposalReady: async proposal =>
                {
                    await _stagingService.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2), ct);
                    await EmitEventAsync(AiSseEventTypes.ProposalReady, proposal);
                },
                model: request.Model,
                cancellationToken: ct);

            stopwatch.Stop();
            var stagedProposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(sessionId, ct);
            var taskCount = stagedProposal?.Tasks.Count ?? 0;
            var storyPoints = stagedProposal?.TotalStoryPoints ?? 0;

            if (_tokenTracker != null)
            {
                var inputLen = (extractedContent?.Length ?? 0) + (request.Prompt?.Length ?? 0);
                var estPromptTokens = Math.Max(250, inputLen / 4);
                var estCompletionTokens = Math.Max(400, taskCount * 140);
                _tokenTracker.RecordUsage(new AiUsageRecord(
                    SessionId: sessionId,
                    UserId: userId.Value,
                    ProjectId: request.ProjectId,
                    Model: request.Model ?? "Gemini 2.5 Flash",
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
                        Model: request.Model ?? "Gemini 2.5 Flash",
                        CreatedAtUtc: DateTime.UtcNow));
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
    /// Retrieves a currently staged proposal by sessionId.
    /// </summary>
    [HttpGet("proposal/{sessionId}")]
    public async Task<IActionResult> GetStagedProposal(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { code = "INVALID_SESSION_ID", message = "Mã phiên không được để trống." });
        }

        var proposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(sessionId, HttpContext.RequestAborted);
        if (proposal == null)
        {
            return NotFound(new { code = "PROPOSAL_EXPIRED", message = "Phiên đề xuất không tồn tại hoặc đã hết hạn." });
        }

        if (!await _projectAccess.AllowsAsync(proposal.ProjectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền truy cập." });
        }

        return Ok(proposal);
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

        var proposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(request.SessionId, ct);
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

        if (!await _projectAccess.AllowsAsync(proposal.ProjectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền thao tác trên dự án này." });
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

        var proposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(request.SessionId, ct);
        if (proposal == null)
        {
            return NotFound(new { code = "PROPOSAL_EXPIRED", message = "Phiên đề xuất đã hết hạn. Vui lòng thực hiện phân tách lại." });
        }

        if (!await _projectAccess.AllowsAsync(proposal.ProjectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền thao tác trên dự án này." });
        }

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proposal.ProjectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return NotFound(new { code = "PROJECT_NOT_FOUND", message = "Dự án liên quan không tồn tại hoặc đã bị xóa." });
        }

        var scope = new ProjectScopeContext(project.OrgId, proposal.ProjectId, userId.Value);
        ProjectScopeGuard.Validate(scope, project.OrgId, proposal.ProjectId);

        var updatedProposal = await _decompositionService.RegenerateFullProposalAsync(
            proposal,
            request.Feedback,
            scope,
            ct);

        await _stagingService.SaveProposalAsync(request.SessionId, updatedProposal, TimeSpan.FromHours(2), ct);

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

        var proposal = await _stagingService.GetProposalAsync<AiSprintProposalDto>(sessionId, ct);
        if (proposal == null)
        {
            return NotFound(new { code = "PROPOSAL_EXPIRED", message = "Phiên đề xuất không tồn tại hoặc đã hết hạn." });
        }

        if (!await _projectAccess.AllowsAsync(proposal.ProjectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền thao tác." });
        }

        proposal.SprintGoal = request.SprintGoal?.Trim() ?? proposal.SprintGoal;
        if (request.Rationale != null) proposal.Rationale = request.Rationale.Trim();
        if (request.TargetSprintId.HasValue) proposal.TargetSprintId = request.TargetSprintId;
        if (request.Tasks != null && request.Tasks.Count > 0)
        {
            proposal.Tasks = request.Tasks;
            proposal.TotalStoryPoints = proposal.Tasks.Sum(t => t.StoryPoints);
            proposal.TotalEstimatedHours = proposal.Tasks.Sum(t => t.EstimatedHours);
        }

        await _stagingService.SaveProposalAsync(sessionId, proposal, TimeSpan.FromHours(2), ct);
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

        var stagedProposal = !string.IsNullOrWhiteSpace(request.SessionId)
            ? await _stagingService.GetProposalAsync<AiSprintProposalDto>(request.SessionId, ct)
            : null;

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

        var canCommitSprint = await _projectAccess.AllowsAsync(projectId, "sprint.create")
            || await _projectAccess.AllowsAsync(projectId, "project.update")
            || project.LeadUserId == userId.Value;

        if (!canCommitSprint)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "FORBIDDEN_SPRINT_COMMIT",
                message = "Chỉ Giám đốc dự án, Chủ tổ chức hoặc Scrum Master mới có quyền áp dụng kế hoạch vào Sprint."
            });
        }

        if (_commitCoordinator == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "SERVICE_UNAVAILABLE", message = "Dịch vụ commit Sprint chưa sẵn sàng." });
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

        return Ok(result);
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
    /// Retrieves recent AI sprint decomposition session summaries for a project.
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetProjectSessions([FromQuery] Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_PROJECT_ID", message = "Mã dự án không hợp lệ." });
        }

        if (!await _projectAccess.AllowsAsync(projectId, "project.read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "Không có quyền truy cập." });
        }

        var sessions = _tokenTracker?.GetProjectSessions(projectId, 15) ?? new List<AiSessionSummary>();
        return Ok(sessions);
    }
}
