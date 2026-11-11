using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>Integration test trên MySQL thật (bỏ qua khi chưa cấu hình PROJECTMGMT_TEST_MYSQL).</summary>
[Collection(MySqlCollection.Name)]
public class IssueRepositoryMySqlTests
{
    private readonly MySqlTestDatabase _database;

    public IssueRepositoryMySqlTests(MySqlTestDatabase database)
    {
        _database = database;
    }

    [MySqlFact]
    public async Task SharedSeedIsReadableThroughRepository()
    {
        await using var context = _database.CreateDeliveryContext();
        var repository = new IssueRepository(context);

        var story = await repository.GetByNumberAsync(SharedTestData.ProjectId, 2);
        Assert.Equal(SharedTestData.StoryIssueId, story?.Id);
        Assert.Equal([SharedTestData.SubtaskIssueId], (await repository.GetChildrenAsync(SharedTestData.StoryIssueId)).Select(i => i.Id));
        Assert.Null(await repository.GetByNumberAsync(SharedTestData.ProjectId, 999));
    }

    [MySqlFact]
    public async Task ListAppliesFiltersAndPagingInSql()
    {
        await using var context = _database.CreateDeliveryContext();
        var repository = new IssueRepository(context);

        var sprintPage = await repository.ListAsync(new IssueListFilter
        {
            ProjectId = SharedTestData.ProjectId,
            SprintId = SharedTestData.ActiveSprintId,
            PageSize = 2
        });
        Assert.Equal(3, sprintPage.TotalCount);
        Assert.Equal([2, 3], sprintPage.Items.Select(i => i.IssueNumber));

        var backlog = await repository.ListAsync(new IssueListFilter { ProjectId = SharedTestData.ProjectId, BacklogOnly = true });
        Assert.Equal([1, 4], backlog.Items.Select(i => i.IssueNumber));

        var search = await repository.ListAsync(new IssueListFilter { ProjectId = SharedTestData.ProjectId, SearchText = "avatar" });
        Assert.Equal(SharedTestData.BugIssueId, Assert.Single(search.Items).Id);

        var other = await repository.ListAsync(new IssueListFilter { ProjectId = SharedTestData.OtherProjectId });
        Assert.Equal(SharedTestData.OtherProjectIssueId, Assert.Single(other.Items).Id);
    }

    [MySqlFact]
    public async Task DuplicateIssueNumberInSameProjectIsRejectedByDatabase()
    {
        await using var context = _database.CreateDeliveryContext();
        var repository = new IssueRepository(context);
        await repository.AddAsync(new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = SharedTestData.ProjectId,
            IssueNumber = 1,
            IssueTypeId = SharedTestData.TaskTypeId,
            StatusId = SharedTestData.BacklogStatusId,
            ReporterId = SharedTestData.OwnerUserId,
            Title = "Trùng số",
            RankOrder = 99,
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    [MySqlFact]
    public async Task SelfParentIsBlockedByDatabaseTrigger()
    {
        await using var context = _database.CreateDeliveryContext();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE `Issue` SET `ParentId` = `Id` WHERE `Id` = {SharedTestData.BacklogTaskIssueId}"));
        Assert.Contains("parent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
