using System.Diagnostics;
using TomeRaider.Application;

namespace TomeRaider.Platform.Storage;

public sealed class DesktopReaderLauncher : IReaderLauncher
{
    public async Task<ReaderLaunchResult> LaunchAsync(
        SourceFile file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localPath = ResolveLocalPath(file.Reference);
        if (localPath is null || !File.Exists(localPath))
        {
            return ReaderLaunchResult.FileUnavailable(
                $"The EPUB file '{file.DisplayName}' is not available.");
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = localPath,
                UseShellExecute = true
            });

            if (process is null)
            {
                return ReaderLaunchResult.LaunchFailed(
                    $"The associated reader did not accept '{file.DisplayName}'.");
            }

            var exited = await WaitForExitWithinAsync(
                process,
                TimeSpan.FromSeconds(2),
                cancellationToken);
            return exited && process.ExitCode != 0
                ? ReaderLaunchResult.LaunchFailed(
                    $"The associated reader failed to open '{file.DisplayName}' (exit code {process.ExitCode}).")
                : ReaderLaunchResult.LaunchRequested(
                    $"Requested '{file.DisplayName}' in the associated reader.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ReaderLaunchResult.LaunchFailed(
                $"The associated reader could not open '{file.DisplayName}': {exception.Message}");
        }
    }

    private static async Task<bool> WaitForExitWithinAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var waitForExit = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(waitForExit, timeoutTask);
        cancellationToken.ThrowIfCancellationRequested();
        return completed == waitForExit;
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
