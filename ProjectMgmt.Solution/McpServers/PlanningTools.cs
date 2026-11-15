namespace ProjectMgmt.Solution.McpServers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;

public class PlanningTools
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

    public PlanningTools(PlanningDbContext planning, DeliveryIntelligenceDbContext delivery)
    {
        _planning = planning;
        _delivery = delivery;
    }

    public IReadOnlyList<AiToolDefinition> GetToolDefinitions()
    {
        return new List<AiToolDefinition>
        {
            new(
                "ai_sprint_plan_propose",
                "Tổng hợp nội dung cuộc họp Sprint Planning + Tiêu chí nghiệm thu (AC) từ Spec Hub + User Story Backlog + Năng lực Developer để tự động đề xuất phân rã Sub-tasks kỹ thuật (Backend, Frontend, QA), gán người làm và ước lượng số giờ chuẩn Scrum.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["projectId"] = new JsonObject { ["type"] = "string", ["description"] = "GUID của dự án" },
                        ["meetingNotes"] = new JsonObject { ["type"] = "string", ["description"] = "Ghi chú cuộc họp thảo luận kỹ thuật hoặc tóm tắt từ PM/PO" },
                        ["targetStoryIds"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "string" },
                            ["description"] = "Danh sách GUID của các User Story được chọn đưa vào Sprint"
                        }
                    },
                    ["required"] = new JsonArray { "projectId", "meetingNotes" }
                }
            )
        };
    }

    public async Task<string> ProposeSprintPlanAsync(
        Guid projectId,
        string meetingNotes,
        IReadOnlyList<Guid>? targetStoryIds,
        IProjectScopeContext scope,
        CancellationToken ct = default)
    {
        if (projectId == Guid.Empty)
        {
            return SerializeJson(new { error = "INVALID_PROJECT_ID", message = "Lỗi: projectId không hợp lệ." });
        }

        if (string.IsNullOrWhiteSpace(meetingNotes))
        {
            return SerializeJson(new { error = "EMPTY_MEETING_NOTES", message = "Vui lòng nhập ghi chú cuộc họp." });
        }

        ProjectScopeGuard.ValidateProject(scope, projectId);

        var project = await _planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
        {
            return SerializeJson(new { error = "PROJECT_NOT_FOUND", message = "Lỗi: Không tìm thấy dự án với ID chỉ định." });
        }

        ProjectScopeGuard.Validate(scope, project.OrgId, projectId);

        var projectKey = project.ProjectKey ?? "PRJ";

        var storiesQuery = _delivery.Issues.AsNoTracking()
            .Where(i => i.ProjectId == projectId && !i.IsDeleted);

        if (targetStoryIds != null && targetStoryIds.Count > 0)
        {
            storiesQuery = storiesQuery.Where(i => targetStoryIds.Contains(i.Id));
        }
        else
        {
            storiesQuery = storiesQuery.Where(i => i.SprintId == null).OrderBy(i => i.RankOrder).Take(5);
        }

        var targetStories = await storiesQuery.ToListAsync(ct);
        var storyIds = targetStories.Select(s => s.Id).ToList();

        var acList = await _delivery.AcceptanceCriteria.AsNoTracking()
            .Where(c => storyIds.Contains(c.IssueId))
            .ToListAsync(ct);

        var firstLineNote = meetingNotes.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "Mục tiêu Sprint";

        var proposal = new
        {
            SprintGoalProposal = $"Hoàn thiện các tính năng cốt lõi: {firstLineNote}",
            TotalProposedStoryPoints = targetStories.Sum(s => s.StoryPoints ?? 3),
            SuggestedWorkItems = targetStories.Select(story => new
            {
                StoryKey = $"{projectKey}-{story.IssueNumber}",
                story.Title,
                Criteria = acList.Where(c => c.IssueId == story.Id).Select(c => c.Content).ToList(),
                SuggestedSubTasks = new[]
                {
                    new { Title = $"[Backend] Thiết kế API & Database cho {story.Title}", EstimatedHours = 6.0, Role = "Backend Developer" },
                    new { Title = $"[Frontend] Xây dựng UI & Tích hợp API cho {story.Title}", EstimatedHours = 6.0, Role = "Frontend Developer" },
                    new { Title = $"[QA] Viết Test Cases & Kiểm thử đối chiếu AC cho {story.Title}", EstimatedHours = 4.0, Role = "QA Tester" }
                }
            }).ToList()
        };

        return SerializeJson(proposal, indented: true);
    }
}
