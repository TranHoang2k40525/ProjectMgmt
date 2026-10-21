using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>Kiểm mô hình EF của bảng Issue (SCRUMAI-166) mà không cần kết nối MySQL.</summary>
public class DeliveryIssueSchemaTests
{
    private static readonly IEntityType IssueEntity = BuildModel().FindEntityType(typeof(Issue))!;

    [Fact]
    public void IssueNumberIsUniquePerProject()
    {
        var index = IssueEntity.GetIndexes().Single(i => i.GetDatabaseName() == "UQ_Issue_Key");
        Assert.True(index.IsUnique);
        Assert.Equal([nameof(Issue.ProjectId), nameof(Issue.IssueNumber)], index.Properties.Select(p => p.Name));
    }

    [Fact]
    public void RankOrderUsesHighPrecisionDecimal()
    {
        Assert.Equal("decimal(30,15)", IssueEntity.FindProperty(nameof(Issue.RankOrder))!.GetColumnType());
        Assert.Equal("decimal(6,2)", IssueEntity.FindProperty(nameof(Issue.StoryPoints))!.GetColumnType());
    }

    [Fact]
    public void ParentIsSelfReferenceInsideDeliveryModuleOnly()
    {
        var foreignKeys = IssueEntity.GetForeignKeys().ToList();
        var parent = Assert.Single(foreignKeys);
        Assert.Equal(nameof(Issue.ParentId), parent.Properties.Single().Name);
        Assert.Equal(IssueEntity, parent.PrincipalEntityType);
        Assert.Equal("FK_Issue_Parent", parent.GetConstraintName());
        Assert.Equal(DeleteBehavior.SetNull, parent.DeleteBehavior);
    }

    [Theory]
    [InlineData(nameof(Issue.ProjectId))]
    [InlineData(nameof(Issue.SprintId))]
    [InlineData(nameof(Issue.AssigneeId))]
    [InlineData(nameof(Issue.ReporterId))]
    [InlineData(nameof(Issue.ParentId))]
    [InlineData(nameof(Issue.EpicId))]
    [InlineData(nameof(Issue.AiGenerationLogId))]
    public void FilteredReferenceColumnsLeadAnIndex(string column)
    {
        Assert.Contains(IssueEntity.GetIndexes(), index => index.Properties[0].Name == column);
    }

    [Fact]
    public void AiAndSoftDeleteFlagsExistWithSafeDefaults()
    {
        foreach (var name in new[] { nameof(Issue.IsAiGenerated), nameof(Issue.IsAiAssigned), nameof(Issue.IsDeleted) })
        {
            var property = IssueEntity.FindProperty(name)!;
            Assert.Equal("tinyint(1)", property.GetColumnType());
            Assert.Equal(false, property.GetDefaultValue());
        }

        var checks = IssueEntity.GetCheckConstraints().Select(c => c.Name).ToList();
        Assert.Contains("CK_Issue_Points", checks);
        Assert.Contains("CK_Issue_AiLogConsistency", checks);
    }

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>()
            .UseMySql("Server=localhost;Database=model_only", new MySqlServerVersion(new Version(8, 0, 46)))
            .Options;
        using var context = new DeliveryIntelligenceDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
