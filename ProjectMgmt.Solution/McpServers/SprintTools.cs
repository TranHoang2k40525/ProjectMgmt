namespace ProjectMgmt.Solution.McpServers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;

public class SprintTools
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

    private readonly PlanningDbContext _planning;
    private readonly DeliveryIntelligenceDbContext _delivery;

    public SprintTools(PlanningDbContext planning, DeliveryIntelligenceDbContext delivery)
    {
        _planning = planning;
        _delivery = delivery;
    }

    public IReadOnlyList<AiToolDefinition> GetToolDefinitions()
    {
        return new List<AiToolDefinition>
        {
            new(
                "sprint_get_history_and_velocity",
                "Truy xuất lịch sử Sprint trước: Vận tốc (Velocity trung bình), các User Story dở dang bị rollover, danh sách Bug tồn đọng và nợ kỹ thuật cần xử lý.",
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

    public async Task<string> GetHistoryAndVelocityAsync(Guid projectId, IProjectScopeContext scope, CancellationToken ct = default)
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

        var pastSprints = await _planning.Sprints.AsNoTracking()
            .Where(s => s.ProjectId == projectId && (s.Status == "Completed" || s.Status == "completed"))
            .OrderByDescending(s => s.EndDate ?? s.StartDate)
            .ThenByDescending(s => s.OrderIndex)
            .Take(5)
            .ToListAsync(ct);

        var activeSprint = await _planning.Sprints.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && (s.Status == "Active" || s.Status == "active"), ct);

        var sprintIds = pastSprints.Select(s => s.Id).ToList();
        if (activeSprint != null)
        {
            sprintIds.Add(activeSprint.Id);
        }

        var doneStatusIds = new HashSet<Guid>();
        try
        {
            var statuses = await _planning.WorkflowStatuses.AsNoTracking()
                .Where(ws => ws.Category == "done" || ws.Name == "Done" || ws.Name == "Hoàn thành")
                .Select(ws => ws.Id)
                .ToListAsync(ct);
            doneStatusIds = statuses.ToHashSet();
        }
        catch
        {
            // Ignore if WorkflowStatuses not available
        }

        var sprintIssues = sprintIds.Count > 0
            ? await _delivery.Issues.AsNoTracking()
                .Where(i => i.ProjectId == projectId && !i.IsDeleted && i.SprintId != null && sprintIds.Contains(i.SprintId.Value) && i.ParentId == null)
                .Select(i => new
                {
                    i.Id,
                    i.IssueNumber,
                    i.SprintId,
                    i.Title,
                    i.StoryPoints,
                    i.EstimatedHours,
                    i.StatusId,
                    i.ResolvedAt,
                    i.AssigneeId
                })
                .ToListAsync(ct)
            : [];

        double avgVelocity = 24.0;
        try
        {
            var velocities = await _delivery.SprintVelocities.AsNoTracking()
                .Where(v => sprintIds.Contains(v.SprintId))
                .ToListAsync(ct);

            if (velocities.Count > 0)
            {
                avgVelocity = velocities.Average(v => (double)v.CompletedPoints);
            }
            else if (pastSprints.Count > 0)
            {
                var pastPointsPerSprint = pastSprints
                    .Select(s => sprintIssues
                        .Where(i => i.SprintId == s.Id && (i.ResolvedAt != null || doneStatusIds.Contains(i.StatusId)))
                        .Sum(i => (double)(i.StoryPoints ?? 0m)))
                    .Where(pts => pts > 0)
                    .ToList();

                if (pastPointsPerSprint.Count > 0)
                {
                    avgVelocity = pastPointsPerSprint.Average();
                }
            }
        }
        catch
        {
            avgVelocity = 24.0;
        }

        var projectKey = project.ProjectKey ?? "QLDA";

        var recentSprintDetails = pastSprints.Select(s =>
        {
            var sIssues = sprintIssues.Where(i => i.SprintId == s.Id).ToList();
            var completed = sIssues.Where(i => i.ResolvedAt != null || doneStatusIds.Contains(i.StatusId)).ToList();
            var unfinished = sIssues.Except(completed).ToList();

            return new
            {
                s.Id,
                s.Name,
                s.Goal,
                s.Status,
                s.StartDate,
                s.EndDate,
                CompletedStoryPoints = completed.Sum(i => i.StoryPoints ?? 0m),
                CompletedTasksCount = completed.Count,
                UnfinishedRolloverCount = unfinished.Count,
                CompletedTasks = completed.Select(i => new
                {
                    IssueKey = $"{projectKey}-{i.IssueNumber}",
                    i.Title,
                    StoryPoints = i.StoryPoints ?? 0m,
                    EstimatedHours = i.EstimatedHours ?? 0m,
                    i.AssigneeId
                }).ToList()
            };
        }).ToList();

        var unfinishedBugs = await _delivery.Issues.AsNoTracking()
            .Where(i => i.ProjectId == projectId && !i.IsDeleted && i.ParentId == null && i.ResolvedAt == null && !doneStatusIds.Contains(i.StatusId))
            .OrderBy(i => i.RankOrder)
            .Take(20)
            .Select(i => new
            {
                i.Id,
                IssueKey = $"{projectKey}-{i.IssueNumber}",
                i.Title,
                i.StoryPoints,
                i.EstimatedHours,
                i.SprintId,
                i.AssigneeId
            })
            .ToListAsync(ct);

        var result = new
        {
            AverageVelocity = Math.Round(avgVelocity, 1),
            ActiveSprint = activeSprint != null
                ? new
                {
                    activeSprint.Id,
                    activeSprint.Name,
                    activeSprint.Goal,
                    activeSprint.StartDate,
                    activeSprint.EndDate,
                    CommittedPoints = sprintIssues.Where(i => i.SprintId == activeSprint.Id).Sum(i => i.StoryPoints ?? 0m)
                }
                : null,
            RecentSprints = recentSprintDetails,
            OpenBugsAndTechnicalDebt = unfinishedBugs
        };

        return SerializeJson(result, indented: true);
    }

    public async Task<string> GetSprintHistoryAndVelocityAsync(IProjectScopeContext scope, CancellationToken ct = default)
    {
        return await GetHistoryAndVelocityAsync(scope.ProjectId, scope, ct);
    }
}
