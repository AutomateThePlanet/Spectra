using Spectra.CLI.Cancellation;
using Spectra.CLI.Commands.Cancel;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Cancel;

[Collection("WorkingDirectory")]
public sealed class CancelHandlerTests : IDisposable
{
    private readonly TempWorkspace _ws;
    private readonly string _originalCwd;

    public CancelHandlerTests()
    {
        _ws = new TempWorkspace("spectra-cancel-h");
        _originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_ws.Root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        _ws.Dispose();
    }

    [Fact]
    public async Task NoActiveRun_NoPidFile_ReturnsSuccess()
    {
        var handler = new CancelHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(force: false);
        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public async Task StalePid_CleansUp_ReturnsSuccess()
    {
        // Write a stale PID file pointing to a non-existent process
        var pidMgr = new PidFileManager(Path.Combine(_ws.SpectraDir, ".pid"));
        await pidMgr.WriteAsync(99999999, "ai generate", "spectra");

        var handler = new CancelHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(force: false);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(File.Exists(pidMgr.Path), "stale PID file should have been cleaned up");
    }

    [Fact]
    public async Task CooperativeCancel_TargetExitsCleanly_ReturnsSuccess()
    {
        // Simulate a cooperative target: write the PID file, then remove it
        // shortly after to mimic a handler that observed the sentinel and
        // unregistered itself.
        var pidMgr = new PidFileManager(Path.Combine(_ws.SpectraDir, ".pid"));
        var current = System.Diagnostics.Process.GetCurrentProcess();
        await pidMgr.WriteAsync(current.Id, "ai generate", current.ProcessName);

        // After 300ms, simulate cooperative shutdown by removing the PID file.
        var cleanupTask = Task.Run(async () =>
        {
            await Task.Delay(300);
            pidMgr.Delete();
        });

        var handler = new CancelHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(force: false);

        await cleanupTask;
        Assert.Equal(ExitCodes.Success, exit);
    }
}
