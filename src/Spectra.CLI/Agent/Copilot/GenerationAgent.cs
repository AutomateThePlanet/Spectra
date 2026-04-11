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

    public string ProviderName => $"copilot-sdk ({_provider?.Name ?? "github-models"})";

    public CopilotGenerationAgent(
        SpectraProviderConfig provider,
        SpectraConfig config,
        string basePath,
        string testsPath,
        Action<string>? onStatus = null,
        TokenUsageTracker? tracker = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _testsPath = testsPath ?? throw new ArgumentNullException(nameof(testsPath));
        _onStatus = onStatus;
        _tracker = tracker;
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
        CancellationToken ct = default)
    {
        try
        {
            var service = await CopilotService.GetInstanceAsync(ct);

            // Scan all existing IDs for global uniqueness
            var allExistingIds = existingTests.Select(t => t.Id).ToHashSet();

            // Create tools for the agent
            var documentTools = DocumentTools.Create(_basePath, _config);
            var testIndexTools = TestIndexTools.Create(
                _basePath,
                _testsPath,
                _config,
                existingTests,
                allExistingIds);

            var tools = documentTools.CreateFunctions()
                .Concat(testIndexTools.CreateFunctions())
                .ToList();

            // v1.46.0: AnalyzeFieldSpec is a local regex parser (no MCP call),
            // always available regardless of whether testimize is enabled.
            tools.Add(Testimize.FieldSpecAnalysisTools.CreateAnalyzeFieldSpecTool());

            // v1.46.0: Optional Testimize integration now flows through the
            // Copilot SDK's native McpServers dictionary. The SDK owns the
            // full lifecycle (spawn, initialize handshake, framing, tool
            // discovery, cost attribution, permission prompts). The old
            // custom TestimizeMcpClient has been deleted — it spoke
            // Content-Length framing while testimize-mcp actually uses
            // newline-delimited JSON, which caused the 5s health-probe hang.
            Dictionary<string, object>? mcpServers = null;
            var testimizeServer = McpConfigBuilder.BuildTestimizeServer(_config.Testimize);
            if (testimizeServer is null)
            {
                DebugLog("TESTIMIZE DISABLED (testimize.enabled=false in config)");
            }
            else
            {
                mcpServers = new Dictionary<string, object> { ["testimize"] = testimizeServer };
                DebugLog($"TESTIMIZE CONFIGURED server=testimize command={_config.Testimize.Mcp.Command} args=[{string.Join(" ", _config.Testimize.Mcp.Args)}] strategy={_config.Testimize.Strategy} mode={_config.Testimize.Mode}");
                _onStatus?.Invoke("Testimize configured — the Copilot SDK will spawn it when the AI session starts");
            }

            try
            {

            // Create session with tools
            _onStatus?.Invoke("Creating AI session...");
            await using var session = await service.CreateGenerationSessionAsync(
                _provider,
                tools,
                ct,
                mcpServers);

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
                // v1.46.0: SDK-driven testimize lifecycle logging. Replaces
                // the old custom TestimizeMcpClient health probe. The SDK
                // exposes per-server status (Connected / Failed / NeedsAuth
                // / Pending / Disabled / NotConfigured) via this event.
                if (evt is SessionMcpServersLoadedEvent loaded && loaded.Data?.Servers is { } servers)
                {
                    foreach (var s in servers)
                    {
                        var name = s.Name ?? "?";
                        var status = s.Status.ToString();
                        var errorSuffix = string.IsNullOrEmpty(s.Error) ? "" : $" error=\"{s.Error}\"";
                        DebugLog($"TESTIMIZE LOADED server={name} status={status} source={s.Source ?? "?"}{errorSuffix}");
                        if (string.Equals(name, "testimize", StringComparison.OrdinalIgnoreCase))
                        {
                            if (status.Equals("Connected", StringComparison.OrdinalIgnoreCase))
                            {
                                _onStatus?.Invoke("Testimize connected — algorithmic test data available");
                            }
                            else
                            {
                                _onStatus?.Invoke($"Testimize server status={status} — falling back to AI-generated values");
                            }
                        }
                    }
                    return;
                }
                if (evt is SessionMcpServerStatusChangedEvent changed && changed.Data is { } changedData)
                {
                    DebugLog($"TESTIMIZE STATUS_CHANGED server={changedData.ServerName} status={changedData.Status}");
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

            // v1.46.1: the old 3-second grace window was deleted. The
            // Copilot CLI's MCP client takes ~60 seconds to attempt +
            // time out the initialize handshake (JSON-RPC error -32001),
            // so a 3-second wait would always fire a false UNHEALTHY
            // line and then the real LOADED event would arrive much
            // later. Instead, we let the SDK fire SessionMcpServersLoadedEvent
            // whenever it's ready — the event handler above logs the
            // real status (Connected / Failed / NeedsAuth / ...) with
            // the actual error message. Generation proceeds immediately
            // because the SDK doesn't block session creation on MCP load.

            // Build the combined prompt with system instructions and user request.
            // The profile format (JSON schema sent to the AI) is resolved from
            // profiles/_default.yaml on disk if present, else the embedded default.
            var profileFormat = ProfileFormatLoader.LoadFormat(_basePath);
            // Spec 038: pass a PromptTemplateLoader so the test-generation.md
            // template's {{#if testimize_enabled}} block is rendered when the
            // user has enabled Testimize. v1.46.0: also passes the tool name
            // hint via testimize_strategy so the AI knows which testimize
            // tool to prefer.
            var promptLoader = new PromptTemplateLoader(_basePath);
            var fullPrompt = BuildFullPrompt(
                prompt,
                requestedCount,
                criteriaContext,
                templateLoader: promptLoader,
                profileFormat: profileFormat,
                testimizeEnabled: _config.Testimize.Enabled,
                testimizeStrategy: _config.Testimize.Strategy);

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
            finally
            {
                // v1.46.0: MCP server process lifecycle is now owned by the
                // Copilot SDK via the session's McpServers config. When the
                // session disposes (above, via `await using`), the SDK kills
                // all attached MCP child processes. We just log the boundary.
                if (mcpServers is not null)
                {
                    DebugLog("TESTIMIZE DISPOSED (session closed)");
                }
            }
        }
        catch (TimeoutException)
        {
            var configuredMinutes = Math.Max(1, _config.Ai.GenerationTimeoutMinutes);
            DebugLogAi($"BATCH TIMEOUT requested={requestedCount} configured_timeout={configuredMinutes}min", null, null, estimated: false);
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
            var innerMessage = ex.Message.Replace("Session error: ", "");
            return new GenerationResult
            {
                Tests = [],
                Errors = [$"Generation error: {innerMessage}"]
            };
        }
        catch (Exception ex)
        {
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
    /// v1.46.0: maps <c>config.Testimize.Strategy</c> to the real MCP tool name
    /// exposed by <c>testimize-mcp --mcp</c>. Used by the test-generation prompt
    /// template as the default tool hint. Unknown strategies fall back to the
    /// hybrid tool since it's the most capable.
    /// </summary>
    internal static string ResolveTestimizeStrategyToolName(string? strategy) =>
        (strategy ?? "").Trim() switch
        {
            "Pairwise" or "PairwiseOptimized" => "testimize/generate_pairwise_test_cases",
            _ => "testimize/generate_hybrid_test_cases"
        };

    internal static string BuildFullPrompt(string userPrompt, int requestedCount, string? criteriaContext = null,
        PromptTemplateLoader? templateLoader = null, string? profileFormat = null,
        bool testimizeEnabled = false, string? testimizeStrategy = null)
    {
        // The JSON output schema sent to the AI. Prefer the caller-supplied
        // profileFormat (resolved from profiles/_default.yaml on disk or the
        // embedded default by ProfileFormatLoader). When null (legacy callers
        // and unit tests), fall back to the embedded default to keep behavior
        // identical.
        var jsonExample = profileFormat ?? ProfileFormatLoader.LoadFormat(Directory.GetCurrentDirectory());

        if (templateLoader is not null)
        {
            var template = templateLoader.LoadTemplate("test-generation");
            var values = new Dictionary<string, string>
            {
                // Spec 038: gates the {{#if testimize_enabled}} block in test-generation.md
                ["testimize_enabled"] = testimizeEnabled ? "true" : "",
                // v1.46.0: name of the preferred testimize MCP tool (derived from
                // config.Testimize.Strategy). The prompt template embeds this so
                // the AI knows which of the two testimize tools to prefer.
                ["testimize_strategy"] = ResolveTestimizeStrategyToolName(testimizeStrategy),
                ["behaviors"] = userPrompt,
                ["suite_name"] = "",
                ["existing_tests"] = "",
                ["acceptance_criteria"] = criteriaContext ?? "",
                ["profile_format"] = jsonExample,
                ["count"] = requestedCount.ToString(),
                ["focus_areas"] = ""
            };

            return PromptTemplateLoader.Resolve(template, values);
        }

        return $"""
            You are a test case generation expert that creates DOCUMENT-GROUNDED test cases.

            ## CRITICAL RULES

            1. NEVER generate generic test patterns. Every test MUST trace to specific documentation.
            2. Use the available tools to read documentation ON-DEMAND instead of relying on memory.
            3. ALWAYS check for duplicates before generating tests.

            ## WORKFLOW

            Follow this exact workflow for test generation:

            1. **LIST DOCUMENTS**: First, call ListDocumentationFiles to see what documentation is available.
            2. **READ TEST INDEX**: Call ReadTestIndex to see existing tests and avoid duplicates.
            3. **READ RELEVANT DOCS**: Based on the user's request, call ReadDocument for specific files.
            4. **CHECK DUPLICATES**: Before finalizing, call CheckDuplicates with your proposed titles.
            5. **GET TEST IDs**: Call GetNextTestIds to allocate unique IDs for new tests.
            6. **GENERATE TESTS**: Return tests as a JSON array.

            ## OUTPUT FORMAT

            After using tools to gather information, your FINAL message must contain ONLY a JSON array of test cases.
            Do NOT include any explanatory text before or after the JSON. Output ONLY the JSON array.

            The JSON array must follow this exact schema:

            ```json
            {jsonExample}
            ```

            ---

            ## YOUR TASK

            Generate {requestedCount} new manual test cases based on this request:

            {userPrompt}

            {(string.IsNullOrEmpty(criteriaContext) ? "" : $"\n## ACCEPTANCE CRITERIA — MANDATORY\n\nYou MUST map each test case to matching acceptance criteria below. Every test MUST have at least one criterion ID in its \"criteria\" array. If a test doesn't match any criterion, use the closest related one.\n\n{criteriaContext}\n")}
            IMPORTANT:
            1. Use the tools to read documentation and check for duplicates first
            2. Only generate tests that are grounded in the documentation
            3. Ensure unique test IDs using GetNextTestIds
            4. Your FINAL response must be ONLY the JSON array — no other text
            5. MANDATORY: For each test, populate the "criteria" array with IDs of acceptance criteria it verifies (e.g. ["AC-REPORTING-001", "AC-REPORTING-003"]). Never leave criteria empty when acceptance criteria are provided above.
            """;
    }

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
