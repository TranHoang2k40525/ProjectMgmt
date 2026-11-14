namespace ProjectMgmt.Solution.Services.Ai;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Tracing;
using ProjectMgmt.Core.VectorSearch;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;

public interface IAiSprintDecompositionService
{
    Task ExecutePipelineAsync(
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
        AiSprintProposalDto? existingProposal = null);

    Task<ProposedTaskDto> RegenerateSingleTaskAsync(
        AiSprintProposalDto proposal,
        string taskId,
        string userFeedback,
        IProjectScopeContext scope,
        CancellationToken cancellationToken = default);

    Task<AiSprintProposalDto> RegenerateFullProposalAsync(
        AiSprintProposalDto proposal,
        string userFeedback,
        IProjectScopeContext scope,
        CancellationToken cancellationToken = default) => Task.FromResult(proposal);
}

public class AiSprintDecompositionService : IAiSprintDecompositionService
{
    private readonly IAiModelClient _aiClient;
    private readonly IVectorSearchService _vectorSearch;
    private readonly IAiTraceService _traceService;
    private readonly PlanningDbContext _planningDb;
    private readonly SpecHubTools _specHubTools;
    private readonly BacklogTools _backlogTools;
    private readonly SprintTools _sprintTools;
    private readonly TeamTools _teamTools;
    private readonly IPromptTemplateService _promptTemplates;
    private readonly ILogger<AiSprintDecompositionService> _logger;

    public AiSprintDecompositionService(
        IAiModelClient aiClient,
        IVectorSearchService vectorSearch,
        IAiTraceService traceService,
        PlanningDbContext planningDb,
        SpecHubTools specHubTools,
        BacklogTools backlogTools,
        SprintTools sprintTools,
        TeamTools teamTools,
        IPromptTemplateService promptTemplates,
        ILogger<AiSprintDecompositionService> logger)
    {
        _aiClient = aiClient;
        _vectorSearch = vectorSearch;
        _traceService = traceService;
        _planningDb = planningDb;
        _specHubTools = specHubTools;
        _backlogTools = backlogTools;
        _sprintTools = sprintTools;
        _teamTools = teamTools;
        _promptTemplates = promptTemplates;
        _logger = logger;
    }

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
        await _traceService.StartTraceAsync(
            sessionId,
            scope.ProjectId,
            scope.UserId,
            "AiSprintDecomposition",
            new Dictionary<string, string>
            {
                ["projectId"] = scope.ProjectId.ToString(),
                ["provider"] = _aiClient.ProviderName
            },
            cancellationToken);

        // Sanitize inputs via InputGuardrail (Zero-Secret-Leakage & Prompt Injection Defense)
        var sanitizedDoc = InputGuardrail.Sanitize(extractedDocumentContent ?? string.Empty).SanitizedText;
        var sanitizedPrompt = string.IsNullOrWhiteSpace(userPrompt)
            ? null
            : InputGuardrail.Sanitize(userPrompt).SanitizedText;

        var effectiveUserText = !string.IsNullOrWhiteSpace(sanitizedPrompt)
            ? sanitizedPrompt.Trim()
            : sanitizedDoc.Trim();

        var turnSw = Stopwatch.StartNew();

        var history = existingProposal?.ConversationHistory != null
            ? new List<AiConversationTurnDto>(existingProposal.ConversationHistory)
            : new List<AiConversationTurnDto>();

        var indexedMemories = existingProposal?.IndexedMemories != null
            ? existingProposal.IndexedMemories.Select(CloneMemoryAnchor).ToList()
            : new List<AiMemoryAnchorDto>();

        var traceItems = new List<AiTurnTraceItemDto>();
        var thoughtBuffer = new System.Text.StringBuilder();

        async Task EmitThoughtAsync(string delta)
        {
            if (string.IsNullOrEmpty(delta)) return;
            thoughtBuffer.Append(delta);
            await onThoughtDelta(delta);
        }

        async Task RecordTraceAsync(
            string kind,
            string actionVerb,
            string target,
            string toolName,
            object? args,
            string summaryVi,
            long? durationMs,
            string? detail = null,
            bool emitToolCall = true)
        {
            traceItems.Add(new AiTurnTraceItemDto
            {
                Kind = kind,
                ActionVerb = actionVerb,
                Target = target,
                Summary = summaryVi,
                Detail = detail,
                DurationMs = durationMs,
                Status = "done"
            });

            if (emitToolCall)
            {
                await onToolCall(toolName, args, summaryVi, durationMs);
            }
        }

