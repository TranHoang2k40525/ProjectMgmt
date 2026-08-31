using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ProjectMgmt.BuildingBlocks.Persistence;

public static class MySqlPersistenceExtensions
{
    public const string ConnectionStringName = "ProjectMgmt";

    private static readonly MySqlServerVersion MinimumServerVersion = new(new Version(8, 0, 16));

    public static IServiceCollection AddProjectMgmtMySqlDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string migrationsHistoryTable,
        string migrationsAssembly,
        string moduleName)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsHistoryTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var connectionString = configuration.GetConnectionString(ConnectionStringName) ?? string.Empty;
        var enableSensitiveLogging = environment.IsDevelopment()
            && configuration.GetValue<bool>("Persistence:EnableSensitiveDataLogging");

        services.AddDbContext<TContext>(options =>
        {
            options.UseMySql(connectionString, MinimumServerVersion, mySql =>
            {
                mySql.MigrationsAssembly(migrationsAssembly);
                mySql.MigrationsHistoryTable(migrationsHistoryTable);
                mySql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            });

            if (enableSensitiveLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddHealthChecks().AddCheck<ProjectMgmtDbContextHealthCheck<TContext>>(
            name: $"db-{moduleName.ToLowerInvariant()}",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["db", "ready", moduleName]);

        services.AddHealthChecks().AddCheck<ProjectMgmtSchemaHealthCheck<TContext>>(
            name: $"schema-{moduleName.ToLowerInvariant()}",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["db", "schema", "ready", moduleName]);

        return services;
    }
}

public sealed class ProjectMgmtDbContextHealthCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The module could not connect to ProjectMgmt.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "The module database check could not be initialized or connected.",
                exception);
        }
    }
}
