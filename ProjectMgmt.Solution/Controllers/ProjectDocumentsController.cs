using System.ComponentModel.DataAnnotations;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Security;
using Planning.Infrastructure.Services;

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

public sealed class CreateFolderRequest
{
    public Guid? ParentId { get; set; }
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
}

public sealed class CreateTextFileRequest
{
    public Guid? ParentId { get; set; }
    [Required, StringLength(150, MinimumLength = 1)] public string Name { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class RenameItemRequest
{
    [Required, StringLength(150, MinimumLength = 1)] public string NewName { get; set; } = "";
}

public sealed class MoveDocumentRequest
{
    public Guid? NewParentId { get; set; }
}

public sealed class UpdateTextContentRequest
{
    [Required] public string Content { get; set; } = "";
}

public sealed class SpecTreeNodeDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public string? Extension { get; set; }
    public long FileSize { get; set; }
    public string Status { get; set; } = "Draft";
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<SpecTreeNodeDto> Children { get; set; } = [];
}

[Route("api")]
[TypeFilter(typeof(ProjectAuthorizationFilter))]
public class ProjectDocumentsController(
    PlanningDbContext planning,
    DeliveryIntelligenceDbContext delivery,
    IUserLookupService userLookup,
    ISpecHubStorageService specStorage,
    ProjectAccess access) : ApiControllerBase
{
    private async Task<(string OrgName, string ProjName)> GetOrgAndProjectNamesAsync(Guid projectId)
    {
        var project = await planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return ("To_chuc", "Du_an");
        var org = await planning.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == project.OrgId);
        return (org?.Name ?? "To_chuc", project.Name);
    }

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
            d.ParentId,
            Name = string.IsNullOrEmpty(d.Name) ? d.Title : d.Name,
            d.RelativePath,
            d.IsDirectory,
            d.Extension,
            d.FileSize,
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

    [HttpGet("projects/{projectId:guid}/documents/tree")]
    public async Task<IActionResult> GetDocumentTree(Guid projectId)
    {
        if (!await access.AllowsAsync(projectId)) return Forbid();

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(projectId);
        await specStorage.EnsureProjectFolderAsync(orgName, projName);

        var docs = await planning.ProjectDocuments.AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => !d.IsDirectory)
            .ThenBy(d => d.Name)
            .ToListAsync();

        var users = await userLookup.GetAllActiveDisplayInfoAsync();
        var userDict = users.ToDictionary(u => u.UserId, u => u.DisplayName);

        var nodeDict = docs.ToDictionary(d => d.Id, d => new SpecTreeNodeDto
        {
            Id = d.Id,
            ParentId = d.ParentId,
            Name = string.IsNullOrEmpty(d.Name) ? d.Title : d.Name,
            Title = d.Title,
            RelativePath = d.RelativePath,
            IsDirectory = d.IsDirectory,
            Extension = d.Extension,
            FileSize = d.FileSize,
            Status = d.Status,
            Version = d.Version,
            CreatedByUserId = d.CreatedByUserId,
            CreatedByName = userDict.GetValueOrDefault(d.CreatedByUserId, "Thành viên"),
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            Children = []
        });

