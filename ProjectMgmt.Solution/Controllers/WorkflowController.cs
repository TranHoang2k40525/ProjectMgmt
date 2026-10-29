using System.ComponentModel.DataAnnotations;
using DeliveryIntelligence.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

public sealed class WorkflowStatusInput
{
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] public string ColorHex { get; set; } = "#3B82F6";
    public string? Category { get; set; }
    public bool IsInitial { get; set; }
    public bool IsDone { get; set; }
    public bool IsActiveWork { get; set; }
    public bool IsCanceled { get; set; }
    public int? OrderIndex { get; set; }
}

public sealed class ReorderStatusesInput
{
    [Required] public List<Guid> OrderedStatusIds { get; set; } = [];
}

public sealed class CreateTransitionInput
{
    [Required] public Guid FromStatusId { get; set; }
    [Required] public Guid ToStatusId { get; set; }
    [StringLength(80)] public string? Name { get; set; }
    [StringLength(80)] public string? RequiredPermissionCode { get; set; }
}

[Route("api")]
[Route("api/v1")]
[TypeFilter(typeof(ProjectAuthorizationFilter))]
public class WorkflowController(
    IProjectManagementService service,
    PlanningDbContext db,
    DeliveryIntelligenceDbContext delivery,
    ProjectAccess access) : ApiControllerBase
{
    // ==========================================
    // STATUS CRUD & REORDER (DYNAMIC BEHAVIOR FLAGS)
    // ==========================================

    [HttpGet("projects/{projectId:guid}/statuses")]
    public async Task<IActionResult> GetStatuses(Guid projectId)
    {
        if (!await access.AllowsAsync(projectId)) return Forbid();
        var statuses = await db.WorkflowStatuses.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync();

        return Ok(statuses.Select(s => new
        {
            s.Id,
            s.ProjectId,
            s.Name,
            Category = string.IsNullOrWhiteSpace(s.Category) ? "Custom" : s.Category,
            s.ColorHex,
            s.IsInitial,
            s.IsDone,
            s.IsActiveWork,
            s.IsCanceled,
            s.OrderIndex
        }));
    }

    [HttpPost("projects/{projectId:guid}/statuses")]
    public async Task<IActionResult> CreateStatus(Guid projectId, WorkflowStatusInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var name = input.Name.Trim();
        if (name.Length == 0) return BadRequest(new { title = "Tên trạng thái không được để trống." });

        if (await db.WorkflowStatuses.AnyAsync(s => s.ProjectId == projectId && s.Name == name))
            return Conflict(new { title = "Trạng thái này đã tồn tại trong dự án." });

        var maxOrder = await db.WorkflowStatuses.Where(s => s.ProjectId == projectId).MaxAsync(s => (int?)s.OrderIndex) ?? 0;
        var category = !string.IsNullOrWhiteSpace(input.Category) ? input.Category
            : input.IsDone ? "Done" : input.IsActiveWork ? "InProgress" : input.IsCanceled ? "Canceled" : "ToDo";

        var status = new WorkflowStatus
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Category = category,
            ColorHex = input.ColorHex,
            IsInitial = input.IsInitial,
            IsDone = input.IsDone,
            IsActiveWork = input.IsActiveWork,
            IsCanceled = input.IsCanceled,
            OrderIndex = input.OrderIndex ?? (maxOrder + 1)
        };

        // Nếu đặt IsInitial = true, bỏ IsInitial của các status khác trong project
        if (status.IsInitial)
        {
            var otherInitials = await db.WorkflowStatuses.Where(s => s.ProjectId == projectId && s.IsInitial).ToListAsync();
            foreach (var o in otherInitials) o.IsInitial = false;
        }

        db.WorkflowStatuses.Add(status);

        // Tự động thêm cột vào các Board của dự án
        var boards = await db.Boards.Where(b => b.ProjectId == projectId).ToListAsync();
        foreach (var board in boards)
        {
            var colOrder = (await db.BoardColumns.Where(c => c.BoardId == board.Id).MaxAsync(c => (int?)c.OrderIndex) ?? 0) + 1;
            db.BoardColumns.Add(new BoardColumn
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                StatusId = status.Id,
                Name = status.Name,
                OrderIndex = colOrder
            });
        }

        await db.SaveChangesAsync();
        return StatusCode(201, new
        {
            status.Id,
            status.ProjectId,
            status.Name,
            status.Category,
            status.ColorHex,
            status.IsInitial,
            status.IsDone,
            status.IsActiveWork,
            status.IsCanceled,
            status.OrderIndex
        });
    }

    [HttpPut("projects/{projectId:guid}/statuses/{statusId:guid}")]
    public async Task<IActionResult> UpdateStatus(Guid projectId, Guid statusId, WorkflowStatusInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var status = await db.WorkflowStatuses.SingleOrDefaultAsync(s => s.ProjectId == projectId && s.Id == statusId);
        if (status is null) return NotFound(new { title = "Không tìm thấy trạng thái." });

        var name = input.Name.Trim();
        if (name.Length == 0) return BadRequest(new { title = "Tên trạng thái không được để trống." });

        if (await db.WorkflowStatuses.AnyAsync(s => s.ProjectId == projectId && s.Id != statusId && s.Name == name))
            return Conflict(new { title = "Trạng thái có tên này đã tồn tại trong dự án." });

        status.Name = name;
        status.ColorHex = input.ColorHex;
        status.IsDone = input.IsDone;
        status.IsActiveWork = input.IsActiveWork;
        status.IsCanceled = input.IsCanceled;
        status.Category = !string.IsNullOrWhiteSpace(input.Category) ? input.Category
            : input.IsDone ? "Done" : input.IsActiveWork ? "InProgress" : input.IsCanceled ? "Canceled" : "ToDo";

        if (input.OrderIndex.HasValue) status.OrderIndex = input.OrderIndex.Value;

        if (input.IsInitial && !status.IsInitial)
        {
            var otherInitials = await db.WorkflowStatuses.Where(s => s.ProjectId == projectId && s.Id != statusId && s.IsInitial).ToListAsync();
            foreach (var o in otherInitials) o.IsInitial = false;
            status.IsInitial = true;
        }
        else if (!input.IsInitial)
        {
            status.IsInitial = false;
        }

        // Cập nhật tên cột Board tương ứng
        var columns = await db.BoardColumns.Where(c => c.StatusId == statusId).ToListAsync();
        foreach (var col in columns) col.Name = name;

        await db.SaveChangesAsync();
        return Ok(new
        {
            status.Id,
            status.ProjectId,
            status.Name,
            status.Category,
            status.ColorHex,
            status.IsInitial,
            status.IsDone,
            status.IsActiveWork,
            status.IsCanceled,
            status.OrderIndex
        });
    }

    [HttpDelete("projects/{projectId:guid}/statuses/{statusId:guid}")]
    public async Task<IActionResult> DeleteStatus(Guid projectId, Guid statusId)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var status = await db.WorkflowStatuses.SingleOrDefaultAsync(s => s.ProjectId == projectId && s.Id == statusId);
        if (status is null) return NotFound(new { title = "Không tìm thấy trạng thái." });

        // Kiểm tra an toàn: Có task nào đang ở trạng thái này không?
        var taskCount = await delivery.Issues.CountAsync(i => i.ProjectId == projectId && i.StatusId == statusId && !i.IsDeleted);
        if (taskCount > 0)
        {
            return Conflict(new
            {
                title = $"Không thể xóa trạng thái '{status.Name}' vì đang có {taskCount} công việc ở trạng thái này. Vui lòng chuyển công việc sang trạng thái khác trước khi xóa."
            });
        }

        // Xóa các transition liên quan
        var transitions = await db.WorkflowTransitions
            .Where(t => t.ProjectId == projectId && (t.FromStatusId == statusId || t.ToStatusId == statusId))
            .ToListAsync();
        db.WorkflowTransitions.RemoveRange(transitions);

        // Xóa các cột trên board
        var columns = await db.BoardColumns.Where(c => c.StatusId == statusId).ToListAsync();
        db.BoardColumns.RemoveRange(columns);

        db.WorkflowStatuses.Remove(status);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("projects/{projectId:guid}/statuses/reorder")]
    public async Task<IActionResult> ReorderStatuses(Guid projectId, ReorderStatusesInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == projectId).ToListAsync();

        for (var i = 0; i < input.OrderedStatusIds.Count; i++)
        {
            var id = input.OrderedStatusIds[i];
            var s = statuses.FirstOrDefault(st => st.Id == id);
            if (s is not null) s.OrderIndex = i + 1;
        }

        // Đồng bộ thứ tự cột trên các Board
        var boards = await db.Boards.Where(b => b.ProjectId == projectId).ToListAsync();
        foreach (var board in boards)
        {
            var columns = await db.BoardColumns.Where(c => c.BoardId == board.Id).ToListAsync();
            for (var i = 0; i < input.OrderedStatusIds.Count; i++)
            {
                var id = input.OrderedStatusIds[i];
                var col = columns.FirstOrDefault(c => c.StatusId == id);
                if (col is not null) col.OrderIndex = i + 1;
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ==========================================
    // WORKFLOW TRANSITIONS
    // ==========================================

    [HttpPost("projects/{projectId:guid}/workflow/transitions")]
    public async Task<IActionResult> CreateTransition(Guid projectId, CreateTransitionInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        if (input.FromStatusId == input.ToStatusId) return BadRequest(new { title = "Trạng thái nguồn và đích không được trùng nhau." });

        var exists = await db.WorkflowTransitions.AnyAsync(t =>
            t.ProjectId == projectId && t.FromStatusId == input.FromStatusId && t.ToStatusId == input.ToStatusId);
        if (exists) return Conflict(new { title = "Đường chuyển trạng thái này đã tồn tại." });

        var fromStatus = await db.WorkflowStatuses.SingleOrDefaultAsync(s => s.ProjectId == projectId && s.Id == input.FromStatusId);
        var toStatus = await db.WorkflowStatuses.SingleOrDefaultAsync(s => s.ProjectId == projectId && s.Id == input.ToStatusId);
        if (fromStatus is null || toStatus is null) return BadRequest(new { title = "Trạng thái nguồn hoặc đích không hợp lệ." });

        var transition = new WorkflowTransition
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            FromStatusId = input.FromStatusId,
            ToStatusId = input.ToStatusId,
            Name = !string.IsNullOrWhiteSpace(input.Name) ? input.Name.Trim() : $"{fromStatus.Name} → {toStatus.Name}",
            RequiredPermissionCode = string.IsNullOrWhiteSpace(input.RequiredPermissionCode) ? "issue.transition" : input.RequiredPermissionCode.Trim()
        };

        db.WorkflowTransitions.Add(transition);
        await db.SaveChangesAsync();

        return StatusCode(201, new
        {
            transition.Id,
            transition.ProjectId,
            transition.FromStatusId,
            FromStatusName = fromStatus.Name,
            transition.ToStatusId,
            ToStatusName = toStatus.Name,
            transition.Name,
            transition.RequiredPermissionCode
        });
    }

    [HttpPut("workflow/transitions/{transitionId:guid}")]
    public async Task<ActionResult<WorkflowTransitionItemDto>> UpdateTransition(
        Guid transitionId,
        [FromBody] UpdateWorkflowTransitionRequestDto request) =>
        FromResult(await service.UpdateWorkflowTransitionAsync(transitionId, request));

    [HttpDelete("workflow/transitions/{transitionId:guid}")]
    public async Task<IActionResult> DeleteTransition(
        Guid transitionId) =>
        FromResult(await service.DeleteWorkflowTransitionAsync(transitionId));
}
