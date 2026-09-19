using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Planning.Infrastructure;

public class PlanningDbContext : DbContext
{
    public PlanningDbContext(DbContextOptions<PlanningDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectComponent> ProjectComponents => Set<ProjectComponent>();
    public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<IssueType> IssueTypes => Set<IssueType>();
    public DbSet<Priority> Priorities => Set<Priority>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<SprintSnapshot> SprintSnapshots => Set<SprintSnapshot>();
    public DbSet<SprintMemberCapacity> SprintMemberCapacities => Set<SprintMemberCapacity>();
    public DbSet<UserWorkloadSnapshot> UserWorkloadSnapshots => Set<UserWorkloadSnapshot>();
    public DbSet<UserPerformanceMetric> UserPerformanceMetrics => Set<UserPerformanceMetric>();
    public DbSet<AiAssignmentRun> AiAssignmentRuns => Set<AiAssignmentRun>();
    public DbSet<AiAssignmentCandidate> AiAssignmentCandidates => Set<AiAssignmentCandidate>();
    public DbSet<AiAssignmentDecision> AiAssignmentDecisions => Set<AiAssignmentDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanningDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
    }
}
