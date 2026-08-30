using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.SprintBacklog;

public sealed class SprintBacklogDbContext(DbContextOptions<SprintBacklogDbContext> options) : DbContext(options);

public static class SprintBacklogModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_SprintBacklog";

    public static IServiceCollection AddSprintBacklogModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<SprintBacklogDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<SprintBacklogDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(SprintBacklogDbContext).Assembly.GetName().Name!,
            "SprintBacklog");
    }
}
