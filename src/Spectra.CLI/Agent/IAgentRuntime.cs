using Spectra.Core.Models;

namespace Spectra.CLI.Agent;

/// <summary>
/// Defines the contract for AI agent runtimes.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Generates test cases from documentation.
    /// </summary>
    /// <param name="prompt">The prompt with context and instructions</param>
    /// <param name="documentMap">Map of source documentation</param>
    /// <param name="existingTests">Existing tests for deduplication</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated test cases</returns>
    Task<GenerationResult> GenerateTestsAsync(
        string prompt,
        DocumentMap documentMap,
        IReadOnlyList<TestCase> existingTests,
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
    /// Whether generation was successful.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;
}

/// <summary>
/// Token usage statistics.
/// </summary>
public sealed record TokenUsage(int InputTokens, int OutputTokens, int TotalTokens);
