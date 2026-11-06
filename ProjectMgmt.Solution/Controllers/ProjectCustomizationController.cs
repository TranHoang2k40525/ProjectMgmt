using System.ComponentModel.DataAnnotations;
using DeliveryIntelligence.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

public sealed class IssueTypeInput
{
    [Required, StringLength(60)] public string Name { get; set; } = "";
    [Required, StringLength(50)] public string Icon { get; set; } = "task";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] public string Color { get; set; } = "#6366F1";
    public sbyte HierarchyLevel { get; set; } = 2;
    public bool IsSubtask { get; set; }
    public int? OrderIndex { get; set; }
}

public sealed class WorkflowTemplateInput
{
    [Required] public string TemplateId { get; set; } = "";
}

[Route("api/projects/{projectId:guid}")]
public sealed class ProjectCustomizationController(PlanningDbContext db, DeliveryIntelligenceDbContext delivery, ProjectAccess access) : ApiControllerBase
{
    private static bool IsSystemType(string name) =>
        name is "Epic" or "Story" or "Task" or "Bug" or "Subtask" or "Sub-task";

    private static string DefaultIconForType(string? name, bool isSubtask = false)
    {
        if (isSubtask) return "subdirectory_arrow_right";
        return (name ?? "").Trim().ToLowerInvariant() switch
        {
            "epic" => "bolt",
            "story" => "bookmark",
            "bug" => "bug_report",
            "subtask" or "sub-task" => "subdirectory_arrow_right",
            _ => "task_alt"
        };
    }

    private static string DefaultColorForStatus(string name, string category)
    {
        if (string.Equals(category, "Done", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Done", StringComparison.OrdinalIgnoreCase))
            return "#22C55E";
        if (string.Equals(category, "ToDo", StringComparison.OrdinalIgnoreCase) || name.Equals("To Do", StringComparison.OrdinalIgnoreCase) || name.Equals("Backlog", StringComparison.OrdinalIgnoreCase))
            return "#42526E";
        if (name.Contains("Review", StringComparison.OrdinalIgnoreCase))
            return "#8B5CF6";
        if (name.Contains("QA", StringComparison.OrdinalIgnoreCase) || name.Contains("Test", StringComparison.OrdinalIgnoreCase))
            return "#F59E0B";
        return "#0052CC";
    }

    [HttpGet("issue-types")]
    public async Task<IActionResult> GetIssueTypes(Guid projectId)
    {
        if (!await access.AllowsAsync(projectId)) return Forbid();
        var types = await db.IssueTypes.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.OrderIndex)
            .ToListAsync();

        return Ok(types.Select(t => new
        {
            t.Id,
            t.Name,
            Icon = !string.IsNullOrWhiteSpace(t.IconKey) && t.IconKey != "task" ? t.IconKey : DefaultIconForType(t.Name, t.IsSubtask),
            Color = t.ColorHex,
            t.HierarchyLevel,
            t.IsSubtask,
            t.OrderIndex,
            IsCustom = !IsSystemType(t.Name)
        }));
    }

    [HttpPost("issue-types")]
    public async Task<IActionResult> CreateIssueType(Guid projectId, IssueTypeInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var name = input.Name.Trim();
        if (name.Length == 0)
            return BadRequest(new { title = "Tên loại công việc không được để trống." });
        if (await db.IssueTypes.AnyAsync(t => t.ProjectId == projectId && t.Name == name))
            return Conflict(new { title = "Loại công việc đã tồn tại trong dự án." });

        var maxOrder = await db.IssueTypes.Where(t => t.ProjectId == projectId).MaxAsync(t => (int?)t.OrderIndex) ?? 0;
        var type = new IssueType
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            IconKey = input.Icon.Trim(),
            ColorHex = input.Color,
            IsSubtask = input.IsSubtask,
            HierarchyLevel = input.HierarchyLevel > 0 ? input.HierarchyLevel : (input.IsSubtask ? (sbyte)1 : (sbyte)2),
            OrderIndex = input.OrderIndex ?? (maxOrder + 1)
        };
        db.IssueTypes.Add(type);
        await db.SaveChangesAsync();
        return StatusCode(201, new
        {
            type.Id,
            type.Name,
            Icon = type.IconKey,
            Color = type.ColorHex,
            type.HierarchyLevel,
            type.IsSubtask,
            type.OrderIndex,
            IsCustom = true
        });
    }

    [HttpPut("issue-types/{id:guid}")]
    public async Task<IActionResult> UpdateIssueType(Guid projectId, Guid id, IssueTypeInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var type = await db.IssueTypes.SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);
        if (type is null) return NotFound(new { title = "Không tìm thấy loại công việc." });

