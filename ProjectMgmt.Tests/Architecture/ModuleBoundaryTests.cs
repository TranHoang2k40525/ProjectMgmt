using System.Xml.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;
using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;
using ProjectMgmt.Modules.Planning.Infrastructure.Persistence;

namespace ProjectMgmt.Tests.Architecture;

public class ModuleBoundaryTests
{
    private static readonly string[] ModuleProjects =
    [
        "ProjectMgmt.Modules.IdentityExperience",
        "ProjectMgmt.Modules.Planning",
        "ProjectMgmt.Modules.DeliveryIntelligence"
    ];

    private static readonly string[] ExpectedProjects =
    [
        "ProjectMgmt.Core",
        "ProjectMgmt.Contracts",
        "ProjectMgmt.Modules.IdentityExperience",
        "ProjectMgmt.Modules.Planning",
        "ProjectMgmt.Modules.DeliveryIntelligence",
        "ProjectMgmt.Solution",
        "ProjectMgmt.Tests"
    ];

    private static readonly string[] LogicalModules =
    [
        "ProjectMgmt.Modules.IdentityExperience/IdentityAccess",
        "ProjectMgmt.Modules.IdentityExperience/Notification",
        "ProjectMgmt.Modules.IdentityExperience/AiAssist",
        "ProjectMgmt.Modules.Planning/ProjectManagement",
        "ProjectMgmt.Modules.Planning/SprintBacklog",
        "ProjectMgmt.Modules.Planning/AiAssignment",
        "ProjectMgmt.Modules.DeliveryIntelligence/IssueTracking",
        "ProjectMgmt.Modules.DeliveryIntelligence/AiCore",
        "ProjectMgmt.Modules.DeliveryIntelligence/AiDataOps"
    ];

