using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent;

/// <summary>
/// Anthropic Claude agent implementation using the official SDK.
/// </summary>
public sealed class AnthropicAgent : IAgentRuntime
{
    private readonly ProviderConfig _provider;
    private readonly AnthropicClient _client;

    public string ProviderName => "anthropic";

    public AnthropicAgent(ProviderConfig provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        var apiKey = GetApiKey(provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                $"API key not found. Set the {provider.ApiKeyEnv ?? "ANTHROPIC_API_KEY"} environment variable.");
        }

        // Set environment variable for SDK to pick up
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", apiKey);
        _client = new AnthropicClient();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var apiKey = GetApiKey(_provider);
        return await Task.FromResult(!string.IsNullOrEmpty(apiKey));
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

            var model = _provider.Model ?? "claude-sonnet-4-5-20250514";

            var parameters = new MessageCreateParams
            {
                Model = model,
                MaxTokens = 4096,
                System = systemPrompt,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = userPrompt
                    }
                ]
            };

            var response = await _client.Messages.Create(parameters, cancellationToken: ct);

            if (response.Content is null || response.Content.Count == 0)
            {
                return new GenerationResult
                {
                    Tests = [],
                    Errors = ["No response received from Anthropic"]
                };
            }

            // The response ToString() provides convenient access to text content
            var responseText = response.ToString() ?? "";

            var tests = ParseTestsFromResponse(responseText);

            var tokenUsage = new TokenUsage(
                (int)(response.Usage?.InputTokens ?? 0),
                (int)(response.Usage?.OutputTokens ?? 0),
                (int)((response.Usage?.InputTokens ?? 0) + (response.Usage?.OutputTokens ?? 0)));

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
                Errors = [$"Anthropic API error: {ex.Message}"]
            };
        }
    }

    private static string GetApiKey(ProviderConfig provider)
    {
        var envVar = provider.ApiKeyEnv ?? "ANTHROPIC_API_KEY";
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
              "test_data": "Any test data needed (optional)"
            }

            Guidelines:
            - Generate unique test IDs in TC-XXX format
            - Cover happy paths, edge cases, and error scenarios
            - Steps should be clear and actionable
            - Expected results should be specific and verifiable
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
        sb.AppendLine("## Source Documentation");
        sb.AppendLine();

        foreach (var doc in documentMap.Documents.Take(5)) // Limit context size
        {
            sb.AppendLine($"### {doc.Title}");
            sb.AppendLine($"Path: {doc.Path}");
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
                FilePath = $"{id}.md"
            };
        }
        catch
        {
            return null;
        }
    }
}
