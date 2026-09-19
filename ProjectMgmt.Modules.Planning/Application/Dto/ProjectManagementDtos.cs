namespace ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

public class OrganizationDto
{
    public OrganizationDto()
    {
    }

    public OrganizationDto(Guid id, string name, string slug, Guid ownerId, bool isActive, DateTime createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        OwnerId = ownerId;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateOrganizationDto
{
    public CreateOrganizationDto()
    {
    }

    public CreateOrganizationDto(string name, string slug, Guid ownerId)
    {
        Name = name;
        Slug = slug;
        OwnerId = ownerId;
    }

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
}

public class ProjectDto
{
    public ProjectDto()
    {
    }

    public ProjectDto(
        Guid id,
        Guid orgId,
        string projectKey,
        string name,
        string? description,
        Guid leadUserId,
        int issueCounter,
        bool isArchived,
        bool isDeleted,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        Id = id;
        OrgId = orgId;
        ProjectKey = projectKey;
        Name = name;
        Description = description;
        LeadUserId = leadUserId;
        IssueCounter = issueCounter;
        IsArchived = isArchived;
        IsDeleted = isDeleted;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

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

public class CreateProjectRequestDto
{
    public CreateProjectRequestDto()
    {
    }

    public CreateProjectRequestDto(
        Guid orgId,
        string projectKey,
        string name,
        string? description,
        Guid leadUserId)
    {
        OrgId = orgId;
        ProjectKey = projectKey;
        Name = name;
        Description = description;
        LeadUserId = leadUserId;
    }

    public Guid OrgId { get; set; }
    public string ProjectKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LeadUserId { get; set; }
}

public class UpdateProjectRequestDto
{
    public UpdateProjectRequestDto()
    {
    }

    public UpdateProjectRequestDto(
        string name,
        string? description,
        Guid leadUserId)
    {
        Name = name;
        Description = description;
        LeadUserId = leadUserId;
    }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LeadUserId { get; set; }
}

public class WorkflowTransitionItemDto
{
    public WorkflowTransitionItemDto()
    {
    }

    public WorkflowTransitionItemDto(
        Guid id,
        Guid projectId,
        Guid fromStatusId,
        string fromStatusName,
        Guid toStatusId,
        string toStatusName,
        string? name,
        string? requiredPermissionCode)
    {
        Id = id;
        ProjectId = projectId;
        FromStatusId = fromStatusId;
        FromStatusName = fromStatusName;
        ToStatusId = toStatusId;
        ToStatusName = toStatusName;
        Name = name;
        RequiredPermissionCode = requiredPermissionCode;
    }

    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FromStatusId { get; set; }
    public string FromStatusName { get; set; } = string.Empty;
    public Guid ToStatusId { get; set; }
    public string ToStatusName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? RequiredPermissionCode { get; set; }
}

public class CreateWorkflowTransitionRequestDto
{
    public CreateWorkflowTransitionRequestDto()
    {
    }

    public CreateWorkflowTransitionRequestDto(
        Guid fromStatusId,
        Guid toStatusId,
        string? name,
        string? requiredPermissionCode)
    {
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        Name = name;
        RequiredPermissionCode = requiredPermissionCode;
    }

    public Guid FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    public string? Name { get; set; }
    public string? RequiredPermissionCode { get; set; }
}

public class UpdateWorkflowTransitionRequestDto
{
    public UpdateWorkflowTransitionRequestDto()
    {
    }

    public UpdateWorkflowTransitionRequestDto(
        string? name,
        string? requiredPermissionCode)
    {
        Name = name;
        RequiredPermissionCode = requiredPermissionCode;
    }

    public string? Name { get; set; }
    public string? RequiredPermissionCode { get; set; }
}

public class BoardDto
{
    public BoardDto()
    {
    }

    public BoardDto(
        Guid id,
        Guid projectId,
        string name,
        string type,
        bool isDefault,
        DateTime createdAt)
    {
        Id = id;
        ProjectId = projectId;
        Name = name;
        Type = type;
        IsDefault = isDefault;
        CreatedAt = createdAt;
    }

    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBoardRequestDto
{
    public CreateBoardRequestDto()
    {
    }

    public CreateBoardRequestDto(
        string name,
        string type,
        bool isDefault)
    {
        Name = name;
        Type = type;
        IsDefault = isDefault;
    }

    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Kanban";
    public bool IsDefault { get; set; }
}

public class BoardColumnDto
{
    public BoardColumnDto()
    {
    }

    public BoardColumnDto(
        Guid id,
        Guid boardId,
        Guid statusId,
        string statusName,
        string? name,
        int orderIndex,
        int? wipLimit)
    {
        Id = id;
        BoardId = boardId;
        StatusId = statusId;
        StatusName = statusName;
        Name = name;
        OrderIndex = orderIndex;
        WipLimit = wipLimit;
    }

    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Guid StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int OrderIndex { get; set; }
    public int? WipLimit { get; set; }
}

public class CreateBoardColumnRequestDto
{
    public CreateBoardColumnRequestDto()
    {
    }

    public CreateBoardColumnRequestDto(
        Guid statusId,
        string? name,
        int? wipLimit)
    {
        StatusId = statusId;
        Name = name;
        WipLimit = wipLimit;
    }

    public Guid StatusId { get; set; }
    public string? Name { get; set; }
    public int? WipLimit { get; set; }
}

public class ReorderBoardColumnsRequestDto
{
    public ReorderBoardColumnsRequestDto()
    {
    }

    public ReorderBoardColumnsRequestDto(IReadOnlyList<Guid> orderedColumnIds)
    {
        OrderedColumnIds = orderedColumnIds;
    }

    public IReadOnlyList<Guid> OrderedColumnIds { get; set; } = [];
}
