using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprint", table =>
        {
            table.HasComment("Chu kỳ Scrum. UNIQUE trên ActiveGuard ép ràng buộc 1 Sprint Active/Project");
            table.HasCheckConstraint("CK_Sprint_Status", "`Status` IN ('Planned','Active','Completed')");
            table.HasCheckConstraint("CK_Sprint_Dates", "`EndDate` IS NULL OR `StartDate` IS NULL OR `EndDate` >= `StartDate`");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ProjectId)
            .HasColumnName("ProjectId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Project.Id");

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(150)")
            .IsRequired();

        builder.Property(entity => entity.Goal)
            .HasColumnName("Goal")
            .HasColumnType("varchar(1000)")
            .HasComment("Sprint Goal");

        builder.Property(entity => entity.StartDate)
            .HasColumnName("StartDate")
            .HasColumnType("date");

        builder.Property(entity => entity.EndDate)
            .HasColumnName("EndDate")
            .HasColumnType("date");

        builder.Property(entity => entity.ActualStartAt)
            .HasColumnName("ActualStartAt")
            .HasColumnType("datetime(6)")
            .HasComment("Thời điểm bấm Start thật");

        builder.Property(entity => entity.ActualCompleteAt)
            .HasColumnName("ActualCompleteAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.Status)
            .HasColumnName("Status")
            .HasColumnType("varchar(20)")
            .HasComment("Planned / Active / Completed")
            .HasDefaultValue("Planned")
            .IsRequired();

        builder.Property(entity => entity.OrderIndex)
            .HasColumnName("OrderIndex")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.Property(entity => entity.ActiveGuard)
            .HasColumnName("ActiveGuard")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComputedColumnSql("IF(`Status` = 'Active', `ProjectId`, NULL)", stored: true)
            .ValueGeneratedOnAddOrUpdate();
        builder.Property(entity => entity.ActiveGuard).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => entity.ActiveGuard, "UQ_Sprint_OneActivePerProject").IsUnique();

        builder.HasIndex(entity => new { entity.ProjectId, entity.Status }, "IX_Sprint_Project_Status");

        builder.HasIndex(entity => entity.Name, "IX_Sprint_Name");

        builder.HasIndex(entity => new { entity.ProjectId, entity.OrderIndex }, "IX_Sprint_Project_Order");

        builder.HasIndex(entity => new { entity.Status, entity.EndDate }, "IX_Sprint_Status_EndDate");

        builder.HasIndex(entity => new { entity.ProjectId, entity.StartDate, entity.EndDate }, "IX_Sprint_Project_Dates");
    }
}
