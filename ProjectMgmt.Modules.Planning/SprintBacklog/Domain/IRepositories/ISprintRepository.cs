using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.SprintBacklog.Domain.IRepositories;

public interface ISprintRepository
{
    Task<Sprint?> GetByIdAsync(Guid sprintId, CancellationToken cancellationToken = default);

    Task<Sprint?> GetActiveAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
