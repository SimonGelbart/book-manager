using System.Runtime.CompilerServices;
using TomeRaider.Application;

namespace TomeRaider.Infrastructure;

public sealed class PhysicalFileSource : IFileSource
{
    private readonly string rootPath;

    public PhysicalFileSource(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException(rootPath);
        }

        this.rootPath = Path.GetFullPath(rootPath);
    }

    public Task<SourceFolder?> SelectFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<SourceFolder?>(CreateFolder());
    }

    public Task<SourceFolder?> RestoreFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<SourceFolder?>(CreateFolder());
    }

    public async IAsyncEnumerable<SourceFile> EnumerateEpubsAsync(
        SourceFolder folder,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(folder.Reference, "*.epub", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SourceFile(file, Path.GetFileName(file));
            await Task.Yield();
        }
    }

    public Task<Stream> OpenReadAsync(
        SourceFile file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = File.OpenRead(file.Reference);
        return Task.FromResult(stream);
    }

    private SourceFolder CreateFolder()
    {
        return new SourceFolder(rootPath, Path.GetFileName(rootPath) is { Length: > 0 } name ? name : rootPath);
    }
}
