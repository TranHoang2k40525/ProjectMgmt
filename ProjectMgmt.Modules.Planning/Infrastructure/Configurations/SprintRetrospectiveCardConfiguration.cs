using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class SprintRetrospectiveCardConfiguration : IEntityTypeConfiguration<SprintRetrospectiveCard>
{
    public void Configure(EntityTypeBuilder<SprintRetrospectiveCard> builder)
    {
        builder.ToTable("SprintRetrospectiveCard");
        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.SprintId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ColumnType).HasColumnType("varchar(20)").IsRequired();
        builder.Property(e => e.Content).HasColumnType("text").IsRequired();
        builder.Property(e => e.IsAnonymous).HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(e => e.AuthorUserId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.VoteCount).HasColumnType("int").HasDefaultValue(0);
        builder.Property(e => e.ConvertedToIssueId).HasColumnType("char(36)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(e => e.SprintId, "IX_Retro_Sprint");

        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(e => e.SprintId)
            .HasConstraintName("FK_Retro_Sprint")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
