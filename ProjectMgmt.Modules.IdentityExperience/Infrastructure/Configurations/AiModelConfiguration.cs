using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class AiModelConfiguration : IEntityTypeConfiguration<AiModel>
{
    public void Configure(EntityTypeBuilder<AiModel> builder)
    {
        builder.ToTable("AiModel", table =>
        {
            table.HasComment("Registry model - BẮT BUỘC có nếu định fine-tune: phải biết kết quả nào sinh bởi model nào để so sánh A/B");
            table.HasCheckConstraint("CK_AiModel_TaskType", "`TaskType` IN ('Breakdown','Assignment','Embedding')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.Code)
            .HasColumnName("Code")
            .HasColumnType("varchar(100)")
            .HasComment("qwen2.5:3b-instruct, qwen2.5:3b-scrum-lora-v1")
            .IsRequired();

        builder.Property(entity => entity.DisplayName)
            .HasColumnName("DisplayName")
            .HasColumnType("varchar(150)")
            .IsRequired();

        builder.Property(entity => entity.Provider)
            .HasColumnName("Provider")
            .HasColumnType("varchar(50)")
            .HasComment("Ollama / OpenAI / Local")
            .HasDefaultValue("Ollama")
            .IsRequired();

        builder.Property(entity => entity.TaskType)
            .HasColumnName("TaskType")
            .HasColumnType("varchar(30)")
            .HasComment("Breakdown / Assignment / Embedding")
            .IsRequired();

        builder.Property(entity => entity.BaseModelCode)
            .HasColumnName("BaseModelCode")
            .HasColumnType("varchar(100)")
            .HasComment("Model gốc nếu đây là bản fine-tune");

        builder.Property(entity => entity.AdapterPath)
            .HasColumnName("AdapterPath")
            .HasColumnType("varchar(500)")
            .HasComment("Đường dẫn LoRA adapter / Modelfile");

        builder.Property(entity => entity.TrainingRunId)
            .HasColumnName("TrainingRunId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> AiTrainingRun.Id nếu sinh từ fine-tune");

        builder.Property(entity => entity.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasComment("Model đang được dùng ở production")
            .HasDefaultValue(false);

        builder.Property(entity => entity.ContextWindow)
            .HasColumnName("ContextWindow")
            .HasColumnType("int");

        builder.Property(entity => entity.DefaultParams)
            .HasColumnName("DefaultParams")
            .HasColumnType("json")
            .HasComment("temperature, top_p, num_predict, seed...");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => entity.Code, "UQ_AiModel_Code").IsUnique();

        builder.HasIndex(entity => new { entity.TaskType, entity.IsActive }, "IX_AiModel_Task_Active");

        builder.HasIndex(entity => entity.DisplayName, "IX_AiModel_DisplayName");
    }
}
