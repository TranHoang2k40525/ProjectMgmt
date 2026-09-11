using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class AiAssignmentCandidateConfiguration : IEntityTypeConfiguration<AiAssignmentCandidate>
{
    public void Configure(EntityTypeBuilder<AiAssignmentCandidate> builder)
    {
        builder.ToTable("AiAssignmentCandidate", table =>
        {
            table.HasComment("Mỗi (issue, user) một dòng có điểm - đây chính là format dữ liệu chuẩn cho learning-to-rank sau này");
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

        builder.Property(entity => entity.CandidateUserId)
            .HasColumnName("CandidateUserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.Rank)
            .HasColumnName("Rank")
            .HasColumnType("int")
            .HasComment("1 = gợi ý tốt nhất");

        builder.Property(entity => entity.TotalScore)
            .HasColumnName("TotalScore")
            .HasColumnType("decimal(9,6)");

        builder.Property(entity => entity.LoadBalanceScore)
            .HasColumnName("LoadBalanceScore")
            .HasColumnType("decimal(9,6)")
            .HasComment("Càng ít point đang gánh, điểm càng cao");

        builder.Property(entity => entity.SkillMatchScore)
            .HasColumnName("SkillMatchScore")
            .HasColumnType("decimal(9,6)")
            .HasComment("Khớp IssueRequiredSkill với UserSkill");

        builder.Property(entity => entity.HistoryScore)
            .HasColumnName("HistoryScore")
            .HasColumnType("decimal(9,6)")
            .HasComment("Từng làm task tương tự (component/label) và làm tốt");

        builder.Property(entity => entity.CapacityScore)
            .HasColumnName("CapacityScore")
            .HasColumnType("decimal(9,6)")
            .HasComment("Còn dư capacity trong sprint");

        builder.Property(entity => entity.IsColdStart)
            .HasColumnName("IsColdStart")
            .HasColumnType("tinyint(1)")
            .HasComment("TRUE = user chưa có log task, điểm tính từ UserProfile/UserSkill thay vì lịch sử")
            .HasDefaultValue(false);

        builder.Property(entity => entity.FeatureSnapshot)
            .HasColumnName("FeatureSnapshot")
            .HasColumnType("json")
            .HasComment("TOÀN BỘ feature thô tại thời điểm chạy: open_points, in_progress_count, capacity, skill_levels, avg_cycle_time... KHÔNG ĐƯỢC tính lại khi train, nếu không sẽ data leakage")
            .IsRequired();

        builder.Property(entity => entity.Explanation)
            .HasColumnName("Explanation")
            .HasColumnType("varchar(1000)")
            .HasComment("Câu giải thích hiển thị cho PM");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.RunId, entity.IssueId, entity.CandidateUserId }, "UQ_AiAssignmentCandidate").IsUnique();

        builder.HasIndex(entity => new { entity.IssueId, entity.Rank }, "IX_AiAssignmentCandidate_Issue");

        builder.HasIndex(entity => new { entity.RunId, entity.IssueId, entity.Rank }, "IX_AiAssignmentCandidate_Run_Issue_Rank");

        builder.HasOne<AiAssignmentRun>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .HasConstraintName("FK_AiAssignmentCandidate_Run")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
