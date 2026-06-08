using System.Text.Json;
using Spectra.CLI.Agent.Analysis;
using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.CLI.Generation;

/// <summary>
/// Spec 059: deterministic, model-free classifier that turns an agent's behavior-analysis JSON
/// into an <see cref="AnalysisRecommendation"/>. The parse helpers and accounting are relocated
/// (copied) verbatim from <c>BehaviorAnalyzer</c> so this class is self-contained — it never
/// references the to-be-deleted in-process analyzer. Fail-loud: empty content → EmptyResponse,
/// parsed-to-zero → ParseFailure.
/// </summary>
public static class AnalysisRecommendationBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Classifies the agent content into a recommendation. Coverage dedup uses the
    /// <paramref name="snapshot"/>'s accurate count when available, else a title-similarity
    /// heuristic against <paramref name="existingTests"/>.
    /// </summary>
    public static AnalysisRecommendation Build(
        string agentContent,
        IReadOnlyList<TestCase> existingTests,
        CoverageSnapshot? snapshot,
        string? focusArea)
    {
        if (string.IsNullOrWhiteSpace(agentContent))
            return AnalysisRecommendation.Empty("Agent analysis content was empty.");

        var behaviors = ParseAnalysisResponse(agentContent);
        if (behaviors is null || behaviors.Count == 0)
            return AnalysisRecommendation.ParseFail(
                "Could not parse any behaviors from the agent analysis content.");

        // Apply focus filter if specified
        if (!string.IsNullOrWhiteSpace(focusArea))
        {
            behaviors = FilterByFocus(behaviors, focusArea);
        }

        // Compute dedup: use snapshot's accurate count when available,
        // fall back to title-similarity heuristic otherwise.
        var coveredCount = snapshot is not null && snapshot.HasData
            ? snapshot.ExistingTestCount
            : CountCoveredBehaviors(behaviors, existingTests);

        // Build breakdown — bucket empty/null/whitespace categories under "uncategorized".
        var breakdown = behaviors
            .GroupBy(b => string.IsNullOrWhiteSpace(b.Category) ? "uncategorized" : b.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        // Build technique breakdown — exclude empty techniques so legacy AI
        // responses produce an empty map rather than a "" → N entry.
        var techniqueBreakdown = behaviors
            .Where(b => !string.IsNullOrWhiteSpace(b.Technique))
            .GroupBy(b => b.Technique.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var documentsAnalyzed = behaviors
            .Select(b => b.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return AnalysisRecommendation.Recommendation(
            totalBehaviors: behaviors.Count,
            alreadyCovered: coveredCount,
            breakdown: breakdown,
            techniqueBreakdown: techniqueBreakdown,
            documentsAnalyzed: documentsAnalyzed);
    }

    // ---- Parse helpers (copied verbatim from BehaviorAnalyzer; self-contained, model-free) ----

    internal static List<IdentifiedBehavior>? ParseAnalysisResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        // Try to extract JSON from the response (may be wrapped in markdown code blocks)
        var json = ExtractJson(responseText);
        if (json is null)
            return null;

        // Strict pass: try the response as-is.
        try
        {
            using var doc = JsonDocument.Parse(json);

            // Try parsing as direct array [...] first
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<IdentifiedBehavior>>(json, JsonOptions);
            }

            // Try parsing as {"behaviors": [...]}
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("behaviors", out var behaviorsElement))
            {
                return JsonSerializer.Deserialize<List<IdentifiedBehavior>>(
                    behaviorsElement.GetRawText(), JsonOptions);
            }
        }
        catch
        {
            // Fall through to tolerant pass below.
        }

        // Tolerant pass: handle truncated / malformed responses by walking the
        // string and grabbing every balanced {...} object inside the first
        // top-level array. Returns whatever objects parsed cleanly, even if the
        // tail of the response is garbage. Returns null only when zero objects
        // could be recovered.
        var recovered = ParseTolerant(responseText);
        return recovered.Count > 0 ? recovered : null;
    }

    /// <summary>
    /// Tolerant parser: walks the response text char-by-char tracking string
    /// state and brace depth, yielding every balanced top-level <c>{...}</c>
    /// object that appears inside the first array. Each object is parsed
    /// independently — partial objects at the end of a truncated response
    /// are silently skipped.
    /// </summary>
    internal static List<IdentifiedBehavior> ParseTolerant(string responseText)
    {
        var results = new List<IdentifiedBehavior>();
        if (string.IsNullOrEmpty(responseText)) return results;

        // Locate the start of the behaviors array. Prefer the array right
        // after the "behaviors" key; otherwise fall back to the first '['.
        var arrayStart = -1;
        var behaviorsKeyIdx = responseText.IndexOf("\"behaviors\"", StringComparison.OrdinalIgnoreCase);
        if (behaviorsKeyIdx >= 0)
        {
            arrayStart = responseText.IndexOf('[', behaviorsKeyIdx);
        }
        if (arrayStart < 0)
        {
            arrayStart = responseText.IndexOf('[');
        }
        if (arrayStart < 0) return results;

        var depth = 0;
        var objStart = -1;
        var inString = false;
        var escape = false;

        for (var i = arrayStart + 1; i < responseText.Length; i++)
        {
            var c = responseText[i];

            if (escape)
            {
                escape = false;
                continue;
            }
            if (inString)
            {
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = false; }
                continue;
            }
            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                if (depth == 0) objStart = i;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && objStart >= 0)
                {
                    var objJson = responseText.Substring(objStart, i - objStart + 1);
                    try
                    {
                        var behavior = JsonSerializer.Deserialize<IdentifiedBehavior>(objJson, JsonOptions);
                        if (behavior is not null) results.Add(behavior);
                    }
                    catch
                    {
                        // Skip malformed object; keep walking.
                    }
                    objStart = -1;
                }
                else if (depth < 0)
                {
                    // Stray '}' — clamp and continue.
                    depth = 0;
                }
            }
        }

        return results;
    }

    private static string? ExtractJson(string text)
    {
        // Try to find JSON in code blocks first
        var codeBlockStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (codeBlockStart >= 0)
        {
            var contentStart = text.IndexOf('\n', codeBlockStart) + 1;
            var codeBlockEnd = text.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (codeBlockEnd > contentStart)
            {
                return text[contentStart..codeBlockEnd].Trim();
            }
        }

        // Try to find raw JSON object or array
        var firstBrace = text.IndexOf('{');
        var firstBracket = text.IndexOf('[');

        if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
        {
            var lastBrace = text.LastIndexOf('}');
            if (lastBrace > firstBrace)
                return text[firstBrace..(lastBrace + 1)];
        }

        if (firstBracket >= 0)
        {
            var lastBracket = text.LastIndexOf(']');
            if (lastBracket > firstBracket)
                return text[firstBracket..(lastBracket + 1)];
        }

        return null;
    }

    internal static List<IdentifiedBehavior> FilterByFocus(
        List<IdentifiedBehavior> behaviors, string focusArea)
    {
        var tokens = focusArea.ToLowerInvariant()
            .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return behaviors;

        var matches = behaviors.Where(b =>
        {
            var normalized = (b.Category ?? "").ToLowerInvariant()
                .Replace('_', ' ').Replace('-', ' ');
            return tokens.Any(t => normalized.Contains(t));
        }).ToList();

        // No category match — return all (focus will be applied to generation prompt instead).
        return matches.Count > 0 ? matches : behaviors;
    }

    internal static int CountCoveredBehaviors(
        IReadOnlyList<IdentifiedBehavior> behaviors,
        IReadOnlyList<TestCase> existingTests)
    {
        if (existingTests.Count == 0)
            return 0;

        var detector = new DuplicateDetector();
        var coveredCount = 0;

        foreach (var behavior in behaviors)
        {
            // Check title similarity directly (behaviors don't have steps).
            var isCovered = existingTests.Any(existing =>
                detector.CalculateTitleSimilarity(behavior.Title, existing.Title) >= 0.6);

            if (isCovered)
            {
                coveredCount++;
            }
        }

        return coveredCount;
    }
}
