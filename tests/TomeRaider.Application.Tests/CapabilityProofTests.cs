using Microsoft.Data.Sqlite;
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
    public async Task ClearingCatalogueRemovesWorksAndReadingRecords()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "clear.epub", "A Clearable Work", "A. Reader");
            var application = CreateApplication(Path.Combine(workspace, "catalogue.sqlite"));
            var result = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var workId = result.Items.Single().Work!.Id;

            await application.SaveReadingRecordAsync(workId, ReadingStatus.Read, "Keep this note until clear.");
            await application.ClearCatalogueAsync();

            await Assert.That(await application.GetWorksAsync()).Count().IsEqualTo(0);
            await Assert.That(await application.GetReadingRecordAsync(workId)).IsNull();
            await Assert.That(await application.GetWorkDetailsAsync(workId)).IsNull();
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task RecoversCatalogueFromAnInterruptedLegacyMigration()
    {
        var workspace = CreateWorkspace();
        try
        {
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var workId = Guid.NewGuid().ToString("D");
            var newerWorkId = Guid.NewGuid().ToString("D");
            var newerVersionId = Guid.NewGuid().ToString("D");
            var newerCopyId = Guid.NewGuid().ToString("D");
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Works_Legacy
                    (
                        Id TEXT NOT NULL PRIMARY KEY,
                        Title TEXT NOT NULL,
                        Author TEXT NULL,
                        Description TEXT NULL,
                        SourceUrl TEXT NULL,
                        Ao3WorkId TEXT NULL,
                        WordCount INTEGER NULL,
                        IsComplete INTEGER NULL,
                        Status INTEGER NOT NULL,
                        PersonalNote TEXT NULL,
                        FileReference TEXT NOT NULL UNIQUE
                    );
                    INSERT INTO Works_Legacy
                        (Id, Title, Author, Description, SourceUrl, Ao3WorkId, WordCount, IsComplete, Status, PersonalNote, FileReference)
                    VALUES
                        ($id, 'Recovered Work', 'A. Reader', 'Recovered summary.', 'https://archiveofourown.org/works/54321', '54321', 1234, 1, 3, 'Recovered note.', '/tmp/recovered.epub');
                    CREATE TABLE Works
                    (
                        Id TEXT NOT NULL PRIMARY KEY,
                        Status INTEGER NOT NULL,
                        PersonalNote TEXT NULL
                    );
                    CREATE TABLE DownloadedVersions
                    (
                        Id TEXT NOT NULL PRIMARY KEY,
                        WorkId TEXT NOT NULL,
                        Title TEXT NOT NULL,
                        Author TEXT NULL,
                        Description TEXT NULL,
                        SourceUrl TEXT NULL,
                        Ao3WorkId TEXT NULL,
                        WordCount INTEGER NULL,
                        IsComplete INTEGER NULL,
                        AuthorNotes TEXT NULL,
                        DownloadedChapterCount INTEGER NULL,
                        TotalChapterCount INTEGER NULL,
                        ImportedAt INTEGER NOT NULL
                    );
                    CREATE TABLE EpubCopies
                    (
                        Id TEXT NOT NULL PRIMARY KEY,
                        VersionId TEXT NOT NULL,
                        FileReference TEXT NOT NULL UNIQUE,
                        DisplayName TEXT NOT NULL
                    );
                    INSERT INTO Works (Id, Status, PersonalNote)
                    VALUES ($newerWorkId, 2, 'Newer note.');
                    INSERT INTO DownloadedVersions
                        (Id, WorkId, Title, Author, Description, SourceUrl, Ao3WorkId, WordCount, IsComplete,
                         AuthorNotes, DownloadedChapterCount, TotalChapterCount, ImportedAt)
                    VALUES
                        ($newerVersionId, $newerWorkId, 'Newer Work', 'B. Reader', 'Newer summary.', NULL, NULL, 99, NULL,
                         NULL, NULL, NULL, 999);
                    INSERT INTO EpubCopies (Id, VersionId, FileReference, DisplayName)
                    VALUES ($newerCopyId, $newerVersionId, '/tmp/newer.epub', 'newer.epub');
                    """;
                command.Parameters.AddWithValue("$id", workId);
                command.Parameters.AddWithValue("$newerWorkId", newerWorkId);
                command.Parameters.AddWithValue("$newerVersionId", newerVersionId);
                command.Parameters.AddWithValue("$newerCopyId", newerCopyId);
                await command.ExecuteNonQueryAsync();
            }

            var catalogue = new SqliteCatalogue(databasePath);
            var works = await catalogue.GetWorksAsync();
            var summary = works.Single(work => work.Title == "Recovered Work");
            var details = await catalogue.GetWorkDetailsAsync(summary.Id);
            var newerSummary = works.Single(work => work.Title == "Newer Work");
            var newerDetails = await catalogue.GetWorkDetailsAsync(newerSummary.Id);

            await Assert.That(works).Count().IsEqualTo(2);
            await Assert.That(summary.Title).IsEqualTo("Recovered Work");
            await Assert.That(summary.ReadingStatus).IsEqualTo(ReadingStatus.Read);
            await Assert.That(summary.PersonalNote).IsEqualTo("Recovered note.");
            await Assert.That(details!.Description).IsEqualTo("Recovered summary.");
            await Assert.That(details.Copies).Count().IsEqualTo(1);
            await Assert.That(newerSummary.ReadingStatus).IsEqualTo(ReadingStatus.Reading);
            await Assert.That(newerSummary.PersonalNote).IsEqualTo("Newer note.");
            await Assert.That(newerDetails!.Description).IsEqualTo("Newer summary.");
            await Assert.That(newerDetails.Copies).Count().IsEqualTo(1);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ClearWaitsForAnActiveImportBeforeRemovingTheCatalogue()
    {
        var workspace = CreateWorkspace();
        try
        {
            var epubPath = SyntheticEpub.Create(workspace, "blocked.epub", "A Blocked Import", "A. Reader");
            var source = new BlockingFileSource(
                new SourceFolder(workspace, "Blocking folder"),
                new SourceFile(epubPath, "blocked.epub"));
            var application = CreateApplication(Path.Combine(workspace, "catalogue.sqlite"));

            var importTask = application.ImportSelectedFolderAsync(source);
            await source.EnumerationStarted;
            var clearTask = application.ClearCatalogueAsync();
            var clearCompletedBeforeImportReleased = clearTask.IsCompleted;

            source.ReleaseEnumeration();
            await importTask;
            await clearTask;

            await Assert.That(clearCompletedBeforeImportReleased).IsFalse();
            await Assert.That(await application.GetWorksAsync()).Count().IsEqualTo(0);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ExtractsAo3WorkFromOriginallyPostedPrefaceMessage()
    {
        await AssertAo3PrefaceMessageAsync("Originally posted on the");
    }

    [Test]
    public async Task ExtractsAo3WorkFromPostedOriginallyPrefaceMessage()
    {
        await AssertAo3PrefaceMessageAsync("Posted originally on the");
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
                statsText: "Published: 2026-01-01 Completed: 2026-02-01 Words: 1,234 Chapters: 10/10");

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
    public async Task ImportsCategorizedTagsAndMetadataFromSplitAo3Prefaces()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.CreateSplitPreface(
                workspace,
                "split-preface.epub",
                "A Split Preface Work",
                "A. Writer",
                "https://archiveofourown.org/works/98765",
                description: "A summary from the package.",
                authorNotes: "Author note from the second preface.");

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            var metadata = result.Items.Single().Metadata!;
            await Assert.That(metadata.CategorizedTags).Contains(
                new CategorizedTag("Fandoms", "Synthetic Stories", "https://archiveofourown.org/tags/Synthetic%20Stories"));
            await Assert.That(metadata.CategorizedTags).Contains(
                new CategorizedTag("Additional Tags", "Found Family", "https://archiveofourown.org/tags/Found%20Family"));
            await Assert.That(metadata.Description).IsEqualTo("A summary from the package.");
            await Assert.That(metadata.AuthorNotes).IsEqualTo("Author note from the second preface.");
            await Assert.That(metadata.WordCount).IsEqualTo(4321);
            await Assert.That(metadata.DownloadedChapterCount).IsEqualTo(10);
            await Assert.That(metadata.TotalChapterCount).IsEqualTo(12);
            await Assert.That(metadata.IsComplete).IsFalse();

            var details = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .GetWorkDetailsAsync(result.Items.Single().Work!.Id);
            var summary = (await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .GetWorksAsync()).Single();
            await Assert.That(summary.DownloadedChapterCount).IsEqualTo(10);
            await Assert.That(summary.TotalChapterCount).IsEqualTo(12);
            await Assert.That(details!.CategorizedTags).Contains(
                new CategorizedTag("Fandoms", "Synthetic Stories", "https://archiveofourown.org/tags/Synthetic%20Stories"));
            await Assert.That(details.AuthorNotes).IsEqualTo("Author note from the second preface.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ParsesOfficialAo3SummaryAndNotesSections()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.CreateSplitPreface(
                workspace,
                "official-preface.epub",
                "An Official Template Work",
                "A. Writer",
                "https://archiveofourown.org/works/24680",
                description: "Summary from the official template shape.",
                authorNotes: "Notes from the official template shape.",
                useOfficialAo3Structure: true);

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var metadata = result.Items.Single().Metadata!;

            await Assert.That(metadata.Description).IsEqualTo("Summary from the official template shape.");
            await Assert.That(metadata.AuthorNotes).IsEqualTo("Notes from the official template shape.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReopensCollectionDetailsAndRetainsPersonalInformationOnUnchangedRescan()
    {
        var workspace = CreateWorkspace();
        try
        {
            var epubPath = SyntheticEpub.Create(
                workspace,
                "generic.epub",
                "A Durable Generic Work",
                "A. Reader",
                description: "A durable summary.");
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var firstApplication = CreateApplication(databasePath);
            var firstImport = await firstApplication.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var originalWork = firstImport.Items.Single().Work!;

            await firstApplication.SaveReadingRecordAsync(
                originalWork.Id,
                ReadingStatus.Reading,
                "Keep this personal note.");

            var reopenedApplication = CreateApplication(databasePath);
            var works = await reopenedApplication.GetWorksAsync();
            var summary = works.Single();
            var details = await reopenedApplication.GetWorkDetailsAsync(summary.Id);

            await Assert.That(summary.Id).IsEqualTo(originalWork.Id);
            await Assert.That(summary.Title).IsEqualTo("A Durable Generic Work");
            await Assert.That(summary.ReadingStatus).IsEqualTo(ReadingStatus.Reading);
            await Assert.That(summary.PersonalNote).IsEqualTo("Keep this personal note.");
            await Assert.That(summary.WordCount).IsNull();
            await Assert.That(summary.CopyCount).IsEqualTo(1);
            await Assert.That(details).IsNotNull();
            await Assert.That(details!.Description).IsEqualTo("A durable summary.");
            await Assert.That(details.Copies).Count().IsEqualTo(1);
            await Assert.That(details.Copies[0].Reference).IsEqualTo(epubPath);

            var secondImport = await reopenedApplication.ImportRememberedFolderAsync(new PhysicalFileSource(workspace));
            var rescannedWork = secondImport.Items.Single().Work!;
            await Assert.That(rescannedWork.Id).IsEqualTo(originalWork.Id);
            await Assert.That(rescannedWork.WasCreated).IsFalse();

            var afterRescan = (await reopenedApplication.GetWorksAsync()).Single();
            await Assert.That(afterRescan.PersonalNote).IsEqualTo("Keep this personal note.");
            await Assert.That(afterRescan.ReadingStatus).IsEqualTo(ReadingStatus.Reading);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task AssociatesCompatibleAo3CopiesWithExistingWork()
    {
        var workspace = CreateWorkspace();
        try
        {
            const string sourceUrl = "https://archiveofourown.org/works/24680";
            SyntheticEpub.Create(workspace, "first.epub", "The Same Work", "A. Writer", sourceUrl);
            SyntheticEpub.Create(workspace, "second.epub", "The Same Work", "A. Writer", sourceUrl);

            var application = CreateApplication(Path.Combine(workspace, "catalogue.sqlite"));
            var result = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var importedWorks = result.Items.Select(item => item.Work!).ToArray();
            var works = await application.GetWorksAsync();
            var details = await application.GetWorkDetailsAsync(importedWorks[0].Id);

            await Assert.That(importedWorks).Count().IsEqualTo(2);
            await Assert.That(importedWorks[0].Id).IsEqualTo(importedWorks[1].Id);
            await Assert.That(importedWorks.Count(work => work.WasCreated)).IsEqualTo(1);
            await Assert.That(works).Count().IsEqualTo(1);
            await Assert.That(details!.Copies).Count().IsEqualTo(2);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task KeepsConflictingSourceIdentityInDurableImportReview()
    {
        var workspace = CreateWorkspace();
        try
        {
            const string sourceUrl = "https://archiveofourown.org/works/24680";
            SyntheticEpub.Create(workspace, "original.epub", "The Same Work", "A. Writer", sourceUrl);
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var firstImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var original = firstImport.Items.Single(item => item.File.DisplayName == "original.epub").Work!;
            await application.SaveReadingRecordAsync(original.Id, ReadingStatus.Read, "Keep this record.");

            SyntheticEpub.Create(
                workspace,
                "conflict.epub",
                "A Different Work",
                "A. Writer",
                sourceUrl,
                description: "Conflicting summary.");
            var result = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var reviewItem = result.Items.Single(item => item.File.DisplayName == "conflict.epub");
            var reopenedApplication = CreateApplication(databasePath);
            var reviews = await reopenedApplication.GetImportReviewsAsync();
            var works = await reopenedApplication.GetWorksAsync();

            await Assert.That(reviewItem.NeedsReview).IsTrue();
            await Assert.That(reviewItem.Review!.File.Reference).IsEqualTo(Path.Combine(workspace, "conflict.epub"));
            await Assert.That(reviewItem.Review.Metadata.Title).IsEqualTo("A Different Work");
            await Assert.That(reviews[0].Metadata.Description).IsEqualTo("Conflicting summary.");
            await Assert.That(reviews).Count().IsEqualTo(1);
            await Assert.That(reviews[0].Candidates).Count().IsEqualTo(1);
            await Assert.That(reviews[0].Candidates[0].WorkId).IsEqualTo(original.Id);
            await Assert.That(reviews[0].Candidates[0].ReadingStatus).IsEqualTo(ReadingStatus.Read);
            await Assert.That(reviews[0].Candidates[0].PersonalNote).IsEqualTo("Keep this record.");
            await Assert.That(works).Count().IsEqualTo(1);
            await Assert.That(works[0].CopyCount).IsEqualTo(1);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task OrdinaryEpubWithoutSourceIdentityDoesNotNeedReview()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "ordinary.epub", "An Ordinary EPUB", "A. Reader");

            var application = CreateApplication(Path.Combine(workspace, "catalogue.sqlite"));
            var result = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            await Assert.That(result.Items.Single().Succeeded).IsTrue();
            await Assert.That(result.ReviewCount).IsEqualTo(0);
            await Assert.That(await application.GetImportReviewsAsync()).Count().IsEqualTo(0);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task SourceEnrichedImportCanReviewAgainstAnExistingOrdinaryWork()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "ordinary.epub", "A Shared Title", "A. Reader");
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var firstImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var ordinary = firstImport.Items.Single(item => item.File.DisplayName == "ordinary.epub").Work!;

            SyntheticEpub.Create(
                workspace,
                "source-enriched.epub",
                "A Shared Title",
                "A. Reader",
                "https://archiveofourown.org/works/24680");
            var result = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var review = result.Items.Single(item => item.File.DisplayName == "source-enriched.epub").Review!;
            var works = await application.GetWorksAsync();

            await Assert.That(review.Reason).IsEqualTo(
                "Available metadata suggests an existing work, but source identity is not established.");
            await Assert.That(review.Candidates).Count().IsEqualTo(1);
            await Assert.That(review.Candidates[0].WorkId).IsEqualTo(ordinary.Id);
            await Assert.That(works).Count().IsEqualTo(1);
            await Assert.That(works[0].CopyCount).IsEqualTo(1);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ResolvesImportReviewToExistingWorkWithoutOverwritingPersonalInformation()
    {
        var workspace = CreateWorkspace();
        try
        {
            const string sourceUrl = "https://archiveofourown.org/works/24680";
            SyntheticEpub.Create(workspace, "original.epub", "The Same Work", "A. Writer", sourceUrl);
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var firstImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var original = firstImport.Items.Single(item => item.File.DisplayName == "original.epub").Work!;
            await application.SaveReadingRecordAsync(original.Id, ReadingStatus.Read, "Keep this record.");

            SyntheticEpub.Create(
                workspace,
                "conflict.epub",
                "A Different Work",
                "A. Writer",
                sourceUrl,
                description: "Conflicting summary.");
            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var review = import.Items.Single(item => item.File.DisplayName == "conflict.epub").Review!;
            var resolved = await application.ResolveImportReviewToExistingWorkAsync(review.Id, original.Id);
            var recovered = await application.GetReadingRecordAsync(original.Id);
            var details = await application.GetWorkDetailsAsync(original.Id);

            await Assert.That(resolved.Id).IsEqualTo(original.Id);
            await Assert.That(resolved.WasCreated).IsFalse();
            await Assert.That(recovered!.Status).IsEqualTo(ReadingStatus.Read);
            await Assert.That(recovered.PersonalNote).IsEqualTo("Keep this record.");
            await Assert.That(await application.GetImportReviewsAsync()).Count().IsEqualTo(0);
            await Assert.That(details!.Copies).Count().IsEqualTo(2);
            await Assert.That(details.Copies.Any(copy => copy.Reference.EndsWith("conflict.epub"))).IsTrue();
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ResolvesImportReviewAsNewWorkWithoutChangingExistingWork()
    {
        var workspace = CreateWorkspace();
        try
        {
            const string sourceUrl = "https://archiveofourown.org/works/24680";
            SyntheticEpub.Create(workspace, "original.epub", "The Same Work", "A. Writer", sourceUrl);
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var firstImport = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var original = firstImport.Items.Single(item => item.File.DisplayName == "original.epub").Work!;
            await application.SaveReadingRecordAsync(original.Id, ReadingStatus.Read, "Keep this record.");

            SyntheticEpub.Create(workspace, "conflict.epub", "A Different Work", "A. Writer", sourceUrl);
            var import = await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var review = import.Items.Single(item => item.File.DisplayName == "conflict.epub").Review!;
            var resolved = await application.ResolveImportReviewAsNewWorkAsync(review.Id);
            var works = await application.GetWorksAsync();
            var recovered = await application.GetReadingRecordAsync(original.Id);

            await Assert.That(resolved.Id).IsNotEqualTo(original.Id);
            await Assert.That(resolved.WasCreated).IsTrue();
            await Assert.That(works).Count().IsEqualTo(2);
            await Assert.That(works.Single(work => work.Id == original.Id).PersonalNote)
                .IsEqualTo("Keep this record.");
            await Assert.That(recovered!.Status).IsEqualTo(ReadingStatus.Read);
            await Assert.That(await application.GetImportReviewsAsync()).Count().IsEqualTo(0);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ReportsAnIncompleteScanWithoutDiscardingTheDurableCatalogue()
    {
        var workspace = CreateWorkspace();
        try
        {
            var filePath = SyntheticEpub.Create(workspace, "valid.epub", "A Retained Work", "A. Writer");
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            await application.ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            var result = await application.ImportSelectedFolderAsync(
                new FailingAfterFirstFileSource(
                    new SourceFolder(workspace, "Synthetic folder"),
                    new SourceFile(filePath, "valid.epub")));

            await Assert.That(result.SuccessCount).IsEqualTo(1);
            await Assert.That(result.ScanError).IsEqualTo("The synthetic source became unavailable.");
            await Assert.That((await application.GetWorksAsync()).Single().Title).IsEqualTo("A Retained Work");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelDisplaysTheCollectionAndSelectedWorkDetails()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "view.epub",
                "A Browseable Work",
                "A. Writer",
                description: "A browseable summary.");
            var viewModel = new MainViewModel(
                new CatalogueSession(
                    CreateApplication(Path.Combine(workspace, "catalogue.sqlite")),
                    new PhysicalFileSource(workspace)));

            await viewModel.SelectFolderAndImportAsync();
            var work = viewModel.Works.Single();
            await viewModel.SelectWorkAsync(work.Id);

            await Assert.That(viewModel.Works).Count().IsEqualTo(1);
            await Assert.That(viewModel.SelectedWork).IsEqualTo(work);
            await Assert.That(viewModel.WorkDetails).IsNotNull();
            await Assert.That(viewModel.WorkDetails!.Description).IsEqualTo("A browseable summary.");
            await Assert.That(viewModel.WorkDetails.Copies).Count().IsEqualTo(1);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelShowsAndResolvesImportReview()
    {
        var workspace = CreateWorkspace();
        try
        {
            const string sourceUrl = "https://archiveofourown.org/works/24680";
            SyntheticEpub.Create(workspace, "original.epub", "The Same Work", "A. Writer", sourceUrl);
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var viewModel = new MainViewModel(
                new CatalogueSession(application, new PhysicalFileSource(workspace)));

            await viewModel.SelectFolderAndImportAsync();
            var original = viewModel.Works.Single();
            await application.SaveReadingRecordAsync(original.Id, ReadingStatus.Read, "Keep this record.");
            SyntheticEpub.Create(
                workspace,
                "conflict.epub",
                "A Different Work",
                "A. Writer",
                sourceUrl,
                description: "Conflicting summary.");

            await viewModel.SelectFolderAndImportAsync();
            var review = viewModel.Reviews.Single();

            await Assert.That(review.FileName).IsEqualTo("conflict.epub");
            await Assert.That(review.Location).IsEqualTo(Path.Combine(workspace, "conflict.epub"));
            await Assert.That(review.Title).IsEqualTo("A Different Work");
            await Assert.That(review.Description).IsEqualTo("Conflicting summary.");
            await Assert.That(review.Candidates).Count().IsEqualTo(1);

            await viewModel.ResolveImportReviewToExistingAsync(review.Id, review.Candidates[0].WorkId);

            await Assert.That(viewModel.Reviews).Count().IsEqualTo(0);
            await Assert.That(viewModel.Works).Count().IsEqualTo(1);
            await Assert.That(viewModel.WorkDetails!.Copies).Count().IsEqualTo(2);
            await Assert.That(viewModel.StatusMessage).Contains("Associated import review");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelInitializesFromTheSavedCollection()
    {
        var workspace = CreateWorkspace();
        try
        {
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            SyntheticEpub.Create(workspace, "saved.epub", "A Saved Work", "A. Reader");
            await CreateApplication(databasePath)
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));

            var viewModel = new MainViewModel(
                new CatalogueSession(
                    CreateApplication(databasePath),
                    new PhysicalFileSource(workspace)));

            await viewModel.InitializeAsync();

            await Assert.That(viewModel.Works).Count().IsEqualTo(1);
            await Assert.That(viewModel.Works.Single().Title).IsEqualTo("A Saved Work");
            await Assert.That(viewModel.WorkDetails).IsNotNull();
            await Assert.That(viewModel.WorkDetails!.Title).IsEqualTo("A Saved Work");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelConfirmsAndClearsTheCollection()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(workspace, "clear-view.epub", "A Clearable View", "A. Reader");
            var viewModel = new MainViewModel(
                new CatalogueSession(
                    CreateApplication(Path.Combine(workspace, "catalogue.sqlite")),
                    new PhysicalFileSource(workspace)));

            await viewModel.SelectFolderAndImportAsync();
            await viewModel.SelectWorkAsync(viewModel.Works.Single().Id);
            viewModel.RequestClearCollection();

            await Assert.That(viewModel.IsClearCollectionConfirmationVisible).IsTrue();
            await viewModel.ConfirmClearCollectionAsync();

            await Assert.That(viewModel.Works).Count().IsEqualTo(0);
            await Assert.That(viewModel.Items).Count().IsEqualTo(0);
            await Assert.That(viewModel.SelectedWork).IsNull();
            await Assert.That(viewModel.WorkDetails).IsNull();
            await Assert.That(viewModel.IsClearCollectionConfirmationVisible).IsFalse();
            await Assert.That(viewModel.StatusMessage).IsEqualTo("Collection cleared.");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task QueuedSaveUsesTheWorkAndNoteSelectedBeforeAnImport()
    {
        var workspace = CreateWorkspace();
        try
        {
            var firstPath = SyntheticEpub.Create(workspace, "first.epub", "First Work", "A. Reader");
            var secondPath = SyntheticEpub.Create(workspace, "second.epub", "Second Work", "B. Reader");
            var databasePath = Path.Combine(workspace, "catalogue.sqlite");
            var application = CreateApplication(databasePath);
            var firstImport = await application.ImportSelectedFolderAsync(
                new FixedFolderFileSource(
                    new SourceFolder(workspace, "First folder"),
                    [new SourceFile(firstPath, "first.epub")]));
            var firstWorkId = firstImport.Items.Single().Work!.Id;
            var blockingSource = new BlockingFileSource(
                new SourceFolder(workspace, "Second folder"),
                new SourceFile(secondPath, "second.epub"));
            var viewModel = new MainViewModel(new CatalogueSession(application, blockingSource));

            await viewModel.InitializeAsync();
            await viewModel.SelectWorkAsync(firstWorkId);
            viewModel.PersonalNote = "Edited before queued save.";

            var importTask = viewModel.SelectFolderAndImportAsync();
            await blockingSource.EnumerationStarted;
            var saveTask = viewModel.SaveReadingAsync();
            var saveCompletedBeforeImportReleased = saveTask.IsCompleted;

            blockingSource.ReleaseEnumeration();
            await importTask;
            await saveTask;

            var firstRecord = await application.GetReadingRecordAsync(firstWorkId);
            var secondWorkId = viewModel.Works.Single(work => work.Title == "Second Work").Id;
            var secondRecord = await application.GetReadingRecordAsync(secondWorkId);

            await Assert.That(saveCompletedBeforeImportReleased).IsFalse();
            await Assert.That(firstRecord!.Status).IsEqualTo(ReadingStatus.Read);
            await Assert.That(firstRecord.PersonalNote).IsEqualTo("Edited before queued save.");
            await Assert.That(secondRecord!.Status).IsEqualTo(ReadingStatus.Unknown);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    [Test]
    public async Task ViewModelFormatsHtmlDescriptionsAndGroupsCategorizedTags()
    {
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "html-description.epub",
                "An HTML Description",
                "A. Reader",
                description: "&lt;p&gt;First paragraph.&lt;/p&gt;&lt;p&gt;Second paragraph.&lt;/p&gt;");
            SyntheticEpub.CreateSplitPreface(
                workspace,
                "formatted.epub",
                "A Formatted Work",
                "A. Writer",
                "https://archiveofourown.org/works/13579",
                description: "<p>First paragraph.</p><p>Second paragraph.</p>",
                authorNotes: "<p>Keep these notes readable.</p>");
            var viewModel = new MainViewModel(
                new CatalogueSession(
                    CreateApplication(Path.Combine(workspace, "catalogue.sqlite")),
                    new PhysicalFileSource(workspace)));

            await viewModel.SelectFolderAndImportAsync();
            var htmlWork = viewModel.Works.Single(work => work.Title == "An HTML Description");
            await viewModel.SelectWorkAsync(htmlWork.Id);

            await Assert.That(viewModel.WorkDetails!.Description)
                .IsEqualTo("First paragraph.\nSecond paragraph.");

            var taggedWork = viewModel.Works.Single(work => work.Title == "A Formatted Work");
            await viewModel.SelectWorkAsync(taggedWork.Id);
            await Assert.That(viewModel.WorkDetails.AuthorNotes)
                .IsEqualTo("Keep these notes readable.");
            var additionalTags = viewModel.WorkDetails.TagGroups
                .Single(group => group.Category == "Additional Tags");
            await Assert.That(additionalTags.Header).IsEqualTo("Additional Tags (1)");
            await Assert.That(additionalTags.Values).Contains("Found Family");
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

    private static async Task AssertAo3PrefaceMessageAsync(string messageIntro)
    {
        const string ao3Url = "https://archiveofourown.org/works/12345";
        var workspace = CreateWorkspace();
        try
        {
            SyntheticEpub.Create(
                workspace,
                "ao3.epub",
                "An AO3 Work",
                "A. Writer",
                ao3Url,
                ao3MessageIntro: messageIntro);

            var result = await CreateApplication(Path.Combine(workspace, "catalogue.sqlite"))
                .ImportSelectedFolderAsync(new PhysicalFileSource(workspace));
            var metadata = result.Items.Single().Metadata!;

            await Assert.That(metadata.Ao3WorkId).IsEqualTo("12345");
            await Assert.That(metadata.SourceUrl).IsEqualTo(ao3Url);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
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

    private sealed class BlockingFileSource(
        SourceFolder folder,
        SourceFile file) : IFileSource
    {
        private readonly TaskCompletionSource<bool> enumerationStartedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EnumerationStarted => enumerationStartedSource.Task;

        public void ReleaseEnumeration()
        {
            releaseSource.TrySetResult(true);
        }

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
            enumerationStartedSource.TrySetResult(true);
            await releaseSource.Task.WaitAsync(cancellationToken);
            yield return file;
        }

        public Task<Stream> OpenReadAsync(SourceFile sourceFile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(File.OpenRead(sourceFile.Reference));
        }
    }

    private sealed class FailingAfterFirstFileSource(
        SourceFolder folder,
        SourceFile firstFile) : IFileSource
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
            cancellationToken.ThrowIfCancellationRequested();
            yield return firstFile;
            await Task.Yield();
            throw new IOException("The synthetic source became unavailable.");
        }

        public Task<Stream> OpenReadAsync(SourceFile file, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(File.OpenRead(file.Reference));
        }
    }
}
