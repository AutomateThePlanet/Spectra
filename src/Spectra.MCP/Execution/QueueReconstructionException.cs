namespace Spectra.MCP.Execution;

/// <summary>
/// Thrown when a run's persisted orchestration snapshot cannot faithfully rebuild the execution
/// queue (snapshot missing, incomplete, or internally inconsistent). This is the fail-loud signal
/// required by spec 064 — it is distinct from the benign "run not found" case (which surfaces as a
/// null queue) and MUST NOT be surfaced to callers as RUN_NOT_FOUND.
/// </summary>
public sealed class QueueReconstructionException : Exception
{
    /// <summary>The run whose queue could not be reconstructed.</summary>
    public string RunId { get; }

    public QueueReconstructionException(string runId, string message)
        : base(message)
    {
        RunId = runId;
    }
}
