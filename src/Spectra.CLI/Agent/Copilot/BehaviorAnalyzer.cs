using System.Text;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Spectra.CLI.Agent.Analysis;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Validation;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Performs AI-powered analysis of source documentation to identify
/// and categorize testable behaviors before test generation.
/// </summary>
public sealed class BehaviorAnalyzer
{
    private readonly SpectraProviderConfig? _provider;
    private readonly Action<string>? _onStatus;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BehaviorAnalyzer(SpectraProviderConfig? provider, Action<string>? onStatus = null)
    {
        _provider = provider;
        _onStatus = onStatus;
    }

    /// <summary>
    /// Analyzes source documentation to identify testable behaviors, then deduplicates
    /// against existing tests to compute a recommended count.
    /// </summary>
    /// <returns>Analysis result, or null if analysis fails.</returns>
    public async Task<BehaviorAnalysisResult?> AnalyzeAsync(
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<TestCase> existingTests,
        string? focusArea,
        CancellationToken ct = default)
    {
        if (documents.Count == 0)
            return null;

        try
        {
            _onStatus?.Invoke("Connecting to AI for behavior analysis...");
            var service = await CopilotService.GetInstanceAsync(ct);

            await using var session = await service.CreateGenerationSessionAsync(
                _provider,
                tools: null,
                ct);

            var prompt = BuildAnalysisPrompt(documents, focusArea);

            _onStatus?.Invoke($"Analyzing {documents.Count} documents for testable behaviors...");
            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = prompt },
                timeout: TimeSpan.FromMinutes(2),
                cancellationToken: ct);

            var responseText = response?.Data?.Content ?? "";

            if (string.IsNullOrWhiteSpace(responseText))
            {
                _onStatus?.Invoke("AI returned empty response for behavior analysis");
                return null;
            }

            var behaviors = ParseAnalysisResponse(responseText);
            if (behaviors is null || behaviors.Count == 0)
            {
                _onStatus?.Invoke("Could not parse behaviors from AI response");
                // Save debug response
                try
                {
                    var debugPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-debug-analysis.txt");
                    File.WriteAllText(debugPath, responseText);
                }
                catch { /* non-critical */ }
                return null;
            }

            _onStatus?.Invoke($"Found {behaviors.Count} testable behaviors");

            // Apply focus filter if specified
            if (!string.IsNullOrWhiteSpace(focusArea))
            {
                behaviors = FilterByFocus(behaviors, focusArea);
            }

            // Compute dedup against existing tests
            var coveredCount = CountCoveredBehaviors(behaviors, existingTests);

            // Build breakdown
            var breakdown = behaviors
                .GroupBy(b => b.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            var totalWords = documents.Sum(d =>
                d.Content.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length);

            return new BehaviorAnalysisResult
            {
                TotalBehaviors = behaviors.Count,
                Breakdown = breakdown,
                Behaviors = behaviors,
                AlreadyCovered = coveredCount,
                DocumentsAnalyzed = documents.Count,
                TotalWords = totalWords
            };
        }
        catch (TimeoutException)
        {
            _onStatus?.Invoke("Behavior analysis timed out (2 min). Using default count.");
            return null;
        }
        catch (Exception ex)
        {
            _onStatus?.Invoke($"Behavior analysis failed: {ex.Message}");
            return null;
        }
    }

    internal static string BuildAnalysisPrompt(
        IReadOnlyList<SourceDocument> documents,
        string? focusArea)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            Analyze the following documentation and identify all distinct testable behaviors.
            Categorize each behavior into one of these categories:

            - happy_path: Normal successful user flows
            - negative: Error handling, invalid inputs, failure scenarios
            - edge_case: Boundary conditions, unusual combinations, limits
            - security: Permission checks, access control, authentication
            - performance: Load, timeout, concurrent access (if mentioned)

            For each behavior, provide:
            - category: one of the categories above
            - title: short description (max 80 chars)
            - source: which document it comes from

            Return ONLY a JSON object in this exact format (no other text):

            {"behaviors": [{"category": "happy_path", "title": "...", "source": "..."}]}

            Count only DISTINCT testable behaviors — do not duplicate similar scenarios.
            """);

        if (!string.IsNullOrWhiteSpace(focusArea))
        {
            sb.AppendLine($"\nFocus area: {focusArea}");
        }

        sb.AppendLine("\nDocumentation:");
        foreach (var doc in documents)
        {
            sb.AppendLine($"\n### {doc.Title} ({doc.Path})");
            // Use document summaries: sections + first 200 chars per section
            if (doc.Sections.Count > 0)
            {
                sb.AppendLine($"Sections: {string.Join(", ", doc.Sections)}");
            }

            // Truncate content to ~200 chars per section or 2000 chars total
            var preview = doc.Content.Length > 2000
                ? doc.Content[..2000] + "..."
                : doc.Content;
            sb.AppendLine(preview);
        }

        return sb.ToString();
    }

    internal static List<IdentifiedBehavior>? ParseAnalysisResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        // Try to extract JSON from the response (may be wrapped in markdown code blocks)
        var json = ExtractJson(responseText);
        if (json is null)
            return null;

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

            return null;
        }
        catch
        {
            return null;
        }
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
        var lower = focusArea.ToLowerInvariant();

        // Map focus terms to categories
        var matchingCategories = new List<BehaviorCategory>();
        if (lower.Contains("happy") || lower.Contains("positive") || lower.Contains("success"))
            matchingCategories.Add(BehaviorCategory.HappyPath);
        if (lower.Contains("negative") || lower.Contains("error") || lower.Contains("fail"))
            matchingCategories.Add(BehaviorCategory.Negative);
        if (lower.Contains("edge") || lower.Contains("boundary") || lower.Contains("limit"))
            matchingCategories.Add(BehaviorCategory.EdgeCase);
        if (lower.Contains("sec") || lower.Contains("permission") || lower.Contains("auth"))
            matchingCategories.Add(BehaviorCategory.Security);
        if (lower.Contains("perf") || lower.Contains("load") || lower.Contains("timeout"))
            matchingCategories.Add(BehaviorCategory.Performance);

        if (matchingCategories.Count > 0)
        {
            return behaviors.Where(b => matchingCategories.Contains(b.Category)).ToList();
        }

        // No category match — return all (focus will be applied to generation prompt instead)
        return behaviors;
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
            // Check title similarity directly (behaviors don't have steps)
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
