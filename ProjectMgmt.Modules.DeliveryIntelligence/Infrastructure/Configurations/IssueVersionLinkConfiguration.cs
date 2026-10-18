using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class IssueVersionLinkConfiguration : IEntityTypeConfiguration<IssueVersionLink>
{
    public void Configure(EntityTypeBuilder<IssueVersionLink> builder)
    {
        builder.ToTable("IssueVersionLink", table =>
        {
            table.HasComment("BỔ SUNG: bảng nối Issue <-> Version cho release note");
            table.HasCheckConstraint("CK_IssueVersion_LinkType", "`LinkType` IN ('FixVersion','AffectsVersion')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => new { entity.IssueId, entity.VersionId, entity.LinkType }).HasName("PRIMARY");

        builder.Property(entity => entity.IssueId)
            .HasColumnName("IssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.VersionId)
            .HasColumnName("VersionId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> ProjectVersion.Id");

        builder.Property(entity => entity.LinkType)
            .HasColumnName("LinkType")
            .HasColumnType("varchar(20)")
            .HasComment("FixVersion / AffectsVersion")
            .HasDefaultValue("FixVersion")
            .IsRequired();

        builder.HasIndex(entity => entity.VersionId, "IX_IssueVersion_Version");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_IssueVersion_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
