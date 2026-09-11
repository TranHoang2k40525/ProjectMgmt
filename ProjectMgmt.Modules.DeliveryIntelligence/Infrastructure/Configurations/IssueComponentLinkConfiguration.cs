using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class IssueComponentLinkConfiguration : IEntityTypeConfiguration<IssueComponentLink>
{
    public void Configure(EntityTypeBuilder<IssueComponentLink> builder)
    {
        builder.ToTable("IssueComponentLink", table =>
        {
            table.HasComment("BỔ SUNG: v2.0 yêu cầu báo cáo theo Component nhưng thiếu bảng nối này");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => new { entity.IssueId, entity.ComponentId }).HasName("PRIMARY");

        builder.Property(entity => entity.IssueId)
            .HasColumnName("IssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ComponentId)
            .HasColumnName("ComponentId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> ProjectComponent.Id");

        builder.HasIndex(entity => entity.ComponentId, "IX_IssueComponent_Component");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_IssueComponent_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
