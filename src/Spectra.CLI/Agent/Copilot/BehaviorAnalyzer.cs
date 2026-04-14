using System.Text;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Prompts;
using Spectra.CLI.Services;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Testimize;
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
    private readonly SpectraConfig? _config;
    private readonly PromptTemplateLoader? _templateLoader;
    private readonly TokenUsageTracker? _tracker;
    private readonly RunErrorTracker? _errorTracker;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BehaviorAnalyzer(
        SpectraProviderConfig? provider,
        Action<string>? onStatus = null,
        SpectraConfig? config = null,
        PromptTemplateLoader? templateLoader = null,
        TokenUsageTracker? tracker = null,
        RunErrorTracker? errorTracker = null)
    {
        _provider = provider;
        _onStatus = onStatus;
        _config = config;
        _templateLoader = templateLoader;
        _tracker = tracker;
        _errorTracker = errorTracker;
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
        CancellationToken ct = default,
        CoverageSnapshot? snapshot = null)
    {
        if (documents.Count == 0)
            return null;

        // Spec post-038 fix: configurable analysis timeout (default 2 min).
        // Slower / reasoning models routinely overshoot 2 minutes when scanning
        // a multi-document suite. Read from ai.analysis_timeout_minutes.
        var timeoutMinutes = Math.Max(1, _config?.Ai.AnalysisTimeoutMinutes ?? 2);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var modelName = _provider?.Model ?? "?";
        var providerName = _provider?.Name ?? "?";
        // Spec 040 follow-up: observer subscribes to AssistantUsageEvent for
        // provider-reported token counts. If no usage event arrives within
        // the grace window, falls back to TokenEstimator(prompt, response).
        var observer = new CopilotUsageObserver();
        var coverageContext = snapshot is not null && snapshot.HasData
            ? CoverageContextFormatter.Format(snapshot)
            : null;
        var prompt = BuildAnalysisPrompt(documents, focusArea, _config, _templateLoader, coverageContext);
        DebugLogAi($"ANALYSIS START documents={documents.Count} timeout={timeoutMinutes}min", null, null, estimated: false);

        try
        {
            _onStatus?.Invoke("Connecting to AI for behavior analysis...");
            var service = await CopilotService.GetInstanceAsync(ct);

            await using var session = await service.CreateGenerationSessionAsync(
                _provider,
                tools: null,
                ct);

            using var usageSub = session.On(evt =>
            {
                if (evt is AssistantUsageEvent u && u.Data is { } data)
                {
                    observer.RecordUsage(
                        (int)(data.InputTokens ?? 0),
                        (int)(data.OutputTokens ?? 0));
                }
            });

            _onStatus?.Invoke($"Analyzing {documents.Count} documents (timeout {timeoutMinutes} min)...");

            // Schedule delayed message updates, cancelled when AI call completes
            using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var timerToken = timerCts.Token;
            _ = Task.Delay(5000, timerToken).ContinueWith(_ =>
                _onStatus?.Invoke("AI is identifying testable behaviors — this may take a few minutes..."), TaskContinuationOptions.OnlyOnRanToCompletion);
            _ = Task.Delay(20000, timerToken).ContinueWith(_ =>
                _onStatus?.Invoke("Still analyzing — categorizing behaviors by type (happy path, negative, edge case)..."), TaskContinuationOptions.OnlyOnRanToCompletion);
            _ = Task.Delay(40000, timerToken).ContinueWith(_ =>
                _onStatus?.Invoke("Almost done — computing recommended test count..."), TaskContinuationOptions.OnlyOnRanToCompletion);

            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = prompt },
                timeout: TimeSpan.FromMinutes(timeoutMinutes),
                cancellationToken: ct);

            // Cancel delayed timers so they don't overwrite the final result
            await timerCts.CancelAsync();

            // Grace window for AssistantUsageEvent ordering — SDK makes no
            // documented guarantee that it fires before SessionIdleEvent.
            await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(200), ct);

            var responseText = response?.Data?.Content ?? "";
            var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(prompt, responseText);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                _onStatus?.Invoke("AI returned empty response for behavior analysis");
                _tracker?.Record("analysis", modelName, providerName, tokensIn, tokensOut, sw.Elapsed, estimated);
                DebugLogAi($"ANALYSIS EMPTY response_chars=0 elapsed={sw.Elapsed.TotalSeconds:F1}s", tokensIn, tokensOut, estimated);
                return null;
            }

            var behaviors = ParseAnalysisResponse(responseText);
            var fieldSpecs = ParseFieldSpecs(responseText);
            if (behaviors is null || behaviors.Count == 0)
            {
                _onStatus?.Invoke("Could not parse behaviors from AI response");
                _tracker?.Record("analysis", modelName, providerName, tokensIn, tokensOut, sw.Elapsed, estimated);
                DebugLogAi($"ANALYSIS PARSE_FAIL response_chars={responseText.Length} elapsed={sw.Elapsed.TotalSeconds:F1}s", tokensIn, tokensOut, estimated);
                return null;
            }

            sw.Stop();
            _tracker?.Record("analysis", modelName, providerName, tokensIn, tokensOut, sw.Elapsed, estimated);
            DebugLogAi($"ANALYSIS OK behaviors={behaviors.Count} response_chars={responseText.Length} elapsed={sw.Elapsed.TotalSeconds:F1}s", tokensIn, tokensOut, estimated);
            _onStatus?.Invoke($"Found {behaviors.Count} testable behaviors");

            // Apply focus filter if specified
            if (!string.IsNullOrWhiteSpace(focusArea))
            {
                behaviors = FilterByFocus(behaviors, focusArea);
            }

            // Compute dedup: use snapshot's accurate count when available,
            // fall back to title-similarity heuristic otherwise
            var coveredCount = snapshot is not null && snapshot.HasData
                ? snapshot.ExistingTestCount
                : CountCoveredBehaviors(behaviors, existingTests);

            // Build breakdown — bucket empty/null/whitespace categories under "uncategorized"
            var breakdown = behaviors
                .GroupBy(b => string.IsNullOrWhiteSpace(b.Category) ? "uncategorized" : b.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            // Build technique breakdown — exclude empty techniques so legacy AI
            // responses produce an empty map rather than a "" → N entry.
            var techniqueBreakdown = behaviors
                .Where(b => !string.IsNullOrWhiteSpace(b.Technique))
                .GroupBy(b => b.Technique.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Count());

            var totalWords = documents.Sum(d =>
                d.Content.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length);

            return new BehaviorAnalysisResult
            {
                TotalBehaviors = behaviors.Count,
                Breakdown = breakdown,
                TechniqueBreakdown = techniqueBreakdown,
                Behaviors = behaviors,
                AlreadyCovered = coveredCount,
                DocumentsAnalyzed = documents.Count,
                TotalWords = totalWords,
                FieldSpecs = fieldSpecs
            };
        }
        catch (TimeoutException ex)
        {
            // Best-effort: record whatever usage the observer captured before
            // the timeout. If none, fall back to prompt-length estimate with
            // zero completion tokens.
            var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(prompt, "");
            _tracker?.Record("analysis", modelName, providerName, tokensIn, tokensOut, sw.Elapsed, estimated);
            // Spec 043: capture full timeout context.
            _errorTracker?.RecordError();
            Spectra.CLI.Infrastructure.ErrorLogger.Write(
                "analyze",
                $"configured_timeout={timeoutMinutes}min elapsed={sw.Elapsed.TotalSeconds:F1}s",
                ex);
            DebugLogAi($"ANALYSIS TIMEOUT configured_timeout={timeoutMinutes}min elapsed={sw.Elapsed.TotalSeconds:F1}s"
                + (Spectra.CLI.Infrastructure.ErrorLogger.Enabled ? $" see={Spectra.CLI.Infrastructure.ErrorLogger.LogFile}" : ""),
                tokensIn, tokensOut, estimated);
            _onStatus?.Invoke($"Behavior analysis timed out after {timeoutMinutes} min (model: {modelName}). Bump ai.analysis_timeout_minutes in spectra.config.json. Using default count.");
            return null;
        }
        catch (Exception ex)
        {
            var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(prompt, "");
            _tracker?.Record("analysis", modelName, providerName, tokensIn, tokensOut, sw.Elapsed, estimated);
            // Spec 043: capture full exception context to the error log.
            _errorTracker?.Record(ex);
            Spectra.CLI.Infrastructure.ErrorLogger.Write(
                "analyze", $"elapsed={sw.Elapsed.TotalSeconds:F1}s", ex);
            DebugLogAi($"ANALYSIS ERROR exception={ex.GetType().Name} message=\"{ex.Message}\" elapsed={sw.Elapsed.TotalSeconds:F1}s"
                + (Spectra.CLI.Infrastructure.ErrorLogger.Enabled ? $" see={Spectra.CLI.Infrastructure.ErrorLogger.LogFile}" : ""),
                tokensIn, tokensOut, estimated);
            _onStatus?.Invoke($"Behavior analysis failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Wrapper around <see cref="Spectra.CLI.Infrastructure.DebugLogger.AppendAi"/>
    /// for analysis AI lines. Enabled flag is set once from the handler.
    /// </summary>
    private void DebugLogAi(string message, int? tokensIn, int? tokensOut, bool estimated = false) =>
        Spectra.CLI.Infrastructure.DebugLogger.AppendAi(
            "analyze ",
            message,
            _provider?.Model,
            _provider?.Name,
            tokensIn,
            tokensOut,
            estimated);

    internal static string BuildAnalysisPrompt(
        IReadOnlyList<SourceDocument> documents,
        string? focusArea,
        SpectraConfig? config = null,
        PromptTemplateLoader? templateLoader = null,
        string? coverageContext = null)
    {
        // Try template-driven prompt
        if (templateLoader is not null)
        {
            var template = templateLoader.LoadTemplate("behavior-analysis");
            var categories = PromptTemplateLoader.GetCategories(config);

            var docText = FormatDocuments(documents);
            var values = new Dictionary<string, string>
            {
                // Spec 038: gates the {{#if testimize_enabled}} block in
                // behavior-analysis.md. Empty string is falsy.
                ["testimize_enabled"] = (config?.Testimize.Enabled ?? false) ? "true" : "",
                ["document_text"] = docText,
                ["document_title"] = string.Join(", ", documents.Select(d => d.Title ?? d.Path)),
                ["suite_name"] = "",
                ["existing_tests"] = "",
                ["focus_areas"] = focusArea ?? "",
                ["acceptance_criteria"] = "",
                // Spec 044: coverage-aware analysis context
                ["coverage_context"] = coverageContext ?? ""
            };

            var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["categories"] = PromptTemplateLoader.FormatCategoriesForTemplate(categories)
            };

            return PromptTemplateLoader.Resolve(template, values, listValues);
        }

        // Legacy fallback (used when no PromptTemplateLoader is wired up).
        // Mirrors the ISTQB-enhanced template in condensed form.
        var sb = new StringBuilder();
        sb.AppendLine("""
            Analyze the following documentation and identify all distinct testable behaviors.

            Apply these test design techniques systematically:
            - Equivalence Partitioning (EP): for every input, identify valid and invalid classes
            - Boundary Value Analysis (BVA): for every range, test at min, max, min-1, max+1
            - Decision Table (DT): for business rules with multiple conditions
            - State Transition (ST): for workflows, test valid and invalid transitions
            - Error Guessing (EG): common defects (null, overflow, special chars, divide-by-zero)
            - Use Case (UC): main success + alternate + exception paths

            Do NOT generate more than 40% of behaviors in any single category.

            Categorize each behavior into one of these categories:
            - happy_path: Normal successful user flows
            - negative: Error handling, invalid inputs, failure scenarios
            - edge_case: Boundary conditions, unusual combinations, limits
            - boundary: Values at exact limits (min, max, min-1, max+1)
            - error_handling: System error responses, recovery, logging
            - security: Permission checks, access control, authentication

            For each behavior, provide:
            - category: one of the categories above
            - title: short description (max 80 chars)
            - source: which document it comes from
            - technique: EP, BVA, DT, ST, EG, or UC

            Return ONLY a JSON object in this exact format (no other text):

            {"behaviors": [{"category": "boundary", "title": "...", "source": "...", "technique": "BVA"}]}

            Count only DISTINCT testable behaviors — do not duplicate similar scenarios.
            """);

        if (!string.IsNullOrWhiteSpace(focusArea))
        {
            sb.AppendLine($"\nFocus area: {focusArea}");
        }

        sb.AppendLine("\nDocumentation:");
        sb.Append(FormatDocuments(documents));

        return sb.ToString();
    }

    private static string FormatDocuments(IReadOnlyList<SourceDocument> documents)
    {
        var sb = new StringBuilder();
        foreach (var doc in documents)
        {
            sb.AppendLine($"\n### {doc.Title} ({doc.Path})");
            if (doc.Sections.Count > 0)
            {
                sb.AppendLine($"Sections: {string.Join(", ", doc.Sections)}");
            }

            var preview = doc.Content.Length > 2000
                ? doc.Content[..2000] + "..."
                : doc.Content;
            sb.AppendLine(preview);
        }
        return sb.ToString();
    }

    /// <summary>
    /// v1.48.3: parses the optional top-level <c>field_specs</c> array from
    /// the behavior-analysis AI response. Gated by the
    /// <c>{{#if testimize_enabled}}</c> block in
    /// <c>behavior-analysis.md</c>. Returns null if missing or unparseable —
    /// <see cref="Spectra.CLI.Agent.Testimize.TestimizeRunner"/> will fall
    /// back to the regex extractor when this is null or empty.
    /// </summary>
    internal static List<FieldSpec>? ParseFieldSpecs(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return null;

        var json = ExtractJson(responseText);
        if (json is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("field_specs", out var fsEl) &&
                !doc.RootElement.TryGetProperty("fieldSpecs", out fsEl))
                return null;

            var specs = JsonSerializer.Deserialize<List<FieldSpec>>(fsEl.GetRawText(), JsonOptions);
            return specs is { Count: > 0 } ? specs : null;
        }
        catch
        {
            return null;
        }
    }

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
        // top-level array. Slow / reasoning models (DeepSeek-V3, o1-class)
        // routinely hit their max output token limit mid-stream and produce
        // a response that's mostly valid JSON but missing the closing braces.
        // This pass returns whatever objects parsed cleanly, even if the tail
        // of the response is garbage. Returns null only when zero objects
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

        // No category match — return all (focus will be applied to generation prompt instead)
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
