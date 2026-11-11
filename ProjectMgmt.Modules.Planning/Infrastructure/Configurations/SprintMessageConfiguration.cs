using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class SprintMessageConfiguration : IEntityTypeConfiguration<SprintMessage>
{
    public void Configure(EntityTypeBuilder<SprintMessage> builder)
    {
        builder.ToTable("SprintMessage");
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
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();

        builder.Property(entity => entity.Content)
            .HasColumnName("Content")
            .HasColumnType("text")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.IsDeleted)
            .HasColumnName("IsDeleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.HasIndex(entity => entity.SprintId).HasDatabaseName("IX_SprintMessage_SprintId");
        builder.HasIndex(entity => entity.UserId).HasDatabaseName("IX_SprintMessage_UserId");
    }
}
