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
        try
        {
            var projectDocs = await _planningDb.ProjectDocuments.AsNoTracking()
                .Where(d => d.ProjectId == scope.ProjectId && !d.IsDirectory)
                .OrderByDescending(d => d.UpdatedAt)
                .Take(4)
                .Select(d => new { d.Id, Name = string.IsNullOrEmpty(d.Name) ? d.Title : d.Name, d.Extension })
                .ToListAsync(cancellationToken);

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

        // =========================================================================
        // STEP 4: Phân rã Sprint Goal, Epics & Tasks (75%)
        // =========================================================================
        await onStepProgress(4, "DECOMPOSE_SPRINT", "Phân rã Sprint Goal, Epics & Tasks", 75, "Mô hình AI đang suy luận cấu trúc Sprint, chia nhỏ Epic và Task...");

        var systemPrompt = @"Bạn là Chuyên gia Scrum Master và Kiến trúc sư phần mềm cấp cao.
Nhiệm vụ của bạn là phân tích tài liệu đặc tả (SRS/BRD), ghi chú cuộc họp, ngữ cảnh Backlog và năng lực nhóm để lập kế hoạch Sprint hoàn chỉnh bằng tiếng Việt chuẩn doanh nghiệp.
Quy tắc bắt buộc:
1. Xác định 1 Sprint Goal rõ ràng, tập trung và đo lường được kết quả.
2. Phân rã thành 3 đến 6 Task/Story cụ thể. Mỗi Task phải có:
   - EpicTitle
   - Title súc tích, chuyên nghiệp
   - Description chi tiết
   - Type (Story, Task, Bug)
   - Priority (Highest, High, Medium, Low)
   - StoryPoints (1, 2, 3, 5, 8)
   - EstimatedHours
   - AssigneeName (chọn từ danh sách thành viên đội ngũ phù hợp kỹ năng)
   - AcceptanceCriteria (ít nhất 3 tiêu chí nghiệm thu rõ ràng)
   - DocumentCitations (trích dẫn tên tài liệu hoặc mục đặc tả liên quan)
   - AiRationale (lập luận kỹ thuật và cân bằng tải tại sao gán task/SP này)
   - Subtasks kỹ thuật (Backend, Frontend, QA, DevOps) kèm số giờ ước lượng.
3. Đầu ra BẮT BUỘC là định dạng JSON hợp lệ (không bọc text thừa ngoài JSON) tuân thủ schema:
{
  ""sprintGoal"": ""string"",
  ""rationale"": ""string"",
  ""tasks"": [
    {
      ""id"": ""task_01"",
      ""epicTitle"": ""string"",
      ""title"": ""string"",
      ""description"": ""string"",
      ""type"": ""Story"",
      ""priority"": ""High"",
      ""storyPoints"": 5,
      ""estimatedHours"": 16,
      ""assigneeName"": ""string"",
      ""acceptanceCriteria"": [""tiêu chí 1"", ""tiêu chí 2"", ""tiêu chí 3""],
      ""documentCitations"": [""trích dẫn 1""],
      ""aiRationale"": ""lập luận"",
      ""subtasks"": [
        { ""id"": ""sub_01_1"", ""title"": ""string"", ""role"": ""Backend"", ""estimatedHours"": 8 }
      ]
    }
  ]
}";

        var userPromptContent = $@"Dữ liệu đầu vào:
### 1. Tài liệu / Ghi chú cuộc họp:
{sanitizedDoc}

### 2. Tài liệu Đặc tả trong Spec Hub của Dự án:
{relevantDocs}
{specHubExcerpts}

### 3. Yêu cầu định hướng từ PM:
{sanitizedPrompt ?? "Lập kế hoạch Sprint tiếp theo tối ưu năng lực nhóm và bám sát tài liệu đặc tả."}

### 4. Vận tốc và Lịch sử Sprint trước:
{sprintHistory}

### 5. Năng lực và kỹ năng thành viên nhóm:
{teamCapacities}

### 6. Backlog hiện tại:
{backlogStories}

Hãy phân tích và trả về đúng cấu trúc JSON SprintProposal.";

        var prompt = new AiPrompt
        {
            SystemInstruction = systemPrompt,
            Messages = new List<AiMessage> { AiMessage.CreateUser(userPromptContent) },
            Temperature = 0.2,
            MaxTokens = 8192
        };

        var responseContentBuilder = new System.Text.StringBuilder();

        try
        {
            await _aiClient.StreamGenerateAsync(
                prompt,
                scope,
                async chunk =>
                {
                    if (chunk.ChunkType == AiStreamChunkType.Thought && !string.IsNullOrEmpty(chunk.DeltaThought))
                    {
                        await onThoughtDelta(chunk.DeltaThought);
                    }
                    else if (chunk.ChunkType == AiStreamChunkType.Content && !string.IsNullOrEmpty(chunk.DeltaContent))
                    {
                        responseContentBuilder.Append(chunk.DeltaContent);
                        await onThoughtDelta(chunk.DeltaContent);
                    }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiSprintDecomposition] Lỗi gọi model AI, sử dụng fallback proposal");
        }

        // =========================================================================
        // STEP 5: Đề xuất Tiêu chí Nghiệm thu & Hoàn tất (100%)
        // =========================================================================
        await onStepProgress(5, "FINALIZE_PROPOSAL", "Đề xuất Tiêu chí Nghiệm thu & Hoàn tất", 100, "Đang chuẩn hóa tiêu chí nghiệm thu và lưu trữ đề xuất...");

        var rawAiOutput = responseContentBuilder.ToString();
        var proposal = ParseAiOutputToProposal(rawAiOutput, sessionId, scope.ProjectId, targetSprintId, sanitizedDoc);
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

        var prompt = new AiPrompt
        {
            SystemInstruction = "Bạn là Chuyên gia Scrum Master. Hãy làm lại toàn bộ đề xuất Sprint (Sprint Goal và danh sách Tasks/Subtasks) dựa trên yêu cầu điều chỉnh mới của người dùng. Trả về đúng JSON hợp lệ theo schema SprintProposal.",
            Messages = new List<AiMessage>
            {
                AiMessage.CreateUser($@"Đề xuất Sprint hiện tại:
- Sprint Goal: {proposal.SprintGoal}
- Danh sách Tasks hiện tại: {currentTasksJson}
- Năng lực đội ngũ: {teamCapacities}

Yêu cầu làm lại / điều chỉnh từ người dùng:
""{sanitizedFeedback}""

Hãy tái cấu trúc Sprint Goal và danh sách Tasks/Subtasks theo đúng yêu cầu. Trả về JSON.")
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

        var prompt = new AiPrompt
        {
            SystemInstruction = "Bạn là trợ lý Scrum Master. Tinh chỉnh lại một Task cụ thể theo phản hồi của người dùng. Trả về đúng JSON của một đối tượng Task.",
            Messages = new List<AiMessage>
            {
                AiMessage.CreateUser($@"Sprint Goal hiện tại: {proposal.SprintGoal}
Task hiện tại:
- Title: {existingTask.Title}
- Description: {existingTask.Description}
- Acceptance Criteria: {string.Join("; ", existingTask.AcceptanceCriteria)}
- StoryPoints: {existingTask.StoryPoints}
- EstimatedHours: {existingTask.EstimatedHours}

Phản hồi điều chỉnh từ người dùng:
""{userFeedback}""

Hãy cập nhật Task này (Title, Description, AcceptanceCriteria, StoryPoints, EstimatedHours, Subtasks). Trả về đúng JSON đối tượng Task.")
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
        string sourceDoc)
    {
        try
        {
            var cleaned = ExtractJsonObject(rawJson);

            var parsed = JsonSerializer.Deserialize<AiSprintProposalDto>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed != null && parsed.Tasks.Count > 0)
            {
                parsed.SessionId = sessionId;
                parsed.ProjectId = projectId;
                parsed.TargetSprintId = targetSprintId;
                for (var i = 0; i < parsed.Tasks.Count; i++)
                {
                    var t = parsed.Tasks[i];
                    if (string.IsNullOrWhiteSpace(t.Id)) t.Id = $"task_{i + 1:D2}";
                    if (t.RemainingHours <= 0) t.RemainingHours = t.EstimatedHours;
                    if (string.IsNullOrWhiteSpace(t.Status)) t.Status = "To Do";
                }
                parsed.TotalStoryPoints = parsed.Tasks.Sum(t => t.StoryPoints);
                parsed.TotalEstimatedHours = parsed.Tasks.Sum(t => t.EstimatedHours);
                return parsed;
            }
        }
        catch
        {
            // Fallback deterministic proposal parser
        }

        return CreateFallbackProposal(sessionId, projectId, targetSprintId, sourceDoc);
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

    private static void EnrichProposalWithTeamMembers(AiSprintProposalDto proposal, string teamCapacitiesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(teamCapacitiesJson);
            if (!doc.RootElement.TryGetProperty("Capacities", out var capArray) || capArray.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var members = new List<(Guid UserId, string DisplayName)>();
            foreach (var item in capArray.EnumerateArray())
            {
                if (item.TryGetProperty("UserId", out var idProp) &&
                    Guid.TryParse(idProp.GetString(), out var userId) &&
                    item.TryGetProperty("DisplayName", out var nameProp))
                {
                    var name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        members.Add((userId, name));
                    }
                }
            }

            if (members.Count == 0) return;

            for (var i = 0; i < proposal.Tasks.Count; i++)
            {
                var task = proposal.Tasks[i];
                if (!string.IsNullOrWhiteSpace(task.AssigneeName))
                {
                    var matched = members.FirstOrDefault(m =>
                        m.DisplayName.Contains(task.AssigneeName, StringComparison.OrdinalIgnoreCase) ||
                        task.AssigneeName.Contains(m.DisplayName, StringComparison.OrdinalIgnoreCase));
                    if (matched.UserId != Guid.Empty)
                    {
                        task.AssigneeUserId = matched.UserId;
                        task.AssigneeName = matched.DisplayName;
                    }
                }

                if (!task.AssigneeUserId.HasValue || string.IsNullOrWhiteSpace(task.AssigneeName))
                {
                    var assigned = members[i % members.Count];
                    task.AssigneeUserId = assigned.UserId;
                    task.AssigneeName = assigned.DisplayName;
                }

                if (task.RemainingHours <= 0)
                {
                    task.RemainingHours = task.EstimatedHours;
                }

                if (task.Subtasks != null)
                {
                    for (var j = 0; j < task.Subtasks.Count; j++)
                    {
                        var sub = task.Subtasks[j];
                        if (!sub.AssigneeUserId.HasValue || string.IsNullOrWhiteSpace(sub.AssigneeName))
                        {
                            var subMember = members[(i + j) % members.Count];
                            sub.AssigneeUserId = subMember.UserId;
                            sub.AssigneeName = subMember.DisplayName;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore enrichment parse errors
        }
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

    private static AiSprintProposalDto CreateFallbackProposal(
        string sessionId,
        Guid projectId,
        Guid? targetSprintId,
        string sourceDoc)
    {
        return new AiSprintProposalDto
        {
            SessionId = sessionId,
            ProjectId = projectId,
            TargetSprintId = targetSprintId,
            SprintGoal = "Hoàn thiện phân hệ chức năng theo tài liệu đặc tả và kiểm thử tự động",
            Rationale = "Đảm bảo đáp ứng đầy đủ các tiêu chí nghiệm thu đã thống nhất trong tài liệu đặc tả dự án.",
            TotalStoryPoints = 13,
            TotalEstimatedHours = 40,
            Tasks = new List<ProposedTaskDto>
            {
                new()
                {
                    Id = "task_01",
                    EpicTitle = "Core Feature Delivery",
                    Title = "Thiết kế và cài đặt luồng xử lý nghiệp vụ chính",
                    Description = "Xây dựng các API và Controller tương ứng theo đặc tả nghiệp vụ đã phân tích.",
                    Type = "Story",
                    Priority = "High",
                    StoryPoints = 5,
                    EstimatedHours = 16,
                    AcceptanceCriteria = new List<string>
                    {
                        "API phản hồi đúng định dạng JSON",
                        "Kiểm tra xác thực JWT và phân quyền Scrum",
                        "Xử lý lỗi ngoại lệ toàn diện"
                    },
                    DocumentCitations = new List<string> { "Đặc tả yêu cầu phần mềm" },
                    AiRationale = "Cần thiết lập trước để các task giao diện frontend tích hợp.",
                    Subtasks = new List<ProposedSubTaskDto>
                    {
                        new() { Id = "sub_01_1", Title = "Backend API & DbContext", Role = "Backend", EstimatedHours = 8 },
                        new() { Id = "sub_01_2", Title = "Unit Tests & Integration", Role = "QA", EstimatedHours = 8 }
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
                    AcceptanceCriteria = new List<string>
                    {
                        "Giao diện responsive không bị tràn lề",
                        "100% icon sử dụng Google Material Symbols Outlined",
                        "Không sử dụng raw keyboard emojis"
                    },
                    DocumentCitations = new List<string> { "Quy chuẩn UI/UX dự án" },
                    AiRationale = "Mang lại trải nghiệm người dùng chuẩn doanh nghiệp.",
                    Subtasks = new List<ProposedSubTaskDto>
                    {
                        new() { Id = "sub_02_1", Title = "Angular Component & Signal state", Role = "Frontend", EstimatedHours = 10 },
                        new() { Id = "sub_02_2", Title = "E2E Playwright validation", Role = "QA", EstimatedHours = 6 }
                    }
                },
                new()
                {
                    Id = "task_03",
                    EpicTitle = "Quality Assurance & Security",
                    Title = "Kiểm thử bảo mật và rà soát Secret Zero-Leakage",
                    Description = "Audit bảo mật trước khi bàn giao sprint.",
                    Type = "Task",
                    Priority = "Medium",
                    StoryPoints = 3,
                    EstimatedHours = 8,
                    AcceptanceCriteria = new List<string>
                    {
                        "Không có secret/key/URL/IP hardcode trong repo",
                        "Kiểm thử bảo mật vượt qua 100%"
                    },
                    DocumentCitations = new List<string> { "GEMINI.md" },
                    AiRationale = "Đảm bảo tuân thủ tiêu chuẩn an toàn thông tin tối cao của tổ chức.",
                    Subtasks = new List<ProposedSubTaskDto>
                    {
                        new() { Id = "sub_03_1", Title = "Security static scan", Role = "DevOps", EstimatedHours = 4 },
                        new() { Id = "sub_03_2", Title = "Regression tests", Role = "QA", EstimatedHours = 4 }
                    }
                }
            }
        };
    }
}
