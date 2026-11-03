using System.Reflection;

namespace TomeRaider.Architecture.Tests;

internal sealed record ProjectDependency(string From, string To);
internal sealed record PackageDependency(string From, string To);

internal static class ArchitectureRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedReferences =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["TomeRaider.Domain"] = new HashSet<string>(StringComparer.Ordinal),
            ["TomeRaider.Application"] = new HashSet<string>(["TomeRaider.Domain"], StringComparer.Ordinal),
            ["TomeRaider.Infrastructure"] = new HashSet<string>(
                ["TomeRaider.Application", "TomeRaider.Domain"],
                StringComparer.Ordinal),
            ["TomeRaider.Platform.Storage"] = new HashSet<string>(["TomeRaider.Application"], StringComparer.Ordinal),
            ["TomeRaider.Presentation"] = new HashSet<string>(["TomeRaider.Application"], StringComparer.Ordinal),
            ["TomeRaider.Desktop"] = new HashSet<string>(
                [
                    "TomeRaider.Application",
                    "TomeRaider.Infrastructure",
                    "TomeRaider.Platform.Storage",
                    "TomeRaider.Presentation"
                ],
                StringComparer.Ordinal),
            ["TomeRaider.Android"] = new HashSet<string>(
                [
                    "TomeRaider.Application",
                    "TomeRaider.Infrastructure",
                    "TomeRaider.Platform.Storage",
                    "TomeRaider.Presentation"
                ],
                StringComparer.Ordinal)
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ForbiddenFrameworkReferences =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["TomeRaider.Domain"] = new HashSet<string>(
                ["Avalonia", "Microsoft.Data.Sqlite", "SQLitePCLRaw", "Android", "Xamarin"],
                StringComparer.OrdinalIgnoreCase),
            ["TomeRaider.Application"] = new HashSet<string>(
                ["Avalonia", "Microsoft.Data.Sqlite", "SQLitePCLRaw", "Android", "Xamarin"],
                StringComparer.OrdinalIgnoreCase),
            ["TomeRaider.Infrastructure"] = new HashSet<string>(
                ["Avalonia", "Android", "Xamarin"],
                StringComparer.OrdinalIgnoreCase),
            ["TomeRaider.Platform.Storage"] = new HashSet<string>(
                ["Microsoft.Data.Sqlite", "SQLitePCLRaw", "Android", "Xamarin"],
                StringComparer.OrdinalIgnoreCase),
            ["TomeRaider.Presentation"] = new HashSet<string>(
                ["Microsoft.Data.Sqlite", "SQLitePCLRaw", "Android", "Xamarin"],
                StringComparer.OrdinalIgnoreCase)
        };

    private static readonly IReadOnlySet<string> EmittedModuleNames =
        new HashSet<string>(
            AllowedReferences.Keys.Where(name => name != "TomeRaider.Android"),
            StringComparer.Ordinal);

    public static IReadOnlyList<string> FindModuleSelectionViolations(
        IReadOnlyCollection<string> selectedNames,
        IReadOnlySet<string> requiredNames)
    {
        if (selectedNames.Count == 0)
        {
            throw new InvalidOperationException("Architecture checks require a non-empty module selection.");
        }

        var selected = selectedNames.ToHashSet(StringComparer.Ordinal);
        return requiredNames
            .Except(selected)
            .Select(name => $"Missing required production module: {name}")
            .Concat(
                selected
                    .Except(AllowedReferences.Keys)
                    .Select(name => $"Unknown production module: {name}"))
            .ToArray();
    }

    public static IReadOnlyList<string> ReadProductionProjectNames(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> FindViolations(IReadOnlyCollection<ProjectDependency> dependencies)
    {
        if (dependencies.Count == 0)
        {
            throw new InvalidOperationException("Architecture checks require a non-empty production graph.");
        }

        return dependencies
            .Where(dependency =>
                !AllowedReferences.TryGetValue(dependency.From, out var allowed)
                || !allowed.Contains(dependency.To))
            .Select(dependency => $"{dependency.From} -> {dependency.To}")
            .ToArray();
    }

    public static IReadOnlyList<string> FindEmittedReferenceViolations(
        IReadOnlyCollection<(string Name, Assembly Assembly)> assemblies)
    {
        if (assemblies.Count == 0)
        {
            throw new InvalidOperationException("Emitted architecture checks require a non-empty assembly set.");
        }

        var selectionViolations = FindModuleSelectionViolations(
            assemblies.Select(assembly => assembly.Name).ToArray(),
            EmittedModuleNames);
        var productionNames = AllowedReferences.Keys.ToHashSet(StringComparer.Ordinal);
        var projectViolations = assemblies
            .SelectMany(assembly => assembly.Assembly
                .GetReferencedAssemblies()
                .Select(reference => (Assembly: assembly.Name, Reference: reference.Name))
                .Where(reference => reference.Reference is not null && productionNames.Contains(reference.Reference))
                .Where(reference => !AllowedReferences[assembly.Name].Contains(reference.Reference!))
                .Select(reference => $"{reference.Assembly} -> {reference.Reference}"))
            .ToArray();
        var mislabeledAssemblies = assemblies
            .Where(assembly => !string.Equals(
                assembly.Name,
                assembly.Assembly.GetName().Name,
                StringComparison.Ordinal))
            .Select(assembly =>
                $"{assembly.Name} -> selected assembly is {assembly.Assembly.GetName().Name ?? "unnamed"}");
        var frameworkViolations = assemblies
            .SelectMany(assembly => assembly.Assembly
                .GetReferencedAssemblies()
                .Select(reference => (Assembly: assembly.Name, Reference: reference.Name))
                .Where(reference => reference.Reference is not null)
                .Where(reference => IsForbiddenFrameworkReference(reference.Assembly, reference.Reference!))
                .Select(reference => $"{reference.Assembly} -> {reference.Reference}"));

        return selectionViolations
            .Concat(mislabeledAssemblies)
            .Concat(projectViolations)
            .Concat(frameworkViolations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> FindPackageReferenceViolations(
        IReadOnlyCollection<PackageDependency> dependencies)
    {
        if (dependencies.Count == 0)
        {
            throw new InvalidOperationException("Package architecture checks require a non-empty production graph.");
        }

        return dependencies
            .Where(dependency => IsForbiddenFrameworkReference(dependency.From, dependency.To))
            .Select(dependency => $"{dependency.From} -> {dependency.To}")
            .ToArray();
    }

    public static IReadOnlyList<ProjectDependency> ReadProjectReferences(string repositoryRoot)
    {
        var projects = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal);
        var dependencies = new List<ProjectDependency>();

        foreach (var project in projects)
        {
            var document = System.Xml.Linq.XDocument.Load(project.Value);
            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var include = (string?)reference.Attribute("Include")
                    ?? throw new InvalidOperationException($"Project reference in {project.Key} has no Include.");
                var targetPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.Value)!, include));
                var target = Path.GetFileNameWithoutExtension(targetPath);
                dependencies.Add(new ProjectDependency(project.Key, target));
            }
        }

        return dependencies;
    }

    public static IReadOnlyList<PackageDependency> ReadPackageReferences(string repositoryRoot)
    {
        var projects = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal);
        var dependencies = new List<PackageDependency>();

        foreach (var project in projects)
        {
            var document = System.Xml.Linq.XDocument.Load(project.Value);
            foreach (var reference in document.Descendants("PackageReference"))
            {
                var include = (string?)reference.Attribute("Include")
                    ?? throw new InvalidOperationException($"Package reference in {project.Key} has no Include.");
                dependencies.Add(new PackageDependency(project.Key, include));
            }
        }

        return dependencies;
    }

    public static void AssertAcyclic(IReadOnlyCollection<ProjectDependency> dependencies)
    {
        var graph = dependencies
            .GroupBy(dependency => dependency.From, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.To).ToArray(), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);

        foreach (var project in AllowedReferences.Keys)
        {
            Visit(project);
        }

        void Visit(string project)
        {
            if (active.Contains(project))
            {
                throw new InvalidOperationException($"Production dependency cycle detected at {project}.");
            }

            if (!visited.Add(project))
            {
                return;
            }

            active.Add(project);
            if (graph.TryGetValue(project, out var references))
            {
                foreach (var reference in references)
                {
                    Visit(reference);
                }
            }

            active.Remove(project);
        }
    }

    private static bool IsForbiddenFrameworkReference(string project, string reference)
    {
        return ForbiddenFrameworkReferences.TryGetValue(project, out var forbidden)
            && forbidden.Any(prefix => reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
