namespace Spectra.Core.Models;

/// <summary>
/// Represents a test suite (folder of tests with index).
/// </summary>
public sealed class TestSuite
{
    /// <summary>
    /// Suite name (folder name, e.g., "checkout").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Relative path (e.g., "tests/checkout").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Test cases in this suite.
    /// </summary>
    public IReadOnlyList<TestCase> Tests { get; init; } = [];

    /// <summary>
    /// Metadata index for this suite (null if not yet generated).
    /// </summary>
    public MetadataIndex? Index { get; init; }

    /// <summary>
    /// Number of tests in this suite.
    /// </summary>
    public int TestCount => Tests.Count;
}
