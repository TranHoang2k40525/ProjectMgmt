namespace ProjectMgmt.AiDataOps.Contracts;

public sealed record EvaluationSetDescriptor(
    string Version,
    int SampleCount,
    string Sha256Checksum,
    DateTimeOffset FrozenAtUtc);

public interface IEvaluationSetCatalog
{
    Task<EvaluationSetDescriptor?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
