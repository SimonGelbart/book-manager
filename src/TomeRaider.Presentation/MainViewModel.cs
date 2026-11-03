using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TomeRaider.Application;

namespace TomeRaider.Presentation;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly CatalogueSession session;

    public MainViewModel(CatalogueSession session)
    {
        this.session = session;
    }

    public ObservableCollection<ImportItemResult> Items { get; } = [];

    public ObservableCollection<CatalogueWorkSummary> Works { get; } = [];

    [ObservableProperty]
    private string statusMessage = "Choose an EPUB folder to begin.";

    [ObservableProperty]
    private string personalNote = string.Empty;

    [ObservableProperty]
    private ImportItemResult? selectedItem;

    [ObservableProperty]
    private CatalogueWorkSummary? selectedWork;

    [ObservableProperty]
    private CatalogueWorkDetails? workDetails;

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

    public async Task SaveReadingAsync()
    {
        var workId = SelectedWork?.Id ?? SelectedItem?.Work?.Id;
        if (workId is null)
        {
            StatusMessage = "Import a work before saving a reading record.";
            return;
        }

        try
        {
            var title = SelectedWork?.Title ?? SelectedItem?.Work?.Title ?? "work";
            await session.Application.SaveAsReadAsync(workId.Value, PersonalNote);
            await LoadWorksAsync();
            await SelectWorkAsync(workId.Value);
            StatusMessage = $"Saved Read and note for '{title}'.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not save the reading record: {exception.Message}";
        }
    }

    public async Task SelectWorkAsync(Guid workId)
    {
        SelectedWork = Works.FirstOrDefault(work => work.Id == workId);
        WorkDetails = SelectedWork is null
            ? null
            : await session.Application.GetWorkDetailsAsync(workId);
        PersonalNote = WorkDetails?.Summary.PersonalNote ?? string.Empty;
    }

    private async Task ImportAsync(Func<Task<ImportResult>> import)
    {
        try
        {
            var result = await import();
            Items.Clear();
            foreach (var item in result.Items)
            {
                Items.Add(item);
            }

            SelectedItem = Items.FirstOrDefault(item => item.Succeeded);
            await LoadWorksAsync();
            if (SelectedItem?.Work is { } importedWork)
            {
                await SelectWorkAsync(importedWork.Id);
            }
            if (result.WasCancelled)
            {
                StatusMessage = "No folder was selected or no saved folder is available.";
            }
            else if (result.ScanError is not null)
            {
                StatusMessage = result.FailureCount == 0
                    ? $"Imported {result.SuccessCount} EPUB file(s), but the scan was incomplete: {result.ScanError}"
                    : $"Imported {result.SuccessCount} EPUB file(s); {result.FailureCount} failed, and the scan was incomplete: {result.ScanError}";
            }
            else if (result.FailureCount == 0)
            {
                StatusMessage = $"Imported {result.SuccessCount} EPUB file(s) from {result.Folder?.DisplayName}.";
            }
            else
            {
                StatusMessage = $"Imported {result.SuccessCount} EPUB file(s); {result.FailureCount} failed from {result.Folder?.DisplayName}.";
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not import the folder: {exception.Message}";
        }
    }

    private async Task LoadWorksAsync()
    {
        var selectedId = SelectedWork?.Id;
        var works = await session.Application.GetWorksAsync();
        Works.Clear();
        foreach (var work in works)
        {
            Works.Add(work);
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
}
