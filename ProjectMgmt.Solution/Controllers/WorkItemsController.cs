using System.ComponentModel.DataAnnotations;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

public sealed class WorkItemRequest
{
    public Guid? Id { get; set; }
    [Required, StringLength(500, MinimumLength = 1)] public string Title { get; set; } = "";
    [StringLength(100000)] public string? Description { get; set; }
    public string StatusName { get; set; } = "To Do";
    public string IssueType { get; set; } = "Task";
    public string Priority { get; set; } = "Medium";
    [Range(0, 9999)] public decimal StoryPoints { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public string? EpicName { get; set; }
    public string? SprintId { get; set; }
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    [Required, MaxLength(30)] public List<string> Labels { get; set; } = [];
}

public sealed class SprintRequest
{
    public Guid? Id { get; set; }
    [Required, StringLength(150, MinimumLength = 1)] public string Name { get; set; } = "";
    [StringLength(1000)] public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [Required, RegularExpression("^(future|active|completed)$")] public string Status { get; set; } = "future";
}

[Route("api/v1")]
public sealed class WorkItemsController(
    DeliveryIntelligenceDbContext delivery, PlanningDbContext planning,
    IdentityExperienceDbContext identity, ProjectAccess access) : ApiControllerBase
{
    private ObjectResult Denied() => StatusCode(403, new { message = "Bạn không có quyền thực hiện thao tác này.", code = "PROJECT_PERMISSION_DENIED" });
    private BadRequestObjectResult Invalid(string message) => BadRequest(new { message });

    [HttpGet("projects/{projectId:guid}/workspace")]
    public async Task<IActionResult> Workspace(Guid projectId)
    {
        if (!await access.AllowsAsync(projectId)) return Denied();
        var issues = await delivery.Issues.AsNoTracking().Where(i => i.ProjectId == projectId && !i.IsDeleted).OrderBy(i => i.RankOrder).ToListAsync();
        var views = await Views(issues);
        var sprints = await planning.Sprints.AsNoTracking().Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
        var statuses = await planning.WorkflowStatuses.AsNoTracking().Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
        return Ok(new { workItems = views, sprints = sprints.Select(s => SprintView(s, issues)), statuses,
            epics = issues.Where(i => views.Any(v => v.Id == i.Id && v.IssueType == "Epic")).Select(i => new { i.Id, name = i.Title, key = views.First(v => v.Id == i.Id).IssueKey, color = "indigo", i.Description, status = views.First(v => v.Id == i.Id).StatusName }) });
    }

    [HttpGet("issues/{id:guid}")]
    public async Task<IActionResult> GetIssue(Guid id)
    {
        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (issue is null) return NotFound();
        if (!await access.AllowsAsync(issue.ProjectId)) return Denied();
        return Ok((await Views([issue])).Single());
    }

    public sealed class IssueOrderRequest { [Required, MinLength(1), MaxLength(2000)] public List<Guid> OrderedIds { get; set; } = []; }
    [HttpPut("projects/{projectId:guid}/issues/order")]
    public async Task<IActionResult> Reorder(Guid projectId, IssueOrderRequest request)
    {
        if (!await access.AllowsAsync(projectId, "backlog.rank")) return Denied();
        if (request.OrderedIds.Distinct().Count() != request.OrderedIds.Count) return Invalid("Danh sách thứ tự bị trùng.");
        var issues = await delivery.Issues.Where(i => i.ProjectId == projectId && !i.IsDeleted && request.OrderedIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
        if (issues.Count != request.OrderedIds.Count) return Invalid("Công việc không thuộc dự án.");
        for (var index = 0; index < request.OrderedIds.Count; index++) issues[request.OrderedIds[index]].RankOrder = index + 1;
        await delivery.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("projects/{projectId:guid}/issues")]
    public Task<IActionResult> Create(Guid projectId, WorkItemRequest request) => Save(projectId, null, request);

    [HttpPut("issues/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, WorkItemRequest request)
    {
        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        return issue is null ? NotFound() : await Save(issue.ProjectId, id, request);
    }

    // TODO(C/B): tạo Issue và đổi trạng thái vẫn chạy luồng cũ cho tới khi Planning bàn giao IIssueNumberGenerator
    // và IWorkflowValidationService; khi đó chuyển sang IIssueCreationService và use case transition của module C.
    private Task<IActionResult> Save(Guid projectId, Guid? id, WorkItemRequest request) =>
        delivery.Database.CreateExecutionStrategy().ExecuteAsync(() => SaveCore(projectId, id, request));

    private async Task<IActionResult> SaveCore(Guid projectId, Guid? id, WorkItemRequest request)
    {
        delivery.ChangeTracker.Clear();
        if (!await access.AllowsAsync(projectId, id.HasValue ? "issue.update" : "issue.create")) return Denied();
        if (request.Id == Guid.Empty) return Invalid("Mã công việc không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.Title)) return Invalid("Tiêu đề không được để trống.");
        var issue = id.HasValue ? await delivery.Issues.SingleAsync(i => i.Id == id) : new Issue { Id = request.Id ?? Guid.NewGuid(), ProjectId = projectId, ReporterId = access.UserId, CreatedAt = DateTime.UtcNow };
        if (!id.HasValue && await delivery.Issues.AnyAsync(i => i.Id == issue.Id)) return Conflict(new { message = "Mã công việc đã tồn tại." });
        var type = await planning.IssueTypes.SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Name == request.IssueType);
        if (type is null && request.IssueType == "Sub-task") type = await planning.IssueTypes.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.IsSubtask);
        var status = await planning.WorkflowStatuses.SingleOrDefaultAsync(s => s.ProjectId == projectId && s.Name == request.StatusName);
        if (type is null || status is null) return Invalid("Loại công việc hoặc trạng thái không thuộc dự án.");
        if (type.IsSubtask != request.ParentId.HasValue) return Invalid("Subtask phải có công việc cha; các loại khác không dùng ParentId.");
        if (request.ParentId.HasValue)
        {
            var parent = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == request.ParentId && i.ProjectId == projectId && !i.IsDeleted);
            if (parent is null || parent.Id == issue.Id || parent.ParentId.HasValue) return Invalid("Công việc cha không hợp lệ.");
        }
        Guid? sprintId = Guid.TryParse(request.SprintId, out var parsedSprint) ? parsedSprint : null;
        if (sprintId.HasValue && !await planning.Sprints.AnyAsync(s => s.Id == sprintId && s.ProjectId == projectId && (s.Status != "Completed" || s.Id == issue.SprintId))) return Invalid("Sprint không hợp lệ hoặc đã hoàn thành.");
        Guid? epicId = request.EpicId;
        if (!epicId.HasValue && !string.IsNullOrWhiteSpace(request.EpicName)) epicId = await delivery.Issues.Where(i => i.ProjectId == projectId && !i.IsDeleted && i.Title == request.EpicName).Select(i => (Guid?)i.Id).FirstOrDefaultAsync();
        if (epicId.HasValue)
        {
            var epic = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == epicId && i.ProjectId == projectId && !i.IsDeleted);
            if (epic is null || epic.Id == issue.Id || !await planning.IssueTypes.AnyAsync(t => t.Id == epic.IssueTypeId && t.Name == "Epic")) return Invalid("Epic không hợp lệ.");
        }
        Guid? assignee = request.AssigneeId;
        if (!string.IsNullOrWhiteSpace(request.AssigneeName))
        {
            if (!assignee.HasValue || !await identity.UserProfiles.AnyAsync(p => p.UserId == assignee && p.DisplayName == request.AssigneeName))
            {
                var matches = await identity.UserProfiles.Where(p => p.DisplayName == request.AssigneeName).Select(p => p.UserId).Take(2).ToListAsync();
                if (matches.Count != 1) return Invalid("Chọn người xử lý bằng tài khoản thành viên hợp lệ.");
                assignee = matches[0];
            }
        }
        else assignee = null;
        if (assignee.HasValue && !await accessFor(assignee.Value, projectId)) return Invalid("Người xử lý chưa là thành viên dự án.");
        var changingStatus = id.HasValue && issue.StatusId != status.Id;
        if (changingStatus)
        {
            if (!await access.AllowsAsync(projectId, "issue.transition")) return Denied();
            var transition = await planning.WorkflowTransitions.SingleOrDefaultAsync(t => t.ProjectId == projectId && t.FromStatusId == issue.StatusId && t.ToStatusId == status.Id);
            if (transition is null) return Invalid("Workflow không cho phép chuyển trạng thái này.");
            if (!string.IsNullOrWhiteSpace(transition.RequiredPermissionCode) && !await access.AllowsAsync(projectId, transition.RequiredPermissionCode)) return Denied();
        }
        if (issue.AssigneeId != assignee && (id.HasValue || assignee != access.UserId) && !await access.AllowsAsync(projectId, "issue.assign")) return Denied();
        var priorityName = request.Priority == "Urgent" ? "Highest" : request.Priority;
        var priority = await planning.Priorities.AsNoTracking().FirstOrDefaultAsync(p => p.Name == request.Priority || p.Name == priorityName);
        if (priority is null) return Invalid("Độ ưu tiên không hợp lệ.");
        if (request.Labels.Count > 30 || request.Labels.Any(l => string.IsNullOrWhiteSpace(l) || l.Length > 100)) return Invalid("Nhãn không hợp lệ.");
        var previousStatus = issue.StatusId;
        var previousCategory = changingStatus ? await planning.WorkflowStatuses.Where(s => s.Id == previousStatus).Select(s => s.Category).SingleAsync() : null;
        await using var transaction = await delivery.Database.BeginTransactionAsync();
        if (!id.HasValue)
        {
            await delivery.Database.ExecuteSqlInterpolatedAsync($"UPDATE `Project` SET `IssueCounter` = `IssueCounter` + 1 WHERE `Id` = {projectId}");
            issue.IssueNumber = await delivery.Database.SqlQuery<int>($"SELECT `IssueCounter` AS `Value` FROM `Project` WHERE `Id` = {projectId}").SingleAsync();
            issue.RankOrder = issue.IssueNumber;
            delivery.Issues.Add(issue);
        }
        issue.Title = request.Title.Trim(); issue.Description = request.Description; issue.StoryPoints = request.StoryPoints;
        issue.StatusId = status.Id; issue.IssueTypeId = type.Id; issue.PriorityId = priority.Id;
        issue.ParentId = request.ParentId; issue.EpicId = epicId; issue.SprintId = sprintId; issue.AssigneeId = assignee; issue.UpdatedAt = DateTime.UtcNow;
        issue.ResolvedAt = status.Category == "Done" ? issue.ResolvedAt ?? DateTime.UtcNow : null;
        if (status.Category == "In Progress") issue.StartedAt ??= DateTime.UtcNow;
        delivery.ActivityLogs.Add(new ActivityLog { Id = Guid.NewGuid(), IssueId = issue.Id, UserId = access.UserId, Action = id.HasValue ? "Updated" : "Created", FieldName = changingStatus ? "Status" : null, OldValue = changingStatus ? previousCategory : null, NewValue = changingStatus ? status.Name : null, Source = "User", CreatedAt = DateTime.UtcNow });
        if (changingStatus) delivery.IssueStatusHistories.Add(new IssueStatusHistory { Id = Guid.NewGuid(), IssueId = issue.Id, FromStatusId = previousStatus, ToStatusId = status.Id, FromCategory = previousCategory, ToCategory = status.Category, ChangedBy = access.UserId, ChangedAt = DateTime.UtcNow });
        await delivery.SaveChangesAsync();
        var links = await delivery.IssueLabels.Where(l => l.IssueId == issue.Id).ToListAsync();
        var requestedLabels = request.Labels.Select(l => l.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingNames = await delivery.Labels.Where(l => links.Select(link => link.LabelId).Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
        delivery.IssueLabels.RemoveRange(links.Where(link => !requestedLabels.Contains(existingNames[link.LabelId], StringComparer.OrdinalIgnoreCase)));
        foreach (var name in requestedLabels)
        {
            var label = await delivery.Labels.SingleOrDefaultAsync(l => l.ProjectId == projectId && l.Name == name);
            if (label is null) { label = new Label { Id = Guid.NewGuid(), ProjectId = projectId, Name = name, ColorHex = "#DFE1E6" }; delivery.Labels.Add(label); }
            if (!links.Any(link => link.LabelId == label.Id)) delivery.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = label.Id });
        }
        await delivery.SaveChangesAsync(); await transaction.CommitAsync();
        return Ok((await Views([issue])).Single());
    }

    private Task<bool> accessFor(Guid userId, Guid projectId) => HttpContext.RequestServices.GetRequiredService<ProjectMgmt.IdentityAccess.Contracts.IPermissionEvaluator>().IsProjectMemberAsync(userId, projectId);

    [HttpDelete("issues/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] IIssueTrackingService issues)
    {
        var issue = await issues.GetAsync(id);
        if (issue.IsFailure) return NotFound();
        if (!await access.AllowsAsync(issue.Value.ProjectId, "issue.delete")) return Denied();
        var deleted = await issues.DeleteAsync(id, access.UserId);
        return deleted.IsSuccess ? NoContent() : NotFound();
    }

    public sealed class CommentRequest
    {
        [Required, StringLength(20000, MinimumLength = 1)] public string Content { get; set; } = "";
        public Guid? ParentCommentId { get; set; }
        [MaxLength(20)] public List<Guid> MentionedUserIds { get; set; } = [];
    }

    [HttpPost("issues/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, CommentRequest request, [FromServices] IIssueCollaborationService collaboration)
    {
        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (issue is null) return NotFound();
        if (!await access.AllowsAsync(issue.ProjectId, "issue.update")) return Denied();
        var added = await collaboration.AddCommentAsync(id, new DeliveryIntelligence.Application.Dto.CreateCommentDto
        {
            Content = request.Content, ParentCommentId = request.ParentCommentId, MentionedUserIds = request.MentionedUserIds
        }, access.UserId);
        if (added.IsFailure) return Invalid(added.Error.Description);
        return Ok((await Views([issue])).Single());
    }

    [HttpPost("projects/{projectId:guid}/sprints")]
    public Task<IActionResult> CreateSprint(Guid projectId, SprintRequest request) => SaveSprint(projectId, null, request);
    [HttpPut("sprints/{id:guid}")]
    public async Task<IActionResult> UpdateSprint(Guid id, SprintRequest request)
    {
        var sprint = await planning.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
        return sprint is null ? NotFound() : await SaveSprint(sprint.ProjectId, id, request);
    }
    private async Task<IActionResult> SaveSprint(Guid projectId, Guid? id, SprintRequest request)
    {
        if (!await access.AllowsAsync(projectId, "sprint.create")) return Denied();
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndDate < request.StartDate) return Invalid("Tên hoặc ngày Sprint không hợp lệ.");
        var sprint = id.HasValue ? await planning.Sprints.SingleAsync(s => s.Id == id) : new Planning.Domain.Entities.Sprint { Id = request.Id ?? Guid.NewGuid(), ProjectId = projectId, CreatedAt = DateTime.UtcNow, OrderIndex = await planning.Sprints.CountAsync(s => s.ProjectId == projectId) };
        var status = request.Status switch { "active" => "Active", "completed" => "Completed", _ => "Planned" };
        if (sprint.Status == "Completed" && status != "Completed") return Invalid("Sprint đã hoàn thành không thể mở lại.");
        if (status != sprint.Status && status != "Planned" && !await access.AllowsAsync(projectId, status == "Active" ? "sprint.start" : "sprint.close")) return Denied();
        if (status == "Active" && await planning.Sprints.AnyAsync(s => s.ProjectId == projectId && s.Id != sprint.Id && s.Status == "Active")) return Conflict(new { message = "Dự án đã có Sprint đang chạy." });
        sprint.Name = request.Name.Trim(); sprint.Goal = request.Goal; sprint.StartDate = request.StartDate; sprint.EndDate = request.EndDate;
        sprint.Status = status; sprint.UpdatedAt = DateTime.UtcNow;
        if (status == "Active") sprint.ActualStartAt ??= DateTime.UtcNow;
        if (status == "Completed") sprint.ActualCompleteAt ??= DateTime.UtcNow;
        if (!id.HasValue) planning.Sprints.Add(sprint);
        await planning.SaveChangesAsync();
        return Ok(SprintView(sprint, await delivery.Issues.AsNoTracking().Where(i => i.SprintId == sprint.Id && !i.IsDeleted).ToListAsync()));
    }

    private static object SprintView(Planning.Domain.Entities.Sprint s, List<Issue> issues) => new { s.Id, s.ProjectId, s.Name, s.Goal, s.StartDate, s.EndDate, status = s.Status == "Active" ? "active" : s.Status == "Completed" ? "completed" : "future", workItemsCount = issues.Count(i => i.SprintId == s.Id), totalStoryPoints = issues.Where(i => i.SprintId == s.Id).Sum(i => i.StoryPoints ?? 0) };

    [HttpPost("issues/{id:guid}/attachments")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, IFormFile file, [FromServices] IIssueCollaborationService collaboration)
    {
        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (issue is null) return NotFound();
        if (!await access.AllowsAsync(issue.ProjectId, "issue.update")) return Denied();
        await using var content = file.OpenReadStream();
        var uploaded = await collaboration.UploadAttachmentAsync(id, file.FileName, file.Length, content, access.UserId);
        if (uploaded.IsFailure) return Invalid(uploaded.Error.Description);
        var attachment = uploaded.Value;
        return Ok(new { attachment.Id, attachment.FileName, attachment.FileSizeBytes, attachment.CreatedAt });
    }
    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, [FromServices] IIssueCollaborationService collaboration)
    {
        var issueId = await collaboration.GetAttachmentIssueIdAsync(id);
        if (issueId.IsFailure) return NotFound();
        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == issueId.Value && !i.IsDeleted);
        if (issue is null) return NotFound();
        if (!await access.AllowsAsync(issue.ProjectId)) return Denied();
        var opened = await collaboration.OpenAttachmentAsync(id);
        return opened.IsSuccess ? File(opened.Value.Content, "application/octet-stream", opened.Value.Metadata.FileName) : NotFound();
    }
    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, [FromServices] IIssueCollaborationService collaboration)
    {
        var issueId = await collaboration.GetAttachmentIssueIdAsync(id);
        if (issueId.IsFailure) return NotFound();
        var issue = await delivery.Issues.AsNoTracking().SingleAsync(i => i.Id == issueId.Value);
        if (!await access.AllowsAsync(issue.ProjectId, "issue.update")) return Denied();
        var deleted = await collaboration.DeleteAttachmentAsync(id, access.UserId);
        return deleted.IsSuccess ? NoContent() : NotFound();
    }

    public sealed class CriterionRequest { [Required, StringLength(4000)] public string Content { get; set; } = ""; public bool IsMet { get; set; } }
    [HttpPost("issues/{id:guid}/criteria")]
    public async Task<IActionResult> AddCriterion(Guid id, CriterionRequest request)
    {
        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (issue is null) return NotFound();
        if (!await access.AllowsAsync(issue.ProjectId, "issue.update")) return Denied();
        if (string.IsNullOrWhiteSpace(request.Content)) return Invalid("Nhập tiêu chí nghiệm thu.");
        delivery.AcceptanceCriteria.Add(new AcceptanceCriteria { Id = Guid.NewGuid(), IssueId = id, Content = request.Content.Trim(), Source = "Manual", CreatedAt = DateTime.UtcNow });
        await delivery.SaveChangesAsync(); return Ok();
    }
    [HttpPut("criteria/{id:guid}")]
    public async Task<IActionResult> UpdateCriterion(Guid id, CriterionRequest request)
    {
        var criterion = await delivery.AcceptanceCriteria.SingleOrDefaultAsync(c => c.Id == id);
        if (criterion is null) return NotFound();
        var issue = await delivery.Issues.AsNoTracking().SingleAsync(i => i.Id == criterion.IssueId);
        if (!await access.AllowsAsync(issue.ProjectId, "issue.update")) return Denied();
        if (string.IsNullOrWhiteSpace(request.Content)) return Invalid("Nhập tiêu chí nghiệm thu.");
        criterion.Content = request.Content.Trim(); criterion.IsMet = request.IsMet; criterion.MetAt = request.IsMet ? DateTime.UtcNow : null; criterion.MetBy = request.IsMet ? access.UserId : null;
        await delivery.SaveChangesAsync(); return Ok();
    }

    private async Task<List<WorkItemView>> Views(List<Issue> issues)
    {
        var ids = issues.Select(i => i.Id).ToList(); var projectIds = issues.Select(i => i.ProjectId).Distinct().ToList();
        var projects = await planning.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var statuses = await planning.WorkflowStatuses.AsNoTracking().Where(s => projectIds.Contains(s.ProjectId)).ToDictionaryAsync(s => s.Id);
        var types = await planning.IssueTypes.AsNoTracking().Where(t => projectIds.Contains(t.ProjectId)).ToDictionaryAsync(t => t.Id);
        var priorities = await planning.Priorities.AsNoTracking().ToDictionaryAsync(p => p.Id);
        var sprints = await planning.Sprints.AsNoTracking().Where(s => projectIds.Contains(s.ProjectId)).ToDictionaryAsync(s => s.Id);
        var profiles = await identity.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId);
        var comments = await delivery.Comments.AsNoTracking().Where(c => ids.Contains(c.IssueId) && !c.IsDeleted).OrderBy(c => c.CreatedAt).ToListAsync();
        var activities = await delivery.ActivityLogs.AsNoTracking().Where(a => ids.Contains(a.IssueId)).OrderByDescending(a => a.CreatedAt).ToListAsync();
        var attachments = await delivery.Attachments.AsNoTracking().Where(a => ids.Contains(a.IssueId)).ToListAsync();
        var criteria = await delivery.AcceptanceCriteria.AsNoTracking().Where(c => ids.Contains(c.IssueId)).OrderBy(c => c.OrderIndex).ToListAsync();
        var labels = await (from link in delivery.IssueLabels join label in delivery.Labels on link.LabelId equals label.Id where ids.Contains(link.IssueId) select new { link.IssueId, label.Name }).ToListAsync();
        var epicIds = issues.Where(i => i.EpicId.HasValue).Select(i => i.EpicId!.Value).Distinct().ToList();
        var epics = await delivery.Issues.AsNoTracking().Where(i => epicIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
        string Name(Guid? userId) => userId.HasValue && profiles.TryGetValue(userId.Value, out var p) ? p.DisplayName : "";
        return issues.Select(i => new WorkItemView {
            Id = i.Id, ProjectId = i.ProjectId, IssueKey = $"{projects[i.ProjectId].ProjectKey}-{i.IssueNumber}", Title = i.Title, Description = i.Description,
            StatusId = i.StatusId, StatusName = statuses[i.StatusId].Name, IssueType = types[i.IssueTypeId].IsSubtask ? "Sub-task" : types[i.IssueTypeId].Name,
            Priority = i.PriorityId.HasValue && priorities.TryGetValue(i.PriorityId.Value, out var p) ? (p.Name == "Highest" ? "Urgent" : p.Name) : "Medium",
            StoryPoints = i.StoryPoints ?? 0, ParentId = i.ParentId, AssigneeId = i.AssigneeId, AssigneeName = Name(i.AssigneeId), ReporterName = Name(i.ReporterId),
            SprintId = i.SprintId, SprintName = i.SprintId.HasValue && sprints.TryGetValue(i.SprintId.Value, out var sp) ? sp.Name : "",
            EpicId = i.EpicId, EpicName = i.EpicId.HasValue && epics.TryGetValue(i.EpicId.Value, out var e) ? e.Title : "", EpicColor = "indigo", CreatedAt = i.CreatedAt, UpdatedAt = i.UpdatedAt,
            Labels = labels.Where(l => l.IssueId == i.Id).Select(l => l.Name).ToList(),
            Comments = comments.Where(c => c.IssueId == i.Id).Select(c => new { c.Id, authorName = Name(c.UserId), c.Content, c.CreatedAt }).ToList(),
            Activities = activities.Where(a => a.IssueId == i.Id).Select(a => new { a.Id, actorName = Name(a.UserId), action = a.Action + (a.NewValue is null ? "" : ": " + a.NewValue), timestamp = a.CreatedAt }).ToList(),
            Attachments = attachments.Where(a => a.IssueId == i.Id).Select(a => new { a.Id, a.FileName, a.FileSizeBytes, a.CreatedAt }).ToList(),
            Criteria = criteria.Where(c => c.IssueId == i.Id).Select(c => new { c.Id, c.Content, c.IsMet }).ToList()
        }).ToList();
    }
}

public sealed class WorkItemView
{
    public Guid Id { get; set; } public Guid ProjectId { get; set; } public string IssueKey { get; set; } = "";
    public string Title { get; set; } = ""; public string? Description { get; set; }
    public Guid StatusId { get; set; } public string StatusName { get; set; } = ""; public string IssueType { get; set; } = "";
    public string Priority { get; set; } = ""; public decimal StoryPoints { get; set; }
    public Guid? ParentId { get; set; } public Guid? AssigneeId { get; set; } public string AssigneeName { get; set; } = ""; public string ReporterName { get; set; } = "";
    public Guid? SprintId { get; set; } public string SprintName { get; set; } = ""; public Guid? EpicId { get; set; } public string EpicName { get; set; } = ""; public string EpicColor { get; set; } = "";
    public DateTime CreatedAt { get; set; } public DateTime? UpdatedAt { get; set; }
    public List<string> Labels { get; set; } = []; public object Comments { get; set; } = Array.Empty<object>(); public object Activities { get; set; } = Array.Empty<object>();
    public object Attachments { get; set; } = Array.Empty<object>(); public object Criteria { get; set; } = Array.Empty<object>();
}
