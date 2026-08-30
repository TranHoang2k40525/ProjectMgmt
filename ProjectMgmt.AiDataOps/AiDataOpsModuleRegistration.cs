using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.AiDataOps;

public sealed class AiDataOpsDbContext(DbContextOptions<AiDataOpsDbContext> options) : DbContext(options);

public static class AiDataOpsModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_AiDataOps";

    public static IServiceCollection AddAiDataOpsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<AiDataOpsDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<AiDataOpsDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(AiDataOpsDbContext).Assembly.GetName().Name!,
            "AiDataOps");
    }
}
