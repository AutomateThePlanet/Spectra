using Spectra.CLI.Cancellation;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Cancellation;

[Collection("WorkingDirectory")]
public sealed class CancellationManagerTests : IDisposable
{
    private readonly TempWorkspace _ws;

    public CancellationManagerTests()
    {
        _ws = new TempWorkspace("spectra-cm");
    }

    public void Dispose()
    {
        _ws.Dispose();
    }

    [Fact]
    public async Task RegisterCommandAsync_WritesPidFile()
    {
        using var _ = CancellationManager.OverrideForTests(_ws.Root);
        using var registration = await CancellationManager.Instance.RegisterCommandAsync("test command");

        Assert.True(File.Exists(CancellationManager.Instance.PidPath));
    }

    [Fact]
    public async Task UnregisterCommand_RemovesPidFile()
    {
        using var _ = CancellationManager.OverrideForTests(_ws.Root);
        var registration = await CancellationManager.Instance.RegisterCommandAsync("test command");
        Assert.True(File.Exists(CancellationManager.Instance.PidPath));

        registration.Dispose();
        Assert.False(File.Exists(CancellationManager.Instance.PidPath));
    }

    [Fact]
    public async Task RegisterCommandAsync_ClearsStaleSentinel()
    {
        using var _ = CancellationManager.OverrideForTests(_ws.Root);
        var sentinelPath = CancellationManager.Instance.SentinelPath;
        Directory.CreateDirectory(Path.GetDirectoryName(sentinelPath)!);
        File.WriteAllText(sentinelPath, "");

        using var registration = await CancellationManager.Instance.RegisterCommandAsync("test");
        Assert.False(File.Exists(sentinelPath));
    }

    [Fact]
    public async Task RegisterCommandAsync_DoubleRegister_Throws()
    {
        using var _ = CancellationManager.OverrideForTests(_ws.Root);
        using var first = await CancellationManager.Instance.RegisterCommandAsync("a");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CancellationManager.Instance.RegisterCommandAsync("b"));
    }

    [Fact]
    public async Task SentinelFileTriggersToken()
    {
        using var _ = CancellationManager.OverrideForTests(_ws.Root);
        using var registration = await CancellationManager.Instance.RegisterCommandAsync("test");

        var sentinelPath = CancellationManager.Instance.SentinelPath;
        File.WriteAllText(sentinelPath, "");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!CancellationManager.Instance.Token.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(CancellationManager.Instance.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task ThrowIfCancellationRequested_SentinelPresent_Throws()
    {
        using var _ = CancellationManager.OverrideForTests(_ws.Root);
        using var registration = await CancellationManager.Instance.RegisterCommandAsync("test");

        File.WriteAllText(CancellationManager.Instance.SentinelPath, "");

        Assert.Throws<OperationCanceledException>(() =>
            CancellationManager.Instance.ThrowIfCancellationRequested());
    }

    [Fact]
    public async Task ExternalToken_Cancelled_PropagatesIntoLinkedToken()
    {
        using var _ = CancellationManager.OverrideForTests(_ws.Root);
        using var externalCts = new CancellationTokenSource();
        using var registration = await CancellationManager.Instance.RegisterCommandAsync("test", externalCts.Token);

        externalCts.Cancel();
        Assert.True(CancellationManager.Instance.Token.IsCancellationRequested);
    }
}