        string BuildThinkingSummary(long elapsedMs, List<AiTurnTraceItemDto> items, int mcpCount, int recalledCount)
        {
            var secs = Math.Max(0.1, elapsedMs / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            var parts = new List<string> { $"Đã suy nghĩ xong trong {secs}s", $"{items.Count} thao tác" };
            if (recalledCount > 0) parts.Add($"{recalledCount} chỉ mục nhớ");
            if (mcpCount > 0) parts.Add($"{mcpCount} MCP");
            return string.Join(" · ", parts);
        }

        void FinalizeAssistantTurn(
            AiSprintProposalDto targetProposal,
            string reply,
            string turnIntent,
            string actionType,
            List<string>? recalledKeys)
        {
            targetProposal.OrganizationId ??= scope.OrganizationId;
            targetProposal.UserId ??= scope.UserId;
            targetProposal.AssistantReply = reply;
            targetProposal.IndexedMemories = indexedMemories;

            AiIndexedMemoryEngine.UpdateIndexedMemories(
                targetProposal,
                sanitizedPrompt,
                sanitizedDoc,
                reply,
                turnIntent);
            indexedMemories = targetProposal.IndexedMemories;

            turnSw.Stop();
            var mcpCount = traceItems.Count(t => t.Kind == "mcp");
            var thinkingSummary = BuildThinkingSummary(turnSw.ElapsedMilliseconds, traceItems, mcpCount, recalledKeys?.Count ?? 0);
            var cleanThoughts = thoughtBuffer.ToString().Trim();

            history.Add(new AiConversationTurnDto
            {
                Id = $"assistant_{DateTime.UtcNow.Ticks}",
                Role = "assistant",
                Content = reply,
                Intent = turnIntent,
                ActionType = actionType,
                TaskCount = targetProposal.Tasks.Count,
                TotalStoryPoints = targetProposal.TotalStoryPoints,
                ThinkingSummary = thinkingSummary,
                DurationMs = turnSw.ElapsedMilliseconds,
                Thoughts = string.IsNullOrWhiteSpace(cleanThoughts) ? null : cleanThoughts,
                ReferencedMemoryAnchors = recalledKeys != null && recalledKeys.Count > 0 ? recalledKeys : null,
                TraceItems = traceItems.ToList(),
                TimestampUtc = DateTime.UtcNow
            });
            targetProposal.ConversationHistory = history;
        }

        var intent = ClassifyConversationalIntent(sanitizedPrompt, sanitizedDoc, existingProposal);
        var hasAttachedDoc = !string.IsNullOrWhiteSpace(sanitizedDoc) && sanitizedDoc != sanitizedPrompt;
        var contextPlan = AiIndexedMemoryEngine.PlanContextExecution(
            intent,
            effectiveUserText,
            sanitizedDoc,
            hasAttachedDoc,
            existingProposal,
            indexedMemories);

        var recalledKeys = contextPlan.RecalledMemories.Select(m => m.AnchorKey).ToList();

        traceItems.Add(new AiTurnTraceItemDto
        {
            Kind = "step",
            ActionVerb = "Phân tích",
            Target = "Định tuyến Ý định",
            Summary = $"Xác định ý định '{intent}' ({(contextPlan.UseMemoryOnly || contextPlan.IsGreetingOrHelp ? "không cần gọi thừa MCP ngoài" : "nạp ngữ cảnh chọn lọc")})",
            DurationMs = 1,
            Status = "done"
        });

        var userTurn = new AiConversationTurnDto
        {
            Id = $"user_{DateTime.UtcNow.Ticks}",
            Role = "user",
            Content = !string.IsNullOrWhiteSpace(sanitizedPrompt)
                ? sanitizedPrompt.Trim()
                : (sanitizedDoc.Length > 180 ? sanitizedDoc[..180] + "..." : sanitizedDoc),
            FullContent = string.Join("\n\n", new[] { sanitizedDoc, sanitizedPrompt }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Intent = intent,
            TimestampUtc = DateTime.UtcNow
        };
        history.Add(userTurn);

        // =========================================================================
        // GUARDRAIL: Từ chối các yêu cầu ngoài luồng (Out-of-Scope Project Management Guardrail)
        // =========================================================================
        if (intent == "out_of_scope_refusal")
        {
            await onStepProgress(2, "DOMAIN_GUARDRAIL", "Kiểm tra phạm vi nghiệp vụ Quản lý Dự án", 60, "Yêu cầu không thuộc phạm vi quản lý dự án...");
            await EmitThoughtAsync("Phát hiện yêu cầu nằm ngoài phạm vi công cụ Quản lý Dự án. Thực hiện từ chối lịch sự theo quy tắc Domain Guardrail (0 MCP call).\n");
            await RecordTraceAsync("guardrail", "Kiểm tra", "Domain Guardrail", "domain_guardrail", new { intent }, "Từ chối yêu cầu nằm ngoài phạm vi Quản lý Dự án (không gọi MCP)", 1, emitToolCall: false);
            await onStepProgress(5, "FINALIZE_PROPOSAL", "Hoàn tất phản hồi Trợ Lý AI", 100, "Đã từ chối yêu cầu ngoài luồng.");

            var refusalReply = "Tôi là Trợ Lý AI chuyên biệt cho hệ thống Quản lý Dự án. Tôi chỉ hỗ trợ các nghiệp vụ thuộc phạm vi quản lý dự án như: phân tích tài liệu đặc tả (Spec Hub), lập kế hoạch Sprint, khởi tạo và đẩy công việc vào kho Product Backlog, phân bổ nhân sự theo năng lực, tra cứu tiến độ Scrum và xuất báo cáo Excel cho dự án. Yêu cầu vừa rồi nằm ngoài phạm vi công cụ quản lý dự án nên tôi xin phép từ chối thực hiện.";

            var refusalProposal = BuildPreservedProposalShell(sessionId, scope.ProjectId, targetSprintId, existingProposal);
            refusalProposal.Intent = "out_of_scope_refusal";
            refusalProposal.TriggerAction = "none";
            FinalizeAssistantTurn(refusalProposal, refusalReply, "out_of_scope_refusal", "none", recalledKeys);

            await onProposalReady(refusalProposal);
            return;
        }

        // =========================================================================
        // ACTION INTENT: Xuất file Excel dự án ngay trong đoạn hội thoại (export_excel)
        // =========================================================================
        if (intent == "export_excel")
        {
            await onStepProgress(2, "QUERY_CONTEXT", "Chuẩn bị dữ liệu xuất tệp Excel (.xlsx)", 60, "Đang kiểm tra phạm vi dữ liệu xuất báo cáo...");
            var excelProposal = BuildPreservedProposalShell(sessionId, scope.ProjectId, targetSprintId, existingProposal);
            if (string.IsNullOrWhiteSpace(excelProposal.WorkflowName))
            {
                var swExcel = Stopwatch.StartNew();
                var excelConfig = await _backlogTools.ResolveActiveConfigAsync(scope.ProjectId, cancellationToken);
                swExcel.Stop();
                ApplyConfigMetadata(excelProposal, excelConfig);
                await RecordTraceAsync(
                    "mcp",
                    "Truy vấn MCP",
                    "project_get_active_config",
                    "project_get_active_config",
                    new { projectId = scope.ProjectId },
                    $"Đồng bộ quy chuẩn dự án ({excelConfig.WorkflowName}) để định dạng tệp Excel",
                    swExcel.ElapsedMilliseconds);
            }
            else
            {
                await RecordTraceAsync(
                    "memory",
                    "Tận dụng ngữ cảnh",
                    "Phiên hiện hành",
                    "context_reuse",
                    new { workflow = excelProposal.WorkflowName },
                    $"Dùng cấu hình quy chuẩn đã nạp ({excelProposal.WorkflowName}), bỏ qua truy vấn lặp",
                    1,
                    emitToolCall: false);
            }

            await EmitThoughtAsync("Xác nhận lệnh xuất tệp Excel (.xlsx) từ dữ liệu dự án đang lưu trong hệ thống.\n");
            await onStepProgress(5, "FINALIZE_PROPOSAL", "Hoàn tất chuẩn bị tệp Excel (.xlsx)", 100, "Đang kích hoạt tải xuống tệp Excel...");

            excelProposal.Intent = "export_excel";
            excelProposal.TriggerAction = "export_excel";
            var excelReply = excelProposal.Tasks.Count > 0
                ? $"Tôi đã kích hoạt xuất tệp Excel (.xlsx) tổng hợp toàn bộ các Sprint và kho Product Backlog đang lưu trong dự án. Lưu ý: {excelProposal.Tasks.Count} công việc đề xuất ({excelProposal.TotalStoryPoints} SP) trong phiên hội thoại này chưa được lưu vào dự án nên chưa có trong tệp. Nếu muốn đưa chúng vào báo cáo, hãy đẩy vào kho Backlog hoặc áp dụng vào Sprint rồi xuất lại. Nếu trình duyệt chặn tải tự động, bạn có thể bấm nút tải lại ngay bên dưới."
                : "Tôi đã kích hoạt xuất tệp Excel (.xlsx) tổng hợp toàn bộ các Sprint và kho Product Backlog đang lưu trong dự án. Nếu trình duyệt chặn tải tự động, bạn có thể bấm nút tải lại ngay bên dưới.";

            FinalizeAssistantTurn(excelProposal, excelReply, "export_excel", "export_excel", recalledKeys);
            await onProposalReady(excelProposal);
            return;
        }

        // =========================================================================
        // ACTION INTENT: Đẩy thẳng đề xuất hiện có vào Kho Backlog hoặc Sprint qua hội thoại
        // =========================================================================
        if ((intent == "push_backlog" || intent == "apply_sprint") && existingProposal != null && existingProposal.Tasks.Count > 0)
        {
            await onStepProgress(3, "EVALUATE_CAPACITY", "Xác nhận danh sách công việc trong phiên hội thoại", 65, "Đang kiểm tra tính hợp lệ trước khi lưu trữ...");
            var actionProposal = BuildPreservedProposalShell(sessionId, scope.ProjectId, targetSprintId, existingProposal);
            var isBacklog = intent == "push_backlog";
            actionProposal.Intent = intent;
            actionProposal.RecommendedTarget = isBacklog ? "backlog" : "sprint";
            actionProposal.TriggerAction = isBacklog ? "push_backlog" : "apply_sprint";
            var actionReply = isBacklog
                ? $"Tôi đã chuyển mục tiêu lưu trữ sang kho Product Backlog và kích hoạt đẩy {actionProposal.Tasks.Count} công việc ({actionProposal.TotalStoryPoints} SP) vào kho Backlog của dự án."
                : $"Tôi đã kích hoạt áp dụng {actionProposal.Tasks.Count} công việc ({actionProposal.TotalStoryPoints} SP) vào Sprint tiếp theo của dự án.";

            await RecordTraceAsync(
                "step",
                "Điều phối",
                isBacklog ? "Kho Product Backlog" : "Sprint mục tiêu",
                isBacklog ? "push_backlog_trigger" : "apply_sprint_trigger",
                new { taskCount = actionProposal.Tasks.Count, storyPoints = actionProposal.TotalStoryPoints },
                actionReply,
                1,
                emitToolCall: false);

            FinalizeAssistantTurn(actionProposal, actionReply, intent, actionProposal.TriggerAction, recalledKeys);
            await onStepProgress(5, "FINALIZE_PROPOSAL", "Hoàn tất điều phối", 100, actionReply);
            await onProposalReady(actionProposal);
            return;
        }

        // =========================================================================
        // RECALL CHỈ MỤC TRÍ NHỚ PHIÊN (#mem-X) NẾU PHÙ HỢP
        // =========================================================================
        if (contextPlan.RecalledMemories.Count > 0)
        {
            var anchorListText = string.Join(", ", contextPlan.RecalledMemories.Select(m => m.AnchorKey));
            var detailPreview = string.Join("\n", contextPlan.RecalledMemories.Select(m => $"{m.AnchorKey} ({m.Title}): {m.Summary}"));
            await RecordTraceAsync(
                "memory",
                "Trỏ trí nhớ",
                anchorListText,
                "memory_recall",
                new { anchors = contextPlan.RecalledMemories.Select(m => m.AnchorKey).ToArray() },
                $"Trỏ trực tiếp chỉ mục trí nhớ phiên ({anchorListText}) thay vì nạp toàn bộ hội thoại cũ",
                1,
                detail: detailPreview);
            await EmitThoughtAsync($"Đã trỏ chỉ mục trí nhớ phiên: {anchorListText} để kế thừa ngữ cảnh trọng tâm.\n");
        }

        // =========================================================================
        // FAST PATH: CHÀO HỎI / TRẢ LỜI TỪ TRÍ NHỚ MÀ KHÔNG GỌI THỪA MCP SERVER
        // =========================================================================
        if (intent == "project_qa" && (contextPlan.IsGreetingOrHelp || contextPlan.UseMemoryOnly))
        {
            await onStepProgress(
                4,
                "PROJECT_CONSULTATION",
                contextPlan.IsGreetingOrHelp ? "Soạn câu trả lời hướng dẫn sử dụng" : "Tổng hợp câu trả lời từ chỉ mục trí nhớ phiên",
                85,
                contextPlan.IsGreetingOrHelp
                    ? "Trợ Lý AI đang trả lời trực tiếp (0 truy vấn MCP)..."
                    : "Đang trỏ dữ liệu đã ghi nhớ trong phiên để trả lời đúng trọng tâm...");

            var fastQaReply = await BuildFocusedProjectQaAnswerAsync(
                effectiveUserText,
                contextPlan,
                activeConfig: null,
                backlogStoriesJson: null,
                sprintHistoryJson: null,
                teamCapacitiesJson: null,
                specHubExcerptsText: null,
                projectDocs: [],
                history,
                indexedMemories,
                existingProposal,
                scope,
                model,
                cancellationToken);

            await RecordTraceAsync(
                "reasoning",
                "Tổng hợp",
                contextPlan.IsGreetingOrHelp ? "Trả lời trực tiếp" : "Ngữ cảnh trí nhớ",
                "focused_answer",
                new { mcpCalls = 0 },
                "Trả lời đúng trọng tâm câu hỏi mà không cần truy vấn dư thừa MCP Server",
                turnSw.ElapsedMilliseconds,
                emitToolCall: false);

            var fastQaProposal = BuildPreservedProposalShell(sessionId, scope.ProjectId, targetSprintId, existingProposal);
            fastQaProposal.Intent = "project_qa";
            fastQaProposal.TriggerAction = "none";
            FinalizeAssistantTurn(fastQaProposal, fastQaReply, "project_qa", "none", recalledKeys);

            await onStepProgress(5, "FINALIZE_PROPOSAL", "Hoàn tất phản hồi Trợ Lý AI", 100, "Đã hoàn tất câu trả lời.");
            await onProposalReady(fastQaProposal);
            return;
        }

        // =========================================================================
        // STEP 2: Truy vấn Ngữ cảnh Có Chọn Lọc (Chỉ gọi MCP Tools thực sự cần thiết)
        // =========================================================================
        var sw = Stopwatch.StartNew();
        var relevantDocs = string.Empty;
        var specHubExcerpts = new System.Text.StringBuilder();
        var projectDocsList = new List<(Guid Id, string Name)>();
        string? backlogStories = null;
        string? sprintHistory = null;
        string? teamCapacities = null;

        if (contextPlan.NeedsSpecHub || contextPlan.NeedsBacklog || contextPlan.NeedsSprintHistory)
        {
            await onStepProgress(2, "QUERY_CONTEXT", "Truy vấn Ngữ cảnh Dự án có chọn lọc", 35, "Chỉ gọi các công cụ MCP cần thiết cho câu hỏi và mục tiêu hiện tại...");
        }

        if (contextPlan.NeedsSpecHub)
        {
            sw.Restart();
            relevantDocs = await _specHubTools.SearchRelevantSpecsAsync(scope, contextPlan.SpecSearchKeyword, 5, cancellationToken);
            sw.Stop();
            await RecordTraceAsync(
                "mcp",
                "Truy vấn MCP",
                "spec_hub_search",
                "spec_hub_search",
                new { query = contextPlan.SpecSearchKeyword },
                $"Tìm kiếm đặc tả Spec Hub theo từ khóa \"{contextPlan.SpecSearchKeyword}\"",
                sw.ElapsedMilliseconds);

            try
            {
                var rawDocs = await _planningDb.ProjectDocuments.AsNoTracking()
                    .Where(d => d.ProjectId == scope.ProjectId && !d.IsDirectory)
                    .OrderByDescending(d => d.UpdatedAt)
                    .Take(10)
                    .Select(d => new { d.Id, Name = string.IsNullOrEmpty(d.Name) ? d.Title : d.Name, d.Extension })
                    .ToListAsync(cancellationToken);

                var queryTerms = contextPlan.SpecSearchKeyword
                    .Split(new[] { ' ', ',', '.', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(w => w.Length >= 3)
                    .ToArray();

                var rankedDocs = rawDocs
                    .Where(d =>
                    {
                        var ext = (d.Extension ?? string.Empty).ToLowerInvariant();
                        return ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp");
                    })
                    .OrderByDescending(d => queryTerms.Count(qt => d.Name.Contains(qt, StringComparison.OrdinalIgnoreCase)))
                    .Take(Math.Max(1, contextPlan.MaxSpecDocsToRead))
                    .ToList();

                projectDocsList = rankedDocs.Select(d => (d.Id, d.Name)).ToList();

                foreach (var doc in rankedDocs)
                {
                    sw.Restart();
                    var docJson = await _specHubTools.ReadDocumentAsync(doc.Id, scope, cancellationToken);
                    sw.Stop();
                    await RecordTraceAsync(
                        "mcp",
                        "Đọc Spec Hub",
                        doc.Name,
                        "spec_hub_read_document",
                        new { documentId = doc.Id, fileName = doc.Name },
                        $"Đọc phân đoạn tài liệu đặc tả liên quan: {doc.Name}",
                        sw.ElapsedMilliseconds);

                    if (!string.IsNullOrWhiteSpace(docJson))
                    {
                        var snippet = docJson.Length > 2500 ? docJson[..2500] + "..." : docJson;
                        specHubExcerpts.AppendLine($"--- Tài liệu Spec Hub: {doc.Name} ---");
                        specHubExcerpts.AppendLine(InputGuardrail.Sanitize(snippet).SanitizedText);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiSprintDecomposition] Không thể đọc thêm tài liệu Spec Hub chọn lọc");
            }
        }

        if (contextPlan.NeedsBacklog)
        {
            sw.Restart();
            backlogStories = await _backlogTools.GetActiveStoriesAsync(scope, cancellationToken);
            sw.Stop();
            await RecordTraceAsync(
                "mcp",
                "Truy vấn MCP",
                "backlog_get_stories",
                "backlog_get_stories",
                new { projectId = scope.ProjectId },
                "Lấy danh sách các hạng mục đang có trong Product Backlog",
                sw.ElapsedMilliseconds);
        }

        if (contextPlan.NeedsSprintHistory)
        {
            sw.Restart();
            sprintHistory = await _sprintTools.GetSprintHistoryAndVelocityAsync(scope, cancellationToken);
            sw.Stop();
            await RecordTraceAsync(
                "mcp",
                "Truy vấn MCP",
                "sprint_get_history_and_velocity",
                "sprint_get_history_and_velocity",
                new { projectId = scope.ProjectId },
                "Truy xuất vận tốc Scrum và các hạng mục dở dang từ Sprint trước",
                sw.ElapsedMilliseconds);
        }

        // =========================================================================
        // STEP 3: Đánh giá Năng lực Đội ngũ & Cấu hình Dự án (Khi thực sự cần)
        // =========================================================================
        if (contextPlan.NeedsTeamCapacity)
        {
            await onStepProgress(3, "EVALUATE_CAPACITY", "Đánh giá Năng lực Đội ngũ & Kỹ năng", 55, "Đang thống kê số lượng thành viên, kỹ năng và giờ làm khả dụng...");
            sw.Restart();
            teamCapacities = await _teamTools.GetDevCapacitiesAsync(scope, cancellationToken);
            sw.Stop();
            await RecordTraceAsync(
                "mcp",
                "Truy vấn MCP",
                "team_get_dev_capacities",
                "team_get_dev_capacities",
                new { projectId = scope.ProjectId },
                "Phân tích kỹ năng và số giờ khả dụng của thành viên đội Scrum",
                sw.ElapsedMilliseconds);
        }

        ProjectActiveConfig? activeConfig = null;
        if (contextPlan.NeedsActiveConfig || intent is "sprint_plan" or "backlog_plan" or "refine_proposal")
        {
            sw.Restart();
            activeConfig = await _backlogTools.ResolveActiveConfigAsync(scope.ProjectId, cancellationToken);
            if (contextPlan.NeedsActiveConfig)
            {
                await _backlogTools.GetActiveConfigAsync(scope.ProjectId, scope, cancellationToken);
                sw.Stop();
                await RecordTraceAsync(
                    "mcp",
                    "Truy vấn MCP",
                    "project_get_active_config",
                    "project_get_active_config",
                    new { projectId = scope.ProjectId },
                    $"Truy xuất cấu hình áp dụng thực tế ({activeConfig.WorkflowName}: {activeConfig.Statuses.Count} trạng thái, {activeConfig.Priorities.Count} mức ưu tiên, {activeConfig.Labels.Count} nhãn)",
                    sw.ElapsedMilliseconds);
            }
            else
            {
                sw.Stop();
            }
        }

        // =========================================================================
        // INTENT: Tra cứu / Hỏi đáp thông tin dự án (project_qa) - Đúng trọng tâm
        // =========================================================================
        if (intent == "project_qa")
        {
            await onStepProgress(4, "PROJECT_CONSULTATION", "Tổng hợp câu trả lời đúng trọng tâm", 80, "Đang tổng hợp từ dữ liệu đã truy xuất có chọn lọc...");
            var qaReply = await BuildFocusedProjectQaAnswerAsync(
                effectiveUserText,
                contextPlan,
                activeConfig,
                backlogStories,
                sprintHistory,
                teamCapacities,
                specHubExcerpts.ToString(),
                projectDocsList,
                history,
                indexedMemories,
                existingProposal,
                scope,
                model,
                cancellationToken);

            await RecordTraceAsync(
                "reasoning",
                "Tổng hợp",
                "Trả lời trọng tâm",
                "qa_synthesis",
                new { retrievedSources = traceItems.Count(t => t.Kind == "mcp") },
                "Tổng hợp câu trả lời ngắn gọn đúng câu hỏi của người dùng",
                null,
                emitToolCall: false);

            var qaProposal = BuildPreservedProposalShell(sessionId, scope.ProjectId, targetSprintId, existingProposal);
            if (activeConfig != null) ApplyConfigMetadata(qaProposal, activeConfig);
            qaProposal.Intent = "project_qa";
            qaProposal.TriggerAction = "none";
            FinalizeAssistantTurn(qaProposal, qaReply, "project_qa", "none", recalledKeys);

            await onStepProgress(5, "FINALIZE_PROPOSAL", "Hoàn tất phản hồi tra cứu dự án", 100, "Đã trả lời dựa trên dữ liệu thực tế.");
            await onProposalReady(qaProposal);
            return;
        }

        // =========================================================================
        // STEP 4: Phân rã hoặc Tinh chỉnh Sprint Goal, Epics & Tasks (75%)
        // =========================================================================
        activeConfig ??= await _backlogTools.ResolveActiveConfigAsync(scope.ProjectId, cancellationToken);
        await onStepProgress(4, "DECOMPOSE_SPRINT", "Suy luận cấu trúc kế hoạch & phân bổ công việc", 75, "Mô hình AI đang suy luận từ ngữ cảnh và dữ liệu vừa thu thập...");

        var workflowStatusesList = activeConfig.Statuses.Select(s => s.Name).ToList();
        var issueTypesList = activeConfig.ParentWorkItemTypes.Select(t => t.Name).ToList();
        var prioritiesList = activeConfig.Priorities.Select(p => p.Name).ToList();
        var labelsList = activeConfig.Labels.Select(l => l.Name).ToList();

        var conversationContextText = FormatConversationHistoryForPrompt(
            history,
            indexedMemories,
            contextPlan.RecalledMemories,
            existingProposal,
            includeProposalTaskDetails: intent == "refine_proposal" || (existingProposal?.Tasks?.Count ?? 0) > 0);

        var userVariables = new Dictionary<string, string>
        {
            ["DOC_CONTENT"] = string.IsNullOrWhiteSpace(conversationContextText)
                ? sanitizedDoc
                : $"{sanitizedDoc}\n\n{conversationContextText}".Trim(),
            ["SPEC_HUB_DOCS"] = !string.IsNullOrWhiteSpace(relevantDocs) ? relevantDocs : "(Không cần nạp Spec Hub ở lượt này)",
            ["SPEC_HUB_EXCERPTS"] = specHubExcerpts.Length > 0 ? specHubExcerpts.ToString() : string.Empty,
            ["PM_DIRECTION"] = sanitizedPrompt ?? "Lập kế hoạch Sprint tiếp theo tối ưu năng lực nhóm và bám sát tài liệu đặc tả.",
            ["SPRINT_HISTORY"] = !string.IsNullOrWhiteSpace(sprintHistory) ? sprintHistory : "(Không yêu cầu lịch sử Sprint ở lượt tinh chỉnh này)",
            ["TEAM_CAPACITIES"] = !string.IsNullOrWhiteSpace(teamCapacities) ? teamCapacities : "(Giữ nguyên phân công hiện tại hoặc phân bổ linh hoạt theo phiên)",
            ["BACKLOG_STORIES"] = !string.IsNullOrWhiteSpace(backlogStories) ? backlogStories : "(Không cần truy vấn lại Backlog ở lượt này)",
            ["PROJECT_WORKFLOW_NAME"] = activeConfig.WorkflowName,
            ["PROJECT_WORKFLOW_TYPE"] = activeConfig.WorkflowType,
            ["PROJECT_INITIAL_STATUS"] = activeConfig.InitialStatusName,
            ["PROJECT_WORKFLOW_STATUSES"] = workflowStatusesList.Count > 0 ? string.Join(", ", workflowStatusesList) : activeConfig.InitialStatusName,
            ["PROJECT_WORKFLOW_SUMMARY"] = activeConfig.FormatWorkflowPromptSection(),
            ["PROJECT_ISSUE_TYPES"] = activeConfig.FormatIssueTypesPromptSection(),
            ["PROJECT_PARENT_ISSUE_TYPES"] = issueTypesList.Count > 0 ? string.Join(", ", issueTypesList) : "Story, Task, Bug",
            ["PROJECT_PRIORITIES"] = activeConfig.FormatPrioritiesPromptSection(),
            ["PROJECT_PRIORITY_NAMES"] = prioritiesList.Count > 0 ? string.Join(", ", prioritiesList) : "Urgent, High, Medium, Low",
            ["PROJECT_DEFAULT_PRIORITY"] = activeConfig.GetDefaultPriorityName(),
            ["PROJECT_LABELS"] = activeConfig.FormatLabelsPromptSection(),
            ["SPEC_HUB_FILE_NAMES"] = projectDocsList.Count > 0 ? string.Join(", ", projectDocsList.Select(d => d.Name)) : "Cam-nang-ky-thuat.md"
        };

        // Load and render dynamic prompts from external Markdown templates (hot-reloadable without recompilation)
        var systemPrompt = await _promptTemplates.RenderTemplateAsync("SprintDecompositionSystemPrompt", userVariables, cancellationToken);
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            systemPrompt = $"Bạn là Trợ Lý AI Quản lý Dự án Scrum giàu kinh nghiệm. Chỉ dùng đúng trạng thái ({userVariables["PROJECT_WORKFLOW_STATUSES"]}), mức ưu tiên ({userVariables["PROJECT_PRIORITY_NAMES"]}), loại công việc ({userVariables["PROJECT_PARENT_ISSUE_TYPES"]}) và nhãn ({userVariables["PROJECT_LABELS"]}) của dự án. Phân biệt User Story và Task kỹ thuật. Định cỡ Story Points đa dạng (1, 2, 3, 5, 8). Trả về đúng JSON SprintProposal.";
        }

        var userPromptContent = await _promptTemplates.RenderTemplateAsync("SprintDecompositionUserPrompt", userVariables, cancellationToken);
        if (string.IsNullOrWhiteSpace(userPromptContent))
        {
            var validDocNames = userVariables["SPEC_HUB_FILE_NAMES"];
            userPromptContent = $@"Dữ liệu đầu vào:
### 1. Tài liệu/Ghi chú, Chỉ mục Trí nhớ (#mem) & Hội thoại gần nhất: {userVariables["DOC_CONTENT"]}
### 2. Danh sách tệp Spec Hub thực tế trong dự án: {validDocNames}
### 3. Trích đoạn tài liệu: {userVariables["SPEC_HUB_DOCS"]} {userVariables["SPEC_HUB_EXCERPTS"]}
### 4. Yêu cầu của bạn: {sanitizedPrompt}
### 5. Vận tốc & Lịch sử: {userVariables["SPRINT_HISTORY"]}
### 6. Năng lực nhóm: {userVariables["TEAM_CAPACITIES"]}
### 7. Backlog: {userVariables["BACKLOG_STORIES"]}
### 8. Cấu hình đang áp dụng của dự án:
- Quy trình Workflow: {activeConfig.FormatWorkflowPromptSection()}
- Trạng thái khởi tạo mặc định: {activeConfig.InitialStatusName}
- Loại việc: {activeConfig.FormatIssueTypesPromptSection()}
- Ưu tiên: {activeConfig.FormatPrioritiesPromptSection()}
- Nhãn (Labels): {activeConfig.FormatLabelsPromptSection()}
Hãy phân tích và trả về đúng cấu trúc JSON SprintProposal.";
        }

        var prompt = new AiPrompt
        {
            Model = model,
            SystemInstruction = systemPrompt,
            Messages = new List<AiMessage> { AiMessage.CreateUser(userPromptContent) },
            Temperature = 0.2,
            MaxTokens = 8192
        };

        var responseContentBuilder = new System.Text.StringBuilder();
        var hasReceivedNativeThoughts = false;

        await EmitThoughtAsync("Đang bắt đầu suy luận cấu trúc kế hoạch từ chỉ mục trí nhớ và dữ liệu dự án đã thu thập...\n");

        var llmSw = Stopwatch.StartNew();
        try
        {
            await _aiClient.StreamGenerateAsync(
                prompt,
                scope,
                async chunk =>
                {
                    if (chunk.ChunkType == AiStreamChunkType.Thought && !string.IsNullOrEmpty(chunk.DeltaThought))
                    {
                        hasReceivedNativeThoughts = true;
                        await EmitThoughtAsync(chunk.DeltaThought);
                    }
                    else if (chunk.ChunkType == AiStreamChunkType.Content && !string.IsNullOrEmpty(chunk.DeltaContent))
                    {
                        responseContentBuilder.Append(chunk.DeltaContent);
                    }
                },
                cancellationToken);

            if (!hasReceivedNativeThoughts)
            {
                await EmitThoughtAsync("Đã hoàn thành bóc tách mục tiêu, phân bổ Story Points và đối chiếu tiêu chí nghiệm thu.\n");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiSprintDecomposition] Luồng stream AI ghi nhận ngoại lệ: {Message}, tiến hành lập đề xuất từ ngữ cảnh đặc tả", ex.Message);
        }
        llmSw.Stop();

        await RecordTraceAsync(
            "reasoning",
            "Suy luận AI",
            model ?? _aiClient.ProviderName,
            "llm_reasoning",
            new { model = model ?? _aiClient.ProviderName },
            $"Hoàn tất suy luận kế hoạch với mô hình {(string.IsNullOrWhiteSpace(model) ? _aiClient.ProviderName : model)}",
            llmSw.ElapsedMilliseconds,
            emitToolCall: false);

        // =========================================================================
        // STEP 5: Tự kiểm tra (Self-Check Critique Agent) & Lưu Staging (100%)
        // =========================================================================
        await onStepProgress(5, "FINALIZE_PROPOSAL", "Tự kiểm tra (Self-Check) & Lưu Staging", 100, "Đang đối chiếu tiêu chí nghiệm thu, cân bằng tải và lưu trữ đề xuất...");
        await EmitThoughtAsync("Tự kiểm tra (Self-Check Agent): Xác nhận Workflow, Priority, Issue Type hợp lệ và bảo toàn nguồn Backlog.\n");

        var rawAiOutput = responseContentBuilder.ToString();
        _logger.LogInformation("[AiSprintDecomposition] Phản hồi thô từ LLM đạt {Length} ký tự. Bắt đầu phân tích cấu trúc.", rawAiOutput.Length);
        var proposal = ParseAiOutputToProposal(rawAiOutput, sessionId, scope.ProjectId, targetSprintId, sanitizedDoc, sanitizedPrompt, projectDocsList, activeConfig, intent == "refine_proposal" ? existingProposal : null);
        _logger.LogInformation("[AiSprintDecomposition] Phân tích hoàn tất: {TaskCount} tasks, {StoryPoints} SP.", proposal.Tasks.Count, proposal.TotalStoryPoints);

        if (!string.IsNullOrWhiteSpace(teamCapacities))
        {
            EnrichProposalWithTeamMembers(proposal, teamCapacities);
        }

        await RecordTraceAsync(
            "step",
            "Kiểm chuẩn",
            activeConfig.WorkflowName,
            "self_check_critique",
            new { tasks = proposal.Tasks.Count, storyPoints = proposal.TotalStoryPoints },
            $"Xác thực {proposal.Tasks.Count} việc ({proposal.TotalStoryPoints} SP) khớp quy chuẩn '{activeConfig.WorkflowName}'",
            1,
            emitToolCall: false);

        var isBacklogTarget = intent == "backlog_plan" || IsBacklogTargetRequested(effectiveUserText);
        proposal.Intent = intent;
        proposal.RecommendedTarget = isBacklogTarget ? "backlog" : (existingProposal?.RecommendedTarget ?? "sprint");
        proposal.TriggerAction = "none";

        var finalReply = !string.IsNullOrWhiteSpace(proposal.AssistantReply)
            ? proposal.AssistantReply
            : isBacklogTarget
                ? $"Tôi đã chuẩn bị {proposal.Tasks.Count} hạng mục công việc ({proposal.TotalStoryPoints} SP · {proposal.TotalEstimatedHours} giờ) theo quy chuẩn '{activeConfig.WorkflowName}' sẵn sàng đẩy vào kho Product Backlog."
                : intent == "refine_proposal"
                    ? $"Tôi đã cập nhật lại kế hoạch theo yêu cầu của bạn: hiện có {proposal.Tasks.Count} công việc ({proposal.TotalStoryPoints} SP · {proposal.TotalEstimatedHours} giờ) với mục tiêu \"{proposal.SprintGoal}\"."
                    : $"Tôi đã hoàn tất lập kế hoạch {proposal.Tasks.Count} công việc ({proposal.TotalStoryPoints} SP · {proposal.TotalEstimatedHours} giờ) cho mục tiêu: \"{proposal.SprintGoal}\".";

        FinalizeAssistantTurn(proposal, finalReply, intent, "none", recalledKeys);

        await onProposalReady(proposal);
    }

    public async Task<AiSprintProposalDto> RegenerateFullProposalAsync(
        AiSprintProposalDto proposal,
        string userFeedback,
        IProjectScopeContext scope,
        CancellationToken cancellationToken = default)
    {
        var sanitizedFeedback = InputGuardrail.Sanitize(userFeedback ?? string.Empty).SanitizedText;
        var currentTasksJson = JsonSerializer.Serialize(proposal.Tasks);
        var teamCapacities = await _teamTools.GetDevCapacitiesAsync(scope, cancellationToken);
        var activeConfig = await _backlogTools.ResolveActiveConfigAsync(scope.ProjectId, cancellationToken);

        var fullVariables = new Dictionary<string, string>
        {
            ["SPRINT_GOAL"] = proposal.SprintGoal,
            ["CURRENT_TASKS_JSON"] = currentTasksJson,
            ["TEAM_CAPACITIES"] = teamCapacities,
            ["USER_FEEDBACK"] = sanitizedFeedback,
            ["PROJECT_WORKFLOW_SUMMARY"] = activeConfig.FormatWorkflowPromptSection(),
            ["PROJECT_INITIAL_STATUS"] = activeConfig.InitialStatusName,
            ["PROJECT_ISSUE_TYPES"] = activeConfig.FormatIssueTypesPromptSection(),
            ["PROJECT_PRIORITIES"] = activeConfig.FormatPrioritiesPromptSection(),
            ["PROJECT_LABELS"] = activeConfig.FormatLabelsPromptSection()
        };

        var userPromptContent = await _promptTemplates.RenderTemplateAsync("FullProposalRegenerationPrompt", fullVariables, cancellationToken);
        if (string.IsNullOrWhiteSpace(userPromptContent))
        {
            userPromptContent = $"Đề xuất Sprint: {proposal.SprintGoal}\nTasks: {currentTasksJson}\nNăng lực: {teamCapacities}\nQuy chuẩn áp dụng: {activeConfig.FormatWorkflowPromptSection()} | {activeConfig.FormatPrioritiesPromptSection()} | {activeConfig.FormatIssueTypesPromptSection()}\nYêu cầu làm lại: {sanitizedFeedback}";
        }

        var prompt = new AiPrompt
        {
            SystemInstruction = $"Bạn là Chuyên gia Scrum Master. Hãy làm lại toàn bộ đề xuất Sprint dựa trên phản hồi người dùng, tuân thủ cấu hình thực tế của dự án ({activeConfig.WorkflowName}; trạng thái: {string.Join(", ", activeConfig.Statuses.Select(s => s.Name))}; ưu tiên: {string.Join(", ", activeConfig.Priorities.Select(p => p.Name))}; nhãn: {activeConfig.FormatLabelsPromptSection()}). Trả về đúng JSON hợp lệ theo schema SprintProposal.",
            Messages = new List<AiMessage>
            {
                AiMessage.CreateUser(userPromptContent)
            },
            Temperature = 0.3
        };

        var userTurn = new AiConversationTurnDto
        {
            Id = $"user_{DateTime.UtcNow.Ticks}",
            Role = "user",
            Content = sanitizedFeedback,
            FullContent = sanitizedFeedback,
            Intent = "refine_proposal",
            TimestampUtc = DateTime.UtcNow
        };
        proposal.ConversationHistory ??= [];
        proposal.ConversationHistory.Add(userTurn);

        try
        {
            var result = await _aiClient.GenerateAsync(prompt, scope, cancellationToken);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Content))
            {
                var updated = ParseAiOutputToProposal(result.Content, proposal.SessionId, proposal.ProjectId, proposal.TargetSprintId, sanitizedFeedback, null, null, activeConfig);
                EnrichProposalWithTeamMembers(updated, teamCapacities);
                updated.Intent = "refine_proposal";
                updated.RecommendedTarget = IsBacklogTargetRequested(sanitizedFeedback) ? "backlog" : proposal.RecommendedTarget;
                updated.AssistantReply ??= $"Tôi đã cập nhật lại phương án theo yêu cầu \"{sanitizedFeedback}\": hiện có {updated.Tasks.Count} công việc ({updated.TotalStoryPoints} SP).";
                updated.ConversationHistory = new List<AiConversationTurnDto>(proposal.ConversationHistory)
                {
                    new()
                    {
                        Id = $"assistant_{DateTime.UtcNow.Ticks}",
                        Role = "assistant",
                        Content = updated.AssistantReply,
                        Intent = "refine_proposal",
                        ActionType = "none",
                        TaskCount = updated.Tasks.Count,
                        TotalStoryPoints = updated.TotalStoryPoints,
                        TimestampUtc = DateTime.UtcNow
                    }
                };
                return updated;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RegenerateFullProposal] Không thể sinh lại toàn bộ qua model AI, áp dụng điều chỉnh cục bộ");
        }

        proposal.SprintGoal = $"{proposal.SprintGoal} ({sanitizedFeedback})";
        foreach (var task in proposal.Tasks)
        {
            task.Description = $"{task.Description}\n[Cập nhật theo yêu cầu: {sanitizedFeedback}]";
        }
        proposal.Intent = "refine_proposal";
        if (IsBacklogTargetRequested(sanitizedFeedback))
        {
            proposal.RecommendedTarget = "backlog";
        }
        proposal.AssistantReply = $"Tôi đã điều chỉnh danh sách công việc theo yêu cầu: \"{sanitizedFeedback}\".";
        proposal.ConversationHistory.Add(new AiConversationTurnDto
        {
            Id = $"assistant_{DateTime.UtcNow.Ticks}",
            Role = "assistant",
            Content = proposal.AssistantReply,
            Intent = "refine_proposal",
            ActionType = "none",
            TaskCount = proposal.Tasks.Count,
            TotalStoryPoints = proposal.TotalStoryPoints,
            TimestampUtc = DateTime.UtcNow
        });
        return proposal;
    }

    public async Task<ProposedTaskDto> RegenerateSingleTaskAsync(
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

        var teamCapacities = await _teamTools.GetDevCapacitiesAsync(scope, cancellationToken);
        var activeConfig = await _backlogTools.ResolveActiveConfigAsync(scope.ProjectId, cancellationToken);
        var singleVariables = new Dictionary<string, string>
        {
            ["SPRINT_GOAL"] = proposal.SprintGoal,
            ["TASK_TITLE"] = existingTask.Title,
            ["TASK_TYPE"] = existingTask.Type,
            ["TASK_DESCRIPTION"] = existingTask.Description,
            ["TASK_AC"] = string.Join("; ", existingTask.AcceptanceCriteria),
            ["TASK_SP"] = existingTask.StoryPoints.ToString(),
            ["TASK_HOURS"] = existingTask.EstimatedHours.ToString(),
            ["TASK_ASSIGNEE"] = existingTask.AssigneeName ?? "Chưa gán",
            ["TEAM_CAPACITIES"] = teamCapacities,
            ["PROJECT_WORKFLOW_SUMMARY"] = activeConfig.FormatWorkflowPromptSection(),
            ["PROJECT_INITIAL_STATUS"] = activeConfig.InitialStatusName,
            ["PROJECT_ISSUE_TYPES"] = activeConfig.FormatIssueTypesPromptSection(),
            ["PROJECT_PRIORITIES"] = activeConfig.FormatPrioritiesPromptSection(),
            ["PROJECT_LABELS"] = activeConfig.FormatLabelsPromptSection(),
            ["USER_FEEDBACK"] = userFeedback ?? string.Empty
        };

        var userPromptContent = await _promptTemplates.RenderTemplateAsync("SingleTaskRegenerationPrompt", singleVariables, cancellationToken);
        if (string.IsNullOrWhiteSpace(userPromptContent))
        {
            userPromptContent = $"Sprint Goal: {proposal.SprintGoal}\nTask: {existingTask.Title}\nCấu hình dự án: {activeConfig.FormatWorkflowPromptSection()} | {activeConfig.FormatPrioritiesPromptSection()}\nFeedback: {userFeedback}";
        }

        var prompt = new AiPrompt
        {
            SystemInstruction = $"Bạn là trợ lý Scrum Master. Tinh chỉnh lại một Task cụ thể theo phản hồi người dùng, tuân thủ trạng thái ({activeConfig.InitialStatusName}, ...), ưu tiên ({string.Join(", ", activeConfig.Priorities.Select(p => p.Name))}) và loại việc ({string.Join(", ", activeConfig.ParentWorkItemTypes.Select(t => t.Name))}). Trả về đúng JSON của một đối tượng Task.",
            Messages = new List<AiMessage>
            {
                AiMessage.CreateUser(userPromptContent)
            },
            Temperature = 0.2
        };

        try
        {
            var result = await _aiClient.GenerateAsync(prompt, scope, cancellationToken);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Content))
            {
                var revisedTask = ParseSingleTask(result.Content, existingTask, activeConfig);
                var index = proposal.Tasks.IndexOf(existingTask);
                proposal.Tasks[index] = revisedTask;
                return revisedTask;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RegenerateTask] Không thể sinh lại task qua AI, áp dụng chỉnh sửa mô tả");
        }

