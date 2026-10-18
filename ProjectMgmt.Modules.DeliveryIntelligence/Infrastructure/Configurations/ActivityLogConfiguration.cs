using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLog", table =>
        {
            table.HasComment("Nhật ký thay đổi tự động - sinh qua IActivityLogService");
            table.HasCheckConstraint("CK_ActivityLog_Source", "`Source` IN ('User','System','AI')");
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

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. NULL = do hệ thống/AI thực hiện");

        builder.Property(entity => entity.Action)
            .HasColumnName("Action")
            .HasColumnType("varchar(60)")
            .HasComment("Created / StatusChanged / AssigneeChanged / AiBreakdownApplied...")
            .IsRequired();

        builder.Property(entity => entity.FieldName)
            .HasColumnName("FieldName")
            .HasColumnType("varchar(80)");

        builder.Property(entity => entity.OldValue)
            .HasColumnName("OldValue")
            .HasColumnType("text");

        builder.Property(entity => entity.NewValue)
            .HasColumnName("NewValue")
            .HasColumnType("text");

        builder.Property(entity => entity.Detail)
            .HasColumnName("Detail")
            .HasColumnType("json")
            .HasComment("Payload đầy đủ dạng JSON, query được bằng JSON_EXTRACT");

        builder.Property(entity => entity.Source)
            .HasColumnName("Source")
            .HasColumnType("varchar(20)")
            .HasComment("User / System / AI - phân biệt để báo cáo AI")
            .HasDefaultValue("User")
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.IssueId, entity.CreatedAt }, "IX_ActivityLog_Issue");

        builder.HasIndex(entity => new { entity.Action, entity.CreatedAt }, "IX_ActivityLog_Action");

        builder.HasIndex(entity => new { entity.UserId, entity.CreatedAt }, "IX_ActivityLog_User_Created");

        builder.HasIndex(entity => new { entity.Source, entity.CreatedAt }, "IX_ActivityLog_Source_Created");

        builder.HasIndex(entity => entity.CreatedAt, "IX_ActivityLog_CreatedAt");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_ActivityLog_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
