using Planning.Domain.Entities;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;
using Xunit;

namespace ProjectMgmt.Tests;

public class ProjectOwnershipTests
{
    [Fact]
    public async Task EmptyOrganizationListDoesNotCreateFakeDefaultOrganization()
    {
        var repository = new FakeProjectRepository();
        var service = new ProjectManagementService(repository);

        var result = await service.GetOrganizationsAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(repository.AddedOrganizations);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task OrganizationOwnerAlwaysComesFromAuthenticatedUser()
    {
        var repository = new FakeProjectRepository();
        var service = new ProjectManagementService(repository);
        var authenticatedUserId = Guid.NewGuid();

        var result = await service.CreateOrganizationAsync(
            new CreateOrganizationDto
            {
                Name = "Nhóm 7 Kỹ Sư",
                Slug = "nhom-7-ky-su",
                OwnerId = Guid.NewGuid()
            },
            authenticatedUserId);

        Assert.True(result.IsSuccess);
        Assert.Single(repository.AddedOrganizations);
        Assert.Equal(authenticatedUserId, repository.AddedOrganizations[0].OwnerId);
        Assert.Equal(authenticatedUserId, result.Value.OwnerId);
    }

    [Fact]
    public async Task ProjectCreatorAndInitialLeadAlwaysComeFromAuthenticatedUser()
    {
        var organizationId = Guid.NewGuid();
        var repository = new FakeProjectRepository
        {
            Organization = new Organization
            {
                Id = organizationId,
                Name = "Nhóm 7 Kỹ Sư",
                Slug = "nhom-7",
                OwnerId = Guid.NewGuid(),
                IsActive = true
            }
        };
        var service = new ProjectManagementService(repository);
        var authenticatedUserId = Guid.NewGuid();

        var result = await service.CreateProjectAsync(
            new CreateProjectRequestDto
            {
                OrgId = organizationId,
                ProjectKey = "SCRUM7",
                Name = "Hệ thống Quản lý Dự án Scrum tích hợp AI",
                LeadUserId = Guid.NewGuid()
            },
            authenticatedUserId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.AddedProject);
        Assert.Equal(authenticatedUserId, repository.AddedProject.CreatedByUserId);
        Assert.Equal(authenticatedUserId, repository.AddedProject.LeadUserId);
        Assert.Equal(authenticatedUserId, result.Value.CreatedByUserId);
        Assert.Equal(5, repository.AddedIssueTypes.Count);
        Assert.Equal(5, repository.AddedStatuses.Count);
        Assert.Equal(7, repository.AddedTransitions.Count);
        Assert.Single(repository.AddedBoards);
        Assert.Equal(4, repository.AddedBoardColumns.Count);
    }
}

public class FakeProjectRepository : IProjectRepository
{
    public Organization? Organization { get; set; }
    public Project? ExistingProject { get; set; }
    public Project? AddedProject { get; private set; }
    public List<Organization> AddedOrganizations { get; } = [];
    public List<IssueType> AddedIssueTypes { get; } = [];
    public List<WorkflowStatus> AddedStatuses { get; } = [];
    public List<WorkflowTransition> AddedTransitions { get; } = [];
    public List<Board> AddedBoards { get; } = [];
    public List<BoardColumn> AddedBoardColumns { get; } = [];
    public int SaveCount { get; private set; }

    public Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingProject?.Id == projectId);
    }

    public Task<Project?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingProject?.Id == projectId ? ExistingProject : null);
    }

    public Task<Project?> GetByKeyAsync(
        Guid organizationId,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            ExistingProject?.OrgId == organizationId && ExistingProject.ProjectKey == projectKey
                ? ExistingProject
                : null);
    }

    public Task<IReadOnlyList<IssueType>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<IssueType>>(AddedIssueTypes);
    }

    public Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<WorkflowStatus>>(AddedStatuses);
    }

    public Task<WorkflowTransition?> GetTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<WorkflowTransition?>(null);
    }

    public Task<WorkflowStatus?> GetInitialStatusAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AddedStatuses.FirstOrDefault(status => status.IsInitial));
    }

    public Task<BoardColumnState?> GetBoardColumnStateAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<BoardColumnState?>(null);
    }

    public Task<int> ReserveNextIssueNumberAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(1);
    }

    public Task<IReadOnlyList<Organization>> GetOrganizationsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Organization> organizations = Organization is null ? [] : [Organization];
        return Task.FromResult(organizations);
    }

    public Task<Organization?> GetOrganizationByIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Organization?.Id == orgId ? Organization : null);
    }

    public Task AddOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        AddedOrganizations.Add(organization);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Project>> GetAllProjectsAsync(
        Guid? orgId = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Project> projects = ExistingProject is null ? [] : [ExistingProject];
        return Task.FromResult(projects);
    }

    public void UpdateProject(Project project)
    {
        ExistingProject = project;
    }

    public Task AddIssueTypesAsync(
        IEnumerable<IssueType> issueTypes,
        CancellationToken cancellationToken = default)
    {
        AddedIssueTypes.AddRange(issueTypes);
        return Task.CompletedTask;
    }

    public Task AddWorkflowStatusesAsync(
        IEnumerable<WorkflowStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        AddedStatuses.AddRange(statuses);
        return Task.CompletedTask;
    }

    public Task AddWorkflowTransitionsAsync(
        IEnumerable<WorkflowTransition> transitions,
        CancellationToken cancellationToken = default)
    {
        AddedTransitions.AddRange(transitions);
        return Task.CompletedTask;
    }

    public Task AddBoardAsync(Board board, CancellationToken cancellationToken = default)
    {
        AddedBoards.Add(board);
        return Task.CompletedTask;
    }

    public Task AddBoardColumnsAsync(
        IEnumerable<BoardColumn> columns,
        CancellationToken cancellationToken = default)
    {
        AddedBoardColumns.AddRange(columns);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowTransition>> GetTransitionsByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<WorkflowTransition>>(AddedTransitions);
    }

    public Task<WorkflowTransition?> GetTransitionByIdAsync(
        Guid transitionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AddedTransitions.FirstOrDefault(item => item.Id == transitionId));
    }

    public Task AddWorkflowTransitionAsync(
        WorkflowTransition transition,
        CancellationToken cancellationToken = default)
    {
        AddedTransitions.Add(transition);
        return Task.CompletedTask;
    }

    public void DeleteWorkflowTransition(WorkflowTransition transition)
    {
        AddedTransitions.Remove(transition);
    }

    public Task<IReadOnlyList<Board>> GetBoardsByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Board>>(AddedBoards);
    }

    public Task<Board?> GetBoardByIdAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AddedBoards.FirstOrDefault(board => board.Id == boardId));
    }

    public Task<IReadOnlyList<BoardColumn>> GetBoardColumnsAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BoardColumn>>(
            AddedBoardColumns.Where(column => column.BoardId == boardId).ToList());
    }

    public Task<BoardColumn?> GetBoardColumnByIdAsync(
        Guid columnId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AddedBoardColumns.FirstOrDefault(column => column.Id == columnId));
    }

    public Task AddBoardColumnAsync(
        BoardColumn column,
        CancellationToken cancellationToken = default)
    {
        AddedBoardColumns.Add(column);
        return Task.CompletedTask;
    }

    public void DeleteBoardColumn(BoardColumn column)
    {
        AddedBoardColumns.Remove(column);
    }

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        AddedProject = project;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
