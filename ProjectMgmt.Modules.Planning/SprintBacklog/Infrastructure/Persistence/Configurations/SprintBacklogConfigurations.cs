using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.SprintBacklog.Infrastructure.Persistence.Configurations;

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprint");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Goal).HasMaxLength(1000);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Planned");
        builder.Property(x => x.OrderIndex).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.Property(x => x.ActiveGuard)
            .HasComputedColumnSql("IF(`Status` = 'Active', `ProjectId`, NULL)", stored: true);
        builder.HasIndex(x => x.ActiveGuard).IsUnique().HasDatabaseName("UQ_Sprint_OneActivePerProject");
        builder.HasIndex(x => new { x.ProjectId, x.Status }).HasDatabaseName("IX_Sprint_Project_Status");
    }
}

internal sealed class SprintSnapshotConfiguration : IEntityTypeConfiguration<SprintSnapshot>
{
    public void Configure(EntityTypeBuilder<SprintSnapshot> builder)
    {
        builder.ToTable("SprintSnapshot");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TotalPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.RemainingPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.CompletedPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.AddedPoints).HasPrecision(9, 2).HasDefaultValue(0m);
        builder.Property(x => x.RemainingIssueCount).HasDefaultValue(0);
        builder.Property(x => x.TotalIssueCount).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.SprintId, x.SnapshotDate }).IsUnique().HasDatabaseName("UQ_SprintSnapshot_Day");
        builder.HasOne<Sprint>().WithMany().HasForeignKey(x => x.SprintId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SprintMemberCapacityConfiguration : IEntityTypeConfiguration<SprintMemberCapacity>
{
    public void Configure(EntityTypeBuilder<SprintMemberCapacity> builder)
    {
        builder.ToTable("SprintMemberCapacity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CapacityPoints).HasPrecision(9, 2);
        builder.Property(x => x.AvailableHours).HasPrecision(7, 2);
        builder.Property(x => x.Note).HasMaxLength(255);
        builder.HasIndex(x => new { x.SprintId, x.UserId }).IsUnique().HasDatabaseName("UQ_SprintCapacity");
        builder.HasOne<Sprint>().WithMany().HasForeignKey(x => x.SprintId).OnDelete(DeleteBehavior.Cascade);
    }
}
