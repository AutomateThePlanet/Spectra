using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for behavior analysis settings.
/// </summary>
public sealed class AnalysisConfig
{
    [JsonPropertyName("categories")]
    public IReadOnlyList<CategoryDefinition> Categories { get; init; } = [];

    /// <summary>
    /// Pre-flight token budget for the behavior-analysis prompt
    /// (Spec 040 §3.7). When the estimated prompt size exceeds this value,
    /// the command fails fast with an actionable error rather than letting
    /// the model overflow its 128K context window. Default 96,000 leaves
    /// 32K for response + prompt template + ancillary content. Set to 0
    /// to disable the check.
    /// </summary>
    [JsonPropertyName("max_prompt_tokens")]
    public int MaxPromptTokens { get; init; } = 96_000;
}
