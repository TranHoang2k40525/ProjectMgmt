using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Application.IServices;
using IdentityExperience.Application.Services;
using IdentityExperience.Infrastructure;
using IdentityExperience.Infrastructure.IRepository;
using IdentityExperience.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Planning.Infrastructure;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.IdentityExperience.Application.Services;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;
using ProjectMgmt.Modules.Planning.ProjectManagement.Infrastructure.Repositories;
using ProjectMgmt.ProjectManagement.Contracts;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ProjectMgmt");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing ConnectionStrings:ProjectMgmt. Configure it with User Secrets or ConnectionStrings__ProjectMgmt.");
}

var serverVersionText = builder.Configuration["Database:ServerVersion"] ?? "8.0.46";
var serverVersion = new MySqlServerVersion(Version.Parse(serverVersionText));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ProjectMgmt API",
        Version = "v1",
        Description = "API cho hệ thống quản lý dự án ProjectMgmt."
    });
});
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<IdentityExperienceDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        mysql =>
        {
            mysql.MigrationsHistoryTable("__EFMigrationsHistory_IdentityExperience");
            mysql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        }));

builder.Services.AddDbContext<PlanningDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        mysql =>
        {
            mysql.MigrationsHistoryTable("__EFMigrationsHistory_Planning");
            mysql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        }));

builder.Services.AddDbContext<DeliveryIntelligenceDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        mysql =>
        {
            mysql.MigrationsHistoryTable("__EFMigrationsHistory_DeliveryIntelligence");
            mysql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        }));

// DI
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<IAccountServices, AccountServices>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectManagementService, ProjectManagementService>();
builder.Services.AddScoped<IProjectLookupService, ProjectLookupService>();
builder.Services.AddScoped<IUserLookupService, UserLookupService>();
builder.Services.AddScoped<IUserSkillService, UserLookupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProjectMgmt API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "ProjectMgmt API - Swagger";
    });
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsStaging())
{
    app.UseHttpsRedirection();
}

var frontendIndexPath = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
var hasFrontendArtifact = File.Exists(frontendIndexPath);
if (hasFrontendArtifact)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

if (hasFrontendArtifact)
{
    app.MapFallback(async context =>
    {
        var path = context.Request.Path;
        var isBackendPath = path.StartsWithSegments("/api")
            || path.StartsWithSegments("/health")
            || path.StartsWithSegments("/swagger")
            || path.StartsWithSegments("/hubs");

        if (!HttpMethods.IsGet(context.Request.Method) || isBackendPath)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(frontendIndexPath);
    });
}

app.Run();

public partial class Program;
