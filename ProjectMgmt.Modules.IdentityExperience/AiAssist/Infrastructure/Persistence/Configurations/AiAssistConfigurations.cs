using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Entities;

namespace ProjectMgmt.Modules.IdentityExperience.AiAssist.Infrastructure.Persistence.Configurations;

internal sealed class AiGenerationLogConfiguration : IEntityTypeConfiguration<AiGenerationLog>
{
    public void Configure(EntityTypeBuilder<AiGenerationLog> builder)
    {
        builder.ToTable("AiGenerationLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InputText).HasColumnType("text");
        builder.Property(x => x.RenderedPrompt).HasColumnType("mediumtext");
        builder.Property(x => x.RawResponseJson).HasColumnType("mediumtext");
        builder.Property(x => x.ParsedJson).HasColumnType("json");
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.ErrorCode).HasMaxLength(50);
        builder.Property(x => x.RetryCount).HasDefaultValue(0);
        builder.Property(x => x.HangfireJobId).HasMaxLength(64);
        builder.Property(x => x.AppliedCount).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.IssueId, x.CreatedAt }).HasDatabaseName("IX_AiGenerationLog_Issue");
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAt }).HasDatabaseName("IX_AiGenerationLog_RateLimit");
        builder.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_AiGenerationLog_Status");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("IX_AiGenerationLog_User");
    }
}

internal sealed class AiSuggestedTaskConfiguration : IEntityTypeConfiguration<AiSuggestedTask>
{
    public void Configure(EntityTypeBuilder<AiSuggestedTask> builder)
    {
        builder.ToTable("AiSuggestedTask");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderIndex).HasDefaultValue(0);
        builder.Property(x => x.OriginalSummary).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OriginalDescription).HasColumnType("text");
        builder.Property(x => x.OriginalAcceptanceCriteria).HasColumnType("json");
        builder.Property(x => x.OriginalEstimatePoints).HasPrecision(6, 2);
        builder.Property(x => x.OriginalSuggestedSkills).HasColumnType("json");
        builder.Property(x => x.FinalSummary).HasMaxLength(500);
        builder.Property(x => x.FinalDescription).HasColumnType("text");
        builder.Property(x => x.FinalAcceptanceCriteria).HasColumnType("json");
        builder.Property(x => x.FinalEstimatePoints).HasPrecision(6, 2);
        builder.Property(x => x.UserAction).HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(x => x.EditDistanceRatio).HasPrecision(5, 4);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.AiGenerationLogId, x.OrderIndex }).HasDatabaseName("IX_AiSuggestedTask_Log");
        builder.HasIndex(x => x.UserAction).HasDatabaseName("IX_AiSuggestedTask_Action");
        builder.HasIndex(x => x.CreatedIssueId).HasDatabaseName("IX_AiSuggestedTask_CreatedIssue");
        builder.HasOne<AiGenerationLog>().WithMany().HasForeignKey(x => x.AiGenerationLogId).OnDelete(DeleteBehavior.Cascade);
    }
}
