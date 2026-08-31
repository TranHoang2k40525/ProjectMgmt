using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Repositories;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Modules.Planning.SprintBacklog.Application.Services;

internal sealed class SprintLookupService(ISprintRepository repository) : ISprintLookupService
{
    public async Task<SprintLookupInfo?> GetByIdAsync(
        Guid sprintId,
        CancellationToken cancellationToken = default)
    {
        var sprint = await repository.GetByIdAsync(sprintId, cancellationToken);
        return sprint is null ? null : new SprintLookupInfo(sprint.Id, sprint.ProjectId, sprint.Status);
    }
}
