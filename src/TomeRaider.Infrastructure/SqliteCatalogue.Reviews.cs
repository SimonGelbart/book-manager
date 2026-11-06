using Microsoft.Data.Sqlite;
using System.Text.Json;
using TomeRaider.Application;
using TomeRaider.Domain;

namespace TomeRaider.Infrastructure;

public sealed partial class SqliteCatalogue
{
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

    private readonly record struct StoredReviewRow(
        Guid Id,
        SourceFile File,
        EpubMetadata Metadata,
        string Reason);
}
