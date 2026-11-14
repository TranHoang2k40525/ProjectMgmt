using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiConversationSessionConfiguration : IEntityTypeConfiguration<AiConversationSession>
{
    public void Configure(EntityTypeBuilder<AiConversationSession> builder)
    {
        builder.ToTable("AiConversationSession", table =>
        {
            table.HasComment("Phiên làm việc hội thoại đa lượt của Trợ Lý AI, khóa cứng theo (OrganizationId, ProjectId, UserId)");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(e => e.SessionId)
            .HasColumnName("SessionId")
            .HasColumnType("varchar(120)")
            .IsRequired();

        builder.Property(e => e.OrganizationId)
            .HasColumnName("OrganizationId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();

        builder.Property(e => e.ProjectId)
            .HasColumnName("ProjectId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();

        builder.Property(e => e.CreatedByName)
            .HasColumnName("CreatedByName")
            .HasColumnType("varchar(150)")
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("Title")
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(e => e.Model)
            .HasColumnName("Model")
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(e => e.LastIntent)
            .HasColumnName("LastIntent")
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(e => e.RecommendedTarget)
            .HasColumnName("RecommendedTarget")
            .HasColumnType("varchar(30)")
            .IsRequired();

        builder.Property(e => e.TargetSprintId)
            .HasColumnName("TargetSprintId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(e => e.TurnCount)
            .HasColumnName("TurnCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(e => e.TaskCount)
            .HasColumnName("TaskCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(e => e.TotalStoryPoints)
            .HasColumnName("TotalStoryPoints")
            .HasColumnType("decimal(8,2)")
            .HasDefaultValue(0m);

        builder.Property(e => e.MemoryCount)
            .HasColumnName("MemoryCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(e => e.ConversationHistoryJson)
            .HasColumnName("ConversationHistoryJson")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(e => e.ProposalJson)
            .HasColumnName("ProposalJson")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .HasColumnName("IsDeleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("CreatedAtUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("UpdatedAtUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(e => e.DeletedAtUtc)
            .HasColumnName("DeletedAtUtc")
            .HasColumnType("datetime(6)");

        builder.HasIndex(e => e.SessionId, "UQ_AiConversationSession_SessionId").IsUnique();
        builder.HasIndex(
            e => new { e.OrganizationId, e.ProjectId, e.UserId, e.IsDeleted, e.UpdatedAtUtc },
            "IX_AiConversationSession_TenantUserProject");
    }
}
