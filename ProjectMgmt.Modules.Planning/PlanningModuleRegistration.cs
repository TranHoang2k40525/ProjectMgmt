using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.AiAssignment.Contracts;
using ProjectMgmt.BuildingBlocks.Persistence;
using ProjectMgmt.Modules.Planning.AiAssignment.Application.Services;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Repositories;
using ProjectMgmt.Modules.Planning.AiAssignment.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Repositories;
using ProjectMgmt.Modules.Planning.ProjectManagement.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Repositories;
using ProjectMgmt.Modules.Planning.SprintBacklog.Application.Services;
using ProjectMgmt.Modules.Planning.SprintBacklog.Infrastructure.Persistence;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Modules.Planning;

public static class PlanningModuleRegistration
{
    public static IServiceCollection AddPlanningModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddProjectMgmtMySqlDbContext<PlanningDbContext>(
            configuration,
            environment,
            "__EFMigrationsHistory_Planning",
            typeof(PlanningDbContext).Assembly.GetName().Name!,
            "planning");

        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<ISprintRepository, EfSprintRepository>();
        services.AddScoped<IAiAssignmentRepository, EfAiAssignmentRepository>();
        services.AddScoped<IProjectLookupService, ProjectLookupService>();
        services.AddScoped<IWorkflowValidationService, WorkflowValidationService>();
        services.AddScoped<IIssueNumberGenerator, IssueNumberGenerator>();
        services.AddScoped<ISprintLookupService, SprintLookupService>();
        services.AddScoped<IAiAssignmentFeedbackExportService, AiAssignmentFeedbackExportService>();

        return services;
    }
}
