using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.IssueTracking;

public sealed class IssueTrackingDbContext(DbContextOptions<IssueTrackingDbContext> options) : DbContext(options);

public static class IssueTrackingModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_IssueTracking";

    public static IServiceCollection AddIssueTrackingModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<IssueTrackingDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<IssueTrackingDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(IssueTrackingDbContext).Assembly.GetName().Name!,
            "IssueTracking");
    }
}
