namespace ProjectMgmt.Solution.McpServers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.Services.Ai;

public class BacklogTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions JsonIndentedOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private static string SerializeJson<T>(T value, bool indented = false) =>
        JsonSerializer.Serialize(value, indented ? JsonIndentedOptions : JsonOptions);

    private readonly DeliveryIntelligenceDbContext _delivery;
    private readonly PlanningDbContext _planning;

    public BacklogTools(DeliveryIntelligenceDbContext delivery, PlanningDbContext planning)
    {
        _delivery = delivery;
        _planning = planning;
    }

    public IReadOnlyList<AiToolDefinition> GetToolDefinitions()
    {
        return new List<AiToolDefinition>
        {
            new(
                "backlog_get_stories",
                "Lấy danh sách toàn bộ hạng mục công việc đang nằm trong Product Backlog (User Story, Task, Bug) kèm loại công việc, mức ưu tiên, nhãn, độ phức tạp (Story Points), Tiêu chí nghiệm thu (Acceptance Criteria) và các việc con sẵn có.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" }
                    },
                    ["required"] = new JsonArray { "projectId" }
                }
            ),
            new(
                "project_get_active_config",
                "Truy xuất cấu hình đang được ÁP DỤNG THỰC TẾ của dự án: Quy trình Workflow (Board, trạng thái theo thứ tự cột, cờ khởi tạo/hoàn thành, luồng chuyển tiếp), Mức độ ưu tiên đang dùng (tùy chỉnh hoặc mặc định), Loại công việc (Issue Types) và danh sách Nhãn (Labels).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" }
                    },
                    ["required"] = new JsonArray { "projectId" }
                }
            )
        };
    }

    public async Task<string> GetStoriesAsync(Guid projectId, IProjectScopeContext scope, CancellationToken ct = default)
    {
        if (projectId == Guid.Empty)
        {
            return SerializeJson(new { error = "INVALID_PROJECT_ID", message = "Lỗi: projectId không hợp lệ." });
        }

        ProjectScopeGuard.ValidateProject(scope, projectId);

        var project = await _planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return SerializeJson(new { error = "PROJECT_NOT_FOUND", message = "Lỗi: Không tìm thấy dự án với ID chỉ định." });
        }

        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        var projectKey = project.ProjectKey ?? "PRJ";
        var config = await ProjectActiveConfigResolver.ResolveAsync(_planning, _delivery, projectId, ct);

        var issueTypeById = config.IssueTypes.ToDictionary(t => t.Id, t => t);
        var statusNameById = config.Statuses.ToDictionary(s => s.Id, s => s.Name);
        var priorityNameById = config.Priorities.ToDictionary(p => p.Id, p => p.Name);

        var epicIds = config.EpicType != null ? config.EpicType.Id : (Guid?)null;

        var rawBacklogIssues = await _delivery.Issues.AsNoTracking()
            .Where(i => i.ProjectId == projectId && i.SprintId == null && !i.IsDeleted)
            .OrderBy(i => i.RankOrder)
            .ToListAsync(ct);

        // Exclude Epics and filter parent items (keep children separate under their parents)
        var epicTitleById = rawBacklogIssues
            .Where(i => epicIds.HasValue && i.IssueTypeId == epicIds.Value)
            .ToDictionary(i => i.Id, i => i.Title);

        var backlogParents = rawBacklogIssues
            .Where(i => i.ParentId == null && (!epicIds.HasValue || i.IssueTypeId != epicIds.Value))
            .Take(50)
            .ToList();

        var parentIds = backlogParents.Select(s => s.Id).ToList();
        var missingEpicIds = backlogParents
            .Where(s => s.EpicId.HasValue && !epicTitleById.ContainsKey(s.EpicId.Value))
            .Select(s => s.EpicId!.Value)
            .Distinct()
            .ToList();

        if (missingEpicIds.Count > 0)
        {
            var extraEpics = await _delivery.Issues.AsNoTracking()
                .Where(i => missingEpicIds.Contains(i.Id) && !i.IsDeleted)
                .Select(i => new { i.Id, i.Title })
                .ToListAsync(ct);
            foreach (var ep in extraEpics) epicTitleById[ep.Id] = ep.Title;
        }

        var subtasksByParent = rawBacklogIssues
            .Where(i => i.ParentId.HasValue && parentIds.Contains(i.ParentId.Value))
            .GroupBy(i => i.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(sub => new
            {
                sub.Id,
                IssueKey = $"{projectKey}-{sub.IssueNumber}",
                sub.Title,
                StatusName = statusNameById.GetValueOrDefault(sub.StatusId, config.InitialStatusName)
            }).ToList());

        var criteria = await _delivery.AcceptanceCriteria.AsNoTracking()
            .Where(c => parentIds.Contains(c.IssueId))
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(ct);

        var criteriaDict = criteria.GroupBy(c => c.IssueId).ToDictionary(g => g.Key, g => g.Select(c => c.Content).ToList());

        var labelNameById = config.Labels.ToDictionary(l => l.Id, l => l.Name);
        var issueLabelLinks = await _delivery.IssueLabels.AsNoTracking()
            .Where(il => parentIds.Contains(il.IssueId))
            .ToListAsync(ct);
        var labelsByIssue = issueLabelLinks
            .Where(il => labelNameById.ContainsKey(il.LabelId))
            .GroupBy(il => il.IssueId)
            .ToDictionary(g => g.Key, g => g.Select(il => labelNameById[il.LabelId]).Distinct().ToList());

        var result = backlogParents.Select(s =>
        {
            var issueTypeName = issueTypeById.TryGetValue(s.IssueTypeId, out var it) ? it.Name : "Story";
            return new
            {
                s.Id,
                IssueKey = $"{projectKey}-{s.IssueNumber}",
                s.Title,
                s.Description,
                IssueType = issueTypeName,
                IsUserStory = issueTypeName.Equals("Story", StringComparison.OrdinalIgnoreCase),
                Priority = s.PriorityId.HasValue ? priorityNameById.GetValueOrDefault(s.PriorityId.Value, config.GetDefaultPriorityName()) : config.GetDefaultPriorityName(),
                StatusName = statusNameById.GetValueOrDefault(s.StatusId, config.InitialStatusName),
                Labels = labelsByIssue.GetValueOrDefault(s.Id, []),
                EpicTitle = s.EpicId.HasValue ? epicTitleById.GetValueOrDefault(s.EpicId.Value) : null,
                s.StoryPoints,
                s.EstimatedHours,
                s.StatusId,
                ExistingSubtasks = subtasksByParent.GetValueOrDefault(s.Id, []),
                AcceptanceCriteria = criteriaDict.GetValueOrDefault(s.Id, [])
            };
        });

        return SerializeJson(result, indented: true);
    }

    public async Task<string> GetActiveConfigAsync(Guid projectId, IProjectScopeContext scope, CancellationToken ct = default)
    {
        if (projectId == Guid.Empty)
        {
            return SerializeJson(new { error = "INVALID_PROJECT_ID", message = "Lỗi: projectId không hợp lệ." });
        }

        ProjectScopeGuard.ValidateProject(scope, projectId);

        var project = await _planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return SerializeJson(new { error = "PROJECT_NOT_FOUND", message = "Lỗi: Không tìm thấy dự án với ID chỉ định." });
        }

        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        var config = await ProjectActiveConfigResolver.ResolveAsync(_planning, _delivery, projectId, ct);

        return SerializeJson(new
        {
            config.ProjectId,
            config.ProjectKey,
            config.ProjectName,
            Workflow = new
            {
                Name = config.WorkflowName,
                Type = config.WorkflowType,
                InitialStatus = config.InitialStatusName,
                Statuses = config.Statuses.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Category,
                    s.ColorHex,
                    s.OrderIndex,
                    s.IsInitial,
                    s.IsActiveWork,
                    s.IsDone,
                    s.IsCanceled,
                    s.WipLimit
                }),
                Transitions = config.Transitions
            },
            Priorities = new
            {
                IsCustomProjectConfiguration = config.HasCustomPriorities,
                DefaultPriority = config.GetDefaultPriorityName(),
                Items = config.Priorities.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Level,
                    p.ColorHex,
                    p.IconKey
                })
            },
            IssueTypes = new
            {
                ParentWorkItems = config.ParentWorkItemTypes.Select(t => new { t.Id, t.Name, t.OrderIndex }),
                Subtasks = config.SubtaskTypes.Select(t => new { t.Id, t.Name, t.OrderIndex }),
                Epic = config.EpicType != null ? new { config.EpicType.Id, config.EpicType.Name } : null
            },
            Labels = config.Labels.Select(l => new { l.Id, l.Name, l.ColorHex })
        }, indented: true);
    }

    public Task<ProjectActiveConfig> ResolveActiveConfigAsync(Guid projectId, CancellationToken ct = default)
        => ProjectActiveConfigResolver.ResolveAsync(_planning, _delivery, projectId, ct);

    public async Task<string> GetActiveStoriesAsync(IProjectScopeContext scope, CancellationToken ct = default)
    {
        return await GetStoriesAsync(scope.ProjectId, scope, ct);
    }

    /// <summary>
    /// Nạp siêu dữ liệu thực tế của Sprint, Công việc, Epic và Thành viên trong phạm vi dự án để đối chiếu phạm vi xuất Excel.
    /// </summary>
    public async Task<ProjectExportMetadataResult?> GetExportMetadataAsync(
        Guid projectId,
        IProjectScopeContext scope,
        IReadOnlyDictionary<Guid, string>? memberNamesById = null,
        CancellationToken ct = default)
    {
        if (projectId == Guid.Empty)
        {
            return null;
        }

        ProjectScopeGuard.ValidateProject(scope, projectId);

        var project = await _planning.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return null;
        }

        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        var projectKey = string.IsNullOrWhiteSpace(project.ProjectKey) ? "PRJ" : project.ProjectKey.Trim();
        var config = await ProjectActiveConfigResolver.ResolveAsync(_planning, _delivery, projectId, ct);

        var issueTypeById = config.IssueTypes.ToDictionary(t => t.Id, t => t);
        var statusNameById = config.Statuses.ToDictionary(s => s.Id, s => s.Name);
        var priorityNameById = config.Priorities.ToDictionary(p => p.Id, p => p.Name);

        var sprints = await _planning.Sprints.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.OrderIndex)
            .ThenBy(s => s.StartDate)
            .ToListAsync(ct);

        var sprintById = sprints.ToDictionary(s => s.Id, s => s);

        var rawIssues = await _delivery.Issues.AsNoTracking()
            .Where(i => i.ProjectId == projectId && !i.IsDeleted)
            .OrderBy(i => i.RankOrder)
            .ThenBy(i => i.IssueNumber)
            .ToListAsync(ct);

        var epicIdSet = new HashSet<Guid>();
        if (config.EpicType != null)
        {
            epicIdSet.Add(config.EpicType.Id);
        }

        var epicTitleById = new Dictionary<Guid, string>();
        foreach (var issue in rawIssues)
        {
            bool isEpic = epicIdSet.Contains(issue.IssueTypeId) ||
                          (issueTypeById.TryGetValue(issue.IssueTypeId, out var tInfo) &&
                           tInfo.Name.Equals("Epic", StringComparison.OrdinalIgnoreCase));
            if (isEpic)
            {
                epicTitleById[issue.Id] = issue.Title;
            }
        }

        var issueSummaries = rawIssues.Select(i =>
        {
            var typeInfo = issueTypeById.GetValueOrDefault(i.IssueTypeId);
            var typeName = typeInfo?.Name ?? "Story";
            var isEpic = typeName.Equals("Epic", StringComparison.OrdinalIgnoreCase);
            var sprintName = i.SprintId.HasValue && sprintById.TryGetValue(i.SprintId.Value, out var sp)
                ? sp.Name
                : null;
            var epicTitle = i.EpicId.HasValue ? epicTitleById.GetValueOrDefault(i.EpicId.Value) : null;
            var statusName = statusNameById.GetValueOrDefault(i.StatusId, config.InitialStatusName);
            var prioName = i.PriorityId.HasValue
                ? priorityNameById.GetValueOrDefault(i.PriorityId.Value, config.GetDefaultPriorityName())
                : config.GetDefaultPriorityName();
            var assigneeName = i.AssigneeId.HasValue && memberNamesById != null
                ? memberNamesById.GetValueOrDefault(i.AssigneeId.Value)
                : null;

            return new ExportIssueMetadata(
                i.Id,
                $"{projectKey}-{i.IssueNumber}",
                i.IssueNumber,
                i.ParentId,
                i.SprintId,
                sprintName,
                i.EpicId,
                epicTitle,
                i.Title,
                typeName,
                isEpic,
                typeInfo?.IsSubtask ?? false,
                statusName,
                prioName,
                i.StoryPoints ?? 0m,
                i.IsBlocked,
                i.BlockReason,
                i.AssigneeId,
                assigneeName);
        }).ToList();

        var parentCountBySprint = issueSummaries
            .Where(i => i.SprintId.HasValue && !i.IsEpic && i.ParentId == null)
            .GroupBy(i => i.SprintId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var sprintSummaries = sprints.Select(s => new ExportSprintMetadata(
            s.Id,
            s.Name,
            s.Goal,
            (s.Status ?? "future").Trim().ToLowerInvariant(),
            s.OrderIndex,
            s.StartDate,
            s.EndDate,
            parentCountBySprint.GetValueOrDefault(s.Id, 0))).ToList();

        var memberList = (memberNamesById ?? new Dictionary<Guid, string>())
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => new ExportMemberMetadata(kv.Key, kv.Value.Trim()))
            .OrderBy(m => m.DisplayName)
            .ToList();

        return new ProjectExportMetadataResult(
            projectKey,
            project.Name,
            sprintSummaries,
            issueSummaries,
            memberList,
            epicTitleById.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList());
    }
}

public sealed record ExportSprintMetadata(
    Guid Id,
    string Name,
    string? Goal,
    string Status,
    int OrderIndex,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int ParentItemCount);

public sealed record ExportIssueMetadata(
    Guid Id,
    string IssueKey,
    int IssueNumber,
    Guid? ParentId,
    Guid? SprintId,
    string? SprintName,
    Guid? EpicId,
    string? EpicTitle,
    string Title,
    string IssueType,
    bool IsEpic,
    bool IsSubtask,
    string StatusName,
    string Priority,
    decimal StoryPoints,
    bool IsBlocked,
    string? BlockReason,
    Guid? AssigneeUserId,
    string? AssigneeName);

public sealed record ExportMemberMetadata(
    Guid UserId,
    string DisplayName);

public sealed record ProjectExportMetadataResult(
    string ProjectKey,
    string ProjectName,
    IReadOnlyList<ExportSprintMetadata> Sprints,
    IReadOnlyList<ExportIssueMetadata> Issues,
    IReadOnlyList<ExportMemberMetadata> Members,
    IReadOnlyList<string> Epics);
