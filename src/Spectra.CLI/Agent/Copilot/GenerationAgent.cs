using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Spectra.CLI.Agent.Tools;
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

    public string ProviderName => $"copilot-sdk ({_provider?.Name ?? "github-models"})";

    public CopilotGenerationAgent(
        SpectraProviderConfig provider,
        SpectraConfig config,
        string basePath,
        string testsPath,
        Action<string>? onStatus = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _testsPath = testsPath ?? throw new ArgumentNullException(nameof(testsPath));
        _onStatus = onStatus;
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

            // Create session with tools
            _onStatus?.Invoke("Creating AI session...");
            await using var session = await service.CreateGenerationSessionAsync(
                _provider,
                tools,
                ct);

            // Subscribe to events ONLY for tool call status updates (not delta spam)
            using var subscription = session.On(evt =>
            {
                switch (evt)
                {
                    case ToolExecutionStartEvent toolStart:
                        var friendlyName = toolStart.Data.ToolName switch
                        {
                            "report_intent" => "Planning test generation strategy...",
                            "ListDocumentationFiles" or "GetDocumentMap" => "Scanning documentation files...",
                            "ReadDocument" or "LoadSourceDocument" => "Reading source documentation...",
                            "ReadTestIndex" => "Checking existing test cases...",
                            "CheckDuplicates" or "CheckDuplicatesBatch" => "Checking for duplicate tests...",
                            "GetNextTestIds" => "Allocating test case IDs...",
                            "BatchWriteTests" => "Writing test cases...",
                            "SearchSourceDocs" => "Searching documentation...",
                            _ => $"Processing: {toolStart.Data.ToolName}"
                        };
                        _onStatus?.Invoke(friendlyName);
                        break;
                }
            });

            // Build the combined prompt with system instructions and user request
            var fullPrompt = BuildFullPrompt(prompt, requestedCount);

            // Send and wait for the complete response (handles agent loop internally)
            _onStatus?.Invoke("Generating test cases...");
            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = fullPrompt },
                timeout: TimeSpan.FromMinutes(5),
                cancellationToken: ct);

            var responseText = response?.Data?.Content ?? "";

            // Save raw response for debugging
            SaveDebugResponse(responseText);

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
                        "Check .spectra-debug-response.txt for the raw AI output.",
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
        catch (TimeoutException)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = ["Generation timed out after 5 minutes. Try reducing --count."]
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

    private void SaveDebugResponse(string responseText)
    {
        try
        {
            var debugPath = Path.Combine(_basePath, ".spectra-debug-response.txt");
            var header = $"--- Spectra AI Response Debug ---\n" +
                         $"Timestamp: {DateTime.UtcNow:O}\n" +
                         $"Provider: {_provider?.Name}\n" +
                         $"Model: {_provider?.Model}\n" +
                         $"Response length: {responseText.Length} chars\n" +
                         $"---\n\n";
            File.WriteAllText(debugPath, header + responseText);
        }
        catch
        {
            // Debug file write failure is non-fatal
        }
    }

    private static string BuildFullPrompt(string userPrompt, int requestedCount)
    {
        var jsonExample = """
            [
              {
                "id": "TC-XXX",
                "title": "Descriptive title based on documentation",
                "priority": "high|medium|low",
                "tags": ["tag1", "tag2"],
                "component": "component-name",
                "preconditions": "Setup requirements",
                "steps": ["Step 1", "Step 2", "Step 3"],
                "expected_result": "Specific outcome from documentation",
                "test_data": "Test data if needed",
                "source_refs": ["docs/file.md#Section-Name"],
                "scenario_from_doc": "Quote or paraphrase the documented behavior",
                "estimated_duration": "5m"
              }
            ]
            """;

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

            IMPORTANT:
            1. Use the tools to read documentation and check for duplicates first
            2. Only generate tests that are grounded in the documentation
            3. Ensure unique test IDs using GetNextTestIds
            4. Your FINAL response must be ONLY the JSON array — no other text
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
