using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class SpecStoryLinkConfiguration : IEntityTypeConfiguration<SpecStoryLink>
{
    public void Configure(EntityTypeBuilder<SpecStoryLink> builder)
    {
        builder.ToTable("SpecStoryLink");
        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.DocumentId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.IssueId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.Excerpt).HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(e => new { e.DocumentId, e.IssueId }, "UQ_SpecStoryLink").IsUnique();
        builder.HasIndex(e => e.IssueId, "IX_SpecStoryLink_Issue");

        builder.HasOne<ProjectDocument>()
            .WithMany()
            .HasForeignKey(e => e.DocumentId)
            .HasConstraintName("FK_SpecStoryLink_Doc")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
