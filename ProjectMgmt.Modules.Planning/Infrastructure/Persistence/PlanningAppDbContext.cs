using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Entities;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Entities;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.Infrastructure.Persistence;

public class PlanningAppDbContext : DbContext
{
    public PlanningAppDbContext(DbContextOptions<PlanningAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<ProjectComponent> ProjectComponents { get; set; } = null!;
    public DbSet<ProjectVersion> ProjectVersions { get; set; } = null!;
    public DbSet<WorkflowStatus> WorkflowStatuses { get; set; } = null!;
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; } = null!;
    public DbSet<IssueType> IssueTypes { get; set; } = null!;
    public DbSet<Priority> Priorities { get; set; } = null!;
    public DbSet<Board> Boards { get; set; } = null!;
    public DbSet<BoardColumn> BoardColumns { get; set; } = null!;
    public DbSet<Sprint> Sprints { get; set; } = null!;
    public DbSet<SprintSnapshot> SprintSnapshots { get; set; } = null!;
    public DbSet<SprintMemberCapacity> SprintMemberCapacities { get; set; } = null!;
    public DbSet<UserWorkloadSnapshot> UserWorkloadSnapshots { get; set; } = null!;
    public DbSet<UserPerformanceMetric> UserPerformanceMetrics { get; set; } = null!;
    public DbSet<AiAssignmentRun> AiAssignmentRuns { get; set; } = null!;
    public DbSet<AiAssignmentCandidate> AiAssignmentCandidates { get; set; } = null!;
    public DbSet<AiAssignmentDecision> AiAssignmentDecisions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanningAppDbContext).Assembly);
        ApplyColumnConventions(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyColumnConventions(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type == typeof(Guid))
            {
                property.SetColumnType("char(36)");
            }
            else if (type == typeof(DateTime))
            {
                property.SetColumnType("datetime(6)");
            }
            else if (type == typeof(DateOnly))
            {
                property.SetColumnType("date");
            }
        }
    }
}
