using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class SprintMemberCapacityConfiguration : IEntityTypeConfiguration<SprintMemberCapacity>
{
    public void Configure(EntityTypeBuilder<SprintMemberCapacity> builder)
    {
        builder.ToTable("SprintMemberCapacity", table =>
        {
            table.HasComment("BỔ SUNG - AI ASSIGNMENT: trần công suất từng người, tránh gán quá tải");
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

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.CapacityPoints)
            .HasColumnName("CapacityPoints")
            .HasColumnType("decimal(9,2)")
            .HasComment("Số point tối đa nhận được trong sprint này");

        builder.Property(entity => entity.AvailableHours)
            .HasColumnName("AvailableHours")
            .HasColumnType("decimal(7,2)")
            .HasComment("Trừ nghỉ phép, họp, on-call");

        builder.Property(entity => entity.Note)
            .HasColumnName("Note")
            .HasColumnType("varchar(255)");

        builder.HasIndex(entity => new { entity.SprintId, entity.UserId }, "UQ_SprintCapacity").IsUnique();

        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(entity => entity.SprintId)
            .HasConstraintName("FK_SprintCapacity_Sprint")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
