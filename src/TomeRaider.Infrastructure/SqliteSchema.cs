using Microsoft.Data.Sqlite;

namespace TomeRaider.Infrastructure;

internal static class SqliteSchema
{
    public static void Initialize(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        var worksColumns = ReadColumns(connection, "Works", transaction);
        var legacyColumns = ReadColumns(connection, "Works_Legacy", transaction);
        if (worksColumns.Contains("FileReference") && legacyColumns.Contains("FileReference"))
        {
            throw new InvalidOperationException("The catalogue contains two incomplete legacy schemas.");
        }

        if (worksColumns.Contains("FileReference"))
        {
            using var rename = connection.CreateCommand();
            rename.Transaction = transaction;
            rename.CommandText = "ALTER TABLE Works RENAME TO Works_Legacy;";
            rename.ExecuteNonQuery();
            legacyColumns = worksColumns;
        }

        var hasLegacyCatalogue = legacyColumns.Contains("FileReference");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Works
            (
                Id TEXT NOT NULL PRIMARY KEY,
                Status INTEGER NOT NULL,
                PersonalNote TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS DownloadedVersions
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
                ImportedAt INTEGER NOT NULL,
                FOREIGN KEY (WorkId) REFERENCES Works (Id)
            );
            CREATE TABLE IF NOT EXISTS EpubCopies
            (
                Id TEXT NOT NULL PRIMARY KEY,
                VersionId TEXT NOT NULL,
                FileReference TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                FOREIGN KEY (VersionId) REFERENCES DownloadedVersions (Id)
            );
            CREATE TABLE IF NOT EXISTS EpubTags
            (
                VersionId TEXT NOT NULL,
                Category TEXT NOT NULL,
                Value TEXT NOT NULL,
                SourceUrl TEXT NULL,
                PRIMARY KEY (VersionId, Category, Value),
                FOREIGN KEY (VersionId) REFERENCES DownloadedVersions (Id)
            );
            CREATE TABLE IF NOT EXISTS ImportReviews
            (
                Id TEXT NOT NULL PRIMARY KEY,
                FileReference TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                MetadataJson TEXT NOT NULL,
                Reason TEXT NOT NULL,
                CreatedAt INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ImportReviewCandidates
            (
                ReviewId TEXT NOT NULL,
                WorkId TEXT NOT NULL,
                PRIMARY KEY (ReviewId, WorkId),
                FOREIGN KEY (ReviewId) REFERENCES ImportReviews (Id),
                FOREIGN KEY (WorkId) REFERENCES Works (Id)
            );
            CREATE INDEX IF NOT EXISTS IX_DownloadedVersions_WorkId ON DownloadedVersions (WorkId);
            CREATE INDEX IF NOT EXISTS IX_DownloadedVersions_Ao3WorkId ON DownloadedVersions (Ao3WorkId);
            CREATE INDEX IF NOT EXISTS IX_EpubCopies_VersionId ON EpubCopies (VersionId);
            CREATE INDEX IF NOT EXISTS IX_ImportReviewCandidates_WorkId ON ImportReviewCandidates (WorkId);
            """;
        command.ExecuteNonQuery();

        if (hasLegacyCatalogue)
        {
            using var migrate = connection.CreateCommand();
            migrate.Transaction = transaction;
            migrate.CommandText = """
                INSERT OR IGNORE INTO Works (Id, Status, PersonalNote)
                SELECT Id, Status, PersonalNote FROM Works_Legacy;
                INSERT OR IGNORE INTO DownloadedVersions
                    (Id, WorkId, Title, Author, Description, SourceUrl, Ao3WorkId, WordCount, IsComplete,
                     AuthorNotes, DownloadedChapterCount, TotalChapterCount, ImportedAt)
                SELECT Id, Id, Title, Author, Description, SourceUrl, Ao3WorkId, WordCount, IsComplete,
                       NULL, NULL, NULL, 0
                FROM Works_Legacy;
                INSERT OR IGNORE INTO EpubCopies (Id, VersionId, FileReference, DisplayName)
                SELECT Id, Id, FileReference, FileReference FROM Works_Legacy;
                DROP TABLE Works_Legacy;
                """;
            migrate.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static HashSet<string> ReadColumns(
        SqliteConnection connection,
        string tableName,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }
}
