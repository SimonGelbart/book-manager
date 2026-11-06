using TomeRaider.Application;
using TomeRaider.Domain;
using TomeRaider.Infrastructure;
using TomeRaider.Presentation;
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
            var launcher = new ControlledReaderLauncher(file => file.DisplayName == "second.epub"
                ? ReaderLaunchResult.LaunchRequested("Requested the second EPUB.")
                : ReaderLaunchResult.LaunchFailed("The wrong EPUB was selected."));
            var application = CreateApplication(workspace, launcher);

            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var work = import.Items.First(item => item.Work is not null).Work!;
            await application.SaveReadingRecordAsync(work.Id, ReadingStatus.Reading, "Keep this note.");
            var details = await application.GetWorkDetailsAsync(work.Id);
            var selectedCopy = details!.Copies.Single(copy => copy.DisplayName == "second.epub");

            var result = await application.OpenCopyAsync(work.Id, selectedCopy.Id);

            await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchRequested);
            await Assert.That(result.Message).IsEqualTo("Requested the second EPUB.");
            var requestedFile = launcher.RequestedFiles.Single();
            await Assert.That(requestedFile.DisplayName).IsEqualTo("second.epub");
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
            var launcher = new ControlledReaderLauncher(_ =>
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
            var launcher = new ControlledReaderLauncher(_ =>
                ReaderLaunchResult.LaunchFailed("The associated reader could not be started."));
            var application = CreateApplication(workspace, launcher);
            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var work = import.Items.Single().Work!;
            var copy = (await application.GetWorkDetailsAsync(work.Id))!.Copies.Single();

            var result = await application.OpenCopyAsync(work.Id, copy.Id);

            await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchFailed);
            await Assert.That(result.Message).IsEqualTo("The associated reader could not be started.");
            var details = await application.GetWorkDetailsAsync(work.Id);
            await Assert.That(details).IsNotNull();
            await Assert.That(details!.Copies).Count().IsEqualTo(1);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReportsWhenTheSelectedCopyIsNoLongerInTheCatalogue()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "catalogued.epub", "A Catalogued Work", "A. Reader");
            var launcher = new ControlledReaderLauncher(_ =>
                ReaderLaunchResult.LaunchRequested("Should not be requested."));
            var application = CreateApplication(workspace, launcher);
            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var work = import.Items.Single().Work!;

            var result = await application.OpenCopyAsync(work.Id, Guid.NewGuid());

            await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.CopyNotFound);
            await Assert.That(result.Message).IsEqualTo("The selected EPUB copy is no longer in the catalogue.");
            await Assert.That(launcher.RequestedFiles).IsEmpty();
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelOpensACopyFromTheSelectedWorkAndReportsTheOutcome()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "visible.epub", "A Visible Work", "A. Reader");
            var launcher = new ControlledReaderLauncher(file => file.DisplayName == "visible.epub"
                ? ReaderLaunchResult.LaunchRequested("Requested the visible EPUB.")
                : ReaderLaunchResult.LaunchFailed("The wrong EPUB was selected."));
            var application = CreateApplication(workspace, launcher);
            var viewModel = new MainViewModel(
                new CatalogueSession(application, new PhysicalFileSource(workspace)));

            await viewModel.SelectFolderAndImportAsync();
            var copy = viewModel.WorkDetails!.Copies.Single();

            await viewModel.OpenCopyAsync(copy);

            await Assert.That(viewModel.StatusMessage).IsEqualTo("Requested the visible EPUB.");
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

    private sealed class ControlledReaderLauncher(
        Func<SourceFile, ReaderLaunchResult> resultFactory) : IReaderLauncher
    {
        public List<SourceFile> RequestedFiles { get; } = [];

        public Task<ReaderLaunchResult> LaunchAsync(
            SourceFile file,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedFiles.Add(file);
            return Task.FromResult(resultFactory(file));
        }
    }
}
