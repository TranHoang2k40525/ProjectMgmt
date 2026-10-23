using System.ComponentModel.DataAnnotations;
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
}

public sealed class WorkflowTemplateInput
{
    [Required] public string TemplateId { get; set; } = "";
}

[Route("api/projects/{projectId:guid}")]
[Route("api/v1/projects/{projectId:guid}")]
public sealed class ProjectCustomizationController(PlanningDbContext db, ProjectAccess access) : ApiControllerBase
{
    [HttpPost("issue-types")]
    public async Task<IActionResult> CreateIssueType(Guid projectId, IssueTypeInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var name = input.Name.Trim();
        if (name.Length == 0 || name.Equals("Sub-task", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { title = "Tên loại công việc không hợp lệ hoặc đã được dành riêng." });
        if (await db.IssueTypes.AnyAsync(t => t.ProjectId == projectId && t.Name == name))
            return Conflict(new { title = "Loại công việc đã tồn tại trong dự án." });
        var type = new IssueType { Id = Guid.NewGuid(), ProjectId = projectId, Name = name,
            IconKey = input.Icon.Trim(), ColorHex = input.Color, IsSubtask = false, HierarchyLevel = 2,
            OrderIndex = (await db.IssueTypes.Where(t => t.ProjectId == projectId).MaxAsync(t => (int?)t.OrderIndex) ?? 0) + 1 };
        db.IssueTypes.Add(type);
        await db.SaveChangesAsync();
        return StatusCode(201, new { type.Id, type.Name, Icon = type.IconKey, Color = type.ColorHex, IsCustom = true });
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
            // Serialize template applications for this project, including retries.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Project SET IssueCounter = IssueCounter WHERE Id = {projectId}");
            var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
            var transitions = await db.WorkflowTransitions.Where(t => t.ProjectId == projectId).ToListAsync();
            var sequence = new List<WorkflowStatus>();
            foreach (var name in names)
            {
                var category = name is "To Do" or "Backlog" or "Spec Review" ? "ToDo" : name == "Done" ? "Done" : "InProgress";
                var status = statuses.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (status is not null && status.Category != category)
                    return Conflict(new { title = $"Trạng thái {name} có nhóm khác với mẫu. Hãy kiểm tra cấu hình hiện tại." });
                if (status is null)
                {
                    status = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = projectId, Name = name,
                        Category = category, ColorHex = category == "Done" ? "#22C55E" : "#3B82F6",
                        OrderIndex = statuses.Count == 0 ? 1 : statuses.Max(s => s.OrderIndex) + 1,
                        IsInitial = !statuses.Any(s => s.IsInitial) && category == "ToDo" };
                    statuses.Add(status); db.WorkflowStatuses.Add(status);
                }
                sequence.Add(status);
            }
            var ordered = sequence.Concat(statuses.Where(s => !sequence.Contains(s))).ToList();
            for (var i = 0; i < ordered.Count; i++) ordered[i].OrderIndex = i + 1;
            for (var i = 0; i < sequence.Count - 1; i++)
            {
                var from = sequence[i]; var to = sequence[i + 1];
                if (!transitions.Any(t => t.FromStatusId == from.Id && t.ToStatusId == to.Id))
                    db.WorkflowTransitions.Add(new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = projectId,
                        FromStatusId = from.Id, ToStatusId = to.Id, Name = $"{from.Name} → {to.Name}", RequiredPermissionCode = "issue.transition" });
            }
            // Persist statuses first for FK constraints, then make new statuses visible on existing boards.
            await db.SaveChangesAsync();
            var boards = await db.Boards.Where(b => b.ProjectId == projectId).ToListAsync();
            foreach (var board in boards)
            {
                var columns = await db.BoardColumns.Where(c => c.BoardId == board.Id).ToListAsync();
                foreach (var status in sequence.Where(s => !columns.Any(c => c.StatusId == s.Id)))
                {
                    var column = new BoardColumn { Id = Guid.NewGuid(), BoardId = board.Id, StatusId = status.Id, Name = status.Name };
                    columns.Add(column); db.BoardColumns.Add(column);
                }
                var orderedColumns = sequence.Select(s => columns.Single(c => c.StatusId == s.Id))
                    .Concat(columns.Where(c => !sequence.Any(s => s.Id == c.StatusId)).OrderBy(c => c.OrderIndex)).ToList();
                for (var i = 0; i < orderedColumns.Count; i++) orderedColumns[i].OrderIndex = i + 1;
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ordered.Select(s => new { s.Id, s.Name, s.Category, s.IsInitial, s.OrderIndex }));
        });
    }
}
