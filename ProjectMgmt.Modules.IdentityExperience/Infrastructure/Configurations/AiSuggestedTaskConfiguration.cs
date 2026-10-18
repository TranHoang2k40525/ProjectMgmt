using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class AiSuggestedTaskConfiguration : IEntityTypeConfiguration<AiSuggestedTask>
{
    public void Configure(EntityTypeBuilder<AiSuggestedTask> builder)
    {
        builder.ToTable("AiSuggestedTask", table =>
        {
            table.HasComment("BẢNG CỐT LÕI CHO FINE-TUNE. Chỉ lưu RawResponseJson như thiết kế v2.0 thì KHÔNG BAO GIỜ biết người dùng đã sửa gì -> mất sạch tín hiệu huấn luyện");
            table.HasCheckConstraint("CK_AiSuggestedTask_Action", "`UserAction` IN ('Pending','Kept','Edited','Rejected')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.AiGenerationLogId)
            .HasColumnName("AiGenerationLogId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.OrderIndex)
            .HasColumnName("OrderIndex")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.OriginalSummary)
            .HasColumnName("OriginalSummary")
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(entity => entity.OriginalDescription)
            .HasColumnName("OriginalDescription")
            .HasColumnType("text");

        builder.Property(entity => entity.OriginalAcceptanceCriteria)
            .HasColumnName("OriginalAcceptanceCriteria")
            .HasColumnType("json")
            .HasComment("Mảng chuỗi AC do AI sinh");

        builder.Property(entity => entity.OriginalEstimatePoints)
            .HasColumnName("OriginalEstimatePoints")
            .HasColumnType("decimal(6,2)");

        builder.Property(entity => entity.OriginalSuggestedSkills)
            .HasColumnName("OriginalSuggestedSkills")
            .HasColumnType("json")
            .HasComment("AI gợi ý skill cần có -> nạp vào IssueRequiredSkill");

        builder.Property(entity => entity.FinalSummary)
            .HasColumnName("FinalSummary")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.FinalDescription)
            .HasColumnName("FinalDescription")
            .HasColumnType("text");

        builder.Property(entity => entity.FinalAcceptanceCriteria)
            .HasColumnName("FinalAcceptanceCriteria")
            .HasColumnType("json");

        builder.Property(entity => entity.FinalEstimatePoints)
            .HasColumnName("FinalEstimatePoints")
            .HasColumnType("decimal(6,2)");

        builder.Property(entity => entity.UserAction)
            .HasColumnName("UserAction")
            .HasColumnType("varchar(20)")
            .HasComment("Pending / Kept / Edited / Rejected — ĐÂY LÀ NHÃN HUẤN LUYỆN QUAN TRỌNG NHẤT")
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(entity => entity.EditDistanceRatio)
            .HasColumnName("EditDistanceRatio")
            .HasColumnType("decimal(5,4)")
            .HasComment("0.0 = giữ nguyên, 1.0 = viết lại hoàn toàn. Tính bằng Levenshtein chuẩn hóa");

        builder.Property(entity => entity.RejectReason)
            .HasColumnName("RejectReason")
            .HasColumnType("varchar(500)")
            .HasComment("Vì sao bỏ gợi ý này - dữ liệu vàng để cải thiện prompt");

        builder.Property(entity => entity.CreatedIssueId)
            .HasColumnName("CreatedIssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Issue.Id nếu đã được tạo thành Sub-task thật");

        builder.Property(entity => entity.ReviewedBy)
            .HasColumnName("ReviewedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.ReviewedAt)
            .HasColumnName("ReviewedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.AiGenerationLogId, entity.OrderIndex }, "IX_AiSuggestedTask_Log");

        builder.HasIndex(entity => new { entity.UserAction, entity.CreatedAt }, "IX_AiSuggestedTask_Action");

        builder.HasIndex(entity => entity.CreatedIssueId, "IX_AiSuggestedTask_CreatedIssue");

        builder.HasIndex(entity => new { entity.OriginalSummary, entity.OriginalDescription, entity.FinalSummary, entity.FinalDescription }, "FT_AiSuggestedTask_Search").IsFullText();

        builder.HasOne<AiGenerationLog>()
            .WithMany()
            .HasForeignKey(entity => entity.AiGenerationLogId)
            .HasConstraintName("FK_AiSuggestedTask_Log")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
