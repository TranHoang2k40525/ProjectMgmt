namespace ProjectMgmt.Solution.Services.Ai;

using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Dtos.Ai;

public interface IAiSprintCommitCoordinator
{
    Task<AiApplySprintProposalResponse> ApplySprintProposalAsync(
        AiApplySprintProposalRequest request,
        IProjectScopeContext scope,
        CancellationToken ct = default);
}

public class AiSprintCommitCoordinator : IAiSprintCommitCoordinator
{
    private readonly PlanningDbContext _planningDb;
    private readonly DeliveryIntelligenceDbContext _deliveryDb;
    private readonly IStateStagingService _stagingService;
    private readonly IUserLookupService _userLookup;
    private readonly ILogger<AiSprintCommitCoordinator> _logger;

    public AiSprintCommitCoordinator(
        PlanningDbContext planningDb,
        DeliveryIntelligenceDbContext deliveryDb,
        IStateStagingService stagingService,
        IUserLookupService userLookup,
        ILogger<AiSprintCommitCoordinator> logger)
    {
        _planningDb = planningDb;
        _deliveryDb = deliveryDb;
        _stagingService = stagingService;
        _userLookup = userLookup;
        _logger = logger;
    }

    public async Task<AiApplySprintProposalResponse> ApplySprintProposalAsync(
        AiApplySprintProposalRequest request,
        IProjectScopeContext scope,
        CancellationToken ct = default)
    {
        ProjectScopeGuard.EnsureValidScope(scope);

        if (string.IsNullOrWhiteSpace(request.SprintGoal))
        {
            return Failure("INVALID_SPRINT_GOAL", "Sprint Goal không được để trống.");
        }

        if (request.Tasks == null || request.Tasks.Count == 0)
        {
            return Failure("EMPTY_TASK_LIST", "Danh sách công việc đề xuất không được để trống.");
        }

        try
        {
            var project = await _planningDb.Projects
                .FirstOrDefaultAsync(p => p.Id == scope.ProjectId && !p.IsDeleted && !p.IsArchived, ct);

            if (project == null)
            {
                return Failure("PROJECT_NOT_FOUND", "Dự án không tồn tại hoặc đã bị lưu trữ/xóa.");
            }

            ProjectScopeGuard.Validate(scope, project.OrgId, project.Id);

            var issueTypes = await _planningDb.IssueTypes.AsNoTracking()
                .Where(t => t.ProjectId == project.Id)
                .OrderBy(t => t.OrderIndex)
                .ToListAsync(ct);

            var statuses = await _planningDb.WorkflowStatuses.AsNoTracking()
                .Where(s => s.ProjectId == project.Id)
                .OrderByDescending(s => s.IsInitial)
                .ThenBy(s => s.OrderIndex)
                .ToListAsync(ct);

            var initialStatus = statuses.FirstOrDefault();
            if (initialStatus == null || issueTypes.Count == 0)
            {
                return Failure("PROJECT_CONFIG_MISSING", "Dự án chưa được cấu hình loại công việc hoặc trạng thái quy trình.");
            }

            var priorities = await _planningDb.Priorities.AsNoTracking()
                .Where(p => p.ProjectId == project.Id || p.ProjectId == null)
                .OrderBy(p => p.Level)
                .ToListAsync(ct);

            var defaultPriority = priorities.FirstOrDefault(p => p.Name.Equals("Medium", StringComparison.OrdinalIgnoreCase))
                ?? priorities.FirstOrDefault();

            var epicIssueType = issueTypes.FirstOrDefault(t => !t.IsSubtask && t.Name.Equals("Epic", StringComparison.OrdinalIgnoreCase));
            var subtaskIssueType = issueTypes.FirstOrDefault(t => t.IsSubtask)
                ?? issueTypes.FirstOrDefault(t => t.Name.Contains("Sub", StringComparison.OrdinalIgnoreCase));
            var defaultParentType = issueTypes.FirstOrDefault(t => !t.IsSubtask && !t.Name.Equals("Epic", StringComparison.OrdinalIgnoreCase))
                ?? issueTypes.First();

            var now = DateTime.UtcNow;

            // 1. Resolve or create target Sprint (next Planned sprint)
            Sprint? targetSprint = null;
            if (request.TargetSprintId.HasValue && request.TargetSprintId.Value != Guid.Empty)
            {
                targetSprint = await _planningDb.Sprints
                    .FirstOrDefaultAsync(s => s.Id == request.TargetSprintId.Value && s.ProjectId == project.Id && s.Status != "Completed", ct);
            }

            targetSprint ??= await _planningDb.Sprints
                .Where(s => s.ProjectId == project.Id && s.Status == "Planned")
                .OrderBy(s => s.OrderIndex)
                .ThenBy(s => s.StartDate)
                .FirstOrDefaultAsync(ct);

            if (targetSprint == null)
            {
                var sprintCount = await _planningDb.Sprints.CountAsync(s => s.ProjectId == project.Id, ct);
                targetSprint = new Sprint
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = $"{project.ProjectKey} Sprint {sprintCount + 1}",
                    Goal = request.SprintGoal.Trim(),
                    Status = "Planned",
                    StartDate = DateOnly.FromDateTime(now),
                    EndDate = DateOnly.FromDateTime(now.AddDays(14)),
                    OrderIndex = sprintCount,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _planningDb.Sprints.Add(targetSprint);
            }
            else
            {
                targetSprint.Goal = request.SprintGoal.Trim();
                targetSprint.UpdatedAt = now;
            }

            // 2. Load active users for assignee resolution
            var allUsers = await _userLookup.GetAllActiveDisplayInfoAsync();
            var userById = allUsers.ToDictionary(u => u.UserId);

            // 3. Load existing Epics in project
            var existingEpics = epicIssueType != null
                ? await _deliveryDb.Issues.AsNoTracking()
                    .Where(i => i.ProjectId == project.Id && !i.IsDeleted && i.IssueTypeId == epicIssueType.Id)
                    .ToListAsync(ct)
                : [];

            var epicIdByTitle = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var ep in existingEpics)
            {
                if (!string.IsNullOrWhiteSpace(ep.Title) && !epicIdByTitle.ContainsKey(ep.Title.Trim()))
                {
                    epicIdByTitle[ep.Title.Trim()] = ep.Id;
                }
            }

            var maxIssueNumber = await _deliveryDb.Issues
                .Where(i => i.ProjectId == project.Id)
                .MaxAsync(i => (int?)i.IssueNumber, ct) ?? 0;
            var currentIssueNumber = Math.Max(project.IssueCounter, maxIssueNumber);

            var maxRank = await _deliveryDb.Issues
                .Where(i => i.ProjectId == project.Id)
                .MaxAsync(i => (decimal?)i.RankOrder, ct) ?? 0m;

            var newIssues = new List<Issue>();
            var newCriteria = new List<AcceptanceCriteria>();
            var newLogs = new List<ActivityLog>();
            var createdSummaries = new List<AiCreatedIssueItemDto>();
            var createdTaskCount = 0;
            var createdSubtaskCount = 0;
            var aiLogId = Guid.NewGuid();

            foreach (var taskDto in request.Tasks)
            {
                if (string.IsNullOrWhiteSpace(taskDto.Title))
                {
                    continue;
                }

                // Resolve EpicId
                Guid? resolvedEpicId = null;
                if (taskDto.EpicId.HasValue && existingEpics.Any(e => e.Id == taskDto.EpicId.Value))
                {
                    resolvedEpicId = taskDto.EpicId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(taskDto.EpicTitle))
                {
                    var cleanEpicTitle = taskDto.EpicTitle.Trim();
                    if (epicIdByTitle.TryGetValue(cleanEpicTitle, out var cachedEpicId))
                    {
                        resolvedEpicId = cachedEpicId;
                    }
                    else if (epicIssueType != null)
                    {
                        currentIssueNumber++;
                        maxRank += 1000m;
                        var newEpic = new Issue
                        {
                            Id = Guid.NewGuid(),
                            ProjectId = project.Id,
                            IssueNumber = currentIssueNumber,
                            SprintId = null,
                            ReporterId = scope.UserId,
                            StatusId = initialStatus.Id,
                            IssueTypeId = epicIssueType.Id,
                            PriorityId = defaultPriority?.Id,
                            Title = cleanEpicTitle,
                            Description = $"Epic được khởi tạo tự động từ AI Phân tách cho {targetSprint.Name}.",
                            StoryPoints = 0,
                            RankOrder = maxRank,
                            IsAiGenerated = true,
                            AiGenerationLogId = aiLogId,
                            IsDeleted = false,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        newIssues.Add(newEpic);
                        epicIdByTitle[cleanEpicTitle] = newEpic.Id;
                        resolvedEpicId = newEpic.Id;
                    }
                }

                // Resolve IssueType
                var parentIssueType = issueTypes.FirstOrDefault(t =>
                    !t.IsSubtask && t.Name.Equals(taskDto.Type?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? defaultParentType;

                // Resolve Priority
                var priorityName = string.Equals(taskDto.Priority, "Urgent", StringComparison.OrdinalIgnoreCase)
                    ? "Highest"
                    : (taskDto.Priority ?? "Medium");
                var matchedPriority = priorities.FirstOrDefault(p => p.Name.Equals(priorityName, StringComparison.OrdinalIgnoreCase))
                    ?? defaultPriority;

                // Resolve Status
                var matchedStatus = !string.IsNullOrWhiteSpace(taskDto.Status)
                    ? statuses.FirstOrDefault(s => s.Name.Equals(taskDto.Status.Trim(), StringComparison.OrdinalIgnoreCase)) ?? initialStatus
                    : initialStatus;

                // Resolve Assignee
                Guid? resolvedAssigneeId = null;
                if (taskDto.AssigneeUserId.HasValue && userById.ContainsKey(taskDto.AssigneeUserId.Value))
                {
                    resolvedAssigneeId = taskDto.AssigneeUserId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(taskDto.AssigneeName))
                {
                    var matchedUser = allUsers.FirstOrDefault(u =>
                        u.DisplayName.Equals(taskDto.AssigneeName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        u.Email.Equals(taskDto.AssigneeName.Trim(), StringComparison.OrdinalIgnoreCase));
                    resolvedAssigneeId = matchedUser?.UserId;
                }

                currentIssueNumber++;
                maxRank += 1000m;
                var parentIssueId = Guid.NewGuid();
                var estHours = Math.Max(0m, taskDto.EstimatedHours);
                var remHours = taskDto.RemainingHours > 0m ? taskDto.RemainingHours : estHours;
                var points = Math.Max(0m, taskDto.StoryPoints);

                var parentIssue = new Issue
                {
                    Id = parentIssueId,
                    ProjectId = project.Id,
                    IssueNumber = currentIssueNumber,
                    SprintId = targetSprint.Id,
                    EpicId = resolvedEpicId,
                    AssigneeId = resolvedAssigneeId,
                    ReporterId = scope.UserId,
                    StatusId = matchedStatus.Id,
                    IssueTypeId = parentIssueType.Id,
                    PriorityId = matchedPriority?.Id,
                    Title = taskDto.Title.Trim(),
                    Description = taskDto.Description?.Trim(),
                    StoryPoints = points,
                    EstimatedHours = estHours,
                    RemainingHours = remHours,
                    OriginalEstimateMinutes = (int)Math.Round(estHours * 60m, MidpointRounding.AwayFromZero),
                    TimeSpentMinutes = 0,
                    RankOrder = maxRank,
                    IsAiGenerated = true,
                    AiGenerationLogId = aiLogId,
                    IsAiAssigned = resolvedAssigneeId.HasValue,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                newIssues.Add(parentIssue);
                createdTaskCount++;
                createdSummaries.Add(new AiCreatedIssueItemDto
                {
                    IssueId = parentIssue.Id,
                    IssueKey = $"{project.ProjectKey}-{parentIssue.IssueNumber}",
                    Title = parentIssue.Title,
                    Type = parentIssueType.Name,
                    StoryPoints = parentIssue.StoryPoints
                });

                // Add Acceptance Criteria
                if (taskDto.AcceptanceCriteria != null)
                {
                    for (var idx = 0; idx < taskDto.AcceptanceCriteria.Count; idx++)
                    {
                        var criterionText = taskDto.AcceptanceCriteria[idx]?.Trim();
                        if (string.IsNullOrWhiteSpace(criterionText)) continue;

                        newCriteria.Add(new AcceptanceCriteria
                        {
                            Id = Guid.NewGuid(),
                            IssueId = parentIssueId,
                            Content = criterionText.Length > 2000 ? criterionText[..2000] : criterionText,
                            IsMet = false,
                            OrderIndex = idx,
                            Source = "AI",
                            CreatedAt = now
                        });
                    }
                }

                newLogs.Add(new ActivityLog
                {
                    Id = Guid.NewGuid(),
                    IssueId = parentIssueId,
                    UserId = scope.UserId,
                    Action = "AiSprintTaskCommitted",
                    Detail = JsonSerializer.Serialize(new
                    {
                        sessionId = request.SessionId,
                        sprintId = targetSprint.Id,
                        sprintName = targetSprint.Name
                    }),
                    Source = "AI",
                    CreatedAt = now
                });

                // Add Subtasks if Subtask IssueType exists
                if (subtaskIssueType != null && taskDto.Subtasks != null)
                {
                    foreach (var subDto in taskDto.Subtasks)
                    {
                        if (string.IsNullOrWhiteSpace(subDto.Title)) continue;

                        currentIssueNumber++;
                        maxRank += 1000m;
                        var subId = Guid.NewGuid();
                        var subHours = Math.Max(0m, subDto.EstimatedHours);
                        var subTitle = subDto.Title.Trim();
                        if (!string.IsNullOrWhiteSpace(subDto.Role) && !subTitle.StartsWith("[", StringComparison.Ordinal))
                        {
                            subTitle = $"[{subDto.Role.Trim()}] {subTitle}";
                        }

                        Guid? subAssigneeId = resolvedAssigneeId;
                        if (subDto.AssigneeUserId.HasValue && userById.ContainsKey(subDto.AssigneeUserId.Value))
                        {
                            subAssigneeId = subDto.AssigneeUserId.Value;
                        }
                        else if (!string.IsNullOrWhiteSpace(subDto.AssigneeName))
                        {
                            var matchedSubUser = allUsers.FirstOrDefault(u =>
                                u.DisplayName.Equals(subDto.AssigneeName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                u.Email.Equals(subDto.AssigneeName.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (matchedSubUser != null) subAssigneeId = matchedSubUser.UserId;
                        }

                        var subIssue = new Issue
                        {
                            Id = subId,
                            ProjectId = project.Id,
                            IssueNumber = currentIssueNumber,
                            SprintId = targetSprint.Id,
                            ParentId = parentIssueId,
                            EpicId = resolvedEpicId,
                            AssigneeId = subAssigneeId,
                            ReporterId = scope.UserId,
                            StatusId = initialStatus.Id,
                            IssueTypeId = subtaskIssueType.Id,
                            PriorityId = matchedPriority?.Id,
                            Title = subTitle,
                            Description = $"Công việc con thuộc {project.ProjectKey}-{parentIssue.IssueNumber}: {parentIssue.Title}",
                            StoryPoints = 0,
                            EstimatedHours = subHours,
                            RemainingHours = subHours,
                            OriginalEstimateMinutes = (int)Math.Round(subHours * 60m, MidpointRounding.AwayFromZero),
                            TimeSpentMinutes = 0,
                            RankOrder = maxRank,
                            IsAiGenerated = true,
                            AiGenerationLogId = aiLogId,
                            IsAiAssigned = subAssigneeId.HasValue,
                            IsDeleted = false,
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        newIssues.Add(subIssue);
                        createdSubtaskCount++;
                        createdSummaries.Add(new AiCreatedIssueItemDto
                        {
                            IssueId = subIssue.Id,
                            IssueKey = $"{project.ProjectKey}-{subIssue.IssueNumber}",
                            Title = subIssue.Title,
                            Type = subtaskIssueType.Name,
                            ParentIssueId = parentIssueId,
                            StoryPoints = 0
                        });
                    }
                }
            }

            if (createdTaskCount == 0)
            {
                return Failure("NO_VALID_TASKS", "Không có công việc hợp lệ nào để tạo vào Sprint.");
            }

            project.IssueCounter = currentIssueNumber;

            await _deliveryDb.Issues.AddRangeAsync(newIssues, ct);
            if (newCriteria.Count > 0)
            {
                await _deliveryDb.AcceptanceCriteria.AddRangeAsync(newCriteria, ct);
            }
            if (newLogs.Count > 0)
            {
                await _deliveryDb.ActivityLogs.AddRangeAsync(newLogs, ct);
            }

            await _planningDb.SaveChangesAsync(ct);
            await _deliveryDb.SaveChangesAsync(ct);

            if (!string.IsNullOrWhiteSpace(request.SessionId))
            {
                await _stagingService.RemoveProposalAsync(request.SessionId, ct);
            }

            var totalSp = request.Tasks.Sum(t => Math.Max(0m, t.StoryPoints));

            return new AiApplySprintProposalResponse
            {
                Success = true,
                Message = $"Đã áp dụng thành công {createdTaskCount} công việc và {createdSubtaskCount} công việc con vào {targetSprint.Name}.",
                SprintId = targetSprint.Id,
                SprintName = targetSprint.Name,
                SprintGoal = targetSprint.Goal ?? request.SprintGoal,
                CreatedIssueCount = createdTaskCount + createdSubtaskCount,
                CreatedTaskCount = createdTaskCount,
                CreatedSubtaskCount = createdSubtaskCount,
                TotalStoryPoints = totalSp,
                CreatedIssues = createdSummaries
            };
        }
        catch (ProjectScopeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiSprintCommitCoordinator] Lỗi khi commit đề xuất Sprint cho dự án {ProjectId}", scope.ProjectId);
            _planningDb.ChangeTracker.Clear();
            _deliveryDb.ChangeTracker.Clear();
            return Failure("COMMIT_FAILED", $"Không thể lưu đề xuất vào Sprint: {ex.Message}");
        }
    }

    private static AiApplySprintProposalResponse Failure(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        Message = message
    };
}
