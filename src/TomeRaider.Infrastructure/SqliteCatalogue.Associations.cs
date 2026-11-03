using Microsoft.Data.Sqlite;
using TomeRaider.Application;
using TomeRaider.Domain;

namespace TomeRaider.Infrastructure;

public sealed partial class SqliteCatalogue
{
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
}
