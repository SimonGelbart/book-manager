using Microsoft.Data.Sqlite;
using TomeRaider.Application;
using TomeRaider.Domain;

namespace TomeRaider.Infrastructure;

public sealed class SqliteCatalogue : IWorkCatalogue
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

    public async Task<ImportedWork> ImportAsync(
        EpubMetadata metadata,
        SourceFile sourceFile,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var existing = await FindExistingAsync(connection, transaction, sourceFile, cancellationToken);
        var workId = existing?.WorkId ?? Guid.Empty;
        var wasCreated = existing is null;
        var status = existing?.Status ?? ReadingStatus.Unknown;
        var personalNote = existing?.PersonalNote;

        if (existing is null && metadata.Ao3WorkId is not null)
        {
            var existingWork = await FindWorkByAo3IdAsync(
                connection,
                transaction,
                metadata.Ao3WorkId,
                cancellationToken);
            if (existingWork is not null)
            {
                workId = existingWork.Value.WorkId;
                status = existingWork.Value.Status;
                personalNote = existingWork.Value.PersonalNote;
                wasCreated = false;
            }
        }

        if (workId == Guid.Empty)
        {
            workId = Guid.NewGuid();
            await using var insertWork = connection.CreateCommand();
            insertWork.Transaction = transaction;
            insertWork.CommandText = "INSERT INTO Works (Id, Status, PersonalNote) VALUES ($id, $status, $personalNote);";
            insertWork.Parameters.AddWithValue("$id", workId.ToString("D"));
            insertWork.Parameters.AddWithValue("$status", (int)ReadingStatus.Unknown);
            insertWork.Parameters.AddWithValue("$personalNote", DBNull.Value);
            await insertWork.ExecuteNonQueryAsync(cancellationToken);
        }

        var copyId = existing?.CopyId ?? Guid.NewGuid();
        if (existing is null)
        {
            await using var insertCopy = connection.CreateCommand();
            insertCopy.Transaction = transaction;
            insertCopy.CommandText = """
                INSERT INTO EpubCopies
                    (Id, WorkId, FileReference, DisplayName, Title, Author, Description, SourceUrl, Ao3WorkId,
                     WordCount, IsComplete, AuthorNotes, DownloadedChapterCount, TotalChapterCount, ImportedAt)
                VALUES
                    ($id, $workId, $fileReference, $displayName, $title, $author, $description, $sourceUrl, $ao3WorkId,
                     $wordCount, $isComplete, $authorNotes, $downloadedChapterCount, $totalChapterCount, $importedAt);
                """;
            AddCopyParameters(insertCopy, copyId, workId, metadata, sourceFile);
            await insertCopy.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var updateCopy = connection.CreateCommand();
            updateCopy.Transaction = transaction;
            updateCopy.CommandText = """
                UPDATE EpubCopies
                SET WorkId = $workId,
                    FileReference = $fileReference,
                    DisplayName = $displayName,
                    Title = $title,
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
            AddCopyParameters(updateCopy, copyId, workId, metadata, sourceFile);
            await updateCopy.ExecuteNonQueryAsync(cancellationToken);
        }

        await ReplaceTagsAsync(connection, transaction, copyId, metadata, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToImportedWork(workId, metadata, sourceFile, status, personalNote, wasCreated);
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

    public async Task<IReadOnlyList<CatalogueWorkSummary>> GetWorksAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = LatestCopyQuery + """
            SELECT w.Id, latest.Title, latest.Author, latest.SourceUrl, latest.Ao3WorkId,
                   w.Status, w.PersonalNote, latest.WordCount, latest.IsComplete,
                   (SELECT COUNT(*) FROM EpubCopies copyCount WHERE copyCount.WorkId = w.Id)
            FROM Works w
            INNER JOIN LatestCopies latest ON latest.WorkId = w.Id AND latest.RowNumber = 1
            ORDER BY latest.Title COLLATE NOCASE;
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
        summaryCommand.CommandText = LatestCopyQuery + """
            SELECT w.Id, latest.Title, latest.Author, latest.SourceUrl, latest.Ao3WorkId,
                   w.Status, w.PersonalNote, latest.WordCount, latest.IsComplete,
                   (SELECT COUNT(*) FROM EpubCopies copyCount WHERE copyCount.WorkId = w.Id),
                   latest.Id
            FROM Works w
            INNER JOIN LatestCopies latest ON latest.WorkId = w.Id AND latest.RowNumber = 1
            WHERE w.Id = $id;
            """;
        summaryCommand.Parameters.AddWithValue("$id", workId.ToString("D"));

        await using var summaryReader = await summaryCommand.ExecuteReaderAsync(cancellationToken);
        if (!await summaryReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var summary = ReadSummary(summaryReader);
        var latestCopyId = Guid.Parse(summaryReader.GetString(10));
        await summaryReader.DisposeAsync();

        var (tags, categorizedTags) = await ReadTagsAsync(connection, latestCopyId, cancellationToken);
        var copies = await ReadCopiesAsync(connection, workId, cancellationToken);
        await using var metadataCommand = connection.CreateCommand();
        metadataCommand.CommandText = """
            SELECT Description, AuthorNotes, DownloadedChapterCount, TotalChapterCount
            FROM EpubCopies
            WHERE Id = $id;
            """;
        metadataCommand.Parameters.AddWithValue("$id", latestCopyId.ToString("D"));
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

    private const string LatestCopyQuery = """
        WITH LatestCopies AS
        (
            SELECT copies.*,
                   ROW_NUMBER() OVER (PARTITION BY WorkId ORDER BY ImportedAt DESC, Id DESC) AS RowNumber
            FROM EpubCopies copies
        )
        """;

    private void Initialize()
    {
        using var connection = OpenConnection();
        var legacyColumns = ReadColumns(connection, "Works");
        if (legacyColumns.Contains("FileReference"))
        {
            using var rename = connection.CreateCommand();
            rename.CommandText = "ALTER TABLE Works RENAME TO Works_Legacy;";
            rename.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS Works
            (
                Id TEXT NOT NULL PRIMARY KEY,
                Status INTEGER NOT NULL,
                PersonalNote TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS EpubCopies
            (
                Id TEXT NOT NULL PRIMARY KEY,
                WorkId TEXT NOT NULL,
                FileReference TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
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
                ImportedAt INTEGER NOT NULL,
                FOREIGN KEY (WorkId) REFERENCES Works (Id)
            );
            CREATE TABLE IF NOT EXISTS EpubTags
            (
                CopyId TEXT NOT NULL,
                Category TEXT NOT NULL,
                Value TEXT NOT NULL,
                SourceUrl TEXT NULL,
                PRIMARY KEY (CopyId, Category, Value),
                FOREIGN KEY (CopyId) REFERENCES EpubCopies (Id)
            );
            CREATE INDEX IF NOT EXISTS IX_EpubCopies_WorkId ON EpubCopies (WorkId);
            CREATE INDEX IF NOT EXISTS IX_EpubCopies_Ao3WorkId ON EpubCopies (Ao3WorkId);
            """;
        command.ExecuteNonQuery();

        if (legacyColumns.Contains("FileReference"))
        {
            using var migrate = connection.CreateCommand();
            migrate.CommandText = """
                INSERT INTO Works (Id, Status, PersonalNote)
                SELECT Id, Status, PersonalNote FROM Works_Legacy;
                INSERT INTO EpubCopies
                    (Id, WorkId, FileReference, DisplayName, Title, Author, Description, SourceUrl, Ao3WorkId,
                     WordCount, IsComplete, AuthorNotes, DownloadedChapterCount, TotalChapterCount, ImportedAt)
                SELECT Id, Id, FileReference, FileReference, Title, Author, Description, SourceUrl, Ao3WorkId,
                       WordCount, IsComplete, NULL, NULL, NULL, 0
                FROM Works_Legacy;
                DROP TABLE Works_Legacy;
                """;
            migrate.ExecuteNonQuery();
        }
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

    private static HashSet<string> ReadColumns(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
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
            SELECT copies.Id, copies.WorkId, works.Status, works.PersonalNote
            FROM EpubCopies copies
            INNER JOIN Works works ON works.Id = copies.WorkId
            WHERE copies.FileReference = $fileReference
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$fileReference", sourceFile.Reference);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingCopy(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                (ReadingStatus)reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3))
            : null;
    }

    private static async Task<ExistingWork?> FindWorkByAo3IdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string ao3WorkId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT copies.WorkId, works.Status, works.PersonalNote
            FROM EpubCopies copies
            INNER JOIN Works works ON works.Id = copies.WorkId
            WHERE copies.Ao3WorkId = $ao3WorkId
            ORDER BY copies.ImportedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$ao3WorkId", ao3WorkId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingWork(
                Guid.Parse(reader.GetString(0)),
                (ReadingStatus)reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2))
            : null;
    }

    private static void AddCopyParameters(
        SqliteCommand command,
        Guid copyId,
        Guid workId,
        EpubMetadata metadata,
        SourceFile sourceFile)
    {
        command.Parameters.AddWithValue("$id", copyId.ToString("D"));
        command.Parameters.AddWithValue("$workId", workId.ToString("D"));
        command.Parameters.AddWithValue("$fileReference", sourceFile.Reference);
        command.Parameters.AddWithValue("$displayName", sourceFile.DisplayName);
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

    private static async Task ReplaceTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid copyId,
        EpubMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM EpubTags WHERE CopyId = $copyId;";
        delete.Parameters.AddWithValue("$copyId", copyId.ToString("D"));
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
                INSERT INTO EpubTags (CopyId, Category, Value, SourceUrl)
                VALUES ($copyId, $category, $value, $sourceUrl);
                """;
            insert.Parameters.AddWithValue("$copyId", copyId.ToString("D"));
            insert.Parameters.AddWithValue("$category", tag.Category);
            insert.Parameters.AddWithValue("$value", tag.Value);
            insert.Parameters.AddWithValue("$sourceUrl", (object?)tag.SourceUrl ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<(IReadOnlyList<string> Tags, IReadOnlyList<CategorizedTag> CategorizedTags)> ReadTagsAsync(
        SqliteConnection connection,
        Guid copyId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Category, Value, SourceUrl
            FROM EpubTags
            WHERE CopyId = $copyId
            ORDER BY Category, Value;
            """;
        command.Parameters.AddWithValue("$copyId", copyId.ToString("D"));
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
            SELECT Id, FileReference, DisplayName
            FROM EpubCopies
            WHERE WorkId = $workId
            ORDER BY DisplayName COLLATE NOCASE;
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
            reader.GetInt32(9));
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
        bool wasCreated)
    {
        return new ImportedWork(
            id,
            metadata.Title,
            metadata.Author,
            metadata.SourceUrl,
            metadata.Ao3WorkId,
            status,
            personalNote,
            wasCreated)
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
        Guid WorkId,
        ReadingStatus Status,
        string? PersonalNote);

    private readonly record struct ExistingWork(
        Guid WorkId,
        ReadingStatus Status,
        string? PersonalNote);
}
