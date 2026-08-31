using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.Planning.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Entities;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.IRepositories;

namespace ProjectMgmt.Modules.Planning.SprintBacklog.Infrastructure.Repositories;

internal class SprintRepository : ISprintRepository
{
    private readonly PlanningAppDbContext _dbContext;

    public SprintRepository(PlanningAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<Sprint?> GetByIdAsync(Guid sprintId, CancellationToken cancellationToken = default) =>
        _dbContext.Sprints.SingleOrDefaultAsync(x => x.Id == sprintId, cancellationToken);

    public Task<Sprint?> GetActiveAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _dbContext.Sprints.SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.Status == "Active",
            cancellationToken);

    public async Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default) =>
        await _dbContext.Sprints.AddAsync(sprint, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
