using Spectra.Core.IdAllocation;

namespace Spectra.Core.Tests.IdAllocation;

public sealed class FileLockHandleTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _lockPath;

    public FileLockHandleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _lockPath = Path.Combine(_tempDir, "test.lock");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task AcquireAsync_NoContention_Succeeds()
    {
        using var handle = await FileLockHandle.AcquireAsync(_lockPath, TimeSpan.FromSeconds(2));
        Assert.NotNull(handle);
    }

    [Fact]
    public async Task AcquireAsync_HeldByAnother_TimesOut()
    {
        using var first = await FileLockHandle.AcquireAsync(_lockPath, TimeSpan.FromSeconds(2));

        // Second acquisition should fail within ~500 ms timeout
        await Assert.ThrowsAsync<TimeoutException>(() =>
            FileLockHandle.AcquireAsync(_lockPath, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task Dispose_ReleasesLock_AllowsReacquire()
    {
        var first = await FileLockHandle.AcquireAsync(_lockPath, TimeSpan.FromSeconds(2));
        first.Dispose();

        using var second = await FileLockHandle.AcquireAsync(_lockPath, TimeSpan.FromSeconds(2));
        Assert.NotNull(second);
    }

    [Fact]
    public async Task AcquireAsync_CreatesParentDirectory()
    {
        var nested = Path.Combine(_tempDir, "nested", "deep", "test.lock");
        using var handle = await FileLockHandle.AcquireAsync(nested, TimeSpan.FromSeconds(2));
        Assert.True(Directory.Exists(Path.GetDirectoryName(nested)));
    }

    [Fact]
    public async Task AcquireAsync_RetriesUntilSuccess()
    {
        // Hold lock for a short window, ensure the second call eventually succeeds
        var first = await FileLockHandle.AcquireAsync(_lockPath, TimeSpan.FromSeconds(2));

        var releaseTask = Task.Run(async () =>
        {
            await Task.Delay(300);
            first.Dispose();
        });

        using var second = await FileLockHandle.AcquireAsync(_lockPath, TimeSpan.FromSeconds(3));
        await releaseTask;
        Assert.NotNull(second);
    }
}
