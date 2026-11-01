using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TomeRaider.Application;
using TomeRaider.Domain;

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
            await session.Application.SaveReadingRecordAsync(work.Id, ReadingStatus.Read, PersonalNote);
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
            foreach (var item in result.Items.Where(item => item.Work is not null))
            {
                Items.Add(item);
            }

            SelectedItem = Items.FirstOrDefault();
            StatusMessage = result.WasCancelled
                ? "No folder was selected or no saved folder is available."
                : $"Imported {Items.Count} EPUB file(s) from {result.Folder?.DisplayName}.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not import the folder: {exception.Message}";
        }
    }
}
