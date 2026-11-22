namespace ProjectMgmt.Solution.Services.Ai;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;

public interface IAiTaskQuickActionService
{
    Task<AiQuickActionPlanDto> RunQuickActionAsync(
        AiQuickActionRunRequest request,
        IProjectScopeContext scope,
        Func<int, int, string, string, int, string?, Task>? onStepProgress = null,
        Func<string, object?, string, long?, Task>? onToolCall = null,
        CancellationToken ct = default);
}

/// <summary>
/// Thực thi Trợ Lý AI không qua khung chat cho từng công việc (Phân rã Sub-task & Đề xuất phân công),
/// gọi MCP Tools + mô hình AI thật và lưu tạm kết quả hoàn toàn trên Redis chờ người dùng xác nhận.
/// </summary>
public sealed partial class AiTaskQuickActionService : IAiTaskQuickActionService
{
    private const string ActionKeyPrefix = "ai_quick_";

    private readonly IAiModelClient _aiClient;
    private readonly PlanningDbContext _planningDb;
    private readonly DeliveryIntelligenceDbContext _deliveryDb;
    private readonly IStateStagingService _stagingService;
    private readonly TeamTools _teamTools;
    private readonly SpecHubTools _specHubTools;
    private readonly ISkillCatalogReader? _skillCatalog;
    private readonly IAiTokenUsageTracker? _tokenTracker;
    private readonly ILogger<AiTaskQuickActionService>? _logger;

    public AiTaskQuickActionService(
        IAiModelClient aiClient,
        PlanningDbContext planningDb,
        DeliveryIntelligenceDbContext deliveryDb,
        IStateStagingService stagingService,
        TeamTools teamTools,
        SpecHubTools specHubTools,
        ISkillCatalogReader? skillCatalog = null,
        IAiTokenUsageTracker? tokenTracker = null,
        ILogger<AiTaskQuickActionService>? logger = null)
    {
        _aiClient = aiClient;
        _planningDb = planningDb;
        _deliveryDb = deliveryDb;
        _stagingService = stagingService;
        _teamTools = teamTools;
        _specHubTools = specHubTools;
        _skillCatalog = skillCatalog;
        _tokenTracker = tokenTracker;
        _logger = logger;
    }

    public async Task<AiQuickActionPlanDto> RunQuickActionAsync(
        AiQuickActionRunRequest request,
        IProjectScopeContext scope,
        Func<int, int, string, string, int, string?, Task>? onStepProgress = null,
        Func<string, object?, string, long?, Task>? onToolCall = null,
        CancellationToken ct = default)
    {
        ProjectScopeGuard.EnsureValidScope(scope);
        ProjectScopeGuard.ValidateProject(scope, request.ProjectId);

        var actionType = NormalizeActionType(request.ActionType);
        var traceItems = new List<AiTurnTraceItemDto>();
        var overallSw = Stopwatch.StartNew();

        async Task EmitStepAsync(int stepIdx, int totalSteps, string stepName, string labelVi, int percent, string? detail)
        {
            if (onStepProgress != null)
            {
                await onStepProgress(stepIdx, totalSteps, stepName, labelVi, percent, detail);
            }
        }

        async Task RecordToolAsync(string kind, string actionVerb, string target, string toolName, object? args, string summaryVi, long? durationMs)
        {
            traceItems.Add(new AiTurnTraceItemDto
            {
                Kind = kind,
                ActionVerb = actionVerb,
                Target = target,
                Summary = summaryVi,
                DurationMs = durationMs,
                Status = "done"
            });

            if (onToolCall != null)
            {
                await onToolCall(toolName, args, summaryVi, durationMs);
            }
        }

        // Bước 1: Đọc dữ liệu thật của công việc & cấu hình dự án
        var stepSw = Stopwatch.StartNew();
        await EmitStepAsync(1, 4, "READ_ISSUE_CONTEXT", "Đang đọc ngữ cảnh công việc và cấu hình dự án...", 20, null);

        var project = await _planningDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == scope.ProjectId && !p.IsDeleted && !p.IsArchived, ct)
            ?? throw new InvalidOperationException("Dự án không tồn tại hoặc đã bị lưu trữ/xóa.");

        ProjectScopeGuard.Validate(scope, project.OrgId, project.Id);

