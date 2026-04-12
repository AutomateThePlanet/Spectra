using Spectra.Core.Models;
using Spectra.Core.Models.Testimize;
using TokenUsage = Spectra.Core.Models.TokenUsage;

namespace Spectra.CLI.Agent;

/// <summary>
/// Defines the contract for AI agent runtimes.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Generates test cases from full source documentation.
    /// Uses document-grounded generation with semantic deduplication.
    /// </summary>
    /// <param name="prompt">The user's request prompt</param>
    /// <param name="documents">Full source documents with content</param>
    /// <param name="existingTests">Existing tests for semantic deduplication</param>
    /// <param name="requestedCount">Number of tests requested</param>
    /// <param name="criteriaContext">Optional acceptance criteria context block</param>
    /// <param name="testimizeData">
    /// Optional pre-computed algorithmic test data for this suite (from
    /// <see cref="Spectra.CLI.Agent.Testimize.TestimizeRunner"/>). When non-null,
    /// the agent embeds it into the generation prompt as an authoritative
    /// "Pre-computed algorithmic test data (from Testimize…)" block so the
    /// model uses the exact values/categories/messages verbatim.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated test cases</returns>
    Task<GenerationResult> GenerateTestsAsync(
        string prompt,
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<TestCase> existingTests,
        int requestedCount,
        string? criteriaContext = null,
        TestimizeDataset? testimizeData = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if the runtime is available and configured.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Result of test generation.
/// </summary>
public sealed class GenerationResult
{
    /// <summary>
    /// Generated test cases.
    /// </summary>
    public required IReadOnlyList<TestCase> Tests { get; init; }

    /// <summary>
    /// Test cases that were skipped as duplicates.
    /// </summary>
    public IReadOnlyList<string> SkippedDuplicates { get; init; } = [];

    /// <summary>
    /// Any errors that occurred during generation.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Token usage information.
    /// </summary>
    public TokenUsage? TokenUsage { get; init; }

    /// <summary>
    /// Coverage gaps remaining after generation.
    /// </summary>
    public IReadOnlyList<Core.Models.CoverageGap> CoverageGapsRemaining { get; init; } = [];

    /// <summary>
    /// Whether generation was successful.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;
}

