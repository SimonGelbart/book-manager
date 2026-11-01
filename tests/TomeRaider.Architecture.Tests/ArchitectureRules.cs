namespace TomeRaider.Architecture.Tests;

internal sealed record ProjectDependency(string From, string To);

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
}
