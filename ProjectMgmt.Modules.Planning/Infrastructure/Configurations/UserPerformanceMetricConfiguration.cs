using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class UserPerformanceMetricConfiguration : IEntityTypeConfiguration<UserPerformanceMetric>
{
    public void Configure(EntityTypeBuilder<UserPerformanceMetric> builder)
    {
        builder.ToTable("UserPerformanceMetric", table =>
        {
            table.HasComment("Feature \"năng lực thực tế\" cho AI phân phối task");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.ProjectId)
            .HasColumnName("ProjectId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Project.Id");

        builder.Property(entity => entity.SprintId)
            .HasColumnName("SprintId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Sprint.Id. NULL = số liệu tổng toàn project");

        builder.Property(entity => entity.PeriodStart)
            .HasColumnName("PeriodStart")
            .HasColumnType("date");

        builder.Property(entity => entity.PeriodEnd)
            .HasColumnName("PeriodEnd")
            .HasColumnType("date");

        builder.Property(entity => entity.AssignedIssueCount)
            .HasColumnName("AssignedIssueCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.CompletedIssueCount)
            .HasColumnName("CompletedIssueCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.CommittedPoints)
            .HasColumnName("CommittedPoints")
            .HasColumnType("decimal(9,2)")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.CompletedPoints)
            .HasColumnName("CompletedPoints")
            .HasColumnType("decimal(9,2)")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.AvgCycleTimeHours)
            .HasColumnName("AvgCycleTimeHours")
            .HasColumnType("decimal(9,2)")
            .HasComment("Trung bình từ InProgress -> Done");

        builder.Property(entity => entity.MedianCycleTimeHours)
            .HasColumnName("MedianCycleTimeHours")
            .HasColumnType("decimal(9,2)")
            .HasComment("Median chống ảnh hưởng của outlier tốt hơn Avg");

        builder.Property(entity => entity.OnTimeRatio)
            .HasColumnName("OnTimeRatio")
            .HasColumnType("decimal(5,4)")
            .HasComment("Tỉ lệ hoàn thành trước DueDate");

        builder.Property(entity => entity.ReopenedCount)
            .HasColumnName("ReopenedCount")
            .HasColumnType("int")
            .HasComment("Proxy cho chất lượng công việc")
            .HasDefaultValue(0);

        builder.Property(entity => entity.EstimateAccuracyRatio)
            .HasColumnName("EstimateAccuracyRatio")
            .HasColumnType("decimal(6,3)")
            .HasComment("TimeSpent / OriginalEstimate");

        builder.Property(entity => entity.CalculatedAt)
            .HasColumnName("CalculatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.UserId, entity.ProjectId, entity.PeriodStart, entity.PeriodEnd }, "UQ_UserPerfMetric").IsUnique();

        builder.HasIndex(entity => entity.SprintId, "IX_UserPerfMetric_Sprint");

        builder.HasIndex(entity => new { entity.ProjectId, entity.PeriodEnd }, "IX_UserPerfMetric_Project_Period");
    }
}
