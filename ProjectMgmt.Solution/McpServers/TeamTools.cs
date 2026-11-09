namespace ProjectMgmt.Solution.McpServers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public TeamTools(PlanningDbContext planning, IdentityExperienceDbContext identity)
    {
        _planning = planning;
        _identity = identity;
    }

    public IReadOnlyList<AiToolDefinition> GetToolDefinitions()
    {
        return new List<AiToolDefinition>
        {
            new(
                "team_get_dev_capacities",
                "Lấy danh sách các thành viên trong đội ngũ phát triển (Developers, QA, Tester), chức danh, kỹ năng chuyên môn và số giờ khả dụng (Capacity) trong chu kỳ Sprint.",
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
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Status == "active", ct);

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
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(ct);

        var profiles = await _identity.UserProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(ct);
        var profileDict = profiles.ToDictionary(p => p.UserId, p => p.DisplayName);

        var roles = await _identity.Roles.AsNoTracking().ToListAsync(ct);
        var roleDict = roles.ToDictionary(r => r.Id, r => r.Name);

        var userSkills = await _identity.UserSkills.AsNoTracking()
            .Where(us => userIds.Contains(us.UserId))
            .ToListAsync(ct);

        var skillCatalogs = await _identity.SkillCatalogs.AsNoTracking().ToListAsync(ct);
        var skillDict = skillCatalogs.ToDictionary(s => s.Id, s => s.Name);

        var capacityDict = capacities.ToDictionary(c => c.UserId);

        var memberDetails = users.Select(u =>
        {
            var userRoleEntries = projectMemberRoles.Where(ur => ur.UserId == u.Id).ToList();
            var userRoleNames = userRoleEntries.Select(ur => roleDict.GetValueOrDefault(ur.RoleId, "Developer")).ToList();
            if (project.LeadUserId == u.Id && !userRoleNames.Contains("Project Director"))
            {
                userRoleNames.Add("Project Director");
            }

            var skills = userSkills.Where(us => us.UserId == u.Id)
                .Select(us => skillDict.GetValueOrDefault(us.SkillId, "General"))
                .ToList();

            capacityDict.TryGetValue(u.Id, out var cap);

            var displayName = profileDict.TryGetValue(u.Id, out var dn) && !string.IsNullOrWhiteSpace(dn) ? dn : u.Email;

            return new
            {
                UserId = u.Id,
                DisplayName = displayName,
                Email = u.Email,
                Roles = userRoleNames,
                AvailableHours = cap?.AvailableHours ?? 40.0m,
                CapacityPoints = cap?.CapacityPoints ?? 8.0m,
                Skills = skills,
                Note = cap?.Note
            };
        }).ToList();

        var result = new
        {
            ProjectLeadUserId = project.LeadUserId,
            ActiveSprintId = currentSprint?.Id,
            Capacities = memberDetails
        };

        return SerializeJson(result, indented: true);
    }

    public async Task<string> GetDevCapacitiesAsync(IProjectScopeContext scope, CancellationToken ct = default)
    {
        return await GetDevCapacitiesAsync(scope.ProjectId, scope, ct);
    }
}
