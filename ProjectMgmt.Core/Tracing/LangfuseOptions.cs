namespace ProjectMgmt.Core.Tracing;

public sealed class LangfuseOptions
{
    public const string SectionName = "Langfuse";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "YOUR_LANGFUSE_HOST";
    public int Port { get; set; } = 443;
    public string PublicKey { get; set; } = "YOUR_LANGFUSE_PUBLIC_KEY";
    public string SecretKey { get; set; } = "YOUR_LANGFUSE_SECRET_KEY";
    public int TimeoutMs { get; set; } = 5000;
    public string Environment { get; set; } = "development";
    public double SampleRate { get; set; } = 1.0;
}
