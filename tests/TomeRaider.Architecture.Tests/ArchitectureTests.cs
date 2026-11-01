using TomeRaider.Application;
using TomeRaider.Domain;
using TUnit.Assertions;
using TUnit.Core;

namespace TomeRaider.Architecture.Tests;

public sealed class ArchitectureTests
{
    [Test]
    public async Task ProductionProjectReferencesFollowTheApprovedDirection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dependencies = ArchitectureRules.ReadProjectReferences(repositoryRoot);
        var violations = ArchitectureRules.FindViolations(dependencies);

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
    public async Task InnerAssembliesDoNotReferenceOuterImplementations()
    {
        var domainReferences = typeof(Work).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);
        var applicationReferences = typeof(CatalogueApplication).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);

        await Assert.That(domainReferences).DoesNotContain("TomeRaider.Application");
        await Assert.That(domainReferences).DoesNotContain("TomeRaider.Infrastructure");
        await Assert.That(applicationReferences).DoesNotContain("TomeRaider.Infrastructure");
        await Assert.That(applicationReferences).DoesNotContain("TomeRaider.Presentation");
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
