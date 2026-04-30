using Spectra.Core.IdAllocation;

namespace Spectra.Core.Tests.IdAllocation;

public sealed class PersistentTestIdAllocatorTests : IDisposable
{
    private readonly string _workspace;

    public PersistentTestIdAllocatorTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"spectra-pa-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspace);
        Directory.CreateDirectory(Path.Combine(_workspace, "test-cases"));
        Directory.CreateDirectory(Path.Combine(_workspace, ".spectra"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_workspace, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task AllocateAsync_FreshWorkspace_StartsAtIdStart()
    {
        var allocator = new PersistentTestIdAllocator(_workspace);
        var ids = await allocator.AllocateAsync(3, "TC", 100, "test");
        Assert.Equal(new[] { "TC-100", "TC-101", "TC-102" }, ids);
    }

    [Fact]
    public async Task AllocateAsync_ZeroCount_ReturnsEmpty()
    {
        var allocator = new PersistentTestIdAllocator(_workspace);
        var ids = await allocator.AllocateAsync(0, "TC", 100, "test");
        Assert.Empty(ids);
    }

    [Fact]
    public async Task AllocateAsync_HwmMonotonic_PersistsAcrossInstances()
    {
        var first = new PersistentTestIdAllocator(_workspace);
        await first.AllocateAsync(5, "TC", 100, "first");

        var second = new PersistentTestIdAllocator(_workspace);
        var nextIds = await second.AllocateAsync(2, "TC", 100, "second");

        Assert.Equal(new[] { "TC-105", "TC-106" }, nextIds);
    }

    [Fact]
    public async Task AllocateAsync_DeletedIdNeverReused()
    {
        // Allocate, then delete the resulting test files; allocator must not
        // reuse the deleted IDs because the HWM is monotonic.
        var allocator = new PersistentTestIdAllocator(_workspace);
        var first = await allocator.AllocateAsync(3, "TC", 100, "first");
        Assert.Equal(new[] { "TC-100", "TC-101", "TC-102" }, first);

        // Simulate "deleted IDs" (no actual files were ever written; HWM still advanced)
        var next = await allocator.AllocateAsync(1, "TC", 100, "second");
        Assert.Equal("TC-103", next[0]);
    }

    [Fact]
    public async Task AllocateAsync_SkipsPastFilesystemMax()
    {
        // Plant a TC-200 file on disk with no HWM; the allocator should skip past it.
        var dir = Path.Combine(_workspace, "test-cases", "checkout");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "TC-200.md"), "---\nid: TC-200\n---\nbody");

        var allocator = new PersistentTestIdAllocator(_workspace);
        var ids = await allocator.AllocateAsync(2, "TC", 100, "test");

        Assert.Equal(new[] { "TC-201", "TC-202" }, ids);
    }

    [Fact]
    public async Task AllocateAsync_RespectsIdStartFloor()
    {
        var allocator = new PersistentTestIdAllocator(_workspace);
        var ids = await allocator.AllocateAsync(2, "TC", 500, "test");
        Assert.Equal(new[] { "TC-500", "TC-501" }, ids);
    }

    [Fact]
    public async Task AllocateAsync_ConcurrentAllocations_NoCollisions()
    {
        // Two threads racing on the same workspace must produce disjoint ranges.
        var allocator1 = new PersistentTestIdAllocator(_workspace);
        var allocator2 = new PersistentTestIdAllocator(_workspace);

        var task1 = allocator1.AllocateAsync(10, "TC", 100, "run1");
        var task2 = allocator2.AllocateAsync(10, "TC", 100, "run2");

        await Task.WhenAll(task1, task2);

        var combined = task1.Result.Concat(task2.Result).ToList();
        Assert.Equal(20, combined.Count);
        Assert.Equal(20, combined.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task AllocateAsync_NegativeCount_Throws()
    {
        var allocator = new PersistentTestIdAllocator(_workspace);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            allocator.AllocateAsync(-1, "TC", 100, "test"));
    }

    [Fact]
    public async Task PeekNextAsync_DoesNotMutateHwm()
    {
        var allocator = new PersistentTestIdAllocator(_workspace);
        await allocator.AllocateAsync(5, "TC", 100, "test");

        var (effective, nextId) = await allocator.PeekNextAsync("TC", 100);
        Assert.Equal(104, effective);
        Assert.Equal("TC-105", nextId);

        // Peek again — same value
        var second = await allocator.PeekNextAsync("TC", 100);
        Assert.Equal(104, second.Effective);
    }

    [Fact]
    public async Task AllocateAsync_FirstRunSeed_LogsInfoLine()
    {
        // Plant some on-disk tests; first allocation should log seed info.
        var dir = Path.Combine(_workspace, "test-cases", "checkout");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "TC-150.md"), "---\nid: TC-150\n---\nbody");

        var logs = new List<string>();
        var allocator = new PersistentTestIdAllocator(_workspace, msg => logs.Add(msg));
        await allocator.AllocateAsync(1, "TC", 100, "ai generate");

        Assert.Contains(logs, l => l.Contains("Initialized ID allocator", StringComparison.Ordinal));
    }
}