        var name = input.Name.Trim();
        if (name.Length == 0) return BadRequest(new { title = "Tên loại công việc không được để trống." });
        if (await db.IssueTypes.AnyAsync(t => t.ProjectId == projectId && t.Id != id && t.Name == name))
            return Conflict(new { title = "Loại công việc có tên này đã tồn tại trong dự án." });

        type.Name = name;
        type.IconKey = input.Icon.Trim();
        type.ColorHex = input.Color;
        type.HierarchyLevel = input.HierarchyLevel > 0 ? input.HierarchyLevel : type.HierarchyLevel;
        type.IsSubtask = input.IsSubtask;
        if (input.OrderIndex.HasValue) type.OrderIndex = input.OrderIndex.Value;

        await db.SaveChangesAsync();
        return Ok(new
        {
            type.Id,
            type.Name,
            Icon = type.IconKey,
            Color = type.ColorHex,
            type.HierarchyLevel,
            type.IsSubtask,
            type.OrderIndex,
            IsCustom = !IsSystemType(type.Name)
        });
    }

    [HttpDelete("issue-types/{id:guid}")]
    public async Task<IActionResult> DeleteIssueType(Guid projectId, Guid id)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var type = await db.IssueTypes.SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);
        if (type is null) return NotFound(new { title = "Không tìm thấy loại công việc." });

        // Chặn xóa nếu còn task sử dụng loại này
        var inUse = await delivery.Issues.AnyAsync(i => i.ProjectId == projectId && i.IssueTypeId == id && !i.IsDeleted);
        if (inUse)
        {
            return Conflict(new { title = "Không thể xóa loại công việc đang có task sử dụng. Hãy chuyển các task sang loại khác trước." });
        }

        db.IssueTypes.Remove(type);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Áp dụng mẫu quy trình từ cơ sở dữ liệu: đồng bộ đúng danh sách trạng thái, bước chuyển tiếp và cột Board theo mẫu đã chọn.
    [HttpPost("workflow/template")]
    public async Task<IActionResult> ApplyTemplate(Guid projectId, WorkflowTemplateInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var templateSteps = new List<(string Name, string Category, string? ColorHex)>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT StatusesJson FROM organizationworkflowtemplate
                WHERE IsActive = 1 AND (Id = @tid OR Code = @tid OR REPLACE(Id, 'preset-', '') = @tid)
                LIMIT 1;";
            var p = cmd.CreateParameter();
            p.ParameterName = "@tid";
            p.Value = input.TemplateId;
            cmd.Parameters.Add(p);
            var json = (await cmd.ExecuteScalarAsync())?.ToString();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        var name = elem.TryGetProperty("Name", out var np) ? np.GetString()
                            : elem.TryGetProperty("name", out var np2) ? np2.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        var cat = elem.TryGetProperty("Category", out var cp) ? cp.GetString()
                            : elem.TryGetProperty("category", out var cp2) ? cp2.GetString() : null;
                        var color = elem.TryGetProperty("ColorHex", out var clp) ? clp.GetString()
                            : elem.TryGetProperty("colorHex", out var clp2) ? clp2.GetString()
                            : elem.TryGetProperty("Color", out var clp3) ? clp3.GetString()
                            : elem.TryGetProperty("color", out var clp4) ? clp4.GetString() : null;

                        var trimmedName = name.Trim();
                        var resolvedCat = !string.IsNullOrWhiteSpace(cat) ? cat.Trim()
                            : trimmedName.Equals("Done", StringComparison.OrdinalIgnoreCase) ? "Done"
                            : (trimmedName.Equals("To Do", StringComparison.OrdinalIgnoreCase) || trimmedName.Equals("Backlog", StringComparison.OrdinalIgnoreCase)) ? "ToDo"
                            : "InProgress";

                        if (!templateSteps.Any(s => s.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
                        {
                            templateSteps.Add((trimmedName, resolvedCat, color));
                        }
                    }
                }
                catch { }
            }
        }

        if (templateSteps.Count == 0)
            return BadRequest(new { title = "Mẫu workflow không tồn tại trong cơ sở dữ liệu." });

        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Project SET IssueCounter = IssueCounter WHERE Id = {projectId}");

            var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
            var transitions = await db.WorkflowTransitions.Where(t => t.ProjectId == projectId).ToListAsync();
            var sequence = new List<WorkflowStatus>();

            for (var idx = 0; idx < templateSteps.Count; idx++)
            {
                var step = templateSteps[idx];
                var isInitial = idx == 0;
                var isDone = string.Equals(step.Category, "Done", StringComparison.OrdinalIgnoreCase)
                    || step.Name.Equals("Done", StringComparison.OrdinalIgnoreCase);
                var isActive = !isInitial && !isDone;
                var category = isDone ? "Done" : (isInitial || string.Equals(step.Category, "ToDo", StringComparison.OrdinalIgnoreCase)) ? "ToDo" : "InProgress";
                var defaultColor = !string.IsNullOrWhiteSpace(step.ColorHex) ? step.ColorHex! : DefaultColorForStatus(step.Name, category);

                var status = statuses.FirstOrDefault(s => s.Name.Equals(step.Name, StringComparison.OrdinalIgnoreCase));
                if (status is null)
                {
                    status = new WorkflowStatus
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Name = step.Name,
                        Category = category,
                        ColorHex = defaultColor,
                        OrderIndex = idx + 1,
                        IsInitial = isInitial,
                        IsDone = isDone,
                        IsActiveWork = isActive,
                        IsCanceled = false
                    };
                    statuses.Add(status);
                    db.WorkflowStatuses.Add(status);
                }
                else
                {
                    status.Name = step.Name;
                    status.Category = category;
                    if (!string.IsNullOrWhiteSpace(step.ColorHex) || string.IsNullOrWhiteSpace(status.ColorHex))
                    {
                        status.ColorHex = defaultColor;
                    }
                    status.OrderIndex = idx + 1;
                    status.IsInitial = isInitial;
                    status.IsDone = isDone;
                    status.IsActiveWork = isActive;
                    status.IsCanceled = false;
                }
                sequence.Add(status);
            }

            await db.SaveChangesAsync();

            var obsoleteStatuses = statuses.Where(s => !sequence.Any(seq => seq.Id == s.Id)).ToList();
            var obsoleteIds = obsoleteStatuses.Select(s => s.Id).ToHashSet();

            // Chuyển các công việc đang nằm ở trạng thái cũ (không thuộc mẫu mới) về trạng thái khởi tạo của mẫu mới
            if (obsoleteIds.Count > 0)
            {
                var fallbackStatusId = sequence[0].Id;
                foreach (var oldId in obsoleteIds)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE issue SET StatusId = {fallbackStatusId} WHERE ProjectId = {projectId} AND StatusId = {oldId}");
                }
            }

            // Làm mới toàn bộ WorkflowTransitions theo đúng quy trình của mẫu mới (cho phép chuyển tiếp tiến và lùi liền kề)
            if (transitions.Count > 0)
            {
                db.WorkflowTransitions.RemoveRange(transitions);
            }

            for (var i = 0; i < sequence.Count - 1; i++)
            {
                var from = sequence[i];
                var to = sequence[i + 1];
                db.WorkflowTransitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    FromStatusId = from.Id,
                    ToStatusId = to.Id,
                    Name = $"{from.Name} -> {to.Name}",
                    RequiredPermissionCode = "issue.transition"
                });
                db.WorkflowTransitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    FromStatusId = to.Id,
                    ToStatusId = from.Id,
                    Name = $"{to.Name} -> {from.Name}",
                    RequiredPermissionCode = "issue.transition"
                });
            }

            // Đồng bộ các cột Board của dự án đúng với các trạng thái trong mẫu
            var boards = await db.Boards.Where(b => b.ProjectId == projectId).ToListAsync();
            foreach (var board in boards)
            {
                var columns = await db.BoardColumns.Where(c => c.BoardId == board.Id).ToListAsync();
                var obsoleteColumns = columns.Where(c => obsoleteIds.Contains(c.StatusId)).ToList();
                if (obsoleteColumns.Count > 0)
                {
                    db.BoardColumns.RemoveRange(obsoleteColumns);
                    foreach (var oc in obsoleteColumns) columns.Remove(oc);
                }

                for (var i = 0; i < sequence.Count; i++)
                {
                    var st = sequence[i];
                    var col = columns.FirstOrDefault(c => c.StatusId == st.Id);
                    if (col is null)
                    {
                        col = new BoardColumn
                        {
                            Id = Guid.NewGuid(),
                            BoardId = board.Id,
                            StatusId = st.Id,
                            Name = st.Name,
                            OrderIndex = i + 1
                        };
                        columns.Add(col);
                        db.BoardColumns.Add(col);
                    }
                    else
                    {
                        col.Name = st.Name;
                        col.OrderIndex = i + 1;
                    }
                }
            }

            if (obsoleteStatuses.Count > 0)
            {
                db.WorkflowStatuses.RemoveRange(obsoleteStatuses);
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(sequence.Select(s => new
            {
                s.Id,
                s.Name,
                s.Category,
                s.ColorHex,
                s.IsInitial,
                s.IsDone,
                s.IsActiveWork,
                s.IsCanceled,
                s.OrderIndex
            }));
        });
    }
}
