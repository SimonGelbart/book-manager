using TomeRaider.Application;
using TomeRaider.Domain;
using TomeRaider.Infrastructure;
using TomeRaider.Platform.Storage;
using TomeRaider.Presentation;
using TUnit.Assertions;
using TUnit.Core;

namespace TomeRaider.Architecture.Tests;

public sealed class ArchitectureTests
{
    [Test]
    public async Task ProductionProjectReferencesFollowTheApprovedDirection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var selectedModules = ArchitectureRules.ReadProductionProjectNames(repositoryRoot);
        var dependencies = ArchitectureRules.ReadProjectReferences(repositoryRoot);
        var selectionViolations = ArchitectureRules.FindModuleSelectionViolations(
            selectedModules,
            new HashSet<string>(
            [
                "TomeRaider.Android",
                "TomeRaider.Application",
                "TomeRaider.Desktop",
                "TomeRaider.Domain",
                "TomeRaider.Infrastructure",
                "TomeRaider.Platform.Storage",
                "TomeRaider.Presentation"
            ],
            StringComparer.Ordinal));
        var violations = ArchitectureRules.FindViolations(dependencies);

        await Assert.That(selectionViolations).Count().IsEqualTo(0);
        await Assert.That(violations).Count().IsEqualTo(0);
        ArchitectureRules.AssertAcyclic(dependencies);
    }

    [Test]
    public async Task ADeliberatelyForbiddenReferenceIsRejected()
    {
        var violations = ArchitectureRules.FindViolations(
        [
            new ProjectDependency("TomeRaider.Domain", "TomeRaider.Infrastructure")
        ]);

        await Assert.That(violations).Count().IsEqualTo(1);
        await Assert.That(violations[0]).IsEqualTo("TomeRaider.Domain -> TomeRaider.Infrastructure");
    }

    [Test]
    public async Task ProductionPackageReferencesFollowTheApprovedDirection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dependencies = ArchitectureRules.ReadPackageReferences(repositoryRoot);
        var violations = ArchitectureRules.FindPackageReferenceViolations(dependencies);

        await Assert.That(violations).Count().IsEqualTo(0);
    }

    [Test]
    public async Task AndroidActivityThemeUsesAnAppCompatBaseTheme()
    {
        var repositoryRoot = FindRepositoryRoot();
        var stylesPath = Path.Combine(
            repositoryRoot,
            "src",
            "TomeRaider.Android",
            "Resources",
            "values",
            "styles.xml");
        var document = System.Xml.Linq.XDocument.Load(stylesPath);
        var theme = document.Root?.Elements("style")
            .Single(style => style.Attribute("name")?.Value == "TomeRaiderTheme");

        await Assert.That(theme?.Attribute("parent")?.Value)
            .IsEqualTo("Theme.AppCompat.Light.NoActionBar");
    }

    [Test]
    public async Task ADeliberatelyForbiddenFrameworkReferenceIsRejected()
    {
        var violations = ArchitectureRules.FindPackageReferenceViolations(
        [
            new PackageDependency("TomeRaider.Domain", "Avalonia")
        ]);

        await Assert.That(violations).Count().IsEqualTo(1);
        await Assert.That(violations[0]).IsEqualTo("TomeRaider.Domain -> Avalonia");
    }

    [Test]
    public async Task EmptySelectionsAreRejectedInsteadOfPassingSilently()
    {
        var rejected = false;
        try
        {
            ArchitectureRules.FindViolations([]);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        await Assert.That(rejected).IsTrue();
    }

    [Test]
    public async Task ModuleSelectionChecksRejectEmptySelections()
    {
        var rejected = false;
        try
        {
            ArchitectureRules.FindModuleSelectionViolations(
                [],
                new HashSet<string>(["TomeRaider.Domain"], StringComparer.Ordinal));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        await Assert.That(rejected).IsTrue();
    }

    [Test]
    public async Task InnerAssembliesDoNotReferenceOuterImplementations()
    {
        var domainReferences = typeof(Work).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);
        var applicationReferences = typeof(CatalogueApplication).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);

        await Assert.That(domainReferences).DoesNotContain("TomeRaider.Application");
        await Assert.That(domainReferences).DoesNotContain("TomeRaider.Infrastructure");
        await Assert.That(applicationReferences).DoesNotContain("TomeRaider.Infrastructure");
        await Assert.That(applicationReferences).DoesNotContain("TomeRaider.Presentation");
    }

    [Test]
    public async Task EmittedProductionReferencesStayWithinTheApprovedDirection()
    {
        var assemblies = new[]
        {
            ("TomeRaider.Domain", typeof(Work).Assembly),
            ("TomeRaider.Application", typeof(CatalogueApplication).Assembly),
            ("TomeRaider.Infrastructure", typeof(EpubMetadataExtractor).Assembly),
            ("TomeRaider.Platform.Storage", typeof(AvaloniaStorageFileSource).Assembly),
            ("TomeRaider.Presentation", typeof(MainViewModel).Assembly),
            ("TomeRaider.Desktop", System.Reflection.Assembly.Load("TomeRaider.Desktop"))
        };
        var violations = ArchitectureRules.FindEmittedReferenceViolations(assemblies);

        await Assert.That(violations).Count().IsEqualTo(0);
    }

    [Test]
    public async Task EmittedChecksRejectASelectionThatDoesNotNameTheSelectedAssembly()
    {
        var violations = ArchitectureRules.FindEmittedReferenceViolations(
        [
            ("TomeRaider.Domain", typeof(CatalogueApplication).Assembly),
            ("TomeRaider.Application", typeof(CatalogueApplication).Assembly),
            ("TomeRaider.Infrastructure", typeof(EpubMetadataExtractor).Assembly),
            ("TomeRaider.Platform.Storage", typeof(AvaloniaStorageFileSource).Assembly),
            ("TomeRaider.Presentation", typeof(MainViewModel).Assembly),
            ("TomeRaider.Desktop", System.Reflection.Assembly.Load("TomeRaider.Desktop"))
        ]);

        await Assert.That(violations).Contains(
            "TomeRaider.Domain -> selected assembly is TomeRaider.Application");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomeRaider.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
