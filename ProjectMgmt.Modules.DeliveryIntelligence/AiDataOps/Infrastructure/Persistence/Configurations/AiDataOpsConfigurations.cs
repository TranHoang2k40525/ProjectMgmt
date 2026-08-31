using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Infrastructure.Persistence.Configurations;

internal class AiDatasetConfiguration : IEntityTypeConfiguration<AiDataset>
{
    public void Configure(EntityTypeBuilder<AiDataset> builder)
    {
        builder.ToTable("AiDataset");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TaskType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.Languages).HasColumnType("json");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_AiDataset_Code");
    }
}

internal class AiDatasetVersionConfiguration : IEntityTypeConfiguration<AiDatasetVersion>
{
    public void Configure(EntityTypeBuilder<AiDatasetVersion> builder)
    {
        builder.ToTable("AiDatasetVersion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VersionTag).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SampleCount).HasDefaultValue(0);
        builder.Property(x => x.TrainCount).HasDefaultValue(0);
        builder.Property(x => x.ValidationCount).HasDefaultValue(0);
        builder.Property(x => x.TestCount).HasDefaultValue(0);
        builder.Property(x => x.Checksum).HasColumnType("char(64)");
        builder.Property(x => x.ExportPath).HasMaxLength(500);
        builder.Property(x => x.IsFrozen).HasDefaultValue(false);
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.DatasetId, x.VersionTag }).IsUnique().HasDatabaseName("UQ_AiDatasetVersion");
        builder.HasOne<AiDataset>().WithMany().HasForeignKey(x => x.DatasetId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class AiDatasetSampleConfiguration : IEntityTypeConfiguration<AiDatasetSample>
{
    public void Configure(EntityTypeBuilder<AiDatasetSample> builder)
    {
        builder.ToTable("AiDatasetSample");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Instruction).HasColumnType("mediumtext").IsRequired();
        builder.Property(x => x.InputJson).HasColumnType("json").IsRequired();
        builder.Property(x => x.OutputJson).HasColumnType("json").IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10).HasDefaultValue("vi");
        builder.Property(x => x.SplitType).HasMaxLength(20).HasDefaultValue("Train");
        builder.Property(x => x.QualityStatus).HasMaxLength(20).HasDefaultValue("Raw");
        builder.Property(x => x.QualityScore).HasPrecision(4, 3);
        builder.Property(x => x.IsPiiRedacted).HasDefaultValue(false);
        builder.Property(x => x.ContentHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.ReviewStatus).HasMaxLength(20).HasDefaultValue("NotReviewed");
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => new { x.DatasetVersionId, x.ContentHash }).IsUnique().HasDatabaseName("UQ_AiDatasetSample_Dedup");
        builder.HasIndex(x => new { x.DatasetVersionId, x.SplitType, x.QualityStatus }).HasDatabaseName("IX_AiDatasetSample_Split");
        builder.HasIndex(x => new { x.SourceType, x.SourceRefId }).HasDatabaseName("IX_AiDatasetSample_Source");
        builder.HasOne<AiDatasetVersion>().WithMany().HasForeignKey(x => x.DatasetVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class AiDataCleaningRuleConfiguration : IEntityTypeConfiguration<AiDataCleaningRule>
{
    public void Configure(EntityTypeBuilder<AiDataCleaningRule> builder)
    {
        builder.ToTable("AiDataCleaningRule");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RuleType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(20).HasDefaultValue("Warning");
        builder.Property(x => x.Config).HasColumnType("json");
        builder.Property(x => x.AppliesTo).HasMaxLength(30).HasDefaultValue("All");
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_AiDataCleaningRule_Code");
    }
}

internal class AiDataQualityFlagConfiguration : IEntityTypeConfiguration<AiDataQualityFlag>
{
    public void Configure(EntityTypeBuilder<AiDataQualityFlag> builder)
    {
        builder.ToTable("AiDataQualityFlag");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.FieldPath).HasMaxLength(200);
        builder.Property(x => x.IsResolved).HasDefaultValue(false);
        builder.Property(x => x.DetectedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.SampleId, x.IsResolved }).HasDatabaseName("IX_AiDataQualityFlag_Sample");
        builder.HasIndex(x => x.RuleId).HasDatabaseName("IX_AiDataQualityFlag_Rule");
        builder.HasOne<AiDatasetSample>().WithMany().HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AiDataCleaningRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class AiTrainingRunConfiguration : IEntityTypeConfiguration<AiTrainingRun>
{
    public void Configure(EntityTypeBuilder<AiTrainingRun> builder)
    {
        builder.ToTable("AiTrainingRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TaskType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.BaseModelCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(30).HasDefaultValue("LoRA");
        builder.Property(x => x.Hyperparameters).HasColumnType("json");
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Queued");
        builder.Property(x => x.ArtifactPath).HasMaxLength(500);
        builder.Property(x => x.LogPath).HasMaxLength(500);
        builder.Property(x => x.TrainLoss).HasPrecision(10, 6);
        builder.Property(x => x.ValidationLoss).HasPrecision(10, 6);
        builder.Property(x => x.HardwareInfo).HasMaxLength(255);
        builder.Property(x => x.ErrorMessage).HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => x.DatasetVersionId).HasDatabaseName("IX_AiTrainingRun_Dataset");
        builder.HasOne<AiDatasetVersion>().WithMany().HasForeignKey(x => x.DatasetVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class AiEvaluationResultConfiguration : IEntityTypeConfiguration<AiEvaluationResult>
{
    public void Configure(EntityTypeBuilder<AiEvaluationResult> builder)
    {
        builder.ToTable("AiEvaluationResult");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SplitType).HasMaxLength(20).HasDefaultValue("Test");
        builder.Property(x => x.MetricName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.MetricValue).HasPrecision(12, 6);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.EvaluatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.TrainingRunId, x.MetricName }).HasDatabaseName("IX_AiEvaluationResult_Run");
        builder.HasIndex(x => new { x.ModelId, x.MetricName }).HasDatabaseName("IX_AiEvaluationResult_Model");
        builder.HasOne<AiTrainingRun>().WithMany().HasForeignKey(x => x.TrainingRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AiModel>().WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AiDatasetVersion>().WithMany().HasForeignKey(x => x.DatasetVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
