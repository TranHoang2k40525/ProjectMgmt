using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiEvaluationResultConfiguration : IEntityTypeConfiguration<AiEvaluationResult>
{
    public void Configure(EntityTypeBuilder<AiEvaluationResult> builder)
    {
        builder.ToTable("AiEvaluationResult", table =>
        {
            table.HasComment("Không có bảng này thì không biết fine-tune có tốt hơn prompt thuần hay không");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.TrainingRunId)
            .HasColumnName("TrainingRunId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("NULL nếu đánh giá model gốc / prompt thuần (baseline)");

        builder.Property(entity => entity.ModelId)
            .HasColumnName("ModelId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.DatasetVersionId)
            .HasColumnName("DatasetVersionId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SplitType)
            .HasColumnName("SplitType")
            .HasColumnType("varchar(20)")
            .HasDefaultValue("Test")
            .IsRequired();

        builder.Property(entity => entity.MetricName)
            .HasColumnName("MetricName")
            .HasColumnType("varchar(80)")
            .HasComment("Breakdown: json_valid_rate, schema_match_rate, avg_edit_distance, keep_rate, rouge_l. Assignment: precision@1, ndcg@3, mae_workload_gap")
            .IsRequired();

        builder.Property(entity => entity.MetricValue)
            .HasColumnName("MetricValue")
            .HasColumnType("decimal(12,6)");

        builder.Property(entity => entity.SampleSize)
            .HasColumnName("SampleSize")
            .HasColumnType("int");

        builder.Property(entity => entity.Notes)
            .HasColumnName("Notes")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.EvaluatedAt)
            .HasColumnName("EvaluatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.TrainingRunId, entity.MetricName }, "IX_AiEvaluationResult_Run");

        builder.HasIndex(entity => new { entity.ModelId, entity.MetricName }, "IX_AiEvaluationResult_Model");

        builder.HasIndex(entity => new { entity.DatasetVersionId, entity.SplitType, entity.MetricName }, "IX_AiEvaluationResult_Dataset_Metric");

        builder.HasOne<AiTrainingRun>()
            .WithMany()
            .HasForeignKey(entity => entity.TrainingRunId)
            .HasConstraintName("FK_AiEvaluationResult_TrainingRun")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AiDatasetVersion>()
            .WithMany()
            .HasForeignKey(entity => entity.DatasetVersionId)
            .HasConstraintName("FK_AiEvaluationResult_Dataset")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