    [Fact]
    public void SolutionContainsExactlySevenProjects()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "ProjectMgmt.slnx"));
        var projects = document.Descendants("Project")
            .Select(x => Path.GetFileNameWithoutExtension(x.Attribute("Path")?.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedProjects.Order(StringComparer.Ordinal), projects);
    }

    [Fact]
    public void BusinessModulesReferenceOnlyContractsAndCore()
    {
        var root = FindRepositoryRoot();
        var allowed = new[] { "ProjectMgmt.Contracts", "ProjectMgmt.Core" };

        var violations = ModuleProjects
            .Select(project => Path.Combine(root, project, $"{project}.csproj"))
            .SelectMany(project => ReadProjectReferences(project)
                .Where(reference => !allowed.Contains(reference, StringComparer.Ordinal))
                .Select(reference => $"{Path.GetFileNameWithoutExtension(project)} -> {reference}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ContractsAndCoreDoNotReferenceBusinessModules()
    {
        var root = FindRepositoryRoot();
        var contracts = ReadProjectReferences(Path.Combine(root, "ProjectMgmt.Contracts", "ProjectMgmt.Contracts.csproj"));
        var core = ReadProjectReferences(Path.Combine(root, "ProjectMgmt.Core", "ProjectMgmt.Core.csproj"));

        Assert.Equal(["ProjectMgmt.Core"], contracts);
        Assert.Empty(core);
    }

    [Fact]
    public void WebApiIsTheCompositionRootForAllThreeModules()
    {
        var host = Path.Combine(FindRepositoryRoot(), "ProjectMgmt.Solution", "ProjectMgmt.Solution.csproj");
        var references = ReadProjectReferences(host);

        Assert.All(ModuleProjects, module => Assert.Contains(module, references));
        Assert.DoesNotContain("ProjectMgmt.Host", references);
    }

    [Fact]
    public void WebApiUsesControllersDirectoryAndDoesNotUseApiDirectory()
    {
        var webApi = Path.Combine(FindRepositoryRoot(), "ProjectMgmt.Solution");

        Assert.True(Directory.Exists(Path.Combine(webApi, "Controllers")));
        Assert.False(Directory.Exists(Path.Combine(webApi, "Api")));
    }

    [Fact]
    public void CoreDoesNotOwnDatabaseConfiguration()
    {
        var core = Path.Combine(FindRepositoryRoot(), "ProjectMgmt.Core");
        var project = File.ReadAllText(Path.Combine(core, "ProjectMgmt.Core.csproj"));

        Assert.False(Directory.Exists(Path.Combine(core, "Persistence")));
        Assert.DoesNotContain("EntityFrameworkCore", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Pomelo", project, StringComparison.Ordinal);
    }

    [Fact]
    public void LogicalModulesHaveTraditionalServiceAndRepositoryFolders()
    {
        var root = FindRepositoryRoot();

        foreach (var logicalModule in LogicalModules)
        {
            Assert.True(Directory.Exists(Path.Combine(root, logicalModule, "Application", "IServices")));
            Assert.True(Directory.Exists(Path.Combine(root, logicalModule, "Application", "Services")));
            Assert.True(Directory.Exists(Path.Combine(root, logicalModule, "Domain", "IRepositories")));
            Assert.True(Directory.Exists(Path.Combine(root, logicalModule, "Infrastructure", "Repositories")));
        }
    }

    [Fact]
    public void ProductionSourceUsesTraditionalClassDeclarations()
    {
        var root = FindRepositoryRoot();
        var sourceRoots = ExpectedProjects
            .Where(project => !string.Equals(project, "ProjectMgmt.Tests", StringComparison.Ordinal))
            .Select(project => Path.Combine(root, project));
        var violations = sourceRoots
            .SelectMany(project => Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return Regex.IsMatch(source, @"\bsealed\b")
                    || Regex.IsMatch(source, @"\brecord\s+(?:(?:class|struct)\s+)?[A-Za-z_]\w*")
                    || Regex.IsMatch(source, @"\bclass\s+[A-Za-z_]\w*(?:\s*<[^>{}\r\n]+>)?\s*\(");
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ApplicationLayerDoesNotDependOnInfrastructure()
    {
        var root = FindRepositoryRoot();
        var violations = ModuleProjects
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(root, project),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => path.Contains(
                $"{Path.DirectorySeparatorChar}Application{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains(".Infrastructure", StringComparison.Ordinal)
                    || source.Contains("DbContext", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DbContextsOwnAllFiftyFiveTablesWithoutOverlap()
    {
        const string connection = "Server=localhost;Port=3306;Database=projectmgmt;User=test;Password=test;";
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 16));
        using var identity = new IdentityExperienceAppDbContext(
            new DbContextOptionsBuilder<IdentityExperienceAppDbContext>().UseMySql(connection, serverVersion).Options);
        using var planning = new PlanningAppDbContext(
            new DbContextOptionsBuilder<PlanningAppDbContext>().UseMySql(connection, serverVersion).Options);
        using var delivery = new DeliveryIntelligenceAppDbContext(
            new DbContextOptionsBuilder<DeliveryIntelligenceAppDbContext>().UseMySql(connection, serverVersion).Options);

        var identityTables = GetTables(identity);
        var planningTables = GetTables(planning);
        var deliveryTables = GetTables(delivery);
        var allTables = identityTables.Concat(planningTables).Concat(deliveryTables).ToArray();

        Assert.Equal(14, identityTables.Length);
        Assert.Equal(18, planningTables.Length);
        Assert.Equal(23, deliveryTables.Length);
        Assert.Equal(55, allTables.Length);
        Assert.Equal(55, allTables.Distinct(StringComparer.Ordinal).Count());
    }

    private static string[] GetTables(DbContext context) => context.Model.GetEntityTypes()
        .Select(entity => entity.GetTableName())
        .Where(name => name is not null)
        .Cast<string>()
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string[] ReadProjectReferences(string projectPath) => XDocument.Load(projectPath)
        .Descendants("ProjectReference")
        .Select(element => element.Attribute("Include")?.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => Path.GetFileNameWithoutExtension(value!))
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectMgmt.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate ProjectMgmt.slnx.");
    }
}
