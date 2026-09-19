using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AcceptanceCriteriaConfiguration : IEntityTypeConfiguration<AcceptanceCriteria>
{
    public void Configure(EntityTypeBuilder<AcceptanceCriteria> builder)
    {
        builder.ToTable("AcceptanceCriteria", table =>
        {
            table.HasComment("Tiêu chí nghiệm thu, tick được từng dòng cho Definition of Done");
            table.HasCheckConstraint("CK_AcceptanceCriteria_Source", "`Source` IN ('AI','Manual')");
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
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("SỬA: cho phép gắn mọi loại issue, không chỉ Story (ví dụ JSON ở Mục 5.9.8 sinh AC cho Sub-task)");

        builder.Property(entity => entity.Content)
            .HasColumnName("Content")
            .HasColumnType("text")
            .HasComment("Given-When-Then hoặc gạch đầu dòng")
            .IsRequired();

        builder.Property(entity => entity.IsMet)
            .HasColumnName("IsMet")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.MetBy)
            .HasColumnName("MetBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.MetAt)
            .HasColumnName("MetAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.OrderIndex)
            .HasColumnName("OrderIndex")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.Source)
            .HasColumnName("Source")
            .HasColumnType("varchar(20)")
            .HasComment("AI / Manual - đánh giá độ tin cậy gợi ý AI")
            .HasDefaultValue("Manual")
            .IsRequired();

        builder.Property(entity => entity.AiGenerationLogId)
            .HasColumnName("AiGenerationLogId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD: truy vết AC này do request AI nào sinh");

        builder.Property(entity => entity.WasEditedAfterAi)
            .HasColumnName("WasEditedAfterAi")
            .HasColumnType("tinyint(1)")
            .HasComment("BỔ SUNG: nhãn huấn luyện - AC do AI sinh có bị sửa không")
            .HasDefaultValue(false);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => new { entity.IssueId, entity.OrderIndex }, "IX_AcceptanceCriteria_Issue");

        builder.HasIndex(entity => new { entity.Source, entity.CreatedAt }, "IX_AcceptanceCriteria_Source");

        builder.HasIndex(entity => entity.Content, "FT_AcceptanceCriteria_Content").IsFullText();

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_AcceptanceCriteria_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
