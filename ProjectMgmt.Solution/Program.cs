using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ProjectMgmt.BuildingBlocks.Web;
using ProjectMgmt.Modules.DeliveryIntelligence;
using ProjectMgmt.Modules.IdentityExperience;
using ProjectMgmt.Modules.Planning;
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
    .AddIdentityExperienceModule(builder.Configuration, builder.Environment)
    .AddPlanningModule(builder.Configuration, builder.Environment)
    .AddDeliveryIntelligenceModule(builder.Configuration, builder.Environment);

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
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    data = entry.Value.Data
                })
        });
    }
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => !registration.Tags.Contains("ready")
});
app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "ScrumAI Project Management",
    version = "0.2.0-modular-monolith",
    utc = DateTimeOffset.UtcNow
}));

app.Run();

public partial class Program;
