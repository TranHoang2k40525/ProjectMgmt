using Microsoft.EntityFrameworkCore;
using ProjectMgmt.BuildingBlocks.Persistence;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Entities;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Entities;
using ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.Infrastructure.Persistence;

public sealed class PlanningDbContext(DbContextOptions<PlanningDbContext> options) : DbContext(options)
{
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanningDbContext).Assembly);
        modelBuilder.ApplyProjectMgmtColumnConventions();
    }
}
