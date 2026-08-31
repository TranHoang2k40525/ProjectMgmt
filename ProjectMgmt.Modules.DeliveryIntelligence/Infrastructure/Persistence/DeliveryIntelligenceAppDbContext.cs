using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;

public class DeliveryIntelligenceAppDbContext : DbContext
{
    public DeliveryIntelligenceAppDbContext(DbContextOptions<DeliveryIntelligenceAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Issue> Issues { get; set; } = null!;
    public DbSet<IssueLink> IssueLinks { get; set; } = null!;
    public DbSet<IssueWatcher> IssueWatchers { get; set; } = null!;
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<Attachment> Attachments { get; set; } = null!;
    public DbSet<ActivityLog> ActivityLogs { get; set; } = null!;
    public DbSet<IssueStatusHistory> IssueStatusHistory { get; set; } = null!;
    public DbSet<IssueAssignmentHistory> IssueAssignmentHistory { get; set; } = null!;
    public DbSet<Label> Labels { get; set; } = null!;
    public DbSet<IssueLabel> IssueLabels { get; set; } = null!;
    public DbSet<IssueComponentLink> IssueComponentLinks { get; set; } = null!;
    public DbSet<IssueVersionLink> IssueVersionLinks { get; set; } = null!;
    public DbSet<IssueRequiredSkill> IssueRequiredSkills { get; set; } = null!;
    public DbSet<AcceptanceCriteria> AcceptanceCriteria { get; set; } = null!;
    public DbSet<AiModel> AiModels { get; set; } = null!;
    public DbSet<AiPromptTemplate> AiPromptTemplates { get; set; } = null!;
    public DbSet<AiDataset> AiDatasets { get; set; } = null!;
    public DbSet<AiDatasetVersion> AiDatasetVersions { get; set; } = null!;
    public DbSet<AiDatasetSample> AiDatasetSamples { get; set; } = null!;
    public DbSet<AiDataCleaningRule> AiDataCleaningRules { get; set; } = null!;
    public DbSet<AiDataQualityFlag> AiDataQualityFlags { get; set; } = null!;
    public DbSet<AiTrainingRun> AiTrainingRuns { get; set; } = null!;
    public DbSet<AiEvaluationResult> AiEvaluationResults { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryIntelligenceAppDbContext).Assembly);
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
