using System.Reflection;
using Planning.Domain.Entities;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;
using Xunit;

namespace ProjectMgmt.Tests;

public class PlanningServiceTests
{
    [Fact]
    public async Task EmptyOrganizationListDoesNotWrite()
    {
        var f = new Fixture();
        var result = await f.Service.GetOrganizationsAsync();
        Assert.Empty(result.Value);
        Assert.Equal(0, f.Writes);
        Assert.DoesNotContain("AddOrganizationAsync", f.Proxy.Calls);
    }

    [Fact]
    public async Task ProjectCreationSeedsAllConfigurationInOneSave()
    {
        var f = new Fixture();
        var result = await f.Service.CreateProjectAsync(new(f.Organization.Id, "demo", " Project ", null, Guid.NewGuid()));
        Assert.True(result.IsSuccess);
        Assert.Equal("DEMO", result.Value.ProjectKey);
        Assert.Equal("Project", result.Value.Name);
        Assert.Equal(5, f.Types.Count);
        Assert.Equal(5, f.Statuses.Count);
        Assert.Equal(7, f.Transitions.Count);
        Assert.Equal(4, f.Columns.Count);
        Assert.All(f.Columns, c => Assert.Contains(f.Statuses, status => status.Id == c.StatusId));
        Assert.Equal(1, f.Writes);
    }

