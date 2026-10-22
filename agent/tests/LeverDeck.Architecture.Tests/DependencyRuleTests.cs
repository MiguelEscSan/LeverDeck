using System.Xml.Linq;

namespace LeverDeck.Architecture.Tests;

/// <summary>
/// Enforces the dependency rule from docs/ARCHITECTURE.md by reading the project files:
/// App → Infrastructure → Application → Domain, and Infrastructure → Protocol → Domain.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["LeverDeck.Domain"] = [],
        ["LeverDeck.Application"] = ["LeverDeck.Domain"],
        ["LeverDeck.Protocol"] = ["LeverDeck.Domain"],
        ["LeverDeck.Infrastructure"] = ["LeverDeck.Application", "LeverDeck.Protocol"],
        ["LeverDeck.App"] = ["LeverDeck.Infrastructure"],
    };

    // The core must stay free of frameworks, OS and network packages.
    private static readonly string[] PackageFreeProjects = ["LeverDeck.Domain", "LeverDeck.Application"];

    public static TheoryData<string> Projects => [.. AllowedReferences.Keys];

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_ReferencesOnlyAllowedProjects(string project)
    {
        var references = ProjectReferences(project);

        var forbidden = references.Except(AllowedReferences[project]).ToArray();

        Assert.True(forbidden.Length == 0, $"{project} must not reference: {string.Join(", ", forbidden)}");
    }

    [Theory]
    [MemberData(nameof(PackageFreeProjectsData))]
    public void CoreProject_HasNoPackageReferences(string project)
    {
        var packages = Items(project, "PackageReference").ToArray();

        Assert.True(packages.Length == 0, $"{project} must not reference packages: {string.Join(", ", packages)}");
    }

    [Theory]
    [MemberData(nameof(PackageFreeProjectsData))]
    public void CoreProject_UsesPlainSdk(string project)
    {
        var sdk = LoadProject(project).Root?.Attribute("Sdk")?.Value;

        Assert.Equal("Microsoft.NET.Sdk", sdk);
    }

    public static TheoryData<string> PackageFreeProjectsData => [.. PackageFreeProjects];

    private static IEnumerable<string> ProjectReferences(string project) =>
        Items(project, "ProjectReference").Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')));

    private static IEnumerable<string> Items(string project, string itemType) =>
        LoadProject(project)
            .Descendants(itemType)
            .Select(item => item.Attribute("Include")?.Value)
            .OfType<string>();

    private static XDocument LoadProject(string project) =>
        XDocument.Load(Path.Combine(AgentRoot.Value, "src", project, $"{project}.csproj"));

    private static readonly Lazy<string> AgentRoot = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LeverDeck.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("LeverDeck.slnx not found above the test output.");
    });
}
