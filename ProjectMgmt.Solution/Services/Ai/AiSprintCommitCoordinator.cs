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

            var activeConfig = await ProjectActiveConfigResolver.ResolveAsync(_planningDb, _deliveryDb, project.Id, ct);

            var issueTypes = await _planningDb.IssueTypes.AsNoTracking()
                .Where(t => t.ProjectId == project.Id)
                .OrderBy(t => t.OrderIndex)
                .ToListAsync(ct);

            var initialStatus = activeConfig.Statuses
                .FirstOrDefault(s => s.Name.Equals(activeConfig.InitialStatusName, StringComparison.OrdinalIgnoreCase))
                ?? activeConfig.Statuses.FirstOrDefault();
            if (initialStatus == null || issueTypes.Count == 0)
            {
                return Failure("PROJECT_CONFIG_MISSING", "Dự án chưa được cấu hình loại công việc hoặc trạng thái quy trình.");
            }

            var defaultPriority = activeConfig.ResolvePriorityEntity(null);

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

            var epicTypeId = epicIssueType?.Id;
            var backlogIssues = await _deliveryDb.Issues
                .Where(i => i.ProjectId == project.Id && i.SprintId == null && !i.IsDeleted && i.ParentId == null && (!epicTypeId.HasValue || i.IssueTypeId != epicTypeId.Value))
                .ToListAsync(ct);
            var backlogById = backlogIssues.ToDictionary(i => i.Id);
            var backlogByNumber = backlogIssues.GroupBy(i => i.IssueNumber).ToDictionary(g => g.Key, g => g.First());
            var movedBacklogIds = new HashSet<Guid>();

            var backlogParentIds = backlogIssues.Select(i => i.Id).ToList();
            var existingBacklogSubtasks = backlogParentIds.Count > 0
                ? await _deliveryDb.Issues
                    .Where(i => i.ProjectId == project.Id && i.ParentId.HasValue && backlogParentIds.Contains(i.ParentId.Value) && !i.IsDeleted)
                    .ToListAsync(ct)
                : [];
            var existingCriteriaByIssue = backlogParentIds.Count > 0
                ? (await _deliveryDb.AcceptanceCriteria.AsNoTracking()
                    .Where(c => backlogParentIds.Contains(c.IssueId))
                    .ToListAsync(ct))
                    .GroupBy(c => c.IssueId)
                    .ToDictionary(g => g.Key, g => g.Select(c => c.Content).ToHashSet(StringComparer.OrdinalIgnoreCase))
                : new Dictionary<Guid, HashSet<string>>();
            var existingIssueLabelsByIssue = backlogParentIds.Count > 0
                ? (await _deliveryDb.IssueLabels.AsNoTracking()
                    .Where(il => backlogParentIds.Contains(il.IssueId))
                    .ToListAsync(ct))
                    .GroupBy(il => il.IssueId)
                    .ToDictionary(g => g.Key, g => g.Select(il => il.LabelId).ToHashSet())
                : new Dictionary<Guid, HashSet<Guid>>();

            var existingLabels = await _deliveryDb.Labels
                .Where(l => l.ProjectId == project.Id)
                .ToListAsync(ct);
            var labelByName = existingLabels
                .GroupBy(l => l.Name.Trim().TrimStart('#'), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var newIssues = new List<Issue>();
            var newLabels = new List<Label>();
            var newIssueLabels = new List<IssueLabel>();
            var newCriteria = new List<AcceptanceCriteria>();
            var newLogs = new List<ActivityLog>();
            var createdSummaries = new List<AiCreatedIssueItemDto>();
            var createdTaskCount = 0;
            var createdSubtaskCount = 0;
            var movedFromBacklogCount = 0;
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

                // Resolve IssueType from active project IssueTypes
                var resolvedTypeName = activeConfig.ResolveIssueTypeName(taskDto.Type);
                var parentIssueType = issueTypes.FirstOrDefault(t =>
                    !t.IsSubtask && t.Name.Equals(resolvedTypeName, StringComparison.OrdinalIgnoreCase))
                    ?? defaultParentType;

                // Resolve Priority from active project Priorities (respects custom priority schemes such as Urgent/High/Normal/Low)
                var matchedPriority = activeConfig.ResolvePriorityEntity(taskDto.Priority) ?? defaultPriority;

                // Resolve Status from active project WorkflowStatuses (respects custom initial/active statuses)
                var resolvedStatusName = activeConfig.ResolveStatusName(taskDto.Status);
                var matchedStatus = activeConfig.Statuses.FirstOrDefault(s =>
                    s.Name.Equals(resolvedStatusName, StringComparison.OrdinalIgnoreCase)) ?? initialStatus;

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

                // Match existing Product Backlog item so it is MOVED into Sprint instead of duplicated
                Issue? matchedBacklogIssue = null;
                if (taskDto.SourceBacklogIssueId.HasValue &&
                    backlogById.TryGetValue(taskDto.SourceBacklogIssueId.Value, out var byIdCandidate) &&
                    !movedBacklogIds.Contains(byIdCandidate.Id))
                {
                    matchedBacklogIssue = byIdCandidate;
                }
                else if (!string.IsNullOrWhiteSpace(taskDto.SourceBacklogIssueKey))
                {
                    var dashIdx = taskDto.SourceBacklogIssueKey.LastIndexOf('-');
                    var numStr = dashIdx >= 0 ? taskDto.SourceBacklogIssueKey[(dashIdx + 1)..] : taskDto.SourceBacklogIssueKey;
                    if (int.TryParse(numStr.Trim(), out var issueNum) &&
                        backlogByNumber.TryGetValue(issueNum, out var byKeyCandidate) &&
                        !movedBacklogIds.Contains(byKeyCandidate.Id))
                    {
                        matchedBacklogIssue = byKeyCandidate;
                    }
                }
                else
                {
                    var normalizedTitle = taskDto.Title.Trim();
                    matchedBacklogIssue = backlogIssues.FirstOrDefault(b =>
                        !movedBacklogIds.Contains(b.Id) &&
                        string.Equals(b.Title?.Trim(), normalizedTitle, StringComparison.OrdinalIgnoreCase));
                }

                Guid parentIssueId;
                string parentIssueKey;
                string parentTitle;
                var estHours = Math.Max(0m, taskDto.EstimatedHours);
                var remHours = taskDto.RemainingHours > 0m ? taskDto.RemainingHours : estHours;
                var points = Math.Max(0m, taskDto.StoryPoints);

                if (matchedBacklogIssue != null)
                {
                    movedBacklogIds.Add(matchedBacklogIssue.Id);
                    movedFromBacklogCount++;
                    parentIssueId = matchedBacklogIssue.Id;
                    parentIssueKey = $"{project.ProjectKey}-{matchedBacklogIssue.IssueNumber}";

                    matchedBacklogIssue.SprintId = targetSprint.Id;
                    if (resolvedEpicId.HasValue) matchedBacklogIssue.EpicId = resolvedEpicId.Value;
                    matchedBacklogIssue.StatusId = matchedStatus.Id;
                    matchedBacklogIssue.IssueTypeId = parentIssueType.Id;
                    if (matchedPriority != null) matchedBacklogIssue.PriorityId = matchedPriority.Id;
                    if (resolvedAssigneeId.HasValue)
                    {
                        matchedBacklogIssue.AssigneeId = resolvedAssigneeId.Value;
                        matchedBacklogIssue.IsAiAssigned = true;
                    }
                    matchedBacklogIssue.Title = taskDto.Title.Trim();
                    if (!string.IsNullOrWhiteSpace(taskDto.Description))
                    {
                        matchedBacklogIssue.Description = taskDto.Description.Trim();
                    }
                    matchedBacklogIssue.StoryPoints = points;
                    matchedBacklogIssue.EstimatedHours = estHours;
                    matchedBacklogIssue.RemainingHours = remHours;
                    matchedBacklogIssue.OriginalEstimateMinutes = (int)Math.Round(estHours * 60m, MidpointRounding.AwayFromZero);
                    matchedBacklogIssue.IsAiGenerated = true;
                    matchedBacklogIssue.AiGenerationLogId = aiLogId;
                    matchedBacklogIssue.UpdatedAt = now;
                    parentTitle = matchedBacklogIssue.Title;

                    // Also move any pre-existing child subtasks belonging to this backlog item into the target sprint
                    foreach (var existingChild in existingBacklogSubtasks.Where(s => s.ParentId == matchedBacklogIssue.Id))
                    {
                        existingChild.SprintId = targetSprint.Id;
                        existingChild.UpdatedAt = now;
                    }

                    createdTaskCount++;
                    createdSummaries.Add(new AiCreatedIssueItemDto
                    {
                        IssueId = matchedBacklogIssue.Id,
                        IssueKey = parentIssueKey,
                        Title = matchedBacklogIssue.Title,
                        Type = parentIssueType.Name,
                        StoryPoints = matchedBacklogIssue.StoryPoints
                    });
                }
                else
                {
                    currentIssueNumber++;
                    maxRank += 1000m;
                    parentIssueId = Guid.NewGuid();
                    parentIssueKey = $"{project.ProjectKey}-{currentIssueNumber}";

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

                    parentTitle = parentIssue.Title;
                    newIssues.Add(parentIssue);
                    createdTaskCount++;
                    createdSummaries.Add(new AiCreatedIssueItemDto
                    {
                        IssueId = parentIssue.Id,
                        IssueKey = parentIssueKey,
                        Title = parentIssue.Title,
                        Type = parentIssueType.Name,
                        StoryPoints = parentIssue.StoryPoints
                    });
                }

                // Persist project Labels (reusing existing project labels first)
                if (taskDto.Tags != null && taskDto.Tags.Count > 0)
                {
                    var alreadyLinkedLabels = existingIssueLabelsByIssue.GetValueOrDefault(parentIssueId) ?? new HashSet<Guid>();
                    foreach (var rawTag in taskDto.Tags)
                    {
                        var cleanTag = rawTag?.Trim().TrimStart('#').Trim();
                        if (string.IsNullOrWhiteSpace(cleanTag) || cleanTag.Length > 100) continue;

                        if (!labelByName.TryGetValue(cleanTag, out var targetLabel))
                        {
                            targetLabel = new Label
                            {
                                Id = Guid.NewGuid(),
                                ProjectId = project.Id,
                                Name = cleanTag,
                                ColorHex = "#DFE1E6"
                            };
                            labelByName[cleanTag] = targetLabel;
                            newLabels.Add(targetLabel);
                        }

                        if (alreadyLinkedLabels.Add(targetLabel.Id))
                        {
                            newIssueLabels.Add(new IssueLabel
                            {
                                IssueId = parentIssueId,
                                LabelId = targetLabel.Id
                            });
                        }
                    }
                }

                // Add Acceptance Criteria (skipping duplicates on moved backlog items)
                if (taskDto.AcceptanceCriteria != null)
                {
                    var existingAcSet = existingCriteriaByIssue.GetValueOrDefault(parentIssueId) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var idx = 0; idx < taskDto.AcceptanceCriteria.Count; idx++)
                    {
                        var criterionText = taskDto.AcceptanceCriteria[idx]?.Trim();
                        if (string.IsNullOrWhiteSpace(criterionText) || existingAcSet.Contains(criterionText)) continue;

                        existingAcSet.Add(criterionText);
                        newCriteria.Add(new AcceptanceCriteria
                        {
                            Id = Guid.NewGuid(),
                            IssueId = parentIssueId,
                            Content = criterionText.Length > 2000 ? criterionText[..2000] : criterionText,
                            IsMet = false,
                            OrderIndex = existingAcSet.Count + idx,
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
                    Action = matchedBacklogIssue != null ? "AiSprintBacklogItemMovedToSprint" : "AiSprintTaskCommitted",
                    Detail = JsonSerializer.Serialize(new
                    {
                        sessionId = request.SessionId,
                        sprintId = targetSprint.Id,
                        sprintName = targetSprint.Name,
                        movedFromBacklog = matchedBacklogIssue != null
                    }),
                    Source = "AI",
                    CreatedAt = now
                });

                // Add Subtasks if Subtask IssueType exists
                if (subtaskIssueType != null && taskDto.Subtasks != null)
                {
                    var existingChildTitles = existingBacklogSubtasks
                        .Where(s => s.ParentId == parentIssueId)
                        .Select(s => s.Title.Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var subDto in taskDto.Subtasks)
                    {
                        if (string.IsNullOrWhiteSpace(subDto.Title)) continue;

                        var rawSubTitle = subDto.Title.Trim();
                        var prefixedSubTitle = !string.IsNullOrWhiteSpace(subDto.Role) && !rawSubTitle.StartsWith("[", StringComparison.Ordinal)
                            ? $"[{subDto.Role.Trim()}] {rawSubTitle}"
                            : rawSubTitle;

                        if (existingChildTitles.Contains(rawSubTitle) || existingChildTitles.Contains(prefixedSubTitle))
                        {
                            continue;
                        }

                        currentIssueNumber++;
                        maxRank += 1000m;
                        var subId = Guid.NewGuid();
                        var subHours = Math.Max(0m, subDto.EstimatedHours);

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

                        var subStatusName = activeConfig.ResolveStatusName(subDto.Status);
                        var subStatus = activeConfig.Statuses.FirstOrDefault(s =>
                            s.Name.Equals(subStatusName, StringComparison.OrdinalIgnoreCase)) ?? matchedStatus;
                        var subPriority = activeConfig.ResolvePriorityEntity(subDto.Priority) ?? matchedPriority;

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
                            StatusId = subStatus.Id,
                            IssueTypeId = subtaskIssueType.Id,
                            PriorityId = subPriority?.Id,
                            Title = prefixedSubTitle,
                            Description = !string.IsNullOrWhiteSpace(subDto.Description)
                                ? subDto.Description.Trim()
                                : $"Công việc con thuộc {parentIssueKey}: {parentTitle}",
                            StoryPoints = Math.Max(0, subDto.StoryPoints),
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

                        if (subDto.AcceptanceCriteria != null)
                        {
                            for (var acIdx = 0; acIdx < subDto.AcceptanceCriteria.Count; acIdx++)
                            {
                                var subAc = subDto.AcceptanceCriteria[acIdx]?.Trim();
                                if (string.IsNullOrWhiteSpace(subAc)) continue;
                                newCriteria.Add(new AcceptanceCriteria
                                {
                                    Id = Guid.NewGuid(),
                                    IssueId = subId,
                                    Content = subAc.Length > 2000 ? subAc[..2000] : subAc,
                                    IsMet = false,
                                    OrderIndex = acIdx,
                                    Source = "AI",
                                    CreatedAt = now
                                });
                            }
                        }

                        createdSummaries.Add(new AiCreatedIssueItemDto
                        {
                            IssueId = subIssue.Id,
                            IssueKey = $"{project.ProjectKey}-{subIssue.IssueNumber}",
                            Title = subIssue.Title,
                            Type = subtaskIssueType.Name,
                            ParentIssueId = parentIssueId,
                            StoryPoints = subIssue.StoryPoints
                        });
                    }
                }
            }

            if (createdTaskCount == 0)
            {
                return Failure("NO_VALID_TASKS", "Không có công việc hợp lệ nào để tạo vào Sprint.");
            }

            project.IssueCounter = currentIssueNumber;

            // 4. Kế thừa công việc dở dang từ Sprint trước (Sprint Rollover) & Xử lý Task quá hạn
            var rolloverCount = 0;
            var backlogReturnedCount = 0;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var doneStatusIds = activeConfig.DoneStatusIds;

            // Tìm Sprint trước đó của dự án (Sprint có OrderIndex ngay trước targetSprint hoặc Sprint gần nhất)
            var previousSprint = await _planningDb.Sprints.AsNoTracking()
                .Where(s => s.ProjectId == project.Id && s.Id != targetSprint.Id && s.OrderIndex <= targetSprint.OrderIndex)
                .OrderByDescending(s => s.OrderIndex)
                .ThenByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (previousSprint != null)
            {
                var unfinishedIssues = await _deliveryDb.Issues
                    .Where(i => i.SprintId == previousSprint.Id && !i.IsDeleted && !doneStatusIds.Contains(i.StatusId))
                    .ToListAsync(ct);

                var bugTypeId = issueTypes.FirstOrDefault(t => t.Name.Contains("Bug", StringComparison.OrdinalIgnoreCase))?.Id;

                foreach (var unfinished in unfinishedIssues)
                {
                    var isBug = bugTypeId.HasValue && unfinished.IssueTypeId == bugTypeId.Value;
                    var isInProgress = unfinished.TimeSpentMinutes > 0 ||
                                       activeConfig.Statuses.Any(s => s.Id == unfinished.StatusId && (s.IsActiveWork || s.Category.Equals("InProgress", StringComparison.OrdinalIgnoreCase)));
                    var isOverdue = unfinished.DueDate.HasValue && unfinished.DueDate.Value < today;

                    if (isBug || isInProgress || !isOverdue)
                    {
                        // Kế thừa sang Sprint mới (Rollover)
                        unfinished.SprintId = targetSprint.Id;
                        unfinished.UpdatedAt = now;

                        // Nếu là Bug quá hạn, tăng mức ưu tiên lên cấp độ khẩn cấp nhất theo cấu hình dự án
                        if (isBug && isOverdue)
                        {
                            var highestPrio = activeConfig.GetHighestUrgencyPriority();
                            if (highestPrio != null)
                            {
                                unfinished.PriorityId = highestPrio.Id;
                            }
                        }

                        newLogs.Add(new ActivityLog
                        {
                            Id = Guid.NewGuid(),
                            IssueId = unfinished.Id,
                            UserId = scope.UserId,
                            Action = "AiSprintRollover",
                            Detail = JsonSerializer.Serialize(new
                            {
                                fromSprintId = previousSprint.Id,
                                fromSprintName = previousSprint.Name,
                                toSprintId = targetSprint.Id,
                                toSprintName = targetSprint.Name,
                                reason = isBug ? "Bug tồn đọng nghiêm trọng được tự động kế thừa" : "Công việc dở dang được kế thừa tiếp tục hoàn thiện"
                            }),
                            Source = "AI",
                            CreatedAt = now
                        });

                        rolloverCount++;
                    }
                    else
                    {
                        // Task thường chưa từng bắt đầu (TimeSpent = 0) nhưng đã quá hạn -> Chuyển về Product Backlog
                        unfinished.SprintId = null;
                        unfinished.UpdatedAt = now;

                        newLogs.Add(new ActivityLog
                        {
                            Id = Guid.NewGuid(),
                            IssueId = unfinished.Id,
                            UserId = scope.UserId,
                            Action = "AiSprintOverdueReturnedToBacklog",
                            Detail = JsonSerializer.Serialize(new
                            {
                                fromSprintId = previousSprint.Id,
                                fromSprintName = previousSprint.Name,
                                reason = "Công việc quá hạn từ Sprint cũ chưa bắt đầu, chuyển về Product Backlog để PO thẩm định lại độ ưu tiên và giá trị"
                            }),
                            Source = "AI",
                            CreatedAt = now
                        });

                        backlogReturnedCount++;
                    }
                }
            }

            if (newIssues.Count > 0)
            {
                await _deliveryDb.Issues.AddRangeAsync(newIssues, ct);
            }
            if (newLabels.Count > 0)
            {
                await _deliveryDb.Labels.AddRangeAsync(newLabels, ct);
            }
            if (newIssueLabels.Count > 0)
            {
                await _deliveryDb.IssueLabels.AddRangeAsync(newIssueLabels, ct);
            }
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

            var movedSummary = movedFromBacklogCount > 0
                ? $" (trong đó đã chuyển {movedFromBacklogCount} công việc từ Backlog sang)"
                : string.Empty;
            var rolloverSummary = rolloverCount > 0
                ? $" Đã kế thừa {rolloverCount} hạng mục dở dang từ {previousSprint?.Name}."
                : string.Empty;
            var backlogSummary = backlogReturnedCount > 0
                ? $" Đã chuyển {backlogReturnedCount} công việc quá hạn về Product Backlog để thẩm định lại."
                : string.Empty;

            return new AiApplySprintProposalResponse
            {
                Success = true,
                Message = $"Đã áp dụng thành công {createdTaskCount} công việc{movedSummary} và {createdSubtaskCount} công việc con vào {targetSprint.Name}.{rolloverSummary}{backlogSummary}",
                SprintId = targetSprint.Id,
                SprintName = targetSprint.Name,
                SprintGoal = targetSprint.Goal ?? request.SprintGoal,
                CreatedIssueCount = createdTaskCount + createdSubtaskCount,
                CreatedTaskCount = createdTaskCount,
                CreatedSubtaskCount = createdSubtaskCount,
                MovedFromBacklogCount = movedFromBacklogCount,
                RolloverTaskCount = rolloverCount,
                BacklogReturnedCount = backlogReturnedCount,
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
