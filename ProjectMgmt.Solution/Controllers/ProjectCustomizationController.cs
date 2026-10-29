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
[Route("api/v1/projects/{projectId:guid}")]
public sealed class ProjectCustomizationController(PlanningDbContext db, DeliveryIntelligenceDbContext delivery, ProjectAccess access) : ApiControllerBase
{
    private static bool IsSystemType(string name) =>
        name is "Epic" or "Story" or "Task" or "Bug" or "Subtask" or "Sub-task";

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
            Icon = t.IconKey ?? "task",
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

    // Templates extend the existing workflow: existing status IDs, issues and restricted transitions survive.
    [HttpPost("workflow/template")]
    public async Task<IActionResult> ApplyTemplate(Guid projectId, WorkflowTemplateInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        string[]? names = input.TemplateId switch {
            "scrum-std" => ["To Do", "In Progress", "Code Review", "Done"],
            "agile-qa" => ["To Do", "In Progress", "Code Review", "QA Testing", "Done"],
            "kanban-simple" => ["To Do", "In Progress", "Done"],
            "enterprise" => ["Backlog", "Spec Review", "In Progress", "Code Review", "QA Verification", "Done"],
            _ => null
        };
        if (names is null) return BadRequest(new { title = "Mẫu workflow không hợp lệ." });
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Project SET IssueCounter = IssueCounter WHERE Id = {projectId}");
            var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
            var transitions = await db.WorkflowTransitions.Where(t => t.ProjectId == projectId).ToListAsync();
            var sequence = new List<WorkflowStatus>();
            foreach (var name in names)
            {
                var isDone = name == "Done";
                var isInitial = name is "To Do" or "Backlog";
                var isActive = !isDone && !isInitial;
                var category = isInitial ? "ToDo" : isDone ? "Done" : "InProgress";
                var status = statuses.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (status is null)
                {
                    status = new WorkflowStatus
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Name = name,
                        Category = category,
                        ColorHex = isDone ? "#22C55E" : "#3B82F6",
                        OrderIndex = statuses.Count == 0 ? 1 : statuses.Max(s => s.OrderIndex) + 1,
                        IsInitial = isInitial && !statuses.Any(s => s.IsInitial),
                        IsDone = isDone,
                        IsActiveWork = isActive,
                        IsCanceled = false
                    };
                    statuses.Add(status);
                    db.WorkflowStatuses.Add(status);
                }
                else
                {
                    status.IsDone = isDone;
                    status.IsActiveWork = isActive;
                }
                sequence.Add(status);
            }
            var ordered = sequence.Concat(statuses.Where(s => !sequence.Contains(s))).ToList();
            for (var i = 0; i < ordered.Count; i++) ordered[i].OrderIndex = i + 1;
            for (var i = 0; i < sequence.Count - 1; i++)
            {
                var from = sequence[i]; var to = sequence[i + 1];
                if (!transitions.Any(t => t.FromStatusId == from.Id && t.ToStatusId == to.Id))
                    db.WorkflowTransitions.Add(new WorkflowTransition
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        FromStatusId = from.Id,
                        ToStatusId = to.Id,
                        Name = $"{from.Name} → {to.Name}",
                        RequiredPermissionCode = "issue.transition"
                    });
            }
            await db.SaveChangesAsync();
            var boards = await db.Boards.Where(b => b.ProjectId == projectId).ToListAsync();
            foreach (var board in boards)
            {
                var columns = await db.BoardColumns.Where(c => c.BoardId == board.Id).ToListAsync();
                foreach (var status in sequence.Where(s => !columns.Any(c => c.StatusId == s.Id)))
                {
                    var column = new BoardColumn { Id = Guid.NewGuid(), BoardId = board.Id, StatusId = status.Id, Name = status.Name };
                    columns.Add(column);
                    db.BoardColumns.Add(column);
                }
                var orderedColumns = sequence.Select(s => columns.Single(c => c.StatusId == s.Id))
                    .Concat(columns.Where(c => !sequence.Any(s => s.Id == c.StatusId)).OrderBy(c => c.OrderIndex)).ToList();
                for (var i = 0; i < orderedColumns.Count; i++) orderedColumns[i].OrderIndex = i + 1;
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ordered.Select(s => new
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
