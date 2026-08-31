using ProjectMgmt.AiDataOps.Contracts;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.IRepositories;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Application.Services;

internal class EvaluationSetCatalog : IEvaluationSetCatalog
{
    private readonly IAiDatasetRepository _repository;

    public EvaluationSetCatalog(IAiDatasetRepository repository)
    {
        _repository = repository;
    }

    public async Task<EvaluationSetDescriptor?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var version = await _repository.GetLatestFrozenVersionAsync(cancellationToken);
        if (version?.FrozenAt is null || version.Checksum is null)
        {
            return null;
        }

        return new EvaluationSetDescriptor(
            version.VersionTag,
            version.TestCount,
            version.Checksum,
            new DateTimeOffset(DateTime.SpecifyKind(version.FrozenAt.Value, DateTimeKind.Utc)));
    }
}
