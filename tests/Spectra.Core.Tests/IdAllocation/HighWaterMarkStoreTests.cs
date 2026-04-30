using Spectra.Core.IdAllocation;

namespace Spectra.Core.Tests.IdAllocation;

public sealed class HighWaterMarkStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _hwmPath;

    public HighWaterMarkStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-hwm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _hwmPath = Path.Combine(_tempDir, "id-allocator.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsZero()
    {
        var store = new HighWaterMarkStore(_hwmPath);
        var value = await store.ReadAsync();
        Assert.Equal(0, value);
    }

    [Fact]
    public async Task RoundTrip_PreservesValue()
    {
        var store = new HighWaterMarkStore(_hwmPath);
        await store.WriteAsync(247, "ai generate");

        var read = await store.ReadAsync();
        Assert.Equal(247, read);
    }

    [Fact]
    public async Task ReadWithWarningAsync_CorruptedFile_ReturnsZeroWithWarning()
    {
        await File.WriteAllTextAsync(_hwmPath, "{ not valid json");

        var store = new HighWaterMarkStore(_hwmPath);
        var (value, warning) = await store.ReadWithWarningAsync();

        Assert.Equal(0, value);
        Assert.NotNull(warning);
        Assert.Contains("unreadable", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadWithWarningAsync_FutureVersion_ReturnsZeroWithWarning()
    {
        await File.WriteAllTextAsync(_hwmPath,
            """{"version":99,"high_water_mark":50,"last_allocated_at":"2026-01-01T00:00:00Z","last_allocated_command":"future"}""");

        var store = new HighWaterMarkStore(_hwmPath);
        var (value, warning) = await store.ReadWithWarningAsync();

        Assert.Equal(0, value);
        Assert.NotNull(warning);
        Assert.Contains("version", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteAsync_AtomicViaTempRename_NoLeftoverTmpFile()
    {
        var store = new HighWaterMarkStore(_hwmPath);
        await store.WriteAsync(42, "test");

        Assert.True(File.Exists(_hwmPath));
        Assert.False(File.Exists(_hwmPath + ".tmp"));
    }

    [Fact]
    public async Task WriteAsync_NegativeValue_Throws()
    {
        var store = new HighWaterMarkStore(_hwmPath);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.WriteAsync(-1, "test"));
    }
}
