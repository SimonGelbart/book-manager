using TomeRaider.Application;
using TomeRaider.Domain;
using TomeRaider.Infrastructure;
using TUnit.Assertions;
using TUnit.Core;

namespace TomeRaider.Application.Tests;

public sealed class ReaderHandoffTests
{
    [Test]
    public async Task OpensTheRequestedCopyWithoutChangingTheReadingRecord()
    {
        var workspace = CreateWorkspace();
        try
        {
            const string sourceUrl = "https://archiveofourown.org/works/12345";
            SyntheticEpub.Create(workspace, "first.epub", "A Selected Work", "A. Reader", sourceUrl);
            SyntheticEpub.Create(workspace, "second.epub", "A Selected Work", "A. Reader", sourceUrl);
            var launcher = new ControlledReaderLauncher(ReaderLaunchResult.Opened("Opened the EPUB."));
            var application = CreateApplication(workspace, launcher);

            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var work = import.Items.First(item => item.Work is not null).Work!;
            await application.SaveReadingRecordAsync(work.Id, ReadingStatus.Reading, "Keep this note.");
            var details = await application.GetWorkDetailsAsync(work.Id);
            var selectedCopy = details!.Copies.Single(copy => copy.DisplayName == "second.epub");

            var result = await application.OpenCopyAsync(work.Id, selectedCopy.Id);

            await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.Opened);
            await Assert.That(result.Message).IsEqualTo("Opened the EPUB.");
            await Assert.That(launcher.RequestedFiles).Count().IsEqualTo(1);
            await Assert.That(launcher.RequestedFiles[0].Reference).IsEqualTo(selectedCopy.Reference);
            var readingRecord = await application.GetReadingRecordAsync(work.Id);
            await Assert.That(readingRecord).IsNotNull();
            await Assert.That(readingRecord!.Status).IsEqualTo(ReadingStatus.Reading);
            await Assert.That(readingRecord.PersonalNote).IsEqualTo("Keep this note.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReportsAnInaccessibleCopyAndPreservesTheReadingRecord()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "missing.epub", "An Unavailable Work", "A. Reader");
            var launcher = new ControlledReaderLauncher(
                ReaderLaunchResult.FileUnavailable("The EPUB file is no longer available."));
            var application = CreateApplication(workspace, launcher);
            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var work = import.Items.Single().Work!;
            await application.SaveReadingRecordAsync(work.Id, ReadingStatus.Read, "Already read.");
            var copy = (await application.GetWorkDetailsAsync(work.Id))!.Copies.Single();

            var result = await application.OpenCopyAsync(work.Id, copy.Id);

            await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.FileUnavailable);
            await Assert.That(result.Message).IsEqualTo("The EPUB file is no longer available.");
            var readingRecord = await application.GetReadingRecordAsync(work.Id);
            await Assert.That(readingRecord!.Status).IsEqualTo(ReadingStatus.Read);
            await Assert.That(readingRecord.PersonalNote).IsEqualTo("Already read.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReportsAReaderLaunchFailureAndPreservesTheCatalogue()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "failed.epub", "A Failed Launch Work", "A. Reader");
            var launcher = new ControlledReaderLauncher(
                ReaderLaunchResult.LaunchFailed("The associated reader could not be started."));
            var application = CreateApplication(workspace, launcher);
            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var work = import.Items.Single().Work!;
            var copy = (await application.GetWorkDetailsAsync(work.Id))!.Copies.Single();

            var result = await application.OpenCopyAsync(work.Id, copy.Id);

            await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchFailed);
            await Assert.That(result.Message).IsEqualTo("The associated reader could not be started.");
            await Assert.That(await application.GetWorkDetailsAsync(work.Id)).IsNotNull();
            await Assert.That((await application.GetWorkDetailsAsync(work.Id))!.Copies).Count().IsEqualTo(1);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    private static CatalogueApplication CreateApplication(
        string workspace,
        IReaderLauncher readerLauncher)
    {
        return new CatalogueApplication(
            new EpubMetadataExtractor(),
            new SqliteCatalogue(Path.Combine(workspace, "catalogue.sqlite")),
            readerLauncher);
    }

    private static string CreateWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "tome-raider-reader-handoff-" + Guid.NewGuid().ToString("N"));
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

    private sealed class ControlledReaderLauncher(ReaderLaunchResult result) : IReaderLauncher
    {
        public List<SourceFile> RequestedFiles { get; } = [];

        public Task<ReaderLaunchResult> LaunchAsync(
            SourceFile file,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedFiles.Add(file);
            return Task.FromResult(result);
        }
    }
}
