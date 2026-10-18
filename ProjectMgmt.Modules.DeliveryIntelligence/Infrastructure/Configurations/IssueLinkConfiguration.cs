using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class IssueLinkConfiguration : IEntityTypeConfiguration<IssueLink>
{
    public void Configure(EntityTypeBuilder<IssueLink> builder)
    {
        builder.ToTable("IssueLink", table =>
        {
            table.HasComment("Quan hệ ngang giữa issue. Tạo Blocks thì tự sinh bản ghi IsBlockedBy ngược chiều");
            table.HasCheckConstraint("CK_IssueLink_NotSelf", "`SourceIssueId` <> `TargetIssueId`");
            table.HasCheckConstraint("CK_IssueLink_Type", "`LinkType` IN ('Blocks','IsBlockedBy','Relates','Duplicates','IsDuplicatedBy')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SourceIssueId)
            .HasColumnName("SourceIssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.TargetIssueId)
            .HasColumnName("TargetIssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.LinkType)
            .HasColumnName("LinkType")
            .HasColumnType("varchar(30)")
            .HasComment("Blocks / IsBlockedBy / Relates / Duplicates / IsDuplicatedBy")
            .IsRequired();

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("CreatedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.SourceIssueId, entity.TargetIssueId, entity.LinkType }, "UQ_IssueLink").IsUnique();

        builder.HasIndex(entity => entity.TargetIssueId, "IX_IssueLink_Target");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.SourceIssueId)
            .HasConstraintName("FK_IssueLink_Source")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.TargetIssueId)
            .HasConstraintName("FK_IssueLink_Target")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
