using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;

public class ProjectLookupService : IProjectLookupService
{
    private readonly IProjectRepository _repository;

    public ProjectLookupService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var types = await _repository.GetIssueTypesAsync(projectId, cancellationToken);
        return types.Select(x => new ProjectIssueTypeDto(x.Id, x.ProjectId, x.Name, x.ColorHex, x.HierarchyLevel, x.IsSubtask, x.OrderIndex)).ToList();
    }

    public async Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var statuses = await _repository.GetStatusesAsync(projectId, cancellationToken);
        return statuses.Select(x => new ProjectStatusDto(x.Id, x.ProjectId, x.Name, x.Category, x.ColorHex, x.OrderIndex, x.IsInitial)).ToList();
    }
}
