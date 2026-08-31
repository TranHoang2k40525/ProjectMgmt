using Microsoft.EntityFrameworkCore;
using ProjectMgmt.BuildingBlocks.Persistence;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;

public sealed class DeliveryIntelligenceDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options)
    : DbContext(options)
{
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueLink> IssueLinks => Set<IssueLink>();
    public DbSet<IssueWatcher> IssueWatchers => Set<IssueWatcher>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<IssueStatusHistory> IssueStatusHistory => Set<IssueStatusHistory>();
    public DbSet<IssueAssignmentHistory> IssueAssignmentHistory => Set<IssueAssignmentHistory>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();
    public DbSet<IssueComponentLink> IssueComponentLinks => Set<IssueComponentLink>();
    public DbSet<IssueVersionLink> IssueVersionLinks => Set<IssueVersionLink>();
    public DbSet<IssueRequiredSkill> IssueRequiredSkills => Set<IssueRequiredSkill>();
    public DbSet<AcceptanceCriteria> AcceptanceCriteria => Set<AcceptanceCriteria>();
    public DbSet<AiModel> AiModels => Set<AiModel>();
    public DbSet<AiPromptTemplate> AiPromptTemplates => Set<AiPromptTemplate>();
    public DbSet<AiDataset> AiDatasets => Set<AiDataset>();
    public DbSet<AiDatasetVersion> AiDatasetVersions => Set<AiDatasetVersion>();
    public DbSet<AiDatasetSample> AiDatasetSamples => Set<AiDatasetSample>();
    public DbSet<AiDataCleaningRule> AiDataCleaningRules => Set<AiDataCleaningRule>();
    public DbSet<AiDataQualityFlag> AiDataQualityFlags => Set<AiDataQualityFlag>();
    public DbSet<AiTrainingRun> AiTrainingRuns => Set<AiTrainingRun>();
    public DbSet<AiEvaluationResult> AiEvaluationResults => Set<AiEvaluationResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryIntelligenceDbContext).Assembly);
        modelBuilder.ApplyProjectMgmtColumnConventions();
    }
}
