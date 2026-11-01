using TomeRaider.Domain;

namespace TomeRaider.Application;

public sealed record SourceFolder(string Reference, string DisplayName);

public sealed record SourceFile(string Reference, string DisplayName);

public sealed record EpubMetadata(
    string Title,
    string? Author,
    string? Description,
    string? SourceUrl,
    string? Ao3WorkId,
    IReadOnlyList<string> Tags,
    int? WordCount,
    bool? IsComplete);

public sealed record ImportedWork(
    Guid Id,
    string Title,
    string? Author,
    string? SourceUrl,
    string? Ao3WorkId,
    ReadingStatus ReadingStatus,
    string? PersonalNote,
    bool WasCreated);

public sealed record ReadingRecord(ReadingStatus Status, string? PersonalNote);

public sealed record ImportItemResult(
    SourceFile File,
    ImportedWork? Work,
    EpubMetadata? Metadata,
    string? Error);

public sealed record ImportResult(
    SourceFolder? Folder,
    IReadOnlyList<ImportItemResult> Items,
    bool WasCancelled)
{
    public bool Succeeded => Items.Any(item => item.Work is not null);
}

public interface IEpubMetadataExtractor
{
    Task<EpubMetadata> ExtractAsync(Stream epub, CancellationToken cancellationToken = default);
}

public interface IWorkCatalogue
{
    Task<ImportedWork> ImportAsync(
        EpubMetadata metadata,
        SourceFile sourceFile,
        CancellationToken cancellationToken = default);

    Task SaveReadingRecordAsync(
        Guid workId,
        ReadingStatus status,
        string? personalNote,
        CancellationToken cancellationToken = default);

    Task<ReadingRecord?> GetReadingRecordAsync(
        Guid workId,
        CancellationToken cancellationToken = default);
}

public interface IFileSource
{
    Task<SourceFolder?> SelectFolderAsync(CancellationToken cancellationToken = default);

    Task<SourceFolder?> RestoreFolderAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<SourceFile> EnumerateEpubsAsync(
        SourceFolder folder,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        SourceFile file,
        CancellationToken cancellationToken = default);
}
