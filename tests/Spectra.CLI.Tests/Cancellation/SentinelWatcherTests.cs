using Spectra.CLI.Cancellation;

namespace Spectra.CLI.Tests.Cancellation;

public sealed class SentinelWatcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sentinelPath;

    public SentinelWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-sentinel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sentinelPath = Path.Combine(_tempDir, ".cancel");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Start_TriggersWhenSentinelAppears()
    {
        var triggered = false;
        using var watcher = new SentinelWatcher(_sentinelPath, TimeSpan.FromMilliseconds(50));
        watcher.Start(() => triggered = true);

        await Task.Delay(100);
        Assert.False(triggered);

        File.WriteAllText(_sentinelPath, "");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!triggered && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(triggered);
    }

    [Fact]
    public async Task Start_TwiceThrows()
    {
        using var watcher = new SentinelWatcher(_sentinelPath);
        watcher.Start(() => { });

        Assert.Throws<InvalidOperationException>(() => watcher.Start(() => { }));
        await Task.CompletedTask;
    }

    [Fact]
    public void Dispose_StopsPolling()
    {
        var triggered = false;
        var watcher = new SentinelWatcher(_sentinelPath, TimeSpan.FromMilliseconds(50));
        watcher.Start(() => triggered = true);
        watcher.Dispose();

        // After dispose, even creating the sentinel should not trigger
        File.WriteAllText(_sentinelPath, "");
        Thread.Sleep(200);
        Assert.False(triggered);
    }

    [Fact]
    public async Task Start_FiresOnlyOnceEvenAfterReappearance()
    {
        var triggerCount = 0;
        using var watcher = new SentinelWatcher(_sentinelPath, TimeSpan.FromMilliseconds(50));
        watcher.Start(() => Interlocked.Increment(ref triggerCount));

        File.WriteAllText(_sentinelPath, "");
        await Task.Delay(200);

        File.Delete(_sentinelPath);
        await Task.Delay(100);
        File.WriteAllText(_sentinelPath, "");
        await Task.Delay(200);

        Assert.Equal(1, triggerCount);
    }
}
