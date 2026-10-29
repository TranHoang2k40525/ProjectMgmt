using System.ComponentModel.DataAnnotations;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

public sealed class SaveDocumentRequest
{
    [Required, StringLength(250, MinimumLength = 1)] public string Title { get; set; } = "";
    [Required] public string Content { get; set; } = "";
}

public sealed class UpdateDocumentStatusRequest
{
    [Required, RegularExpression("^(Draft|Review|Approved)$")]
    public string Status { get; set; } = "Draft";
}

public sealed class DocumentCommentRequest
{
    public string? HighlightedText { get; set; }
    [Required, StringLength(10000, MinimumLength = 1)] public string Content { get; set; } = "";
}

public sealed class ExtractStoryRequest
{
    [Required, StringLength(500, MinimumLength = 1)] public string Title { get; set; } = "";
    public string? Excerpt { get; set; }
    public decimal StoryPoints { get; set; } = 3;
    public List<string> AcceptanceCriteria { get; set; } = [];
}

[Route("api")]
[Route("api/v1")]
[TypeFilter(typeof(ProjectAuthorizationFilter))]
public class ProjectDocumentsController(
    PlanningDbContext planning,
    DeliveryIntelligenceDbContext delivery,
    IUserLookupService userLookup,
    ProjectAccess access) : ApiControllerBase
{
    // ==========================================
    // SPEC HUB DOCUMENTS (PHASE 0)
    // ==========================================

    [HttpGet("projects/{projectId:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid projectId)
    {
        if (!await access.AllowsAsync(projectId)) return Forbid();
        var docs = await planning.ProjectDocuments.AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var users = await userLookup.GetAllActiveDisplayInfoAsync();
        var userDict = users.ToDictionary(u => u.UserId, u => u.DisplayName);

        return Ok(docs.Select(d => new
        {
            d.Id,
            d.ProjectId,
            d.Title,
            d.Status,
            d.Version,
            d.CreatedByUserId,
            CreatedByName = userDict.GetValueOrDefault(d.CreatedByUserId, "Thành viên"),
            d.ApprovedByUserId,
            ApprovedByName = d.ApprovedByUserId.HasValue ? userDict.GetValueOrDefault(d.ApprovedByUserId.Value, "") : "",
            d.CreatedAt,
            d.UpdatedAt
        }));
    }

    [HttpGet("documents/{id:guid}")]
    public async Task<IActionResult> GetDocument(Guid id)
    {
        var doc = await planning.ProjectDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId)) return Forbid();

        var users = await userLookup.GetAllActiveDisplayInfoAsync();
        var userDict = users.ToDictionary(u => u.UserId, u => u.DisplayName);

        var comments = await planning.ProjectDocumentComments.AsNoTracking()
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var links = await planning.SpecStoryLinks.AsNoTracking()
            .Where(l => l.DocumentId == id)
            .ToListAsync();

        var issueIds = links.Select(l => l.IssueId).ToList();
        var issues = await delivery.Issues.AsNoTracking()
            .Where(i => issueIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var project = await planning.Projects.AsNoTracking().SingleAsync(p => p.Id == doc.ProjectId);

        return Ok(new
        {
            doc.Id,
            doc.ProjectId,
            doc.Title,
            doc.Content,
            doc.Status,
            doc.Version,
            doc.CreatedByUserId,
            CreatedByName = userDict.GetValueOrDefault(doc.CreatedByUserId, "Thành viên"),
            doc.ApprovedByUserId,
            ApprovedByName = doc.ApprovedByUserId.HasValue ? userDict.GetValueOrDefault(doc.ApprovedByUserId.Value, "") : "",
            doc.CreatedAt,
            doc.UpdatedAt,
            Comments = comments.Select(c => new
            {
                c.Id,
                c.UserId,
                UserName = userDict.GetValueOrDefault(c.UserId, "Thành viên"),
                c.HighlightedText,
                c.Content,
                c.CreatedAt
            }),
            LinkedStories = links.Select(l => new
            {
                l.Id,
                l.IssueId,
                IssueKey = issues.TryGetValue(l.IssueId, out var iss) ? $"{project.ProjectKey}-{iss.IssueNumber}" : "",
                Title = issues.TryGetValue(l.IssueId, out var iss2) ? iss2.Title : "",
                l.Excerpt,
                l.CreatedAt
            })
        });
    }

    [HttpPost("projects/{projectId:guid}/documents")]
    public async Task<IActionResult> CreateDocument(Guid projectId, [FromBody] SaveDocumentRequest request)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage") && !await access.AllowsAsync(projectId, "issue.create"))
            return Forbid();

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Content = request.Content,
            Status = "Draft",
            Version = 1,
            CreatedByUserId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };

        planning.ProjectDocuments.Add(doc);
        await planning.SaveChangesAsync();
        return StatusCode(201, new { doc.Id, doc.Title, doc.Status, doc.Version, doc.CreatedAt });
    }

    [HttpPut("documents/{id:guid}")]
    public async Task<IActionResult> UpdateDocument(Guid id, [FromBody] SaveDocumentRequest request)
    {
        var doc = await planning.ProjectDocuments.SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId, "project.settings.manage") && !await access.AllowsAsync(doc.ProjectId, "issue.update"))
            return Forbid();

        doc.Title = request.Title.Trim();
        doc.Content = request.Content;
        doc.Version += 1;
        doc.UpdatedAt = DateTime.UtcNow;

        await planning.SaveChangesAsync();
        return Ok(new { doc.Id, doc.Title, doc.Status, doc.Version, doc.UpdatedAt });
    }

    [HttpPut("documents/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDocumentStatusRequest request)
    {
        var doc = await planning.ProjectDocuments.SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();

        // Chỉ PO / PM / Lead mới được duyệt Approved
        if (request.Status == "Approved" && !await access.AllowsAsync(doc.ProjectId, "project.settings.manage"))
            return Forbid();

        doc.Status = request.Status;
        if (request.Status == "Approved")
        {
            doc.ApprovedByUserId = access.UserId;
        }
        doc.UpdatedAt = DateTime.UtcNow;

        await planning.SaveChangesAsync();
        return Ok(new { doc.Id, doc.Status, doc.ApprovedByUserId, doc.UpdatedAt });
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var doc = await planning.ProjectDocuments.SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId, "project.settings.manage")) return Forbid();

        planning.ProjectDocuments.Remove(doc);
        await planning.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("documents/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] DocumentCommentRequest request)
    {
        var doc = await planning.ProjectDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId)) return Forbid();

        var comment = new ProjectDocumentComment
        {
            Id = Guid.NewGuid(),
            DocumentId = id,
            UserId = access.UserId,
            HighlightedText = request.HighlightedText,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        planning.ProjectDocumentComments.Add(comment);
        await planning.SaveChangesAsync();
        return StatusCode(201, comment);
    }

    [HttpPost("documents/{id:guid}/extract-story")]
    public async Task<IActionResult> ExtractStory(Guid id, [FromBody] ExtractStoryRequest request)
    {
        var doc = await planning.ProjectDocuments.SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId, "issue.create")) return Forbid();

        // Tìm loại Issue là Story của dự án
        var storyType = await planning.IssueTypes.FirstOrDefaultAsync(t => t.ProjectId == doc.ProjectId && t.Name == "Story")
            ?? await planning.IssueTypes.FirstOrDefaultAsync(t => t.ProjectId == doc.ProjectId && !t.IsSubtask && t.HierarchyLevel == 2);
        if (storyType is null) return BadRequest(new { title = "Dự án chưa cấu hình loại công việc Story." });

        // Tìm trạng thái khởi tạo ToDo
        var initStatus = await planning.WorkflowStatuses.FirstOrDefaultAsync(s => s.ProjectId == doc.ProjectId && s.IsInitial)
            ?? await planning.WorkflowStatuses.FirstOrDefaultAsync(s => s.ProjectId == doc.ProjectId && s.OrderIndex == 1);
        if (initStatus is null) return BadRequest(new { title = "Dự án chưa cấu hình trạng thái ban đầu." });

        var priority = await planning.Priorities.FirstOrDefaultAsync(p => p.Name == "Medium")
            ?? await planning.Priorities.FirstAsync();

        await using var transaction = await delivery.Database.BeginTransactionAsync();

        // Tăng IssueCounter
        await delivery.Database.ExecuteSqlInterpolatedAsync($"UPDATE `Project` SET `IssueCounter` = `IssueCounter` + 1 WHERE `Id` = {doc.ProjectId}");
        var issueNumber = await delivery.Database.SqlQuery<int>($"SELECT `IssueCounter` AS `Value` FROM `Project` WHERE `Id` = {doc.ProjectId}").SingleAsync();

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = doc.ProjectId,
            IssueNumber = issueNumber,
            RankOrder = issueNumber,
            Title = request.Title.Trim(),
            Description = request.Excerpt,
            StoryPoints = request.StoryPoints,
            StatusId = initStatus.Id,
            IssueTypeId = storyType.Id,
            PriorityId = priority.Id,
            ReporterId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };

        delivery.Issues.Add(issue);
        await delivery.SaveChangesAsync();

        // Thêm Acceptance Criteria nếu có
        for (var i = 0; i < request.AcceptanceCriteria.Count; i++)
        {
            var ac = request.AcceptanceCriteria[i].Trim();
            if (!string.IsNullOrWhiteSpace(ac))
            {
                delivery.AcceptanceCriteria.Add(new AcceptanceCriteria
                {
                    Id = Guid.NewGuid(),
                    IssueId = issue.Id,
                    Content = ac,
                    OrderIndex = i + 1,
                    IsMet = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await delivery.SaveChangesAsync();

        // Lưu liên kết hai chiều SpecStoryLink
        var link = new SpecStoryLink
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            IssueId = issue.Id,
            Excerpt = request.Excerpt,
            CreatedAt = DateTime.UtcNow
        };
        planning.SpecStoryLinks.Add(link);
        await planning.SaveChangesAsync();

        await transaction.CommitAsync();

        var project = await planning.Projects.AsNoTracking().SingleAsync(p => p.Id == doc.ProjectId);

        return StatusCode(201, new
        {
            IssueId = issue.Id,
            IssueKey = $"{project.ProjectKey}-{issue.IssueNumber}",
            issue.Title,
            issue.StoryPoints,
            LinkedExcerpt = link.Excerpt
        });
    }
}
