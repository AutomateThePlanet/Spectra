using Spectra.Core.Models;

namespace Spectra.Core.Index;

/// <summary>
/// Allocates sequential test IDs for new test cases.
/// </summary>
public sealed class TestIdAllocator
{
    private readonly string _prefix;
    private readonly int _startNumber;
    private readonly HashSet<string> _usedIds;
    private int _nextNumber;

    /// <summary>
    /// Creates a new TestIdAllocator with the specified prefix and start number.
    /// </summary>
    /// <param name="prefix">ID prefix (e.g., "TC"). Default is "TC".</param>
    /// <param name="startNumber">Starting number for new IDs. Default is 100.</param>
    public TestIdAllocator(string prefix = "TC", int startNumber = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (startNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startNumber), "Start number must be non-negative");
        }

        _prefix = prefix;
        _startNumber = startNumber;
        _usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _nextNumber = startNumber;
    }

    /// <summary>
    /// Creates a TestIdAllocator from existing test cases.
    /// </summary>
    public static TestIdAllocator FromExistingTests(
        IEnumerable<TestCase> existingTests,
        string prefix = "TC",
        int startNumber = 100)
    {
        ArgumentNullException.ThrowIfNull(existingTests);

        var allocator = new TestIdAllocator(prefix, startNumber);

        foreach (var test in existingTests)
        {
            allocator.Reserve(test.Id);
        }

        return allocator;
    }

    /// <summary>
    /// Creates a TestIdAllocator from existing IDs.
    /// </summary>
    public static TestIdAllocator FromExistingIds(
        IEnumerable<string> existingIds,
        string prefix = "TC",
        int startNumber = 100)
    {
        ArgumentNullException.ThrowIfNull(existingIds);

        var allocator = new TestIdAllocator(prefix, startNumber);

        foreach (var id in existingIds)
        {
            allocator.Reserve(id);
        }

        return allocator;
    }

    /// <summary>
    /// Allocates the next available ID.
    /// </summary>
    public string AllocateNext()
    {
        while (true)
        {
            var id = FormatId(_nextNumber);
            _nextNumber++;

            if (!_usedIds.Contains(id))
            {
                _usedIds.Add(id);
                return id;
            }
        }
    }

    /// <summary>
    /// Allocates multiple IDs at once.
    /// </summary>
    public IReadOnlyList<string> AllocateMany(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative");
        }

        var ids = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            ids.Add(AllocateNext());
        }

        return ids;
    }

    /// <summary>
    /// Reserves an ID so it won't be allocated.
    /// </summary>
    public void Reserve(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        _usedIds.Add(id);

        // Update next number if this ID's number is higher
        var number = ExtractNumber(id);
        if (number >= _nextNumber)
        {
            _nextNumber = number + 1;
        }
    }

    /// <summary>
    /// Reserves multiple IDs.
    /// </summary>
    public void ReserveMany(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        foreach (var id in ids)
        {
            Reserve(id);
        }
    }

    /// <summary>
    /// Checks if an ID is available (not reserved).
    /// </summary>
    public bool IsAvailable(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return !_usedIds.Contains(id);
    }

    /// <summary>
    /// Gets the next number that would be allocated (without allocating).
    /// </summary>
    public int PeekNextNumber()
    {
        var tempNumber = _nextNumber;

        while (_usedIds.Contains(FormatId(tempNumber)))
        {
            tempNumber++;
        }

        return tempNumber;
    }

    /// <summary>
    /// Gets all reserved IDs.
    /// </summary>
    public IReadOnlySet<string> GetReservedIds()
    {
        return _usedIds;
    }

    private string FormatId(int number)
    {
        return $"{_prefix}-{number:D3}";
    }

    private int ExtractNumber(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        // Try to extract number from ID like "TC-123" or "TEST-001"
        var dashIndex = id.LastIndexOf('-');
        if (dashIndex < 0 || dashIndex >= id.Length - 1)
        {
            return 0;
        }

        var numberPart = id[(dashIndex + 1)..];

        return int.TryParse(numberPart, out var number) ? number : 0;
    }
}
