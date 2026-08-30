using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.BuildingBlocks.Persistence;

namespace ProjectMgmt.Notification;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options);

public static class NotificationModuleRegistration
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Notification";

    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddValidatorsFromAssemblyContaining<NotificationDbContext>(includeInternalTypes: true);
        return services.AddProjectMgmtMySqlDbContext<NotificationDbContext>(
            configuration,
            environment,
            MigrationsHistoryTable,
            typeof(NotificationDbContext).Assembly.GetName().Name!,
            "Notification");
    }
}
