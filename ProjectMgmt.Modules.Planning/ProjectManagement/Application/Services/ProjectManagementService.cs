using System.Text.RegularExpressions;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using Planning.Domain.Entities;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;

public class ProjectManagementService : IProjectManagementService
{
    private static readonly Regex ProjectKeyRegex = new Regex("^[A-Z][A-Z0-9]{1,9}$", RegexOptions.Compiled);
    private readonly IProjectRepository _repository;

    public ProjectManagementService(IProjectRepository repository)
    {
        _repository = repository;
    }

    // ==========================================
    // ORGANIZATIONS
    // ==========================================

    public async Task<Result<IReadOnlyList<OrganizationDto>>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var orgs = await _repository.GetOrganizationsAsync(cancellationToken);
        if (orgs.Count == 0)
        {
            // Tự động tạo một Default Organization nếu hệ thống chưa có org nào để người dùng trải nghiệm ngay
            var defaultOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Default Organization",
                Slug = "default-org",
                OwnerId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _repository.AddOrganizationAsync(defaultOrg, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            orgs = new List<Organization> { defaultOrg };
        }

        IReadOnlyList<OrganizationDto> dtos = orgs.Select(x => new OrganizationDto(
            x.Id,
            x.Name,
            x.Slug,
            x.OwnerId,
            x.IsActive,
            x.CreatedAt)).ToList();

        return Result<IReadOnlyList<OrganizationDto>>.Success(dtos);
    }

