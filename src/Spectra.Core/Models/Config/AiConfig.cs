using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for AI providers.
/// </summary>
public sealed class AiConfig
{
    // Spec 069: `ai.providers` and the in-process critic were removed — SPECTRA no longer runs an in-process
    // model (all inference is the user's Claude Code session), so the config carries no provider block.
    // Legacy configs that still carry those keys deserialize cleanly (unmapped members are ignored).
    // Spec 097: `ai.critic.model` is back, as the model the spectra-critic subagent runs on.

    /// <summary>Spec 097 (FR-020): the optional <c>ai.critic</c> block.</summary>
    [JsonPropertyName("critic")]
    public CriticConfig? Critic { get; init; }

    /// <summary>Spec 097: the critic's model — <c>ai.critic.model</c>, or <see cref="CriticConfig.DefaultModel"/>.</summary>
    [JsonIgnore]
    public string EffectiveCriticModel => Critic?.Model ?? CriticConfig.DefaultModel;

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
    /// Read by nothing since Spec 059 removed the in-process generator. Spec 096 (FR-013): kept only so an existing
    /// config that still has <c>ai.generation_batch_size</c> loads (no section or key is ever required — Spec 083);
    /// <c>spectra init</c> no longer writes it. The generation batch size is <c>generation.batch_size</c> (Spec 094).
    /// </summary>
    [Obsolete("Ignored. The generation batch size is generation.batch_size (Spec 094).")]
    [JsonPropertyName("generation_batch_size")]
    public int GenerationBatchSize { get; init; } = 30;
}
