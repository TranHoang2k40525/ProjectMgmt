namespace ProjectMgmt.Solution.McpServers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;

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
                "Lấy danh sách các User Story đang có trong Product Backlog kèm theo mô tả, độ phức tạp (Story Points), Tiêu chí nghiệm thu (Acceptance Criteria) và liên kết tài liệu đặc tả gốc.",
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

        var stories = await _delivery.Issues.AsNoTracking()
            .Where(i => i.ProjectId == projectId && i.SprintId == null && !i.IsDeleted)
            .OrderBy(i => i.RankOrder)
            .Take(50)
            .ToListAsync(ct);

        var storyIds = stories.Select(s => s.Id).ToList();
        var criteria = await _delivery.AcceptanceCriteria.AsNoTracking()
            .Where(c => storyIds.Contains(c.IssueId))
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(ct);

        var criteriaDict = criteria.GroupBy(c => c.IssueId).ToDictionary(g => g.Key, g => g.Select(c => c.Content).ToList());

        var result = stories.Select(s => new
        {
            s.Id,
            IssueKey = $"{projectKey}-{s.IssueNumber}",
            s.Title,
            s.Description,
            s.StoryPoints,
            s.StatusId,
            AcceptanceCriteria = criteriaDict.GetValueOrDefault(s.Id, [])
        });

        return SerializeJson(result, indented: true);
    }

    public async Task<string> GetActiveStoriesAsync(IProjectScopeContext scope, CancellationToken ct = default)
    {
        return await GetStoriesAsync(scope.ProjectId, scope, ct);
    }
}
