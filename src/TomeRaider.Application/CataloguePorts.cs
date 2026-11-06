using TomeRaider.Domain;

namespace TomeRaider.Application;

public interface IEpubMetadataExtractor
{
    Task<EpubMetadata> ExtractAsync(Stream epub, CancellationToken cancellationToken = default);
}

public interface IWorkCatalogue
{
    Task<IReadOnlyList<ImportReviewCandidate>> FindAssociationCandidatesAsync(
        EpubMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<CatalogueImportResult> ImportAsync(
        EpubMetadata metadata,
        SourceFile sourceFile,
        ImportAssociationDecision decision,
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

public interface IReaderLauncher
{
    Task<ReaderLaunchResult> LaunchAsync(
        SourceFile file,
        CancellationToken cancellationToken = default);
}
