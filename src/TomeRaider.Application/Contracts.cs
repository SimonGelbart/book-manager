using TomeRaider.Domain;

namespace TomeRaider.Application;

public sealed record SourceFolder(string Reference, string DisplayName);

public sealed record SourceFile(string Reference, string DisplayName);

public sealed record CategorizedTag(string Category, string Value, string? SourceUrl);

public sealed record EpubMetadata(
    string Title,
    string? Author,
    string? Description,
    string? SourceUrl,
    string? Ao3WorkId,
    IReadOnlyList<string> Tags,
    int? WordCount,
    bool? IsComplete)
{
    public IReadOnlyList<CategorizedTag> CategorizedTags { get; init; } = [];

    public string? AuthorNotes { get; init; }

    public int? DownloadedChapterCount { get; init; }

    public int? TotalChapterCount { get; init; }
}

public sealed record ImportedWork(
    Guid Id,
    string Title,
    string? Author,
    string? SourceUrl,
    string? Ao3WorkId,
    ReadingStatus ReadingStatus,
    string? PersonalNote,
    bool WasCreated)
{
    public string? Description { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<CategorizedTag> CategorizedTags { get; init; } = [];

    public string? AuthorNotes { get; init; }

    public int? WordCount { get; init; }

    public bool? IsComplete { get; init; }

    public int? DownloadedChapterCount { get; init; }

    public int? TotalChapterCount { get; init; }

    public string? FileReference { get; init; }
}

public sealed record CatalogueImportResult(
    ImportedWork? Work,
    ImportReview? Review);

public sealed record ImportReviewCandidate(
    Guid WorkId,
    string Title,
    string? Author,
    ReadingStatus ReadingStatus,
    string? PersonalNote,
    int CopyCount);

public sealed record ImportReview(
    Guid Id,
    SourceFile File,
    EpubMetadata Metadata,
    string Reason,
    IReadOnlyList<ImportReviewCandidate> Candidates);

public sealed record CatalogueWorkSummary(
    Guid Id,
    string Title,
    string? Author,
    string? SourceUrl,
    string? Ao3WorkId,
    ReadingStatus ReadingStatus,
    string? PersonalNote,
    int? WordCount,
    bool? IsComplete,
    int? DownloadedChapterCount,
    int? TotalChapterCount,
    int CopyCount)
{
    public string ReadingStatusText => ReadingStatus.ToString();
}

public sealed record CatalogueCopy(
    Guid Id,
    string Reference,
    string DisplayName);

public sealed record CatalogueWorkDetails(
    CatalogueWorkSummary Summary,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<CategorizedTag> CategorizedTags,
    string? AuthorNotes,
    int? DownloadedChapterCount,
    int? TotalChapterCount,
    IReadOnlyList<CatalogueCopy> Copies);

public sealed record ReadingRecord(ReadingStatus Status, string? PersonalNote);

public sealed record ImportItemResult(
    SourceFile File,
    ImportedWork? Work,
    EpubMetadata? Metadata,
    string? Error,
    ImportReview? Review = null)
{
    public bool Succeeded => Work is not null;

    public bool NeedsReview => Review is not null;

    public string DisplayText => Succeeded
        ? $"{File.DisplayName}: {Work!.Title}"
        : NeedsReview
            ? $"{File.DisplayName}: Needs review"
        : $"{File.DisplayName}: {Error ?? "Import failed."}";
}

public sealed record ImportResult(
    SourceFolder? Folder,
    IReadOnlyList<ImportItemResult> Items,
    bool WasCancelled,
    string? ScanError = null)
{
    public int SuccessCount => Items.Count(item => item.Succeeded);

    public int FailureCount => Items.Count - SuccessCount;

    public int ReviewCount => Items.Count(item => item.NeedsReview);

    public bool Succeeded => SuccessCount > 0;
}

public interface IEpubMetadataExtractor
{
    Task<EpubMetadata> ExtractAsync(Stream epub, CancellationToken cancellationToken = default);
}

public interface IWorkCatalogue
{
    Task<CatalogueImportResult> ImportAsync(
        EpubMetadata metadata,
        SourceFile sourceFile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportReview>> GetImportReviewsAsync(
        CancellationToken cancellationToken = default);

    Task<ImportedWork> ResolveImportReviewAsNewWorkAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default);

    Task<ImportedWork> ResolveImportReviewToExistingWorkAsync(
        Guid reviewId,
        Guid workId,
        CancellationToken cancellationToken = default);

    Task SaveReadingRecordAsync(
        Guid workId,
        ReadingStatus status,
        string? personalNote,
        CancellationToken cancellationToken = default);

    Task<ReadingRecord?> GetReadingRecordAsync(
        Guid workId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogueWorkSummary>> GetWorksAsync(
        CancellationToken cancellationToken = default);

    Task<CatalogueWorkDetails?> GetWorkDetailsAsync(
        Guid workId,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
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
