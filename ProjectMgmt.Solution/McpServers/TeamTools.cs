namespace ProjectMgmt.Solution.McpServers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;

public class TeamTools
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
    private readonly IdentityExperienceDbContext _identity;
    private readonly DeliveryIntelligenceDbContext? _delivery;

    public TeamTools(PlanningDbContext planning, IdentityExperienceDbContext identity)
    {
        _planning = planning;
        _identity = identity;
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TeamTools(
        PlanningDbContext planning,
        IdentityExperienceDbContext identity,
        DeliveryIntelligenceDbContext delivery)
    {
        _planning = planning;
        _identity = identity;
        _delivery = delivery;
    }

    public IReadOnlyList<AiToolDefinition> GetToolDefinitions()
    {
        return new List<AiToolDefinition>
        {
            new(
                "team_get_dev_capacities",
                "Lấy hồ sơ chuyên môn an toàn (họ tên, chức danh, cấp bậc, kỹ năng, kinh nghiệm), lịch sử công việc đã thực hiện ở các Sprint trước và số giờ khả dụng (Capacity) của từng thành viên trong đội dự án (không bao gồm mật khẩu hay thông tin nhạy cảm).",
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

    public async Task<string> GetDevCapacitiesAsync(Guid projectId, IProjectScopeContext scope, CancellationToken ct = default)
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

        var currentSprint = await _planning.Sprints.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && (s.Status == "Active" || s.Status == "active"), ct);

        var capacities = currentSprint != null
            ? await _planning.SprintMemberCapacities.AsNoTracking()
                .Where(c => c.SprintId == currentSprint.Id)
                .ToListAsync(ct)
            : [];

        // Project members and roles
        var projectMemberRoles = await _identity.UserRoles.AsNoTracking()
            .Where(ur => ur.ScopeType == "Project" && ur.ScopeId == projectId)
            .ToListAsync(ct);

        var userIds = projectMemberRoles.Select(ur => ur.UserId).Distinct().ToList();
        if (project.LeadUserId != Guid.Empty && !userIds.Contains(project.LeadUserId))
        {
            userIds.Add(project.LeadUserId);
        }

        var users = await _identity.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id }) // Strictly select only Id - never read PasswordHash, SecurityStamp, or sensitive fields
            .ToListAsync(ct);

        var profiles = await _identity.UserProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new
            {
                p.UserId,
                p.DisplayName,
                p.JobTitle,
                p.SeniorityLevel,
                p.YearsOfExperience,
                p.Bio
            })
            .ToListAsync(ct);
        var profileDict = profiles.ToDictionary(p => p.UserId);

        var roles = await _identity.Roles.AsNoTracking().ToListAsync(ct);
        var roleDict = roles.ToDictionary(r => r.Id, r => r.Name);

        var userSkills = await _identity.UserSkills.AsNoTracking()
            .Where(us => userIds.Contains(us.UserId))
            .ToListAsync(ct);

        var skillCatalogs = await _identity.SkillCatalogs.AsNoTracking().ToListAsync(ct);
        var skillDict = skillCatalogs.ToDictionary(s => s.Id);

        var capacityDict = capacities.GroupBy(c => c.UserId).ToDictionary(g => g.Key, g => g.First());

        // Historical & active sprint workload per member from DeliveryIntelligenceDbContext
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
            // Ignore if WorkflowStatuses not populated
        }

        var projectIssues = _delivery != null
            ? await _delivery.Issues.AsNoTracking()
                .Where(i => i.ProjectId == projectId && !i.IsDeleted && i.AssigneeId != null && i.ParentId == null)
                .Select(i => new
                {
                    i.Id,
                    i.IssueNumber,
                    i.Title,
                    i.AssigneeId,
                    i.SprintId,
                    i.StatusId,
                    i.StoryPoints,
                    i.EstimatedHours,
                    i.RemainingHours,
                    i.ResolvedAt
                })
                .ToListAsync(ct)
            : [];

        var memberDetails = users.Select(u =>
        {
            var userRoleEntries = projectMemberRoles.Where(ur => ur.UserId == u.Id).ToList();
            var userRoleNames = userRoleEntries.Select(ur => roleDict.GetValueOrDefault(ur.RoleId, "Developer")).Distinct().ToList();
            if (project.LeadUserId == u.Id && !userRoleNames.Contains("Project Director"))
            {
                userRoleNames.Insert(0, "Project Director");
            }

            profileDict.TryGetValue(u.Id, out var profile);

            var memberSkillDetails = userSkills
                .Where(us => us.UserId == u.Id)
                .Select(us =>
                {
                    skillDict.TryGetValue(us.SkillId, out var catalog);
                    var skillName = catalog?.Name ?? "Kỹ thuật phần mềm";
                    var category = catalog?.Category ?? "General";
                    return new
                    {
                        SkillCode = catalog?.Code ?? "GENERAL",
                        SkillName = skillName,
                        Category = category,
                        ProficiencyLevel = (int)us.ProficiencyLevel,
                        YearsOfExperience = us.YearsOfExperience ?? 2.0m,
                        Summary = $"{skillName} (Mức {us.ProficiencyLevel}/5, {us.YearsOfExperience ?? 2.0m:0.#} năm KN)"
                    };
                })
                .ToList();

            var skills = memberSkillDetails.Select(s => s.Summary).ToList();

            capacityDict.TryGetValue(u.Id, out var cap);

            var displayName = !string.IsNullOrWhiteSpace(profile?.DisplayName)
                ? profile.DisplayName
                : $"Thành viên dự án ({u.Id.ToString()[..8]})";

            var assignedIssues = projectIssues.Where(i => i.AssigneeId == u.Id).ToList();
            var completedIssues = assignedIssues
                .Where(i => i.ResolvedAt != null || doneStatusIds.Contains(i.StatusId))
                .ToList();
            var activeSprintIssues = currentSprint != null
                ? assignedIssues.Where(i => i.SprintId == currentSprint.Id && i.ResolvedAt == null && !doneStatusIds.Contains(i.StatusId)).ToList()
                : [];

            var availableHours = cap?.AvailableHours ?? 40.0m;
            var committedActiveHours = activeSprintIssues.Sum(i => i.RemainingHours ?? i.EstimatedHours ?? 0m);
            var remainingHours = Math.Max(0m, availableHours - committedActiveHours);

            return new
            {
                UserId = u.Id,
                DisplayName = displayName,
                FullName = displayName,
                JobTitle = profile?.JobTitle ?? "Kỹ sư dự án Scrum",
                SeniorityLevel = profile?.SeniorityLevel ?? "Senior",
                YearsOfExperience = profile?.YearsOfExperience ?? 3.0m,
                Bio = profile?.Bio ?? "Thành viên thực thi công việc trong dự án Scrum.",
                Roles = userRoleNames,
                AvailableHours = availableHours,
                RemainingAvailableHours = remainingHours,
                CapacityPoints = cap?.CapacityPoints ?? 8.0m,
                Skills = skills,
                SkillProfiles = memberSkillDetails,
                HistoricalPerformance = new
                {
                    CompletedTasksCount = completedIssues.Count,
                    CompletedStoryPoints = completedIssues.Sum(i => i.StoryPoints ?? 0m),
                    ActiveSprintInProgressCount = activeSprintIssues.Count,
                    ActiveSprintCommittedPoints = activeSprintIssues.Sum(i => i.StoryPoints ?? 0m),
                    RecentCompletedTasks = completedIssues
                        .OrderByDescending(i => i.IssueNumber)
                        .Take(5)
                        .Select(i => $"{project.ProjectKey}-{i.IssueNumber}: {i.Title} ({i.StoryPoints ?? 0} SP)")
                        .ToList()
                },
                Note = cap?.Note
            };
        }).ToList();

        var result = new
        {
            ProjectLeadUserId = project.LeadUserId,
            ActiveSprintId = currentSprint?.Id,
            ActiveSprintName = currentSprint?.Name,
            PrivacyNotice = "Chỉ trích xuất họ tên, chức danh, cấp bậc, kỹ năng và lịch sử công việc; đã loại bỏ 100% mật khẩu và thông tin nhạy cảm.",
            Capacities = memberDetails
        };

        return SerializeJson(result, indented: true);
    }

    public async Task<string> GetDevCapacitiesAsync(IProjectScopeContext scope, CancellationToken ct = default)
    {
        return await GetDevCapacitiesAsync(scope.ProjectId, scope, ct);
    }
}
