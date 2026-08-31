using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Infrastructure.Persistence.Configurations;

internal sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issue");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasColumnType("mediumtext");
        builder.Property(x => x.StoryPoints).HasPrecision(6, 2);
        builder.Property(x => x.TimeSpentMinutes).HasDefaultValue(0);
        builder.Property(x => x.RankOrder).HasPrecision(30, 15);
        builder.Property(x => x.IsAiGenerated).HasDefaultValue(false);
        builder.Property(x => x.IsAiAssigned).HasDefaultValue(false);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => new { x.ProjectId, x.IssueNumber }).IsUnique().HasDatabaseName("UQ_Issue_Key");
        builder.HasIndex(x => new { x.ProjectId, x.SprintId, x.RankOrder }).HasDatabaseName("IX_Issue_Backlog");
        builder.HasIndex(x => new { x.SprintId, x.StatusId }).HasDatabaseName("IX_Issue_Sprint_Status");
        builder.HasIndex(x => new { x.AssigneeId, x.StatusId }).HasDatabaseName("IX_Issue_Assignee");
        builder.HasIndex(x => x.ParentId).HasDatabaseName("IX_Issue_Parent");
        builder.HasIndex(x => x.EpicId).HasDatabaseName("IX_Issue_Epic");
        builder.HasIndex(x => x.AiGenerationLogId).HasDatabaseName("IX_Issue_AiLog");
        builder.HasIndex(x => new { x.ProjectId, x.ResolvedAt }).HasDatabaseName("IX_Issue_Resolved");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class IssueLinkConfiguration : IEntityTypeConfiguration<IssueLink>
{
    public void Configure(EntityTypeBuilder<IssueLink> builder)
    {
        builder.ToTable("IssueLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LinkType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.SourceIssueId, x.TargetIssueId, x.LinkType }).IsUnique().HasDatabaseName("UQ_IssueLink");
        builder.HasIndex(x => x.TargetIssueId).HasDatabaseName("IX_IssueLink_Target");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.SourceIssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.TargetIssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueWatcherConfiguration : IEntityTypeConfiguration<IssueWatcher>
{
    public void Configure(EntityTypeBuilder<IssueWatcher> builder)
    {
        builder.ToTable("IssueWatcher");
        builder.HasKey(x => new { x.IssueId, x.UserId });
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_IssueWatcher_UserId");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).HasColumnType("mediumtext").IsRequired();
        builder.Property(x => x.MentionedUserIds).HasColumnType("json");
        builder.Property(x => x.IsEdited).HasDefaultValue(false);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => new { x.IssueId, x.CreatedAt }).HasDatabaseName("IX_Comment_Issue");
        builder.HasIndex(x => x.ParentCommentId).HasDatabaseName("IX_Comment_Parent");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Comment>().WithMany().HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoredPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => x.IssueId).HasDatabaseName("IX_Attachment_Issue");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(60).IsRequired();
        builder.Property(x => x.FieldName).HasMaxLength(80);
        builder.Property(x => x.OldValue).HasColumnType("text");
        builder.Property(x => x.NewValue).HasColumnType("text");
        builder.Property(x => x.Detail).HasColumnType("json");
        builder.Property(x => x.Source).HasMaxLength(20).HasDefaultValue("User");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.IssueId, x.CreatedAt }).HasDatabaseName("IX_ActivityLog_Issue");
        builder.HasIndex(x => new { x.Action, x.CreatedAt }).HasDatabaseName("IX_ActivityLog_Action");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueStatusHistoryConfiguration : IEntityTypeConfiguration<IssueStatusHistory>
{
    public void Configure(EntityTypeBuilder<IssueStatusHistory> builder)
    {
        builder.ToTable("IssueStatusHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromCategory).HasMaxLength(20);
        builder.Property(x => x.ToCategory).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ChangedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.IssueId, x.ChangedAt }).HasDatabaseName("IX_IssueStatusHistory_Issue");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueAssignmentHistoryConfiguration : IEntityTypeConfiguration<IssueAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<IssueAssignmentHistory> builder)
    {
        builder.ToTable("IssueAssignmentHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssignmentSource).HasMaxLength(20).HasDefaultValue("Manual");
        builder.Property(x => x.StoryPointsAtTime).HasPrecision(6, 2);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.AssignedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.IssueId, x.AssignedAt }).HasDatabaseName("IX_IssueAssignHistory_Issue");
        builder.HasIndex(x => new { x.ToAssigneeId, x.AssignedAt }).HasDatabaseName("IX_IssueAssignHistory_To");
        builder.HasIndex(x => x.AssignmentSource).HasDatabaseName("IX_IssueAssignHistory_Source");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("Label");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ColorHex).HasColumnType("char(7)").HasDefaultValue("#DFE1E6");
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("UQ_Label_Name");
    }
}

