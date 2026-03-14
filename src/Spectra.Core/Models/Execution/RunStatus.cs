namespace Spectra.Core.Models.Execution;

/// <summary>
/// Current state of a test execution run.
/// </summary>
public enum RunStatus
{
    /// <summary>Run initialized, not yet started.</summary>
    Created,

    /// <summary>Tests being executed.</summary>
    Running,

    /// <summary>Temporarily stopped, can resume.</summary>
    Paused,

    /// <summary>All tests done, report generated.</summary>
    Completed,

    /// <summary>Manually stopped before completion.</summary>
    Cancelled,

    /// <summary>Auto-cancelled after timeout (72h default).</summary>
    Abandoned
}
