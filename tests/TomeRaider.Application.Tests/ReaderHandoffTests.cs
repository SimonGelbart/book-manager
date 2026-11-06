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