internal sealed class IssueLabelConfiguration : IEntityTypeConfiguration<IssueLabel>
{
    public void Configure(EntityTypeBuilder<IssueLabel> builder)
    {
        builder.ToTable("IssueLabel");
        builder.HasKey(x => new { x.IssueId, x.LabelId });
        builder.HasIndex(x => x.LabelId).HasDatabaseName("IX_IssueLabel_Label");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Label>().WithMany().HasForeignKey(x => x.LabelId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueComponentLinkConfiguration : IEntityTypeConfiguration<IssueComponentLink>
{
    public void Configure(EntityTypeBuilder<IssueComponentLink> builder)
    {
        builder.ToTable("IssueComponentLink");
        builder.HasKey(x => new { x.IssueId, x.ComponentId });
        builder.HasIndex(x => x.ComponentId).HasDatabaseName("IX_IssueComponent_Component");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueVersionLinkConfiguration : IEntityTypeConfiguration<IssueVersionLink>
{
    public void Configure(EntityTypeBuilder<IssueVersionLink> builder)
    {
        builder.ToTable("IssueVersionLink");
        builder.HasKey(x => new { x.IssueId, x.VersionId, x.LinkType });
        builder.Property(x => x.LinkType).HasMaxLength(20).HasDefaultValue("FixVersion");
        builder.HasIndex(x => x.VersionId).HasDatabaseName("IX_IssueVersion_Version");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueRequiredSkillConfiguration : IEntityTypeConfiguration<IssueRequiredSkill>
{
    public void Configure(EntityTypeBuilder<IssueRequiredSkill> builder)
    {
        builder.ToTable("IssueRequiredSkill");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MinLevel).HasColumnType("tinyint").HasDefaultValue(1);
        builder.Property(x => x.Weight).HasPrecision(4, 3).HasDefaultValue(1m);
        builder.Property(x => x.Source).HasMaxLength(20).HasDefaultValue("Manual");
        builder.HasIndex(x => new { x.IssueId, x.SkillId }).IsUnique().HasDatabaseName("UQ_IssueRequiredSkill");
        builder.HasIndex(x => x.SkillId).HasDatabaseName("IX_IssueRequiredSkill_Skill");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AcceptanceCriteriaConfiguration : IEntityTypeConfiguration<AcceptanceCriteria>
{
    public void Configure(EntityTypeBuilder<AcceptanceCriteria> builder)
    {
        builder.ToTable("AcceptanceCriteria");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.Property(x => x.IsMet).HasDefaultValue(false);
        builder.Property(x => x.OrderIndex).HasDefaultValue(0);
        builder.Property(x => x.Source).HasMaxLength(20).HasDefaultValue("Manual");
        builder.Property(x => x.WasEditedAfterAi).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => new { x.IssueId, x.OrderIndex }).HasDatabaseName("IX_AcceptanceCriteria_Issue");
        builder.HasIndex(x => x.Source).HasDatabaseName("IX_AcceptanceCriteria_Source");
        builder.HasOne<Issue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
    }
}