        var issue = await _deliveryDb.Issues.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.IssueId && i.ProjectId == project.Id && !i.IsDeleted, ct)
            ?? throw new InvalidOperationException("Công việc không tồn tại hoặc đã bị xóa.");

        var issueKey = $"{project.ProjectKey}-{issue.IssueNumber}";
        var activeConfig = await ProjectActiveConfigResolver.ResolveAsync(_planningDb, _deliveryDb, project.Id, ct);

        SprintInfo? sprintInfo = null;
        if (issue.SprintId.HasValue)
        {
            var sp = await _planningDb.Sprints.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == issue.SprintId.Value && s.ProjectId == project.Id, ct);
            if (sp != null)
            {
                sprintInfo = new SprintInfo(sp.Id, sp.Name, sp.Status);
                if (string.Equals(sp.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Công việc [{issueKey}] thuộc Sprint '{sp.Name}' đã đóng (Completed), không thể áp dụng thay đổi AI.");
                }
            }
        }

        if (actionType == "subtask_breakdown" && issue.ParentId.HasValue)
        {
            throw new InvalidOperationException($"Công việc [{issueKey}] đã là việc con (Sub-task); chỉ có thể phân rã Sub-task từ công việc cấp cha.");
        }

        var issueTypeName = await _planningDb.IssueTypes.AsNoTracking()
            .Where(t => t.Id == issue.IssueTypeId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "Story";

        var priorityName = activeConfig.Priorities.FirstOrDefault(p => p.Id == issue.PriorityId)?.Name
            ?? activeConfig.GetDefaultPriorityName();

        var existingCriteria = await _deliveryDb.AcceptanceCriteria.AsNoTracking()
            .Where(ac => ac.IssueId == issue.Id)
            .OrderBy(ac => ac.OrderIndex)
            .Select(ac => ac.Content)
            .ToListAsync(ct);

        var existingSubtasks = await _deliveryDb.Issues.AsNoTracking()
            .Where(i => i.ParentId == issue.Id && !i.IsDeleted)
            .Select(i => i.Title)
            .ToListAsync(ct);

        var requiredSkillIds = await _deliveryDb.IssueRequiredSkills.AsNoTracking()
            .Where(rs => rs.IssueId == issue.Id)
            .Select(rs => rs.SkillId)
            .ToListAsync(ct);

        var allSkills = _skillCatalog != null ? await _skillCatalog.GetSkillsAsync() : [];
        var requiredSkillNames = allSkills
            .Where(s => requiredSkillIds.Contains(s.Id))
            .Select(s => s.Name)
            .ToList();

        stepSw.Stop();
        await RecordToolAsync(
            "mcp",
            "Truy vấn MCP",
            "project_get_active_config",
            "project_get_active_config",
            new { projectId = project.Id, issueKey },
            $"Đã đọc cấu hình dự án và công việc [{issueKey}] ({issueTypeName})",
            stepSw.ElapsedMilliseconds);

        // Bước 2: Gọi MCP TeamTools + SpecHubTools để lấy năng lực đội ngũ & tài liệu đặc tả liên quan
        stepSw.Restart();
        await EmitStepAsync(2, 4, "FETCH_MCP_TEAM_SPECS", "Đang gọi MCP lấy năng lực đội ngũ và tài liệu Spec Hub...", 45, null);

        var teamJson = await _teamTools.GetDevCapacitiesAsync(project.Id, scope, ct);
        var teamMembers = ParseTeamMembers(teamJson);

        var searchKeyword = ExtractSearchKeyword(issue.Title);
        var specJson = string.IsNullOrWhiteSpace(searchKeyword)
            ? "[]"
            : await _specHubTools.SearchAsync(project.Id, searchKeyword, scope, ct);
        var specDocs = await _planningDb.ProjectDocuments.AsNoTracking()
            .Where(d => d.ProjectId == project.Id && !d.IsDirectory)
            .Select(d => new SpecDocMeta(d.Id, string.IsNullOrWhiteSpace(d.Name) ? d.Title : d.Name))
            .ToListAsync(ct);
        var specMatches = ParseSpecSearchMatches(specJson);

        stepSw.Stop();
        await RecordToolAsync(
            "mcp",
            "Truy vấn MCP",
            "team_get_dev_capacities",
            "team_get_dev_capacities",
            new { projectId = project.Id, members = teamMembers.Count },
            $"Đã truy xuất năng lực thực tế của {teamMembers.Count} thành viên & {specMatches.Count} tài liệu Spec Hub liên quan",
            stepSw.ElapsedMilliseconds);

        // Bước 3: Suy luận bằng Trợ Lý AI thật (LLM Engine + Prompt Guardrail)
        stepSw.Restart();
        await EmitStepAsync(3, 4, "AI_REASONING", "Trợ Lý AI đang phân tích và tổng hợp đề xuất...", 75, null);

        var safeTitle = InputGuardrail.SanitizeDocumentContent(issue.Title).SanitizedText;
        var safeDescription = InputGuardrail.SanitizeDocumentContent(issue.Description ?? string.Empty).SanitizedText;
        var safeCustomInstruction = !string.IsNullOrWhiteSpace(request.CustomInstruction)
            ? InputGuardrail.Sanitize(request.CustomInstruction).SanitizedText
            : null;

        var prompt = actionType == "assignee_suggestion"
            ? BuildAssigneePrompt(issueKey, safeTitle, safeDescription, issueTypeName, priorityName, issue.StoryPoints ?? 3, requiredSkillNames, existingCriteria, teamJson, specJson, safeCustomInstruction, request.Model)
            : BuildSubtaskPrompt(issueKey, safeTitle, safeDescription, issueTypeName, priorityName, issue.StoryPoints ?? 3, existingCriteria, existingSubtasks, requiredSkillNames, activeConfig, teamJson, specJson, safeCustomInstruction, request.Model);

        string? rawAiContent = null;
        string? usedModel = request.Model;
        var usedLlm = false;
        AiUsageInfo? usageInfo = null;

        try
        {
            var aiResult = await _aiClient.GenerateAsync(prompt, scope, ct);
            usedModel = aiResult.Model ?? request.Model;
            usageInfo = aiResult.Usage;
            if (aiResult.IsSuccess && !string.IsNullOrWhiteSpace(aiResult.Content))
            {
                rawAiContent = aiResult.Content;
                usedLlm = true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[AiTaskQuickActionService] Không thể gọi LLM cho {IssueKey}, chuyển sang tổng hợp chuẩn từ dữ liệu MCP", issueKey);
        }

        stepSw.Stop();

        var actionId = $"{ActionKeyPrefix}{Guid.NewGuid():N}";
        if (_tokenTracker != null)
        {
            _tokenTracker.RecordUsage(new AiUsageRecord(
                actionId,
                scope.UserId,
                scope.ProjectId,
                usedModel ?? "gemini-default",
                usageInfo?.PromptTokens ?? Math.Max(150, prompt.Messages.Sum(m => m.Content.Length / 4)),
                usageInfo?.CompletionTokens ?? Math.Max(80, (rawAiContent?.Length ?? 320) / 4),
                overallSw.ElapsedMilliseconds,
                usedLlm,
                DateTime.UtcNow,
                $"QuickAction:{actionType}"));
        }

        await RecordToolAsync(
            "reasoning",
            "Suy luận AI",
            "Mô hình Trợ lý AI",
            "gemini_reasoner",
            new { actionType, issueKey, usedLlm },
            usedLlm
                ? $"Trợ Lý AI đã hoàn tất phân tích đề xuất cho [{issueKey}]"
                : $"Đã tổng hợp đề xuất chuẩn từ dữ liệu MCP cho [{issueKey}]",
            stepSw.ElapsedMilliseconds);

        // Bước 4: Chuẩn hóa dữ liệu đối chiếu với nhân sự thật & lưu tạm hoàn toàn trên Redis
        await EmitStepAsync(4, 4, "STAGE_REDIS", "Đang đối chiếu nhân sự & lưu tạm kế hoạch lên Redis...", 95, null);

        var plan = new AiQuickActionPlanDto
        {
            ActionId = actionId,
            ProjectId = project.Id,
            OrganizationId = project.OrgId,
            UserId = scope.UserId,
            IssueId = issue.Id,
            IssueKey = issueKey,
            IssueTitle = issue.Title,
            IssueType = issueTypeName,
            SprintId = sprintInfo?.Id,
            SprintName = sprintInfo?.Name ?? "Product Backlog",
            ActionType = actionType,
            Status = "pending",
            StagedInRedis = true,
            UsedLlm = usedLlm,
            ModelUsed = usedModel,
            DocumentCitations = specMatches.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList(),
            TraceItems = traceItems,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (actionType == "assignee_suggestion")
        {
            PopulateAssigneeCandidates(plan, rawAiContent, issue.Title, issue.Description ?? string.Empty, requiredSkillNames, teamMembers);
            var top = plan.AssigneeCandidates.FirstOrDefault();
            plan.Summary = top != null
                ? $"Đề xuất phân công [{issueKey}] cho {top.DisplayName} (Điểm phù hợp {top.TotalScore:0.#}/100)"
                : $"Chưa tìm thấy thành viên khả dụng trong dự án cho [{issueKey}]";
        }
        else
        {
            PopulateSubtaskCandidates(plan, rawAiContent, issue.Title, issue.StoryPoints ?? 3, existingSubtasks, requiredSkillNames, activeConfig, teamMembers, specDocs);
            plan.Summary = $"Đã phân rã {plan.ProposedSubtasks.Count} Sub-task cho [{issueKey}] · {plan.ProposedSubtasks.Sum(s => s.EstimatedHours):0.#}h";
        }

        await _stagingService.SaveProposalAsync(actionId, plan, TimeSpan.FromHours(2), ct);

        await EmitStepAsync(4, 4, "STAGE_REDIS", "Đã lưu tạm trên Redis, chờ bạn xác nhận áp dụng.", 100, null);
        return plan;
    }

    private static string NormalizeActionType(string? actionType)
    {
        var clean = (actionType ?? string.Empty).Trim().ToLowerInvariant();
        return clean switch
        {
            "assignee_suggestion" or "ai-assign" or "assign" => "assignee_suggestion",
            _ => "subtask_breakdown"
        };
    }

    private static AiPrompt BuildSubtaskPrompt(
        string issueKey,
        string safeTitle,
        string safeDescription,
        string issueTypeName,
        string priorityName,
        decimal storyPoints,
        IReadOnlyList<string> existingCriteria,
        IReadOnlyList<string> existingSubtasks,
        IReadOnlyList<string> requiredSkills,
        ProjectActiveConfig activeConfig,
        string teamJson,
        string specJson,
        string? customInstruction,
        string? model)
    {
        var acText = existingCriteria.Count > 0 ? string.Join("\n- ", existingCriteria) : "Chưa có";
        var existingSubsText = existingSubtasks.Count > 0 ? string.Join(", ", existingSubtasks) : "Chưa có";
        var skillsText = requiredSkills.Count > 0 ? string.Join(", ", requiredSkills) : "Chưa gắn nhãn kỹ năng";
        var prioritiesText = string.Join(", ", activeConfig.Priorities.Select(p => p.Name));

        var userText = $$"""
Hãy phân rã công việc [{{issueKey}}] ({{issueTypeName}}, {{storyPoints}} SP, ưu tiên {{priorityName}}) thành 3-5 Sub-task thực thi cụ thể (Backend / Frontend / QA / DevOps nếu phù hợp).

Tiêu đề: {{safeTitle}}
Mô tả: {{safeDescription}}
Tiêu chí nghiệm thu (AC):
- {{acText}}
Kỹ năng yêu cầu: {{skillsText}}
Sub-task đã tồn tại (TRÁNH trùng lặp): {{existingSubsText}}
Mức ưu tiên hợp lệ của dự án: {{prioritiesText}}

Dữ liệu thành viên dự án thật từ MCP (chỉ chọn đúng tên thành viên có trong danh sách này):
{{teamJson}}

Tài liệu Spec Hub liên quan:
{{specJson}}
{{(string.IsNullOrWhiteSpace(customInstruction) ? string.Empty : $"\nChỉ dẫn bổ sung: {customInstruction}")}}

Trả về DUY NHẤT một JSON object hợp lệ theo mẫu:
{
  "rationale": "Lý do phân rã ngắn gọn bằng tiếng Việt",
  "subtasks": [
    {
      "title": "Tên Sub-task ngắn gọn, rõ đầu ra kỹ thuật",
      "role": "Backend | Frontend | QA | DevOps | Design",
      "description": "Mô tả chi tiết phạm vi kỹ thuật",
      "estimatedHours": 4,
      "storyPoints": 2,
      "priority": "{{priorityName}}",
      "assigneeName": "Tên thành viên thật trong TEAM",
      "acceptanceCriteria": ["Tiêu chí kiểm chứng cụ thể 1", "Tiêu chí 2"],
      "suggestedSkills": ["C#", "Angular"]
    }
  ]
}
""";

        return new AiPrompt
        {
            Model = model,
            SystemInstruction = "Bạn là Kỹ sư trưởng & Scrum Master AI. Phân rã 1 công việc cha thành các Sub-task độc lập, rõ ràng, không trùng lặp với Sub-task hiện có, ước lượng giờ và chọn đúng thành viên có thật từ dữ liệu MCP TeamTools. Luôn trả lời JSON hợp lệ bằng tiếng Việt.",
            Messages = [AiMessage.CreateUser(userText)],
            Temperature = 0.2
        };
    }

    private static AiPrompt BuildAssigneePrompt(
        string issueKey,
        string safeTitle,
        string safeDescription,
        string issueTypeName,
        string priorityName,
        decimal storyPoints,
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<string> existingCriteria,
        string teamJson,
        string specJson,
        string? customInstruction,
        string? model)
    {
        var skillsText = requiredSkills.Count > 0 ? string.Join(", ", requiredSkills) : "Suy luận từ tiêu đề và mô tả";
        var acText = existingCriteria.Count > 0 ? string.Join("; ", existingCriteria) : "Chưa có";

        var userText = $$"""
Hãy đánh giá và xếp hạng tối đa 5 ứng viên phù hợp nhất trong đội dự án để thực hiện công việc [{{issueKey}}]:
- Tiêu đề: {{safeTitle}}
- Loại: {{issueTypeName}} | Ưu tiên: {{priorityName}} | Story Points: {{storyPoints}}
- Mô tả: {{safeDescription}}
- Tiêu chí AC: {{acText}}
- Kỹ năng yêu cầu: {{skillsText}}

Dữ liệu năng lực, kỹ năng, tải công việc Sprint hiện tại và lịch sử hoàn thành thật từ MCP TeamTools:
{{teamJson}}

Tài liệu Spec Hub liên quan:
{{specJson}}
{{(string.IsNullOrWhiteSpace(customInstruction) ? string.Empty : $"\nChỉ dẫn bổ sung: {customInstruction}")}}

Trả về DUY NHẤT JSON object theo cấu trúc:
{
  "rationale": "Tổng hợp lý do xếp hạng đội ngũ bằng tiếng Việt",
  "candidates": [
    {
      "userId": "GUID thành viên từ MCP",
      "displayName": "Tên thành viên",
      "totalScore": 91.5,
      "loadBalanceScore": 88,
      "skillMatchScore": 95,
      "historyScore": 90,
      "capacityScore": 92,
      "reason": "Giải thích ngắn gọn dựa trên kỹ năng khớp, tải việc hiện tại và lịch sử sprint"
    }
  ]
}
""";

        return new AiPrompt
        {
            Model = model,
            SystemInstruction = "Bạn là Chuyên gia Điều phối Nhân lực Agile. Đánh giá công bằng kỹ năng chuyên môn, khối lượng việc đang gánh (ActiveSprintInProgressCount), giờ rảnh còn lại (RemainingAvailableHours) và lịch sử hoàn thành để xếp hạng thành viên phù hợp nhất. Chỉ chọn đúng UserId từ dữ liệu MCP, trả JSON hợp lệ.",
            Messages = [AiMessage.CreateUser(userText)],
            Temperature = 0.15
        };
    }

    private static void PopulateSubtaskCandidates(
        AiQuickActionPlanDto plan,
        string? rawAiContent,
        string parentTitle,
        decimal parentSp,
        IReadOnlyList<string> existingSubtasks,
        IReadOnlyList<string> requiredSkills,
        ProjectActiveConfig activeConfig,
        IReadOnlyList<TeamMemberInfo> teamMembers,
        IReadOnlyList<SpecDocMeta> specDocs)
    {
        var existingSet = new HashSet<string>(existingSubtasks.Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        var defaultPriority = activeConfig.GetDefaultPriorityName();
        var candidates = new List<AiQuickSubtaskCandidateDto>();
        string? parsedRationale = null;

        if (!string.IsNullOrWhiteSpace(rawAiContent))
        {
            try
            {
                var jsonText = AiSprintDecompositionService.ExtractJsonObject(rawAiContent);
                if (!TryParseJsonObject(jsonText, out var rootObj) &&
                    AiSprintDecompositionService.TryRepairTruncatedJson(rawAiContent, out var repaired))
                {
                    TryParseJsonObject(repaired, out rootObj);
                }

                if (rootObj != null)
                {
                    parsedRationale = ReadString(rootObj, "rationale");
                    var arr = (rootObj["subtasks"] ?? rootObj["items"] ?? rootObj["proposedSubtasks"])?.AsArray();
                    if (arr != null)
                    {
                        var idx = 1;
                        foreach (var node in arr)
                        {
                            if (node is not JsonObject item) continue;
                            var rawTitle = ReadString(item, "title");
                            if (string.IsNullOrWhiteSpace(rawTitle)) continue;

                            var cleanTitle = InputGuardrail.StripUnsafeHtmlOrScript(rawTitle).Trim();
                            var role = NormalizeRole(ReadString(item, "role"), cleanTitle);
                            if (existingSet.Contains(cleanTitle) || existingSet.Contains($"[{role}] {cleanTitle}"))
                            {
                                continue;
                            }

                            var estHours = ReadDecimal(item, "estimatedHours", 4m);
                            estHours = Math.Clamp(estHours <= 0 ? 4m : estHours, 1m, 40m);
                            var sp = (int)Math.Clamp(ReadDecimal(item, "storyPoints", 1m), 1m, 8m);
                            var prio = activeConfig.ResolvePriorityEntity(ReadString(item, "priority"))?.Name ?? defaultPriority;
                            var matchedMember = MatchMember(teamMembers, ReadString(item, "assigneeUserId"), ReadString(item, "assigneeName"), role);

                            var acList = ReadStringArray(item, "acceptanceCriteria");
                            var skills = ReadStringArray(item, "suggestedSkills");
                            if (skills.Count == 0 && requiredSkills.Count > 0)
                            {
                                skills.AddRange(requiredSkills.Take(3));
                            }

                            candidates.Add(new AiQuickSubtaskCandidateDto
                            {
                                TempId = $"sub_{idx++}",
                                Title = StripRolePrefix(cleanTitle),
                                Description = InputGuardrail.StripUnsafeHtmlOrScript(ReadString(item, "description") ?? $"Thực hiện hạng mục {cleanTitle} cho {parentTitle}."),
                                Role = role,
                                EstimatedHours = estHours,
                                StoryPoints = sp,
                                Priority = prio,
                                SuggestedAssigneeUserId = matchedMember?.UserId,
                                SuggestedAssigneeName = matchedMember?.DisplayName,
                                AcceptanceCriteria = acList.Count > 0 ? acList : [$"Hoàn tất & kiểm thử {cleanTitle} đạt chuẩn DoD."],
                                SuggestedSkills = skills,
                                DocumentCitations = plan.DocumentCitations.Take(2).ToList(),
                                DocumentId = specDocs.FirstOrDefault()?.Id,
                                Selected = true
                            });
                        }
                    }
                }
            }
            catch
            {
                candidates.Clear();
            }
        }

        if (candidates.Count == 0)
        {
            candidates.AddRange(BuildDeterministicSubtasks(parentTitle, parentSp, existingSet, requiredSkills, defaultPriority, teamMembers, plan.DocumentCitations, specDocs));
        }

        plan.ProposedSubtasks = candidates.Take(6).ToList();
        plan.Rationale = !string.IsNullOrWhiteSpace(parsedRationale)
            ? InputGuardrail.StripUnsafeHtmlOrScript(parsedRationale)
            : $"Phân rã [{plan.IssueKey}] thành {plan.ProposedSubtasks.Count} việc con theo vai trò kỹ thuật và năng lực đội ngũ thực tế.";
    }

    private static void PopulateAssigneeCandidates(
        AiQuickActionPlanDto plan,
        string? rawAiContent,
        string issueTitle,
        string issueDescription,
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<TeamMemberInfo> teamMembers)
    {
        var deterministicScores = ComputeDeterministicScores(issueTitle, issueDescription, requiredSkills, teamMembers)
            .ToDictionary(c => c.UserId);
        var result = new List<AiQuickAssigneeCandidateDto>();
        string? parsedRationale = null;

        if (!string.IsNullOrWhiteSpace(rawAiContent) && teamMembers.Count > 0)
        {
            try
            {
                var jsonText = AiSprintDecompositionService.ExtractJsonObject(rawAiContent);
                if (TryParseJsonObject(jsonText, out var rootObj) && rootObj != null)
                {
                    parsedRationale = ReadString(rootObj, "rationale");
                    var arr = (rootObj["candidates"] ?? rootObj["rankings"])?.AsArray();
                    if (arr != null)
                    {
                        var seenIds = new HashSet<Guid>();
                        foreach (var node in arr)
                        {
                            if (node is not JsonObject item) continue;
                            var matched = MatchMember(teamMembers, ReadString(item, "userId"), ReadString(item, "displayName"), null);
                            if (matched == null || !seenIds.Add(matched.UserId)) continue;

                            deterministicScores.TryGetValue(matched.UserId, out var baseScore);
                            var totalScore = ClampScore(ReadDecimal(item, "totalScore", baseScore?.TotalScore ?? 75m));
                            var loadScore = ClampScore(ReadDecimal(item, "loadBalanceScore", baseScore?.LoadBalanceScore ?? 75m));
                            var skillScore = ClampScore(ReadDecimal(item, "skillMatchScore", baseScore?.SkillMatchScore ?? 75m));
                            var histScore = ClampScore(ReadDecimal(item, "historyScore", baseScore?.HistoryScore ?? 70m));
                            var capScore = ClampScore(ReadDecimal(item, "capacityScore", baseScore?.CapacityScore ?? 80m));
                            var reason = InputGuardrail.StripUnsafeHtmlOrScript(ReadString(item, "reason") ?? baseScore?.Reason ?? string.Empty);

                            result.Add(new AiQuickAssigneeCandidateDto
                            {
                                Rank = result.Count + 1,
                                UserId = matched.UserId,
                                DisplayName = matched.DisplayName,
                                JobTitle = matched.JobTitle,
                                SeniorityLevel = matched.SeniorityLevel,
                                Roles = matched.Roles,
                                TotalScore = totalScore,
                                LoadBalanceScore = loadScore,
                                SkillMatchScore = skillScore,
                                HistoryScore = histScore,
                                CapacityScore = capScore,
                                ActiveSprintTasksCount = matched.ActiveTasksCount,
                                RemainingAvailableHours = matched.RemainingHours,
                                Skills = matched.SkillNames.Take(4).ToList(),
                                Reason = string.IsNullOrWhiteSpace(reason)
                                    ? BuildMemberReason(matched, skillScore, loadScore)
                                    : reason,
                                IsRecommended = false
                            });
                        }
                    }
                }
            }
            catch
            {
                result.Clear();
            }
        }

        // Bổ sung các thành viên còn lại theo điểm thực tế nếu AI trả chưa đủ
        foreach (var fallback in deterministicScores.Values.OrderByDescending(c => c.TotalScore))
        {
            if (result.All(r => r.UserId != fallback.UserId))
            {
                result.Add(fallback);
            }
        }

        result = result
            .OrderByDescending(c => c.TotalScore)
            .ThenByDescending(c => c.RemainingAvailableHours)
            .Take(5)
            .ToList();

        for (var i = 0; i < result.Count; i++)
        {
            result[i].Rank = i + 1;
            result[i].IsRecommended = i == 0;
        }

        plan.AssigneeCandidates = result;
        plan.RecommendedUserId = result.FirstOrDefault()?.UserId;
        plan.Rationale = !string.IsNullOrWhiteSpace(parsedRationale)
            ? InputGuardrail.StripUnsafeHtmlOrScript(parsedRationale)
            : (result.FirstOrDefault() is { } top
                ? $"Xếp hạng dựa trên dữ liệu thực tế: mức khớp kỹ năng ({top.SkillMatchScore:0.#}%), cân bằng tải ({top.ActiveSprintTasksCount} việc đang làm) và {top.RemainingAvailableHours:0.#}h khả dụng của {top.DisplayName}."
                : "Chưa có thành viên khả dụng để xếp hạng.");
    }

    private static List<AiQuickSubtaskCandidateDto> BuildDeterministicSubtasks(
        string parentTitle,
        decimal parentSp,
        HashSet<string> existingSet,
        IReadOnlyList<string> requiredSkills,
        string defaultPriority,
        IReadOnlyList<TeamMemberInfo> teamMembers,
        IReadOnlyList<string> citations,
        IReadOnlyList<SpecDocMeta> specDocs)
    {
        var baseHours = Math.Clamp(Math.Max(6m, parentSp * 4m), 6m, 24m);
        var templates = new[]
        {
            ("Backend", $"Thiết kế & cài đặt API xử lý cho: {parentTitle}", Math.Round(baseHours * 0.4m, 1), 2),
            ("Frontend", $"Xây dựng giao diện và tích hợp luồng cho: {parentTitle}", Math.Round(baseHours * 0.35m, 1), 2),
            ("QA", $"Viết test case & kiểm thử xác nhận nghiệm thu: {parentTitle}", Math.Round(baseHours * 0.25m, 1), 1)
        };

        var list = new List<AiQuickSubtaskCandidateDto>();
        var idx = 1;
        foreach (var (role, title, hours, sp) in templates)
        {
            if (existingSet.Contains(title) || existingSet.Contains($"[{role}] {title}")) continue;
            var member = MatchMember(teamMembers, null, null, role);
            list.Add(new AiQuickSubtaskCandidateDto
            {
                TempId = $"sub_{idx++}",
                Title = title,
                Description = $"Thực thi phần việc {role} cho công việc: {parentTitle}.",
                Role = role,
                EstimatedHours = Math.Clamp(hours, 2m, 24m),
                StoryPoints = sp,
                Priority = defaultPriority,
                SuggestedAssigneeUserId = member?.UserId,
                SuggestedAssigneeName = member?.DisplayName,
                AcceptanceCriteria = [$"Hoàn tất hạng mục {role} và kiểm chứng không phát sinh lỗi hồi quy."],
                SuggestedSkills = requiredSkills.Take(2).ToList(),
                DocumentCitations = citations.Take(2).ToList(),
                DocumentId = specDocs.FirstOrDefault()?.Id,
                Selected = true
            });
        }
        return list;
    }

    private static List<AiQuickAssigneeCandidateDto> ComputeDeterministicScores(
        string issueTitle,
        string issueDescription,
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<TeamMemberInfo> teamMembers)
    {
        var tokens = TokenizeText($"{issueTitle} {issueDescription} {string.Join(' ', requiredSkills)}");
        var list = new List<AiQuickAssigneeCandidateDto>();

        foreach (var m in teamMembers)
        {
            // Loại trừ Viewer khỏi gợi ý mặc định nếu đội ngũ có Dev/QA/Manager
            var isViewerOnly = m.Roles.Count > 0 && m.Roles.All(r => r.Contains("Viewer", StringComparison.OrdinalIgnoreCase));
            var skillHitCount = 0;
            foreach (var sk in m.SkillNames)
            {
                var skTokens = TokenizeText(sk);
                if (skTokens.Any(t => tokens.Contains(t))) skillHitCount++;
            }

            var jobTokens = TokenizeText($"{m.JobTitle} {string.Join(' ', m.Roles)}");
            var roleKeywordBonus = jobTokens.Any(t => tokens.Contains(t)) ? 18m : 0m;
            var skillScore = Math.Clamp(52m + skillHitCount * 16m + roleKeywordBonus, 35m, 98m);
            if (isViewerOnly) skillScore = Math.Min(skillScore, 30m);

            var loadScore = Math.Clamp(100m - m.ActiveTasksCount * 16m, 20m, 98m);
            var capacityScore = Math.Clamp((m.RemainingHours / Math.Max(1m, m.AvailableHours)) * 100m, 15m, 98m);
            var historyScore = Math.Clamp(58m + m.CompletedTasksCount * 6m, 50m, 96m);
            var total = Math.Round(skillScore * 0.35m + loadScore * 0.35m + capacityScore * 0.15m + historyScore * 0.15m, 1);

            list.Add(new AiQuickAssigneeCandidateDto
            {
                UserId = m.UserId,
                DisplayName = m.DisplayName,
                JobTitle = m.JobTitle,
                SeniorityLevel = m.SeniorityLevel,
                Roles = m.Roles,
                TotalScore = total,
                LoadBalanceScore = Math.Round(loadScore, 1),
                SkillMatchScore = Math.Round(skillScore, 1),
                HistoryScore = Math.Round(historyScore, 1),
                CapacityScore = Math.Round(capacityScore, 1),
                ActiveSprintTasksCount = m.ActiveTasksCount,
                RemainingAvailableHours = m.RemainingHours,
                Skills = m.SkillNames.Take(4).ToList(),
                Reason = BuildMemberReason(m, skillScore, loadScore)
            });
        }

        return list.OrderByDescending(c => c.TotalScore).ToList();
    }

    private static string BuildMemberReason(TeamMemberInfo m, decimal skillScore, decimal loadScore)
    {
        var skillHint = m.SkillNames.Count > 0 ? string.Join(", ", m.SkillNames.Take(2)) : m.JobTitle;
        return $"{m.JobTitle} ({skillHint}); đang xử lý {m.ActiveTasksCount} việc (điểm tải {loadScore:0.#}), còn {m.RemainingHours:0.#}h khả dụng, khớp kỹ năng {skillScore:0.#}%.";
    }

    private static List<TeamMemberInfo> ParseTeamMembers(string teamJson)
    {
        var list = new List<TeamMemberInfo>();
        try
        {
            using var doc = JsonDocument.Parse(teamJson);
            if (!doc.RootElement.TryGetProperty("Capacities", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var elem in arr.EnumerateArray())
            {
                if (!elem.TryGetProperty("UserId", out var uidProp) || !Guid.TryParse(uidProp.GetString(), out var userId))
                {
                    continue;
                }

                var displayName = elem.TryGetProperty("DisplayName", out var dn) ? dn.GetString() ?? string.Empty : string.Empty;
                var jobTitle = elem.TryGetProperty("JobTitle", out var jt) ? jt.GetString() ?? "Kỹ sư" : "Kỹ sư";
                var seniority = elem.TryGetProperty("SeniorityLevel", out var sn) ? sn.GetString() ?? "Senior" : "Senior";
                var availableHours = elem.TryGetProperty("AvailableHours", out var ah) && ah.TryGetDecimal(out var ahVal) ? ahVal : 40m;
                var remainingHours = elem.TryGetProperty("RemainingAvailableHours", out var rh) && rh.TryGetDecimal(out var rhVal) ? rhVal : availableHours;

                var roles = new List<string>();
                if (elem.TryGetProperty("Roles", out var rolesEl) && rolesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in rolesEl.EnumerateArray())
                    {
                        var val = r.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) roles.Add(val);
                    }
                }

                var skillNames = new List<string>();
                if (elem.TryGetProperty("SkillProfiles", out var spEl) && spEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sp in spEl.EnumerateArray())
                    {
                        var snVal = sp.TryGetProperty("SkillName", out var snp) ? snp.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(snVal)) skillNames.Add(snVal);
                    }
                }

                var activeTasksCount = 0;
                var completedTasksCount = 0;
                if (elem.TryGetProperty("HistoricalPerformance", out var hp) && hp.ValueKind == JsonValueKind.Object)
                {
                    if (hp.TryGetProperty("ActiveSprintInProgressCount", out var ac) && ac.TryGetInt32(out var acVal)) activeTasksCount = acVal;
                    if (hp.TryGetProperty("CompletedTasksCount", out var cc) && cc.TryGetInt32(out var ccVal)) completedTasksCount = ccVal;
                }

                list.Add(new TeamMemberInfo(userId, displayName, jobTitle, seniority, availableHours, remainingHours, roles, skillNames, activeTasksCount, completedTasksCount));
            }
        }
        catch
        {
            // Ignore parse glitches gracefully
        }
        return list;
    }

    private static List<SpecMatchMeta> ParseSpecSearchMatches(string specJson)
    {
        var list = new List<SpecMatchMeta>();
        try
        {
            using var doc = JsonDocument.Parse(specJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var id = elem.TryGetProperty("Id", out var idProp) && Guid.TryParse(idProp.GetString(), out var g) ? g : Guid.Empty;
                var name = elem.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    list.Add(new SpecMatchMeta(id, name));
                }
            }
        }
        catch
        {
            // Ignore malformed snippet search response
        }
        return list;
    }

    private static TeamMemberInfo? MatchMember(
        IReadOnlyList<TeamMemberInfo> members,
        string? rawUserId,
        string? rawName,
        string? preferredRole)
    {
        if (Guid.TryParse(rawUserId, out var gid))
        {
            var byId = members.FirstOrDefault(m => m.UserId == gid);
            if (byId != null) return byId;
        }

        if (!string.IsNullOrWhiteSpace(rawName))
        {
            var token = AiSprintCommitCoordinator.NormalizeLookupToken(rawName);
            var exact = members.FirstOrDefault(m =>
                m.DisplayName.Equals(rawName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                AiSprintCommitCoordinator.NormalizeLookupToken(m.DisplayName) == token);
            if (exact != null) return exact;

            var partial = members.FirstOrDefault(m =>
                !string.IsNullOrEmpty(token) &&
                AiSprintCommitCoordinator.NormalizeLookupToken(m.DisplayName).Contains(token, StringComparison.Ordinal));
            if (partial != null) return partial;
        }

        if (!string.IsNullOrWhiteSpace(preferredRole))
        {
            var roleToken = preferredRole.ToLowerInvariant();
            var roleMatched = members
                .Where(m => m.JobTitle.Contains(roleToken, StringComparison.OrdinalIgnoreCase) ||
                            m.Roles.Any(r => r.Contains(roleToken, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(m => m.RemainingHours)
                .FirstOrDefault();
            if (roleMatched != null) return roleMatched;
        }

        return members.OrderByDescending(m => m.RemainingHours).FirstOrDefault();
    }

    private static string ExtractSearchKeyword(string title)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 3)
            .Take(3);
        return string.Join(' ', words);
    }

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{N}])(qa|qc|tester?|e2e)(?![\p{L}\p{N}])|kiểm\s*thử|unit\s*test|integration\s*test")]
    private static partial Regex QaRoleRegex();

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{N}])(frontend|front-end|angular|react|vue|tailwind|scss|css|html|ui|ux|ui/ux)(?![\p{L}\p{N}])|giao\s*diện")]
    private static partial Regex FrontendRoleRegex();

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{N}])(devops|docker|kubernetes|k8s|ci/cd|terraform|infra)(?![\p{L}\p{N}])")]
    private static partial Regex DevOpsRoleRegex();

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{N}])(design|designer|figma)(?![\p{L}\p{N}])|thiết\s*kế\s*(ui|ux|giao\s*diện|figma)")]
    private static partial Regex DesignRoleRegex();

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{N}])(backend|back-end|api|server|sql|mysql|redis|database|microservice|middleware)(?![\p{L}\p{N}])|cơ\s*sở\s*dữ\s*liệu")]
    private static partial Regex BackendRoleRegex();

    private static string? TryMatchRoleText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (QaRoleRegex().IsMatch(text)) return "QA";
        if (FrontendRoleRegex().IsMatch(text)) return "Frontend";
        if (DevOpsRoleRegex().IsMatch(text)) return "DevOps";
        if (DesignRoleRegex().IsMatch(text)) return "Design";
        if (BackendRoleRegex().IsMatch(text)) return "Backend";
        return null;
    }

    internal static string NormalizeRole(string? rawRole, string title)
    {
        if (!string.IsNullOrWhiteSpace(rawRole))
        {
            var explicitMatch = TryMatchRoleText(rawRole);
            if (explicitMatch != null) return explicitMatch;

            var trimmed = rawRole.Trim();
            var isGenericRole = trimmed.Equals("developer", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("dev", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("engineer", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("task", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("general", StringComparison.OrdinalIgnoreCase);

            if (!isGenericRole) return trimmed;
        }

        return TryMatchRoleText(title) ?? "Backend";
    }

    private static string StripRolePrefix(string title)
    {
        if (title.StartsWith('[') && title.IndexOf(']') is var closeIdx && closeIdx > 1 && closeIdx + 1 < title.Length)
        {
            return title[(closeIdx + 1)..].Trim();
        }
        return title;
    }

    private static HashSet<string> TokenizeText(string text)
    {
        var normalized = AiSprintCommitCoordinator.NormalizeLookupToken(text);
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static decimal ClampScore(decimal val) => Math.Clamp(Math.Round(val, 1), 0m, 100m);

    private static bool TryParseJsonObject(string json, out JsonObject? obj)
    {
        obj = null;
        try
        {
            obj = JsonNode.Parse(json) as JsonObject;
            return obj != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadString(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node == null) return null;
        return node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node.ToString();
    }

    private static decimal ReadDecimal(JsonObject obj, string key, decimal fallback)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node == null) return fallback;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<decimal>(out var d)) return d;
            if (v.TryGetValue<double>(out var dbl)) return (decimal)dbl;
            if (v.TryGetValue<int>(out var i)) return i;
            if (v.TryGetValue<string>(out var s) && decimal.TryParse(s, out var parsed)) return parsed;
        }
        return fallback;
    }

    private static List<string> ReadStringArray(JsonObject obj, string key)
    {
        var list = new List<string>();
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonArray arr) return list;
        foreach (var elem in arr)
        {
            var raw = elem?.ToString();
            var safe = InputGuardrail.StripUnsafeHtmlOrScript(raw ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(safe)) list.Add(safe);
        }
        return list;
    }

    private sealed record SprintInfo(Guid Id, string Name, string Status);
    private sealed record SpecDocMeta(Guid Id, string Name);
    private sealed record SpecMatchMeta(Guid Id, string Name);
    private sealed record TeamMemberInfo(
        Guid UserId,
        string DisplayName,
        string JobTitle,
        string SeniorityLevel,
        decimal AvailableHours,
        decimal RemainingHours,
        List<string> Roles,
        List<string> SkillNames,
        int ActiveTasksCount,
        int CompletedTasksCount);
}
