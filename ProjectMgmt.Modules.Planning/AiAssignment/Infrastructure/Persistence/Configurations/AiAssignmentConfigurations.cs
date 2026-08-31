using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.AiAssignment.Infrastructure.Persistence.Configurations;

internal class UserWorkloadSnapshotConfiguration : IEntityTypeConfiguration<UserWorkloadSnapshot>
{
    public void Configure(EntityTypeBuilder<UserWorkloadSnapshot> builder)
    {
        builder.ToTable("UserWorkloadSnapshot");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OpenIssueCount).HasDefaultValue(0);
        builder.Property(x => x.InProgressCount).HasDefaultValue(0);
        builder.Property(x => x.OpenPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.InProgressPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.OverdueCount).HasDefaultValue(0);
        builder.Property(x => x.CapacityPoints).HasPrecision(9, 2);
        builder.Property(x => x.UtilizationRatio).HasPrecision(6, 3);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.UserId, x.ProjectId, x.SnapshotDate }).IsUnique().HasDatabaseName("UQ_UserWorkloadSnapshot");
        builder.HasIndex(x => new { x.ProjectId, x.SnapshotDate }).HasDatabaseName("IX_UserWorkload_Project_Date");
    }
}

internal class UserPerformanceMetricConfiguration : IEntityTypeConfiguration<UserPerformanceMetric>
{
    public void Configure(EntityTypeBuilder<UserPerformanceMetric> builder)
    {
        builder.ToTable("UserPerformanceMetric");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssignedIssueCount).HasDefaultValue(0);
        builder.Property(x => x.CompletedIssueCount).HasDefaultValue(0);
        builder.Property(x => x.CommittedPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.CompletedPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.AvgCycleTimeHours).HasPrecision(9, 2);
        builder.Property(x => x.MedianCycleTimeHours).HasPrecision(9, 2);
        builder.Property(x => x.OnTimeRatio).HasPrecision(5, 4);
        builder.Property(x => x.ReopenedCount).HasDefaultValue(0);
        builder.Property(x => x.EstimateAccuracyRatio).HasPrecision(6, 3);
        builder.Property(x => x.CalculatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.UserId, x.ProjectId, x.PeriodStart, x.PeriodEnd }).IsUnique().HasDatabaseName("UQ_UserPerfMetric");
        builder.HasIndex(x => x.SprintId).HasDatabaseName("IX_UserPerfMetric_Sprint");
    }
}

internal class AiAssignmentRunConfiguration : IEntityTypeConfiguration<AiAssignmentRun>
{
    public void Configure(EntityTypeBuilder<AiAssignmentRun> builder)
    {
        builder.ToTable("AiAssignmentRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TriggerSource).HasMaxLength(30).HasDefaultValue("Manual");
        builder.Property(x => x.Strategy).HasMaxLength(30).HasDefaultValue("WeightedScore");
        builder.Property(x => x.Weights).HasColumnType("json");
        builder.Property(x => x.CandidateUserIds).HasColumnType("json");
        builder.Property(x => x.IssueCount).HasDefaultValue(0);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAt }).HasDatabaseName("IX_AiAssignmentRun_Project");
        builder.HasIndex(x => x.SourceGenerationLogId).HasDatabaseName("IX_AiAssignmentRun_Source");
    }
}

internal class AiAssignmentCandidateConfiguration : IEntityTypeConfiguration<AiAssignmentCandidate>
{
    public void Configure(EntityTypeBuilder<AiAssignmentCandidate> builder)
    {
        builder.ToTable("AiAssignmentCandidate");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TotalScore).HasPrecision(9, 6);
        builder.Property(x => x.LoadBalanceScore).HasPrecision(9, 6);
        builder.Property(x => x.SkillMatchScore).HasPrecision(9, 6);
        builder.Property(x => x.HistoryScore).HasPrecision(9, 6);
        builder.Property(x => x.CapacityScore).HasPrecision(9, 6);
        builder.Property(x => x.IsColdStart).HasDefaultValue(false);
        builder.Property(x => x.FeatureSnapshot).HasColumnType("json").IsRequired();
        builder.Property(x => x.Explanation).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.RunId, x.IssueId, x.CandidateUserId }).IsUnique().HasDatabaseName("UQ_AiAssignmentCandidate");
        builder.HasIndex(x => new { x.IssueId, x.Rank }).HasDatabaseName("IX_AiAssignmentCandidate_Issue");
        builder.HasOne<AiAssignmentRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class AiAssignmentDecisionConfiguration : IEntityTypeConfiguration<AiAssignmentDecision>
{
    public void Configure(EntityTypeBuilder<AiAssignmentDecision> builder)
    {
        builder.ToTable("AiAssignmentDecision");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Outcome).HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(x => x.OverrideReason).HasMaxLength(500);
        builder.Property(x => x.ActualCycleTimeHours).HasPrecision(9, 2);
        builder.Property(x => x.WasReassignedLater).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.RunId, x.IssueId }).IsUnique().HasDatabaseName("UQ_AiAssignmentDecision");
        builder.HasIndex(x => x.Outcome).HasDatabaseName("IX_AiAssignmentDecision_Outcome");
        builder.HasIndex(x => x.IssueId).HasDatabaseName("IX_AiAssignmentDecision_Issue");
        builder.HasOne<AiAssignmentRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AiAssignmentCandidate>().WithMany().HasForeignKey(x => x.SuggestedCandidateId).OnDelete(DeleteBehavior.SetNull);
    }
}
