namespace ProjectMgmt.Solution.Services.Ai.Adapters;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.Staging;
using StackExchange.Redis;

public sealed class StackExchangeRedisStagingProvider : IExternalStagingProvider, IAsyncDisposable
{
    private readonly RedisOptions _options;
    private readonly ILogger<StackExchangeRedisStagingProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile IConnectionMultiplexer? _connection;

    public StackExchangeRedisStagingProvider(
        IOptions<RedisOptions> options,
        ILogger<StackExchangeRedisStagingProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "StackExchangeRedis";

    private async Task<IDatabase?> GetDatabaseAsync()
    {
        if (_connection is { IsConnected: true })
        {
            return _connection.GetDatabase(_options.Database);
        }

        if (!_options.Enabled)
        {
            return null;
        }

        await _lock.WaitAsync();
        try
        {
            if (_connection is { IsConnected: true })
            {
                return _connection.GetDatabase(_options.Database);
            }

            if (string.IsNullOrWhiteSpace(_options.Host) || _options.Host.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var config = ConfigurationOptions.Parse(_options.BuildConnectionString());
            config.AbortOnConnectFail = false;
            config.ConnectTimeout = _options.ConnectTimeoutMs;
            config.SyncTimeout = _options.SyncTimeoutMs;

            _connection = await ConnectionMultiplexer.ConnectAsync(config);
            return _connection.IsConnected ? _connection.GetDatabase(_options.Database) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisStaging] Không thể kết nối tới Redis tại {Host}:{Port}", _options.Host, _options.Port);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private string Key(string id) => $"{_options.InstanceName}{id}";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled) return false;
        try
        {
            var db = await GetDatabaseAsync();
            if (db == null) return false;
            var pong = await db.PingAsync();
            return pong > TimeSpan.Zero;
        }
        catch
        {
            return false;
        }
    }

    public async Task SetAsync(string key, string serializedValue, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = await GetDatabaseAsync() ?? throw new InvalidOperationException("Redis staging provider unavailable.");
        await db.StringSetAsync(Key(key), serializedValue, ttl);
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var db = await GetDatabaseAsync();
        if (db == null) return null;
        RedisValue val = await db.StringGetAsync(Key(key));
        return val.HasValue ? (string?)val : null;
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        var db = await GetDatabaseAsync();
        return db != null && await db.KeyDeleteAsync(Key(key));
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var db = await GetDatabaseAsync();
        return db != null && await db.KeyExistsAsync(Key(key));
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        _lock.Dispose();
    }
}
