namespace TomeRaider.Application;

public sealed class CatalogueApplication
{
    private readonly IEpubMetadataExtractor metadataExtractor;
    private readonly IWorkCatalogue catalogue;

    public CatalogueApplication(
        IEpubMetadataExtractor metadataExtractor,
        IWorkCatalogue catalogue)
    {
        this.metadataExtractor = metadataExtractor;
        this.catalogue = catalogue;
    }

    public async Task<ImportResult> ImportSelectedFolderAsync(
        IFileSource fileSource,
        CancellationToken cancellationToken = default)
    {
        var folder = await fileSource.SelectFolderAsync(cancellationToken);
        return folder is null
            ? new ImportResult(null, [], WasCancelled: true)
            : await ImportFolderAsync(fileSource, folder, cancellationToken);
    }

    public async Task<ImportResult> ImportRememberedFolderAsync(
        IFileSource fileSource,
        CancellationToken cancellationToken = default)
    {
        var folder = await fileSource.RestoreFolderAsync(cancellationToken);
        return folder is null
            ? new ImportResult(null, [], WasCancelled: true)
            : await ImportFolderAsync(fileSource, folder, cancellationToken);
    }

    public Task SaveReadingRecordAsync(
        Guid workId,
        TomeRaider.Domain.ReadingStatus status,
        string? personalNote,
        CancellationToken cancellationToken = default)
    {
        return catalogue.SaveReadingRecordAsync(workId, status, personalNote, cancellationToken);
    }

    public Task<ReadingRecord?> GetReadingRecordAsync(
        Guid workId,
        CancellationToken cancellationToken = default)
    {
        return catalogue.GetReadingRecordAsync(workId, cancellationToken);
    }

    private async Task<ImportResult> ImportFolderAsync(
        IFileSource fileSource,
        SourceFolder folder,
        CancellationToken cancellationToken)
    {
        var results = new List<ImportItemResult>();

        await foreach (var file in fileSource.EnumerateEpubsAsync(folder, cancellationToken))
        {
            try
            {
                await using var epub = await fileSource.OpenReadAsync(file, cancellationToken);
                var metadata = await metadataExtractor.ExtractAsync(epub, cancellationToken);
                var work = await catalogue.ImportAsync(metadata, file, cancellationToken);
                results.Add(new ImportItemResult(file, work, metadata, null));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new ImportItemResult(file, null, null, exception.Message));
            }
        }

        return new ImportResult(folder, results, WasCancelled: false);
    }
}
