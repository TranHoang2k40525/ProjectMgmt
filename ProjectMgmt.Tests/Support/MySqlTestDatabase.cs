using System.Globalization;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using Xunit;

namespace ProjectMgmt.Tests.Support;

/// <summary>
/// Chỉ chạy khi biến môi trường PROJECTMGMT_TEST_MYSQL chứa connection string tới một MySQL dùng để test
/// (tài khoản cần quyền CREATE/DROP DATABASE). Không có biến này thì test được bỏ qua, CI vẫn xanh.
/// </summary>
public class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (!MySqlTestDatabase.IsConfigured)
        {
            Skip = $"Đặt biến môi trường {MySqlTestDatabase.ConnectionVariable} để chạy integration test MySQL.";
        }
    }
}

[CollectionDefinition(Name)]
public class MySqlCollection : ICollectionFixture<MySqlTestDatabase>
{
    public const string Name = "mysql";
}

/// <summary>
/// Harness integration dùng chung cho A/B/C: tạo database tạm tên ngẫu nhiên, chạy migration của ba DbContext
/// theo thứ tự Identity → Planning → Delivery, seed <see cref="SharedTestData"/>, và DROP đúng database đó khi xong.
/// Không bao giờ dùng database ghi trong connection string, nên không đụng vào dữ liệu thật.
/// </summary>
public class MySqlTestDatabase : IAsyncLifetime
{
    public const string ConnectionVariable = "PROJECTMGMT_TEST_MYSQL";
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 46));

    private readonly string _databaseName =
        "projectmgmt_test_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

    private string? _serverConnectionString;
    private bool _created;

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        var server = new MySqlConnectionStringBuilder(configured) { Database = string.Empty };
        _serverConnectionString = server.ConnectionString;
        await ExecuteOnServerAsync(
            $"CREATE DATABASE `{_databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci");
        _created = true;
        ConnectionString = new MySqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ConnectionString;

        await using (var identity = CreateIdentityContext())
        {
            await identity.Database.MigrateAsync();
        }

        await using (var planning = CreatePlanningContext())
        {
            await planning.Database.MigrateAsync();
        }

        await using (var delivery = CreateDeliveryContext())
        {
            await delivery.Database.MigrateAsync();
        }

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_created)
        {
            await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS `{_databaseName}`");
        }
    }

    public IdentityExperienceDbContext CreateIdentityContext() =>
        new(Options<IdentityExperienceDbContext>("__EFMigrationsHistory_IdentityExperience"));

    public PlanningDbContext CreatePlanningContext() =>
        new(Options<PlanningDbContext>("__EFMigrationsHistory_Planning"));

    public DeliveryIntelligenceDbContext CreateDeliveryContext() =>
        new(Options<DeliveryIntelligenceDbContext>("__EFMigrationsHistory_DeliveryIntelligence"));

    private DbContextOptions<TContext> Options<TContext>(string historyTable)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseMySql(ConnectionString, ServerVersion, mysql => mysql.MigrationsHistoryTable(historyTable))
            .Options;

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new MySqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync()
    {
        var now = SharedTestData.SeededAtUtc;
        await using (var identity = CreateIdentityContext())
        {
            foreach (var user in SharedTestData.Users)
            {
                var email = user.DisplayName.Replace(' ', '.').ToLowerInvariant() + "@test.local";
                identity.Users.Add(new User
                {
                    Id = user.UserId,
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    IsEmailVerified = true,
                    IsActive = true,
                    SecurityStamp = Guid.NewGuid(),
                    CreatedAt = now
                });
                identity.UserProfiles.Add(new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    DisplayName = user.DisplayName,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = now
                });
            }

            foreach (var (userId, roleId) in SharedTestData.DemoMembers)
            {
                identity.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleId = roleId,
                    ScopeType = "Project",
                    ScopeId = SharedTestData.ProjectId,
                    GrantedBy = SharedTestData.OwnerUserId,
                    CreatedAt = now
                });
            }

            identity.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = SharedTestData.SystemAdminUserId,
                RoleId = SharedTestData.AdminRoleId,
                ScopeType = "System",
                ScopeId = null,
                CreatedAt = now
            });
            await identity.SaveChangesAsync();
        }

        await using (var planning = CreatePlanningContext())
        {
            planning.Organizations.Add(new Organization
            {
                Id = SharedTestData.OrganizationId,
                Name = "Demo Organization",
                Slug = "demo-org",
                OwnerId = SharedTestData.OwnerUserId,
                IsActive = true,
                CreatedAt = now
            });
            planning.Projects.Add(NewProject(SharedTestData.ProjectId, SharedTestData.ProjectKey, "Demo Project", now));
            planning.Projects.Add(NewProject(SharedTestData.OtherProjectId, SharedTestData.OtherProjectKey, "Other Project", now));
            planning.IssueTypes.AddRange(SharedTestData.IssueTypes.Select(type => new IssueType
            {
                Id = type.Id,
                ProjectId = type.ProjectId,
                Name = type.Name,
                ColorHex = type.ColorHex,
                HierarchyLevel = (sbyte)type.HierarchyLevel,
                IsSubtask = type.IsSubtask,
                OrderIndex = type.OrderIndex
            }));
            planning.WorkflowStatuses.AddRange(SharedTestData.Statuses.Select(status => new WorkflowStatus
            {
                Id = status.Id,
                ProjectId = status.ProjectId,
                Name = status.Name,
                Category = status.Category,
                ColorHex = status.ColorHex,
                OrderIndex = status.OrderIndex,
                IsInitial = status.IsInitial
            }));
            planning.WorkflowTransitions.AddRange(SharedTestData.Transitions.Select(transition => new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                ProjectId = SharedTestData.ProjectId,
                FromStatusId = transition.From,
                ToStatusId = transition.To,
                Name = transition.Name
            }));
            planning.Sprints.Add(new Sprint
            {
                Id = SharedTestData.ActiveSprintId,
                ProjectId = SharedTestData.ProjectId,
                Name = "DEMO Sprint 1",
                Status = "Active",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 9, 14),
                ActualStartAt = now,
                CreatedAt = now
            });
            await planning.SaveChangesAsync();
        }

        await using (var delivery = CreateDeliveryContext())
        {
            delivery.Issues.AddRange(SharedTestData.CreateIssues());
            await delivery.SaveChangesAsync();
        }
    }

    private static Project NewProject(Guid id, string key, string name, DateTime now) => new()
    {
        Id = id,
        OrgId = SharedTestData.OrganizationId,
        ProjectKey = key,
        Name = name,
        CreatedByUserId = SharedTestData.OwnerUserId,
        LeadUserId = SharedTestData.OwnerUserId,
        IssueCounter = SharedTestData.IssueCounter(id),
        CreatedAt = now
    };
}
