using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for AI providers.
/// </summary>
public sealed class AiConfig
{
    // Spec 069: `ai.providers` and `ai.critic` were removed — SPECTRA no longer runs an in-process
    // model (all inference is the user's Claude Code session), so the config carries no provider or
    // critic block. Legacy configs that still carry those keys deserialize cleanly (unmapped members
    // are ignored). Only the surviving cost/telemetry levers remain below.

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
}
