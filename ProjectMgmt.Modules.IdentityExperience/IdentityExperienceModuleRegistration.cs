using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.AiAssist.Contracts;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Notification.Contracts;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Application.Services;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.IRepositories;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Infrastructure.Repositories;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Application.Services;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.IRepositories;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Infrastructure.Repositories;
using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;
using ProjectMgmt.Modules.IdentityExperience.Notification.Application.Services;
using ProjectMgmt.Modules.IdentityExperience.Notification.Domain.IRepositories;
using ProjectMgmt.Modules.IdentityExperience.Notification.Infrastructure.Repositories;

namespace ProjectMgmt.Modules.IdentityExperience;

public static class IdentityExperienceModuleRegistration
{
    public static IServiceCollection AddIdentityExperienceModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddIdentityExperienceDatabase(configuration, environment);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAiAssistRepository, AiAssistRepository>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IUserSkillService, UserSkillService>();
        services.AddScoped<INotificationSender, NotificationSender>();
        services.AddScoped<IAiBreakdownFeedbackExportService, AiBreakdownFeedbackExportService>();

        return services;
    }
}
