using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Spec 097 (FR-020): the optional <c>ai.critic</c> block — which model the <c>spectra-critic</c> subagent runs on.
/// <c>init</c> and <c>update-skills</c> write it into the installed agent's frontmatter; Claude Code reads it from
/// there. The keys of the removed in-process critic (<c>enabled</c>, <c>timeout_seconds</c>, <c>max_concurrent</c>, …)
/// are no longer modelled and are ignored; the retired provider keys keep their notice (<c>ConfigLoader.DetectDeprecatedKeys</c>).
/// </summary>
public sealed class CriticConfig
{
    /// <summary>The model when <c>ai.critic.model</c> (or <c>ai.critic</c>, or <c>ai</c>) is missing.</summary>
    public const string DefaultModel = "sonnet";

    /// <summary>
    /// An alias (<c>sonnet</c>, <c>opus</c>, <c>haiku</c>), a model id, or <c>inherit</c>: one token that starts with a
    /// letter or digit (validated by <c>ConfigLoader</c>), so the unquoted frontmatter value is always a YAML string.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }
}
