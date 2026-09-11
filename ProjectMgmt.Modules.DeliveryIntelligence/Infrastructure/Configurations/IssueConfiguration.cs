using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issue", table =>
        {
            table.HasComment("Entity trung tâm: Epic/Story/Task/Bug/Sub-task");
            table.HasCheckConstraint("CK_Issue_Points", "`StoryPoints` IS NULL OR `StoryPoints` >= 0");
            table.HasCheckConstraint("CK_Issue_AiLogConsistency", "`IsAiGenerated` = 0 OR `AiGenerationLogId` IS NOT NULL");
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

        builder.Property(entity => entity.IssueNumber)
            .HasColumnName("IssueNumber")
            .HasColumnType("int")
            .HasComment("BỔ SUNG: số thứ tự trong project, ghép Project.ProjectKey thành \"PROJ-123\"");

        builder.Property(entity => entity.SprintId)
            .HasColumnName("SprintId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Sprint.Id. NULL = còn ở Backlog");

        builder.Property(entity => entity.ParentId)
            .HasColumnName("ParentId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("Self-ref: Epic -> Story -> Sub-task");

        builder.Property(entity => entity.EpicId)
            .HasColumnName("EpicId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("BỔ SUNG: denormalize Epic gốc, tránh đệ quy khi gom nhóm Backlog");

        builder.Property(entity => entity.AssigneeId)
            .HasColumnName("AssigneeId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.ReporterId)
            .HasColumnName("ReporterId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.StatusId)
            .HasColumnName("StatusId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> WorkflowStatus.Id");

        builder.Property(entity => entity.IssueTypeId)
            .HasColumnName("IssueTypeId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> IssueType.Id");

        builder.Property(entity => entity.PriorityId)
            .HasColumnName("PriorityId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Priority.Id");

        builder.Property(entity => entity.Title)
            .HasColumnName("Title")
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("Description")
            .HasColumnType("mediumtext");

        builder.Property(entity => entity.StoryPoints)
            .HasColumnName("StoryPoints")
            .HasColumnType("decimal(6,2)")
            .HasComment("DECIMAL thay INT để hỗ trợ 0.5 point");

        builder.Property(entity => entity.OriginalEstimateMinutes)
            .HasColumnName("OriginalEstimateMinutes")
            .HasColumnType("int");

        builder.Property(entity => entity.TimeSpentMinutes)
            .HasColumnName("TimeSpentMinutes")
            .HasColumnType("int")
            .HasComment("AI ASSIGNMENT: feature ước lượng độ chính xác")
            .HasDefaultValue(0);

        builder.Property(entity => entity.RankOrder)
            .HasColumnName("RankOrder")
            .HasColumnType("decimal(30,15)")
            .HasComment("Chèn giữa = trung bình cộng 2 rank kề. Job rebalance khi khoảng cách < 1e-9");

        builder.Property(entity => entity.DueDate)
            .HasColumnName("DueDate")
            .HasColumnType("date");

        builder.Property(entity => entity.StartedAt)
            .HasColumnName("StartedAt")
            .HasColumnType("datetime(6)")
            .HasComment("AI ASSIGNMENT: lần đầu vào trạng thái InProgress");

        builder.Property(entity => entity.ResolvedAt)
            .HasColumnName("ResolvedAt")
            .HasColumnType("datetime(6)")
            .HasComment("AI ASSIGNMENT: thời điểm vào Done, dùng tính cycle time");

        builder.Property(entity => entity.IsAiGenerated)
            .HasColumnName("IsAiGenerated")
            .HasColumnType("tinyint(1)")
            .HasComment("Sub-task do AI Breakdown sinh ra")
            .HasDefaultValue(false);

        builder.Property(entity => entity.AiGenerationLogId)
            .HasColumnName("AiGenerationLogId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> AiGenerationLog.Id. Truy vết sub-task đến từ request AI nào");

        builder.Property(entity => entity.IsAiAssigned)
            .HasColumnName("IsAiAssigned")
            .HasColumnType("tinyint(1)")
            .HasComment("BỔ SUNG: assignee hiện tại do AI gợi ý và được chấp nhận")
            .HasDefaultValue(false);

        builder.Property(entity => entity.IsDeleted)
            .HasColumnName("IsDeleted")
            .HasColumnType("tinyint(1)")
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

        builder.HasIndex(entity => new { entity.ProjectId, entity.IssueNumber }, "UQ_Issue_Key").IsUnique();

        builder.HasIndex(entity => new { entity.ProjectId, entity.SprintId, entity.RankOrder }, "IX_Issue_Backlog");

        builder.HasIndex(entity => new { entity.SprintId, entity.StatusId }, "IX_Issue_Sprint_Status");

        builder.HasIndex(entity => new { entity.AssigneeId, entity.StatusId }, "IX_Issue_Assignee");

        builder.HasIndex(entity => entity.ParentId, "IX_Issue_Parent");

        builder.HasIndex(entity => entity.EpicId, "IX_Issue_Epic");

        builder.HasIndex(entity => entity.AiGenerationLogId, "IX_Issue_AiLog");

        builder.HasIndex(entity => new { entity.ProjectId, entity.ResolvedAt }, "IX_Issue_Resolved");

        builder.HasIndex(entity => new { entity.ProjectId, entity.IsDeleted, entity.CreatedAt }, "IX_Issue_Project_Recent");

        builder.HasIndex(entity => new { entity.SprintId, entity.StatusId, entity.IsDeleted, entity.RankOrder }, "IX_Issue_Board_Filter");

        builder.HasIndex(entity => new { entity.AssigneeId, entity.IsDeleted, entity.StatusId, entity.DueDate }, "IX_Issue_Assignee_Workload");

        builder.HasIndex(entity => new { entity.ProjectId, entity.IsDeleted, entity.DueDate }, "IX_Issue_Project_Due");

        builder.HasIndex(entity => new { entity.ReporterId, entity.CreatedAt }, "IX_Issue_Reporter_Created");

        builder.HasIndex(entity => new { entity.Title, entity.Description }, "FT_Issue_Search").IsFullText();

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.ParentId)
            .HasConstraintName("FK_Issue_Parent")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
