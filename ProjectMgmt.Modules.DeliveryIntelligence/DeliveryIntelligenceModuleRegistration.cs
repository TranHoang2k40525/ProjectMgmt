using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.AiDataOps.Contracts;
using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.IRepositories;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Infrastructure.Repositories;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Application.Services;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.IRepositories;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Infrastructure.Repositories;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Application.Services;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.IRepositories;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Infrastructure.Repositories;

namespace ProjectMgmt.Modules.DeliveryIntelligence;

public static class DeliveryIntelligenceModuleRegistration
{
    public static IServiceCollection AddDeliveryIntelligenceModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDeliveryIntelligenceDatabase(configuration, environment);

        services.AddScoped<IIssueRepository, IssueRepository>();
        services.AddScoped<IAiModelRepository, AiModelRepository>();
        services.AddScoped<IAiDatasetRepository, AiDatasetRepository>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IIssueReadService, IssueReadService>();
        services.AddScoped<IIssueSprintService, IssueSprintService>();
        services.AddScoped<IIssueSkillService, IssueSkillService>();
        services.AddScoped<IIssueWorkInProgressCounter, IssueWorkInProgressCounter>();
        services.AddScoped<IEvaluationSetCatalog, EvaluationSetCatalog>();

        return services;
    }
}
