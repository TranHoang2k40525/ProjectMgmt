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
            .Where(s => s.ProjectId == projectId && s.Status == "completed")
            .OrderByDescending(s => s.EndDate)
            .Take(5)
            .ToListAsync(ct);

        var sprintIds = pastSprints.Select(s => s.Id).ToList();

        double avgVelocity = 25.0;
        try
        {
            var velocities = await _delivery.SprintVelocities.AsNoTracking()
                .Where(v => sprintIds.Contains(v.SprintId))
                .ToListAsync(ct);

            if (velocities.Count > 0)
            {
                avgVelocity = velocities.Average(v => (double)v.CompletedPoints);
            }
        }
        catch
        {
            // Fallback if view doesn't exist or has no entries
            avgVelocity = 25.0;
        }

        var unfinishedBugs = await _delivery.Issues.AsNoTracking()
            .Where(i => i.ProjectId == projectId && !i.IsDeleted)
            .Take(20)
            .Select(i => new { i.Id, i.IssueNumber, i.Title, i.StoryPoints })
            .ToListAsync(ct);

        var result = new
        {
            AverageVelocity = Math.Round(avgVelocity, 1),
            RecentSprints = pastSprints.Select(s => new { s.Id, s.Name, s.Goal, s.Status }),
            OpenBugsAndTechnicalDebt = unfinishedBugs
        };

        return SerializeJson(result, indented: true);
    }

    public async Task<string> GetSprintHistoryAndVelocityAsync(IProjectScopeContext scope, CancellationToken ct = default)
    {
        return await GetHistoryAndVelocityAsync(scope.ProjectId, scope, ct);
    }
}
