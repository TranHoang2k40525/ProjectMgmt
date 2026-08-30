using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.ProjectManagement;

public sealed class ProjectManagementDbContext(DbContextOptions<ProjectManagementDbContext> options) : DbContext(options);

public static class ProjectManagementModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_ProjectManagement";

    public static IServiceCollection AddProjectManagementModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<ProjectManagementDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<ProjectManagementDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(ProjectManagementDbContext).Assembly.GetName().Name!,
            "ProjectManagement");
    }
}
