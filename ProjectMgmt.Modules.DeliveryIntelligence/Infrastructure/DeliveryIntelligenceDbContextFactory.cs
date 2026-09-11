using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeliveryIntelligence.Infrastructure;

public class DeliveryIntelligenceDbContextFactory : IDesignTimeDbContextFactory<DeliveryIntelligenceDbContext>
{
    public DeliveryIntelligenceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ProjectMgmt");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set environment variable ConnectionStrings__ProjectMgmt before running dotnet ef.");
        }

        var options = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 46)),
                mysql => mysql.MigrationsHistoryTable("__EFMigrationsHistory_DeliveryIntelligence"))
            .Options;

        return new DeliveryIntelligenceDbContext(options);
    }
}
