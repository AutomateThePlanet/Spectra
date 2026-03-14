using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.Core.Tests.Index;

public class TestIdAllocatorTests
{
    [Fact]
    public void AllocateNext_StartsFromStartNumber()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);

        var id = allocator.AllocateNext();

        Assert.Equal("TC-100", id);
    }

    [Fact]
    public void AllocateNext_IncrementsSequentially()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);

        var id1 = allocator.AllocateNext();
        var id2 = allocator.AllocateNext();
        var id3 = allocator.AllocateNext();

        Assert.Equal("TC-100", id1);
        Assert.Equal("TC-101", id2);
        Assert.Equal("TC-102", id3);
    }

    [Fact]
    public void AllocateNext_UsesCustomPrefix()
    {
        var allocator = new TestIdAllocator("TEST", startNumber: 1);

        var id = allocator.AllocateNext();

        Assert.Equal("TEST-001", id);
    }

    [Fact]
    public void AllocateNext_SkipsReservedIds()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);
        allocator.Reserve("TC-100");
        allocator.Reserve("TC-101");

        var id = allocator.AllocateNext();

        Assert.Equal("TC-102", id);
    }

    [Fact]
    public void AllocateMany_ReturnsRequestedCount()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);

        var ids = allocator.AllocateMany(5);

        Assert.Equal(5, ids.Count);
        Assert.Equal(["TC-100", "TC-101", "TC-102", "TC-103", "TC-104"], ids);
    }

    [Fact]
    public void AllocateMany_ZeroCount_ReturnsEmpty()
    {
        var allocator = new TestIdAllocator();

        var ids = allocator.AllocateMany(0);

        Assert.Empty(ids);
    }

    [Fact]
    public void Reserve_PreventsIdFromBeingAllocated()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);
        allocator.Reserve("TC-100");

        var id = allocator.AllocateNext();

        Assert.Equal("TC-101", id);
    }

    [Fact]
    public void Reserve_UpdatesNextNumber()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);
        allocator.Reserve("TC-150");

        var id = allocator.AllocateNext();

        Assert.Equal("TC-151", id);
    }

    [Fact]
    public void ReserveMany_ReservesAllIds()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);
        allocator.ReserveMany(["TC-100", "TC-101", "TC-105"]);

        Assert.False(allocator.IsAvailable("TC-100"));
        Assert.False(allocator.IsAvailable("TC-101"));
        Assert.False(allocator.IsAvailable("TC-105"));
        Assert.True(allocator.IsAvailable("TC-102"));
    }

    [Fact]
    public void IsAvailable_UnreservedId_ReturnsTrue()
    {
        var allocator = new TestIdAllocator();

        Assert.True(allocator.IsAvailable("TC-999"));
    }

    [Fact]
    public void IsAvailable_ReservedId_ReturnsFalse()
    {
        var allocator = new TestIdAllocator();
        allocator.Reserve("TC-100");

        Assert.False(allocator.IsAvailable("TC-100"));
    }

    [Fact]
    public void IsAvailable_AllocatedId_ReturnsFalse()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);
        var id = allocator.AllocateNext();

        Assert.False(allocator.IsAvailable(id));
    }

    [Fact]
    public void PeekNextNumber_DoesNotAllocate()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);

        var peek1 = allocator.PeekNextNumber();
        var peek2 = allocator.PeekNextNumber();
        var actual = allocator.AllocateNext();

        Assert.Equal(100, peek1);
        Assert.Equal(100, peek2);
        Assert.Equal("TC-100", actual);
    }

    [Fact]
    public void FromExistingTests_ReservesAllIds()
    {
        var tests = new[]
        {
            CreateTestCase("TC-100"),
            CreateTestCase("TC-105"),
            CreateTestCase("TC-103")
        };

        var allocator = TestIdAllocator.FromExistingTests(tests, "TC", 100);

        Assert.False(allocator.IsAvailable("TC-100"));
        Assert.False(allocator.IsAvailable("TC-103"));
        Assert.False(allocator.IsAvailable("TC-105"));
        Assert.Equal("TC-106", allocator.AllocateNext());
    }

    [Fact]
    public void FromExistingIds_ReservesAllIds()
    {
        var allocator = TestIdAllocator.FromExistingIds(
            ["TC-100", "TC-102", "TC-104"],
            "TC",
            100);

        Assert.Equal("TC-105", allocator.AllocateNext());
    }

    [Fact]
    public void GetReservedIds_ReturnsAllReserved()
    {
        var allocator = new TestIdAllocator();
        allocator.Reserve("TC-100");
        allocator.Reserve("TC-101");

        var reserved = allocator.GetReservedIds();

        Assert.Contains("TC-100", reserved);
        Assert.Contains("TC-101", reserved);
    }

    [Fact]
    public void Constructor_NegativeStartNumber_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TestIdAllocator("TC", startNumber: -1));
    }

    [Fact]
    public void Constructor_EmptyPrefix_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TestIdAllocator("", startNumber: 100));
    }

    [Fact]
    public void Reserve_NullOrEmpty_IsIgnored()
    {
        var allocator = new TestIdAllocator("TC", startNumber: 100);

        allocator.Reserve(null!);
        allocator.Reserve("");
        allocator.Reserve("   ");

        var id = allocator.AllocateNext();

        Assert.Equal("TC-100", id);
    }

    private static TestCase CreateTestCase(string id)
    {
        return new TestCase
        {
            Id = id,
            Title = $"Test {id}",
            Priority = Priority.Medium,
            Steps = [],
            ExpectedResult = "Result",
            FilePath = $"{id}.md"
        };
    }
}
