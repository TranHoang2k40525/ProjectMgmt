namespace ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Entities;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class Project
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public string ProjectKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LeadUserId { get; set; }
    public int IssueCounter { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ProjectComponent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? LeadUserId { get; set; }
    public Guid? DefaultSkillId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ProjectVersion
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public bool IsReleased { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WorkflowStatus
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#8993A4";
    public int OrderIndex { get; set; }
    public bool IsInitial { get; set; }
}

public sealed class WorkflowTransition
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    public string? Name { get; set; }
    public string? RequiredPermissionCode { get; set; }
}

public sealed class IssueType
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconKey { get; set; }
    public string ColorHex { get; set; } = "#0052CC";
    public bool IsSubtask { get; set; }
    public int HierarchyLevel { get; set; } = 1;
    public int OrderIndex { get; set; }
}

public sealed class Priority
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string ColorHex { get; set; } = "#6B778C";
    public string? IconKey { get; set; }
}

public sealed class Board
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class BoardColumn
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Guid StatusId { get; set; }
    public string? Name { get; set; }
    public int OrderIndex { get; set; }
    public int? WipLimit { get; set; }
}
