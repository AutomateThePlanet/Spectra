namespace Spectra.Core.Models;

/// <summary>
/// Information about a test suite for discovery.
/// </summary>
public record SuiteInfo(
    string Name,
    int TestCount,
    string Path,
    bool IsStale = false
);
