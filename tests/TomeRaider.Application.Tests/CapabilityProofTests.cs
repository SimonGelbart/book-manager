using TomeRaider.Application;
using TomeRaider.Domain;
using TomeRaider.Infrastructure;
using TUnit.Assertions;
using TUnit.Core;

namespace TomeRaider.Application.Tests;

public sealed class CapabilityProofTests
{
    [Test]
    public async Task ImportsGenericAndAo3EpubsAndRecoversReadingRecord()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "generic.epub", "A Generic Work", "A. Reader");
            var nestedWorkspace = Path.Combine(workspace, "nested");
            Directory.CreateDirectory(nestedWorkspace);
            SyntheticEpub.Create(
                nestedWorkspace,
                "ao3.epub",
                "An AO3 Work",
                "A. Writer",
                "https://archiveofourown.org/works/12345",
                wordCount: 1234,
                status: "Complete");
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");

            var firstApplication = CreateApplication(databasePath);
            var result = await firstApplication.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(result.Items).Count().IsEqualTo(2);

            var generic = result.Items.Single(item => item.Metadata?.Title == "A Generic Work");
            var ao3 = result.Items.Single(item => item.Metadata?.Title == "An AO3 Work");
            await Assert.That(generic.Metadata!.Ao3WorkId).IsNull();
            await Assert.That(ao3.Metadata!.Ao3WorkId).IsEqualTo("12345");
            await Assert.That(ao3.Metadata.WordCount).IsEqualTo(1234);
            await Assert.That(ao3.Metadata.IsComplete).IsTrue();
            await Assert.That(generic.Work!.Id).IsNotEqualTo(ao3.Work!.Id);

            await firstApplication.SaveReadingRecordAsync(
                generic.Work.Id,
                ReadingStatus.Reading,
                "Read the opening chapter.");

            var reopenedApplication = CreateApplication(databasePath);
            var recovered = await reopenedApplication.GetReadingRecordAsync(generic.Work.Id);
            await Assert.That(recovered).IsNotNull();
            await Assert.That(recovered!.Status).IsEqualTo(ReadingStatus.Reading);
            await Assert.That(recovered.PersonalNote).IsEqualTo("Read the opening chapter.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReportsCancelledFolderSelectionInsteadOfPassingAnEmptyImport()
    {
        var workspace = CreateWorkspace();
        try
        {
            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new CancelledFileSource());

            await Assert.That(result.WasCancelled).IsTrue();
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Folder).IsNull();
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    private static CatalogueApplication CreateApplication(string databasePath)
    {
        return new CatalogueApplication(
            new EpubMetadataExtractor(),
            new SqliteCatalogue(databasePath));
    }

    private static string CreateWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "tome-raider-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteWorkspace(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class CancelledFileSource : IFileSource
    {
        public Task<SourceFolder?> SelectFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SourceFolder?>(null);
        }

        public Task<SourceFolder?> RestoreFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SourceFolder?>(null);
        }

        public async IAsyncEnumerable<SourceFile> EnumerateEpubsAsync(
            SourceFolder folder,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<Stream> OpenReadAsync(SourceFile file, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