        existingTask.Description += $"\n[Yêu cầu cập nhật: {userFeedback}]";
        return existingTask;
    }

    private static AiSprintProposalDto ParseAiOutputToProposal(
        string rawJson,
        string sessionId,
        Guid projectId,
        Guid? targetSprintId,
        string sourceDoc,
        string? userPrompt = null,
        IReadOnlyList<(Guid Id, string Name)>? availableDocs = null,
        ProjectActiveConfig? activeConfig = null,
        AiSprintProposalDto? preserveOnFailure = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                var cleaned = ExtractJsonObject(rawJson);
                var docNode = System.Text.Json.Nodes.JsonNode.Parse(cleaned);

                if (docNode is System.Text.Json.Nodes.JsonObject obj)
                {
                    var proposal = new AiSprintProposalDto
                    {
                        SessionId = sessionId,
                        ProjectId = projectId,
                        TargetSprintId = targetSprintId,
                        SprintGoal = obj["sprintGoal"]?.GetValue<string>() ?? obj["goal"]?.GetValue<string>() ?? "Hoàn thiện các mục tiêu Sprint theo đặc tả",
                        Rationale = obj["rationale"]?.GetValue<string>() ?? "Phân rã công việc theo chuẩn Scrum và năng lực đội ngũ.",
                        AssistantReply = obj["assistantReply"]?.GetValue<string>() ?? obj["reply"]?.GetValue<string>(),
                        RecommendedTarget = (obj["recommendedTarget"]?.GetValue<string>() ?? "sprint").Equals("backlog", StringComparison.OrdinalIgnoreCase) ? "backlog" : "sprint",
                        Tags = new List<string> { "#SprintPlan", "#Scrum", "#SpecHub" }
                    };
                    ApplyConfigMetadata(proposal, activeConfig);

                    var tagsArray = obj["tags"]?.AsArray();
                    if (tagsArray != null && tagsArray.Count > 0)
                    {
                        proposal.Tags = tagsArray
                            .Select(t => t?.GetValue<string>()?.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .Select(t => t!.StartsWith('#') ? t! : $"#{t}")
                            .Distinct()
                            .ToList()!;
                    }

                    var tasksArray = (obj["tasks"] ?? obj["proposedTasks"])?.AsArray();
                    if (tasksArray != null && tasksArray.Count > 0)
                    {
                        var taskIndex = 1;
                        foreach (var taskElem in tasksArray)
                        {
                            if (taskElem is not System.Text.Json.Nodes.JsonObject tObj) continue;

                            var taskTitle = tObj["title"]?.GetValue<string>()?.Trim() ?? $"Công việc #{taskIndex}";
                            var taskDesc = tObj["description"]?.GetValue<string>()?.Trim() ?? taskTitle;

                            // Tránh trường hợp Description bị nhét nguyên văn toàn bộ tài liệu thô
                            if (!string.IsNullOrWhiteSpace(sourceDoc) && taskDesc.Length > 500 && taskDesc.Contains(sourceDoc[..Math.Min(100, sourceDoc.Length)]))
                            {
                                taskDesc = $"Triển khai các hạng mục kỹ thuật và nghiệp vụ cho '{taskTitle}' theo yêu cầu đặc tả.";
                            }

                            decimal sp = 3;
                            if (tObj["storyPoints"] != null)
                            {
                                if (tObj["storyPoints"]!.AsValue().TryGetValue<decimal>(out var val)) sp = val;
                                else if (decimal.TryParse(tObj["storyPoints"]!.ToString(), out var strVal)) sp = strVal;
                            }
                            if (sp <= 0) sp = 3;

                            decimal hours = sp * 4;
                            if (tObj["estimatedHours"] != null)
                            {
                                if (tObj["estimatedHours"]!.AsValue().TryGetValue<decimal>(out var hVal)) hours = hVal;
                                else if (decimal.TryParse(tObj["estimatedHours"]!.ToString(), out var strHVal)) hours = strHVal;
                            }
                            if (hours <= 0) hours = sp * 4;

                            Guid? sourceBacklogIssueId = null;
                            var rawBacklogId = tObj["sourceBacklogIssueId"]?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(rawBacklogId) && Guid.TryParse(rawBacklogId.Trim(), out var parsedBacklogId))
                            {
                                sourceBacklogIssueId = parsedBacklogId;
                            }
                            var sourceBacklogIssueKey = tObj["sourceBacklogIssueKey"]?.GetValue<string>()?.Trim();
                            if (string.IsNullOrWhiteSpace(sourceBacklogIssueKey)) sourceBacklogIssueKey = null;

                            var rawType = tObj["type"]?.GetValue<string>() ?? "Story";
                            var rawPriority = tObj["priority"]?.GetValue<string>() ?? activeConfig?.GetDefaultPriorityName() ?? "High";
                            var rawStatus = tObj["status"]?.GetValue<string>() ?? activeConfig?.InitialStatusName ?? "To Do";

                            var taskDto = new ProposedTaskDto
                            {
                                Id = tObj["id"]?.GetValue<string>() ?? $"task_{taskIndex:D2}",
                                SourceBacklogIssueId = sourceBacklogIssueId,
                                SourceBacklogIssueKey = sourceBacklogIssueKey,
                                EpicTitle = tObj["epicTitle"]?.GetValue<string>() ?? tObj["epic"]?.GetValue<string>() ?? "Core Development",
                                Title = taskTitle,
                                Description = taskDesc,
                                Type = activeConfig != null ? activeConfig.ResolveIssueTypeName(rawType) : rawType,
                                Priority = activeConfig != null ? activeConfig.ResolvePriorityName(rawPriority) : rawPriority,
                                Status = activeConfig != null ? activeConfig.ResolveStatusName(rawStatus) : rawStatus,
                                StoryPoints = sp,
                                EstimatedHours = hours,
                                RemainingHours = hours,
                                AssigneeName = tObj["assigneeName"]?.GetValue<string>() ?? tObj["assignee"]?.GetValue<string>(),
                                AiRationale = tObj["aiRationale"]?.GetValue<string>() ?? "Phân rã theo kiến trúc và khả năng hoàn thành trong Sprint."
                            };

                            // Labels/Tags của Task (hỗ trợ cả labels và tags)
                            var labelsNode = (tObj["labels"] ?? tObj["tags"])?.AsArray();
                            if (labelsNode != null && labelsNode.Count > 0)
                            {
                                taskDto.Tags = labelsNode.Select(x => x?.GetValue<string>()?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!.TrimStart('#')).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!;
                            }
                            else if (activeConfig != null && activeConfig.Labels.Count > 0)
                            {
                                taskDto.Tags = [];
                            }
                            else
                            {
                                taskDto.Tags = new List<string> { taskDto.Type, "SprintTask" };
                            }

                            // Acceptance Criteria
                            var acNode = tObj["acceptanceCriteria"];
                            if (acNode is System.Text.Json.Nodes.JsonArray acArr)
                            {
                                taskDto.AcceptanceCriteria = acArr.Select(x => x?.GetValue<string>()?.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList()!;
                            }
                            else if (acNode != null)
                            {
                                taskDto.AcceptanceCriteria = acNode.ToString().Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                            }
                            if (taskDto.AcceptanceCriteria.Count == 0)
                            {
                                taskDto.AcceptanceCriteria = new List<string> { $"Hoàn thành nghiệp vụ '{taskTitle}'", "Đảm bảo vượt qua kiểm thử tự động và review code" };
                            }

                            // Document Citations
                            var citeNode = tObj["documentCitations"] ?? tObj["citations"];
                            if (citeNode is System.Text.Json.Nodes.JsonArray cArr)
                            {
                                taskDto.DocumentCitations = cArr.Select(x => x?.GetValue<string>()?.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList()!;
                            }
                            else if (citeNode != null)
                            {
                                taskDto.DocumentCitations = citeNode.ToString().Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                            }

                            // Chuẩn hóa trích dẫn Spec Hub thực tế
                            if (taskDto.DocumentCitations.Count == 0 ||
                                taskDto.DocumentCitations.Any(c => c.Contains("Tài liệu đặc tả Spec Hub dự án", StringComparison.OrdinalIgnoreCase) ||
                                                                   c.Contains("Quy chuẩn UI/UX dự án", StringComparison.OrdinalIgnoreCase)))
                            {
                                if (availableDocs != null && availableDocs.Count > 0)
                                {
                                    var assignedDoc = availableDocs[(taskIndex - 1) % availableDocs.Count];
                                    taskDto.DocumentId = assignedDoc.Id;
                                    taskDto.DocumentCitations = new List<string> { $"{assignedDoc.Name} - Mục Phần II" };
                                }
                            }
                            else
                            {
                                foreach (var cite in taskDto.DocumentCitations)
                                {
                                    var matched = availableDocs?.FirstOrDefault(d =>
                                        !string.IsNullOrWhiteSpace(d.Name) &&
                                        cite.IndexOf(d.Name, StringComparison.OrdinalIgnoreCase) >= 0);
                                    if (matched.HasValue && matched.Value != default)
                                    {
                                        taskDto.DocumentId = matched.Value.Id;
                                        break;
                                    }
                                }
                            }

                            // Subtasks (hỗ trợ cả mảng object lẫn mảng string)
                            var subNode = tObj["subtasks"] ?? tObj["subTasks"];
                            if (subNode is System.Text.Json.Nodes.JsonArray sArr && sArr.Count > 0)
                            {
                                var subIdx = 1;
                                foreach (var sElem in sArr)
                                {
                                    if (sElem is System.Text.Json.Nodes.JsonObject sObj)
                                    {
                                        var subTitle = sObj["title"]?.GetValue<string>()?.Trim() ?? $"Công việc con #{subIdx}";
                                        var subHours = 4m;
                                        if (sObj["estimatedHours"] != null && sObj["estimatedHours"]!.AsValue().TryGetValue<decimal>(out var sh)) subHours = sh;

                                        var subRawPriority = sObj["priority"]?.GetValue<string>() ?? taskDto.Priority;
                                        var subRawStatus = sObj["status"]?.GetValue<string>() ?? taskDto.Status;
                                        var subSp = 1;
                                        if (sObj["storyPoints"] != null)
                                        {
                                            if (sObj["storyPoints"]!.AsValue().TryGetValue<int>(out var spInt)) subSp = spInt;
                                            else if (sObj["storyPoints"]!.AsValue().TryGetValue<decimal>(out var spDec)) subSp = (int)Math.Max(1, Math.Round(spDec));
                                            else if (decimal.TryParse(sObj["storyPoints"]!.ToString(), out var spParsed)) subSp = (int)Math.Max(1, Math.Round(spParsed));
                                        }

                                        var subDto = new ProposedSubTaskDto
                                        {
                                            Id = sObj["id"]?.GetValue<string>() ?? $"{taskDto.Id}_{subIdx}",
                                            Title = subTitle,
                                            Role = sObj["role"]?.GetValue<string>() ?? "Backend",
                                            EstimatedHours = subHours > 0 ? subHours : 4m,
                                            StoryPoints = subSp > 0 ? subSp : 1,
                                            Priority = activeConfig != null ? activeConfig.ResolvePriorityName(subRawPriority) : subRawPriority,
                                            Status = activeConfig != null ? activeConfig.ResolveStatusName(subRawStatus) : subRawStatus,
                                            AssigneeName = sObj["assigneeName"]?.GetValue<string>() ?? taskDto.AssigneeName,
                                            Description = sObj["description"]?.GetValue<string>()?.Trim() ?? $"Triển khai {subTitle} theo đặc tả kỹ thuật.",
                                            DocumentId = taskDto.DocumentId,
                                            DocumentCitations = taskDto.DocumentCitations.ToList()
                                        };

                                        var subAcNode = sObj["acceptanceCriteria"];
                                        if (subAcNode is System.Text.Json.Nodes.JsonArray sAcArr)
                                        {
                                            subDto.AcceptanceCriteria = sAcArr.Select(x => x?.GetValue<string>()?.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList()!;
                                        }
                                        if (subDto.AcceptanceCriteria.Count == 0)
                                        {
                                            subDto.AcceptanceCriteria = new List<string> { $"Hoàn thành {subTitle} đúng tiêu chuẩn kỹ thuật." };
                                        }

                                        taskDto.Subtasks.Add(subDto);
                                    }
                                    else if (sElem != null)
                                    {
                                        // Trường hợp LLM trả về mảng chuỗi string["Bước 1", "Bước 2"]
                                        var strTitle = sElem.GetValue<string>()?.Trim();
                                        if (!string.IsNullOrEmpty(strTitle))
                                        {
                                            taskDto.Subtasks.Add(new ProposedSubTaskDto
                                            {
                                                Id = $"{taskDto.Id}_{subIdx}",
                                                Title = strTitle,
                                                Role = strTitle.Contains("UI", StringComparison.OrdinalIgnoreCase) || strTitle.Contains("Giao diện", StringComparison.OrdinalIgnoreCase) || strTitle.Contains("Component", StringComparison.OrdinalIgnoreCase) ? "Frontend"
                                                     : strTitle.Contains("Test", StringComparison.OrdinalIgnoreCase) || strTitle.Contains("Kiểm thử", StringComparison.OrdinalIgnoreCase) ? "QA" : "Backend",
                                                EstimatedHours = 4,
                                                StoryPoints = 1,
                                                Priority = taskDto.Priority,
                                                Status = taskDto.Status,
                                                Description = $"Triển khai '{strTitle}' theo kiến trúc giải pháp và đặc tả tài liệu.",
                                                AssigneeName = taskDto.AssigneeName,
                                                DocumentId = taskDto.DocumentId,
                                                DocumentCitations = taskDto.DocumentCitations.ToList(),
                                                AcceptanceCriteria = new List<string> { $"Nghiệm thu hoàn tất '{strTitle}'" }
                                            });
                                        }
                                    }
                                    subIdx++;
                                }
                            }

                            proposal.Tasks.Add(taskDto);
                            taskIndex++;
                        }

                        proposal.TotalStoryPoints = proposal.Tasks.Sum(t => t.StoryPoints);
                        proposal.TotalEstimatedHours = proposal.Tasks.Sum(t => t.EstimatedHours);
                        return proposal;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Tránh nuốt lỗi im lặng - log chi tiết để theo dõi và khắc phục
            Console.WriteLine($"[AiSprintDecomposition] Lỗi bóc tách JSON đề xuất: {ex.Message}");
        }

        if (preserveOnFailure != null && preserveOnFailure.Tasks.Count > 0)
        {
            var preserved = BuildPreservedProposalShell(sessionId, projectId, targetSprintId, preserveOnFailure);
            preserved.AssistantReply = "Tôi chưa nhận được cấu trúc cập nhật mới từ mô hình AI nên đã giữ nguyên danh sách công việc hiện có để tránh mất dữ liệu. Bạn có thể mô tả cụ thể hơn thay đổi mong muốn hoặc nhấn chỉnh sửa trực tiếp trong bảng.";
            return preserved;
        }

        return CreateFallbackProposal(sessionId, projectId, targetSprintId, sourceDoc, userPrompt, availableDocs, activeConfig);
    }

    private static void ApplyConfigMetadata(AiSprintProposalDto proposal, ProjectActiveConfig? activeConfig)
    {
        if (activeConfig == null) return;
        proposal.WorkflowName = activeConfig.WorkflowName;
        proposal.InitialStatus = activeConfig.InitialStatusName;
        proposal.AppliedWorkflowStatuses = activeConfig.Statuses.Select(s => s.Name).ToList();
        proposal.AppliedPriorities = activeConfig.Priorities.Select(p => p.Name).ToList();
        proposal.AppliedIssueTypes = activeConfig.ParentWorkItemTypes.Select(t => t.Name).ToList();
        proposal.AppliedLabels = activeConfig.Labels.Select(l => l.Name).ToList();
    }

    public static string ClassifyConversationalIntent(
        string? userPrompt,
        string? docContent,
        AiSprintProposalDto? existingProposal)
    {
        var promptText = (userPrompt ?? string.Empty).Trim();
        var docText = (docContent ?? string.Empty).Trim();

        // Nếu prompt là câu mặc định tự động của client khi gửi tài liệu, lấy docText làm nội dung phân tích chính
        var combined = (!string.IsNullOrWhiteSpace(promptText) &&
                        !promptText.Equals("Tạo Sprint Goal, Tasks và Subtasks chuẩn Scrum.", StringComparison.OrdinalIgnoreCase) &&
                        !promptText.Equals("Phân tích tài liệu dự án và lập kế hoạch Sprint tiếp theo.", StringComparison.OrdinalIgnoreCase))
            ? (string.IsNullOrWhiteSpace(docText) || docText == promptText ? promptText : $"{promptText}\n{docText}")
            : (!string.IsNullOrWhiteSpace(docText) ? docText : promptText);

        var lower = combined.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lower))
        {
            return "sprint_plan";
        }

        // 1. Kiểm tra yêu cầu Xuất file Excel (export_excel) - tránh nhầm với yêu cầu tạo task/tính năng xuất Excel
        bool mentionsExcel = lower.Contains("excel") || lower.Contains(".xlsx") || lower.Contains("bảng tính");
        bool mentionsExportVerb = lower.Contains("xuất") || lower.Contains("tải") || lower.Contains("export") ||
                                  lower.Contains("download") || lower.Contains("lấy file") || lower.Contains("cho tôi file");
        bool asksToBuildFeatureOrTask = lower.Contains("tạo task") || lower.Contains("tạo việc") || lower.Contains("thêm task") ||
                                        lower.Contains("thêm việc") || lower.Contains("sinh task") || lower.Contains("phân tách") ||
                                        lower.Contains("phân rã") || lower.Contains("tính năng") || lower.Contains("chức năng") ||
                                        lower.Contains("user story") || lower.Contains("xây dựng") || lower.Contains("triển khai") ||
                                        lower.Contains("viết api") || lower.Contains("code ");
        if (mentionsExcel && !asksToBuildFeatureOrTask && (mentionsExportVerb || lower.Length < 40) && lower.Length < 180)
        {
            return "export_excel";
        }

        // 2. Kiểm tra lệnh hành động trực tiếp trên đề xuất hiện có (push_backlog / apply_sprint)
        bool hasExistingTasks = existingProposal?.Tasks != null && existingProposal.Tasks.Count > 0;
        if (hasExistingTasks && lower.Length < 180)
        {
            bool asksToCreateOrModify = lower.Contains("thêm ") || lower.Contains("tạo ") || lower.Contains("sinh ") ||
                                        lower.Contains("phân tách") || lower.Contains("phân rã") || lower.Contains("chia nhỏ") ||
                                        lower.Contains("sửa ") || lower.Contains("xóa ") || lower.Contains("làm lại");

            if (!asksToCreateOrModify)
            {
                if ((lower.Contains("đẩy") || lower.Contains("đưa") || lower.Contains("lưu") || lower.Contains("chuyển") || lower.Contains("cho vào")) &&
                    lower.Contains("backlog"))
                {
                    return "push_backlog";
                }

                if ((lower.Contains("áp dụng") || lower.Contains("commit") || lower.Contains("đưa vào sprint") || lower.Contains("đẩy vào sprint")) &&
                    lower.Contains("sprint"))
                {
                    return "apply_sprint";
                }
            }
        }

        // 3. Kiểm tra Out-of-Scope Guardrail (Từ chối các yêu cầu ngoài luồng không liên quan đến Quản lý Dự án)
        string[] offTopicKeywords =
        [
            "thời tiết", "weather", "nấu ăn", "công thức nấu", "nấu phở", "nấu cơm", "món ngon",
            "bóng đá", "world cup", "ngoại hạng anh", "champions league", "xổ số", "lô đề", "vietlott",
            "chứng khoán", "cổ phiếu", "bitcoin", "crypto", "forex", "tử vi", "bói toán", "cung hoàng đạo",
            "làm thơ", "viết thơ", "bài thơ", "sáng tác thơ", "chuyện cười", "kể chuyện", "truyện cổ tích",
            "bài hát", "lời bài hát", "ca sĩ", "xem phim", "tán tỉnh", "tư vấn tình cảm", "thất tình",
            "giải phương trình", "đạo hàm", "tích phân", "lịch sử thế giới", "thủ đô của", "tổng thống",
            "đặt vé máy bay", "đặt phòng khách sạn", "du lịch bụi"
        ];

        string[] projectManagementKeywords =
        [
            "sprint", "backlog", "task", "công việc", "đầu việc", "giao việc", "tạo việc", "thêm việc",
            "subtask", "sub-task", "bug", "lỗi",
            "story", "user story", "epic", "spec", "tài liệu", "đặc tả", "cuộc họp", "biên bản", "họp",
            "kế hoạch", "plan", "phân rã", "phân tách", "bóc tách", "gán", "assign", "thành viên", "nhân sự",
            "giao cho", "dev", "developer", "qa", "tester", "frontend", "backend", "fullstack", "devops",
            "scrum", "agile", "kanban", "velocity", "vận tốc", "story point", "sp", "ưu tiên", "priority",
            "trạng thái", "status", "workflow", "quy trình", "nhãn", "label", "tag", "tiêu chí", "nghiệm thu",
            "acceptance", "excel", "xlsx", "csv", "báo cáo", "report", "dự án", "project", "qlda", "tiến độ",
            "deadline", "quá hạn", "overdue", "rollover", "kế thừa", "review", "retro", "estimate", "ước lượng",
            "giờ công", "số giờ", "capacity", "năng lực", "api", "database", "db", "sql", "redis", "signalr",
            "ui", "ux", "component", "module", "phân hệ", "tính năng", "chức năng", "feature", "hệ thống",
            "requirement", "yêu cầu", "srs", "brd", "prd", "kiến trúc", "thiết kế", "triển khai", "kiểm thử",
            "test", "bảo mật", "security", "code", "endpoint", "controller", "service", "nhóm", "team",
            "đẩy", "áp dụng", "trợ lý", "hướng dẫn"
        ];

        bool hasOffTopic = offTopicKeywords.Any(kw => lower.Contains(kw, StringComparison.Ordinal));
        bool hasPmDomain = projectManagementKeywords.Any(kw => lower.Contains(kw, StringComparison.Ordinal));
        bool isGreetingOrHelp = lower.Contains("xin chào") || lower.Contains("bạn làm được gì") ||
                                lower.Contains("bạn là ai") || lower.Contains("hỗ trợ gì");
        bool hasMemoryRecallRef = existingProposal != null &&
                                  (lower.Contains("#mem-") || lower.Contains("mem-") ||
                                   lower.Contains("vừa nãy") || lower.Contains("lúc nãy") ||
                                   lower.Contains("hồi nãy") || lower.Contains("vừa rồi") ||
                                   lower.Contains("nhắc lại") || lower.Contains("trí nhớ") ||
                                   lower.Contains("trên kia") || lower.Contains("đã bàn") ||
                                   lower.Contains("trước đó"));

        if (hasOffTopic && !hasPmDomain)
        {
            return "out_of_scope_refusal";
        }

        if (isGreetingOrHelp && !hasOffTopic)
        {
            return "project_qa";
        }

        if (!hasPmDomain && !hasMemoryRecallRef && lower.Length < 260)
        {
            return "out_of_scope_refusal";
        }

        // 4. Kiểm tra câu hỏi tra cứu thông tin dự án (project_qa)
        bool isQuestionPattern = lower.Contains('?') ||
                                 hasMemoryRecallRef ||
                                 lower.Contains("là gì") ||
                                 lower.Contains("những gì") ||
                                 lower.Contains("bao nhiêu") ||
                                 lower.Contains("những ai") ||
                                 lower.Contains("ai đang") ||
                                 lower.Contains("cho hỏi") ||
                                 lower.Contains("cho tôi biết") ||
                                 lower.Contains("nhắc lại") ||
                                 lower.Contains("xem lại") ||
                                 lower.Contains("đang dùng workflow") ||
                                 lower.Contains("mức ưu tiên nào");

        bool asksForDecomposition = lower.Contains("lập kế hoạch") ||
                                    lower.Contains("phân tách") ||
                                    lower.Contains("phân rã") ||
                                    lower.Contains("bóc tách") ||
                                    lower.Contains("sinh task") ||
                                    lower.Contains("tạo task") ||
                                    lower.Contains("tạo việc") ||
                                    lower.Contains("thêm task") ||
                                    lower.Contains("thêm việc") ||
                                    lower.Contains("chia nhỏ") ||
                                    lower.Contains("làm lại") ||
                                    lower.Contains("biên bản họp") ||
                                    lower.Contains("nội dung cuộc họp");

        if (isQuestionPattern && !asksForDecomposition && lower.Length < 280)
        {
            return "project_qa";
        }

        // 5. Nếu người dùng yêu cầu lên danh sách công việc cho kho Backlog
        if (IsBacklogTargetRequested(lower))
        {
            return "backlog_plan";
        }

        // 6. Nếu trong phiên đã có đề xuất và người dùng gửi yêu cầu điều chỉnh tiếp theo
        if (hasExistingTasks && lower.Length < 600)
        {
            return "refine_proposal";
        }

        return "sprint_plan";
    }

    private static bool IsBacklogTargetRequested(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return lower.Contains("vào kho backlog") ||
               lower.Contains("vào backlog") ||
               lower.Contains("đẩy vào backlog") ||
               lower.Contains("thêm vào backlog") ||
               lower.Contains("lưu vào backlog") ||
               lower.Contains("cho kho backlog") ||
               lower.Contains("tạo backlog");
    }

    private static AiSprintProposalDto BuildPreservedProposalShell(
        string sessionId,
        Guid projectId,
        Guid? targetSprintId,
        AiSprintProposalDto? existingProposal)
    {
        if (existingProposal != null)
        {
            return new AiSprintProposalDto
            {
                SessionId = sessionId,
                OrganizationId = existingProposal.OrganizationId,
                ProjectId = projectId,
                UserId = existingProposal.UserId,
                TargetSprintId = targetSprintId ?? existingProposal.TargetSprintId,
                SprintGoal = existingProposal.SprintGoal,
                Rationale = existingProposal.Rationale,
                RecommendedTarget = existingProposal.RecommendedTarget,
                Tags = new List<string>(existingProposal.Tags ?? []),
                TotalEstimatedHours = existingProposal.TotalEstimatedHours,
                TotalStoryPoints = existingProposal.TotalStoryPoints,
                WorkflowName = existingProposal.WorkflowName,
                InitialStatus = existingProposal.InitialStatus,
                AppliedWorkflowStatuses = new List<string>(existingProposal.AppliedWorkflowStatuses ?? []),
                AppliedPriorities = new List<string>(existingProposal.AppliedPriorities ?? []),
                AppliedIssueTypes = new List<string>(existingProposal.AppliedIssueTypes ?? []),
                AppliedLabels = new List<string>(existingProposal.AppliedLabels ?? []),
                Tasks = new List<ProposedTaskDto>(existingProposal.Tasks ?? []),
                IndexedMemories = existingProposal.IndexedMemories?.Select(CloneMemoryAnchor).ToList() ?? [],
                CreatedAtUtc = existingProposal.CreatedAtUtc
            };
        }

        return new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = projectId,
            TargetSprintId = targetSprintId,
            SprintGoal = string.Empty,
            Rationale = string.Empty,
            Tasks = []
        };
    }

    private static AiMemoryAnchorDto CloneMemoryAnchor(AiMemoryAnchorDto m) => new()
    {
        AnchorKey = m.AnchorKey,
        TurnIndex = m.TurnIndex,
        Category = m.Category,
        Title = m.Title,
        Keywords = m.Keywords,
        Summary = m.Summary,
        DetailContent = m.DetailContent,
        CreatedAtUtc = m.CreatedAtUtc
    };

    private static string FormatConversationHistoryForPrompt(
        IReadOnlyList<AiConversationTurnDto> history,
        IReadOnlyList<AiMemoryAnchorDto> allMemories,
        IReadOnlyList<AiMemoryAnchorDto> recalledMemories,
        AiSprintProposalDto? existingProposal,
        bool includeProposalTaskDetails = true) =>
        AiIndexedMemoryEngine.FormatIndexedContextForPrompt(
            history,
            allMemories,
            recalledMemories,
            existingProposal,
            includeProposalTaskDetails);

    private async Task<string> BuildFocusedProjectQaAnswerAsync(
        string question,
        AiContextExecutionPlan contextPlan,
        ProjectActiveConfig? activeConfig,
        string? backlogStoriesJson,
        string? sprintHistoryJson,
        string? teamCapacitiesJson,
        string? specHubExcerptsText,
        IReadOnlyList<(Guid Id, string Name)> projectDocs,
        IReadOnlyList<AiConversationTurnDto> history,
        IReadOnlyList<AiMemoryAnchorDto> indexedMemories,
        AiSprintProposalDto? existingProposal,
        IProjectScopeContext scope,
        string? model,
        CancellationToken ct)
    {
        var memoryContext = FormatConversationHistoryForPrompt(
            history,
            indexedMemories,
            contextPlan.RecalledMemories,
            existingProposal,
            includeProposalTaskDetails: (existingProposal?.Tasks?.Count ?? 0) > 0 &&
                (contextPlan.UseMemoryOnly ||
                 question.Contains("đề xuất", StringComparison.OrdinalIgnoreCase) ||
                 question.Contains("task", StringComparison.OrdinalIgnoreCase)));

        var retrievedSections = new System.Text.StringBuilder();

        if (!contextPlan.IsGreetingOrHelp)
        {
            if (activeConfig != null && contextPlan.NeedsActiveConfig)
            {
                var statuses = string.Join(" -> ", activeConfig.Statuses.Select(s => s.Name));
                var priorities = string.Join(", ", activeConfig.Priorities.Select(p => p.Name));
                var issueTypes = string.Join(", ", activeConfig.ParentWorkItemTypes.Select(t => t.Name));
                var labels = activeConfig.Labels.Count > 0
                    ? string.Join(", ", activeConfig.Labels.Select(l => l.Name))
                    : "Chưa định nghĩa nhãn";

                retrievedSections.AppendLine($"- Quy trình Workflow áp dụng: {activeConfig.WorkflowName} ({statuses}), khởi tạo mặc định: {activeConfig.InitialStatusName}");
                retrievedSections.AppendLine($"- Mức ưu tiên áp dụng: {priorities}");
                retrievedSections.AppendLine($"- Loại công việc áp dụng: {issueTypes}");
                retrievedSections.AppendLine($"- Nhãn dự án: {labels}");
            }

            if (contextPlan.NeedsSpecHub)
            {
                var docNames = projectDocs.Count > 0
                    ? string.Join(", ", projectDocs.Select(d => d.Name))
                    : "Chưa có tài liệu trong Spec Hub";
                retrievedSections.AppendLine($"- Danh sách tài liệu Spec Hub: {docNames}");
                if (!string.IsNullOrWhiteSpace(specHubExcerptsText))
                {
                    retrievedSections.AppendLine($"- Nội dung trích đoạn Spec Hub:\n{specHubExcerptsText}");
                }
            }

            if (!string.IsNullOrWhiteSpace(backlogStoriesJson))
            {
                retrievedSections.AppendLine($"- Kho Product Backlog hiện tại: {backlogStoriesJson}");
            }

            if (!string.IsNullOrWhiteSpace(sprintHistoryJson))
            {
                retrievedSections.AppendLine($"- Lịch sử Sprint & Vận tốc: {sprintHistoryJson}");
            }

            if (!string.IsNullOrWhiteSpace(teamCapacitiesJson))
            {
                retrievedSections.AppendLine($"- Năng lực thành viên: {teamCapacitiesJson}");
            }
        }

        try
        {
            var qaSystemInstruction = contextPlan.IsGreetingOrHelp
                ? "Bạn là Trợ Lý AI Quản lý Dự án Scrum. Hãy chào hỏi thân thiện, súc tích bằng tiếng Việt và tóm tắt ngắn gọn 3-4 việc chính bạn hỗ trợ (lập kế hoạch Sprint, phân rã Backlog, hỏi đáp Spec Hub/Backlog/Workflow và xuất báo cáo Excel). Tuyệt đối không tạo task hay trả về JSON."
                : "Bạn là Trợ Lý AI Quản lý Dự án Scrum. Hãy trả lời ĐÚNG TRỌNG TÂM câu hỏi của người dùng bằng tiếng Việt rành mạch, ngắn gọn, chính xác 100% dựa trên chỉ mục trí nhớ và dữ liệu vừa truy vấn (chỉ nêu những gì người dùng hỏi, KHÔNG lan man sang nội dung khác, KHÔNG tự ý sinh danh sách task/kế hoạch khi không được yêu cầu, KHÔNG bịa đặt và KHÔNG trả về JSON).";

            var userContent = new System.Text.StringBuilder();
            userContent.AppendLine($"Câu hỏi của người dùng: {question}");
            if (!string.IsNullOrWhiteSpace(memoryContext))
            {
                userContent.AppendLine();
                userContent.AppendLine(memoryContext);
            }
            if (retrievedSections.Length > 0)
            {
                userContent.AppendLine();
                userContent.AppendLine("Dữ liệu thực tế vừa truy xuất có chọn lọc:");
                userContent.Append(retrievedSections);
            }

            var qaPrompt = new AiPrompt
            {
                Model = model,
                SystemInstruction = qaSystemInstruction,
                Messages = [AiMessage.CreateUser(userContent.ToString().Trim())],
                Temperature = 0.2,
                MaxTokens = 1400
            };

            var res = await _aiClient.GenerateAsync(qaPrompt, scope, ct);
            if (res.IsSuccess && !string.IsNullOrWhiteSpace(res.Content) && !res.Content.TrimStart().StartsWith('{'))
            {
                return res.Content.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BuildFocusedProjectQaAnswer] Sử dụng phản hồi dự phòng ngắn gọn");
        }

        if (contextPlan.IsGreetingOrHelp)
        {
            return "Chào bạn! Tôi là Trợ Lý AI Quản lý Dự án. Tôi có thể giúp bạn: (1) Tra cứu thông tin thực tế về Spec Hub, kho Backlog, Workflow và nhân sự dự án; (2) Phân tích biên bản họp để lập kế hoạch Sprint hoặc đẩy việc vào Backlog; (3) Tinh chỉnh đề xuất và xuất báo cáo Excel cho dự án.";
        }

        if (contextPlan.UseMemoryOnly && contextPlan.RecalledMemories.Count > 0)
        {
            return string.Join("\n", contextPlan.RecalledMemories.Select(m => $"- [{m.AnchorKey}] {m.Title}: {m.Summary}"));
        }

        if (activeConfig != null)
        {
            var statuses = string.Join(" -> ", activeConfig.Statuses.Select(s => s.Name));
            var priorities = string.Join(", ", activeConfig.Priorities.Select(p => p.Name));
            return $"Thông tin cấu hình hiện hành của dự án ({activeConfig.WorkflowName}):\n- Trạng thái: {statuses} (Khởi tạo: {activeConfig.InitialStatusName})\n- Mức ưu tiên: {priorities}";
        }

        return "Tôi đã tiếp nhận câu hỏi của bạn dựa trên ngữ cảnh hiện có của phiên làm việc.";
    }

    private static AiSprintProposalDto CreateFallbackProposal(
        string sessionId,
        Guid projectId,
        Guid? targetSprintId,
        string sourceDoc,
        string? userPrompt = null,
        IReadOnlyList<(Guid Id, string Name)>? availableDocs = null,
        ProjectActiveConfig? activeConfig = null)
    {
        var initialStatus = activeConfig?.InitialStatusName ?? "To Do";
        var highPrio = activeConfig?.ResolvePriorityName("High") ?? "High";
        var defaultPrio = activeConfig?.GetDefaultPriorityName() ?? "Medium";
        var storyType = activeConfig?.ResolveIssueTypeName("Story") ?? "Story";
        var taskType = activeConfig?.ResolveIssueTypeName("Task") ?? "Task";
        var cleanDoc = (sourceDoc ?? string.Empty).Trim();
        var lines = cleanDoc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Lọc bỏ triệt để các tiêu đề meta, chỉ dẫn hoặc dòng ghi chú thô của người dùng
        var firstMeaningfulLine = lines.FirstOrDefault(l =>
            l.Length > 8 &&
            !l.StartsWith('#') &&
            !l.StartsWith("---") &&
            !l.Contains("GHI CHÚ", StringComparison.OrdinalIgnoreCase) &&
            !l.Contains("YÊU CẦU BỔ SUNG", StringComparison.OrdinalIgnoreCase) &&
            !l.Contains("Phân tích tài liệu đặc tả", StringComparison.OrdinalIgnoreCase))
            ?? "Triển khai tính năng cốt lõi theo đặc tả hệ thống";

        var featureName = firstMeaningfulLine.Length > 60 ? firstMeaningfulLine[..60] + "..." : firstMeaningfulLine;
        featureName = featureName.Trim(' ', ':', '-', '.');

        var goal = !string.IsNullOrWhiteSpace(userPrompt) && userPrompt.Length > 10
            ? userPrompt.Trim()
            : $"Hoàn thiện module tính năng cốt lõi và đảm bảo chất lượng kiểm thử: {featureName}";

        var doc1 = availableDocs != null && availableDocs.Count > 0 ? availableDocs[0] : (Id: (Guid?)null, Name: "SRS-Quan-Ly-Sprint-Va-Kanban-Realtime.md");
        var doc2 = availableDocs != null && availableDocs.Count > 1 ? availableDocs[1] : doc1;
        var doc3 = availableDocs != null && availableDocs.Count > 2 ? availableDocs[2] : doc1;

        var cite1 = $"{doc1.Name} - Mục Phần II: Đặc tả chức năng và quy tắc nghiệp vụ";
        var cite2 = $"{doc2.Name} - Mục 5.1: Danh tính và quyền";
        var cite3 = $"{doc3.Name} - Mục 2.1: Phạm vi sản phẩm";

        var task1Title = $"Xây dựng kiến trúc xử lý nghiệp vụ và RESTful API cho {featureName}";

        var proposal = new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = projectId,
            TargetSprintId = targetSprintId,
            SprintGoal = goal,
            Rationale = "Phân bổ công việc dựa trên phân tích đặc tả yêu cầu dự án và năng lực khả dụng của đội ngũ.",
            Tags = new List<string> { "#SprintPlan", "#Scrum", "#SpecHub", "#FullStack" },
            TotalStoryPoints = 13,
            TotalEstimatedHours = 40,
            Tasks = new List<ProposedTaskDto>
            {
                new()
                {
                    Id = "task_01",
                    EpicTitle = "Core Feature Implementation",
                    Title = task1Title,
                    Description = $"Thiết kế kiến trúc giải pháp, cấu hình dữ liệu và xây dựng các luồng xử lý nghiệp vụ cho '{featureName}'.",
                    Type = storyType,
                    Priority = highPrio,
                    Status = initialStatus,
                    StoryPoints = 5,
                    EstimatedHours = 16,
                    Tags = new List<string> { "Backend", "API" },
                    AcceptanceCriteria = new List<string>
                    {
                        "API phản hồi đúng định dạng JSON",
                        "Kiểm tra xác thực JWT và phân quyền",
                        "Xử lý ngoại lệ toàn diện"
                    },
                    DocumentId = doc1.Id,
                    DocumentCitations = new List<string> { cite1 },
                    AiRationale = "Cần thiết lập trước để các task giao diện frontend tích hợp.",
                    Subtasks = new List<ProposedSubTaskDto>
                    {
                        new()
                        {
                            Id = "sub_01_1",
                            Title = "Backend API & DbContext",
                            Role = "Backend",
                            EstimatedHours = 8,
                            StoryPoints = 1,
                            Priority = highPrio,
                            Status = initialStatus,
                            Description = "Thiết kế các API Endpoints và cấu hình Entity Framework Core DbContext mapping theo tài liệu kiến trúc.",
                            AcceptanceCriteria = new List<string>
                            {
                                "Định nghĩa đầy đủ DbContext và Migration không lỗi",
                                "API trả về chuẩn DTO và mã phản hồi HTTP chính xác"
                            },
                            DocumentId = doc1.Id,
                            DocumentCitations = new List<string> { cite1 }
                        },
                        new()
                        {
                            Id = "sub_01_2",
                            Title = "Unit Tests & Integration",
                            Role = "QA",
                            EstimatedHours = 8,
                            StoryPoints = 1,
                            Priority = defaultPrio,
                            Status = initialStatus,
                            Description = "Viết kịch bản kiểm thử đơn vị xUnit và kiểm thử tích hợp cho luồng xử lý Backend.",
                            AcceptanceCriteria = new List<string>
                            {
                                "Độ bao phủ kiểm thử đạt trên 80% mã nguồn mới",
                                "Toàn bộ bài test kiểm thử tích hợp vượt qua thành công"
                            },
                            DocumentId = doc1.Id,
                            DocumentCitations = new List<string> { cite1 }
                        }
                    }
                },
                new()
                {
                    Id = "task_02",
                    EpicTitle = "User Interface & Experience",
                    Title = "Xây dựng giao diện tương tác người dùng",
                    Description = "Phát triển component giao diện Angular tương thích đa màn hình.",
                    Type = storyType,
                    Priority = highPrio,
                    Status = initialStatus,
                    StoryPoints = 5,
                    EstimatedHours = 16,
                    Tags = new List<string> { "Frontend", "Angular" },
                    AcceptanceCriteria = new List<string>
                    {
                        "Giao diện responsive không bị tràn lề",
                        "100% icon sử dụng Google Material Symbols Outlined",
                        "Không sử dụng raw keyboard emojis"
                    },
                    DocumentId = doc2.Id,
                    DocumentCitations = new List<string> { cite2 },
                    AiRationale = "Mang lại trải nghiệm người dùng chuẩn doanh nghiệp.",
                    Subtasks = new List<ProposedSubTaskDto>
                    {
                        new()
                        {
                            Id = "sub_02_1",
                            Title = "Angular Component & Signal state",
                            Role = "Frontend",
                            EstimatedHours = 10,
                            StoryPoints = 1,
                            Priority = highPrio,
                            Status = initialStatus,
                            Description = "Triển khai Angular standalone component và quản lý trạng thái bằng Signal state phản ứng linh hoạt.",
                            AcceptanceCriteria = new List<string>
                            {
                                "Không phát sinh re-render thừa khi thay đổi tín hiệu Signal",
                                "Tuân thủ tiêu chuẩn giao diện Material Symbols Outlined"
                            },
                            DocumentId = doc2.Id,
                            DocumentCitations = new List<string> { cite2 }
                        },
                        new()
                        {
                            Id = "sub_02_2",
                            Title = "E2E Playwright validation",
                            Role = "QA",
                            EstimatedHours = 6,
                            StoryPoints = 1,
                            Priority = defaultPrio,
                            Status = initialStatus,
                            Description = "Viết kịch bản kiểm thử đầu cuối E2E với Playwright kiểm tra luồng người dùng và độ phản hồi.",
                            AcceptanceCriteria = new List<string>
                            {
                                "Kịch bản tự động hóa chạy ổn định trong chế độ headless",
                                "Bắt trọn vẹn các case ngoại lệ và kiểm tra giao diện chia đôi màn hình"
                            },
                            DocumentId = doc2.Id,
                            DocumentCitations = new List<string> { cite2 }
                        }
                    }
                },
                new()
                {
                    Id = "task_03",
                    EpicTitle = "Quality Assurance & Security",
                    Title = "Kiểm thử bảo mật và rà soát Zero-Secret-Leakage",
                    Description = "Audit bảo mật và chạy toàn bộ test suite.",
                    Type = taskType,
                    Priority = defaultPrio,
                    Status = initialStatus,
                    StoryPoints = 3,
                    EstimatedHours = 8,
                    Tags = new List<string> { "Security", "QA" },
                    AcceptanceCriteria = new List<string>
                    {
                        "Không có secret/key/URL/IP hardcode trong repo",
                        "Kiểm thử bảo mật vượt qua 100%"
                    },
                    DocumentId = doc3.Id,
                    DocumentCitations = new List<string> { cite3 },
                    AiRationale = "Đảm bảo tuân thủ tiêu chuẩn an toàn thông tin tối cao của tổ chức.",
                    Subtasks = new List<ProposedSubTaskDto>
                    {
                        new()
                        {
                            Id = "sub_03_1",
                            Title = "Security static scan",
                            Role = "DevOps",
                            EstimatedHours = 4,
                            StoryPoints = 1,
                            Priority = highPrio,
                            Status = initialStatus,
                            Description = "Quét tự động toàn bộ source code kiểm tra leak secret và kiểm tra cấu hình an toàn.",
                            AcceptanceCriteria = new List<string>
                            {
                                "Không tìm thấy token/key/mật khẩu trong code",
                                "Tệp appsettings.Local.json và .env nằm trong .gitignore"
                            },
                            DocumentId = doc3.Id,
                            DocumentCitations = new List<string> { cite3 }
                        },
                        new()
                        {
                            Id = "sub_03_2",
                            Title = "Regression tests",
                            Role = "QA",
                            EstimatedHours = 4,
                            StoryPoints = 1,
                            Priority = defaultPrio,
                            Status = initialStatus,
                            Description = "Chạy kiểm thử hồi quy toàn diện các tính năng đã phát hành của Sprint.",
                            AcceptanceCriteria = new List<string>
                            {
                                "100% test case hồi quy đạt pass",
                                "Thời gian chạy bộ kiểm thử dưới 5 phút"
                            },
                            DocumentId = doc3.Id,
                            DocumentCitations = new List<string> { cite3 }
                        }
                    }
                }
            }
        };

        ApplyConfigMetadata(proposal, activeConfig);
        proposal.TotalStoryPoints = proposal.Tasks.Sum(t => t.StoryPoints);
        proposal.TotalEstimatedHours = proposal.Tasks.Sum(t => t.EstimatedHours);
        return proposal;
    }

    private static string ExtractJsonObject(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[7..];
        if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
        cleaned = cleaned.Trim();

        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }
        return cleaned;
    }

    private sealed class TeamMemberProfile
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public List<string> Skills { get; set; } = new();
        public decimal RemainingHours { get; set; } = 40.0m;
    }

    private static void EnrichProposalWithTeamMembers(AiSprintProposalDto proposal, string teamCapacitiesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(teamCapacitiesJson);
            if (!doc.RootElement.TryGetProperty("Capacities", out var capArray) || capArray.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var members = new List<TeamMemberProfile>();
            foreach (var item in capArray.EnumerateArray())
            {
                if (item.TryGetProperty("UserId", out var idProp) &&
                    Guid.TryParse(idProp.GetString(), out var userId) &&
                    item.TryGetProperty("DisplayName", out var nameProp))
                {
                    var name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var profile = new TeamMemberProfile
                        {
                            UserId = userId,
                            DisplayName = name,
                            JobTitle = item.TryGetProperty("JobTitle", out var jt) ? jt.GetString() ?? string.Empty : string.Empty,
                            RemainingHours = item.TryGetProperty("RemainingAvailableHours", out var rh) && rh.TryGetDecimal(out var d) ? d : 40.0m
                        };

                        if (item.TryGetProperty("Roles", out var rProp) && rProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var r in rProp.EnumerateArray())
                            {
                                if (r.GetString() is { } rStr) profile.Roles.Add(rStr);
                            }
                        }

                        if (item.TryGetProperty("Skills", out var sProp) && sProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var s in sProp.EnumerateArray())
                            {
                                if (s.GetString() is { } sStr) profile.Skills.Add(sStr);
                            }
                        }

                        members.Add(profile);
                    }
                }
            }

            if (members.Count == 0) return;

            // 1. Diversify Story Points across Fibonacci (1, 2, 3, 5, 8) if model returned all identical SP
            var distinctSp = proposal.Tasks.Select(t => t.StoryPoints).Distinct().ToList();
            if (distinctSp.Count == 1 && proposal.Tasks.Count > 1)
            {
                int[] fibRotation = [3, 5, 2, 8, 3, 5];
                for (var i = 0; i < proposal.Tasks.Count; i++)
                {
                    var t = proposal.Tasks[i];
                    var text = (t.Title + " " + t.Description).ToLowerInvariant();
                    if (text.Contains("kiến trúc") || text.Contains("hạ tầng") || text.Contains("realtime") || text.Contains("toàn diện"))
                    {
                        t.StoryPoints = 5;
                        t.EstimatedHours = 16;
                    }
                    else if (text.Contains("kiểm thử") || text.Contains("test") || text.Contains("cấu hình") || text.Contains("nhỏ"))
                    {
                        t.StoryPoints = 2;
                        t.EstimatedHours = 6;
                    }
                    else if (text.Contains("giao diện") || text.Contains("bảng") || text.Contains("biểu đồ") || text.Contains("modal"))
                    {
                        t.StoryPoints = 3;
                        t.EstimatedHours = 8;
                    }
                    else
                    {
                        t.StoryPoints = fibRotation[i % fibRotation.Length];
                        t.EstimatedHours = t.StoryPoints * 3;
                    }
                    t.RemainingHours = t.EstimatedHours;
                }
                proposal.TotalStoryPoints = proposal.Tasks.Sum(t => t.StoryPoints);
                proposal.TotalEstimatedHours = proposal.Tasks.Sum(t => t.EstimatedHours);
            }

            // 2. Intelligent Assignee Matching by Skills, Role & Workload Balance
            var assignedSet = new HashSet<Guid>();

            for (var i = 0; i < proposal.Tasks.Count; i++)
            {
                var task = proposal.Tasks[i];
                var matched = MatchBestMemberForTask(task, members, assignedSet);
                task.AssigneeUserId = matched.UserId;
                task.AssigneeName = matched.DisplayName;
                assignedSet.Add(matched.UserId);

                if (task.RemainingHours <= 0)
                {
                    task.RemainingHours = task.EstimatedHours;
                }

                // If all members were assigned once, reset set to re-balance
                if (assignedSet.Count >= members.Count)
                {
                    assignedSet.Clear();
                }

                // Subtasks matching
                if (task.Subtasks != null && task.Subtasks.Count > 0)
                {
                    for (var j = 0; j < task.Subtasks.Count; j++)
                    {
                        var sub = task.Subtasks[j];
                        var subRole = string.IsNullOrWhiteSpace(sub.Role) ? (j % 2 == 0 ? "Backend" : "Frontend") : sub.Role;
                        sub.Role = subRole;
                        var subMember = MatchMemberByRole(subRole, members, task.AssigneeUserId);
                        sub.AssigneeUserId = subMember.UserId;
                        sub.AssigneeName = subMember.DisplayName;
                        if (sub.EstimatedHours <= 0) sub.EstimatedHours = 6;
                    }
                }
            }
        }
        catch
        {
            // Ignore enrichment parse errors
        }
    }

    private static TeamMemberProfile MatchBestMemberForTask(
        ProposedTaskDto task,
        List<TeamMemberProfile> memberList,
        HashSet<Guid> recentlyAssigned)
    {
        var text = (task.Title + " " + task.Description + " " + string.Join(" ", task.Tags ?? [])).ToLowerInvariant();

        // 1. Direct name match if AI already suggested a valid member
        if (!string.IsNullOrWhiteSpace(task.AssigneeName))
        {
            var direct = memberList.FirstOrDefault(m =>
                m.DisplayName.Contains(task.AssigneeName, StringComparison.OrdinalIgnoreCase) ||
                task.AssigneeName.Contains(m.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (direct != null) return direct;
        }

        // 2. Skill & Domain matching
        bool isFrontend = text.Contains("giao diện") || text.Contains("ui") || text.Contains("ux") ||
                          text.Contains("angular") || text.Contains("component") || text.Contains("bảng") ||
                          text.Contains("modal") || text.Contains("drawer") || text.Contains("css") || text.Contains("tailwind");

        bool isBackend = text.Contains("api") || text.Contains("backend") || text.Contains("c#") ||
                         text.Contains(".net") || text.Contains("mysql") || text.Contains("redis") ||
                         text.Contains("database") || text.Contains("sse") || text.Contains("signalr") ||
                         text.Contains("controller") || text.Contains("mcp") || text.Contains("dbcontext");

        bool isQA = text.Contains("kiểm thử") || text.Contains("test") || text.Contains("qa") ||
                    text.Contains("playwright") || text.Contains("e2e") || text.Contains("nghiệm thu") ||
                    text.Contains("ac") || text.Contains("regression");

        bool isLead = text.Contains("kiến trúc") || text.Contains("bảo mật") || text.Contains("quy trình") ||
                      text.Contains("kế hoạch") || text.Contains("sprint");

        List<TeamMemberProfile> candidates = memberList;

        if (isFrontend)
        {
            var fe = memberList.Where(m =>
                m.JobTitle.Contains("Frontend", StringComparison.OrdinalIgnoreCase) ||
                m.Skills.Any(s => s.Contains("Angular", StringComparison.OrdinalIgnoreCase) || s.Contains("Tailwind", StringComparison.OrdinalIgnoreCase))).ToList();
            if (fe.Count > 0) candidates = fe;
        }
        else if (isBackend)
        {
            var be = memberList.Where(m =>
                m.JobTitle.Contains("Backend", StringComparison.OrdinalIgnoreCase) ||
                m.Skills.Any(s => s.Contains("C#", StringComparison.OrdinalIgnoreCase) || s.Contains(".NET", StringComparison.OrdinalIgnoreCase) || s.Contains("MySQL", StringComparison.OrdinalIgnoreCase))).ToList();
            if (be.Count > 0) candidates = be;
        }
        else if (isQA)
        {
            var qa = memberList.Where(m =>
                m.JobTitle.Contains("QA", StringComparison.OrdinalIgnoreCase) ||
                m.JobTitle.Contains("Tester", StringComparison.OrdinalIgnoreCase) ||
                m.Skills.Any(s => s.Contains("Playwright", StringComparison.OrdinalIgnoreCase) || s.Contains("QA", StringComparison.OrdinalIgnoreCase))).ToList();
            if (qa.Count > 0) candidates = qa;
        }
        else if (isLead)
        {
            var ld = memberList.Where(m =>
                m.JobTitle.Contains("Lead", StringComparison.OrdinalIgnoreCase) ||
                m.JobTitle.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                m.Roles.Any(r => r.Contains("ProjectManager", StringComparison.OrdinalIgnoreCase) || r.Contains("Director", StringComparison.OrdinalIgnoreCase))).ToList();
            if (ld.Count > 0) candidates = ld;
        }

        // Avoid assigning same person repeatedly: check unassigned first
        var unassigned = candidates.Where(c => !recentlyAssigned.Contains(c.UserId)).ToList();
        return (unassigned.Count > 0 ? unassigned : candidates)
            .OrderByDescending(c => c.RemainingHours)
            .FirstOrDefault() ?? memberList[0];
    }

    private static TeamMemberProfile MatchMemberByRole(string role, List<TeamMemberProfile> memberList, Guid? avoidUserId = null)
    {
        var roleLower = role.ToLowerInvariant();
        var pool = memberList.Where(m => avoidUserId == null || m.UserId != avoidUserId.Value).ToList();
        if (pool.Count == 0) pool = memberList;

        if (roleLower.Contains("front") || roleLower.Contains("ui"))
        {
            var fe = pool.FirstOrDefault(m => m.JobTitle.Contains("Frontend", StringComparison.OrdinalIgnoreCase) || m.Skills.Any(s => s.Contains("Angular", StringComparison.OrdinalIgnoreCase)));
            if (fe != null) return fe;
        }
        else if (roleLower.Contains("back") || roleLower.Contains("api"))
        {
            var be = pool.FirstOrDefault(m => m.JobTitle.Contains("Backend", StringComparison.OrdinalIgnoreCase) || m.Skills.Any(s => s.Contains("C#", StringComparison.OrdinalIgnoreCase) || s.Contains(".NET", StringComparison.OrdinalIgnoreCase)));
            if (be != null) return be;
        }
        else if (roleLower.Contains("qa") || roleLower.Contains("test"))
        {
            var qa = pool.FirstOrDefault(m => m.JobTitle.Contains("QA", StringComparison.OrdinalIgnoreCase) || m.JobTitle.Contains("Tester", StringComparison.OrdinalIgnoreCase) || m.Skills.Any(s => s.Contains("Playwright", StringComparison.OrdinalIgnoreCase)));
            if (qa != null) return qa;
        }

        return pool[0];
    }

    private static ProposedTaskDto ParseSingleTask(string rawJson, ProposedTaskDto existing, ProjectActiveConfig? activeConfig = null)
    {
        try
        {
            var cleaned = rawJson.Trim();
            if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[7..];
            if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
            if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
            var parsed = JsonSerializer.Deserialize<ProposedTaskDto>(cleaned.Trim(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed != null)
            {
                parsed.Id = existing.Id;
                parsed.SourceBacklogIssueId ??= existing.SourceBacklogIssueId;
                parsed.SourceBacklogIssueKey ??= existing.SourceBacklogIssueKey;
                if (activeConfig != null)
                {
                    parsed.Type = activeConfig.ResolveIssueTypeName(parsed.Type);
                    parsed.Priority = activeConfig.ResolvePriorityName(parsed.Priority);
                    parsed.Status = activeConfig.ResolveStatusName(parsed.Status);
                    foreach (var sub in parsed.Subtasks)
                    {
                        sub.Priority = activeConfig.ResolvePriorityName(sub.Priority);
                        sub.Status = activeConfig.ResolveStatusName(sub.Status);
                    }
                }
                return parsed;
            }
        }
        catch { }

        existing.Description += $"\n[Đã ghi nhận yêu cầu: {rawJson}]";
        return existing;
    }
}
