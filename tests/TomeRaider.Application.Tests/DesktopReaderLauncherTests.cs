using TomeRaider.Application;
using TomeRaider.Platform.Storage;
using TUnit.Assertions;
using TUnit.Core;

namespace TomeRaider.Application.Tests;

public sealed class DesktopReaderLauncherTests
{
    [Test]
    public async Task TreatsANonThrowingShellHandoffWithoutAProcessAsRequested()
    {
        using var file = TestFile.Create();
        var nativeLauncher = new ControlledNativeLauncher(null);
        var launcher = new DesktopReaderLauncher(nativeLauncher, TimeProvider.System);

        var result = await launcher.LaunchAsync(file.SourceFile);

        await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchRequested);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(nativeLauncher.RequestedPath).IsEqualTo(file.Path);
    }

    [Test]
    public async Task ReportsANonZeroNativeExitAsALaunchFailure()
    {
        using var file = TestFile.Create();
        var process = new ControlledNativeProcess(exitCode: 17, completesImmediately: true);
        var launcher = new DesktopReaderLauncher(
            new ControlledNativeLauncher(process),
            TimeProvider.System);

        var result = await launcher.LaunchAsync(file.SourceFile);

        await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchFailed);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(process.Disposed).IsTrue();
    }

    [Test]
    public async Task CancelsAndDisposesAProcessWhenTheObservationWindowExpires()
    {
        using var file = TestFile.Create();
        var process = new ControlledNativeProcess(exitCode: 0, completesImmediately: false);
        var launcher = new DesktopReaderLauncher(
            new ControlledNativeLauncher(process),
            new ImmediateTimeProvider());

        var result = await launcher.LaunchAsync(file.SourceFile);

        await Assert.That(result.Outcome).IsEqualTo(ReaderLaunchOutcome.LaunchRequested);
        await Assert.That(process.CancellationObserved).IsTrue();
        await Assert.That(process.Disposed).IsTrue();
    }

    [Test]
    public async Task CancelsAndDisposesAProcessWhenTheCallerCancels()
    {
        using var file = TestFile.Create();
        var process = new ControlledNativeProcess(exitCode: 0, completesImmediately: false);
        var launcher = new DesktopReaderLauncher(
            new ControlledNativeLauncher(process),
            TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        var launchTask = launcher.LaunchAsync(file.SourceFile, cancellation.Token);

        cancellation.Cancel();

        await Assert.That(async () => await launchTask).Throws<OperationCanceledException>();
        await Assert.That(process.CancellationObserved).IsTrue();
        await Assert.That(process.Disposed).IsTrue();
    }

    private sealed class ControlledNativeLauncher(IReaderProcess? process) : IReaderProcessLauncher
    {
        public string? RequestedPath { get; private set; }

        public IReaderProcess? Start(string localPath)
        {
            RequestedPath = localPath;
            return process;
        }
    }

    private sealed class ControlledNativeProcess(int exitCode, bool completesImmediately) : IReaderProcess
    {
        private readonly TaskCompletionSource<bool> exitSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int ExitCode => exitCode;

        public bool CancellationObserved { get; private set; }

        public bool Disposed { get; private set; }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            if (completesImmediately)
            {
                return Task.CompletedTask;
            }

            cancellationToken.Register(() =>
            {
                CancellationObserved = true;
                exitSource.TrySetCanceled(cancellationToken);
            });
            return exitSource.Task;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoOpTimer();
        }

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class TestFile : IDisposable
    {
        private TestFile(string path)
        {
            Path = path;
            SourceFile = new SourceFile(path, "synthetic.epub");
        }

        public string Path { get; }

        public SourceFile SourceFile { get; }

        public static TestFile Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "tome-raider-reader-launch-" + Guid.NewGuid().ToString("N") + ".epub");
            File.WriteAllText(path, "synthetic EPUB placeholder");
            return new TestFile(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
