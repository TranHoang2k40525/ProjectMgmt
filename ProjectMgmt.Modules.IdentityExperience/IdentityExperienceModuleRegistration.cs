using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.AiAssist.Contracts;
using ProjectMgmt.BuildingBlocks.Persistence;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Notification.Contracts;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Application.Services;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Repositories;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Infrastructure.Persistence;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Application.Services;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Repositories;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Infrastructure.Persistence;
using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;
using ProjectMgmt.Modules.IdentityExperience.Notification.Application.Services;
using ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Repositories;
using ProjectMgmt.Modules.IdentityExperience.Notification.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.IdentityExperience;

public static class IdentityExperienceModuleRegistration
{
    public static IServiceCollection AddIdentityExperienceModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddProjectMgmtMySqlDbContext<IdentityExperienceDbContext>(
            configuration,
            environment,
            "__EFMigrationsHistory_IdentityExperience",
            typeof(IdentityExperienceDbContext).Assembly.GetName().Name!,
            "identity-experience");

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<IAiAssistRepository, EfAiAssistRepository>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IUserSkillService, UserSkillService>();
        services.AddScoped<INotificationSender, NotificationSender>();
        services.AddScoped<IAiBreakdownFeedbackExportService, AiBreakdownFeedbackExportService>();

        return services;
    }
}
