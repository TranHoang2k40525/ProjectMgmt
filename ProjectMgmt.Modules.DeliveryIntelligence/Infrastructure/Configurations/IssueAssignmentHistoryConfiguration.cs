using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class IssueAssignmentHistoryConfiguration : IEntityTypeConfiguration<IssueAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<IssueAssignmentHistory> builder)
    {
        builder.ToTable("IssueAssignmentHistory", table =>
        {
            table.HasComment("BỔ SUNG - BẢNG QUAN TRỌNG NHẤT cho AI phân phối task: toàn bộ lịch sử giao việc + nguồn gán");
            table.HasCheckConstraint("CK_IssueAssignHistory_Source", "`AssignmentSource` IN ('Manual','AiSuggested','AiAuto','SelfPick')");
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

        builder.Property(entity => entity.FromAssigneeId)
            .HasColumnName("FromAssigneeId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. NULL = chưa gán ai");

        builder.Property(entity => entity.ToAssigneeId)
            .HasColumnName("ToAssigneeId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. NULL = gỡ assignee");

        builder.Property(entity => entity.AssignedBy)
            .HasColumnName("AssignedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. NULL nếu do AI tự gán");

        builder.Property(entity => entity.AssignmentSource)
            .HasColumnName("AssignmentSource")
            .HasColumnType("varchar(20)")
            .HasComment("Manual / AiSuggested / AiAuto / SelfPick")
            .HasDefaultValue("Manual")
            .IsRequired();

        builder.Property(entity => entity.AiCandidateId)
            .HasColumnName("AiCandidateId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> AiAssignmentCandidate.Id nếu đến từ gợi ý AI");

        builder.Property(entity => entity.StoryPointsAtTime)
            .HasColumnName("StoryPointsAtTime")
            .HasColumnType("decimal(6,2)")
            .HasComment("Snapshot point tại thời điểm gán (point có thể bị đổi sau)");

        builder.Property(entity => entity.Reason)
            .HasColumnName("Reason")
            .HasColumnType("varchar(500)")
            .HasComment("Lý do override nếu PM không theo gợi ý AI");

        builder.Property(entity => entity.AssignedAt)
            .HasColumnName("AssignedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.IssueId, entity.AssignedAt }, "IX_IssueAssignHistory_Issue");

        builder.HasIndex(entity => new { entity.ToAssigneeId, entity.AssignedAt }, "IX_IssueAssignHistory_To");

        builder.HasIndex(entity => new { entity.AssignmentSource, entity.AssignedAt }, "IX_IssueAssignHistory_Source");

        builder.HasIndex(entity => entity.AssignedAt, "IX_IssueAssignHistory_AssignedAt");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_IssueAssignHistory_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
