using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class AiGenerationLogConfiguration : IEntityTypeConfiguration<AiGenerationLog>
{
    public void Configure(EntityTypeBuilder<AiGenerationLog> builder)
    {
        builder.ToTable("AiGenerationLog", table =>
        {
            table.HasComment("Mỗi lần yêu cầu AI chia nhỏ Story");
            table.HasCheckConstraint("CK_AiGenerationLog_Status", "`Status` IN ('Pending','Processing','Completed','Failed','Cancelled')");
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
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Issue.Id (Story gốc được yêu cầu chia nhỏ)");

        builder.Property(entity => entity.ProjectId)
            .HasColumnName("ProjectId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> Project.Id. Denormalize để rate-limit theo project không phải JOIN");

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id (PO/Tech Lead kích hoạt)");

        builder.Property(entity => entity.ModelId)
            .HasColumnName("ModelId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.PromptTemplateId)
            .HasColumnName("PromptTemplateId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.InputText)
            .HasColumnName("InputText")
            .HasColumnType("text")
            .HasComment("Mô tả bổ sung người dùng nhập");

        builder.Property(entity => entity.RenderedPrompt)
            .HasColumnName("RenderedPrompt")
            .HasColumnType("mediumtext")
            .HasComment("Prompt cuối cùng đã ghép - BẮT BUỘC lưu để tái lập kết quả");

        builder.Property(entity => entity.RawResponseJson)
            .HasColumnName("RawResponseJson")
            .HasColumnType("mediumtext")
            .HasComment("Phản hồi thô trước khi parse, phục vụ audit & đổi logic parse");

        builder.Property(entity => entity.ParsedJson)
            .HasColumnName("ParsedJson")
            .HasColumnType("json")
            .HasComment("Sau khi parse & validate schema");

        builder.Property(entity => entity.Status)
            .HasColumnName("Status")
            .HasColumnType("varchar(20)")
            .HasComment("Pending / Processing / Completed / Failed / Cancelled")
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(entity => entity.ErrorMessage)
            .HasColumnName("ErrorMessage")
            .HasColumnType("varchar(1000)");

        builder.Property(entity => entity.ErrorCode)
            .HasColumnName("ErrorCode")
            .HasColumnType("varchar(50)")
            .HasComment("Timeout / InvalidJson / SchemaMismatch / OllamaUnavailable");

        builder.Property(entity => entity.PromptTokens)
            .HasColumnName("PromptTokens")
            .HasColumnType("int");

        builder.Property(entity => entity.CompletionTokens)
            .HasColumnName("CompletionTokens")
            .HasColumnType("int");

        builder.Property(entity => entity.LatencyMs)
            .HasColumnName("LatencyMs")
            .HasColumnType("int")
            .HasComment("Theo dõi hiệu năng model cục bộ");

        builder.Property(entity => entity.RetryCount)
            .HasColumnName("RetryCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.HangfireJobId)
            .HasColumnName("HangfireJobId")
            .HasColumnType("varchar(64)")
            .HasComment("Truy vết ngược sang Hangfire dashboard khi debug");

        builder.Property(entity => entity.AppliedAt)
            .HasColumnName("AppliedAt")
            .HasColumnType("datetime(6)")
            .HasComment("Thời điểm user bấm \"Áp dụng\". NULL = chưa từng áp dụng");

        builder.Property(entity => entity.AppliedCount)
            .HasColumnName("AppliedCount")
            .HasColumnType("int")
            .HasComment("Số sub-task thực sự được tạo (có thể < số gợi ý)")
            .HasDefaultValue(0);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.CompletedAt)
            .HasColumnName("CompletedAt")
            .HasColumnType("datetime(6)");

        builder.HasIndex(entity => new { entity.IssueId, entity.CreatedAt }, "IX_AiGenerationLog_Issue");

        builder.HasIndex(entity => new { entity.ProjectId, entity.CreatedAt }, "IX_AiGenerationLog_RateLimit");

        builder.HasIndex(entity => new { entity.Status, entity.CreatedAt }, "IX_AiGenerationLog_Status");

        builder.HasIndex(entity => new { entity.UserId, entity.CreatedAt }, "IX_AiGenerationLog_User");

        builder.HasIndex(entity => entity.HangfireJobId, "IX_AiGenerationLog_HangfireJobId");

        builder.HasIndex(entity => new { entity.ErrorCode, entity.CreatedAt }, "IX_AiGenerationLog_Error");

        builder.HasOne<AiModel>()
            .WithMany()
            .HasForeignKey(entity => entity.ModelId)
            .HasConstraintName("FK_AiGenerationLog_Model")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<AiPromptTemplate>()
            .WithMany()
            .HasForeignKey(entity => entity.PromptTemplateId)
            .HasConstraintName("FK_AiGenerationLog_Prompt")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
