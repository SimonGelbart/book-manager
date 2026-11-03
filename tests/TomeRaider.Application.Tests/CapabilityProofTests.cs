using TomeRaider.Application;
using TomeRaider.Domain;
using TomeRaider.Infrastructure;
using TomeRaider.Presentation;
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

    [Test]
    public async Task DescriptionLinksDoNotCreateAo3IdentityCollisions()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "generic.epub",
                "A Generic Work",
                "A. Reader",
                description: "Recommendation: https://archiveofourown.org/works/12345");
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var firstImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var generic = firstImport.Items.Single(item => item.Metadata?.Title == "A Generic Work").Work!;
            await application.SaveReadingRecordAsync(generic.Id, ReadingStatus.Reading, "Keep this note.");

            SyntheticEpub.Create(
                workspace,
                "actual.epub",
                "The Actual AO3 Work",
                "A. Writer",
                "https://archiveofourown.org/works/12345",
                packageSourceUrl: "https://archiveofourown.org/works/12345");
            var secondImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var actual = secondImport.Items.Single(item => item.Metadata?.Title == "The Actual AO3 Work");

            await Assert.That(firstImport.Items.Single().Metadata!.Ao3WorkId).IsNull();
            await Assert.That(actual.Metadata!.Ao3WorkId).IsEqualTo("12345");
            await Assert.That(actual.Work!.Id).IsNotEqualTo(generic.Id);
            var recovered = await application.GetReadingRecordAsync(generic.Id);
            await Assert.That(recovered!.PersonalNote).IsEqualTo("Keep this note.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ChapterLinksDoNotBecomeWorkIdentityOrMergeWithWorkLinks()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "chapter.epub",
                "A Chapter Download",
                "A. Writer",
                "https://archiveofourown.org/chapters/12345",
                packageSourceUrl: "https://archiveofourown.org/chapters/12345");
            SyntheticEpub.Create(
                workspace,
                "work.epub",
                "The Parent Work",
                "A. Writer",
                "https://archiveofourown.org/works/12345",
                packageSourceUrl: "https://archiveofourown.org/works/12345");

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var chapter = result.Items.Single(item => item.Metadata?.Title == "A Chapter Download");
            var work = result.Items.Single(item => item.Metadata?.Title == "The Parent Work");

            await Assert.That(chapter.Metadata!.Ao3WorkId).IsNull();
            await Assert.That(chapter.Metadata.SourceUrl).IsNull();
            await Assert.That(work.Metadata!.Ao3WorkId).IsEqualTo("12345");
            await Assert.That(chapter.Work!.Id).IsNotEqualTo(work.Work!.Id);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task GenericChapterRecommendationsDoNotCreateAo3IdentityCollisions()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "generic.epub",
                "A Generic Work",
                "A. Reader",
                chapterMarkup: "<p class=\"message\"><a href=\"https://archiveofourown.org/works/12345\">Recommendation</a></p>");
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var firstImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var generic = firstImport.Items.Single(item => item.Metadata?.Title == "A Generic Work").Work!;
            await application.SaveReadingRecordAsync(generic.Id, ReadingStatus.Reading, "Keep this note.");

            SyntheticEpub.Create(
                workspace,
                "actual.epub",
                "The Actual AO3 Work",
                "A. Writer",
                "https://archiveofourown.org/works/12345",
                packageSourceUrl: "https://archiveofourown.org/works/12345");
            var secondImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var actual = secondImport.Items.Single(item => item.Metadata?.Title == "The Actual AO3 Work");

            await Assert.That(firstImport.Items.Single().Metadata!.Ao3WorkId).IsNull();
            await Assert.That(actual.Metadata!.Ao3WorkId).IsEqualTo("12345");
            await Assert.That(actual.Work!.Id).IsNotEqualTo(generic.Id);
            var recovered = await application.GetReadingRecordAsync(generic.Id);
            await Assert.That(recovered!.PersonalNote).IsEqualTo("Keep this note.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ExtractsCompletionFromDocumentedAo3Stats()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "completed.epub",
                "A Completed AO3 Work",
                "A. Writer",
                "https://archiveofourown.org/works/12345",
                statsMarkup: "<dt>Published:</dt><dd>2026-01-01</dd><dt>Completed:</dt><dd>2026-02-01</dd><dt>Words:</dt><dd>1,234</dd><dt>Chapters:</dt><dd>10/10</dd>");

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var metadata = result.Items.Single().Metadata!;

            await Assert.That(metadata.WordCount).IsEqualTo(1234);
            await Assert.That(metadata.IsComplete).IsTrue();
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task GenericNarrativeStatsRemainUnknown()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "generic.epub",
                "A Generic Work",
                "A. Reader",
                chapterMarkup: "<p>Words: 900. Status: Complete.</p>");

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var metadata = result.Items.Single().Metadata!;

            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(metadata.WordCount).IsNull();
            await Assert.That(metadata.IsComplete).IsNull();
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ImageFirstSpineItemWithXhtmlFallbackStillImportsBaseMetadata()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.CreateImageFirstSpineWithFallback(
                workspace,
                "image-first.epub",
                "A Generic Work",
                "A. Reader");

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(result.Items).Count().IsEqualTo(1);
            await Assert.That(result.Items.Single().Metadata!.Title).IsEqualTo("A Generic Work");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReportsAllFailedImportsWithFileAndErrorDetails()
    {
        var workspace = CreateWorkspace();
        try
        {
            File.WriteAllText(Path.Combine(workspace, "broken.epub"), "not an EPUB");

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailureCount).IsEqualTo(1);
            await Assert.That(result.Items[0].File.DisplayName).IsEqualTo("broken.epub");
            await Assert.That(result.Items[0].Error).IsNotNull();
            await Assert.That(result.Items[0].DisplayText).Contains("broken.epub");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReportsSuccessfulAndFailedImportsTogether()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "valid.epub", "A Valid Work", "A. Writer");
            File.WriteAllText(Path.Combine(workspace, "broken.epub"), "not an EPUB");

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            await Assert.That(result.SuccessCount).IsEqualTo(1);
            await Assert.That(result.FailureCount).IsEqualTo(1);
            await Assert.That(result.Items).Count().IsEqualTo(2);
            await Assert.That(result.Items.Single(item => item.Work is null).DisplayText).Contains("broken.epub");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelPreservesPartialFailuresAndReportsTheirCount()
    {
        var workspace = CreateWorkspace();
        try
        {
            var validPath = SyntheticEpub.Create(workspace, "valid.epub", "A Valid Work", "A. Writer");
            var invalidPath = Path.Combine(workspace, "broken.epub");
            File.WriteAllText(invalidPath, "not an EPUB");
            var source = new FixedFolderFileSource(
                new SourceFolder(workspace, "Synthetic folder"),
                [
                    new SourceFile(validPath, "valid.epub"),
                    new SourceFile(invalidPath, "broken.epub")
                ]);
            var viewModel = new MainViewModel(
                new CatalogueSession(
                    CreateApplication(Path.Combine(workspace, "catalogue.sqlite")),
                    source));

            await viewModel.SelectFolderAndImportAsync();

            await Assert.That(viewModel.Items).Count().IsEqualTo(2);
            await Assert.That(viewModel.Items.Single(item => item.Work is null).DisplayText).Contains("broken.epub");
            await Assert.That(viewModel.StatusMessage).Contains("1 failed");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelReportsAllFailedImportsInsteadOfAnEmptyFolder()
    {
        var workspace = CreateWorkspace();
        try
        {
            var invalidPath = Path.Combine(workspace, "broken.epub");
            File.WriteAllText(invalidPath, "not an EPUB");
            var source = new FixedFolderFileSource(
                new SourceFolder(workspace, "Synthetic folder"),
                [new SourceFile(invalidPath, "broken.epub")]);
            var viewModel = new MainViewModel(
                new CatalogueSession(
                    CreateApplication(Path.Combine(workspace, "catalogue.sqlite")),
                    source));

            await viewModel.SelectFolderAndImportAsync();

            await Assert.That(viewModel.Items).Count().IsEqualTo(1);
            await Assert.That(viewModel.Items[0].DisplayText).Contains("broken.epub");
            await Assert.That(viewModel.StatusMessage).Contains("Imported 0 EPUB file(s); 1 failed");
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

    private sealed class FixedFolderFileSource(
        SourceFolder folder,
        IReadOnlyList<SourceFile> files) : IFileSource
    {
        public Task<SourceFolder?> SelectFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SourceFolder?>(folder);
        }

        public Task<SourceFolder?> RestoreFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SourceFolder?>(folder);
        }

        public async IAsyncEnumerable<SourceFile> EnumerateEpubsAsync(
            SourceFolder sourceFolder,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
                await Task.Yield();
            }
        }

        public Task<Stream> OpenReadAsync(SourceFile file, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(File.OpenRead(file.Reference));
        }
    }
}
