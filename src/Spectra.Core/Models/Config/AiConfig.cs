using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for AI providers.
/// </summary>
public sealed class AiConfig
{
    [JsonPropertyName("providers")]
    public required IReadOnlyList<ProviderConfig> Providers { get; init; }

    [JsonPropertyName("fallback_strategy")]
    public string FallbackStrategy { get; init; } = "auto";

    /// <summary>
    /// Configuration for the grounding verification critic.
    /// When enabled, generated tests are verified against source documentation.
    /// </summary>
    [JsonPropertyName("critic")]
    public CriticConfig? Critic { get; init; }

    /// <summary>
    /// Per-batch timeout for the AI generation SDK call, in minutes. Default 5.
    /// Slower / larger models (e.g. reasoning models, large Azure deployments)
    /// may need to bump this to 10–20+ minutes. The timer measures the entire
    /// batch round-trip including all tool calls the AI makes.
    /// </summary>
    [JsonPropertyName("generation_timeout_minutes")]
    public int GenerationTimeoutMinutes { get; init; } = 5;

    /// <summary>
    /// Timeout for the behavior analysis SDK call (the analyze step that
    /// runs before generation), in minutes. Default 2. Slower / reasoning
    /// models routinely overshoot 2 minutes when scanning a multi-document
    /// suite — bump to 5–10 minutes for those. The same timer applies to
    /// the retry attempt.
    /// </summary>
    [JsonPropertyName("analysis_timeout_minutes")]
    public int AnalysisTimeoutMinutes { get; init; } = 2;

    /// <summary>
    /// Number of tests requested per AI call. Default 30. Smaller batches
    /// reduce per-batch latency on slow models at the cost of more total
    /// round-trips. Pair with <see cref="GenerationTimeoutMinutes"/>.
    /// </summary>
    [JsonPropertyName("generation_batch_size")]
    public int GenerationBatchSize { get; init; } = 30;

    /// <summary>
    /// Append per-batch diagnostics to <c>.spectra-debug.log</c> in the
    /// project root. Default true. Useful for diagnosing slow models and
    /// timeout issues. Set false to silence.
    /// </summary>
    [JsonPropertyName("debug_log_enabled")]
    public bool DebugLogEnabled { get; init; } = true;
}
