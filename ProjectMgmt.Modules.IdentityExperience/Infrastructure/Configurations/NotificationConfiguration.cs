using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification", table =>
        {
            table.HasComment("Thông báo, kết hợp SignalR đẩy realtime");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id (người nhận)");

        builder.Property(entity => entity.Type)
            .HasColumnName("Type")
            .HasColumnType("varchar(50)")
            .HasComment("IssueAssigned / NewComment / Mention / SprintEnding / AiBreakdownCompleted / AiBreakdownFailed / AiAssignmentReady")
            .IsRequired();

        builder.Property(entity => entity.Title)
            .HasColumnName("Title")
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(entity => entity.Content)
            .HasColumnName("Content")
            .HasColumnType("text");

        builder.Property(entity => entity.EntityType)
            .HasColumnName("EntityType")
            .HasColumnType("varchar(50)")
            .HasComment("BỔ SUNG: Issue / Sprint / Project / AiGenerationLog");

        builder.Property(entity => entity.EntityId)
            .HasColumnName("EntityId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("BỔ SUNG: v2.0 thiếu cột này -> bấm vào thông báo không biết đi đâu");

        builder.Property(entity => entity.ProjectId)
            .HasColumnName("ProjectId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD: lọc thông báo theo project");

        builder.Property(entity => entity.ActorId)
            .HasColumnName("ActorId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. NULL = do hệ thống/AI");

        builder.Property(entity => entity.IsRead)
            .HasColumnName("IsRead")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.ReadAt)
            .HasColumnName("ReadAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.UserId, entity.IsRead, entity.CreatedAt }, "IX_Notification_Inbox");

        builder.HasIndex(entity => new { entity.EntityType, entity.EntityId }, "IX_Notification_Entity");

        builder.HasIndex(entity => new { entity.ProjectId, entity.CreatedAt }, "IX_Notification_Project_Created");

        builder.HasIndex(entity => new { entity.Type, entity.CreatedAt }, "IX_Notification_Type_Created");

        builder.HasIndex(entity => new { entity.Title, entity.Content }, "FT_Notification_Search").IsFullText();
    }
}
