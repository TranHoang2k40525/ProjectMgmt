using System.Collections.Concurrent;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Tests.Fakes;

public sealed class FakeProjectLookupService : IProjectLookupService
{
    public Dictionary<Guid, string> ProjectKeys { get; } = [];

    public Dictionary<Guid, IReadOnlyList<ProjectIssueTypeDto>> IssueTypes { get; } = [];

    public Dictionary<Guid, IReadOnlyList<ProjectStatusDto>> Statuses { get; } = [];

    public Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProjectKeys.ContainsKey(projectId));

    public Task<string?> GetProjectKeyAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProjectKeys.GetValueOrDefault(projectId));

    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IssueTypes.GetValueOrDefault(projectId) ?? []);

    public Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Statuses.GetValueOrDefault(projectId) ?? []);
}

public sealed class FakeWorkflowValidationService : IWorkflowValidationService
{
    public WorkflowValidationResult TransitionResult { get; set; } = new(true);

    public WorkflowValidationResult WipResult { get; set; } = new(true);

    public ProjectStatusDto? InitialStatus { get; set; }

    public Task<WorkflowValidationResult> CanTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        IReadOnlyCollection<string> userPermissions,
        CancellationToken cancellationToken = default) => Task.FromResult(TransitionResult);

    public Task<ProjectStatusDto?> GetInitialStatusAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) => Task.FromResult(InitialStatus);

    public Task<WorkflowValidationResult> CheckWipLimitAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default) => Task.FromResult(WipResult);
}

public sealed class FakeIssueNumberGenerator : IIssueNumberGenerator
{
    private readonly ConcurrentDictionary<Guid, int> _counters = new();

    public Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_counters.AddOrUpdate(projectId, 1, (_, current) => checked(current + 1)));
    }
}
