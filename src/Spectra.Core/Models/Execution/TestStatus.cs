using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Execution result for a single test.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TestStatus
{
    /// <summary>Not yet executed.</summary>
    Pending,

    /// <summary>Currently being executed.</summary>
    InProgress,

    /// <summary>Test passed.</summary>
    Passed,

    /// <summary>Test failed.</summary>
    Failed,

    /// <summary>Manually skipped with reason.</summary>
    Skipped,

    /// <summary>Auto-blocked due to dependency failure.</summary>
    Blocked
}
