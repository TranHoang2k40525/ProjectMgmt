using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class IssueStatusHistoryConfiguration : IEntityTypeConfiguration<IssueStatusHistory>
{
    public void Configure(EntityTypeBuilder<IssueStatusHistory> builder)
    {
        builder.ToTable("IssueStatusHistory", table =>
        {
            table.HasComment("BỔ SUNG - AI ASSIGNMENT: ActivityLog dạng text không tính được cycle time. Bảng này lưu có cấu trúc + duration tính sẵn");
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

        builder.Property(entity => entity.FromStatusId)
            .HasColumnName("FromStatusId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD. NULL = trạng thái đầu tiên khi tạo");

        builder.Property(entity => entity.ToStatusId)
            .HasColumnName("ToStatusId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> WorkflowStatus.Id");

        builder.Property(entity => entity.FromCategory)
            .HasColumnName("FromCategory")
            .HasColumnType("varchar(20)");

        builder.Property(entity => entity.ToCategory)
            .HasColumnName("ToCategory")
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(entity => entity.ChangedBy)
            .HasColumnName("ChangedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.DurationSeconds)
            .HasColumnName("DurationSeconds")
            .HasColumnType("bigint")
            .HasComment("Thời gian ĐÃ Ở trạng thái trước đó - tính sẵn khi ghi");

        builder.Property(entity => entity.ChangedAt)
            .HasColumnName("ChangedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.IssueId, entity.ChangedAt }, "IX_IssueStatusHistory_Issue");

        builder.HasIndex(entity => new { entity.ToCategory, entity.ChangedAt }, "IX_IssueStatusHistory_Category_Date");

        builder.HasIndex(entity => entity.ChangedAt, "IX_IssueStatusHistory_ChangedAt");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_IssueStatusHistory_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
