using Microsoft.Data.Sqlite;
using TomeRaider.Application;
using TomeRaider.Domain;

namespace TomeRaider.Infrastructure;

public sealed partial class SqliteCatalogue
{
    public async Task<IReadOnlyList<CatalogueWorkSummary>> GetWorksAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = CurrentMetadataQuery + SummaryProjection + "\n" + SummaryFromClause + "\n" + """
            ORDER BY current.Title COLLATE NOCASE;
            """;

        var works = new List<CatalogueWorkSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            works.Add(ReadSummary(reader));
        }

        return works;
    }

    public async Task<CatalogueWorkDetails?> GetWorkDetailsAsync(
        Guid workId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var summaryCommand = connection.CreateCommand();
        summaryCommand.CommandText = CurrentMetadataQuery + SummaryProjection + ", current.Id\n" + SummaryFromClause + "\n" + """
            WHERE w.Id = $id;
            """;
        summaryCommand.Parameters.AddWithValue("$id", workId.ToString("D"));

        await using var summaryReader = await summaryCommand.ExecuteReaderAsync(cancellationToken);
        if (!await summaryReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var summary = ReadSummary(summaryReader);
        var currentVersionId = Guid.Parse(summaryReader.GetString(12));
        await summaryReader.DisposeAsync();

        var (tags, categorizedTags) = await ReadTagsAsync(connection, currentVersionId, cancellationToken);
        var copies = await ReadCopiesAsync(connection, workId, cancellationToken);
        await using var metadataCommand = connection.CreateCommand();
        metadataCommand.CommandText = """
            SELECT Description, AuthorNotes, DownloadedChapterCount, TotalChapterCount
            FROM DownloadedVersions
            WHERE Id = $id;
            """;
        metadataCommand.Parameters.AddWithValue("$id", currentVersionId.ToString("D"));
        await using var metadataReader = await metadataCommand.ExecuteReaderAsync(cancellationToken);
        await metadataReader.ReadAsync(cancellationToken);

        return new CatalogueWorkDetails(
            summary,
            ReadNullableString(metadataReader, 0),
            tags,
            categorizedTags,
            ReadNullableString(metadataReader, 1),
            ReadNullableInt(metadataReader, 2),
            ReadNullableInt(metadataReader, 3),
            copies);
    }

    private const string CurrentMetadataQuery = """
        WITH CurrentMetadata AS
        (
            SELECT versions.*,
                   ROW_NUMBER() OVER (PARTITION BY WorkId ORDER BY ImportedAt DESC, Id DESC) AS RowNumber
            FROM DownloadedVersions versions
        )
        """;

    private const string SummaryProjection = """
        SELECT w.Id, current.Title, current.Author, current.SourceUrl, current.Ao3WorkId,
               w.Status, w.PersonalNote, current.WordCount, current.IsComplete,
               current.DownloadedChapterCount, current.TotalChapterCount,
               (SELECT COUNT(*)
                FROM EpubCopies copyCount
                INNER JOIN DownloadedVersions versionCount ON versionCount.Id = copyCount.VersionId
                WHERE versionCount.WorkId = w.Id)
        """;

    private const string SummaryFromClause = """
        FROM Works w
        INNER JOIN CurrentMetadata current ON current.WorkId = w.Id AND current.RowNumber = 1
        """;

    private static async Task<(IReadOnlyList<string> Tags, IReadOnlyList<CategorizedTag> CategorizedTags)> ReadTagsAsync(
        SqliteConnection connection,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Category, Value, SourceUrl
            FROM EpubTags
            WHERE VersionId = $versionId
            ORDER BY Category, Value;
            """;
        command.Parameters.AddWithValue("$versionId", versionId.ToString("D"));
        var tags = new List<string>();
        var categorizedTags = new List<CategorizedTag>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var category = reader.GetString(0);
            var value = reader.GetString(1);
            var sourceUrl = reader.IsDBNull(2) ? null : reader.GetString(2);
            categorizedTags.Add(new CategorizedTag(category, value, sourceUrl));
            if (string.Equals(category, "Subject", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add(value);
            }
        }

        return (tags, categorizedTags);
    }

    private static async Task<IReadOnlyList<CatalogueCopy>> ReadCopiesAsync(
        SqliteConnection connection,
        Guid workId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT copies.Id, copies.FileReference, copies.DisplayName
            FROM EpubCopies copies
            INNER JOIN DownloadedVersions versions ON versions.Id = copies.VersionId
            WHERE versions.WorkId = $workId
            ORDER BY copies.DisplayName COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$workId", workId.ToString("D"));
        var copies = new List<CatalogueCopy>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            copies.Add(new CatalogueCopy(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return copies;
    }

    private static CatalogueWorkSummary ReadSummary(SqliteDataReader reader)
    {
        return new CatalogueWorkSummary(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            ReadNullableString(reader, 2),
            ReadNullableString(reader, 3),
            ReadNullableString(reader, 4),
            (ReadingStatus)reader.GetInt32(5),
            ReadNullableString(reader, 6),
            ReadNullableInt(reader, 7),
            reader.IsDBNull(8) ? null : reader.GetInt32(8) != 0,
            ReadNullableInt(reader, 9),
            ReadNullableInt(reader, 10),
            reader.GetInt32(11));
    }
}
