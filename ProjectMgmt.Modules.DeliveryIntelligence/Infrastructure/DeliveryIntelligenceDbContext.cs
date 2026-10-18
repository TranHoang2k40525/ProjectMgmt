using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DeliveryIntelligence.Infrastructure;

public class DeliveryIntelligenceDbContext : DbContext
{
    public DeliveryIntelligenceDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueLink> IssueLinks => Set<IssueLink>();
    public DbSet<IssueWatcher> IssueWatchers => Set<IssueWatcher>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<IssueStatusHistory> IssueStatusHistories => Set<IssueStatusHistory>();
    public DbSet<IssueAssignmentHistory> IssueAssignmentHistories => Set<IssueAssignmentHistory>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();
    public DbSet<IssueComponentLink> IssueComponentLinks => Set<IssueComponentLink>();
    public DbSet<IssueVersionLink> IssueVersionLinks => Set<IssueVersionLink>();
    public DbSet<IssueRequiredSkill> IssueRequiredSkills => Set<IssueRequiredSkill>();
    public DbSet<AcceptanceCriteria> AcceptanceCriteria => Set<AcceptanceCriteria>();
    public DbSet<AiDataset> AiDatasets => Set<AiDataset>();
    public DbSet<AiDatasetVersion> AiDatasetVersions => Set<AiDatasetVersion>();
    public DbSet<AiDatasetSample> AiDatasetSamples => Set<AiDatasetSample>();
    public DbSet<AiDataCleaningRule> AiDataCleaningRules => Set<AiDataCleaningRule>();
    public DbSet<AiDataQualityFlag> AiDataQualityFlags => Set<AiDataQualityFlag>();
    public DbSet<AiTrainingRun> AiTrainingRuns => Set<AiTrainingRun>();
    public DbSet<AiEvaluationResult> AiEvaluationResults => Set<AiEvaluationResult>();
    public DbSet<UserActiveWorkload> UserActiveWorkloads => Set<UserActiveWorkload>();
    public DbSet<SprintVelocity> SprintVelocities => Set<SprintVelocity>();
    public DbSet<AiBreakdownQuality> AiBreakdownQualities => Set<AiBreakdownQuality>();
    public DbSet<AiAssignmentAccuracy> AiAssignmentAccuracies => Set<AiAssignmentAccuracy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryIntelligenceDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
    }
}
