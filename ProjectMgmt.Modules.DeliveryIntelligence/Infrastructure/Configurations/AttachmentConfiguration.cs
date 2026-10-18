using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachment", table =>
        {
            table.HasComment("File đính kèm");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.IssueId)
            .HasColumnName("IssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.UploadedBy)
            .HasColumnName("UploadedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.FileName)
            .HasColumnName("FileName")
            .HasColumnType("varchar(255)")
            .HasComment("Tên file gốc")
            .IsRequired();

        builder.Property(entity => entity.StoredPath)
            .HasColumnName("StoredPath")
            .HasColumnType("varchar(500)")
            .HasComment("Đường dẫn local / S3 key")
            .IsRequired();

        builder.Property(entity => entity.ContentType)
            .HasColumnName("ContentType")
            .HasColumnType("varchar(120)")
            .HasComment("BỔ SUNG: cần cho Content-Disposition khi tải về")
            .IsRequired();

        builder.Property(entity => entity.FileSizeBytes)
            .HasColumnName("FileSizeBytes")
            .HasColumnType("bigint")
            .HasComment("BỔ SUNG: kiểm tra quota");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => entity.IssueId, "IX_Attachment_Issue");

        builder.HasIndex(entity => entity.FileName, "IX_Attachment_FileName");

        builder.HasIndex(entity => entity.CreatedAt, "IX_Attachment_CreatedAt");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_Attachment_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
