using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using Planning.Domain.Entities;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly PlanningDbContext _dbContext;

    public ProjectRepository(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(Guid projectId) =>
        _dbContext.Projects.AsNoTracking().AnyAsync(
            x => x.Id == projectId && !x.IsDeleted);

    public Task<Project?> GetByIdAsync(Guid projectId) =>
        _dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted);

    public Task<Project?> GetByKeyAsync(
        Guid organizationId,
        string projectKey) =>
        _dbContext.Projects.SingleOrDefaultAsync(
            x => x.OrgId == organizationId && x.ProjectKey == projectKey);

    public async Task<IReadOnlyList<IssueType>> GetIssueTypesAsync(
        Guid projectId) =>
        await _dbContext.IssueTypes.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync();

    public async Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(
        Guid projectId) =>
        await _dbContext.WorkflowStatuses.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync();

    public Task<WorkflowTransition?> GetTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId) =>
        _dbContext.WorkflowTransitions.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.FromStatusId == fromStatusId && x.ToStatusId == toStatusId);

    public Task<WorkflowStatus?> GetInitialStatusAsync(
        Guid projectId) =>
        _dbContext.WorkflowStatuses.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsInitial)
            .OrderBy(x => x.OrderIndex)
            .FirstOrDefaultAsync();

    public Task<BoardColumnState?> GetBoardColumnStateAsync(
        Guid boardId,
        Guid statusId) =>
        (from boardColumn in _dbContext.BoardColumns.AsNoTracking()
         join board in _dbContext.Boards.AsNoTracking() on boardColumn.BoardId equals board.Id
         where boardColumn.BoardId == boardId && boardColumn.StatusId == statusId
         select new BoardColumnState(board.ProjectId, boardColumn.WipLimit))
        .SingleOrDefaultAsync();

    public async Task<int> ReserveNextIssueNumberAsync(
        Guid projectId)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var project = await _dbContext.Projects
            .FromSqlInterpolated($"SELECT * FROM `Project` WHERE `Id` = {projectId} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"Project '{projectId}' does not exist.");

        project.IssueCounter++;
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return project.IssueCounter;
    }

    public async Task<IReadOnlyList<Organization>> GetOrganizationsAsync() =>
        await _dbContext.Organizations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();

    public async Task<Organization?> GetOrganizationByIdAsync(Guid orgId) =>
        await _dbContext.Organizations.SingleOrDefaultAsync(x => x.Id == orgId);

    public async Task AddOrganizationAsync(Organization organization) =>
        await _dbContext.Organizations.AddAsync(organization);

    public async Task<IReadOnlyList<Project>> GetAllProjectsAsync(Guid? orgId = null)
    {
        var query = _dbContext.Projects.AsNoTracking().Where(x => !x.IsDeleted);
        if (orgId.HasValue)
        {
            query = query.Where(x => x.OrgId == orgId.Value);
        }
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    public void UpdateProject(Project project) =>
        _dbContext.Projects.Update(project);

    public async Task AddIssueTypesAsync(IEnumerable<IssueType> issueTypes) =>
        await _dbContext.IssueTypes.AddRangeAsync(issueTypes);

    public async Task AddWorkflowStatusesAsync(IEnumerable<WorkflowStatus> statuses) =>
        await _dbContext.WorkflowStatuses.AddRangeAsync(statuses);

    public async Task AddWorkflowTransitionsAsync(IEnumerable<WorkflowTransition> transitions) =>
        await _dbContext.WorkflowTransitions.AddRangeAsync(transitions);

    public async Task AddBoardAsync(Board board) =>
        await _dbContext.Boards.AddAsync(board);

    public async Task AddBoardColumnsAsync(IEnumerable<BoardColumn> columns) =>
        await _dbContext.BoardColumns.AddRangeAsync(columns);

    public async Task<IReadOnlyList<WorkflowTransition>> GetTransitionsByProjectAsync(Guid projectId) =>
        await _dbContext.WorkflowTransitions.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .ToListAsync();

    public async Task<WorkflowTransition?> GetTransitionByIdAsync(Guid transitionId) =>
        await _dbContext.WorkflowTransitions.SingleOrDefaultAsync(x => x.Id == transitionId);

    public async Task AddWorkflowTransitionAsync(WorkflowTransition transition) =>
        await _dbContext.WorkflowTransitions.AddAsync(transition);

    public void DeleteWorkflowTransition(WorkflowTransition transition) =>
        _dbContext.WorkflowTransitions.Remove(transition);

    public async Task<IReadOnlyList<Board>> GetBoardsByProjectAsync(Guid projectId) =>
        await _dbContext.Boards.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync();

    public async Task<Board?> GetBoardByIdAsync(Guid boardId) =>
        await _dbContext.Boards.SingleOrDefaultAsync(x => x.Id == boardId);

    public async Task<IReadOnlyList<BoardColumn>> GetBoardColumnsAsync(Guid boardId) =>
        await _dbContext.BoardColumns
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync();

    public async Task<BoardColumn?> GetBoardColumnByIdAsync(Guid columnId) =>
        await _dbContext.BoardColumns.SingleOrDefaultAsync(x => x.Id == columnId);

    public async Task AddBoardColumnAsync(BoardColumn column) =>
        await _dbContext.BoardColumns.AddAsync(column);

    public void DeleteBoardColumn(BoardColumn column) =>
        _dbContext.BoardColumns.Remove(column);

    public async Task AddAsync(Project project) =>
        await _dbContext.Projects.AddAsync(project);

    public async Task SaveChangesAsync() =>
        await _dbContext.SaveChangesAsync();
}
