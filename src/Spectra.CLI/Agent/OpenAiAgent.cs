using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using OpenAI;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent;

/// <summary>
/// OpenAI-based agent implementation using Microsoft.Extensions.AI.
/// Supports OpenAI API directly or Azure OpenAI.
/// </summary>
public sealed class OpenAiAgent : IAgentRuntime
{
    private readonly ProviderConfig _provider;
    private readonly IChatClient _client;

    public string ProviderName => "openai";

    public OpenAiAgent(ProviderConfig provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        var apiKey = GetApiKey(provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                $"API key not found. Set the {provider.ApiKeyEnv ?? "OPENAI_API_KEY"} environment variable.");
        }

        var model = provider.Model ?? "gpt-4o";
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey));
        _client = openAiClient.GetChatClient(model).AsIChatClient();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // Simple check - try to get a response
            var response = await _client.GetResponseAsync("Say 'ok'", cancellationToken: ct);
            return response != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<GenerationResult> GenerateTestsAsync(
        string prompt,
        DocumentMap documentMap,
        IReadOnlyList<TestCase> existingTests,
        CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(prompt, documentMap, existingTests);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var options = new ChatOptions
            {
                Temperature = 0.7f,
                MaxOutputTokens = 4096
            };

            var response = await _client.GetResponseAsync(messages, options, ct);

            if (response?.Text is null)
            {
                return new GenerationResult
                {
                    Tests = [],
                    Errors = ["No response received from OpenAI"]
                };
            }

            var tests = ParseTestsFromResponse(response.Text);

            var usage = response.Usage;
            var tokenUsage = usage is not null
                ? new TokenUsage(
                    (int)(usage.InputTokenCount ?? 0),
                    (int)(usage.OutputTokenCount ?? 0),
                    (int)(usage.TotalTokenCount ?? 0))
                : null;

            return new GenerationResult
            {
                Tests = tests,
                TokenUsage = tokenUsage
            };
        }
        catch (Exception ex)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [$"OpenAI API error: {ex.Message}"]
            };
        }
    }

    private static string GetApiKey(ProviderConfig provider)
    {
        var envVar = provider.ApiKeyEnv ?? "OPENAI_API_KEY";
        return Environment.GetEnvironmentVariable(envVar) ?? string.Empty;
    }

    private static string BuildSystemPrompt()
    {
        return """
            You are a test case generation expert. Generate comprehensive manual test cases based on the provided documentation.

            Output format: Return ONLY a JSON array of test cases. No markdown, no explanation, just valid JSON.

            Each test case must have this structure:
            {
              "id": "TC-XXX",
              "title": "Short descriptive title",
              "priority": "high|medium|low",
              "tags": ["tag1", "tag2"],
              "component": "component-name",
              "preconditions": "Prerequisites for the test",
              "steps": ["Step 1", "Step 2", "Step 3"],
              "expected_result": "What should happen",
              "test_data": "Any test data needed (optional)",
              "source_refs": ["path/to/source/doc.md"],
              "estimated_duration": "5m"
            }

            Guidelines:
            - Generate unique test IDs in TC-XXX format
            - Cover happy paths, edge cases, and error scenarios
            - Steps should be clear and actionable
            - Expected results should be specific and verifiable
            - IMPORTANT: Include source_refs with the exact paths to documentation files that informed this test
            - Use estimated_duration format: Ns for seconds, Nm for minutes, Nh for hours (e.g., "30s", "5m", "1h")
            """;
    }

    private static string BuildUserPrompt(
        string prompt,
        DocumentMap documentMap,
        IReadOnlyList<TestCase> existingTests)
    {
        var sb = new StringBuilder();

        sb.AppendLine(prompt);
        sb.AppendLine();

        // Add document context
        sb.AppendLine("## Source Documentation (use these paths in source_refs)");
        sb.AppendLine();

        foreach (var doc in documentMap.Documents.Take(5)) // Limit context size
        {
            sb.AppendLine($"### {doc.Title}");
            sb.AppendLine($"**Path: {doc.Path}**");
            if (!string.IsNullOrEmpty(doc.Preview))
            {
                sb.AppendLine(doc.Preview);
            }
            sb.AppendLine();
        }

        // Add existing test IDs to avoid
        if (existingTests.Count > 0)
        {
            sb.AppendLine("## Existing Test IDs (do not duplicate)");
            sb.AppendLine(string.Join(", ", existingTests.Select(t => t.Id)));
            sb.AppendLine();
        }

        sb.AppendLine("Generate the test cases as a JSON array:");

        return sb.ToString();
    }

    private static List<TestCase> ParseTestsFromResponse(string response)
    {
        var tests = new List<TestCase>();

        try
        {
            // Extract JSON from response (may be wrapped in markdown code blocks)
            var json = ExtractJson(response);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                return tests;
            }

            foreach (var element in root.EnumerateArray())
            {
                var test = ParseTestCase(element);
                if (test is not null)
                {
                    tests.Add(test);
                }
            }
        }
        catch (JsonException)
        {
            // Failed to parse JSON - return empty list
        }

        return tests;
    }

    private static string ExtractJson(string response)
    {
        // Remove markdown code blocks if present
        var json = response.Trim();

        if (json.StartsWith("```json"))
        {
            json = json[7..];
        }
        else if (json.StartsWith("```"))
        {
            json = json[3..];
        }

        if (json.EndsWith("```"))
        {
            json = json[..^3];
        }

        return json.Trim();
    }

    private static TestCase? ParseTestCase(JsonElement element)
    {
        try
        {
            var id = element.GetProperty("id").GetString() ?? "";
            var title = element.GetProperty("title").GetString() ?? "";

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
            {
                return null;
            }

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
                    {
                        tags.Add(t);
                    }
                }
            }

            var steps = new List<string>();
            if (element.TryGetProperty("steps", out var stepsElement) && stepsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in stepsElement.EnumerateArray())
                {
                    if (step.GetString() is string s)
                    {
                        steps.Add(s);
                    }
                }
            }

            // Parse source_refs array
            var sourceRefs = new List<string>();
            if (element.TryGetProperty("source_refs", out var refsElement) &&
                refsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var refItem in refsElement.EnumerateArray())
                {
                    if (refItem.GetString() is string r)
                    {
                        sourceRefs.Add(r);
                    }
                }
            }

            // Parse estimated_duration
            TimeSpan? estimatedDuration = null;
            if (element.TryGetProperty("estimated_duration", out var durElement))
            {
                var durStr = durElement.GetString();
                if (!string.IsNullOrEmpty(durStr))
                {
                    estimatedDuration = ParseDuration(durStr);
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
