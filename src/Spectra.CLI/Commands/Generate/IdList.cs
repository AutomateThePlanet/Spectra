namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 091 (FR-006) / Spec 093 (FR-017): the <c>--ids a,b</c> option of the batch ingests (<c>ingest-grounding --all</c>,
/// <c>ingest-verdict --all</c>) — one parsing rule and one usage message for both.
/// </summary>
internal static class IdList
{
    /// <summary>Usage error (exit 1, plain stderr) for <c>--ids</c> without <c>--all</c>.</summary>
    public const string IdsWithoutAllMessage = "--ids is only valid with --all.";

    /// <summary>Split on commas, trimmed, empties dropped, de-duplicated case-insensitively (first spelling kept).</summary>
    public static IReadOnlyList<string> Parse(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
