namespace Spectra.CLI.Services;

/// <summary>
/// Rough token-count estimate using the <c>text.Length / 4</c> heuristic
/// (Spec 040 follow-up). Used as a fallback when the Copilot SDK's
/// <c>AssistantUsageEvent</c> does not arrive within the grace window after
/// a request — see <see cref="CopilotUsageObserver"/>.
///
/// Accuracy: good enough for cost ballparking on English GPT-family prompts
/// (cl100k / o200k tokenizers average ~4 characters per token). Can be off
/// by 20–40% on code-heavy, non-English, or structurally unusual text.
/// Callers mark fallback-estimated values with a <c>~</c> prefix in the
/// debug log and <c>"estimated": true</c> in JSON output so consumers know
/// the numbers are approximate.
/// </summary>
public static class TokenEstimator
{
    /// <summary>
    /// Returns an approximate token count for <paramref name="text"/>, or 0
    /// for null/empty input. Uses integer truncation — 3-character strings
    /// round down to 0 tokens, which is fine for our ballpark use case.
    /// </summary>
    public static int Estimate(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
}
