using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.CLI.Services;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// AI agent for test generation using the Copilot SDK with an agent loop.
/// Implements IAgentRuntime for compatibility with existing code.
/// </summary>
public sealed class CopilotGenerationAgent : IAgentRuntime
{
    private readonly SpectraProviderConfig _provider;
    private readonly SpectraConfig _config;
    private readonly string _basePath;
    private readonly string _testsPath;
    private readonly Action<string>? _onStatus;
    private readonly TokenUsageTracker? _tracker;
    private readonly RunErrorTracker? _errorTracker;

    public string ProviderName => $"copilot-sdk ({_provider?.Name ?? "github-models"})";

    public CopilotGenerationAgent(
        SpectraProviderConfig provider,
        SpectraConfig config,
        string basePath,
        string testsPath,
        Action<string>? onStatus = null,
        TokenUsageTracker? tracker = null,
        RunErrorTracker? errorTracker = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _testsPath = testsPath ?? throw new ArgumentNullException(nameof(testsPath));
        _onStatus = onStatus;
        _tracker = tracker;
        _errorTracker = errorTracker;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // For BYOK providers, only check CLI presence (no Copilot auth needed)
            var isByok = _provider?.Name?.ToLowerInvariant() is not (null or "" or "github-models" or "github-copilot" or "copilot");
            if (isByok)
            {
                var (cliAvailable, _) = CopilotService.CheckCliAvailable();
                if (!cliAvailable) return false;
            }
            else
            {
                var (available, _) = await CopilotService.CheckAvailabilityAsync(ct);
                if (!available) return false;
            }

            var (valid, _) = CopilotService.ValidateProvider(_provider);
            return valid;
        }
        catch
        {
            return false;
        }
    }

    public async Task<GenerationResult> GenerateTestsAsync(
        string prompt,
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<TestCase> existingTests,
        int requestedCount,
        string? criteriaContext = null,
        Spectra.Core.Models.Testimize.TestimizeDataset? testimizeData = null,
        CancellationToken ct = default)
    {
        try
        {
            var service = await CopilotService.GetInstanceAsync(ct);

            // Spec 040: ID uniqueness is enforced by the cross-process
            // PersistentTestIdAllocator (file lock + HWM + filesystem scan)
            // inside TestIndexTools.Create — no need to seed from
            // existingTests here.

            // Create tools for the agent
            var documentTools = DocumentTools.Create(_basePath, _config);
            var testIndexTools = TestIndexTools.Create(
                _basePath,
                _testsPath,
                _config,
                existingTests);

            var tools = documentTools.CreateFunctions()
                .Concat(testIndexTools.CreateFunctions())
                .ToList();

            // v1.48.3: AnalyzeFieldSpec stays as a local AIFunction. The
            // model can still call it mid-turn if it wants to structure a
            // prose snippet on the fly, but the primary Testimize flow now
            // runs in-process via TestimizeRunner *before* this prompt is
            // sent (see GenerateHandler). There is no MCP server anymore.
            tools.Add(Testimize.FieldSpecAnalysisTools.CreateAnalyzeFieldSpecTool());

            // Create session with tools
            _onStatus?.Invoke("Creating AI session...");
            await using var session = await service.CreateGenerationSessionAsync(
                _provider,
                tools,
                ct);

            // Spec 040 follow-up: observer captures provider-reported token
            // counts from AssistantUsageEvent. Falls back to text.Length / 4
            // estimate if no usage event arrives within the grace window.
            var observer = new CopilotUsageObserver();

            // Track tool calls and schedule delayed composing message
            using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Spec 042: per-tool start tracking, keyed by ToolCallId. Captured by
            // the event subscription below — Start records the timestamp + name,
            // Complete looks it up to compute elapsed and emits one
            // `TOOL CALL ...` line to .spectra-debug.log per tool invocation.
            // ConcurrentDictionary because SDK events fire from internal tasks.
            var toolStarts = new ConcurrentDictionary<string, (DateTime StartUtc, string ToolName, string? McpServer, string? McpToolName)>();

            using var subscription = session.On(evt =>
            {
                if (evt is AssistantUsageEvent usage && usage.Data is { } usageData)
                {
                    observer.RecordUsage(
                        (int)(usageData.InputTokens ?? 0),
                        (int)(usageData.OutputTokens ?? 0));
                    return;
                }
                if (evt is ToolExecutionStartEvent toolStart)
                {
                    // Spec 042: record start so the matching Complete event can
                    // compute elapsed time. Key on ToolCallId (always present);
                    // values are stable for the lifetime of the call.
                    var startData = toolStart.Data;
                    if (!string.IsNullOrEmpty(startData.ToolCallId))
                    {
                        toolStarts[startData.ToolCallId] = (
                            DateTime.UtcNow,
                            startData.ToolName ?? "?",
                            startData.McpServerName,
                            startData.McpToolName);
                    }

                    var friendlyName = toolStart.Data.ToolName switch
                    {
                        "report_intent" => "Planning test generation strategy...",
                        "ListDocumentationFiles" or "GetDocumentMap" => "Scanning documentation files...",
                        "ReadDocument" or "LoadSourceDocument" => "Reading source documentation...",
                        "ReadTestIndex" or "GetExistingTestDetails" => "Checking existing test cases...",
                        "CheckDuplicates" or "CheckDuplicatesBatch" => "Checking for duplicate tests...",
                        "GetNextTestIds" => $"Preparing to generate {requestedCount} test cases...",
                        "BatchWriteTests" => "Writing test cases...",
                        "SearchSourceDocs" => "Searching documentation...",
                        // Suppress internal/noisy tool names
                        "powershell" or "view" or "bash" or "sh" or "cmd" => null,
                        _ => null // Don't show unknown tool names
                    };
                    if (friendlyName is not null)
                        _onStatus?.Invoke(friendlyName);

                    if (toolStart.Data.ToolName == "GetNextTestIds")
                    {
                        _ = Task.Delay(3000, timerCts.Token).ContinueWith(_ =>
                            _onStatus?.Invoke($"AI is writing {requestedCount} test cases — this takes about a minute..."),
                            TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    return;
                }

                // Spec 042: emit one TOOL CALL line per tool invocation to the
                // debug log. We see exactly which tools the AI actually used
                // (e.g. testimize/generate_hybrid_test_cases) and how long each
                // one took, so we can tell whether testimize was a no-op.
                if (evt is ToolExecutionCompleteEvent toolComplete)
                {
                    var completeData = toolComplete.Data;
                    if (string.IsNullOrEmpty(completeData.ToolCallId)) return;

                    if (toolStarts.TryRemove(completeData.ToolCallId, out var startInfo))
                    {
                        var elapsed = DateTime.UtcNow - startInfo.StartUtc;
                        var displayName = !string.IsNullOrEmpty(startInfo.McpToolName)
                            ? startInfo.McpToolName
                            : startInfo.ToolName;
                        var serverName = !string.IsNullOrEmpty(startInfo.McpServer)
                            ? startInfo.McpServer
                            : "-";
                        var success = completeData.Success ? "true" : "false";
                        DebugLog($"TOOL CALL tool={displayName} mcp_server={serverName} elapsed={elapsed.TotalSeconds:F2}s success={success}");
                    }
                    return;
                }
            });

            // Build the combined prompt with system instructions and user request.
            // The profile format (JSON schema sent to the AI) is resolved from
            // profiles/_default.yaml on disk if present, else the embedded default.
            var profileFormat = ProfileFormatLoader.LoadFormat(_basePath);
            // v1.48.3: testimizeData is pre-computed by TestimizeRunner in
            // GenerateHandler — when non-null, we embed it as a literal
            // authoritative "Pre-computed algorithmic test data (from
            // Testimize, strategy=X)" block in the prompt via the
            // {{#if testimize_dataset}} template section.
            var promptLoader = new PromptTemplateLoader(_basePath);
            var fullPrompt = BuildFullPrompt(
                prompt,
                requestedCount,
                criteriaContext,
                templateLoader: promptLoader,
                profileFormat: profileFormat,
                testimizeData: testimizeData);

            // Send and wait for the complete response.
            // The per-batch timeout is configurable via ai.generation_timeout_minutes.
            // Slower / reasoning models may need 10–20+ minutes per batch.
            var timeoutMinutes = Math.Max(1, _config.Ai.GenerationTimeoutMinutes);
            var batchTimeout = TimeSpan.FromMinutes(timeoutMinutes);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            DebugLogAi($"BATCH START requested={requestedCount} timeout={timeoutMinutes}min", null, null, estimated: false);
            _onStatus?.Invoke($"Starting AI generation ({requestedCount} tests, timeout {timeoutMinutes} min)...");
            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = fullPrompt },
                timeout: batchTimeout,
                cancellationToken: ct);

            // Cancel delayed timers so they don't overwrite the final result
            await timerCts.CancelAsync();
            sw.Stop();

            // Grace window for AssistantUsageEvent to arrive before we read.
            await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(200), ct);

            var responseText = response?.Data?.Content ?? "";
            var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(fullPrompt, responseText);

            _tracker?.Record("generation", _provider?.Model ?? "", _provider?.Name ?? "", tokensIn, tokensOut, sw.Elapsed, estimated);
            DebugLogAi($"BATCH OK   requested={requestedCount} elapsed={sw.Elapsed.TotalSeconds:F1}s", tokensIn, tokensOut, estimated);

            // Parse the response
            var tests = ParseTestsFromResponse(responseText);

            if (tests.Count == 0 && !string.IsNullOrEmpty(responseText))
            {
                // Response exists but no tests parsed — log for debugging
                return new GenerationResult
                {
                    Tests = [],
                    Errors = [
                        $"AI returned a response ({responseText.Length} chars) but no test cases could be parsed.",
                        "Check .spectra-debug.log for per-batch details.",
                        "The response may not contain a valid JSON array of test cases."
                    ]
                };
            }

            return new GenerationResult
            {
                Tests = tests,
                TokenUsage = null
            };
        }
        catch (TimeoutException ex)
        {
            var configuredMinutes = Math.Max(1, _config.Ai.GenerationTimeoutMinutes);
            // Spec 043: capture full timeout context to error log + bump tracker.
            _errorTracker?.RecordError();
            Spectra.CLI.Infrastructure.ErrorLogger.Write(
                "generate",
                $"requested={requestedCount} configured_timeout={configuredMinutes}min",
                ex);
            DebugLogAi($"BATCH TIMEOUT requested={requestedCount} configured_timeout={configuredMinutes}min"
                + (Spectra.CLI.Infrastructure.ErrorLogger.Enabled ? $" see={Spectra.CLI.Infrastructure.ErrorLogger.LogFile}" : ""),
                null, null, estimated: false);
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    $"Generation timed out after {configuredMinutes} minutes (model: {_provider?.Model ?? "?"}, batch size: {requestedCount}).",
                    "Options to fix this:",
                    "  1. Increase the per-batch timeout in spectra.config.json:",
                    "       \"ai\": { \"generation_timeout_minutes\": 15 }",
                    "  2. Reduce the batch size:",
                    "       \"ai\": { \"generation_batch_size\": 10 }",
                    "  3. Reduce --count or use a faster model.",
                    "See .spectra-debug.log for per-batch timing details."
                ]
            };
        }
        catch (Exception ex) when (ex.Message.Contains("copilot", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("CLI", StringComparison.OrdinalIgnoreCase))
        {
            _errorTracker?.Record(ex);
            Spectra.CLI.Infrastructure.ErrorLogger.Write("generate", $"requested={requestedCount}", ex);
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    "Copilot SDK error: " + ex.Message,
                    "Ensure the 'copilot' CLI is installed and authenticated.",
                    "Run: copilot --version"
                ]
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            _errorTracker?.Record(ex);
            Spectra.CLI.Infrastructure.ErrorLogger.Write("generate", $"requested={requestedCount}", ex);
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    "Rate limit exceeded.",
                    "Retry: Wait a few minutes and try again, or reduce --count."
                ]
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Session error", StringComparison.OrdinalIgnoreCase))
        {
            // SDK wraps session errors in InvalidOperationException
            _errorTracker?.Record(ex);
            Spectra.CLI.Infrastructure.ErrorLogger.Write("generate", $"requested={requestedCount}", ex);
            var innerMessage = ex.Message.Replace("Session error: ", "");
            return new GenerationResult
            {
                Tests = [],
                Errors = [$"Generation error: {innerMessage}"]
            };
        }
        catch (Exception ex)
        {
            _errorTracker?.Record(ex);
            Spectra.CLI.Infrastructure.ErrorLogger.Write("generate", $"requested={requestedCount}", ex);
            return new GenerationResult
            {
                Tests = [],
                Errors = [$"Generation error: {ex.Message}"]
            };
        }
    }

    /// <summary>
    /// Wrapper around <see cref="Spectra.CLI.Infrastructure.DebugLogger"/> for
    /// non-AI lines (e.g. testimize lifecycle). AI call lines go through
    /// <see cref="DebugLogAi"/> to include model/provider/token suffixes.
    /// </summary>
    private void DebugLog(string message) =>
        Spectra.CLI.Infrastructure.DebugLogger.Append("generate", message);

    private void DebugLogAi(string message, int? tokensIn, int? tokensOut, bool estimated = false) =>
        Spectra.CLI.Infrastructure.DebugLogger.AppendAi(
            "generate",
            message,
            _provider?.Model,
            _provider?.Name,
            tokensIn,
            tokensOut,
            estimated);

    /// <summary>
    /// Spec 053: relocated to <see cref="Spectra.CLI.Generation.PromptCompiler"/>. Kept as a
    /// thin delegate so existing callers/tests are unchanged.
    /// </summary>
    internal static string FormatTestimizeDataset(Spectra.Core.Models.Testimize.TestimizeDataset? dataset)
        => Spectra.CLI.Generation.PromptCompiler.FormatTestimizeDataset(dataset);

    /// <summary>
    /// Spec 053: prompt assembly is relocated to
    /// <see cref="Spectra.CLI.Generation.PromptCompiler.Assemble"/> (single source of prompt
    /// truth, decoupled from the Copilot SDK). This delegate preserves the existing lenient
    /// behavior and signature — for the refuse-to-emit validated path use
    /// <see cref="Spectra.CLI.Generation.PromptCompiler.Compile"/>.
    /// </summary>
    internal static string BuildFullPrompt(string userPrompt, int requestedCount, string? criteriaContext = null,
        PromptTemplateLoader? templateLoader = null, string? profileFormat = null,
        Spectra.Core.Models.Testimize.TestimizeDataset? testimizeData = null)
        => Spectra.CLI.Generation.PromptCompiler.Assemble(
            userPrompt, requestedCount, criteriaContext, templateLoader, profileFormat, testimizeData);

    private static List<TestCase> ParseTestsFromResponse(string response)
    {
        var tests = new List<TestCase>();

        if (string.IsNullOrWhiteSpace(response))
            return tests;

        var json = ExtractJson(response);
        if (string.IsNullOrWhiteSpace(json))
            return tests;

        // Try parsing as a complete JSON array
        var parsed = TryParseJsonArray(json);
        if (parsed is null)
        {
            // JSON may be truncated — try to salvage complete objects
            parsed = TryRepairTruncatedArray(json);
        }

        if (parsed is null)
            return tests;

        foreach (var element in parsed.Value.EnumerateArray())
        {
            var test = ParseTestCase(element);
            if (test is not null)
            {
                tests.Add(test);
            }
        }

        return tests;
    }

    private static string ExtractJson(string response)
    {
        // Strategy 1: Find JSON array inside a markdown code block
        var match = Regex.Match(response, @"```(?:json)?\s*(\[[\s\S]*)", RegexOptions.Singleline);
        if (match.Success)
        {
            var content = match.Groups[1].Value;
            // Strip trailing ``` if present
            var fenceEnd = content.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd > 0)
                content = content[..fenceEnd];
            return content.Trim();
        }

        // Strategy 2: Find the first [ in the response (start of JSON array)
        var firstBracket = response.IndexOf('[');
        if (firstBracket >= 0)
        {
            return response[firstBracket..].Trim();
        }

        return "";
    }

    /// <summary>
    /// Attempts to parse a string as a complete JSON array.
    /// </summary>
    private static JsonElement? TryParseJsonArray(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.Clone();
            doc.Dispose();
        }
        catch (JsonException)
        {
            // Not valid JSON
        }

        return null;
    }

    /// <summary>
    /// Repairs a truncated JSON array by finding the last complete object and closing the array.
    /// Handles cases where the response was cut off mid-object (e.g., token limit).
    /// </summary>
    private static JsonElement? TryRepairTruncatedArray(string json)
    {
        if (!json.TrimStart().StartsWith('['))
            return null;

        // Find the last complete JSON object by looking for },
        // or a final } that closes an object before the array would close
        var lastCompleteObject = -1;
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = 0; i < json.Length; i++)
        {
            var ch = json[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            switch (ch)
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                        lastCompleteObject = i;
                    break;
            }
        }

        if (lastCompleteObject <= 0)
            return null;

        // Close the array after the last complete object
        var repaired = json[..(lastCompleteObject + 1)].TrimEnd().TrimEnd(',') + "\n]";

        return TryParseJsonArray(repaired);
    }

    private static TestCase? ParseTestCase(JsonElement element)
    {
        try
        {
            var id = element.GetProperty("id").GetString() ?? "";
            var title = element.GetProperty("title").GetString() ?? "";

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
                return null;

            var priorityStr = element.TryGetProperty("priority", out var p) ? p.GetString() : "medium";
            var priority = priorityStr?.ToLowerInvariant() switch
            {
                "high" => Priority.High,
                "low" => Priority.Low,
                _ => Priority.Medium
            };

            var tags = new List<string>();
            if (element.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsElement.EnumerateArray())
                {
                    if (tag.GetString() is string t)
                        tags.Add(t);
                }
            }

            var steps = new List<string>();
            if (element.TryGetProperty("steps", out var stepsElement) && stepsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in stepsElement.EnumerateArray())
                {
                    if (step.GetString() is string s)
                        steps.Add(s);
                }
            }

            var sourceRefs = new List<string>();
            if (element.TryGetProperty("source_refs", out var refsElement) && refsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var refItem in refsElement.EnumerateArray())
                {
                    if (refItem.GetString() is string r)
                        sourceRefs.Add(r);
                }
            }

            TimeSpan? estimatedDuration = null;
            if (element.TryGetProperty("estimated_duration", out var durElement))
            {
                var durStr = durElement.GetString();
                if (!string.IsNullOrEmpty(durStr))
                    estimatedDuration = ParseDuration(durStr);
            }

            string? scenarioFromDoc = null;
            if (element.TryGetProperty("scenario_from_doc", out var scenarioElement))
                scenarioFromDoc = scenarioElement.GetString();

            var criteria = new List<string>();
            if (element.TryGetProperty("criteria", out var criteriaElement) && criteriaElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var criterionItem in criteriaElement.EnumerateArray())
                {
                    if (criterionItem.GetString() is string cr)
                        criteria.Add(cr);
                }
            }

            return new TestCase
            {
                Id = id,
                Title = title,
                Priority = priority,
                Tags = tags,
                Component = element.TryGetProperty("component", out var c) ? c.GetString() : null,
                Preconditions = element.TryGetProperty("preconditions", out var pre) ? pre.GetString() : null,
                Steps = steps,
                ExpectedResult = element.TryGetProperty("expected_result", out var er) ? er.GetString() ?? "" : "",
                TestData = element.TryGetProperty("test_data", out var td) ? td.GetString() : null,
                SourceRefs = sourceRefs,
                ScenarioFromDoc = scenarioFromDoc,
                EstimatedDuration = estimatedDuration,
                Criteria = criteria,
                FilePath = $"{id}.md"
            };
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? ParseDuration(string duration)
    {
        if (string.IsNullOrEmpty(duration)) return null;

        var match = Regex.Match(duration.Trim(), @"^(\d+)(s|m|h)$", RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var value = int.Parse(match.Groups[1].Value);
        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            _ => null
        };
    }
}
