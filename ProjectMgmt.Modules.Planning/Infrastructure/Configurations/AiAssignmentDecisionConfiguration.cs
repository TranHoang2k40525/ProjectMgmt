using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class AiAssignmentDecisionConfiguration : IEntityTypeConfiguration<AiAssignmentDecision>
{
    public void Configure(EntityTypeBuilder<AiAssignmentDecision> builder)
    {
        builder.ToTable("AiAssignmentDecision", table =>
        {
            table.HasComment("Nhãn cho AI phân phối: PM có theo gợi ý không, và kết quả thực tế ra sao");
            table.HasCheckConstraint("CK_AiAssignmentDecision_Outcome", "`Outcome` IN ('Pending','Accepted','Overridden','Rejected')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.RunId)
            .HasColumnName("RunId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.IssueId)
            .HasColumnName("IssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Issue.Id");

        builder.Property(entity => entity.SuggestedUserId)
            .HasColumnName("SuggestedUserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. Ứng viên Rank = 1");

        builder.Property(entity => entity.SuggestedCandidateId)
            .HasColumnName("SuggestedCandidateId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.FinalUserId)
            .HasColumnName("FinalUserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. Người thực sự được gán");

        builder.Property(entity => entity.Outcome)
            .HasColumnName("Outcome")
            .HasColumnType("varchar(20)")
            .HasComment("Pending / Accepted / Overridden / Rejected — NHÃN HUẤN LUYỆN")
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(entity => entity.OverrideReason)
            .HasColumnName("OverrideReason")
            .HasColumnType("varchar(500)")
            .HasComment("PM ghi lý do đổi người - dữ liệu quý nhất để cải thiện model");

        builder.Property(entity => entity.DecidedBy)
            .HasColumnName("DecidedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.DecidedAt)
            .HasColumnName("DecidedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.ActualCycleTimeHours)
            .HasColumnName("ActualCycleTimeHours")
            .HasColumnType("decimal(9,2)");

        builder.Property(entity => entity.WasCompletedOnTime)
            .HasColumnName("WasCompletedOnTime")
            .HasColumnType("tinyint(1)");

        builder.Property(entity => entity.WasReassignedLater)
            .HasColumnName("WasReassignedLater")
            .HasColumnType("tinyint(1)")
            .HasComment("Bị chuyển người sau đó = dấu hiệu gợi ý sai")
            .HasDefaultValue(false);

        builder.Property(entity => entity.OutcomeEvaluatedAt)
            .HasColumnName("OutcomeEvaluatedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.RunId, entity.IssueId }, "UQ_AiAssignmentDecision").IsUnique();

        builder.HasIndex(entity => new { entity.Outcome, entity.CreatedAt }, "IX_AiAssignmentDecision_Outcome");

        builder.HasIndex(entity => entity.IssueId, "IX_AiAssignmentDecision_Issue");

        builder.HasOne<AiAssignmentRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .HasConstraintName("FK_AiAssignmentDecision_Run")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AiAssignmentCandidate>()
            .WithMany()
            .HasForeignKey(entity => entity.SuggestedCandidateId)
            .HasConstraintName("FK_AiAssignmentDecision_Candidate")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