        var rootNodes = new List<SpecTreeNodeDto>();
        foreach (var node in nodeDict.Values)
        {
            if (node.ParentId.HasValue && nodeDict.TryGetValue(node.ParentId.Value, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        return Ok(rootNodes);
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
            doc.ParentId,
            Name = string.IsNullOrEmpty(doc.Name) ? doc.Title : doc.Name,
            doc.RelativePath,
            doc.IsDirectory,
            doc.Extension,
            doc.FileSize,
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

    [HttpGet("documents/{id:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(Guid id)
    {
        var doc = await planning.ProjectDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId)) return Forbid();
        if (doc.IsDirectory) return BadRequest(new { title = "Thư mục không có nội dung văn bản." });

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(doc.ProjectId);
        var ext = (doc.Extension ?? Path.GetExtension(doc.Name)).ToLowerInvariant();

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

        object? previewData = null;
        string fileType = "text";
        string textContent = doc.Content ?? "";

        try
        {
            if (ext == ".docx")
            {
                fileType = "docx";
                previewData = await specStorage.ExtractDocxPreviewAsync(orgName, projName, doc.RelativePath);
            }
            else if (ext == ".xlsx")
            {
                fileType = "xlsx";
                previewData = await specStorage.ExtractXlsxPreviewAsync(orgName, projName, doc.RelativePath);
            }
            else if (ext == ".pdf")
            {
                fileType = "pdf";
            }
            else if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp")
            {
                fileType = "image";
            }
            else if (ext == ".md")
            {
                fileType = "markdown";
                if (!string.IsNullOrEmpty(doc.RelativePath))
                {
                    try { textContent = await specStorage.ReadTextFileAsync(orgName, projName, doc.RelativePath); } catch { }
                }
            }
            else
            {
                fileType = "text";
                if (!string.IsNullOrEmpty(doc.RelativePath))
                {
                    try
                    {
                        textContent = await specStorage.ReadTextFileAsync(orgName, projName, doc.RelativePath);
                    }
                    catch
                    {
                        // Fallback to DB Content
                    }
                }
            }
        }
        catch (Exception ex)
        {
            textContent = $"Lỗi đọc tệp tin: {ex.Message}";
        }

        return Ok(new
        {
            doc.Id,
            doc.ProjectId,
            Name = string.IsNullOrEmpty(doc.Name) ? doc.Title : doc.Name,
            doc.Title,
            doc.RelativePath,
            doc.Extension,
            doc.FileSize,
            FileType = fileType,
            Content = textContent,
            PreviewData = previewData,
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

    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var doc = await planning.ProjectDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId)) return Forbid();
        if (doc.IsDirectory) return BadRequest(new { title = "Không thể tải xuống thư mục trực tiếp." });

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(doc.ProjectId);
        try
        {
            var (stream, contentType, fileName) = await specStorage.GetFileStreamAsync(orgName, projName, doc.RelativePath);
            return File(stream, contentType, string.IsNullOrEmpty(doc.Name) ? fileName : doc.Name);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { title = "Tệp tin vật lý không tồn tại trên hệ thống lưu trữ." });
        }
    }

    [HttpGet("documents/{id:guid}/raw")]
    public async Task<IActionResult> GetRawDocumentStream(Guid id)
    {
        var doc = await planning.ProjectDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId)) return Forbid();
        if (doc.IsDirectory) return BadRequest(new { title = "Thư mục không thể xem thô." });

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(doc.ProjectId);
        try
        {
            var (stream, contentType, _) = await specStorage.GetFileStreamAsync(orgName, projName, doc.RelativePath);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { title = "Tệp tin vật lý không tồn tại trên máy chủ." });
        }
    }

    [HttpPost("projects/{projectId:guid}/documents/folders")]
    public async Task<IActionResult> CreateFolder(Guid projectId, [FromBody] CreateFolderRequest request)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage") && !await access.AllowsAsync(projectId, "issue.create"))
            return Forbid();

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(projectId);
        string parentRelPath = "";

        if (request.ParentId.HasValue)
        {
            var parent = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == request.ParentId.Value && d.ProjectId == projectId);
            if (parent is null || !parent.IsDirectory) return BadRequest(new { title = "Thư mục cha không hợp lệ." });
            parentRelPath = parent.RelativePath;
        }

        var safeFolderName = SpecHubStorageService.SanitizeName(request.Name);
        var relPath = string.IsNullOrEmpty(parentRelPath) ? safeFolderName : Path.Combine(parentRelPath, safeFolderName).Replace('\\', '/');

        await specStorage.CreateFolderAsync(orgName, projName, relPath);

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            Title = request.Name.Trim(),
            RelativePath = relPath,
            IsDirectory = true,
            Status = "Draft",
            Version = 1,
            CreatedByUserId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };

        planning.ProjectDocuments.Add(doc);
        await planning.SaveChangesAsync();

        return StatusCode(201, new
        {
            doc.Id,
            doc.ParentId,
            doc.Name,
            doc.RelativePath,
            doc.IsDirectory,
            doc.CreatedAt
        });
    }

    [HttpPost("projects/{projectId:guid}/documents/files/text")]
    public async Task<IActionResult> CreateTextFile(Guid projectId, [FromBody] CreateTextFileRequest request)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage") && !await access.AllowsAsync(projectId, "issue.create"))
            return Forbid();

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(projectId);
        string parentRelPath = "";

        if (request.ParentId.HasValue)
        {
            var parent = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == request.ParentId.Value && d.ProjectId == projectId);
            if (parent is null || !parent.IsDirectory) return BadRequest(new { title = "Thư mục cha không hợp lệ." });
            parentRelPath = parent.RelativePath;
        }

        var fileName = request.Name.Trim();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            fileName += ".txt";
            ext = ".txt";
        }

        var safeFileName = SpecHubStorageService.SanitizeName(fileName);
        var relPath = string.IsNullOrEmpty(parentRelPath) ? safeFileName : Path.Combine(parentRelPath, safeFileName).Replace('\\', '/');

        await specStorage.WriteTextFileAsync(orgName, projName, relPath, request.Content);

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParentId = request.ParentId,
            Name = fileName,
            Title = Path.GetFileNameWithoutExtension(fileName),
            Content = request.Content,
            RelativePath = relPath,
            IsDirectory = false,
            Extension = ext,
            FileSize = System.Text.Encoding.UTF8.GetByteCount(request.Content),
            Status = "Draft",
            Version = 1,
            CreatedByUserId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };

        planning.ProjectDocuments.Add(doc);
        await planning.SaveChangesAsync();

        return StatusCode(201, new
        {
            doc.Id,
            doc.ParentId,
            doc.Name,
            doc.Title,
            doc.RelativePath,
            doc.IsDirectory,
            doc.Extension,
            doc.FileSize,
            doc.CreatedAt
        });
    }

    [HttpPost("projects/{projectId:guid}/documents/files/upload")]
    [RequestSizeLimit(50_000_000)] // 50MB
    public async Task<IActionResult> UploadFile(Guid projectId, [FromForm] IFormFile file, [FromForm] Guid? parentId)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage") && !await access.AllowsAsync(projectId, "issue.create"))
            return Forbid();

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { title = "Tệp tin không được để trống." });
        }

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(projectId);
        string parentRelPath = "";

        if (parentId.HasValue)
        {
            var parent = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == parentId.Value && d.ProjectId == projectId);
            if (parent is null || !parent.IsDirectory) return BadRequest(new { title = "Thư mục cha không hợp lệ." });
            parentRelPath = parent.RelativePath;
        }

        var fileName = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var allowedExts = new[] { ".txt", ".md", ".pdf", ".docx", ".xlsx", ".json", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp" };

        if (!allowedExts.Contains(ext))
        {
            return BadRequest(new { title = $"Định dạng tệp {ext} không được hỗ trợ trong Spec Hub." });
        }

        var safeFileName = SpecHubStorageService.SanitizeName(fileName);
        var relPath = string.IsNullOrEmpty(parentRelPath) ? safeFileName : Path.Combine(parentRelPath, safeFileName).Replace('\\', '/');

        await using var stream = file.OpenReadStream();
        var (savedPath, fileSize) = await specStorage.SaveUploadedFileAsync(orgName, projName, relPath, stream);

        string initialText = "";
        if (ext == ".txt" || ext == ".md")
        {
            try
            {
                initialText = await specStorage.ReadTextFileAsync(orgName, projName, relPath);
            }
            catch { }
        }

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParentId = parentId,
            Name = fileName,
            Title = Path.GetFileNameWithoutExtension(fileName),
            Content = initialText,
            RelativePath = savedPath,
            IsDirectory = false,
            Extension = ext,
            FileSize = fileSize,
            Status = "Draft",
            Version = 1,
            CreatedByUserId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };

        planning.ProjectDocuments.Add(doc);
        await planning.SaveChangesAsync();

        return StatusCode(201, new
        {
            doc.Id,
            doc.ParentId,
            doc.Name,
            doc.Title,
            doc.RelativePath,
            doc.IsDirectory,
            doc.Extension,
            doc.FileSize,
            doc.CreatedAt
        });
    }

    [HttpPut("documents/{id:guid}/move")]
    public async Task<IActionResult> MoveDocument(Guid id, [FromBody] MoveDocumentRequest request)
    {
        var doc = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound(new { title = "Không tìm thấy tệp hoặc thư mục." });
        if (!await access.AllowsAsync(doc.ProjectId, "project.settings.manage") && !await access.AllowsAsync(doc.ProjectId, "issue.update"))
            return Forbid();

        if (request.NewParentId.HasValue)
        {
            if (request.NewParentId.Value == doc.Id)
                return BadRequest(new { title = "Không thể chuyển mục vào chính nó." });

            var targetParent = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == request.NewParentId.Value && d.ProjectId == doc.ProjectId);
            if (targetParent is null || !targetParent.IsDirectory)
            {
                return BadRequest(new { title = "Thư mục đích không tồn tại hoặc không phải là thư mục." });
            }

            var curr = targetParent;
            while (curr?.ParentId != null)
            {
                if (curr.ParentId == doc.Id)
                {
                    return BadRequest(new { title = "Không thể chuyển thư mục cha vào thư mục con của chính nó." });
                }
                curr = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == curr.ParentId.Value && d.ProjectId == doc.ProjectId);
            }
        }

        doc.ParentId = request.NewParentId;
        doc.UpdatedAt = DateTime.UtcNow;
        await planning.SaveChangesAsync();

        return Ok(new { success = true, id = doc.Id, parentId = doc.ParentId });
    }

    [HttpPost("projects/{projectId:guid}/documents/files/upload-multiple")]
    [RequestSizeLimit(100_000_000)] // 100MB
    public async Task<IActionResult> UploadMultipleFiles(
        Guid projectId,
        [FromForm] List<IFormFile> files,
        [FromForm] Guid? parentId,
        [FromForm] List<string>? relativePaths)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage") && !await access.AllowsAsync(projectId, "issue.create"))
            return Forbid();

        if (files == null || files.Count == 0)
        {
            return BadRequest(new { title = "Không có tệp tin nào được gửi lên." });
        }

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(projectId);
        string parentRelPath = "";

        if (parentId.HasValue)
        {
            var parent = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == parentId.Value && d.ProjectId == projectId);
            if (parent != null && parent.IsDirectory)
            {
                parentRelPath = parent.RelativePath;
            }
        }

        var allowedExts = new[] { ".txt", ".md", ".pdf", ".docx", ".xlsx", ".json", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp" };
        var createdDocs = new List<object>();
        var folderCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length == 0) continue;

            var relPathGiven = (relativePaths != null && i < relativePaths.Count) ? relativePaths[i] : file.FileName;
            var parts = relPathGiven.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            var currentParentId = parentId;
            var accumulatedPath = parentRelPath;

            if (parts.Length > 1)
            {
                for (var p = 0; p < parts.Length - 1; p++)
                {
                    var folderName = parts[p];
                    var subFolderPath = string.IsNullOrEmpty(accumulatedPath) ? folderName : $"{accumulatedPath}/{folderName}";

                    if (!folderCache.TryGetValue(subFolderPath, out var existingFolderId))
                    {
                        var dbFolder = await planning.ProjectDocuments.FirstOrDefaultAsync(d => d.ProjectId == projectId && d.ParentId == currentParentId && d.Name == folderName && d.IsDirectory);
                        if (dbFolder == null)
                        {
                            await specStorage.CreateFolderAsync(orgName, projName, subFolderPath);
                            dbFolder = new ProjectDocument
                            {
                                Id = Guid.NewGuid(),
                                ProjectId = projectId,
                                ParentId = currentParentId,
                                Name = folderName,
                                Title = folderName,
                                RelativePath = subFolderPath,
                                IsDirectory = true,
                                Status = "Draft",
                                Version = 1,
                                CreatedByUserId = access.UserId,
                                CreatedAt = DateTime.UtcNow
                            };
                            planning.ProjectDocuments.Add(dbFolder);
                            await planning.SaveChangesAsync();
                        }
                        folderCache[subFolderPath] = dbFolder.Id;
                        currentParentId = dbFolder.Id;
                    }
                    else
                    {
                        currentParentId = existingFolderId;
                    }
                    accumulatedPath = subFolderPath;
                }
            }

            var fileName = parts[^1];
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext)) continue;

            var safeFileName = SpecHubStorageService.SanitizeName(fileName);
            var fileRelPath = string.IsNullOrEmpty(accumulatedPath) ? safeFileName : $"{accumulatedPath}/{safeFileName}";

            await using var stream = file.OpenReadStream();
            var (savedPath, fileSize) = await specStorage.SaveUploadedFileAsync(orgName, projName, fileRelPath, stream);

            string initialText = "";
            if (ext == ".txt" || ext == ".md")
            {
                try { initialText = await specStorage.ReadTextFileAsync(orgName, projName, fileRelPath); } catch { }
            }

            var doc = new ProjectDocument
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = currentParentId,
                Name = fileName,
                Title = Path.GetFileNameWithoutExtension(fileName),
                Content = initialText,
                RelativePath = savedPath,
                IsDirectory = false,
                Extension = ext,
                FileSize = fileSize,
                Status = "Draft",
                Version = 1,
                CreatedByUserId = access.UserId,
                CreatedAt = DateTime.UtcNow
            };

            planning.ProjectDocuments.Add(doc);
            await planning.SaveChangesAsync();

            createdDocs.Add(new
            {
                doc.Id,
                doc.ParentId,
                doc.Name,
                doc.Title,
                doc.RelativePath,
                doc.Extension,
                doc.FileSize
            });
        }

        return Ok(new { success = true, uploadedCount = createdDocs.Count, items = createdDocs });
    }

    [HttpPost("projects/{projectId:guid}/documents")]
    public async Task<IActionResult> CreateDocument(Guid projectId, [FromBody] SaveDocumentRequest request)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage") && !await access.AllowsAsync(projectId, "issue.create"))
            return Forbid();

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(projectId);
        var fileName = SpecHubStorageService.SanitizeName(request.Title.Trim()) + ".txt";
        var relPath = fileName;

        await specStorage.WriteTextFileAsync(orgName, projName, relPath, request.Content);

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = fileName,
            Title = request.Title.Trim(),
            Content = request.Content,
            RelativePath = relPath,
            IsDirectory = false,
            Extension = ".txt",
            FileSize = System.Text.Encoding.UTF8.GetByteCount(request.Content),
            Status = "Draft",
            Version = 1,
            CreatedByUserId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };

        planning.ProjectDocuments.Add(doc);
        await planning.SaveChangesAsync();
        return StatusCode(201, new { doc.Id, doc.Title, doc.Name, doc.Status, doc.Version, doc.CreatedAt });
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

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(doc.ProjectId);
        if (!doc.IsDirectory && !string.IsNullOrEmpty(doc.RelativePath))
        {
            await specStorage.WriteTextFileAsync(orgName, projName, doc.RelativePath, request.Content);
            doc.FileSize = System.Text.Encoding.UTF8.GetByteCount(request.Content);
        }

        await planning.SaveChangesAsync();
        return Ok(new { doc.Id, doc.Title, doc.Name, doc.Status, doc.Version, doc.UpdatedAt });
    }

    [HttpPut("documents/{id:guid}/content")]
    public async Task<IActionResult> UpdateTextContent(Guid id, [FromBody] UpdateTextContentRequest request)
    {
        var doc = await planning.ProjectDocuments.SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId, "project.settings.manage") && !await access.AllowsAsync(doc.ProjectId, "issue.update"))
            return Forbid();

        if (doc.IsDirectory) return BadRequest(new { title = "Không thể ghi nội dung vào thư mục." });

        var ext = (doc.Extension ?? Path.GetExtension(doc.Name)).ToLowerInvariant();
        if (ext != ".txt" && ext != ".md" && ext != ".json")
        {
            return BadRequest(new { title = "Chỉ có thể sửa nội dung tệp văn bản (.txt, .md, .json)." });
        }

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(doc.ProjectId);
        await specStorage.WriteTextFileAsync(orgName, projName, doc.RelativePath, request.Content);

        doc.Content = request.Content;
        doc.FileSize = System.Text.Encoding.UTF8.GetByteCount(request.Content);
        doc.Version += 1;
        doc.UpdatedAt = DateTime.UtcNow;

        await planning.SaveChangesAsync();
        return Ok(new { doc.Id, doc.Name, doc.Version, doc.FileSize, doc.UpdatedAt });
    }

    [HttpPut("documents/{id:guid}/rename")]
    public async Task<IActionResult> RenameItem(Guid id, [FromBody] RenameItemRequest request)
    {
        var doc = await planning.ProjectDocuments.SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId, "project.settings.manage") && !await access.AllowsAsync(doc.ProjectId, "issue.update"))
            return Forbid();

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(doc.ProjectId);
        var oldRelPath = doc.RelativePath;

        var parentRelPath = Path.GetDirectoryName(oldRelPath)?.Replace('\\', '/') ?? "";
        var safeNewName = SpecHubStorageService.SanitizeName(request.NewName.Trim());
        var newRelPath = string.IsNullOrEmpty(parentRelPath) ? safeNewName : Path.Combine(parentRelPath, safeNewName).Replace('\\', '/');

        await specStorage.RenameAsync(orgName, projName, oldRelPath, newRelPath, doc.IsDirectory);

        doc.Name = request.NewName.Trim();
        doc.Title = Path.GetFileNameWithoutExtension(request.NewName.Trim());
        doc.RelativePath = newRelPath;
        if (!doc.IsDirectory)
        {
            doc.Extension = Path.GetExtension(request.NewName.Trim()).ToLowerInvariant();
        }
        doc.UpdatedAt = DateTime.UtcNow;

        if (doc.IsDirectory)
        {
            var childDocs = await planning.ProjectDocuments
                .Where(d => d.ProjectId == doc.ProjectId && d.RelativePath.StartsWith(oldRelPath + "/"))
                .ToListAsync();
            foreach (var child in childDocs)
            {
                child.RelativePath = newRelPath + child.RelativePath[oldRelPath.Length..];
            }
        }

        await planning.SaveChangesAsync();
        return Ok(new { doc.Id, doc.Name, doc.RelativePath, doc.UpdatedAt });
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var doc = await planning.ProjectDocuments.SingleOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (!await access.AllowsAsync(doc.ProjectId, "project.settings.manage")) return Forbid();

        var (orgName, projName) = await GetOrgAndProjectNamesAsync(doc.ProjectId);
        await specStorage.DeleteAsync(orgName, projName, doc.RelativePath, doc.IsDirectory);

        if (doc.IsDirectory)
        {
            var descendants = await planning.ProjectDocuments
                .Where(d => d.ProjectId == doc.ProjectId && (d.RelativePath.StartsWith(doc.RelativePath + "/") || d.Id == id))
                .ToListAsync();
            planning.ProjectDocuments.RemoveRange(descendants);
        }
        else
        {
            planning.ProjectDocuments.Remove(doc);
        }

        await planning.SaveChangesAsync();
        return NoContent();
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
