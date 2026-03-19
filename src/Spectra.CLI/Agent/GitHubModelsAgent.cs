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
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<TestCase> existingTests,
        int requestedCount,
        CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = GroundedPromptBuilder.BuildSystemPrompt();
            var userPrompt = GroundedPromptBuilder.BuildUserPrompt(prompt, documents, existingTests, requestedCount);

            var model = _provider.Model ?? "gpt-4o";
            var maxTokens = GetMaxTokensForModel(model);

            var options = new ChatCompletionsOptions
            {
                Model = model,
                Temperature = 0.7f,
                MaxTokens = maxTokens,
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
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    "GitHub Models rate limit exceeded.",
                    "Retry: Wait a few minutes and try again, or reduce --count."
                ]
            };
        }
        catch (RequestFailedException ex) when (ex.Status >= 500)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    $"GitHub Models service error (HTTP {ex.Status}).",
                    "Retry: The service may be temporarily unavailable. Try again in a few minutes."
                ]
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
        catch (HttpRequestException ex)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    $"Network error: {ex.Message}",
                    "Retry: Check your internet connection and try again."
                ]
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    "Request timed out.",
                    "Retry: The AI service is slow. Try again or reduce --count."
                ]
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

            // Parse scenario_from_doc (grounding)
            string? scenarioFromDoc = null;
            if (element.TryGetProperty("scenario_from_doc", out var scenarioElement))
            {
                scenarioFromDoc = scenarioElement.GetString();
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

    /// <summary>
    /// Returns the maximum output tokens supported by the model.
    /// </summary>
    private static int GetMaxTokensForModel(string model)
    {
        var modelLower = model.ToLowerInvariant();

        // Grok models
        if (modelLower.Contains("grok"))
            return 131072;

        // GPT-5.x models
        if (modelLower.Contains("gpt-5") || modelLower.Contains("gpt5"))
            return 128000;

        // Gemini 3.x models
        if (modelLower.Contains("gemini-3") || modelLower.Contains("gemini3"))
            return 65536;

        // Raptor models
        if (modelLower.Contains("raptor"))
            return 32768;

        // GPT-4.1
        if (modelLower.Contains("gpt-4.1") || modelLower.Contains("gpt4.1"))
            return 32768;

        // GPT-4o and variants
        if (modelLower.Contains("gpt-4o"))
            return 16384;

        // GPT-4-turbo
        if (modelLower.Contains("gpt-4-turbo") || modelLower.Contains("gpt-4-1106") || modelLower.Contains("gpt-4-0125"))
            return 4096;

        // GPT-4 (original)
        if (modelLower.Contains("gpt-4"))
            return 8192;

        // GPT-3.5-turbo
        if (modelLower.Contains("gpt-3.5"))
            return 4096;

        // Claude Opus 4.6
        if (modelLower.Contains("claude-opus-4.6") || modelLower.Contains("opus-4.6") || modelLower.Contains("opus4.6"))
            return 128000;

        // Claude Sonnet 4.6
        if (modelLower.Contains("claude-sonnet-4.6") || modelLower.Contains("sonnet-4.6") || modelLower.Contains("sonnet4.6"))
            return 64000;

        // Claude Haiku 4.5
        if (modelLower.Contains("claude-haiku-4.5") || modelLower.Contains("haiku-4.5") || modelLower.Contains("haiku4.5"))
            return 8192;

        // Claude 3.5/4 Sonnet (older)
        if (modelLower.Contains("claude-3-sonnet") || modelLower.Contains("claude-3-5-sonnet") || modelLower.Contains("claude-sonnet-4"))
            return 8192;

        // Claude 3 Opus/Haiku
        if (modelLower.Contains("claude-3-opus"))
            return 4096;
        if (modelLower.Contains("claude-3-haiku"))
            return 4096;

        // Gemini 3.1 Pro
        if (modelLower.Contains("gemini-3.1-pro") || modelLower.Contains("gemini3.1-pro"))
            return 65536;

        // Gemini 3.1 Flash
        if (modelLower.Contains("gemini-3.1-flash") || modelLower.Contains("gemini3.1-flash"))
            return 32768;

        // Gemini 2.0 Flash
        if (modelLower.Contains("gemini-2.0-flash") || modelLower.Contains("gemini2.0-flash"))
            return 8192;

        // Gemini (other)
        if (modelLower.Contains("gemini"))
            return 8192;

        // Llama 4 models
        if (modelLower.Contains("llama-4") || modelLower.Contains("llama4"))
            return 65536;

        // Llama (older)
        if (modelLower.Contains("llama"))
            return 4096;

        // DeepSeek V3.2
        if (modelLower.Contains("deepseek"))
            return 16384;

        // Mistral models
        if (modelLower.Contains("mistral"))
            return 8192;

        // Cohere models
        if (modelLower.Contains("cohere"))
            return 4096;

        // Default fallback - safe for most models
        return 4096;
    }
}
