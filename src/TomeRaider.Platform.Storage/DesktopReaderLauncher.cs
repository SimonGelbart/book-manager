using System.Diagnostics;
using TomeRaider.Application;

namespace TomeRaider.Platform.Storage;

public sealed class DesktopReaderLauncher : IReaderLauncher
{
    private static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(2);
    private readonly IReaderProcessLauncher processLauncher;
    private readonly TimeProvider timeProvider;

    public DesktopReaderLauncher()
        : this(new SystemReaderProcessLauncher(), TimeProvider.System)
    {
    }

    internal DesktopReaderLauncher(
        IReaderProcessLauncher processLauncher,
        TimeProvider timeProvider)
    {
        this.processLauncher = processLauncher;
        this.timeProvider = timeProvider;
    }

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
            var process = processLauncher.Start(localPath);

            if (process is null)
            {
                return ReaderLaunchResult.LaunchRequested(
                    $"Requested '{file.DisplayName}' in the associated reader.");
            }

            using (process)
            {
                var exited = await WaitForExitWithinAsync(process, cancellationToken);
                return exited && process.ExitCode != 0
                    ? ReaderLaunchResult.LaunchFailed(
                        $"The associated reader failed to open '{file.DisplayName}' (exit code {process.ExitCode}).")
                    : ReaderLaunchResult.LaunchRequested(
                        $"Requested '{file.DisplayName}' in the associated reader.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ReaderLaunchResult.LaunchFailed(
                $"The associated reader could not open '{file.DisplayName}': {exception.Message}");
        }
    }

    private async Task<bool> WaitForExitWithinAsync(
        IReaderProcess process,
        CancellationToken cancellationToken)
    {
        using var observationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var waitForExit = process.WaitForExitAsync(observationCancellation.Token);
            var timeoutTask = Task.Delay(ObservationWindow, timeProvider, observationCancellation.Token);
            var completed = await Task.WhenAny(waitForExit, timeoutTask);
            if (completed == waitForExit)
            {
                await waitForExit;
                return true;
            }

            observationCancellation.Cancel();
            await ObserveCancelledWaitAsync(waitForExit);
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        finally
        {
            observationCancellation.Cancel();
        }
    }

    private static async Task ObserveCancelledWaitAsync(Task waitForExit)
    {
        try
        {
            await waitForExit;
        }
        catch (OperationCanceledException)
        {
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

internal interface IReaderProcessLauncher
{
    IReaderProcess? Start(string localPath);
}

internal interface IReaderProcess : IDisposable
{
    int ExitCode { get; }

    Task WaitForExitAsync(CancellationToken cancellationToken);
}

internal sealed class SystemReaderProcessLauncher : IReaderProcessLauncher
{
    public IReaderProcess? Start(string localPath)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = localPath,
            UseShellExecute = true
        });

        return process is null ? null : new SystemReaderProcess(process);
    }
}

internal sealed class SystemReaderProcess(Process process) : IReaderProcess
{
    public int ExitCode => process.ExitCode;

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        process.WaitForExitAsync(cancellationToken);

    public void Dispose()
    {
        process.Dispose();
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
