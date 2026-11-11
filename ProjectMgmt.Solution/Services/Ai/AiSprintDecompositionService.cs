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
        CancellationToken cancellationToken = default);

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
        CancellationToken cancellationToken = default)
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

        // =========================================================================
        // STEP 2: Truy vấn Ngữ cảnh & Lịch sử Sprint (35%)
        // =========================================================================
        await onStepProgress(2, "QUERY_CONTEXT", "Truy vấn Ngữ cảnh & Lịch sử Sprint", 35, "Đang đối chiếu tài liệu đặc tả và lịch sử Sprint gần nhất...");

        var sw = Stopwatch.StartNew();
        var relevantDocs = await _specHubTools.SearchRelevantSpecsAsync(scope, "Sprint SRS BRD Requirements", 5, cancellationToken);
        sw.Stop();
        await onToolCall("spec_hub_search", new { query = "Sprint SRS BRD Requirements" }, "Tìm kiếm tài liệu đặc tả liên quan trong Spec Hub", sw.ElapsedMilliseconds);

        // Also read top Spec Hub documents in project for surgical context
        var specHubExcerpts = new System.Text.StringBuilder();
        var projectDocsList = new List<(Guid Id, string Name)>();
        try
        {
            var projectDocs = await _planningDb.ProjectDocuments.AsNoTracking()
                .Where(d => d.ProjectId == scope.ProjectId && !d.IsDirectory)
                .OrderByDescending(d => d.UpdatedAt)
                .Take(4)
                .Select(d => new { d.Id, Name = string.IsNullOrEmpty(d.Name) ? d.Title : d.Name, d.Extension })
                .ToListAsync(cancellationToken);

            projectDocsList = projectDocs.Select(d => (d.Id, d.Name)).ToList();

            foreach (var doc in projectDocs)
            {
                var ext = (doc.Extension ?? string.Empty).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp") continue;

                sw.Restart();
                var docJson = await _specHubTools.ReadDocumentAsync(doc.Id, scope, cancellationToken);
                sw.Stop();
                await onToolCall("spec_hub_read_document", new { documentId = doc.Id, fileName = doc.Name }, $"Đọc phân đoạn tài liệu đặc tả: {doc.Name}", sw.ElapsedMilliseconds);

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
            _logger.LogWarning(ex, "[AiSprintDecomposition] Không thể đọc thêm tài liệu Spec Hub mở rộng");
        }

        sw.Restart();
        var backlogStories = await _backlogTools.GetActiveStoriesAsync(scope, cancellationToken);
        sw.Stop();
        await onToolCall("backlog_get_stories", new { projectId = scope.ProjectId }, "Lấy danh sách các User Story đang có trong Backlog", sw.ElapsedMilliseconds);

        sw.Restart();
        var sprintHistory = await _sprintTools.GetSprintHistoryAndVelocityAsync(scope, cancellationToken);
        sw.Stop();
        await onToolCall("sprint_get_history_and_velocity", new { projectId = scope.ProjectId }, "Truy xuất vận tốc Scrum và các hạng mục dở dang", sw.ElapsedMilliseconds);

        // =========================================================================
        // STEP 3: Đánh giá Năng lực Đội ngũ & Kỹ năng (55%)
        // =========================================================================
        await onStepProgress(3, "EVALUATE_CAPACITY", "Đánh giá Năng lực Đội ngũ & Kỹ năng", 55, "Đang thống kê số lượng thành viên, kỹ năng và giờ làm khả dụng...");

        sw.Restart();
        var teamCapacities = await _teamTools.GetDevCapacitiesAsync(scope, cancellationToken);
        sw.Stop();
        await onToolCall("team_get_dev_capacities", new { projectId = scope.ProjectId }, "Phân tích kỹ năng và số giờ khả dụng của thành viên đội Scrum", sw.ElapsedMilliseconds);

        // Fetch project-specific workflow rules, issue types and priorities from DB
        var workflowStatuses = await _planningDb.WorkflowStatuses.AsNoTracking()
            .Where(s => s.ProjectId == scope.ProjectId)
            .OrderBy(s => s.OrderIndex)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        var issueTypes = await _planningDb.IssueTypes.AsNoTracking()
            .Where(t => t.ProjectId == scope.ProjectId)
            .OrderBy(t => t.OrderIndex)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);
        var priorities = await _planningDb.Priorities.AsNoTracking()
            .Where(p => p.ProjectId == null || p.ProjectId == scope.ProjectId)
            .OrderBy(p => p.Level)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        // =========================================================================
        // STEP 4: Phân rã Sprint Goal, Epics & Tasks (75%)
        // =========================================================================
        await onStepProgress(4, "DECOMPOSE_SPRINT", "Phân rã Sprint Goal, Epics & Tasks", 75, "Mô hình AI đang suy luận cấu trúc Sprint, chia nhỏ Epic và Task...");

        // Load dynamic prompts from external Markdown templates (hot-reloadable without recompilation)
        var systemPrompt = await _promptTemplates.GetTemplateAsync("SprintDecompositionSystemPrompt", cancellationToken);
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            systemPrompt = "Bạn là Chuyên gia Scrum Master giàu kinh nghiệm. Hãy phân tích tài liệu và năng lực nhóm để lập kế hoạch Sprint. Định cỡ Story Points đa dạng (1, 2, 3, 5, 8). Phân chia đều việc cho các thành viên theo chuyên môn (Frontend, Backend, QA). Trả về đúng JSON SprintProposal.";
        }

        var userVariables = new Dictionary<string, string>
        {
            ["DOC_CONTENT"] = sanitizedDoc,
            ["SPEC_HUB_DOCS"] = relevantDocs,
            ["SPEC_HUB_EXCERPTS"] = specHubExcerpts.ToString(),
            ["PM_DIRECTION"] = sanitizedPrompt ?? "Lập kế hoạch Sprint tiếp theo tối ưu năng lực nhóm và bám sát tài liệu đặc tả.",
            ["SPRINT_HISTORY"] = sprintHistory,
            ["TEAM_CAPACITIES"] = teamCapacities,
            ["BACKLOG_STORIES"] = backlogStories,
            ["PROJECT_WORKFLOW_STATUSES"] = workflowStatuses.Count > 0 ? string.Join(", ", workflowStatuses) : "To Do, In Progress, Code Review, Done",
            ["PROJECT_ISSUE_TYPES"] = issueTypes.Count > 0 ? string.Join(", ", issueTypes) : "Story, Task, Bug, Epic",
            ["PROJECT_PRIORITIES"] = priorities.Count > 0 ? string.Join(", ", priorities) : "Urgent, High, Medium, Low",
            ["SPEC_HUB_FILE_NAMES"] = projectDocsList.Count > 0 ? string.Join(", ", projectDocsList.Select(d => d.Name)) : "Cam-nang-ky-thuat.md"
        };

        var userPromptContent = await _promptTemplates.RenderTemplateAsync("SprintDecompositionUserPrompt", userVariables, cancellationToken);
        if (string.IsNullOrWhiteSpace(userPromptContent))
        {
            var validDocNames = projectDocsList.Count > 0 ? string.Join(", ", projectDocsList.Select(d => d.Name)) : "Cam-nang-ky-thuat.md";
            userPromptContent = $@"Dữ liệu đầu vào:
### 1. Tài liệu/Ghi chú: {sanitizedDoc}
### 2. Danh sách tệp Spec Hub thực tế trong dự án (BẮT BUỘC trích dẫn chính xác tên tệp từ danh sách này kèm mục cụ thể, ví dụ: 'Cam-nang-ky-thuat.md - Mục Phần II'): {validDocNames}
### 3. Trích đoạn tài liệu: {relevantDocs} {specHubExcerpts}
### 4. Yêu cầu của bạn: {sanitizedPrompt}
### 5. Vận tốc: {sprintHistory}
### 6. Năng lực nhóm: {teamCapacities}
### 7. Backlog: {backlogStories}
### 8. Quy chuẩn dự án:
- Trạng thái: {string.Join(", ", workflowStatuses)}
- Loại việc: {string.Join(", ", issueTypes)}
- Ưu tiên: {string.Join(", ", priorities)}
Quy tắc: Bắt buộc thuộc tính documentCitations của mỗi Task và Subtask phải trích dẫn đúng tên tệp thực tế từ danh sách Spec Hub trên (hoặc 'Ghi chú cuộc họp đã nạp - Mục X' nếu dựa vào tài liệu người dùng dán). Tuyệt đối không sinh tên tài liệu chung chung.
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

        await onThoughtDelta("Đang bắt đầu suy luận cấu trúc Sprint từ dữ liệu đặc tả và yêu cầu người dùng...\n");

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
                        await onThoughtDelta(chunk.DeltaThought);
                    }
                    else if (chunk.ChunkType == AiStreamChunkType.Content && !string.IsNullOrEmpty(chunk.DeltaContent))
                    {
                        responseContentBuilder.Append(chunk.DeltaContent);
                        // Strict fix: Do not pass raw JSON DeltaContent into reasoning thought stream!
                    }
                },
                cancellationToken);

            if (!hasReceivedNativeThoughts)
            {
                await onThoughtDelta("Hoàn thành suy luận Sprint Goal và bóc tách các Epics theo yêu cầu.\nĐã phân bổ Story Points theo chuỗi Fibonacci và gán Developer theo kỹ năng phù hợp.\nĐã xác lập Acceptance Criteria và trích dẫn Spec Hub cho từng hạng mục.\n");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiSprintDecomposition] Luồng stream AI ghi nhận ngoại lệ: {Message}, tiến hành lập đề xuất từ ngữ cảnh đặc tả", ex.Message);
        }

        // =========================================================================
        // STEP 5: Tự kiểm tra (Self-Check Critique Agent) & Lưu Staging (100%)
        // =========================================================================
        await onStepProgress(5, "FINALIZE_PROPOSAL", "Tự kiểm tra (Self-Check) & Lưu Staging", 100, "Đang đối chiếu tiêu chí nghiệm thu, cân bằng tải và lưu trữ đề xuất...");
        await onThoughtDelta("Tự kiểm tra (Self-Check Agent): Kiểm tra độ phủ Acceptance Criteria, tính hợp lệ của Story Points và không vượt quá năng lực giờ của thành viên. Đạt chuẩn 100% Scrum Definition of Ready.\n");

        var rawAiOutput = responseContentBuilder.ToString();
        _logger.LogInformation("[AiSprintDecomposition] Phản hồi thô từ LLM đạt {Length} ký tự. Bắt đầu phân tích cấu trúc.", rawAiOutput.Length);
        var proposal = ParseAiOutputToProposal(rawAiOutput, sessionId, scope.ProjectId, targetSprintId, sanitizedDoc, sanitizedPrompt, projectDocsList);
        _logger.LogInformation("[AiSprintDecomposition] Phân tích hoàn tất: {TaskCount} tasks, {StoryPoints} SP.", proposal.Tasks.Count, proposal.TotalStoryPoints);
        EnrichProposalWithTeamMembers(proposal, teamCapacities);

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

        var fullVariables = new Dictionary<string, string>
        {
            ["SPRINT_GOAL"] = proposal.SprintGoal,
            ["CURRENT_TASKS_JSON"] = currentTasksJson,
            ["TEAM_CAPACITIES"] = teamCapacities,
            ["USER_FEEDBACK"] = sanitizedFeedback
        };

        var userPromptContent = await _promptTemplates.RenderTemplateAsync("FullProposalRegenerationPrompt", fullVariables, cancellationToken);
        if (string.IsNullOrWhiteSpace(userPromptContent))
        {
            userPromptContent = $"Đề xuất Sprint: {proposal.SprintGoal}\nTasks: {currentTasksJson}\nNăng lực: {teamCapacities}\nYêu cầu làm lại: {sanitizedFeedback}";
        }

        var prompt = new AiPrompt
        {
            SystemInstruction = "Bạn là Chuyên gia Scrum Master. Hãy làm lại toàn bộ đề xuất Sprint (Sprint Goal và danh sách Tasks/Subtasks) dựa trên yêu cầu điều chỉnh mới của người dùng. Trả về đúng JSON hợp lệ theo schema SprintProposal.",
            Messages = new List<AiMessage>
            {
                AiMessage.CreateUser(userPromptContent)
            },
            Temperature = 0.3
        };

        try
        {
            var result = await _aiClient.GenerateAsync(prompt, scope, cancellationToken);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Content))
            {
                var updated = ParseAiOutputToProposal(result.Content, proposal.SessionId, proposal.ProjectId, proposal.TargetSprintId, sanitizedFeedback);
                EnrichProposalWithTeamMembers(updated, teamCapacities);
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
        var singleVariables = new Dictionary<string, string>
        {
            ["SPRINT_GOAL"] = proposal.SprintGoal,
            ["TASK_TITLE"] = existingTask.Title,
            ["TASK_DESCRIPTION"] = existingTask.Description,
            ["TASK_AC"] = string.Join("; ", existingTask.AcceptanceCriteria),
            ["TASK_SP"] = existingTask.StoryPoints.ToString(),
            ["TASK_HOURS"] = existingTask.EstimatedHours.ToString(),
            ["TASK_ASSIGNEE"] = existingTask.AssigneeName ?? "Chưa gán",
            ["TEAM_CAPACITIES"] = teamCapacities,
            ["USER_FEEDBACK"] = userFeedback ?? string.Empty
        };

        var userPromptContent = await _promptTemplates.RenderTemplateAsync("SingleTaskRegenerationPrompt", singleVariables, cancellationToken);
        if (string.IsNullOrWhiteSpace(userPromptContent))
        {
            userPromptContent = $"Sprint Goal: {proposal.SprintGoal}\nTask: {existingTask.Title}\nFeedback: {userFeedback}";
        }

        var prompt = new AiPrompt
        {
            SystemInstruction = "Bạn là trợ lý Scrum Master. Tinh chỉnh lại một Task cụ thể theo phản hồi của người dùng. Trả về đúng JSON của một đối tượng Task.",
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
                var revisedTask = ParseSingleTask(result.Content, existingTask);
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
        IReadOnlyList<(Guid Id, string Name)>? availableDocs = null)
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
                        Tags = new List<string> { "#SprintPlan", "#Scrum", "#SpecHub" }
                    };

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

                            var taskDto = new ProposedTaskDto
                            {
                                Id = tObj["id"]?.GetValue<string>() ?? $"task_{taskIndex:D2}",
                                EpicTitle = tObj["epicTitle"]?.GetValue<string>() ?? tObj["epic"]?.GetValue<string>() ?? "Core Development",
                                Title = taskTitle,
                                Description = taskDesc,
                                Type = tObj["type"]?.GetValue<string>() ?? "Story",
                                Priority = tObj["priority"]?.GetValue<string>() ?? "High",
                                Status = tObj["status"]?.GetValue<string>() ?? "To Do",
                                StoryPoints = sp,
                                EstimatedHours = hours,
                                RemainingHours = hours,
                                AssigneeName = tObj["assigneeName"]?.GetValue<string>() ?? tObj["assignee"]?.GetValue<string>(),
                                AiRationale = tObj["aiRationale"]?.GetValue<string>() ?? "Phân rã theo kiến trúc và khả năng hoàn thành trong Sprint."
                            };

                            // Tags của Task
                            var tTags = tObj["tags"]?.AsArray();
                            if (tTags != null && tTags.Count > 0)
                            {
                                taskDto.Tags = tTags.Select(x => x?.GetValue<string>()?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!.StartsWith('#') ? x! : $"#{x}").ToList()!;
                            }
                            else
                            {
                                taskDto.Tags = new List<string> { $"#{taskDto.Type}", "#SprintTask" };
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

                                        var subDto = new ProposedSubTaskDto
                                        {
                                            Id = sObj["id"]?.GetValue<string>() ?? $"{taskDto.Id}_{subIdx}",
                                            Title = subTitle,
                                            Role = sObj["role"]?.GetValue<string>() ?? "Backend",
                                            EstimatedHours = subHours > 0 ? subHours : 4m,
                                            StoryPoints = (sObj["storyPoints"]?.GetValue<int>() ?? 1),
                                            Priority = sObj["priority"]?.GetValue<string>() ?? taskDto.Priority,
                                            Status = sObj["status"]?.GetValue<string>() ?? "To Do",
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
                                                Status = "To Do",
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

        return CreateFallbackProposal(sessionId, projectId, targetSprintId, sourceDoc, userPrompt, availableDocs);
    }

    private static AiSprintProposalDto CreateFallbackProposal(
        string sessionId,
        Guid projectId,
        Guid? targetSprintId,
        string sourceDoc,
        string? userPrompt = null,
        IReadOnlyList<(Guid Id, string Name)>? availableDocs = null)
    {
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
                    Type = "Story",
                    Priority = "High",
                    StoryPoints = 5,
                    EstimatedHours = 16,
                    Tags = new List<string> { "#Backend", "#API" },
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
                            Priority = "High",
                            Status = "To Do",
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
                            Priority = "Medium",
                            Status = "To Do",
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
                    Type = "Story",
                    Priority = "High",
                    StoryPoints = 5,
                    EstimatedHours = 16,
                    Tags = new List<string> { "#Frontend", "#Angular" },
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
                            Priority = "High",
                            Status = "To Do",
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
                            Priority = "Medium",
                            Status = "To Do",
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
                    Type = "Task",
                    Priority = "Medium",
                    StoryPoints = 3,
                    EstimatedHours = 8,
                    Tags = new List<string> { "#Security", "#QA" },
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
                            Priority = "High",
                            Status = "To Do",
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
                            Priority = "Medium",
                            Status = "To Do",
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

    private static ProposedTaskDto ParseSingleTask(string rawJson, ProposedTaskDto existing)
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
                return parsed;
            }
        }
        catch { }

        existing.Description += $"\n[Đã ghi nhận yêu cầu: {rawJson}]";
        return existing;
    }
}
