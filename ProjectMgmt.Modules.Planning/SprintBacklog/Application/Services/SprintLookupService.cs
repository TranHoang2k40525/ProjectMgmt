using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.IRepositories;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Modules.Planning.SprintBacklog.Application.Services;

internal class SprintLookupService : ISprintLookupService
{
    private readonly ISprintRepository _repository;

    public SprintLookupService(ISprintRepository repository)
    {
        _repository = repository;
    }

    public async Task<SprintLookupInfo?> GetByIdAsync(
        Guid sprintId,
        CancellationToken cancellationToken = default)
    {
        var sprint = await _repository.GetByIdAsync(sprintId, cancellationToken);
        return sprint is null ? null : new SprintLookupInfo(sprint.Id, sprint.ProjectId, sprint.Status);
    }
}
