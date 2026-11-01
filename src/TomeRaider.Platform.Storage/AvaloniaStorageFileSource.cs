using System.Runtime.CompilerServices;
using Avalonia.Platform.Storage;
using TomeRaider.Application;

namespace TomeRaider.Platform.Storage;

public sealed class AvaloniaStorageFileSource : IFileSource
{
    private readonly IStorageProvider storageProvider;
    private readonly JsonFolderBookmarkStore bookmarkStore;
    private readonly Dictionary<string, IStorageFile> files = new(StringComparer.Ordinal);
    private IStorageFolder? currentFolder;

    public AvaloniaStorageFileSource(
        IStorageProvider storageProvider,
        JsonFolderBookmarkStore bookmarkStore)
    {
        this.storageProvider = storageProvider;
        this.bookmarkStore = bookmarkStore;
    }

    public async Task<SourceFolder?> SelectFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose the EPUB collection folder"
        });
        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return null;
        }

        if (!folder.CanBookmark)
        {
            throw new InvalidOperationException("The selected folder did not provide a persistent access bookmark.");
        }

        var bookmark = await folder.SaveBookmarkAsync();
        if (string.IsNullOrWhiteSpace(bookmark))
        {
            throw new InvalidOperationException("The selected folder could not be saved for later access.");
        }

        await bookmarkStore.SaveAsync(bookmark, folder.Name, cancellationToken);
        currentFolder = folder;
        return ToSourceFolder(folder);
    }

    public async Task<SourceFolder?> RestoreFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stored = await bookmarkStore.LoadAsync(cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var folder = await storageProvider.OpenFolderBookmarkAsync(stored.Value.Bookmark);
        if (folder is null)
        {
            return null;
        }

        currentFolder = folder;
        return ToSourceFolder(folder, stored.Value.DisplayName);
    }

    public async IAsyncEnumerable<SourceFile> EnumerateEpubsAsync(
        SourceFolder folder,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var storageFolder = await ResolveFolderAsync(folder, cancellationToken);
        files.Clear();
        await foreach (var file in EnumerateFolderAsync(storageFolder, cancellationToken))
        {
            yield return file;
        }
    }

    public async Task<Stream> OpenReadAsync(
        SourceFile file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storageFile = files.GetValueOrDefault(file.Reference);
        if (storageFile is null)
        {
            storageFile = await storageProvider.TryGetFileFromPathAsync(new Uri(file.Reference));
        }

        if (storageFile is null)
        {
            throw new FileNotFoundException("The EPUB file is no longer available.", file.Reference);
        }

        return await storageFile.OpenReadAsync();
    }

    private async Task<IStorageFolder> ResolveFolderAsync(
        SourceFolder folder,
        CancellationToken cancellationToken)
    {
        if (currentFolder is not null && string.Equals(currentFolder.Path.ToString(), folder.Reference, StringComparison.Ordinal))
        {
            return currentFolder;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var resolved = await storageProvider.TryGetFolderFromPathAsync(new Uri(folder.Reference));
        return resolved ?? throw new DirectoryNotFoundException(folder.Reference);
    }

    private async IAsyncEnumerable<SourceFile> EnumerateFolderAsync(
        IStorageFolder folder,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in folder.GetItemsAsync().WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is IStorageFolder childFolder)
            {
                await foreach (var file in EnumerateFolderAsync(childFolder, cancellationToken))
                {
                    yield return file;
                }
            }
            else if (item is IStorageFile file && file.Name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
            {
                var reference = file.Path.ToString();
                files[reference] = file;
                yield return new SourceFile(reference, file.Name);
            }

            await Task.Yield();
        }
    }

    private static SourceFolder ToSourceFolder(IStorageFolder folder, string? displayName = null)
    {
        return new SourceFolder(folder.Path.ToString(), displayName ?? folder.Name);
    }
}
