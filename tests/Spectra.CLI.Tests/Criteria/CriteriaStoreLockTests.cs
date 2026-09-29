using Spectra.CLI.Criteria;
using Spectra.Core.IdAllocation;

namespace Spectra.CLI.Tests.Criteria;

/// <summary>Spec 079 FR-009: every criteria write runs under one cross-process lock.</summary>
public sealed class CriteriaStoreLockTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spectra-079-lock-" + Guid.NewGuid().ToString("N"));

    public CriteriaStoreLockTests() => Directory.CreateDirectory(Path.Combine(_root, ".spectra"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string LockPath => Path.Combine(_root, ".spectra", "criteria.lock");

    [Fact]
    public async Task HeldLock_TimesOut_WithLockTimeout()
    {
        using var held = await FileLockHandle.AcquireAsync(LockPath, TimeSpan.FromSeconds(1));

        var ex = await Assert.ThrowsAsync<CriteriaStoreException>(() =>
            CriteriaStore.WithLockAsync(_root, () => Task.FromResult(1), default, TimeSpan.FromMilliseconds(200)));

        Assert.Equal(CriteriaStore.ErrorLockTimeout, ex.ErrorCode);
    }

    [Fact]
    public async Task WaitingWriter_ProceedsAfterRelease()
    {
        var held = await FileLockHandle.AcquireAsync(LockPath, TimeSpan.FromSeconds(1));
        var waiting = CriteriaStore.WithLockAsync(_root, () => Task.FromResult(42), default, TimeSpan.FromSeconds(5));

        await Task.Delay(150);
        Assert.False(waiting.IsCompleted);
        held.Dispose();

        Assert.Equal(42, await waiting);
    }
}
