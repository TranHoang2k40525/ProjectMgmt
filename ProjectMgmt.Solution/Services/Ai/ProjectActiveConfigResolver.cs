namespace ProjectMgmt.Solution.Services.Ai;

using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;

public sealed record ProjectActiveStatusInfo(
    Guid Id,
    string Name,
    string Category,
    string ColorHex,
    int OrderIndex,
    bool IsInitial,
    bool IsActiveWork,
    bool IsDone,
    bool IsCanceled,
    int? WipLimit);

public sealed record ProjectActiveTransitionInfo(
    string FromStatus,
    string ToStatus,
    string? Name);

public sealed record ProjectActivePriorityInfo(
    Guid Id,
    string Name,
    int Level,
    string ColorHex,
    string? IconKey,
    bool IsCustom);

public sealed record ProjectActiveIssueTypeInfo(
    Guid Id,
    string Name,
    bool IsSubtask,
    sbyte HierarchyLevel,
    int OrderIndex);

public sealed record ProjectActiveLabelInfo(
    Guid Id,
    string Name,
    string ColorHex);

public sealed class ProjectActiveConfig
{
    public Guid ProjectId { get; init; }
    public string ProjectKey { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = "Scrum chuẩn";
    public string WorkflowType { get; init; } = "Scrum";
    public IReadOnlyList<ProjectActiveStatusInfo> Statuses { get; init; } = [];
    public string InitialStatusName { get; init; } = "To Do";
    public HashSet<Guid> DoneStatusIds { get; init; } = [];
    public IReadOnlyList<ProjectActiveTransitionInfo> Transitions { get; init; } = [];
    public IReadOnlyList<ProjectActivePriorityInfo> Priorities { get; init; } = [];
    public bool HasCustomPriorities { get; init; }
    public IReadOnlyList<ProjectActiveIssueTypeInfo> IssueTypes { get; init; } = [];
    public IReadOnlyList<ProjectActiveLabelInfo> Labels { get; init; } = [];

    public IReadOnlyList<ProjectActiveIssueTypeInfo> ParentWorkItemTypes =>
        IssueTypes.Where(t => !t.IsSubtask && !t.Name.Equals("Epic", StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<ProjectActiveIssueTypeInfo> SubtaskTypes =>
        IssueTypes.Where(t => t.IsSubtask).ToList();

    public ProjectActiveIssueTypeInfo? EpicType =>
        IssueTypes.FirstOrDefault(t => !t.IsSubtask && t.Name.Equals("Epic", StringComparison.OrdinalIgnoreCase));

    private static readonly string[] UrgentSynonyms = ["Urgent", "Blocker", "Critical", "Highest", "P0", "Khẩn cấp", "Cao nhất"];
    private static readonly string[] HighSynonyms = ["High", "Major", "P1", "Cao"];
    private static readonly string[] MediumSynonyms = ["Medium", "Normal", "Moderate", "P2", "Trung bình", "Bình thường"];
    private static readonly string[] LowSynonyms = ["Low", "Minor", "Lowest", "Trivial", "P3", "Thấp", "Thấp nhất"];

    public string ResolveStatusName(string? requested)
    {
        if (Statuses.Count == 0)
        {
            return string.IsNullOrWhiteSpace(requested) ? "To Do" : requested.Trim();
        }

        if (!string.IsNullOrWhiteSpace(requested))
        {
            var trimmed = requested.Trim();
            var exact = Statuses.FirstOrDefault(s => s.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.Name;

            if (trimmed.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Hoàn thành", StringComparison.OrdinalIgnoreCase))
            {
                var doneStatus = Statuses.FirstOrDefault(s => s.IsDone || s.Category.Equals("Done", StringComparison.OrdinalIgnoreCase));
                if (doneStatus != null) return doneStatus.Name;
            }

            if (trimmed.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Doing", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                var activeStatus = Statuses.FirstOrDefault(s => s.IsActiveWork || s.Category.Equals("InProgress", StringComparison.OrdinalIgnoreCase));
                if (activeStatus != null) return activeStatus.Name;
            }
        }

        return InitialStatusName;
    }

    public ProjectActivePriorityInfo? ResolvePriorityEntity(string? requested)
    {
        if (Priorities.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(requested))
        {
            var trimmed = requested.Trim();
            var exact = Priorities.FirstOrDefault(p => p.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            if (UrgentSynonyms.Any(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return Priorities.FirstOrDefault(p => UrgentSynonyms.Any(s => s.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                    ?? Priorities.OrderBy(p => p.Level).First();
            }

            if (HighSynonyms.Any(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return Priorities.FirstOrDefault(p => HighSynonyms.Any(s => s.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                    ?? Priorities.OrderBy(p => p.Level).Skip(Priorities.Count > 1 ? 1 : 0).FirstOrDefault()
                    ?? Priorities[0];
            }

            if (MediumSynonyms.Any(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return Priorities.FirstOrDefault(p => MediumSynonyms.Any(s => s.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                    ?? Priorities[Priorities.Count / 2];
            }

            if (LowSynonyms.Any(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return Priorities.LastOrDefault(p => LowSynonyms.Any(s => s.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
                    ?? Priorities.OrderByDescending(p => p.Level).First();
            }
        }

        return Priorities.FirstOrDefault(p => MediumSynonyms.Any(s => s.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
            ?? Priorities[Priorities.Count / 2];
    }

    public string ResolvePriorityName(string? requested)
        => ResolvePriorityEntity(requested)?.Name
           ?? (string.IsNullOrWhiteSpace(requested) ? "Medium" : requested.Trim());

    public string GetDefaultPriorityName() => ResolvePriorityName("Medium");

    public ProjectActivePriorityInfo? GetHighestUrgencyPriority()
        => Priorities.OrderBy(p => p.Level).FirstOrDefault();

    public string ResolveIssueTypeName(string? requested, bool preferSubtask = false)
    {
        if (preferSubtask)
        {
            var sub = SubtaskTypes.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(requested) &&
                t.Name.Equals(requested.Trim(), StringComparison.OrdinalIgnoreCase));
            return sub?.Name ?? SubtaskTypes.FirstOrDefault()?.Name ?? "Sub-task";
        }

        var parents = ParentWorkItemTypes;
        if (parents.Count == 0)
        {
            return string.IsNullOrWhiteSpace(requested) ? "Story" : requested.Trim();
        }

        if (!string.IsNullOrWhiteSpace(requested))
        {
            var trimmed = requested.Trim();
            var exact = parents.FirstOrDefault(t => t.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.Name;

            if (trimmed.Contains("Tech", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Improve", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Chore", StringComparison.OrdinalIgnoreCase))
            {
                var taskType = parents.FirstOrDefault(t => t.Name.Equals("Task", StringComparison.OrdinalIgnoreCase));
                if (taskType != null) return taskType.Name;
            }

            if (trimmed.Contains("Defect", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Hotfix", StringComparison.OrdinalIgnoreCase))
            {
                var bugType = parents.FirstOrDefault(t => t.Name.Equals("Bug", StringComparison.OrdinalIgnoreCase));
                if (bugType != null) return bugType.Name;
            }

            if (trimmed.Contains("Feature", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("User Story", StringComparison.OrdinalIgnoreCase))
            {
                var storyType = parents.FirstOrDefault(t => t.Name.Equals("Story", StringComparison.OrdinalIgnoreCase));
                if (storyType != null) return storyType.Name;
            }
        }

        return parents.FirstOrDefault(t => t.Name.Equals("Task", StringComparison.OrdinalIgnoreCase))?.Name
            ?? parents.FirstOrDefault(t => t.Name.Equals("Story", StringComparison.OrdinalIgnoreCase))?.Name
            ?? parents[0].Name;
    }

    public string FormatWorkflowPromptSection()
    {
        if (Statuses.Count == 0)
        {
            return "To Do [Khởi tạo], In Progress, Code Review, Done [Hoàn thành]";
        }

        var parts = Statuses.Select(s =>
        {
            var flags = new List<string>();
            if (s.Name.Equals(InitialStatusName, StringComparison.OrdinalIgnoreCase)) flags.Add("Khởi tạo mặc định");
            else if (s.IsDone || s.Category.Equals("Done", StringComparison.OrdinalIgnoreCase)) flags.Add("Hoàn thành");
            else if (s.IsCanceled) flags.Add("Đã hủy");
            else if (s.IsActiveWork || s.Category.Equals("InProgress", StringComparison.OrdinalIgnoreCase)) flags.Add("Đang thực thi");

            if (s.WipLimit.HasValue && s.WipLimit.Value > 0) flags.Add($"WIP≤{s.WipLimit.Value}");

            return flags.Count > 0 ? $"{s.Name} ({string.Join(", ", flags)})" : s.Name;
        });

        var transitionSummary = Transitions.Count > 0
            ? $" | Luồng chuyển tiếp hợp lệ: {string.Join("; ", Transitions.Select(t => $"{t.FromStatus} -> {t.ToStatus}").Distinct().Take(16))}"
            : string.Empty;

        return $"Quy trình \"{WorkflowName}\" [{WorkflowType}] | Các trạng thái theo thứ tự cột: {string.Join(" -> ", parts)}{transitionSummary}";
    }

    public string FormatIssueTypesPromptSection()
    {
        if (IssueTypes.Count == 0)
        {
            return "Hạng mục cha: Story, Task, Bug | Việc con: Sub-task | Nhóm tính năng: Epic";
        }

        var parentNames = ParentWorkItemTypes.Select(t => t.Name).ToList();
        var subNames = SubtaskTypes.Select(t => t.Name).ToList();
        var epic = EpicType?.Name;

        var segments = new List<string>
        {
            $"Hạng mục cấp 1 (Parent Issue Types): {(parentNames.Count > 0 ? string.Join(", ", parentNames) : "Story, Task, Bug")}"
        };
        if (subNames.Count > 0) segments.Add($"Việc con (Subtasks): {string.Join(", ", subNames)}");
        if (!string.IsNullOrWhiteSpace(epic)) segments.Add($"Nhóm tính năng lớn (Epic): {epic}");
        return string.Join(" | ", segments);
    }

    public string FormatPrioritiesPromptSection()
    {
        if (Priorities.Count == 0) return "Urgent, High, Medium, Low";
        return string.Join(", ", Priorities.Select(p => $"{p.Name} (Level {p.Level})")) +
               $" [Mặc định áp dụng: {(HasCustomPriorities ? "Cấu hình riêng của dự án" : "Mẫu chuẩn hệ thống")}]";
    }

    public string FormatLabelsPromptSection()
    {
        if (Labels.Count == 0) return "(Dự án chưa tạo sẵn nhãn nào; có thể gắn 1-2 nhãn kỹ thuật súc tích như Backend, Frontend, QA, Security nếu phù hợp)";
        return string.Join(", ", Labels.Select(l => l.Name));
    }
}

public static class ProjectActiveConfigResolver
{
    public static async Task<ProjectActiveConfig> ResolveAsync(
        PlanningDbContext planning,
        DeliveryIntelligenceDbContext? delivery,
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await planning.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);

        var primaryBoard = await planning.Boards.AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var boardColumns = primaryBoard != null
            ? await planning.BoardColumns.AsNoTracking()
                .Where(c => c.BoardId == primaryBoard.Id)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync(ct)
            : [];

        var columnOrderMap = boardColumns
            .GroupBy(c => c.StatusId)
            .ToDictionary(g => g.Key, g => (Order: g.First().OrderIndex, WipLimit: g.First().WipLimit));

        var rawStatuses = await planning.WorkflowStatuses.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync(ct);

        // Sort statuses according to default board column sequence first (matches the active workflow board),
        // then remaining project statuses by OrderIndex.
        var orderedStatuses = rawStatuses
            .OrderBy(s => columnOrderMap.TryGetValue(s.Id, out var col) ? 0 : 1)
            .ThenBy(s => columnOrderMap.TryGetValue(s.Id, out var col) ? col.Order : s.OrderIndex)
            .ThenBy(s => s.OrderIndex)
            .Select(s => new ProjectActiveStatusInfo(
                s.Id,
                s.Name,
                string.IsNullOrWhiteSpace(s.Category) ? "ToDo" : s.Category,
                s.ColorHex,
                columnOrderMap.TryGetValue(s.Id, out var col) ? col.Order : s.OrderIndex,
                s.IsInitial,
                s.IsActiveWork,
                s.IsDone,
                s.IsCanceled,
                columnOrderMap.TryGetValue(s.Id, out var colWip) ? colWip.WipLimit : null))
            .ToList();

        var initialStatusName = orderedStatuses.FirstOrDefault(s => s.IsInitial)?.Name
            ?? orderedStatuses.FirstOrDefault(s => s.Category.Equals("ToDo", StringComparison.OrdinalIgnoreCase))?.Name
            ?? orderedStatuses.FirstOrDefault()?.Name
            ?? "To Do";

        var doneStatusIds = orderedStatuses
            .Where(s => s.IsDone ||
                        s.IsCanceled ||
                        s.Category.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                        s.Name.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                        s.Name.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                        s.Name.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
                        s.Name.Equals("Hoàn thành", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Id)
            .ToHashSet();

        var statusNameById = orderedStatuses.ToDictionary(s => s.Id, s => s.Name);
        var rawTransitions = await planning.WorkflowTransitions.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .ToListAsync(ct);

        var transitions = rawTransitions
            .Where(t => statusNameById.ContainsKey(t.FromStatusId) && statusNameById.ContainsKey(t.ToStatusId))
            .Select(t => new ProjectActiveTransitionInfo(
                statusNameById[t.FromStatusId],
                statusNameById[t.ToStatusId],
                t.Name))
            .ToList();

        // Applied priorities rule: if project has custom priorities (ProjectId == projectId), use ONLY those;
        // otherwise fall back to system default priorities (ProjectId == null).
        var customPriorities = await planning.Priorities.AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .OrderBy(p => p.Level)
            .ToListAsync(ct);

        var hasCustomPriorities = customPriorities.Count > 0;
        var activePriorities = hasCustomPriorities
            ? customPriorities
            : await planning.Priorities.AsNoTracking()
                .Where(p => p.ProjectId == null)
                .OrderBy(p => p.Level)
                .ToListAsync(ct);

        var priorities = activePriorities
            .Select(p => new ProjectActivePriorityInfo(p.Id, p.Name, p.Level, p.ColorHex, p.IconKey, hasCustomPriorities))
            .ToList();

        var issueTypes = await planning.IssueTypes.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.OrderIndex)
            .Select(t => new ProjectActiveIssueTypeInfo(t.Id, t.Name, t.IsSubtask, t.HierarchyLevel, t.OrderIndex))
            .ToListAsync(ct);

        var labels = delivery != null
            ? await delivery.Labels.AsNoTracking()
                .Where(l => l.ProjectId == projectId)
                .OrderBy(l => l.Name)
                .Select(l => new ProjectActiveLabelInfo(l.Id, l.Name, l.ColorHex))
                .ToListAsync(ct)
            : [];

        var workflowName = !string.IsNullOrWhiteSpace(primaryBoard?.Name)
            ? primaryBoard.Name.Replace("Bảng ", string.Empty, StringComparison.OrdinalIgnoreCase).Trim()
            : "Scrum chuẩn";

        return new ProjectActiveConfig
        {
            ProjectId = projectId,
            ProjectKey = project?.ProjectKey ?? "PRJ",
            ProjectName = project?.Name ?? string.Empty,
            WorkflowName = string.IsNullOrWhiteSpace(workflowName) ? "Scrum chuẩn" : workflowName,
            WorkflowType = string.IsNullOrWhiteSpace(primaryBoard?.Type) ? "Scrum" : primaryBoard.Type,
            Statuses = orderedStatuses,
            InitialStatusName = initialStatusName,
            DoneStatusIds = doneStatusIds,
            Transitions = transitions,
            Priorities = priorities,
            HasCustomPriorities = hasCustomPriorities,
            IssueTypes = issueTypes,
            Labels = labels
        };
    }
}
