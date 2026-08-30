using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.AiCore;

public sealed class AiCoreDbContext(DbContextOptions<AiCoreDbContext> options) : DbContext(options);

public static class AiCoreModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_AiCore";

    public static IServiceCollection AddAiCoreModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<AiCoreDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<AiCoreDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(AiCoreDbContext).Assembly.GetName().Name!,
            "AiCore");
    }
}
