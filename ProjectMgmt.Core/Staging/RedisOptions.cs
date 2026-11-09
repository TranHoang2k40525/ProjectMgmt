namespace ProjectMgmt.Core.Staging;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "YOUR_REDIS_HOST";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = "YOUR_REDIS_PASSWORD";
    public int Database { get; set; }
    public string InstanceName { get; set; } = "ProjectMgmt:Ai:";
    public int TimeoutMs { get; set; } = 3000;
    public int ConnectTimeoutMs { get; set; } = 5000;
    public int SyncTimeoutMs { get; set; } = 3000;
    public bool Ssl { get; set; }

    public string BuildConnectionString()
    {
        var passwordPart = string.IsNullOrWhiteSpace(Password) || Password.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $",password={Password}";
        return $"{Host}:{Port}{passwordPart},defaultDatabase={Database},connectTimeout={ConnectTimeoutMs},syncTimeout={SyncTimeoutMs},ssl={Ssl}";
    }
}
