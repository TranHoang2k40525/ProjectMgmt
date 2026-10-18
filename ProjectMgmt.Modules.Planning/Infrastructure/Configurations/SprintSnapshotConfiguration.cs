using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class SprintSnapshotConfiguration : IEntityTypeConfiguration<SprintSnapshot>
{
    public void Configure(EntityTypeBuilder<SprintSnapshot> builder)
    {
        builder.ToTable("SprintSnapshot", table =>
        {
            table.HasComment("Ảnh chụp hằng ngày cho Burndown - không thể tính on-the-fly");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SprintId)
            .HasColumnName("SprintId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SnapshotDate)
            .HasColumnName("SnapshotDate")
            .HasColumnType("date");

        builder.Property(entity => entity.TotalPoints)
            .HasColumnName("TotalPoints")
            .HasColumnType("decimal(9,2)")
            .HasComment("BỔ SUNG: để vẽ được ideal line")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.RemainingPoints)
            .HasColumnName("RemainingPoints")
            .HasColumnType("decimal(9,2)")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.CompletedPoints)
            .HasColumnName("CompletedPoints")
            .HasColumnType("decimal(9,2)")
            .HasComment("BỔ SUNG")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.AddedPoints)
            .HasColumnName("AddedPoints")
            .HasColumnType("decimal(9,2)")
            .HasComment("BỔ SUNG: scope change trong sprint")
            .HasDefaultValue(0m);

        builder.Property(entity => entity.RemainingIssueCount)
            .HasColumnName("RemainingIssueCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.TotalIssueCount)
            .HasColumnName("TotalIssueCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.SprintId, entity.SnapshotDate }, "UQ_SprintSnapshot_Day").IsUnique();

        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(entity => entity.SprintId)
            .HasConstraintName("FK_SprintSnapshot_Sprint")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
