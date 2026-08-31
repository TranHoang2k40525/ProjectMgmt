using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.Planning.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Entities;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Repositories;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Infrastructure.Persistence;

internal sealed class EfProjectRepository(PlanningDbContext dbContext) : IProjectRepository
{
    public Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.Projects.AsNoTracking().AnyAsync(
            x => x.Id == projectId && !x.IsDeleted,
            cancellationToken);

    public Task<Project?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted, cancellationToken);

    public Task<Project?> GetByKeyAsync(
        Guid organizationId,
        string projectKey,
        CancellationToken cancellationToken = default) =>
        dbContext.Projects.SingleOrDefaultAsync(
            x => x.OrgId == organizationId && x.ProjectKey == projectKey && !x.IsDeleted,
            cancellationToken);

    public async Task<IReadOnlyList<IssueType>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await dbContext.IssueTypes.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkflowStatuses.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(cancellationToken);

    public Task<WorkflowTransition?> GetTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        CancellationToken cancellationToken = default) =>
        dbContext.WorkflowTransitions.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.FromStatusId == fromStatusId && x.ToStatusId == toStatusId,
            cancellationToken);

    public Task<WorkflowStatus?> GetInitialStatusAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        dbContext.WorkflowStatuses.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsInitial)
            .OrderBy(x => x.OrderIndex)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BoardColumnState?> GetBoardColumnStateAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default) =>
        (from boardColumn in dbContext.BoardColumns.AsNoTracking()
         join board in dbContext.Boards.AsNoTracking() on boardColumn.BoardId equals board.Id
         where boardColumn.BoardId == boardId && boardColumn.StatusId == statusId
         select new BoardColumnState(board.ProjectId, boardColumn.WipLimit))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> ReserveNextIssueNumberAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var project = await dbContext.Projects
            .FromSqlInterpolated($"SELECT * FROM `Project` WHERE `Id` = {projectId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Project '{projectId}' does not exist.");

        project.IssueCounter++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return project.IssueCounter;
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default) =>
        await dbContext.Projects.AddAsync(project, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
