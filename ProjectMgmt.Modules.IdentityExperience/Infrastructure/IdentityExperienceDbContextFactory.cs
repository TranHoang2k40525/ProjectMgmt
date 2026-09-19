using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IdentityExperience.Infrastructure;

public class IdentityExperienceDbContextFactory : IDesignTimeDbContextFactory<IdentityExperienceDbContext>
{
    public IdentityExperienceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ProjectMgmt");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set environment variable ConnectionStrings__ProjectMgmt before running dotnet ef.");
        }

        var options = new DbContextOptionsBuilder<IdentityExperienceDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 46)),
                mysql => mysql.MigrationsHistoryTable("__EFMigrationsHistory_IdentityExperience"))
            .Options;

        return new IdentityExperienceDbContext(options);
    }
}
