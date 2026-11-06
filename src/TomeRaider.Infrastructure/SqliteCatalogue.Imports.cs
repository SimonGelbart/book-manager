using Microsoft.Data.Sqlite;
using TomeRaider.Application;
using TomeRaider.Domain;

namespace TomeRaider.Infrastructure;

public sealed partial class SqliteCatalogue
{
    public async Task<CatalogueImportResult> ImportAsync(
        EpubMetadata metadata,
        SourceFile sourceFile,
        ImportAssociationDecision decision,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var existing = await FindExistingAsync(connection, transaction, sourceFile, cancellationToken);
        if (existing is not null)
        {
            return await UpdateExistingCopyAsync(
                connection,
                transaction,
                existing.Value,
                metadata,
                sourceFile,
                cancellationToken);
        }

        if (decision.NeedsReview)
        {
            return await SaveImportReviewResultAsync(
                connection,
                transaction,
                sourceFile,
                metadata,
                decision,
                cancellationToken);
        }

        return await ImportWorkAsync(
            connection,
            transaction,
            metadata,
            sourceFile,
            decision,
            cancellationToken);
    }

    private static async Task<CatalogueImportResult> UpdateExistingCopyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ExistingCopy existing,
        EpubMetadata metadata,
        SourceFile sourceFile,
        CancellationToken cancellationToken)
    {
        await using var updateVersion = connection.CreateCommand();
        updateVersion.Transaction = transaction;
        updateVersion.CommandText = """
            UPDATE DownloadedVersions
            SET Title = $title,
                Author = $author,
                Description = $description,
                SourceUrl = $sourceUrl,
                Ao3WorkId = $ao3WorkId,
                WordCount = $wordCount,
                IsComplete = $isComplete,
                AuthorNotes = $authorNotes,
                DownloadedChapterCount = $downloadedChapterCount,
                TotalChapterCount = $totalChapterCount,
                ImportedAt = $importedAt
            WHERE Id = $id;
            """;
        AddVersionParameters(updateVersion, existing.VersionId, existing.WorkId, metadata);
        await updateVersion.ExecuteNonQueryAsync(cancellationToken);

        await using var updateCopy = connection.CreateCommand();
        updateCopy.Transaction = transaction;
        updateCopy.CommandText = """
            UPDATE EpubCopies
            SET FileReference = $fileReference,
                DisplayName = $displayName
            WHERE Id = $id;
            """;
        AddCopyParameters(updateCopy, existing.CopyId, existing.VersionId, sourceFile);
        await updateCopy.ExecuteNonQueryAsync(cancellationToken);

        await ReplaceTagsAsync(connection, transaction, existing.VersionId, metadata, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CatalogueImportResult(
            ToImportedWork(
                existing.WorkId,
                metadata,
                sourceFile,
                existing.Status,
                existing.PersonalNote,
                WasCreated: false),
            null);
    }

    private static async Task<CatalogueImportResult> SaveImportReviewResultAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceFile sourceFile,
        EpubMetadata metadata,
        ImportAssociationDecision decision,
        CancellationToken cancellationToken)
    {
        var review = await SaveImportReviewAsync(
            connection,
            transaction,
            sourceFile,
            metadata,
            decision,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CatalogueImportResult(null, review);
    }

    private static async Task<CatalogueImportResult> ImportWorkAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EpubMetadata metadata,
        SourceFile sourceFile,
        ImportAssociationDecision decision,
        CancellationToken cancellationToken)
    {
        var workId = decision.AssociatedWorkId ?? Guid.NewGuid();
        var status = ReadingStatus.Unknown;
        string? personalNote = null;
        if (decision.AssociatedWorkId is null)
        {
            await using var insertWork = connection.CreateCommand();
            insertWork.Transaction = transaction;
            insertWork.CommandText = "INSERT INTO Works (Id, Status, PersonalNote) VALUES ($id, $status, $personalNote);";
            insertWork.Parameters.AddWithValue("$id", workId.ToString("D"));
            insertWork.Parameters.AddWithValue("$status", (int)ReadingStatus.Unknown);
            insertWork.Parameters.AddWithValue("$personalNote", DBNull.Value);
            await insertWork.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            (status, personalNote) = await ReadWorkRecordAsync(
                connection,
                transaction,
                decision.AssociatedWorkId.Value,
                cancellationToken);
        }

        var versionId = Guid.NewGuid();
        await InsertVersionAndCopyAsync(
            connection,
            transaction,
            versionId,
            workId,
            metadata,
            sourceFile,
            cancellationToken);
        await ReplaceTagsAsync(connection, transaction, versionId, metadata, cancellationToken);
        await DeleteImportReviewForFileAsync(connection, transaction, sourceFile.Reference, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CatalogueImportResult(
            ToImportedWork(
                workId,
                metadata,
                sourceFile,
                status,
                personalNote,
                WasCreated: decision.AssociatedWorkId is null),
            null);
    }

    private static async Task InsertVersionAndCopyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid versionId,
        Guid workId,
        EpubMetadata metadata,
        SourceFile sourceFile,
        CancellationToken cancellationToken)
    {
        await using var insertVersion = connection.CreateCommand();
        insertVersion.Transaction = transaction;
        insertVersion.CommandText = """
            INSERT INTO DownloadedVersions
                (Id, WorkId, Title, Author, Description, SourceUrl, Ao3WorkId, WordCount, IsComplete,
                 AuthorNotes, DownloadedChapterCount, TotalChapterCount, ImportedAt)
            VALUES
                ($id, $workId, $title, $author, $description, $sourceUrl, $ao3WorkId, $wordCount, $isComplete,
                 $authorNotes, $downloadedChapterCount, $totalChapterCount, $importedAt);
            """;
        AddVersionParameters(insertVersion, versionId, workId, metadata);
        await insertVersion.ExecuteNonQueryAsync(cancellationToken);

        await using var insertCopy = connection.CreateCommand();
        insertCopy.Transaction = transaction;
        insertCopy.CommandText = """
            INSERT INTO EpubCopies (Id, VersionId, FileReference, DisplayName)
            VALUES ($id, $versionId, $fileReference, $displayName);
            """;
        AddCopyParameters(insertCopy, Guid.NewGuid(), versionId, sourceFile);
        await insertCopy.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(ReadingStatus Status, string? PersonalNote)> ReadWorkRecordAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid workId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Status, PersonalNote FROM Works WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", workId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Work '{workId}' was not found.");
        }

        return (
            (ReadingStatus)reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static async Task<ExistingCopy?> FindExistingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceFile sourceFile,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT copies.Id, copies.VersionId, versions.WorkId, works.Status, works.PersonalNote
            FROM EpubCopies copies
            INNER JOIN DownloadedVersions versions ON versions.Id = copies.VersionId
            INNER JOIN Works works ON works.Id = versions.WorkId
            WHERE copies.FileReference = $fileReference
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$fileReference", sourceFile.Reference);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingCopy(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                (ReadingStatus)reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4))
            : null;
    }

    private static void AddVersionParameters(
        SqliteCommand command,
        Guid versionId,
        Guid workId,
        EpubMetadata metadata)
    {
        command.Parameters.AddWithValue("$id", versionId.ToString("D"));
        command.Parameters.AddWithValue("$workId", workId.ToString("D"));
        command.Parameters.AddWithValue("$title", metadata.Title);
        command.Parameters.AddWithValue("$author", (object?)metadata.Author ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)metadata.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceUrl", (object?)metadata.SourceUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$ao3WorkId", (object?)metadata.Ao3WorkId ?? DBNull.Value);
        command.Parameters.AddWithValue("$wordCount", (object?)metadata.WordCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$isComplete", metadata.IsComplete is null ? DBNull.Value : metadata.IsComplete.Value ? 1 : 0);
        command.Parameters.AddWithValue("$authorNotes", (object?)metadata.AuthorNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$downloadedChapterCount", (object?)metadata.DownloadedChapterCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$totalChapterCount", (object?)metadata.TotalChapterCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$importedAt", DateTime.UtcNow.Ticks);
    }

    private static void AddCopyParameters(
        SqliteCommand command,
        Guid copyId,
        Guid versionId,
        SourceFile sourceFile)
    {
        command.Parameters.AddWithValue("$id", copyId.ToString("D"));
        command.Parameters.AddWithValue("$versionId", versionId.ToString("D"));
        command.Parameters.AddWithValue("$fileReference", sourceFile.Reference);
        command.Parameters.AddWithValue("$displayName", sourceFile.DisplayName);
    }

    private static async Task ReplaceTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid versionId,
        EpubMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM EpubTags WHERE VersionId = $versionId;";
        delete.Parameters.AddWithValue("$versionId", versionId.ToString("D"));
        await delete.ExecuteNonQueryAsync(cancellationToken);

        var tags = metadata.Tags
            .Select(tag => new CategorizedTag("Subject", tag, null))
            .Concat(metadata.CategorizedTags)
            .Distinct()
            .ToArray();
        foreach (var tag in tags)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO EpubTags (VersionId, Category, Value, SourceUrl)
                VALUES ($versionId, $category, $value, $sourceUrl);
                """;
            insert.Parameters.AddWithValue("$versionId", versionId.ToString("D"));
            insert.Parameters.AddWithValue("$category", tag.Category);
            insert.Parameters.AddWithValue("$value", tag.Value);
            insert.Parameters.AddWithValue("$sourceUrl", (object?)tag.SourceUrl ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static ImportedWork ToImportedWork(
        Guid id,
        EpubMetadata metadata,
        SourceFile sourceFile,
        ReadingStatus status,
        string? personalNote,
        bool WasCreated)
    {
        return new ImportedWork(
            id,
            metadata.Title,
            metadata.Author,
            metadata.SourceUrl,
            metadata.Ao3WorkId,
            status,
            personalNote,
            WasCreated)
        {
            Description = metadata.Description,
            Tags = metadata.Tags,
            CategorizedTags = metadata.CategorizedTags,
            AuthorNotes = metadata.AuthorNotes,
            WordCount = metadata.WordCount,
            IsComplete = metadata.IsComplete,
            DownloadedChapterCount = metadata.DownloadedChapterCount,
            TotalChapterCount = metadata.TotalChapterCount,
            FileReference = sourceFile.Reference
        };
    }

    private readonly record struct ExistingCopy(
        Guid CopyId,
        Guid VersionId,
        Guid WorkId,
        ReadingStatus Status,
        string? PersonalNote);
}
