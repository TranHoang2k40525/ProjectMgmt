using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.AiDataOps.Contracts;
using ProjectMgmt.BuildingBlocks.Persistence;
using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Repositories;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Infrastructure.Persistence;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Application.Services;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Repositories;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Infrastructure.Persistence;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Application.Services;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Repositories;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.DeliveryIntelligence;

public static class DeliveryIntelligenceModuleRegistration
{
    public static IServiceCollection AddDeliveryIntelligenceModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddProjectMgmtMySqlDbContext<DeliveryIntelligenceDbContext>(
            configuration,
            environment,
            "__EFMigrationsHistory_DeliveryIntelligence",
            typeof(DeliveryIntelligenceDbContext).Assembly.GetName().Name!,
            "delivery-intelligence");

        services.AddScoped<IIssueRepository, EfIssueRepository>();
        services.AddScoped<IAiModelRepository, EfAiModelRepository>();
        services.AddScoped<IAiDatasetRepository, EfAiDatasetRepository>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IIssueReadService, IssueReadService>();
        services.AddScoped<IIssueSprintService, IssueSprintService>();
        services.AddScoped<IIssueSkillService, IssueSkillService>();
        services.AddScoped<IIssueWorkInProgressCounter, IssueWorkInProgressCounter>();
        services.AddScoped<IEvaluationSetCatalog, EvaluationSetCatalog>();

        return services;
    }
}