    [Fact]
    public async Task ProjectKeyReservedByDeletedProjectReturnsConflict()
    {
        var f = new Fixture();
        f.ExistingKey = new Project { IsDeleted = true };
        var result = await f.Service.CreateProjectAsync(new(f.Organization.Id, "DEMO", "Project", null, Guid.NewGuid()));
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task ProjectCreationRejectsInactiveOrganization()
    {
        var f = new Fixture();
        f.Organization.IsActive = false;
        var result = await f.Service.CreateProjectAsync(new(f.Organization.Id, "DEMO", "Project", null, Guid.NewGuid()));
        Assert.Equal("PROJECT_ORGANIZATION_INVALID", result.Error.Code);
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidWipIsRejectedBeforeSaving(int wip)
    {
        var f = new Fixture();
        var result = await f.Service.CreateBoardColumnAsync(f.Board.Id, new(Guid.NewGuid(), "Column", wip));
        Assert.Equal("WIP_LIMIT_INVALID", result.Error.Code);
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task DuplicateColumnStatusReturnsConflict()
    {
        var f = new Fixture();
        var status = new WorkflowStatus { Id = Guid.NewGuid(), Name = "Todo", ProjectId = f.ProjectId };
        f.Statuses.Add(status);
        f.Columns.Add(new BoardColumn { Id = Guid.NewGuid(), BoardId = f.Board.Id, StatusId = status.Id });
        var result = await f.Service.CreateBoardColumnAsync(f.Board.Id, new(status.Id, null, null));
        Assert.Equal("COLUMN_STATUS_EXISTS", result.Error.Code);
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task ColumnCannotUseStatusOutsideProject()
    {
        var f = new Fixture();
        var result = await f.Service.CreateBoardColumnAsync(f.Board.Id, new(Guid.NewGuid(), null, null));
        Assert.Equal("COLUMN_STATUS_INVALID", result.Error.Code);
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task ReorderRequiresAnExactPermutationAndDoesNotPartiallyMutate()
    {
        var f = new Fixture();
        f.Columns.AddRange([
            new BoardColumn { Id = Guid.NewGuid(), OrderIndex = 1 },
            new BoardColumn { Id = Guid.NewGuid(), OrderIndex = 2 }
        ]);
        foreach (var ids in new IReadOnlyList<Guid>[] {
            [f.Columns[0].Id], [f.Columns[0].Id, f.Columns[0].Id], [f.Columns[0].Id, Guid.NewGuid()]
        })
        {
            var result = await f.Service.ReorderBoardColumnsAsync(f.Board.Id, new(ids));
            Assert.Equal("COLUMN_ORDER_INVALID", result.Error.Code);
        }
        Assert.Equal([1, 2], f.Columns.Select(c => c.OrderIndex));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task ValidReorderUpdatesEveryColumnAndSaves()
    {
        var f = new Fixture();
        var first = new BoardColumn { Id = Guid.NewGuid(), OrderIndex = 1 };
        var second = new BoardColumn { Id = Guid.NewGuid(), OrderIndex = 2 };
        f.Columns.AddRange([first, second]);
        var result = await f.Service.ReorderBoardColumnsAsync(f.Board.Id, new([second.Id, first.Id]));
        Assert.True(result.IsSuccess);
        Assert.Equal(2, first.OrderIndex);
        Assert.Equal(1, second.OrderIndex);
        Assert.Equal(1, f.Writes);
    }

    [Fact]
    public async Task MissingBoardReturnsNotFoundEvenForEmptyOrder()
    {
        var f = new Fixture();
        f.BoardExists = false;
        var result = await f.Service.ReorderBoardColumnsAsync(Guid.NewGuid(), new([]));
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task WorkflowRejectsSelfTransitionAndForeignStatuses()
    {
        var f = new Fixture();
        var id = Guid.NewGuid();
        var self = await f.Service.CreateWorkflowTransitionAsync(f.ProjectId, new(id, id, null, null));
        Assert.Equal("WORKFLOW_SELF_TRANSITION", self.Error.Code);
        var foreign = await f.Service.CreateWorkflowTransitionAsync(f.ProjectId, new(id, Guid.NewGuid(), null, null));
        Assert.Equal("WORKFLOW_STATUS_NOT_IN_PROJECT", foreign.Error.Code);
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(null)]
    public async Task UpdateColumnPreservesMappingAndOrder(int? wip)
    {
        var f = new Fixture();
        var statusId = Guid.NewGuid();
        f.Statuses.Add(new WorkflowStatus { Id = statusId, Name = "Todo" });
        var column = new BoardColumn { Id = Guid.NewGuid(), BoardId = f.Board.Id, StatusId = statusId, Name = "Old", WipLimit = 5, OrderIndex = 7 };
        f.Columns.Add(column);
        var result = await f.Service.UpdateBoardColumnAsync(column.Id, new() { Name = "  Updated  ", WipLimit = wip });
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", column.Name);
        Assert.Equal(wip, column.WipLimit);
        Assert.Equal(statusId, result.Value.StatusId);
        Assert.Equal(7, result.Value.OrderIndex);
        Assert.Equal(1, f.Writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateColumnRejectsInvalidWipWithoutMutation(int wip)
    {
        var f = new Fixture();
        var column = new BoardColumn { Id = Guid.NewGuid(), BoardId = f.Board.Id, Name = "Old", WipLimit = 5 };
        f.Columns.Add(column);
        var result = await f.Service.UpdateBoardColumnAsync(column.Id, new() { Name = "New", WipLimit = wip });
        Assert.Equal("WIP_LIMIT_INVALID", result.Error.Code);
        Assert.Equal("Old", column.Name);
        Assert.Equal(5, column.WipLimit);
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task UpdateColumnClearsNameOverride()
    {
        var f = new Fixture();
        var status = new WorkflowStatus { Id = Guid.NewGuid(), Name = "Todo" };
        f.Statuses.Add(status);
        var column = new BoardColumn { Id = Guid.NewGuid(), BoardId = f.Board.Id, StatusId = status.Id, Name = "Old" };
        f.Columns.Add(column);
        var result = await f.Service.UpdateBoardColumnAsync(column.Id, new() { Name = "   " });
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Name);
        Assert.Equal("Todo", result.Value.StatusName);
    }

    [Fact]
    public async Task UpdateColumnRejectsMissingColumnBoardAndLongName()
    {
        var f = new Fixture();
        var missing = await f.Service.UpdateBoardColumnAsync(Guid.NewGuid(), new());
        Assert.Equal(ErrorType.NotFound, missing.Error.Type);
        var column = new BoardColumn { Id = Guid.NewGuid(), BoardId = f.Board.Id };
        f.Columns.Add(column);
        var tooLong = await f.Service.UpdateBoardColumnAsync(column.Id, new() { Name = new string('x', 81) });
        Assert.Equal("COLUMN_NAME_TOO_LONG", tooLong.Error.Code);
        f.BoardExists = false;
        var missingBoard = await f.Service.UpdateBoardColumnAsync(column.Id, new());
        Assert.Equal(ErrorType.NotFound, missingBoard.Error.Type);
        Assert.Equal(0, f.Writes);
    }

    private class Fixture
    {
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Organization Organization { get; } = new() { Id = Guid.NewGuid(), IsActive = true };
        public Board Board { get; }
        public bool BoardExists { get; set; } = true;
        public Project? ExistingKey { get; set; }
        public List<IssueType> Types { get; } = [];
        public List<WorkflowStatus> Statuses { get; } = [];
        public List<WorkflowTransition> Transitions { get; } = [];
        public List<BoardColumn> Columns { get; } = [];
        public int Writes { get; private set; }
        public RepositoryProxy Proxy { get; }
        public ProjectManagementService Service { get; }

        public Fixture()
        {
            Board = new() { Id = Guid.NewGuid(), ProjectId = ProjectId };
            var repository = DispatchProxy.Create<IProjectRepository, RepositoryProxy>();
            Proxy = (RepositoryProxy)repository;
            Proxy.Handler = (name, args) => name switch
            {
                "ExistsAsync" => Task.FromResult(true),
                "GetOrganizationsAsync" => Task.FromResult<IReadOnlyList<Organization>>([]),
                "GetOrganizationByIdAsync" => Task.FromResult<Organization?>(Organization),
                "GetByKeyAsync" => Task.FromResult(ExistingKey),
                "GetBoardByIdAsync" => Task.FromResult(BoardExists ? Board : null),
                "GetBoardColumnsAsync" => Task.FromResult<IReadOnlyList<BoardColumn>>(Columns),
                "GetBoardColumnByIdAsync" => Task.FromResult(Columns.SingleOrDefault(c => c.Id == (Guid)args[0]!)),
                "GetStatusesAsync" => Task.FromResult<IReadOnlyList<WorkflowStatus>>(Statuses),
                "AddIssueTypesAsync" => Add(Types, args[0]),
                "AddWorkflowStatusesAsync" => Add(Statuses, args[0]),
                "AddWorkflowTransitionsAsync" => Add(Transitions, args[0]),
                "AddBoardColumnsAsync" => Add(Columns, args[0]),
                "AddAsync" or "AddBoardAsync" => Task.CompletedTask,
                "SaveChangesAsync" => Save(),
                _ => throw new NotSupportedException(name)
            };
            Service = new(repository);
        }

        private Task Save() { Writes++; return Task.CompletedTask; }
        private static Task Add<T>(List<T> list, object? value)
        {
            list.AddRange((IEnumerable<T>)value!);
            return Task.CompletedTask;
        }
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Func<string, object?[], object?> Handler { get; set; } = null!;
        public List<string> Calls { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod!.Name;
            Calls.Add(name);
            return Handler(name, args ?? []);
        }
    }
}
