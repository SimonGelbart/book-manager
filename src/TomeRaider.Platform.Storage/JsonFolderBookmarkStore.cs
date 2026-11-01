using System.Text.Json;

namespace TomeRaider.Platform.Storage;

public sealed class JsonFolderBookmarkStore
{
    private readonly string path;

    public JsonFolderBookmarkStore(string path)
    {
        this.path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
    }

    public Task SaveAsync(
        string bookmark,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var stored = new StoredFolderBookmark(bookmark, displayName);
        return File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(stored),
            cancellationToken);
    }

    public async Task<(string Bookmark, string DisplayName)?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var stored = await JsonSerializer.DeserializeAsync<StoredFolderBookmark>(
            stream,
            options: null,
            cancellationToken);
        return stored is null || string.IsNullOrWhiteSpace(stored.Bookmark)
            ? null
            : (stored.Bookmark, stored.DisplayName);
    }

    private sealed record StoredFolderBookmark(string Bookmark, string DisplayName);
}
