using ProjectMgmt.AiDataOps.Contracts;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Repositories;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Application.Services;

internal sealed class EvaluationSetCatalog(IAiDatasetRepository repository) : IEvaluationSetCatalog
{
    public async Task<EvaluationSetDescriptor?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var version = await repository.GetLatestFrozenVersionAsync(cancellationToken);
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
