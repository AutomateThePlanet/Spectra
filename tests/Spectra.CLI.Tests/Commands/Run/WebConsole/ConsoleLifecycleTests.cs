using System.Diagnostics;
using Spectra.CLI.Commands.Run.WebConsole;

namespace Spectra.CLI.Tests.Commands.Run.WebConsole;

/// <summary>
/// Spec 066 (US4): the detached-lifecycle bookkeeping — marker round-trip, stale-marker detection
/// (dead/mismatched pid → treated as absent), and a clean <c>--stop</c> when nothing is running
/// (FR-010/FR-012, SC-005). The detached <em>launch</em> itself (spawning a worker) is exercised manually
/// per the T020 prototype gate and not in unit tests (it would spawn a real server process).
/// </summary>
public class ConsoleLifecycleTests
{
    private sealed class TempDir : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"spectra-console-{Guid.NewGuid():N}");
        public TempDir() => Directory.CreateDirectory(Path.Combine(Root, ".execution"));
        public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
    }

    [Fact]
    public async Task Marker_RoundTrips()
    {
        using var dir = new TempDir();
        var data = new ConsoleMarkerData(1234, 7878, "http://127.0.0.1:7878/", DateTime.UtcNow.ToString("O"), "run_x");
        await ConsoleMarker.WriteAsync(dir.Root, data);

        var read = await ConsoleMarker.ReadAsync(dir.Root);
        Assert.NotNull(read);
        Assert.Equal(7878, read!.Port);
        Assert.Equal("http://127.0.0.1:7878/", read.Url);
        Assert.Equal("run_x", read.RunId);
    }

    [Fact]
    public void IsLive_CurrentProcess_True()
    {
        var self = Process.GetCurrentProcess();
        var marker = new ConsoleMarkerData(self.Id, 7878, "http://127.0.0.1:7878/",
            self.StartTime.ToUniversalTime().ToString("O"), null);
        Assert.True(ConsoleMarker.IsLive(marker));
    }

    [Fact]
    public void IsLive_DeadPid_False()
    {
        // A pid that (almost certainly) does not map to a running process.
        var marker = new ConsoleMarkerData(999_999_999, 7878, "http://127.0.0.1:7878/", DateTime.UtcNow.ToString("O"), null);
        Assert.False(ConsoleMarker.IsLive(marker));
    }

    [Fact]
    public void IsLive_PidReuse_StartTimeMismatch_False()
    {
        // Current pid but a wildly wrong start time → treated as a reused pid (stale).
        var self = Process.GetCurrentProcess();
        var marker = new ConsoleMarkerData(self.Id, 7878, "http://127.0.0.1:7878/",
            DateTime.UtcNow.AddYears(-5).ToString("O"), null);
        Assert.False(ConsoleMarker.IsLive(marker));
    }

    [Fact]
    public async Task Stop_NoRunningConsole_ReportsCleanly()
    {
        using var dir = new TempDir();
        var code = await new ConsoleCommand(dir.Root).StopAsync();
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Stop_StaleMarker_RemovesItAndReturnsZero()
    {
        using var dir = new TempDir();
        await ConsoleMarker.WriteAsync(dir.Root,
            new ConsoleMarkerData(999_999_999, 7878, "http://127.0.0.1:7878/", DateTime.UtcNow.ToString("O"), null));

        var code = await new ConsoleCommand(dir.Root).StopAsync();

        Assert.Equal(0, code);
        Assert.Null(await ConsoleMarker.ReadAsync(dir.Root));
    }
}
