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
        using var workspace = TestWorkspace.Create();
        const string sourceUrl = "https://archiveofourown.org/works/12345";
        SyntheticEpub.Create(workspace.Path, "first.epub", "A Selected Work", "A. Reader", sourceUrl);
        SyntheticEpub.Create(workspace.Path, "second.epub", "A Selected Work", "A. Reader", sourceUrl);
        var launcher = new ControlledReaderLauncher(file => file.DisplayName == "second.epub"
            ? ReaderLaunchResult.LaunchRequested("Requested the second EPUB.")
            : ReaderLaunchResult.LaunchFailed("The wrong EPUB was selected."));
        var application = CreateApplication(workspace.Path, launcher);

        var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace.Path));
        var work = import.Items.First(item => item.Work is not null).Work!;
        await application.SaveReadingRecordAsync(work.Id, ReadingStatus.Reading, "Keep this note.");
        var details = await application.GetWorkDetailsAsync(work.Id);
        var selectedCopy = details!.Copies.Single(copy => copy.DisplayName == "second.epub");

        var result = await application.OpenCopyAsync(work.Id, selectedCopy.Id);

        await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchRequested);
        await Assert.That(result.Message).IsEqualTo("Requested the second EPUB.");
        var readingRecord = await application.GetReadingRecordAsync(work.Id);
        await Assert.That(readingRecord).IsNotNull();
        await Assert.That(readingRecord!.Status).IsEqualTo(ReadingStatus.Reading);
        await Assert.That(readingRecord.PersonalNote).IsEqualTo("Keep this note.");
    }

    [Test]
    public async Task ReportsAnInaccessibleCopyAndPreservesTheReadingRecord()
    {
        using var workspace = TestWorkspace.Create();
        SyntheticEpub.Create(workspace.Path, "missing.epub", "An Unavailable Work", "A. Reader");
        var launcher = new ControlledReaderLauncher(_ =>
            ReaderLaunchResult.FileUnavailable("The EPUB file is no longer available."));
        var application = CreateApplication(workspace.Path, launcher);
        var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace.Path));
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

    [Test]
    public async Task ReportsAReaderLaunchFailureAndPreservesTheCatalogue()
    {
        using var workspace = TestWorkspace.Create();
        SyntheticEpub.Create(workspace.Path, "failed.epub", "A Failed Launch Work", "A. Reader");
        var launcher = new ControlledReaderLauncher(_ =>
            ReaderLaunchResult.LaunchFailed("The associated reader could not be started."));
        var application = CreateApplication(workspace.Path, launcher);
        var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace.Path));
        var work = import.Items.Single().Work!;
        await application.SaveReadingRecordAsync(work.Id, ReadingStatus.Reading, "Keep this note after failure.");
        var copy = (await application.GetWorkDetailsAsync(work.Id))!.Copies.Single();

        var result = await application.OpenCopyAsync(work.Id, copy.Id);

        await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchFailed);
        await Assert.That(result.Message).IsEqualTo("The associated reader could not be started.");
        var details = await application.GetWorkDetailsAsync(work.Id);
        await Assert.That(details).IsNotNull();
        await Assert.That(details!.Summary.Title).IsEqualTo("A Failed Launch Work");
        await Assert.That(details!.Copies).Count().IsEqualTo(1);
        await Assert.That(details!.Copies.Single().DisplayName).IsEqualTo("failed.epub");
        await Assert.That(details!.Copies.Single().Reference)
            .IsEqualTo(System.IO.Path.Combine(workspace.Path, "failed.epub"));
        var readingRecord = await application.GetReadingRecordAsync(work.Id);
        await Assert.That(readingRecord!.Status).IsEqualTo(ReadingStatus.Reading);
        await Assert.That(readingRecord.PersonalNote).IsEqualTo("Keep this note after failure.");
    }

    [Test]
    public async Task ReportsWhenTheSelectedCopyIsNoLongerInTheCatalogue()
    {
        using var workspace = TestWorkspace.Create();
        SyntheticEpub.Create(workspace.Path, "catalogued.epub", "A Catalogued Work", "A. Reader");
        var launcher = new ControlledReaderLauncher(_ =>
            ReaderLaunchResult.LaunchRequested("Should not be requested."));
        var application = CreateApplication(workspace.Path, launcher);
        var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace.Path));
        var work = import.Items.Single().Work!;

        var result = await application.OpenCopyAsync(work.Id, Guid.NewGuid());

        await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.CopyNotFound);
        await Assert.That(result.Message).IsEqualTo("The selected EPUB copy is no longer in the catalogue.");
    }

    [Test]
    public async Task ViewModelOpensACopyFromTheSelectedWorkAndReportsTheOutcome()
    {
        using var workspace = TestWorkspace.Create();
        SyntheticEpub.Create(workspace.Path, "visible.epub", "A Visible Work", "A. Reader");
        var launcher = new ControlledReaderLauncher(file => file.DisplayName == "visible.epub"
            ? ReaderLaunchResult.LaunchRequested("Requested the visible EPUB.")
            : ReaderLaunchResult.LaunchFailed("The wrong EPUB was selected."));
        var application = CreateApplication(workspace.Path, launcher);
        var viewModel = new MainViewModel(
            new CatalogueSession(application, new PhysicalFileSource(workspace.Path)));

        await viewModel.SelectFolderAndImportAsync();
        var copy = viewModel.WorkDetails!.Copies.Single();

        await viewModel.OpenCopyAsync(copy);

        await Assert.That(viewModel.StatusMessage).IsEqualTo("Requested the visible EPUB.");
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

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestWorkspace Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "tome-raider-reader-handoff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TestWorkspace(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class ControlledReaderLauncher(
        Func<SourceFile, ReaderLaunchResult> resultFactory) : IReaderLauncher
    {
        public Task<ReaderLaunchResult> LaunchAsync(
            SourceFile file,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(resultFactory(file));
        }
    }
}
