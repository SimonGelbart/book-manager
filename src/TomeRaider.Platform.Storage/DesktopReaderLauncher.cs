using System.ComponentModel;
using System.Diagnostics;
using TomeRaider.Application;

namespace TomeRaider.Platform.Storage;

public sealed class DesktopReaderLauncher : IReaderLauncher
{
    public Task<ReaderLaunchResult> LaunchAsync(
        SourceFile file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localPath = ResolveLocalPath(file.Reference);
        if (localPath is null || !File.Exists(localPath))
        {
            return Task.FromResult(ReaderLaunchResult.FileUnavailable(
                $"The EPUB file '{file.DisplayName}' is not available."));
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = localPath,
                UseShellExecute = true
            });

            return Task.FromResult(process is null
                ? ReaderLaunchResult.LaunchFailed(
                    $"The associated reader did not accept '{file.DisplayName}'.")
                : ReaderLaunchResult.Opened(
                    $"Opened '{file.DisplayName}' in the associated reader."));
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(ReaderLaunchResult.LaunchFailed(
                $"The associated reader could not open '{file.DisplayName}': {exception.Message}"));
        }
    }

    private static string? ResolveLocalPath(string reference)
    {
        if (Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            return uri.IsFile ? uri.LocalPath : null;
        }

        return reference;
    }
}

public sealed class UnsupportedReaderLauncher(string message) : IReaderLauncher
{
    public Task<ReaderLaunchResult> LaunchAsync(
        SourceFile file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReaderLaunchResult.LaunchFailed(message));
    }
}
