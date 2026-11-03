using Microsoft.Data.Sqlite;
using System.Text.Json;
using TomeRaider.Application;
using TomeRaider.Domain;

namespace TomeRaider.Infrastructure;

public sealed partial class SqliteCatalogue : IWorkCatalogue
{
    private readonly string connectionString;

    public SqliteCatalogue(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        }

        SQLitePCL.Batteries_V2.Init();
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        Initialize();
    }

    public async Task<IReadOnlyList<ImportReviewCandidate>> FindAssociationCandidatesAsync(
        EpubMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(metadata.Ao3WorkId))
        {
            return [];
        }

        await using var connection = OpenConnection();
        return await FindAssociationCandidatesAsync(connection, metadata, cancellationToken);
    }

    private static async Task<IReadOnlyList<ImportReviewCandidate>> FindAssociationCandidatesAsync(
        SqliteConnection connection,
        EpubMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(metadata.Ao3WorkId))
        {
            return [];
        }

        var sourceCandidates = await FindCandidateWorksAsync(
            connection,
            null,
            metadata.Ao3WorkId,
            cancellationToken);
        return sourceCandidates.Count > 0
            ? sourceCandidates
            : await FindTitleAuthorCandidateWorksAsync(connection, metadata, cancellationToken);
    }

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

    public async Task<IReadOnlyList<ImportReview>> GetImportReviewsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FileReference, DisplayName, MetadataJson, Reason
            FROM ImportReviews
            ORDER BY DisplayName COLLATE NOCASE;
            """;

        var reviewRows = new List<StoredReviewRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            reviewRows.Add(new StoredReviewRow(
                Guid.Parse(reader.GetString(0)),
                new SourceFile(reader.GetString(1), reader.GetString(2)),
                DeserializeMetadata(reader.GetString(3)),
                reader.GetString(4)));
        }
        await reader.DisposeAsync();

        var reviews = new List<ImportReview>(reviewRows.Count);
        foreach (var row in reviewRows)
        {
            var candidates = await FindAssociationCandidatesAsync(connection, row.Metadata, cancellationToken);
            if (candidates.Count == 0)
            {
                candidates = await ReadReviewCandidatesAsync(connection, row.Id, cancellationToken);
            }
            reviews.Add(new ImportReview(row.Id, row.File, row.Metadata, row.Reason, candidates));
        }

        return reviews;
    }

    public Task<ImportedWork> ResolveImportReviewAsNewWorkAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        return ResolveImportReviewAsync(reviewId, null, cancellationToken);
    }

    public Task<ImportedWork> ResolveImportReviewToExistingWorkAsync(
        Guid reviewId,
        Guid workId,
        CancellationToken cancellationToken = default)
    {
        return ResolveImportReviewAsync(reviewId, workId, cancellationToken);
    }

    private async Task<ImportedWork> ResolveImportReviewAsync(
        Guid reviewId,
        Guid? targetWorkId,
        CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var storedReview = await FindImportReviewAsync(connection, transaction, reviewId, cancellationToken)
            ?? throw new InvalidOperationException($"Import review '{reviewId}' was not found.");

        var workId = targetWorkId ?? Guid.NewGuid();
        var status = ReadingStatus.Unknown;
        string? personalNote = null;
        var wasCreated = targetWorkId is null;
        if (targetWorkId is null)
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
                targetWorkId.Value,
                cancellationToken);
        }

        var versionId = Guid.NewGuid();
        await InsertVersionAndCopyAsync(
            connection,
            transaction,
            versionId,
            workId,
            storedReview.Metadata,
            storedReview.File,
            cancellationToken);
        await ReplaceTagsAsync(connection, transaction, versionId, storedReview.Metadata, cancellationToken);
        await DeleteImportReviewAsync(connection, transaction, reviewId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToImportedWork(
            workId,
            storedReview.Metadata,
            storedReview.File,
            status,
            personalNote,
            wasCreated);
    }

    public async Task SaveReadingRecordAsync(
        Guid workId,
        ReadingStatus status,
        string? personalNote,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Works
            SET Status = $status,
                PersonalNote = $personalNote
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$personalNote", (object?)personalNote ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", workId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"Work '{workId}' was not found.");
        }
    }

    public async Task<ReadingRecord?> GetReadingRecordAsync(
        Guid workId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status, PersonalNote FROM Works WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", workId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ReadingRecord(
            (ReadingStatus)reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM ImportReviewCandidates;
            DELETE FROM ImportReviews;
            DELETE FROM EpubTags;
            DELETE FROM EpubCopies;
            DELETE FROM DownloadedVersions;
            DELETE FROM Works;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        SqliteSchema.Initialize(connection);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
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

    private static async Task<IReadOnlyList<ImportReviewCandidate>> FindCandidateWorksAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string ao3WorkId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH CurrentMetadata AS
            (
                SELECT versions.*,
                       ROW_NUMBER() OVER (PARTITION BY WorkId ORDER BY ImportedAt DESC, Id DESC) AS RowNumber
                FROM DownloadedVersions versions
            ), CandidateIds AS
            (
                SELECT DISTINCT WorkId
                FROM DownloadedVersions
                WHERE Ao3WorkId = $ao3WorkId
            )
            SELECT candidates.WorkId, current.Title, current.Author, $ao3WorkId,
                   works.Status, works.PersonalNote,
                   COUNT(copies.Id)
            FROM CandidateIds candidates
            INNER JOIN Works works ON works.Id = candidates.WorkId
            INNER JOIN CurrentMetadata current
                ON current.WorkId = candidates.WorkId AND current.RowNumber = 1
            LEFT JOIN DownloadedVersions versions ON versions.WorkId = candidates.WorkId
            LEFT JOIN EpubCopies copies ON copies.VersionId = versions.Id
            GROUP BY candidates.WorkId, current.Title, current.Author,
                     works.Status, works.PersonalNote
            ORDER BY current.Title COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$ao3WorkId", ao3WorkId);

        var candidates = new List<ImportReviewCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new ImportReviewCandidate(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                ReadNullableString(reader, 2),
                (ReadingStatus)reader.GetInt32(4),
                ReadNullableString(reader, 5),
                reader.GetInt32(6))
            {
                Ao3WorkId = ReadNullableString(reader, 3)
            });
        }

        return candidates;
    }

    private static async Task<IReadOnlyList<ImportReviewCandidate>> FindTitleAuthorCandidateWorksAsync(
        SqliteConnection connection,
        EpubMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH CurrentMetadata AS
            (
                SELECT versions.*,
                       ROW_NUMBER() OVER (PARTITION BY WorkId ORDER BY ImportedAt DESC, Id DESC) AS RowNumber
                FROM DownloadedVersions versions
            ), CandidateIds AS
            (
                SELECT WorkId
                FROM CurrentMetadata
                WHERE RowNumber = 1
            )
            SELECT candidates.WorkId, current.Title, current.Author, current.Ao3WorkId,
                   works.Status, works.PersonalNote,
                   COUNT(copies.Id)
            FROM CandidateIds candidates
            INNER JOIN Works works ON works.Id = candidates.WorkId
            INNER JOIN CurrentMetadata current
                ON current.WorkId = candidates.WorkId AND current.RowNumber = 1
            LEFT JOIN DownloadedVersions versions ON versions.WorkId = candidates.WorkId
            LEFT JOIN EpubCopies copies ON copies.VersionId = versions.Id
            GROUP BY candidates.WorkId, current.Title, current.Author, current.Ao3WorkId,
                     works.Status, works.PersonalNote
            ORDER BY current.Title COLLATE NOCASE;
            """;
        var candidates = new List<ImportReviewCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new ImportReviewCandidate(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                ReadNullableString(reader, 2),
                (ReadingStatus)reader.GetInt32(4),
                ReadNullableString(reader, 5),
                reader.GetInt32(6))
            {
                Ao3WorkId = ReadNullableString(reader, 3)
            });
        }

        return candidates
            .Where(candidate => WorkAssociationPolicy.HasCompatibleIdentity(metadata, candidate))
            .ToArray();
    }

    private static async Task<ImportReview> SaveImportReviewAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SourceFile sourceFile,
        EpubMetadata metadata,
        ImportAssociationDecision decision,
        CancellationToken cancellationToken)
    {
        var existingReviewId = await FindImportReviewIdAsync(
            connection,
            transaction,
            sourceFile.Reference,
            cancellationToken);
        var reviewId = existingReviewId ?? Guid.NewGuid();
        var reason = decision.ReviewReason
            ?? throw new InvalidOperationException("An import review requires a reason.");
        var metadataJson = JsonSerializer.Serialize(metadata);

        await using var save = connection.CreateCommand();
        save.Transaction = transaction;
        save.CommandText = existingReviewId is null
            ? """
              INSERT INTO ImportReviews (Id, FileReference, DisplayName, MetadataJson, Reason, CreatedAt)
              VALUES ($id, $fileReference, $displayName, $metadataJson, $reason, $createdAt);
              """
            : """
              UPDATE ImportReviews
              SET DisplayName = $displayName,
                  MetadataJson = $metadataJson,
                  Reason = $reason,
                  CreatedAt = $createdAt
              WHERE Id = $id;
              """;
        save.Parameters.AddWithValue("$id", reviewId.ToString("D"));
        save.Parameters.AddWithValue("$fileReference", sourceFile.Reference);
        save.Parameters.AddWithValue("$displayName", sourceFile.DisplayName);
        save.Parameters.AddWithValue("$metadataJson", metadataJson);
        save.Parameters.AddWithValue("$reason", reason);
        save.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.Ticks);
        await save.ExecuteNonQueryAsync(cancellationToken);

        await using var deleteCandidates = connection.CreateCommand();
        deleteCandidates.Transaction = transaction;
        deleteCandidates.CommandText = "DELETE FROM ImportReviewCandidates WHERE ReviewId = $reviewId;";
        deleteCandidates.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
        await deleteCandidates.ExecuteNonQueryAsync(cancellationToken);

        foreach (var candidate in decision.Candidates)
        {
            await using var insertCandidate = connection.CreateCommand();
            insertCandidate.Transaction = transaction;
            insertCandidate.CommandText = """
                INSERT INTO ImportReviewCandidates (ReviewId, WorkId)
                VALUES ($reviewId, $workId);
                """;
            insertCandidate.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
            insertCandidate.Parameters.AddWithValue("$workId", candidate.WorkId.ToString("D"));
            await insertCandidate.ExecuteNonQueryAsync(cancellationToken);
        }

        return new ImportReview(reviewId, sourceFile, metadata, reason, decision.Candidates);
    }

    private static async Task<Guid?> FindImportReviewIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string fileReference,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM ImportReviews WHERE FileReference = $fileReference;";
        command.Parameters.AddWithValue("$fileReference", fileReference);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string id ? Guid.Parse(id) : null;
    }

    private static async Task<ImportReview?> FindImportReviewAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT FileReference, DisplayName, MetadataJson, Reason
            FROM ImportReviews
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", reviewId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var file = new SourceFile(reader.GetString(0), reader.GetString(1));
        var metadata = DeserializeMetadata(reader.GetString(2));
        var reason = reader.GetString(3);
        await reader.DisposeAsync();
        var candidates = await ReadReviewCandidatesAsync(connection, reviewId, cancellationToken, transaction);
        return new ImportReview(reviewId, file, metadata, reason, candidates);
    }

    private static async Task<IReadOnlyList<ImportReviewCandidate>> ReadReviewCandidatesAsync(
        SqliteConnection connection,
        Guid reviewId,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH CurrentMetadata AS
            (
                SELECT versions.*,
                       ROW_NUMBER() OVER (PARTITION BY WorkId ORDER BY ImportedAt DESC, Id DESC) AS RowNumber
                FROM DownloadedVersions versions
            )
            SELECT reviewCandidates.WorkId, current.Title, current.Author, works.Status, works.PersonalNote,
                   COUNT(copies.Id)
            FROM ImportReviewCandidates reviewCandidates
            INNER JOIN Works works ON works.Id = reviewCandidates.WorkId
            INNER JOIN CurrentMetadata current
                ON current.WorkId = reviewCandidates.WorkId AND current.RowNumber = 1
            LEFT JOIN DownloadedVersions versions ON versions.WorkId = reviewCandidates.WorkId
            LEFT JOIN EpubCopies copies ON copies.VersionId = versions.Id
            WHERE reviewCandidates.ReviewId = $reviewId
            GROUP BY reviewCandidates.WorkId, current.Title, current.Author, works.Status, works.PersonalNote
            ORDER BY current.Title COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));

        var candidates = new List<ImportReviewCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new ImportReviewCandidate(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                ReadNullableString(reader, 2),
                (ReadingStatus)reader.GetInt32(3),
                ReadNullableString(reader, 4),
                reader.GetInt32(5)));
        }

        return candidates;
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

    private static async Task DeleteImportReviewAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        await using var deleteCandidates = connection.CreateCommand();
        deleteCandidates.Transaction = transaction;
        deleteCandidates.CommandText = "DELETE FROM ImportReviewCandidates WHERE ReviewId = $reviewId;";
        deleteCandidates.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
        await deleteCandidates.ExecuteNonQueryAsync(cancellationToken);

        await using var deleteReview = connection.CreateCommand();
        deleteReview.Transaction = transaction;
        deleteReview.CommandText = "DELETE FROM ImportReviews WHERE Id = $reviewId;";
        deleteReview.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
        await deleteReview.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteImportReviewForFileAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string fileReference,
        CancellationToken cancellationToken)
    {
        var reviewId = await FindImportReviewIdAsync(
            connection,
            transaction,
            fileReference,
            cancellationToken);
        if (reviewId is not null)
        {
            await DeleteImportReviewAsync(connection, transaction, reviewId.Value, cancellationToken);
        }
    }

    private static EpubMetadata DeserializeMetadata(string json)
    {
        return JsonSerializer.Deserialize<EpubMetadata>(json)
            ?? throw new InvalidOperationException("The saved import review metadata is invalid.");
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

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
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

    private readonly record struct StoredReviewRow(
        Guid Id,
        SourceFile File,
        EpubMetadata Metadata,
        string Reason);
}
