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
        var existing = await FindExistingAsync(connection, transaction, metadata, sourceFile, cancellationToken);
        if (existing is null)
        {
            var id = Guid.NewGuid();
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Works
                    (Id, Title, Author, Description, SourceUrl, Ao3WorkId, WordCount, IsComplete, Status, PersonalNote, FileReference)
                VALUES
                    ($id, $title, $author, $description, $sourceUrl, $ao3WorkId, $wordCount, $isComplete, $status, $personalNote, $fileReference);
                """;
            AddMetadataParameters(insert, id, metadata, sourceFile, ReadingStatus.Unknown, null);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToImportedWork(id, metadata, ReadingStatus.Unknown, null, WasCreated: true);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE Works
                SET Title = $title,
                    Author = $author,
                    Description = $description,
                    SourceUrl = $sourceUrl,
                    Ao3WorkId = $ao3WorkId,
                    WordCount = $wordCount,
                    IsComplete = $isComplete,
                    FileReference = $fileReference
                WHERE Id = $id;
                """;
            AddMetadataParameters(
                update,
                existing.Value.Id,
                metadata,
                sourceFile,
                existing.Value.Status,
                existing.Value.PersonalNote);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToImportedWork(
            existing.Value.Id,
            metadata,
            existing.Value.Status,
            existing.Value.PersonalNote,
            WasCreated: false);
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

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Works
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
                FileReference TEXT NOT NULL,
                UNIQUE (Ao3WorkId),
                UNIQUE (FileReference)
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static async Task<ExistingWork?> FindExistingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EpubMetadata metadata,
        SourceFile sourceFile,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, Status, PersonalNote
            FROM Works
            WHERE ($ao3WorkId IS NOT NULL AND Ao3WorkId = $ao3WorkId)
               OR FileReference = $fileReference
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$ao3WorkId", (object?)metadata.Ao3WorkId ?? DBNull.Value);
        command.Parameters.AddWithValue("$fileReference", sourceFile.Reference);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingWork(
                Guid.Parse(reader.GetString(0)),
                (ReadingStatus)reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2))
            : null;
    }

    private static void AddMetadataParameters(
        SqliteCommand command,
        Guid id,
        EpubMetadata metadata,
        SourceFile sourceFile,
        ReadingStatus status,
        string? personalNote)
    {
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$title", metadata.Title);
        command.Parameters.AddWithValue("$author", (object?)metadata.Author ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)metadata.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceUrl", (object?)metadata.SourceUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$ao3WorkId", (object?)metadata.Ao3WorkId ?? DBNull.Value);
        command.Parameters.AddWithValue("$wordCount", (object?)metadata.WordCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$isComplete", metadata.IsComplete is null ? DBNull.Value : metadata.IsComplete.Value ? 1 : 0);
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$personalNote", (object?)personalNote ?? DBNull.Value);
        command.Parameters.AddWithValue("$fileReference", sourceFile.Reference);
    }

    private static ImportedWork ToImportedWork(
        Guid id,
        EpubMetadata metadata,
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
            WasCreated);
    }

    private readonly record struct ExistingWork(Guid Id, ReadingStatus Status, string? PersonalNote);
}
