using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Infrastructure.Persistence.Configurations;

internal sealed class AiModelConfiguration : IEntityTypeConfiguration<AiModel>
{
    public void Configure(EntityTypeBuilder<AiModel> builder)
    {
        builder.ToTable("AiModel");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(50).HasDefaultValue("Ollama");
        builder.Property(x => x.TaskType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.BaseModelCode).HasMaxLength(100);
        builder.Property(x => x.AdapterPath).HasMaxLength(500);
        builder.Property(x => x.IsActive).HasDefaultValue(false);
        builder.Property(x => x.DefaultParams).HasColumnType("json");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_AiModel_Code");
        builder.HasIndex(x => new { x.TaskType, x.IsActive }).HasDatabaseName("IX_AiModel_Task_Active");
        builder.HasOne<AiTrainingRun>().WithMany().HasForeignKey(x => x.TrainingRunId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AiPromptTemplateConfiguration : IEntityTypeConfiguration<AiPromptTemplate>
{
    public void Configure(EntityTypeBuilder<AiPromptTemplate> builder)
    {
        builder.ToTable("AiPromptTemplate");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Version).HasDefaultValue(1);
        builder.Property(x => x.TaskType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10).HasDefaultValue("vi");
        builder.Property(x => x.SystemPrompt).HasColumnType("mediumtext").IsRequired();
        builder.Property(x => x.UserTemplate).HasColumnType("mediumtext");
        builder.Property(x => x.JsonSchema).HasColumnType("json");
        builder.Property(x => x.IsActive).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.Code, x.Version }).IsUnique().HasDatabaseName("UQ_AiPromptTemplate");
        builder.HasIndex(x => new { x.TaskType, x.IsActive }).HasDatabaseName("IX_AiPromptTemplate_Active");
    }
}
