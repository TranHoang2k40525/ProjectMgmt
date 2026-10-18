using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class PriorityConfiguration : IEntityTypeConfiguration<Priority>
{
    public void Configure(EntityTypeBuilder<Priority> builder)
    {
        builder.ToTable("Priority", table =>
        {
            table.HasComment("Mức độ ưu tiên");
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
            .HasComment("SỬA: NULL = mức ưu tiên dùng chung toàn hệ thống; có giá trị = riêng project (khớp Mục 5.2)");

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(40)")
            .IsRequired();

        builder.Property(entity => entity.Level)
            .HasColumnName("Level")
            .HasColumnType("int")
            .HasComment("1 = Highest ... 5 = Lowest");

        builder.Property(entity => entity.ColorHex)
            .HasColumnName("ColorHex")
            .HasColumnType("char(7)")
            .HasDefaultValue("#6B778C")
            .IsRequired();

        builder.Property(entity => entity.IconKey)
            .HasColumnName("IconKey")
            .HasColumnType("varchar(50)");

        builder.HasIndex(entity => new { entity.ProjectId, entity.Level }, "IX_Priority_Project");

        builder.HasIndex(entity => entity.Name, "IX_Priority_Name");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("FK_Priority_Project")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
