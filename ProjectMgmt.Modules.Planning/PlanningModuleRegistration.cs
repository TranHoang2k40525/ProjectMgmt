using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectMgmt.AiAssignment.Contracts;
using ProjectMgmt.Modules.Planning.AiAssignment.Application.Services;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.IRepositories;
using ProjectMgmt.Modules.Planning.AiAssignment.Infrastructure.Repositories;
using ProjectMgmt.Modules.Planning.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;
using ProjectMgmt.Modules.Planning.ProjectManagement.Infrastructure.Repositories;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.IRepositories;
using ProjectMgmt.Modules.Planning.SprintBacklog.Application.Services;
using ProjectMgmt.Modules.Planning.SprintBacklog.Infrastructure.Repositories;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Modules.Planning;

public static class PlanningModuleRegistration
{
    public static IServiceCollection AddPlanningModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddPlanningDatabase(configuration, environment);

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<IAiAssignmentRepository, AiAssignmentRepository>();
        services.AddScoped<IProjectLookupService, ProjectLookupService>();
        services.AddScoped<IWorkflowValidationService, WorkflowValidationService>();
        services.AddScoped<IIssueNumberGenerator, IssueNumberGenerator>();
        services.AddScoped<ISprintLookupService, SprintLookupService>();
        services.AddScoped<IAiAssignmentFeedbackExportService, AiAssignmentFeedbackExportService>();

        return services;
    }
}
