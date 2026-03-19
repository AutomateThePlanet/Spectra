using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Defines the contract for critic model runtimes that verify tests against documentation.
/// </summary>
public interface ICriticRuntime
{
    /// <summary>
    /// Verifies a test case against source documentation.
    /// </summary>
    /// <param name="test">The test case to verify</param>
    /// <param name="relevantDocs">Source documents to verify against</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Verification result with verdict and findings</returns>
    Task<VerificationResult> VerifyTestAsync(
        TestCase test,
        IReadOnlyList<SourceDocument> relevantDocs,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if the critic runtime is available and configured.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the model name used for verification.
    /// </summary>
    string ModelName { get; }
}

/// <summary>
/// A source document for verification context.
/// </summary>
public sealed record SourceDocument
{
    /// <summary>
    /// Relative path to the document.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Document title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Relevant content from the document.
    /// </summary>
    public required string Content { get; init; }
}
