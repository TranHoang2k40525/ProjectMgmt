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
    public Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _dbContext.Projects.AsNoTracking().AnyAsync(
            x => x.Id == projectId && !x.IsDeleted,
            cancellationToken);

    public Task<Project?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted, cancellationToken);

    public Task<Project?> GetByKeyAsync(
        Guid organizationId,
        string projectKey,
        CancellationToken cancellationToken = default) =>
        _dbContext.Projects.SingleOrDefaultAsync(
            x => x.OrgId == organizationId && x.ProjectKey == projectKey && !x.IsDeleted,
            cancellationToken);

    public async Task<IReadOnlyList<IssueType>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.IssueTypes.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.WorkflowStatuses.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(cancellationToken);

    public Task<WorkflowTransition?> GetTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        CancellationToken cancellationToken = default) =>
        _dbContext.WorkflowTransitions.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.FromStatusId == fromStatusId && x.ToStatusId == toStatusId,
            cancellationToken);

    public Task<WorkflowStatus?> GetInitialStatusAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        _dbContext.WorkflowStatuses.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsInitial)
            .OrderBy(x => x.OrderIndex)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BoardColumnState?> GetBoardColumnStateAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default) =>
        (from boardColumn in _dbContext.BoardColumns.AsNoTracking()
         join board in _dbContext.Boards.AsNoTracking() on boardColumn.BoardId equals board.Id
         where boardColumn.BoardId == boardId && boardColumn.StatusId == statusId
         select new BoardColumnState(board.ProjectId, boardColumn.WipLimit))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> ReserveNextIssueNumberAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var project = await _dbContext.Projects
            .FromSqlInterpolated($"SELECT * FROM `Project` WHERE `Id` = {projectId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Project '{projectId}' does not exist.");

        project.IssueCounter++;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return project.IssueCounter;
    }

    public async Task<IReadOnlyList<Organization>> GetOrganizationsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Organizations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<Organization?> GetOrganizationByIdAsync(Guid orgId, CancellationToken cancellationToken = default) =>
        await _dbContext.Organizations.SingleOrDefaultAsync(x => x.Id == orgId, cancellationToken);

    public async Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken = default) =>
        await _dbContext.Organizations.AddAsync(organization, cancellationToken);

    public async Task<IReadOnlyList<Project>> GetAllProjectsAsync(Guid? orgId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Projects.AsNoTracking().Where(x => !x.IsDeleted);
        if (orgId.HasValue)
        {
            query = query.Where(x => x.OrgId == orgId.Value);
        }
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public void UpdateProject(Project project) =>
        _dbContext.Projects.Update(project);

    public async Task AddIssueTypesAsync(IEnumerable<IssueType> issueTypes, CancellationToken cancellationToken = default) =>
        await _dbContext.IssueTypes.AddRangeAsync(issueTypes, cancellationToken);

    public async Task AddWorkflowStatusesAsync(IEnumerable<WorkflowStatus> statuses, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkflowStatuses.AddRangeAsync(statuses, cancellationToken);

    public async Task AddWorkflowTransitionsAsync(IEnumerable<WorkflowTransition> transitions, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkflowTransitions.AddRangeAsync(transitions, cancellationToken);

    public async Task AddBoardAsync(Board board, CancellationToken cancellationToken = default) =>
        await _dbContext.Boards.AddAsync(board, cancellationToken);

    public async Task AddBoardColumnsAsync(IEnumerable<BoardColumn> columns, CancellationToken cancellationToken = default) =>
        await _dbContext.BoardColumns.AddRangeAsync(columns, cancellationToken);

    public async Task<IReadOnlyList<WorkflowTransition>> GetTransitionsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkflowTransitions.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    public async Task<WorkflowTransition?> GetTransitionByIdAsync(Guid transitionId, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkflowTransitions.SingleOrDefaultAsync(x => x.Id == transitionId, cancellationToken);

    public async Task AddWorkflowTransitionAsync(WorkflowTransition transition, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkflowTransitions.AddAsync(transition, cancellationToken);

    public void DeleteWorkflowTransition(WorkflowTransition transition) =>
        _dbContext.WorkflowTransitions.Remove(transition);

    public async Task<IReadOnlyList<Board>> GetBoardsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.Boards.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<Board?> GetBoardByIdAsync(Guid boardId, CancellationToken cancellationToken = default) =>
        await _dbContext.Boards.SingleOrDefaultAsync(x => x.Id == boardId, cancellationToken);

    public async Task<IReadOnlyList<BoardColumn>> GetBoardColumnsAsync(Guid boardId, CancellationToken cancellationToken = default) =>
        await _dbContext.BoardColumns.AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(cancellationToken);

    public async Task<BoardColumn?> GetBoardColumnByIdAsync(Guid columnId, CancellationToken cancellationToken = default) =>
        await _dbContext.BoardColumns.SingleOrDefaultAsync(x => x.Id == columnId, cancellationToken);

    public async Task AddBoardColumnAsync(BoardColumn column, CancellationToken cancellationToken = default) =>
        await _dbContext.BoardColumns.AddAsync(column, cancellationToken);

    public void DeleteBoardColumn(BoardColumn column) =>
        _dbContext.BoardColumns.Remove(column);

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default) =>
        await _dbContext.Projects.AddAsync(project, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
