using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Planning.Infrastructure;

public class PlanningDbContextFactory : IDesignTimeDbContextFactory<PlanningDbContext>
{
    public PlanningDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ProjectMgmt");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set environment variable ConnectionStrings__ProjectMgmt before running dotnet ef.");
        }

        var options = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 46)),
                mysql => mysql.MigrationsHistoryTable("__EFMigrationsHistory_Planning"))
            .Options;

        return new PlanningDbContext(options);
    }
}
