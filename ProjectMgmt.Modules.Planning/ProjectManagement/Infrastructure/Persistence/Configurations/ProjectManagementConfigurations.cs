using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Infrastructure.Persistence.Configurations;

internal class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organization");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UQ_Organization_Slug");
        builder.HasIndex(x => x.OwnerId).HasDatabaseName("IX_Organization_OwnerId");
    }
}

internal class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Project");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProjectKey).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.IssueCounter).HasDefaultValue(0);
        builder.Property(x => x.IsArchived).HasDefaultValue(false);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => new { x.OrgId, x.ProjectKey }).IsUnique().HasDatabaseName("UQ_Project_Org_Key");
        builder.HasIndex(x => x.LeadUserId).HasDatabaseName("IX_Project_LeadUserId");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ProjectComponentConfiguration : IEntityTypeConfiguration<ProjectComponent>
{
    public void Configure(EntityTypeBuilder<ProjectComponent> builder)
    {
        builder.ToTable("ProjectComponent");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("UQ_ProjectComponent_Name");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class ProjectVersionConfiguration : IEntityTypeConfiguration<ProjectVersion>
{
    public void Configure(EntityTypeBuilder<ProjectVersion> builder)
    {
        builder.ToTable("ProjectVersion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IsReleased).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("UQ_ProjectVersion_Name");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("WorkflowStatus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ColorHex).HasColumnType("char(7)").HasDefaultValue("#8993A4");
        builder.Property(x => x.OrderIndex).HasDefaultValue(0);
        builder.Property(x => x.IsInitial).HasDefaultValue(false);
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("UQ_WorkflowStatus_Name");
        builder.HasIndex(x => new { x.ProjectId, x.OrderIndex }).HasDatabaseName("IX_WorkflowStatus_Project_Order");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransition");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.RequiredPermissionCode).HasMaxLength(80);
        builder.HasIndex(x => new { x.ProjectId, x.FromStatusId, x.ToStatusId }).IsUnique().HasDatabaseName("UQ_WorkflowTransition");
        builder.HasIndex(x => x.FromStatusId).HasDatabaseName("IX_WorkflowTransition_From");
        builder.HasIndex(x => x.ToStatusId).HasDatabaseName("IX_WorkflowTransition_To");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(x => x.FromStatusId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(x => x.ToStatusId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class IssueTypeConfiguration : IEntityTypeConfiguration<IssueType>
{
    public void Configure(EntityTypeBuilder<IssueType> builder)
    {
        builder.ToTable("IssueType");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.IconKey).HasMaxLength(50);
        builder.Property(x => x.ColorHex).HasColumnType("char(7)").HasDefaultValue("#0052CC");
        builder.Property(x => x.IsSubtask).HasDefaultValue(false);
        builder.Property(x => x.HierarchyLevel).HasColumnType("tinyint").HasDefaultValue(1);
        builder.Property(x => x.OrderIndex).HasDefaultValue(0);
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("UQ_IssueType_Name");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class PriorityConfiguration : IEntityTypeConfiguration<Priority>
{
    public void Configure(EntityTypeBuilder<Priority> builder)
    {
        builder.ToTable("Priority");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ColorHex).HasColumnType("char(7)").HasDefaultValue("#6B778C");
        builder.Property(x => x.IconKey).HasMaxLength(50);
        builder.HasIndex(x => new { x.ProjectId, x.Level }).HasDatabaseName("IX_Priority_Project");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("Board");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IsDefault).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("UQ_Board_Name");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.ToTable("BoardColumn");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.OrderIndex).HasDefaultValue(0);
        builder.HasIndex(x => new { x.BoardId, x.StatusId }).IsUnique().HasDatabaseName("UQ_BoardColumn_Status");
        builder.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Cascade);
    }
}
