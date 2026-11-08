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

public sealed class UpdateBoardModeInput
{
    [Required, StringLength(40)] public string Type { get; set; } = "Scrum";
    [StringLength(150)] public string? Name { get; set; }
}

public sealed class PriorityInput
{
    [Required, StringLength(40)] public string Name { get; set; } = "";
    [Range(1, 20)] public int Level { get; set; } = 3;
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] public string ColorHex { get; set; } = "#E97F33";
    [StringLength(50)] public string? IconKey { get; set; } = "flag";
}

public sealed class ApplyPriorityTemplateInput
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
        string? templateManagementType = null;
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT StatusesJson, ManagementType FROM organizationworkflowtemplate
                WHERE IsActive = 1 AND (Id = @tid OR Code = @tid OR REPLACE(Id, 'preset-', '') = @tid)
                LIMIT 1;";
            var p = cmd.CreateParameter();
            p.ParameterName = "@tid";
            p.Value = input.TemplateId;
            cmd.Parameters.Add(p);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var json = reader.IsDBNull(0) ? null : reader.GetString(0);
                templateManagementType = reader.IsDBNull(1) ? null : reader.GetString(1);
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
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM WorkflowTransition WHERE ProjectId = {projectId}");
            db.ChangeTracker.Entries<WorkflowTransition>()
                .Where(e => e.Entity.ProjectId == projectId)
                .ToList()
                .ForEach(e => e.State = EntityState.Detached);

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

            // Đồng bộ các cột Board của dự án đúng với các trạng thái trong mẫu.
            // Nếu dự án chưa có Board, tự động tạo Board mặc định theo loại quy trình (Scrum hoặc Kanban)
            var boards = await db.Boards.Where(b => b.ProjectId == projectId).ToListAsync();
            if (boards.Count == 0)
            {
                var isKanban = string.Equals(templateManagementType, "Kanban", StringComparison.OrdinalIgnoreCase);
                var newBoard = new Board
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = isKanban ? "Bảng Kanban" : "Bảng Scrum",
                    Type = isKanban ? "Kanban" : "Scrum",
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Boards.Add(newBoard);
                boards.Add(newBoard);
            }

            foreach (var board in boards)
            {
                if (!string.IsNullOrWhiteSpace(templateManagementType))
                {
                    var isKanban = string.Equals(templateManagementType, "Kanban", StringComparison.OrdinalIgnoreCase);
                    board.Type = isKanban ? "Kanban" : "Scrum";

                    // Tránh ghi đè trùng tên UQ_Board_Name (ProjectId, Name) khi dự án có nhiều bảng
                    var targetDefaultName = isKanban ? "Bảng Kanban" : "Bảng Scrum";
                    var otherDefaultName = isKanban ? "Bảng Scrum" : "Bảng Kanban";
                    if (string.Equals(board.Name, otherDefaultName, StringComparison.OrdinalIgnoreCase) &&
                        !boards.Any(b => b.Id != board.Id && string.Equals(b.Name, targetDefaultName, StringComparison.OrdinalIgnoreCase)))
                    {
                        board.Name = targetDefaultName;
                    }
                }

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

    private static readonly (string Id, string Name, string Description, (string Name, int Level, string ColorHex, string IconKey)[] Items)[] PresetPriorityTemplates =
    [
        ("scrum-std-5", "Scrum chuẩn (5 cấp độ)", "Mẫu mặc định hệ thống với 5 cấp độ từ Highest đến Lowest",
        [
            ("Highest", 1, "#CD1317", "keyboard_double_arrow_up"),
            ("High", 2, "#E9494B", "keyboard_arrow_up"),
            ("Medium", 3, "#E97F33", "drag_handle"),
            ("Low", 4, "#2A8735", "keyboard_arrow_down"),
            ("Lowest", 5, "#57A55A", "keyboard_double_arrow_down")
        ]),
        ("agile-devops-4", "Agile / DevOps (4 cấp độ)", "Mẫu 4 cấp độ ngắn gọn: Urgent, High, Normal, Low",
        [
            ("Urgent", 1, "#E11D48", "priority_high"),
            ("High", 2, "#EA580C", "arrow_upward"),
            ("Normal", 3, "#0284C7", "drag_handle"),
            ("Low", 4, "#16A34A", "arrow_downward")
        ]),
        ("simple-3", "Tối giản (3 cấp độ)", "Mẫu 3 cấp độ căn bản: High, Medium, Low",
        [
            ("High", 1, "#DC2626", "arrow_upward"),
            ("Medium", 2, "#D97706", "drag_handle"),
            ("Low", 3, "#16A34A", "arrow_downward")
        ]),
        ("incident-support-4", "Hỗ trợ & Sự cố (4 cấp độ)", "Mẫu xử lý sự cố / ticket hỗ trợ: Blocker, Critical, Major, Minor",
        [
            ("Blocker", 1, "#991B1B", "block"),
            ("Critical", 2, "#DC2626", "warning"),
            ("Major", 3, "#D97706", "error_outline"),
            ("Minor", 4, "#2563EB", "info")
        ])
    ];

    // 1. Danh sách mức độ ưu tiên (Priorities) của dự án
    [HttpGet("priorities")]
    public async Task<IActionResult> GetPriorities(Guid projectId)
    {
        var customList = await db.Priorities.AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .OrderBy(p => p.Level)
            .ToListAsync();

        var isCustom = customList.Count > 0;
        var list = isCustom
            ? customList
            : await db.Priorities.AsNoTracking()
                .Where(p => p.ProjectId == null)
                .OrderBy(p => p.Level)
                .ToListAsync();

        return Ok(list.Select(p => new
        {
            p.Id,
            p.ProjectId,
            p.Name,
            p.Level,
            p.ColorHex,
            p.IconKey,
            IsCustom = isCustom
        }));
    }

    // 2. Danh sách mẫu mức độ ưu tiên có sẵn
    [HttpGet("priorities/templates")]
    public IActionResult GetPriorityTemplates(Guid projectId)
    {
        return Ok(PresetPriorityTemplates.Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            Items = t.Items.Select(i => new
            {
                Name = i.Name,
                Level = i.Level,
                ColorHex = i.ColorHex,
                IconKey = i.IconKey
            })
        }));
    }

    // 3. Áp dụng mẫu mức độ ưu tiên cho dự án
    [HttpPost("priorities/apply-template")]
    public async Task<IActionResult> ApplyPriorityTemplate(Guid projectId, [FromBody] ApplyPriorityTemplateInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage"))
            return Forbid();

        var targetTemplate = PresetPriorityTemplates.FirstOrDefault(t => t.Id == input.TemplateId);
        if (string.IsNullOrEmpty(targetTemplate.Id))
        {
            targetTemplate = PresetPriorityTemplates[0];
        }

        var existing = await db.Priorities.Where(p => p.ProjectId == projectId).ToListAsync();
        db.Priorities.RemoveRange(existing);

        var created = new List<Priority>();
        foreach (var item in targetTemplate.Items)
        {
            var p = new Priority
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = item.Name,
                Level = item.Level,
                ColorHex = item.ColorHex,
                IconKey = item.IconKey
            };
            db.Priorities.Add(p);
            created.Add(p);
        }

        await db.SaveChangesAsync();

        return Ok(created.OrderBy(p => p.Level).Select(p => new
        {
            p.Id,
            p.ProjectId,
            p.Name,
            p.Level,
            p.ColorHex,
            p.IconKey,
            IsCustom = true
        }));
    }

    // 4. Thêm mức độ ưu tiên mới vào dự án
    [HttpPost("priorities")]
    public async Task<IActionResult> CreatePriority(Guid projectId, [FromBody] PriorityInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage"))
            return Forbid();

        var hasCustom = await db.Priorities.AnyAsync(p => p.ProjectId == projectId);
        if (!hasCustom)
        {
            // Dự án chưa có tùy biến riêng, clone mặc định hệ thống vào dự án trước
            var defaults = await db.Priorities.AsNoTracking().Where(p => p.ProjectId == null).OrderBy(p => p.Level).ToListAsync();
            foreach (var d in defaults)
            {
                db.Priorities.Add(new Priority
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = d.Name,
                    Level = d.Level,
                    ColorHex = d.ColorHex,
                    IconKey = d.IconKey
                });
            }
        }

        var newP = new Priority
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = input.Name.Trim(),
            Level = input.Level,
            ColorHex = input.ColorHex.Trim(),
            IconKey = string.IsNullOrWhiteSpace(input.IconKey) ? "flag" : input.IconKey.Trim()
        };
        db.Priorities.Add(newP);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPriorities), new { projectId }, new
        {
            newP.Id,
            newP.ProjectId,
            newP.Name,
            newP.Level,
            newP.ColorHex,
            newP.IconKey,
            IsCustom = true
        });
    }

    // 5. Cập nhật mức độ ưu tiên của dự án
    [HttpPut("priorities/{id:guid}")]
    public async Task<IActionResult> UpdatePriority(Guid projectId, Guid id, [FromBody] PriorityInput input)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage"))
            return Forbid();

        var hasCustom = await db.Priorities.AnyAsync(p => p.ProjectId == projectId);
        if (!hasCustom)
        {
            // Dự án đang dùng system defaults, cần clone hệ thống thành bản ghi riêng của dự án
            var defaults = await db.Priorities.AsNoTracking().Where(p => p.ProjectId == null).OrderBy(p => p.Level).ToListAsync();
            Priority? targetCloned = null;
            foreach (var d in defaults)
            {
                var clone = new Priority
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = d.Id == id ? input.Name.Trim() : d.Name,
                    Level = d.Id == id ? input.Level : d.Level,
                    ColorHex = d.Id == id ? input.ColorHex.Trim() : d.ColorHex,
                    IconKey = d.Id == id ? (string.IsNullOrWhiteSpace(input.IconKey) ? "flag" : input.IconKey.Trim()) : d.IconKey
                };
                db.Priorities.Add(clone);
                if (d.Id == id) targetCloned = clone;
            }
            await db.SaveChangesAsync();
            return Ok(new
            {
                targetCloned?.Id,
                ProjectId = projectId,
                Name = input.Name.Trim(),
                Level = input.Level,
                ColorHex = input.ColorHex.Trim(),
                IconKey = input.IconKey,
                IsCustom = true
            });
        }

        var p = await db.Priorities.FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId);
        if (p is null) return NotFound(new { error = "Không tìm thấy mức ưu tiên cần cập nhật trong dự án." });

        p.Name = input.Name.Trim();
        p.Level = input.Level;
        p.ColorHex = input.ColorHex.Trim();
        p.IconKey = string.IsNullOrWhiteSpace(input.IconKey) ? "flag" : input.IconKey.Trim();

        await db.SaveChangesAsync();

        return Ok(new
        {
            p.Id,
            p.ProjectId,
            p.Name,
            p.Level,
            p.ColorHex,
            p.IconKey,
            IsCustom = true
        });
    }

    // 6. Xóa mức độ ưu tiên khỏi dự án
    [HttpDelete("priorities/{id:guid}")]
    public async Task<IActionResult> DeletePriority(Guid projectId, Guid id)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage"))
            return Forbid();

        var hasCustom = await db.Priorities.AnyAsync(p => p.ProjectId == projectId);
        if (!hasCustom)
        {
            // Clone mặc định hệ thống vào dự án ngoại trừ bản ghi bị xóa
            var defaults = await db.Priorities.AsNoTracking().Where(p => p.ProjectId == null).OrderBy(p => p.Level).ToListAsync();
            if (defaults.Count <= 1)
                return BadRequest(new { error = "Dự án phải có ít nhất 1 mức độ ưu tiên." });

            foreach (var d in defaults.Where(d => d.Id != id))
            {
                db.Priorities.Add(new Priority
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = d.Name,
                    Level = d.Level,
                    ColorHex = d.ColorHex,
                    IconKey = d.IconKey
                });
            }
            await db.SaveChangesAsync();
            return NoContent();
        }

        var count = await db.Priorities.CountAsync(p => p.ProjectId == projectId);
        if (count <= 1)
            return BadRequest(new { error = "Dự án phải có ít nhất 1 mức độ ưu tiên." });

        var p = await db.Priorities.FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId);
        if (p is null) return NotFound(new { error = "Không tìm thấy mức ưu tiên cần xóa." });

        db.Priorities.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // 7. Khôi phục về mặc định hệ thống
    [HttpPost("priorities/reset")]
    public async Task<IActionResult> ResetPriorities(Guid projectId)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage"))
            return Forbid();

        var customList = await db.Priorities.Where(p => p.ProjectId == projectId).ToListAsync();
        if (customList.Count > 0)
        {
            db.Priorities.RemoveRange(customList);
            await db.SaveChangesAsync();
        }

        var defaults = await db.Priorities.AsNoTracking()
            .Where(p => p.ProjectId == null)
            .OrderBy(p => p.Level)
            .ToListAsync();

        return Ok(defaults.Select(p => new
        {
            p.Id,
            p.ProjectId,
            p.Name,
            p.Level,
            p.ColorHex,
            p.IconKey,
            IsCustom = false
        }));
    }
}
