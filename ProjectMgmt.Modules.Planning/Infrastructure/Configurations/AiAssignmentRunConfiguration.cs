using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class AiAssignmentRunConfiguration : IEntityTypeConfiguration<AiAssignmentRun>
{
    public void Configure(EntityTypeBuilder<AiAssignmentRun> builder)
    {
        builder.ToTable("AiAssignmentRun", table =>
        {
            table.HasComment("Một lần chạy phân phối cho một tập issue");
            table.HasCheckConstraint("CK_AiAssignmentRun_Status", "`Status` IN ('Pending','Processing','Completed','Failed')");
            table.HasCheckConstraint("CK_AiAssignmentRun_Strategy", "`Strategy` IN ('WeightedScore','LearnedRanker','RoundRobin','Hybrid')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ProjectId)
            .HasColumnName("ProjectId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Project.Id");

        builder.Property(entity => entity.SprintId)
            .HasColumnName("SprintId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Sprint.Id");

        builder.Property(entity => entity.RequestedBy)
            .HasColumnName("RequestedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.TriggerSource)
            .HasColumnName("TriggerSource")
            .HasColumnType("varchar(30)")
            .HasComment("Manual / AfterAiBreakdown / SprintPlanning - luồng \"AI phân phối task VỪA ĐƯỢC AI bóc tách\" dùng AfterAiBreakdown")
            .HasDefaultValue("Manual")
            .IsRequired();

        builder.Property(entity => entity.SourceGenerationLogId)
            .HasColumnName("SourceGenerationLogId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> AiGenerationLog.Id. Nối 2 AI với nhau");

        builder.Property(entity => entity.Strategy)
            .HasColumnName("Strategy")
            .HasColumnType("varchar(30)")
            .HasComment("WeightedScore (giai đoạn 1) / LearnedRanker (giai đoạn 2) / RoundRobin (baseline đối chứng)")
            .HasDefaultValue("WeightedScore")
            .IsRequired();

        builder.Property(entity => entity.ModelId)
            .HasColumnName("ModelId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("NULL nếu dùng scoring function thuần");

        builder.Property(entity => entity.Weights)
            .HasColumnName("Weights")
            .HasColumnType("json")
            .HasComment("Trọng số dùng cho lần chạy này, VD {\"load\":0.4,\"skill\":0.3,\"history\":0.2,\"capacity\":0.1} - lưu để tái lập");

        builder.Property(entity => entity.CandidateUserIds)
            .HasColumnName("CandidateUserIds")
            .HasColumnType("json")
            .HasComment("Danh sách user được xét (thành viên project tại thời điểm chạy)");

        builder.Property(entity => entity.IssueCount)
            .HasColumnName("IssueCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.Status)
            .HasColumnName("Status")
            .HasColumnType("varchar(20)")
            .HasComment("Pending / Processing / Completed / Failed")
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(entity => entity.ErrorMessage)
            .HasColumnName("ErrorMessage")
            .HasColumnType("varchar(1000)");

        builder.Property(entity => entity.LatencyMs)
            .HasColumnName("LatencyMs")
            .HasColumnType("int");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.CompletedAt)
            .HasColumnName("CompletedAt")
            .HasColumnType("datetime(6)");

        builder.HasIndex(entity => new { entity.ProjectId, entity.CreatedAt }, "IX_AiAssignmentRun_Project");

        builder.HasIndex(entity => entity.SourceGenerationLogId, "IX_AiAssignmentRun_Source");

        builder.HasIndex(entity => new { entity.Status, entity.CreatedAt }, "IX_AiAssignmentRun_Status_Created");

        builder.HasIndex(entity => new { entity.Strategy, entity.CreatedAt }, "IX_AiAssignmentRun_Strategy_Created");
    }
}
