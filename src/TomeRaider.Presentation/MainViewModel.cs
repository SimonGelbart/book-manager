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

    [ObservableProperty]
    private string statusMessage = "Choose an EPUB folder to begin.";

    [ObservableProperty]
    private string personalNote = string.Empty;

    [ObservableProperty]
    private ImportItemResult? selectedItem;

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
        if (SelectedItem?.Work is not { } work)
        {
            StatusMessage = "Import a work before saving a reading record.";
            return;
        }

        try
        {
            await session.Application.SaveAsReadAsync(work.Id, PersonalNote);
            StatusMessage = $"Saved Read and note for '{work.Title}'.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not save the reading record: {exception.Message}";
        }
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
            if (result.WasCancelled)
            {
                StatusMessage = "No folder was selected or no saved folder is available.";
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
}
