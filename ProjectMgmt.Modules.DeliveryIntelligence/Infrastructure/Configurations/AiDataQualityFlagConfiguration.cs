using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiDataQualityFlagConfiguration : IEntityTypeConfiguration<AiDataQualityFlag>
{
    public void Configure(EntityTypeBuilder<AiDataQualityFlag> builder)
    {
        builder.ToTable("AiDataQualityFlag", table =>
        {
            table.HasComment("Kết quả chạy rule làm sạch trên từng mẫu");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SampleId)
            .HasColumnName("SampleId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.RuleId)
            .HasColumnName("RuleId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.Severity)
            .HasColumnName("Severity")
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(entity => entity.Message)
            .HasColumnName("Message")
            .HasColumnType("varchar(1000)");

        builder.Property(entity => entity.FieldPath)
            .HasColumnName("FieldPath")
            .HasColumnType("varchar(200)")
            .HasComment("VD: $.subTasks[0].acceptanceCriteria");

        builder.Property(entity => entity.IsResolved)
            .HasColumnName("IsResolved")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.ResolvedBy)
            .HasColumnName("ResolvedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.ResolvedAt)
            .HasColumnName("ResolvedAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.DetectedAt)
            .HasColumnName("DetectedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.SampleId, entity.IsResolved }, "IX_AiDataQualityFlag_Sample");

        builder.HasIndex(entity => entity.RuleId, "IX_AiDataQualityFlag_Rule");

        builder.HasIndex(entity => new { entity.IsResolved, entity.Severity, entity.DetectedAt }, "IX_AiDataQualityFlag_Queue");

        builder.HasOne<AiDatasetSample>()
            .WithMany()
            .HasForeignKey(entity => entity.SampleId)
            .HasConstraintName("FK_AiDataQualityFlag_Sample")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AiDataCleaningRule>()
            .WithMany()
            .HasForeignKey(entity => entity.RuleId)
            .HasConstraintName("FK_AiDataQualityFlag_Rule")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