    public async Task<Result<OrganizationDto>> CreateOrganizationAsync(CreateOrganizationDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<OrganizationDto>.Failure(Error.Validation("ORG_NAME_REQUIRED", "Organization name is required."));
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? request.Name.Trim().ToLowerInvariant().Replace(' ', '-')
            : request.Slug.Trim().ToLowerInvariant();

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            OwnerId = request.OwnerId == Guid.Empty ? Guid.Parse("11111111-0000-0000-0000-000000000001") : request.OwnerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddOrganizationAsync(org, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var dto = new OrganizationDto(org.Id, org.Name, org.Slug, org.OwnerId, org.IsActive, org.CreatedAt);
        return Result<OrganizationDto>.Success(dto);
    }

    // ==========================================
    // PROJECTS
    // ==========================================

    public async Task<Result<IReadOnlyList<ProjectDto>>> GetProjectsAsync(Guid? orgId = null, CancellationToken cancellationToken = default)
    {
        var projects = await _repository.GetAllProjectsAsync(orgId, cancellationToken);
        IReadOnlyList<ProjectDto> dtos = projects.Select(x => new ProjectDto(
            x.Id,
            x.OrgId,
            x.ProjectKey,
            x.Name,
            x.Description,
            x.LeadUserId,
            x.IssueCounter,
            x.IsArchived,
            x.IsDeleted,
            x.CreatedAt,
            x.UpdatedAt)).ToList();

        return Result<IReadOnlyList<ProjectDto>>.Success(dtos);
    }

    public async Task<Result<ProjectDto>> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(projectId, cancellationToken);
        if (project is null || project.IsDeleted)
        {
            return Result<ProjectDto>.Failure(Error.NotFound("PROJECT_NOT_FOUND", $"Project '{projectId}' was not found."));
        }

        var dto = new ProjectDto(
            project.Id,
            project.OrgId,
            project.ProjectKey,
            project.Name,
            project.Description,
            project.LeadUserId,
            project.IssueCounter,
            project.IsArchived,
            project.IsDeleted,
            project.CreatedAt,
            project.UpdatedAt);

        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result<ProjectDto>> CreateProjectAsync(CreateProjectRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ProjectDto>.Failure(Error.Validation("PROJECT_NAME_REQUIRED", "Project name is required."));
        }

        var projectKey = (request.ProjectKey ?? string.Empty).Trim().ToUpperInvariant();
        if (!ProjectKeyRegex.IsMatch(projectKey))
        {
            return Result<ProjectDto>.Failure(Error.Validation(
                "PROJECT_KEY_INVALID",
                "Project key must start with an uppercase letter and contain 2-10 alphanumeric characters (e.g. PROJ, SCRUM1)."));
        }

        var existingKey = await _repository.GetByKeyAsync(request.OrgId, projectKey, cancellationToken);
        if (existingKey is not null)
        {
            return Result<ProjectDto>.Failure(Error.Conflict(
                "PROJECT_KEY_EXISTS",
                $"Project key '{projectKey}' already exists in this organization."));
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = request.OrgId,
            ProjectKey = projectKey,
            Name = request.Name.Trim(),
            Description = request.Description,
            LeadUserId = request.LeadUserId == Guid.Empty ? Guid.Parse("11111111-0000-0000-0000-000000000001") : request.LeadUserId,
            IssueCounter = 0,
            IsArchived = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(project, cancellationToken);

        // ====================================================
        // SEED CẤU HÌNH MẶC ĐỊNH CHO PROJECT MỚI
        // 1. Issue Types: Epic, Story, Task, Bug, Subtask
        // 2. Workflow Statuses: Backlog, To Do, In Progress, In Review, Done
        // 3. Workflow Transitions: giữa các status
        // 4. Default Board (Kanban) kèm 4 Board Columns
        // ====================================================

        var issueTypes = new List<IssueType>
        {
            new IssueType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Epic", ColorHex = "#904EE2", HierarchyLevel = 3, IsSubtask = false, OrderIndex = 1 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Story", ColorHex = "#65BA43", HierarchyLevel = 2, IsSubtask = false, OrderIndex = 2 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Task", ColorHex = "#4BADE8", HierarchyLevel = 2, IsSubtask = false, OrderIndex = 3 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Bug", ColorHex = "#E5493A", HierarchyLevel = 2, IsSubtask = false, OrderIndex = 4 },
            new IssueType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Subtask", ColorHex = "#4BADE8", HierarchyLevel = 1, IsSubtask = true, OrderIndex = 5 }
        };
        await _repository.AddIssueTypesAsync(issueTypes, cancellationToken);

        var statusBacklog = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Backlog", Category = "ToDo", ColorHex = "#5E6C84", OrderIndex = 1, IsInitial = true };
        var statusToDo = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "To Do", Category = "ToDo", ColorHex = "#42526E", OrderIndex = 2, IsInitial = false };
        var statusInProgress = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "In Progress", Category = "InProgress", ColorHex = "#0052CC", OrderIndex = 3, IsInitial = false };
        var statusInReview = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "In Review", Category = "InProgress", ColorHex = "#FFAB00", OrderIndex = 4, IsInitial = false };
        var statusDone = new WorkflowStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Done", Category = "Done", ColorHex = "#36B37E", OrderIndex = 5, IsInitial = false };

        var statuses = new List<WorkflowStatus> { statusBacklog, statusToDo, statusInProgress, statusInReview, statusDone };
        await _repository.AddWorkflowStatusesAsync(statuses, cancellationToken);

        var transitions = new List<WorkflowTransition>
        {
            new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = project.Id, FromStatusId = statusBacklog.Id, ToStatusId = statusToDo.Id, Name = "Ready for Sprint" },
            new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = project.Id, FromStatusId = statusToDo.Id, ToStatusId = statusInProgress.Id, Name = "Start Work" },
            new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = project.Id, FromStatusId = statusInProgress.Id, ToStatusId = statusInReview.Id, Name = "Submit Review" },
            new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = project.Id, FromStatusId = statusInReview.Id, ToStatusId = statusDone.Id, Name = "Accept & Done" },
            new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = project.Id, FromStatusId = statusInReview.Id, ToStatusId = statusInProgress.Id, Name = "Needs Rework" },
            new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = project.Id, FromStatusId = statusInProgress.Id, ToStatusId = statusToDo.Id, Name = "Stop Work" },
            new WorkflowTransition { Id = Guid.NewGuid(), ProjectId = project.Id, FromStatusId = statusDone.Id, ToStatusId = statusInProgress.Id, Name = "Reopen" }
        };
        await _repository.AddWorkflowTransitionsAsync(transitions, cancellationToken);

        var defaultBoard = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = $"{project.ProjectKey} Board",
            Type = "Kanban",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddBoardAsync(defaultBoard, cancellationToken);

        var boardColumns = new List<BoardColumn>
        {
            new BoardColumn { Id = Guid.NewGuid(), BoardId = defaultBoard.Id, StatusId = statusToDo.Id, Name = "To Do", OrderIndex = 1, WipLimit = null },
            new BoardColumn { Id = Guid.NewGuid(), BoardId = defaultBoard.Id, StatusId = statusInProgress.Id, Name = "In Progress", OrderIndex = 2, WipLimit = 5 },
            new BoardColumn { Id = Guid.NewGuid(), BoardId = defaultBoard.Id, StatusId = statusInReview.Id, Name = "In Review", OrderIndex = 3, WipLimit = 3 },
            new BoardColumn { Id = Guid.NewGuid(), BoardId = defaultBoard.Id, StatusId = statusDone.Id, Name = "Done", OrderIndex = 4, WipLimit = null }
        };
        await _repository.AddBoardColumnsAsync(boardColumns, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        var dto = new ProjectDto(
            project.Id,
            project.OrgId,
            project.ProjectKey,
            project.Name,
            project.Description,
            project.LeadUserId,
            project.IssueCounter,
            project.IsArchived,
            project.IsDeleted,
            project.CreatedAt,
            project.UpdatedAt);

        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectRequestDto request, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(projectId, cancellationToken);
        if (project is null || project.IsDeleted)
        {
            return Result<ProjectDto>.Failure(Error.NotFound("PROJECT_NOT_FOUND", $"Project '{projectId}' was not found."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ProjectDto>.Failure(Error.Validation("PROJECT_NAME_REQUIRED", "Project name is required."));
        }

        project.Name = request.Name.Trim();
        project.Description = request.Description;
        if (request.LeadUserId != Guid.Empty)
        {
            project.LeadUserId = request.LeadUserId;
        }
        project.UpdatedAt = DateTime.UtcNow;

        _repository.UpdateProject(project);
        await _repository.SaveChangesAsync(cancellationToken);

        var dto = new ProjectDto(
            project.Id,
            project.OrgId,
            project.ProjectKey,
            project.Name,
            project.Description,
            project.LeadUserId,
            project.IssueCounter,
            project.IsArchived,
            project.IsDeleted,
            project.CreatedAt,
            project.UpdatedAt);

        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(projectId, cancellationToken);
        if (project is null || project.IsDeleted)
        {
            return Result.Failure(Error.NotFound("PROJECT_NOT_FOUND", $"Project '{projectId}' was not found."));
        }

        project.IsDeleted = true;
        project.UpdatedAt = DateTime.UtcNow;
        _repository.UpdateProject(project);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // ==========================================
    // WORKFLOW TRANSITIONS
    // ==========================================

    public async Task<Result<IReadOnlyList<WorkflowTransitionItemDto>>> GetWorkflowTransitionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var transitions = await _repository.GetTransitionsByProjectAsync(projectId, cancellationToken);
        var statuses = (await _repository.GetStatusesAsync(projectId, cancellationToken)).ToDictionary(x => x.Id, x => x.Name);

        IReadOnlyList<WorkflowTransitionItemDto> dtos = transitions.Select(t => new WorkflowTransitionItemDto(
            t.Id,
            t.ProjectId,
            t.FromStatusId,
            statuses.TryGetValue(t.FromStatusId, out var fromName) ? fromName : "Unknown",
            t.ToStatusId,
            statuses.TryGetValue(t.ToStatusId, out var toName) ? toName : "Unknown",
            t.Name,
            t.RequiredPermissionCode)).ToList();

        return Result<IReadOnlyList<WorkflowTransitionItemDto>>.Success(dtos);
    }

    public async Task<Result<WorkflowTransitionItemDto>> CreateWorkflowTransitionAsync(
        Guid projectId,
        CreateWorkflowTransitionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Chặn Self-transition
        if (request.FromStatusId == request.ToStatusId)
        {
            return Result<WorkflowTransitionItemDto>.Failure(Error.Validation(
                "WORKFLOW_SELF_TRANSITION",
                "A workflow status cannot transition to itself."));
        }

        // 2. Chặn Status khác project
        var statuses = (await _repository.GetStatusesAsync(projectId, cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        if (!statuses.ContainsKey(request.FromStatusId) || !statuses.ContainsKey(request.ToStatusId))
        {
            return Result<WorkflowTransitionItemDto>.Failure(Error.Validation(
                "WORKFLOW_STATUS_NOT_IN_PROJECT",
                "One or both statuses do not belong to this project."));
        }

        // 3. Chặn Duplicate Transition
        var existing = await _repository.GetTransitionAsync(projectId, request.FromStatusId, request.ToStatusId, cancellationToken);
        if (existing is not null)
        {
            return Result<WorkflowTransitionItemDto>.Failure(Error.Conflict(
                "WORKFLOW_DUPLICATE_TRANSITION",
                $"A transition from '{statuses[request.FromStatusId]}' to '{statuses[request.ToStatusId]}' already exists."));
        }

        var transition = new WorkflowTransition
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            FromStatusId = request.FromStatusId,
            ToStatusId = request.ToStatusId,
            Name = request.Name,
            RequiredPermissionCode = string.IsNullOrWhiteSpace(request.RequiredPermissionCode) ? null : request.RequiredPermissionCode.Trim()
        };

        await _repository.AddWorkflowTransitionAsync(transition, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var dto = new WorkflowTransitionItemDto(
            transition.Id,
            transition.ProjectId,
            transition.FromStatusId,
            statuses[transition.FromStatusId],
            transition.ToStatusId,
            statuses[transition.ToStatusId],
            transition.Name,
            transition.RequiredPermissionCode);

        return Result<WorkflowTransitionItemDto>.Success(dto);
    }

    public async Task<Result<WorkflowTransitionItemDto>> UpdateWorkflowTransitionAsync(
        Guid transitionId,
        UpdateWorkflowTransitionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var transition = await _repository.GetTransitionByIdAsync(transitionId, cancellationToken);
        if (transition is null)
        {
            return Result<WorkflowTransitionItemDto>.Failure(Error.NotFound("TRANSITION_NOT_FOUND", $"Transition '{transitionId}' was not found."));
        }

        transition.Name = request.Name;
        transition.RequiredPermissionCode = string.IsNullOrWhiteSpace(request.RequiredPermissionCode) ? null : request.RequiredPermissionCode.Trim();

        await _repository.SaveChangesAsync(cancellationToken);

        var statuses = (await _repository.GetStatusesAsync(transition.ProjectId, cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        var dto = new WorkflowTransitionItemDto(
            transition.Id,
            transition.ProjectId,
            transition.FromStatusId,
            statuses.TryGetValue(transition.FromStatusId, out var fn) ? fn : "Unknown",
            transition.ToStatusId,
            statuses.TryGetValue(transition.ToStatusId, out var tn) ? tn : "Unknown",
            transition.Name,
            transition.RequiredPermissionCode);

        return Result<WorkflowTransitionItemDto>.Success(dto);
    }

    public async Task<Result> DeleteWorkflowTransitionAsync(Guid transitionId, CancellationToken cancellationToken = default)
    {
        var transition = await _repository.GetTransitionByIdAsync(transitionId, cancellationToken);
        if (transition is null)
        {
            return Result.Failure(Error.NotFound("TRANSITION_NOT_FOUND", $"Transition '{transitionId}' was not found."));
        }

        _repository.DeleteWorkflowTransition(transition);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ==========================================
    // BOARDS & COLUMNS
    // ==========================================

    public async Task<Result<IReadOnlyList<BoardDto>>> GetBoardsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var boards = await _repository.GetBoardsByProjectAsync(projectId, cancellationToken);
        IReadOnlyList<BoardDto> dtos = boards.Select(x => new BoardDto(
            x.Id,
            x.ProjectId,
            x.Name,
            x.Type,
            x.IsDefault,
            x.CreatedAt)).ToList();

        return Result<IReadOnlyList<BoardDto>>.Success(dtos);
    }

    public async Task<Result<BoardDto>> CreateBoardAsync(Guid projectId, CreateBoardRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<BoardDto>.Failure(Error.Validation("BOARD_NAME_REQUIRED", "Board name is required."));
        }

        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name.Trim(),
            Type = string.Equals(request.Type, "Scrum", StringComparison.OrdinalIgnoreCase) ? "Scrum" : "Kanban",
            IsDefault = request.IsDefault,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddBoardAsync(board, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var dto = new BoardDto(board.Id, board.ProjectId, board.Name, board.Type, board.IsDefault, board.CreatedAt);
        return Result<BoardDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<BoardColumnDto>>> GetBoardColumnsAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var board = await _repository.GetBoardByIdAsync(boardId, cancellationToken);
        if (board is null)
        {
            return Result<IReadOnlyList<BoardColumnDto>>.Failure(Error.NotFound("BOARD_NOT_FOUND", $"Board '{boardId}' was not found."));
        }

        var columns = await _repository.GetBoardColumnsAsync(boardId, cancellationToken);
        var statuses = (await _repository.GetStatusesAsync(board.ProjectId, cancellationToken)).ToDictionary(x => x.Id, x => x.Name);

        IReadOnlyList<BoardColumnDto> dtos = columns.Select(c => new BoardColumnDto(
            c.Id,
            c.BoardId,
            c.StatusId,
            statuses.TryGetValue(c.StatusId, out var sName) ? sName : "Unknown",
            c.Name,
            c.OrderIndex,
            c.WipLimit)).ToList();

        return Result<IReadOnlyList<BoardColumnDto>>.Success(dtos);
    }

    public async Task<Result<BoardColumnDto>> CreateBoardColumnAsync(Guid boardId, CreateBoardColumnRequestDto request, CancellationToken cancellationToken = default)
    {
        var board = await _repository.GetBoardByIdAsync(boardId, cancellationToken);
        if (board is null)
        {
            return Result<BoardColumnDto>.Failure(Error.NotFound("BOARD_NOT_FOUND", $"Board '{boardId}' was not found."));
        }

        // 1. Chặn WIP < 0
        if (request.WipLimit.HasValue && request.WipLimit.Value < 0)
        {
            return Result<BoardColumnDto>.Failure(Error.Validation("WIP_LIMIT_NEGATIVE", "WIP limit cannot be negative."));
        }

        // 2. Chặn Status sai project
        var statuses = (await _repository.GetStatusesAsync(board.ProjectId, cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        if (!statuses.ContainsKey(request.StatusId))
        {
            return Result<BoardColumnDto>.Failure(Error.Validation("COLUMN_STATUS_INVALID", "The selected status does not belong to this board's project."));
        }

        var existingColumns = await _repository.GetBoardColumnsAsync(boardId, cancellationToken);
        var nextOrder = existingColumns.Count == 0 ? 1 : existingColumns.Max(x => x.OrderIndex) + 1;

        var column = new BoardColumn
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            StatusId = request.StatusId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? statuses[request.StatusId] : request.Name.Trim(),
            OrderIndex = nextOrder,
            WipLimit = request.WipLimit
        };

        await _repository.AddBoardColumnAsync(column, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var dto = new BoardColumnDto(
            column.Id,
            column.BoardId,
            column.StatusId,
            statuses[column.StatusId],
            column.Name,
            column.OrderIndex,
            column.WipLimit);

        return Result<BoardColumnDto>.Success(dto);
    }

    public async Task<Result> ReorderBoardColumnsAsync(Guid boardId, ReorderBoardColumnsRequestDto request, CancellationToken cancellationToken = default)
    {
        var columns = await _repository.GetBoardColumnsAsync(boardId, cancellationToken);
        if (columns.Count == 0)
        {
            return Result.Success();
        }

        var columnMap = columns.ToDictionary(x => x.Id);
        for (int i = 0; i < request.OrderedColumnIds.Count; i++)
        {
            var colId = request.OrderedColumnIds[i];
            if (columnMap.TryGetValue(colId, out var col))
            {
                col.OrderIndex = i + 1;
            }
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteBoardColumnAsync(Guid columnId, CancellationToken cancellationToken = default)
    {
        var column = await _repository.GetBoardColumnByIdAsync(columnId, cancellationToken);
        if (column is null)
        {
            return Result.Failure(Error.NotFound("COLUMN_NOT_FOUND", $"Column '{columnId}' was not found."));
        }

        _repository.DeleteBoardColumn(column);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
