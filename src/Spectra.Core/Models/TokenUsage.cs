namespace Spectra.Core.Models;

/// <summary>
/// Token usage returned by an AI completion call (Spec 040).
/// Field names mirror the OpenAI-style <c>usage</c> object.
/// </summary>
public sealed record TokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}
