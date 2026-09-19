using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class UserWorkloadSnapshotConfiguration : IEntityTypeConfiguration<UserWorkloadSnapshot>
{
    public void Configure(EntityTypeBuilder<UserWorkloadSnapshot> builder)
    {
        builder.ToTable("UserWorkloadSnapshot", table =>
        {
            table.HasComment("Hangfire job hằng ngày. Đây là feature \"độ cân bằng khối lượng công việc\" mà đề xuất nhắc tới");
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
            .HasComment("XMOD -> Sprint.Id");

        builder.Property(entity => entity.SnapshotDate)
            .HasColumnName("SnapshotDate")
            .HasColumnType("date");

        builder.Property(entity => entity.OpenIssueCount)
            .HasColumnName("OpenIssueCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.InProgressCount)
            .HasColumnName("InProgressCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.OpenPoints)
            .HasColumnName("OpenPoints")
            .HasColumnType("decimal(9,2)")
            .HasComment("Tổng point chưa Done đang gánh")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.InProgressPoints)
            .HasColumnName("InProgressPoints")
            .HasColumnType("decimal(9,2)")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.OverdueCount)
            .HasColumnName("OverdueCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.CapacityPoints)
            .HasColumnName("CapacityPoints")
            .HasColumnType("decimal(9,2)")
            .HasComment("Lấy từ SprintMemberCapacity");

        builder.Property(entity => entity.UtilizationRatio)
            .HasColumnName("UtilizationRatio")
            .HasColumnType("decimal(6,3)")
            .HasComment("OpenPoints / CapacityPoints. > 1.0 = quá tải");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.UserId, entity.ProjectId, entity.SnapshotDate }, "UQ_UserWorkloadSnapshot").IsUnique();

        builder.HasIndex(entity => new { entity.ProjectId, entity.SnapshotDate }, "IX_UserWorkload_Project_Date");

        builder.HasIndex(entity => new { entity.SprintId, entity.SnapshotDate }, "IX_UserWorkload_Sprint_Date");
    }
}
