using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiDatasetSampleConfiguration : IEntityTypeConfiguration<AiDatasetSample>
{
    public void Configure(EntityTypeBuilder<AiDatasetSample> builder)
    {
        builder.ToTable("AiDatasetSample", table =>
        {
            table.HasComment("Từng mẫu huấn luyện. Export ra JSONL từ bảng này");
            table.HasCheckConstraint("CK_AiDatasetSample_Split", "`SplitType` IN ('Train','Validation','Test')");
            table.HasCheckConstraint("CK_AiDatasetSample_Quality", "`QualityStatus` IN ('Raw','Cleaned','Approved','Rejected')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.DatasetVersionId)
            .HasColumnName("DatasetVersionId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SourceType)
            .HasColumnName("SourceType")
            .HasColumnType("varchar(30)")
            .HasComment("AiSuggestedTask / AiAssignmentDecision / ManualCurated / Synthetic")
            .IsRequired();

        builder.Property(entity => entity.SourceRefId)
            .HasColumnName("SourceRefId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD: Id của bản ghi nguồn");

        builder.Property(entity => entity.Instruction)
            .HasColumnName("Instruction")
            .HasColumnType("mediumtext")
            .IsRequired();

        builder.Property(entity => entity.InputJson)
            .HasColumnName("InputJson")
            .HasColumnType("json")
            .HasComment("Với Breakdown: {title, description, projectContext}. Với Assignment: {issue_features, candidates[]}")
            .IsRequired();

        builder.Property(entity => entity.OutputJson)
            .HasColumnName("OutputJson")
            .HasColumnType("json")
            .HasComment("Đầu ra CHUẨN đã qua người duyệt")
            .IsRequired();

        builder.Property(entity => entity.Language)
            .HasColumnName("Language")
            .HasColumnType("varchar(10)")
            .HasDefaultValue("vi")
            .IsRequired();

        builder.Property(entity => entity.TokenCount)
            .HasColumnName("TokenCount")
            .HasColumnType("int")
            .HasComment("Loại bỏ mẫu vượt context window");

        builder.Property(entity => entity.SplitType)
            .HasColumnName("SplitType")
            .HasColumnType("varchar(20)")
            .HasComment("Train / Validation / Test")
            .HasDefaultValue("Train")
            .IsRequired();

        builder.Property(entity => entity.QualityStatus)
            .HasColumnName("QualityStatus")
            .HasColumnType("varchar(20)")
            .HasComment("Raw -> Cleaned -> Approved / Rejected")
            .HasDefaultValue("Raw")
            .IsRequired();

        builder.Property(entity => entity.QualityScore)
            .HasColumnName("QualityScore")
            .HasColumnType("decimal(4,3)")
            .HasComment("0..1 do người duyệt chấm");

        builder.Property(entity => entity.IsPiiRedacted)
            .HasColumnName("IsPiiRedacted")
            .HasColumnType("tinyint(1)")
            .HasComment("BẮT BUỘC = 1 trước khi Approved. Story thật hay chứa tên KH, email, tên hệ thống nội bộ")
            .HasDefaultValue(false);

        builder.Property(entity => entity.ContentHash)
            .HasColumnName("ContentHash")
            .HasColumnType("char(64)")
            .HasComment("SHA-256 của Instruction+Input+Output - khử trùng lặp")
            .IsRequired();

        builder.Property(entity => entity.ReviewStatus)
            .HasColumnName("ReviewStatus")
            .HasColumnType("varchar(20)")
            .HasComment("NotReviewed / Approved / Rejected / NeedsFix")
            .HasDefaultValue("NotReviewed")
            .IsRequired();

        builder.Property(entity => entity.ReviewedBy)
            .HasColumnName("ReviewedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.ReviewedAt)
            .HasColumnName("ReviewedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.ReviewNote)
            .HasColumnName("ReviewNote")
            .HasColumnType("varchar(1000)");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => new { entity.DatasetVersionId, entity.ContentHash }, "UQ_AiDatasetSample_Dedup").IsUnique();

        builder.HasIndex(entity => new { entity.DatasetVersionId, entity.SplitType, entity.QualityStatus }, "IX_AiDatasetSample_Split");

        builder.HasIndex(entity => new { entity.SourceType, entity.SourceRefId }, "IX_AiDatasetSample_Source");

        builder.HasIndex(entity => new { entity.ReviewStatus, entity.QualityStatus, entity.CreatedAt }, "IX_AiDatasetSample_ReviewQueue");

        builder.HasIndex(entity => new { entity.DatasetVersionId, entity.ReviewStatus, entity.QualityStatus, entity.CreatedAt }, "IX_AiDatasetSample_Version_Review");

        builder.HasIndex(entity => new { entity.DatasetVersionId, entity.TokenCount }, "IX_AiDatasetSample_TokenCount");

        builder.HasIndex(entity => entity.Instruction, "FT_AiDatasetSample_Instruction").IsFullText();

        builder.HasOne<AiDatasetVersion>()
            .WithMany()
            .HasForeignKey(entity => entity.DatasetVersionId)
            .HasConstraintName("FK_AiDatasetSample_Version")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
