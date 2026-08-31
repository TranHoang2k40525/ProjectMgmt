namespace ProjectMgmt.AiDataOps.Contracts;

public class EvaluationSetDescriptor
{
    public EvaluationSetDescriptor(
        string version,
        int sampleCount,
        string sha256Checksum,
        DateTimeOffset frozenAtUtc)
    {
        Version = version;
        SampleCount = sampleCount;
        Sha256Checksum = sha256Checksum;
        FrozenAtUtc = frozenAtUtc;
    }

    public string Version { get; set; }
    public int SampleCount { get; set; }
    public string Sha256Checksum { get; set; }
    public DateTimeOffset FrozenAtUtc { get; set; }
}

public interface IEvaluationSetCatalog
{
    Task<EvaluationSetDescriptor?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
