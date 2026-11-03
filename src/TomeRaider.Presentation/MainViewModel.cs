using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TomeRaider.Application;

namespace TomeRaider.Presentation;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly CatalogueSession session;
    private readonly SemaphoreSlim operationGate = new(1, 1);

    public MainViewModel(CatalogueSession session)
    {
        this.session = session;
    }

    public ObservableCollection<ImportItemResult> Items { get; } = [];

    public ObservableCollection<ImportReviewViewModel> Reviews { get; } = [];

    public ObservableCollection<WorkSummaryViewModel> Works { get; } = [];

    [ObservableProperty]
    private string statusMessage = "Choose an EPUB folder to begin.";

    [ObservableProperty]
    private string personalNote = string.Empty;

    [ObservableProperty]
    private ImportItemResult? selectedItem;

    [ObservableProperty]
    private WorkSummaryViewModel? selectedWork;

    [ObservableProperty]
    private WorkDetailsViewModel? workDetails;

    [ObservableProperty]
    private bool isClearCollectionConfirmationVisible;

    partial void OnSelectedItemChanged(ImportItemResult? value)
    {
        PersonalNote = value?.Work?.PersonalNote ?? string.Empty;
    }

    public async Task SelectFolderAndImportAsync()
    {
        await ImportAsync(() => session.Application.ImportSelectedFolderAsync(session.FileSource));
    }

    public async Task ImportSavedFolderAsync()
    {
        await ImportAsync(() => session.Application.ImportRememberedFolderAsync(session.FileSource));
    }

    public Task InitializeAsync()
    {
        return RunOperationAsync(
            InitializeCoreAsync,
            exception => $"Could not load the saved collection: {exception.Message}");
    }

    private async Task InitializeCoreAsync()
    {
        await LoadWorksAsync();
        await LoadReviewsAsync();
        if (SelectedWork is not null)
        {
            await SelectWorkCoreAsync(SelectedWork.Id);
        }
    }

    public Task SaveReadingAsync()
    {
        var workId = SelectedWork?.Id ?? SelectedItem?.Work?.Id;
        var title = SelectedWork?.Title ?? SelectedItem?.Work?.Title ?? "work";
        var personalNote = PersonalNote;
        if (workId is null)
        {
            StatusMessage = "Import a work before saving a reading record.";
            return Task.CompletedTask;
        }

        return RunOperationAsync(
            () => SaveReadingCoreAsync(workId.Value, title, personalNote),
            exception => $"Could not save the reading record: {exception.Message}");
    }

    private async Task SaveReadingCoreAsync(Guid workId, string title, string personalNote)
    {
        await session.Application.SaveAsReadAsync(workId, personalNote);
        await LoadWorksAsync();
        await SelectWorkCoreAsync(workId);
        StatusMessage = $"Saved Read and note for '{title}'.";
    }

    public Task ResolveImportReviewAsNewAsync(Guid reviewId)
    {
        return ResolveImportReviewAsync(reviewId, null);
    }

    public Task ResolveImportReviewToExistingAsync(Guid reviewId, Guid workId)
    {
        return ResolveImportReviewAsync(reviewId, workId);
    }

    public void RequestClearCollection()
    {
        IsClearCollectionConfirmationVisible = true;
    }

    public void CancelClearCollection()
    {
        IsClearCollectionConfirmationVisible = false;
    }

    public Task ConfirmClearCollectionAsync()
    {
        return RunOperationAsync(
            ClearCollectionCoreAsync,
            exception => $"Could not clear the collection: {exception.Message}");
    }

    private async Task ClearCollectionCoreAsync()
    {
        await session.Application.ClearCatalogueAsync();
        Items.Clear();
        Works.Clear();
        Reviews.Clear();
        SelectedItem = null;
        SelectedWork = null;
        WorkDetails = null;
        PersonalNote = string.Empty;
        IsClearCollectionConfirmationVisible = false;
        StatusMessage = "Collection cleared.";
    }

    public Task SelectWorkAsync(Guid workId)
    {
        return RunSerializedAsync(() => SelectWorkCoreAsync(workId));
    }

    private async Task SelectWorkCoreAsync(Guid workId)
    {
        SelectedWork = Works.FirstOrDefault(work => work.Id == workId);
        var details = SelectedWork is null
            ? null
            : await session.Application.GetWorkDetailsAsync(workId);
        WorkDetails = details is null ? null : new WorkDetailsViewModel(details);
        PersonalNote = WorkDetails?.PersonalNote ?? string.Empty;
    }

    private Task ImportAsync(Func<Task<ImportResult>> import)
    {
        return RunOperationAsync(
            () => ApplyImportResultAsync(import),
            exception => $"Could not import the folder: {exception.Message}");
    }

    private async Task ApplyImportResultAsync(Func<Task<ImportResult>> import)
    {
        var result = await import();
        Items.Clear();
        foreach (var item in result.Items)
        {
            Items.Add(item);
        }

        SelectedItem = Items.FirstOrDefault(item => item.Succeeded);
        await LoadWorksAsync();
        await LoadReviewsAsync();
        if (SelectedItem?.Work is { } importedWork)
        {
            await SelectWorkCoreAsync(importedWork.Id);
        }

        StatusMessage = result switch
        {
            { WasCancelled: true } => "No folder was selected or no saved folder is available.",
            { ScanError: not null } =>
                $"Imported {result.SuccessCount} EPUB file(s); {result.FailureCount} failed; "
                + $"{result.ReviewCount} needs review, and the scan was incomplete: {result.ScanError}",
            { FailureCount: 0, ReviewCount: 0 } =>
                $"Imported {result.SuccessCount} EPUB file(s) from {result.Folder?.DisplayName}.",
            _ =>
                $"Imported {result.SuccessCount} EPUB file(s); {result.FailureCount} failed; "
                + $"{result.ReviewCount} needs review from {result.Folder?.DisplayName}."
        };
    }

    private async Task LoadWorksAsync()
    {
        var selectedId = SelectedWork?.Id;
        var works = await session.Application.GetWorksAsync();
        Works.Clear();
        foreach (var work in works)
        {
            Works.Add(new WorkSummaryViewModel(work));
        }

        if (selectedId is not null && Works.Any(work => work.Id == selectedId))
        {
            SelectedWork = Works.First(work => work.Id == selectedId);
        }
        else
        {
            SelectedWork = Works.FirstOrDefault();
        }
    }

    private async Task LoadReviewsAsync()
    {
        var reviews = await session.Application.GetImportReviewsAsync();
        Reviews.Clear();
        foreach (var review in reviews)
        {
            Reviews.Add(new ImportReviewViewModel(review));
        }
    }

    private Task ResolveImportReviewAsync(Guid reviewId, Guid? workId)
    {
        return RunOperationAsync(
            () => ResolveImportReviewCoreAsync(reviewId, workId),
            exception => $"Could not resolve the import review: {exception.Message}");
    }

    private async Task ResolveImportReviewCoreAsync(Guid reviewId, Guid? workId)
    {
        var resolved = workId is null
            ? await session.Application.ResolveImportReviewAsNewWorkAsync(reviewId)
            : await session.Application.ResolveImportReviewToExistingWorkAsync(reviewId, workId.Value);
        var item = Items.FirstOrDefault(candidate => candidate.Review?.Id == reviewId);
        if (item is not null)
        {
            Items.Remove(item);
        }

        await LoadReviewsAsync();
        await LoadWorksAsync();
        await SelectWorkCoreAsync(resolved.Id);
        StatusMessage = workId is null
            ? $"Resolved import review as new work '{resolved.Title}'."
            : $"Associated import review with '{resolved.Title}'.";
    }

    private async Task RunOperationAsync(
        Func<Task> operation,
        Func<Exception, string> errorMessage)
    {
        await RunSerializedAsync(async () =>
        {
            try
            {
                await operation();
            }
            catch (Exception exception)
            {
                StatusMessage = errorMessage(exception);
            }
        });
    }

    private async Task RunSerializedAsync(Func<Task> operation)
    {
        await operationGate.WaitAsync();
        try
        {
            await operation();
        }
        finally
        {
            operationGate.Release();
        }
    }
}
