namespace TomeRaider.Application;

public sealed class CatalogueApplication
{
    private readonly IEpubMetadataExtractor metadataExtractor;
    private readonly IWorkCatalogue catalogue;
    private readonly SemaphoreSlim mutationGate = new(1, 1);

    public CatalogueApplication(
        IEpubMetadataExtractor metadataExtractor,
        IWorkCatalogue catalogue)
    {
        this.metadataExtractor = metadataExtractor;
        this.catalogue = catalogue;
    }

    public Task<ImportResult> ImportSelectedFolderAsync(
        IFileSource fileSource,
        CancellationToken cancellationToken = default)
    {
        return RunMutationWithGateAsync(async () =>
        {
            var folder = await fileSource.SelectFolderAsync(cancellationToken);
            return folder is null
                ? new ImportResult(null, [], WasCancelled: true)
                : await ImportFolderAsync(fileSource, folder, cancellationToken);
        }, cancellationToken);
    }

    public Task<ImportResult> ImportRememberedFolderAsync(
        IFileSource fileSource,
        CancellationToken cancellationToken = default)
    {
        return RunMutationWithGateAsync(async () =>
        {
            var folder = await fileSource.RestoreFolderAsync(cancellationToken);
            return folder is null
                ? new ImportResult(null, [], WasCancelled: true)
                : await ImportFolderAsync(fileSource, folder, cancellationToken);
        }, cancellationToken);
    }

    public Task SaveReadingRecordAsync(
        Guid workId,
        TomeRaider.Domain.ReadingStatus status,
        string? personalNote,
        CancellationToken cancellationToken = default)
    {
        return RunMutationWithGateAsync(
            () => catalogue.SaveReadingRecordAsync(workId, status, personalNote, cancellationToken),
            cancellationToken);
    }

    public Task SaveAsReadAsync(
        Guid workId,
        string? personalNote,
        CancellationToken cancellationToken = default)
    {
        return SaveReadingRecordAsync(
            workId,
            TomeRaider.Domain.ReadingStatus.Read,
            personalNote,
            cancellationToken);
    }

    public Task<ReadingRecord?> GetReadingRecordAsync(
        Guid workId,
        CancellationToken cancellationToken = default)
    {
        return catalogue.GetReadingRecordAsync(workId, cancellationToken);
    }

    public Task<IReadOnlyList<ImportReview>> GetImportReviewsAsync(
        CancellationToken cancellationToken = default)
    {
        return catalogue.GetImportReviewsAsync(cancellationToken);
    }

    public Task<ImportedWork> ResolveImportReviewAsNewWorkAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        return RunMutationWithGateAsync(
            () => catalogue.ResolveImportReviewAsNewWorkAsync(reviewId, cancellationToken),
            cancellationToken);
    }

    public Task<ImportedWork> ResolveImportReviewToExistingWorkAsync(
        Guid reviewId,
        Guid workId,
        CancellationToken cancellationToken = default)
    {
        return RunMutationWithGateAsync(
            () => catalogue.ResolveImportReviewToExistingWorkAsync(reviewId, workId, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<CatalogueWorkSummary>> GetWorksAsync(
        CancellationToken cancellationToken = default)
    {
        return catalogue.GetWorksAsync(cancellationToken);
    }

    public Task<CatalogueWorkDetails?> GetWorkDetailsAsync(
        Guid workId,
        CancellationToken cancellationToken = default)
    {
        return catalogue.GetWorkDetailsAsync(workId, cancellationToken);
    }

    public Task ClearCatalogueAsync(CancellationToken cancellationToken = default)
    {
        return RunMutationWithGateAsync(
            () => catalogue.ClearAsync(cancellationToken),
            cancellationToken);
    }

    private async Task RunMutationWithGateAsync(
        Func<Task> mutation,
        CancellationToken cancellationToken)
    {
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            await mutation();
        }
        finally
        {
            mutationGate.Release();
        }
    }

    private async Task<TResult> RunMutationWithGateAsync<TResult>(
        Func<Task<TResult>> mutation,
        CancellationToken cancellationToken)
    {
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            return await mutation();
        }
        finally
        {
            mutationGate.Release();
        }
    }

    private async Task<ImportResult> ImportFolderAsync(
        IFileSource fileSource,
        SourceFolder folder,
        CancellationToken cancellationToken)
    {
        var results = new List<ImportItemResult>();

        string? scanError = null;
        try
        {
            await foreach (var file in fileSource.EnumerateEpubsAsync(folder, cancellationToken))
            {
                try
                {
                    await using var epub = await fileSource.OpenReadAsync(file, cancellationToken);
                    var metadata = await metadataExtractor.ExtractAsync(epub, cancellationToken);
                    var candidates = await catalogue.FindAssociationCandidatesAsync(
                        metadata,
                        cancellationToken);
                    var decision = WorkAssociationPolicy.Decide(metadata, candidates);
                    var import = await catalogue.ImportAsync(metadata, file, decision, cancellationToken);
                    results.Add(new ImportItemResult(file, import.Work, metadata, null, import.Review));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    results.Add(new ImportItemResult(file, null, null, exception.Message));
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            scanError = exception.Message;
        }

        return new ImportResult(folder, results, WasCancelled: false, ScanError: scanError);
    }
}
