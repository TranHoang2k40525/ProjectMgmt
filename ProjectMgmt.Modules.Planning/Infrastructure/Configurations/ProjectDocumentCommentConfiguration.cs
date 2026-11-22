using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class ProjectDocumentCommentConfiguration : IEntityTypeConfiguration<ProjectDocumentComment>
{
    public void Configure(EntityTypeBuilder<ProjectDocumentComment> builder)
    {
        builder.ToTable("ProjectDocumentComment");
        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.DocumentId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.UserId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.HighlightedText).HasColumnType("text");
        builder.Property(e => e.Content).HasColumnType("text").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(e => e.DocumentId, "IX_DocComment_Doc");

        builder.HasOne<ProjectDocument>()
            .WithMany()
            .HasForeignKey(e => e.DocumentId)
            .HasConstraintName("FK_DocComment_Document")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
