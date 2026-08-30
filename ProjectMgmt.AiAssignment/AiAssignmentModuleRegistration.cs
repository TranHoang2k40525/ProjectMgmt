using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.AiAssignment;

public sealed class AiAssignmentDbContext(DbContextOptions<AiAssignmentDbContext> options) : DbContext(options);

public static class AiAssignmentModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_AiAssignment";

    public static IServiceCollection AddAiAssignmentModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<AiAssignmentDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<AiAssignmentDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(AiAssignmentDbContext).Assembly.GetName().Name!,
            "AiAssignment");
    }
}
