using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.AiAssist;

public sealed class AiAssistDbContext(DbContextOptions<AiAssistDbContext> options) : DbContext(options);

public static class AiAssistModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_AiAssist";

    public static IServiceCollection AddAiAssistModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<AiAssistDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<AiAssistDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(AiAssistDbContext).Assembly.GetName().Name!,
            "AiAssist");
    }
}
