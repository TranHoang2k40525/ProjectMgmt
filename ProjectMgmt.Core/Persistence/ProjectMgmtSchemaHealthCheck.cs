using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProjectMgmt.BuildingBlocks.Persistence;

public sealed class ProjectMgmtSchemaHealthCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
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
            var expected = GetExpectedColumns(dbContext.Model);
            var actual = await GetActualColumnsAsync(dbContext, cancellationToken);

            var missingTables = expected.Keys
                .Where(table => !actual.ContainsKey(table))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var missingColumns = expected
                .Where(pair => actual.TryGetValue(pair.Key, out _))
                .SelectMany(pair => pair.Value
                    .Where(column => !actual[pair.Key].Contains(column))
                    .Select(column => $"{pair.Key}.{column}"))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missingTables.Length == 0 && missingColumns.Length == 0)
            {
                return HealthCheckResult.Healthy(
                    $"Schema is compatible ({expected.Count} tables checked).");
            }

            var details = new Dictionary<string, object>
            {
                ["expectedTableCount"] = expected.Count,
                ["actualOwnedTableCount"] = expected.Keys.Count(actual.ContainsKey),
                ["missingTables"] = missingTables,
                ["missingColumns"] = missingColumns
            };

            return HealthCheckResult.Unhealthy(
                $"Schema is incomplete: {missingTables.Length} table(s) and {missingColumns.Length} column(s) are missing.",
                data: details);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Schema compatibility could not be checked.", exception);
        }
    }

    private static Dictionary<string, HashSet<string>> GetExpectedColumns(IModel model)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
            {
                continue;
            }

            var table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            if (!result.TryGetValue(tableName, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                result[tableName] = columns;
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(table);
                if (columnName is not null)
                {
                    columns.Add(columnName);
                }
            }
        }

        return result;
    }

    private static async Task<Dictionary<string, HashSet<string>>> GetActualColumnsAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var connection = dbContext.Database.GetDbConnection();
        var initiallyClosed = connection.State == ConnectionState.Closed;

        if (initiallyClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TABLE_NAME, COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var table = reader.GetString(0);
                var column = reader.GetString(1);
                if (!result.TryGetValue(table, out var columns))
                {
                    columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[table] = columns;
                }

                columns.Add(column);
            }
        }
        finally
        {
            if (initiallyClosed)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }
}
