using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiTrainingRunConfiguration : IEntityTypeConfiguration<AiTrainingRun>
{
    public void Configure(EntityTypeBuilder<AiTrainingRun> builder)
    {
        builder.ToTable("AiTrainingRun", table =>
        {
            table.HasComment("Mỗi lần fine-tune - lưu đủ để tái lập thí nghiệm");
            table.HasCheckConstraint("CK_AiTrainingRun_Status", "`Status` IN ('Queued','Running','Completed','Failed','Cancelled')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.DatasetVersionId)
            .HasColumnName("DatasetVersionId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(entity => entity.TaskType)
            .HasColumnName("TaskType")
            .HasColumnType("varchar(30)")
            .IsRequired();

        builder.Property(entity => entity.BaseModelCode)
            .HasColumnName("BaseModelCode")
            .HasColumnType("varchar(100)")
            .HasComment("qwen2.5:3b-instruct")
            .IsRequired();

        builder.Property(entity => entity.Method)
            .HasColumnName("Method")
            .HasColumnType("varchar(30)")
            .HasComment("LoRA / QLoRA / FullFineTune / GBDT (cho assignment)")
            .HasDefaultValue("LoRA")
            .IsRequired();

        builder.Property(entity => entity.Hyperparameters)
            .HasColumnName("Hyperparameters")
            .HasColumnType("json")
            .HasComment("lora_r, lora_alpha, lr, epochs, batch_size, seed");

        builder.Property(entity => entity.Status)
            .HasColumnName("Status")
            .HasColumnType("varchar(20)")
            .HasComment("Queued / Running / Completed / Failed / Cancelled")
            .HasDefaultValue("Queued")
            .IsRequired();

        builder.Property(entity => entity.ArtifactPath)
            .HasColumnName("ArtifactPath")
            .HasColumnType("varchar(500)")
            .HasComment("Đường dẫn adapter / .gguf sau khi convert");

        builder.Property(entity => entity.LogPath)
            .HasColumnName("LogPath")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.TrainLoss)
            .HasColumnName("TrainLoss")
            .HasColumnType("decimal(10,6)");

        builder.Property(entity => entity.ValidationLoss)
            .HasColumnName("ValidationLoss")
            .HasColumnType("decimal(10,6)");

        builder.Property(entity => entity.DurationSeconds)
            .HasColumnName("DurationSeconds")
            .HasColumnType("int");

        builder.Property(entity => entity.HardwareInfo)
            .HasColumnName("HardwareInfo")
            .HasColumnType("varchar(255)")
            .HasComment("GPU/CPU dùng để train, để so sánh công bằng");

        builder.Property(entity => entity.ErrorMessage)
            .HasColumnName("ErrorMessage")
            .HasColumnType("text");

        builder.Property(entity => entity.StartedAt)
            .HasColumnName("StartedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.FinishedAt)
            .HasColumnName("FinishedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("CreatedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => entity.DatasetVersionId, "IX_AiTrainingRun_Dataset");

        builder.HasIndex(entity => entity.Name, "IX_AiTrainingRun_Name");

        builder.HasIndex(entity => new { entity.Status, entity.CreatedAt }, "IX_AiTrainingRun_Status_Created");

        builder.HasIndex(entity => new { entity.TaskType, entity.Status, entity.CreatedAt }, "IX_AiTrainingRun_Task_Status");

        builder.HasOne<AiDatasetVersion>()
            .WithMany()
            .HasForeignKey(entity => entity.DatasetVersionId)
            .HasConstraintName("FK_AiTrainingRun_Dataset")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
