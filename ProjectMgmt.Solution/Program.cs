using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;

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
builder.Services.AddOpenApi();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

app.Run();

public partial class Program;
