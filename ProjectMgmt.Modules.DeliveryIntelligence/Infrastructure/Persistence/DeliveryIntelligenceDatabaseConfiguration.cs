using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;

public static class DeliveryIntelligenceDatabaseConfiguration
{
    private const string ConnectionStringName = "ProjectMgmt";
    private static readonly MySqlServerVersion ServerVersion = new MySqlServerVersion(new Version(8, 0, 16));
    private static readonly string[] HealthCheckTags = { "db", "ready", "delivery-intelligence" };

    public static IServiceCollection AddDeliveryIntelligenceDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName) ?? string.Empty;
        var enableSensitiveLogging = environment.IsDevelopment()
            && configuration.GetValue<bool>("Persistence:EnableSensitiveDataLogging");

        services.AddDbContext<DeliveryIntelligenceAppDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion, mySql =>
            {
                mySql.MigrationsAssembly(typeof(DeliveryIntelligenceAppDbContext).Assembly.GetName().Name);
                mySql.MigrationsHistoryTable("__EFMigrationsHistory_DeliveryIntelligence");
                mySql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            });

            if (enableSensitiveLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddHealthChecks().AddCheck<DeliveryIntelligenceDatabaseHealthCheck>(
            "db-delivery-intelligence",
            HealthStatus.Unhealthy,
            HealthCheckTags);

        return services;
    }
}

internal class DeliveryIntelligenceDatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DeliveryIntelligenceDatabaseHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryIntelligenceAppDbContext>();
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("DeliveryIntelligence could not connect to ProjectMgmt.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("DeliveryIntelligence database check failed.", exception);
        }
    }
}
