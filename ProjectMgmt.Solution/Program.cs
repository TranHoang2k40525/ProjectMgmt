using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ProjectMgmt.AiAssignment;
using ProjectMgmt.AiAssist;
using ProjectMgmt.AiCore;
using ProjectMgmt.AiDataOps;
using ProjectMgmt.BuildingBlocks.Web;
using ProjectMgmt.IdentityAccess;
using ProjectMgmt.IssueTracking;
using ProjectMgmt.Notification;
using ProjectMgmt.ProjectManagement;
using ProjectMgmt.SprintBacklog;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddProjectMgmtWebCommon();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("AngularDevelopment", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

builder.Services
    .AddIdentityAccessModule(builder.Configuration, builder.Environment)
    .AddProjectManagementModule(builder.Configuration, builder.Environment)
    .AddSprintBacklogModule(builder.Configuration, builder.Environment)
    .AddIssueTrackingModule(builder.Configuration, builder.Environment)
    .AddAiCoreModule(builder.Configuration, builder.Environment)
    .AddAiAssistModule(builder.Configuration, builder.Environment)
    .AddAiAssignmentModule(builder.Configuration, builder.Environment)
    .AddAiDataOpsModule(builder.Configuration, builder.Environment)
    .AddNotificationModule(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseProjectMgmtWebCommon();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AngularDevelopment");

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => !registration.Tags.Contains("ready")
});
app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "ScrumAI Project Management",
    version = "0.1.0-foundation",
    utc = DateTimeOffset.UtcNow
}));

app.Run();

public partial class Program;
