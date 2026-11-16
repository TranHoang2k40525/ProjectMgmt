using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiConversationMemoryEntryConfiguration : IEntityTypeConfiguration<AiConversationMemoryEntry>
{
    public void Configure(EntityTypeBuilder<AiConversationMemoryEntry> builder)
    {
        builder.ToTable("AiConversationMemoryEntry", table =>
        {
            table.HasComment("Chỉ mục trí nhớ đánh mã trỏ (#mem-X) cho từng phiên Trợ Lý AI");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(e => e.ConversationSessionId)
            .HasColumnName("ConversationSessionId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();

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

        builder.Property(e => e.AnchorKey)
            .HasColumnName("AnchorKey")
            .HasColumnType("varchar(32)")
            .IsRequired();

        builder.Property(e => e.TurnIndex)
            .HasColumnName("TurnIndex")
            .HasColumnType("int");

        builder.Property(e => e.Category)
            .HasColumnName("Category")
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("Title")
            .HasColumnType("varchar(220)")
            .IsRequired();

        builder.Property(e => e.Keywords)
            .HasColumnName("Keywords")
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(e => e.Summary)
            .HasColumnName("Summary")
            .HasColumnType("varchar(600)")
            .IsRequired();

        builder.Property(e => e.ContentJson)
            .HasColumnName("ContentJson")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("CreatedAtUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Memories)
            .HasForeignKey(e => e.ConversationSessionId)
            .HasConstraintName("FK_AiConvMemory_Session")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
            e => new { e.ConversationSessionId, e.AnchorKey },
            "IX_AiConversationMemoryEntry_SessionAnchor");
        builder.HasIndex(
            e => new { e.OrganizationId, e.ProjectId, e.UserId, e.SessionId },
            "IX_AiConversationMemoryEntry_TenantLock");
    }
}
