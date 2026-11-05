using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.ToTable("ProjectDocument", table =>
        {
            table.HasComment("Kho tài liệu đặc tả dự án (Spec Hub - Nguồn sự thật)");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ParentId).HasColumnType("char(36)");
        builder.Property(e => e.Name).HasColumnType("varchar(255)").HasDefaultValue("");
        builder.Property(e => e.RelativePath).HasColumnType("varchar(1000)").HasDefaultValue("");
        builder.Property(e => e.IsDirectory).HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(e => e.Extension).HasColumnType("varchar(20)");
        builder.Property(e => e.FileSize).HasColumnType("bigint").HasDefaultValue(0);
        builder.Property(e => e.Title).HasColumnType("varchar(250)").IsRequired();
        builder.Property(e => e.Content).HasColumnType("longtext");
        builder.Property(e => e.Status).HasColumnType("varchar(20)").HasDefaultValue("Draft").IsRequired();
        builder.Property(e => e.Version).HasColumnType("int").HasDefaultValue(1);
        builder.Property(e => e.CreatedByUserId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ApprovedByUserId).HasColumnType("char(36)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");

        builder.HasIndex(e => e.ProjectId, "IX_ProjectDocument_Project");
        builder.HasIndex(e => e.ParentId, "IX_ProjectDocument_Parent");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .HasConstraintName("FK_ProjectDocument_Project")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
