using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IdentityExperience.Infrastructure;

public class IdentityExperienceDbContextFactory : IDesignTimeDbContextFactory<IdentityExperienceDbContext>
{
    public IdentityExperienceDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var options = new DbContextOptionsBuilder<IdentityExperienceDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 46)),
                mysql => mysql.MigrationsHistoryTable("__EFMigrationsHistory_IdentityExperience"))
            .Options;

        return new IdentityExperienceDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        var candidatePaths = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "ProjectMgmt.Solution"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "ProjectMgmt.Solution"),
            AppContext.BaseDirectory
        };

        foreach (var basePath in candidatePaths)
        {
            if (!Directory.Exists(basePath))
            {
                continue;
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile("local.settings.json", optional: true)
                .AddEnvironmentVariables();

            var config = builder.Build();
            var connectionString = config.GetConnectionString("ProjectMgmt") ?? config["ConnectionStrings:ProjectMgmt"];
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }
        }

        var env = Environment.GetEnvironmentVariable("ConnectionStrings__ProjectMgmt");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        throw new InvalidOperationException(
            "Missing ConnectionStrings:ProjectMgmt in appsettings.Development.json, appsettings.json, or local.settings.json.");
    }
}
