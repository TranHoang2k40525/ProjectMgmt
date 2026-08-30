using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.IdentityAccess;

public sealed class IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options) : DbContext(options);

public static class IdentityAccessModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_IdentityAccess";

    public static IServiceCollection AddIdentityAccessModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<IdentityAccessDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<IdentityAccessDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(IdentityAccessDbContext).Assembly.GetName().Name!,
            "IdentityAccess");
    }
}
