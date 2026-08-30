using System.Reflection;
using System.Xml.Linq;
using ProjectMgmt.AiAssignment.Contracts;
using ProjectMgmt.AiAssist.Contracts;
using ProjectMgmt.AiDataOps.Contracts;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.Notification.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;
using Xunit;

namespace ProjectMgmt.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    private static readonly string[] ImplementationProjects =
    [
        "ProjectMgmt.IdentityAccess",
        "ProjectMgmt.ProjectManagement",
        "ProjectMgmt.SprintBacklog",
        "ProjectMgmt.IssueTracking",
        "ProjectMgmt.AiCore",
        "ProjectMgmt.AiAssist",
        "ProjectMgmt.AiAssignment",
        "ProjectMgmt.AiDataOps",
        "ProjectMgmt.Notification"
    ];

    [Fact]
    public void ContractsReferenceOnlyContractsOrBuildingBlocks()
    {
        var violations = FindProjects("*.Contracts.csproj")
            .SelectMany(project => ReadProjectReferences(project)
                .Where(reference => !reference.EndsWith(".Contracts", StringComparison.Ordinal)
                    && reference != "ProjectMgmt.Core")
                .Select(reference => $"{Path.GetFileNameWithoutExtension(project)} -> {reference}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ImplementationsDoNotReferenceOtherImplementations()
    {
        var root = FindRepositoryRoot();
        var violations = ImplementationProjects
            .Select(projectName => Path.Combine(root, projectName, $"{projectName}.csproj"))
            .SelectMany(project => ReadProjectReferences(project)
                .Where(reference => ImplementationProjects.Contains(reference, StringComparer.Ordinal)
                    && reference != Path.GetFileNameWithoutExtension(project))
                .Select(reference => $"{Path.GetFileNameWithoutExtension(project)} -> {reference}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void BuildingBlocksDoesNotReferenceBusinessModules()
    {
        var core = Path.Combine(FindRepositoryRoot(), "ProjectMgmt.Core", "ProjectMgmt.Core.csproj");

        Assert.Empty(ReadProjectReferences(core));
    }

    [Fact]
    public void HostReferencesAllModuleImplementations()
    {
        var host = Path.Combine(FindRepositoryRoot(), "ProjectMgmt.Solution", "ProjectMgmt.Solution.csproj");
        var references = ReadProjectReferences(host);

        foreach (var module in ImplementationProjects)
        {
            Assert.Contains(module, references);
        }
    }

    [Fact]
    public void ContractsDoNotExposeEntityFrameworkOrEntityTypes()
    {
        Assembly[] assemblies =
        [
            typeof(IUserLookupService).Assembly,
            typeof(IProjectLookupService).Assembly,
            typeof(IIssueService).Assembly,
            typeof(INotificationSender).Assembly,
            typeof(IAiBreakdownFeedbackExportService).Assembly,
            typeof(IAiAssignmentFeedbackExportService).Assembly,
            typeof(IEvaluationSetCatalog).Assembly
        ];

        var violations = assemblies
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Name.EndsWith("Entity", StringComparison.Ordinal)
                || type.Namespace?.Contains(".Entities", StringComparison.Ordinal) == true
                || InheritsFromEntityFramework(type)
                || type.GetProperties().Any(property =>
                    property.PropertyType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool InheritsFromEntityFramework(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> FindProjects(string pattern) =>
        Directory.EnumerateFiles(FindRepositoryRoot(), pattern, SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path));

    private static string[] ReadProjectReferences(string projectPath)
    {
        var project = XDocument.Load(projectPath);
        return project.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(value!))
            .ToArray();
    }

    private static bool IsGeneratedPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

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
