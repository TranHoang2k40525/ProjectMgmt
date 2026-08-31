using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.Planning.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Entities;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Repositories;

namespace ProjectMgmt.Modules.Planning.SprintBacklog.Infrastructure.Persistence;

internal sealed class EfSprintRepository(PlanningDbContext dbContext) : ISprintRepository
{
    public Task<Sprint?> GetByIdAsync(Guid sprintId, CancellationToken cancellationToken = default) =>
        dbContext.Sprints.SingleOrDefaultAsync(x => x.Id == sprintId, cancellationToken);

    public Task<Sprint?> GetActiveAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.Sprints.SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.Status == "Active",
            cancellationToken);

    public async Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default) =>
        await dbContext.Sprints.AddAsync(sprint, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
