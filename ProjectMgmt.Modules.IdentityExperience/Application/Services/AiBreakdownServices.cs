using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace IdentityExperience.Application.Services;

public class AiBreakdownServices : IAiBreakdownServices
{
    private static readonly Regex PromptCodePattern = new(
        "^[a-z][a-z0-9.-]{2,79}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> PromptTaskTypes = ["Breakdown", "Assignment"];
    private readonly IAiBreakdownRepository _repository;
    private readonly IAiBreakdownInference _inference;
    private readonly IAiIssueSourceReader _issueSourceReader;
    private readonly IAiBreakdownApplyCoordinator _applyCoordinator;
    private readonly IPermissionEvaluator _permissions;
    private readonly IProjectLookupService _projects;
    private readonly INotificationServices _notifications;
    private readonly TimeProvider _timeProvider;

    public AiBreakdownServices(
        IAiBreakdownRepository repository,
        IAiBreakdownInference inference,
        IAiIssueSourceReader issueSourceReader,
        IAiBreakdownApplyCoordinator applyCoordinator,
        IPermissionEvaluator permissions,
        IProjectLookupService projects,
        INotificationServices notifications,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _inference = inference;
        _issueSourceReader = issueSourceReader;
        _applyCoordinator = applyCoordinator;
        _permissions = permissions;
        _projects = projects;
        _notifications = notifications;
        _timeProvider = timeProvider;
    }

    public async Task<AiBreakdownDto> GenerateAsync(Guid actorUserId, AiBreakdownDto request)
    {
        if (actorUserId == Guid.Empty || !request.IssueId.HasValue || request.IssueId == Guid.Empty)
        {
            return Failure("AI_BREAKDOWN_ISSUE_REQUIRED", "Issue gốc là bắt buộc.");
        }

        if (request.ProjectContext?.Length > 4000)
        {
            return Failure("AI_BREAKDOWN_CONTEXT_TOO_LONG", "Bối cảnh dự án không được vượt quá 4000 ký tự.");
        }

        var source = await _issueSourceReader.GetIssueSourceAsync(request.IssueId.Value);
        if (source is null || source.IsDeleted)
        {
            return Failure("AI_BREAKDOWN_ISSUE_NOT_FOUND", "Không tìm thấy Issue gốc.");
        }

        if (!await HasProjectPermissionAsync(actorUserId, source.ProjectId, "ai.breakdown.request"))
        {
            return Failure("AI_BREAKDOWN_FORBIDDEN", "Bạn không có quyền yêu cầu AI phân rã Issue này.");
        }

        var model = await _repository.GetActiveModelAsync("Breakdown");
        var prompt = await _repository.GetActivePromptAsync("Breakdown");
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var generationId = Guid.NewGuid();
        var input = new AiBreakdownDto
        {
            IssueId = source.IssueId,
            ProjectId = source.ProjectId,
            StoryTitle = source.Title,
            StoryDescription = source.Description,
            ProjectContext = OptionalText(request.ProjectContext)
        };
        var generation = new AiGenerationLog
        {
            Id = generationId,
            IssueId = source.IssueId,
            ProjectId = source.ProjectId,
            UserId = actorUserId,
            ModelId = model?.Id,
            PromptTemplateId = prompt?.Id,
            InputText = JsonSerializer.Serialize(input),
            RenderedPrompt = RenderPrompt(prompt, input),
            Status = "Processing",
            RetryCount = 0,
            CreatedAt = now
        };

        if (!await _repository.AddGenerationAsync(generation))
        {
            return Failure("AI_BREAKDOWN_LOG_FAILED", "Không thể khởi tạo lịch sử yêu cầu AI.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var inferredTasks = await _inference.GenerateAsync(input);
            stopwatch.Stop();
            var suggestions = inferredTasks.Select((task, index) => new AiSuggestedTask
            {
                Id = Guid.NewGuid(),
                AiGenerationLogId = generationId,
                OrderIndex = index,
                OriginalSummary = Truncate(
                    task.Title?.Trim() ?? $"Sub-task {index + 1}",
                    500),
                OriginalDescription = OptionalText(task.Description),
                OriginalAcceptanceCriteria = SerializeList(task.AcceptanceCriteria),
                OriginalEstimatePoints = task.EstimatePoints,
                OriginalSuggestedSkills = SerializeList(task.SuggestedSkills),
                UserAction = "Pending",
                CreatedAt = now
            }).ToList();
            var responseTasks = suggestions.Select(MapSuggestion).ToList();
            var parsedJson = JsonSerializer.Serialize(responseTasks);
            var completed = await _repository.CompleteGenerationAsync(
                generationId,
                parsedJson,
                parsedJson,
                suggestions,
                (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
                _timeProvider.GetUtcNow().UtcDateTime);
            if (!completed)
            {
                await _repository.MarkGenerationFailedAsync(
                    generationId,
                    "PersistenceFailed",
                    "Không thể lưu danh sách gợi ý đã sinh.",
                    _timeProvider.GetUtcNow().UtcDateTime);
                return Failure("AI_BREAKDOWN_SAVE_FAILED", "Không thể lưu kết quả phân rã giả lập.");
            }

            await _notifications.PublishAsync(new NotificationDto
            {
                UserId = actorUserId,
                Type = NotificationTypes.AiBreakdownCompleted,
                Title = "AI đã hoàn tất phân rã công việc",
                Content = $"Đã tạo {responseTasks.Count} gợi ý sub-task cho Issue {source.Title}.",
                EntityType = "AiGenerationLog",
                EntityId = generationId,
                ProjectId = source.ProjectId,
                SendEmail = false
            });

            return new AiBreakdownDto
            {
                Success = true,
                Message = "Đã tạo gợi ý phân rã giả lập và lưu lịch sử thật.",
                GenerationId = generationId,
                IssueId = source.IssueId,
                ProjectId = source.ProjectId,
                Status = "Completed",
                ModelVersion = model?.Code ?? "fake:deterministic-breakdown-v1",
                PromptVersion = prompt is null ? "fake-v1" : $"{prompt.Code}.v{prompt.Version}",
                SuggestedSubTasks = responseTasks
            };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            await _repository.MarkGenerationFailedAsync(
                generationId,
                "FakeInferenceFailed",
                exception.Message,
                _timeProvider.GetUtcNow().UtcDateTime);
            return Failure("AI_BREAKDOWN_INFERENCE_FAILED", "Bộ giả lập AI không thể tạo kết quả.");
        }
    }

    public async Task<AiBreakdownDto> GetAsync(Guid actorUserId, Guid generationId)
    {
        var details = await _repository.GetGenerationAsync(generationId);
        if (details is null)
        {
            return Failure("AI_BREAKDOWN_NOT_FOUND", "Không tìm thấy lần phân rã AI.");
        }

        if (details.Generation.UserId != actorUserId
            && !await HasProjectPermissionAsync(
                actorUserId,
                details.Generation.ProjectId,
                "ai.breakdown.request"))
        {
            return Failure("AI_BREAKDOWN_FORBIDDEN", "Bạn không có quyền xem lần phân rã AI này.");
        }

        return MapGeneration(details);
    }

    public async Task<AiBreakdownDto> SaveFeedbackAsync(
        Guid actorUserId,
        Guid generationId,
        AiBreakdownDto request)
    {
        var details = await _repository.GetGenerationAsync(generationId);
        if (details is null)
        {
            return Failure("AI_BREAKDOWN_NOT_FOUND", "Không tìm thấy lần phân rã AI.");
        }

        if (details.Generation.UserId != actorUserId
            && !await HasProjectPermissionAsync(
                actorUserId,
                details.Generation.ProjectId,
                "ai.breakdown.request"))
        {
            return Failure("AI_BREAKDOWN_FORBIDDEN", "Bạn không có quyền phản hồi gợi ý này.");
        }

        if (!request.SuggestedTaskId.HasValue)
        {
            return Failure("AI_FEEDBACK_TASK_REQUIRED", "Gợi ý cần phản hồi là bắt buộc.");
        }

        var suggestion = details.Suggestions.FirstOrDefault(item => item.Id == request.SuggestedTaskId.Value);
        if (suggestion is null)
        {
            return Failure("AI_SUGGESTION_NOT_FOUND", "Gợi ý không thuộc lần phân rã này.");
        }

        var action = request.UserAction?.Trim();
        if (action is not ("Kept" or "Edited" or "Rejected"))
        {
            return Failure("AI_FEEDBACK_ACTION_INVALID", "Hành động phải là Kept, Edited hoặc Rejected.");
        }

        var finalSummary = action == "Edited"
            ? OptionalText(request.FinalSummary ?? request.Title)
            : action == "Kept" ? suggestion.OriginalSummary : null;
        if (action == "Edited" && (finalSummary is null || finalSummary.Length > 500))
        {
            return Failure("AI_FEEDBACK_SUMMARY_INVALID", "Tiêu đề sau chỉnh sửa phải có từ 1 đến 500 ký tự.");
        }

        var finalDescription = action switch
        {
            "Edited" => OptionalText(request.FinalDescription ?? request.Description),
            "Kept" => suggestion.OriginalDescription,
            _ => null
        };
        if (finalDescription?.Length > 20_000)
        {
            return Failure("AI_FEEDBACK_DESCRIPTION_TOO_LONG", "Mô tả không được vượt quá 20000 ký tự.");
        }

        var rejectReason = action == "Rejected" ? OptionalText(request.RejectReason) : null;
        if (action == "Rejected" && (rejectReason is null || rejectReason.Length > 500))
        {
            return Failure("AI_FEEDBACK_REJECT_REASON_REQUIRED", "Lý do từ chối phải có từ 1 đến 500 ký tự.");
        }

        var finalCriteria = action switch
        {
            "Edited" => SerializeList(request.FinalAcceptanceCriteria ?? request.AcceptanceCriteria),
            "Kept" => suggestion.OriginalAcceptanceCriteria,
            _ => null
        };
        var finalPoints = action switch
        {
            "Edited" => request.FinalEstimatePoints ?? request.EstimatePoints,
            "Kept" => suggestion.OriginalEstimatePoints,
            _ => null
        };
        if (finalPoints is < 0 or > 9999)
        {
            return Failure("AI_FEEDBACK_ESTIMATE_INVALID", "Ước lượng phải nằm trong khoảng 0 đến 9999.");
        }

        var ratio = action switch
        {
            "Kept" => 0m,
            "Rejected" => 1m,
            _ => CalculateEditDistanceRatio(
                TruncateForDistance($"{suggestion.OriginalSummary}\n{suggestion.OriginalDescription}"),
                TruncateForDistance($"{finalSummary}\n{finalDescription}"))
        };
        var status = await _repository.SaveFeedbackAsync(
            generationId,
            suggestion.Id,
            actorUserId,
            action,
            finalSummary,
            finalDescription,
            finalCriteria,
            finalPoints,
            ratio,
            rejectReason,
            _timeProvider.GetUtcNow().UtcDateTime);

        return status switch
        {
            AiFeedbackWriteStatus.Updated => new AiBreakdownDto
            {
                Success = true,
                Message = "Đã lưu phản hồi để phục vụ đánh giá và cải thiện AI.",
                GenerationId = generationId,
                SuggestedTaskId = suggestion.Id,
                UserAction = action,
                EditDistanceRatio = ratio,
                FeedbackLogged = true
            },
            AiFeedbackWriteStatus.GenerationAlreadyApplied =>
                Failure("AI_BREAKDOWN_ALREADY_APPLIED", "Không thể sửa phản hồi sau khi đã áp dụng."),
            _ => Failure("AI_SUGGESTION_NOT_FOUND", "Không tìm thấy gợi ý cần phản hồi.")
        };
    }

    public async Task<AiBreakdownDto> ApplyAsync(
        Guid actorUserId,
        Guid generationId,
        AiBreakdownDto request)
    {
        var details = await _repository.GetGenerationAsync(generationId);
        if (details is null)
        {
            return Failure("AI_BREAKDOWN_NOT_FOUND", "Không tìm thấy lần phân rã AI.");
        }

        if (!await HasProjectPermissionAsync(
                actorUserId,
                details.Generation.ProjectId,
                "ai.breakdown.apply"))
        {
            return Failure("AI_BREAKDOWN_APPLY_FORBIDDEN", "Bạn không có quyền áp dụng gợi ý AI.");
        }

        if (request.ParentIssueId.HasValue
            && request.ParentIssueId.Value != details.Generation.IssueId)
        {
            return Failure("AI_BREAKDOWN_PARENT_MISMATCH", "Issue cha không khớp với yêu cầu AI.");
        }

        return await _applyCoordinator.ApplyAsync(
            generationId,
            actorUserId,
            request.SelectedSubTasks);
    }

    public async Task<AiPromptTemplateDto> GetPromptTemplatesAsync(string? taskType)
    {
        var normalizedTaskType = OptionalText(taskType);
        if (normalizedTaskType is not null && !PromptTaskTypes.Contains(normalizedTaskType))
        {
            return PromptFailure("AI_PROMPT_TASK_TYPE_INVALID", "TaskType không hợp lệ.");
        }

        var prompts = await _repository.GetPromptTemplatesAsync(normalizedTaskType);
        return new AiPromptTemplateDto
        {
            Success = true,
            Items = prompts.Select(MapPrompt).ToList()
        };
    }

    public async Task<AiPromptTemplateDto> CreatePromptTemplateAsync(
        Guid actorUserId,
        AiPromptTemplateDto request)
    {
        if (!await HasPromptManagementPermissionAsync(actorUserId))
        {
            return PromptFailure("AI_PROMPT_FORBIDDEN", "Bạn không có quyền quản lý prompt.");
        }

        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        var taskType = request.TaskType?.Trim() ?? string.Empty;
        var language = request.Language?.Trim().ToLowerInvariant() ?? "vi";
        var systemPrompt = request.SystemPrompt?.Trim() ?? string.Empty;
        if (!PromptCodePattern.IsMatch(code)
            || !PromptTaskTypes.Contains(taskType)
            || language.Length is < 2 or > 10
            || systemPrompt.Length is < 10 or > 50_000
            || request.UserTemplate?.Length > 50_000
            || !IsValidJson(request.JsonSchema))
        {
            return PromptFailure("AI_PROMPT_INVALID", "Mẫu prompt hoặc JSON Schema không hợp lệ.");
        }

        var result = await _repository.CreatePromptTemplateAsync(new AiPromptTemplate
        {
            Id = Guid.NewGuid(),
            Code = code,
            TaskType = taskType,
            Language = language,
            SystemPrompt = systemPrompt,
            UserTemplate = OptionalText(request.UserTemplate),
            JsonSchema = request.JsonSchema,
            IsActive = false,
            CreatedBy = actorUserId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        return result.Status == AiPromptWriteStatus.Created
            ? SuccessPrompt(result.PromptTemplate!, "Đã tạo phiên bản prompt mới.")
            : PromptFailure("AI_PROMPT_VERSION_CONFLICT", "Phiên bản prompt vừa được tạo bởi yêu cầu khác.");
    }

    public async Task<AiPromptTemplateDto> ActivatePromptTemplateAsync(
        Guid actorUserId,
        Guid promptId)
    {
        if (!await HasPromptManagementPermissionAsync(actorUserId))
        {
            return PromptFailure("AI_PROMPT_FORBIDDEN", "Bạn không có quyền quản lý prompt.");
        }

        var result = await _repository.ActivatePromptTemplateAsync(promptId);
        return result.Status == AiPromptWriteStatus.Activated
            ? SuccessPrompt(result.PromptTemplate!, "Đã kích hoạt prompt.")
            : PromptFailure("AI_PROMPT_NOT_FOUND", "Không tìm thấy prompt.");
    }

    private async Task<bool> HasProjectPermissionAsync(
        Guid userId,
        Guid projectId,
        string permissionCode)
    {
        var project = await _projects.GetProjectScopeAsync(projectId);
        return project is not null
            && !project.IsArchived
            && await _permissions.HasPermissionAsync(
                userId,
                permissionCode,
                "Project",
                projectId,
                project.OrganizationId);
    }

    private Task<bool> HasPromptManagementPermissionAsync(Guid userId)
    {
        return _permissions.HasPermissionAsync(
            userId,
            "ai.prompt.manage",
            "System",
            null);
    }

    private static AiBreakdownDto MapGeneration(AiGenerationDetails details)
    {
        return new AiBreakdownDto
        {
            Success = true,
            GenerationId = details.Generation.Id,
            IssueId = details.Generation.IssueId,
            ParentIssueId = details.Generation.IssueId,
            ProjectId = details.Generation.ProjectId,
            Status = details.Generation.Status,
            ModelVersion = details.Model?.Code ?? "fake:deterministic-breakdown-v1",
            PromptVersion = details.PromptTemplate is null
                ? "fake-v1"
                : $"{details.PromptTemplate.Code}.v{details.PromptTemplate.Version}",
            Applied = details.Generation.AppliedAt.HasValue,
            AppliedCount = details.Generation.AppliedCount,
            SuggestedSubTasks = details.Suggestions.Select(MapSuggestion).ToList()
        };
    }

    private static AiBreakdownDto MapSuggestion(AiSuggestedTask suggestion)
    {
        return new AiBreakdownDto
        {
            SuggestedTaskId = suggestion.Id,
            TempId = $"temp_{suggestion.OrderIndex + 1}",
            Title = suggestion.OriginalSummary,
            Description = suggestion.OriginalDescription,
            EstimatePoints = suggestion.OriginalEstimatePoints,
            EstimatedHours = suggestion.OriginalEstimatePoints * 2,
            AcceptanceCriteria = DeserializeList(suggestion.OriginalAcceptanceCriteria),
            SuggestedSkills = DeserializeList(suggestion.OriginalSuggestedSkills),
            UserAction = suggestion.UserAction,
            FinalSummary = suggestion.FinalSummary,
            FinalDescription = suggestion.FinalDescription,
            FinalAcceptanceCriteria = DeserializeList(suggestion.FinalAcceptanceCriteria),
            FinalEstimatePoints = suggestion.FinalEstimatePoints,
            EditDistanceRatio = suggestion.EditDistanceRatio,
            RejectReason = suggestion.RejectReason,
            CreatedIssueId = suggestion.CreatedIssueId
        };
    }

    private static AiPromptTemplateDto MapPrompt(AiPromptTemplate prompt)
    {
        return new AiPromptTemplateDto
        {
            PromptId = prompt.Id,
            Code = prompt.Code,
            Version = prompt.Version,
            TaskType = prompt.TaskType,
            Language = prompt.Language,
            SystemPrompt = prompt.SystemPrompt,
            UserTemplate = prompt.UserTemplate,
            JsonSchema = prompt.JsonSchema,
            IsActive = prompt.IsActive,
            CreatedAt = prompt.CreatedAt
        };
    }

    private static AiPromptTemplateDto SuccessPrompt(AiPromptTemplate prompt, string message)
    {
        var result = MapPrompt(prompt);
        result.Success = true;
        result.Message = message;
        return result;
    }

    private static string RenderPrompt(AiPromptTemplate? prompt, AiBreakdownDto input)
    {
        var systemPrompt = prompt?.SystemPrompt ??
            "FAKE INFERENCE: phân rã yêu cầu thành các sub-task kỹ thuật có thể kiểm thử.";
        var userTemplate = prompt?.UserTemplate ??
            "Title: {{Title}}\nDescription: {{Description}}\nContext: {{ProjectContext}}";
        return $"{systemPrompt}\n\n{userTemplate
            .Replace("{{Title}}", input.StoryTitle, StringComparison.Ordinal)
            .Replace("{{Description}}", input.StoryDescription ?? string.Empty, StringComparison.Ordinal)
            .Replace("{{ProjectContext}}", input.ProjectContext ?? string.Empty, StringComparison.Ordinal)}";
    }

    private static string? SerializeList(List<string>? values)
    {
        var normalized = values?
            .Select(OptionalText)
            .Where(value => value is not null)
            .Select(value => value!)
            .Take(50)
            .ToList();
        return normalized is null || normalized.Count == 0
            ? null
            : JsonSerializer.Serialize(normalized);
    }

    private static List<string>? DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 50_000)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static decimal CalculateEditDistanceRatio(string original, string edited)
    {
        if (original == edited)
        {
            return 0m;
        }

        if (original.Length == 0 || edited.Length == 0)
        {
            return 1m;
        }

        var previous = new int[edited.Length + 1];
        var current = new int[edited.Length + 1];
        for (var column = 0; column <= edited.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= original.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= edited.Length; column++)
            {
                var substitutionCost = original[row - 1] == edited[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return Math.Round(
            (decimal)previous[edited.Length] / Math.Max(original.Length, edited.Length),
            4,
            MidpointRounding.AwayFromZero);
    }

    private static string TruncateForDistance(string value)
    {
        const int maximumLength = 2000;
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    private static string Truncate(string value, int maximumLength)
    {
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    private static AiBreakdownDto Failure(string errorCode, string message)
    {
        return new AiBreakdownDto
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }

    private static AiPromptTemplateDto PromptFailure(string errorCode, string message)
    {
        return new AiPromptTemplateDto
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }

    private static string? OptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
