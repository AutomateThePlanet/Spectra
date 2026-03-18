using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.Inference;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent;

/// <summary>
/// GitHub Models agent implementation using Azure.AI.Inference SDK.
/// Provides access to various models through GitHub's model inference API.
/// </summary>
public sealed class GitHubModelsAgent : IAgentRuntime
{
    private const string BaseUrl = "https://models.inference.ai.azure.com";

    private readonly ProviderConfig _provider;
    private readonly ChatCompletionsClient _client;
    private readonly bool _hasValidToken;

    public string ProviderName => "github-models";

    /// <summary>
    /// Creates a new GitHubModelsAgent with a pre-obtained token.
    /// Preferred constructor for use with GitHubCliTokenProvider.
    /// </summary>
    public GitHubModelsAgent(ProviderConfig provider, string token)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        ArgumentException.ThrowIfNullOrEmpty(token);

        var endpoint = new Uri(_provider.BaseUrl ?? BaseUrl);
        _client = new ChatCompletionsClient(endpoint, new AzureKeyCredential(token));
        _hasValidToken = true;
    }

    /// <summary>
    /// Creates a new GitHubModelsAgent, retrieving the token from environment variables.
    /// Falls back to checking GITHUB_TOKEN if ApiKeyEnv is not set.
    /// </summary>
    public GitHubModelsAgent(ProviderConfig provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "GitHub token not found. Set the GITHUB_TOKEN environment variable.");
        }

        var endpoint = new Uri(_provider.BaseUrl ?? BaseUrl);
        _client = new ChatCompletionsClient(endpoint, new AzureKeyCredential(apiKey));
        _hasValidToken = true;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_hasValidToken);
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

            var model = _provider.Model ?? "gpt-4o";

            var options = new ChatCompletionsOptions
            {
                Model = model,
                Temperature = 0.7f,
                MaxTokens = 4096,
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                }
            };

            var response = await _client.CompleteAsync(options, ct);
            var completion = response.Value;

            if (string.IsNullOrEmpty(completion.Content))
            {
                return new GenerationResult
                {
                    Tests = [],
                    Errors = ["No response received from GitHub Models"]
                };
            }

            var responseText = completion.Content;
            var tests = ParseTestsFromResponse(responseText);

            var usage = completion.Usage;
            var tokenUsage = usage is not null
                ? new TokenUsage(
                    usage.PromptTokens,
                    usage.CompletionTokens,
                    usage.TotalTokens)
                : null;

            return new GenerationResult
            {
                Tests = tests,
                TokenUsage = tokenUsage
            };
        }
        catch (RequestFailedException ex)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [$"GitHub Models API error: {ex.Message}"]
            };
        }
        catch (Exception ex)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [$"GitHub Models error: {ex.Message}"]
            };
        }
    }

    private static string GetApiKey()
    {
        return Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? string.Empty;
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
