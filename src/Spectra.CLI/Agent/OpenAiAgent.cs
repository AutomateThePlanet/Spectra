using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
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
    private readonly string _model;
    private readonly bool _isAzure;

    public string ProviderName => _isAzure ? "azure-openai" : "openai";

    public OpenAiAgent(ProviderConfig provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        var apiKey = GetApiKey(provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            var defaultEnv = IsAzureEndpoint(provider.BaseUrl) ? "AZURE_OPENAI_API_KEY" : "OPENAI_API_KEY";
            throw new InvalidOperationException(
                $"API key not found. Set the {provider.ApiKeyEnv ?? defaultEnv} environment variable.");
        }

        _model = provider.Model ?? "gpt-4o";
        _isAzure = IsAzureEndpoint(provider.BaseUrl);

        if (_isAzure)
        {
            // Azure OpenAI - use AzureOpenAIClient with endpoint
            var endpoint = new Uri(provider.BaseUrl!);
            var azureClient = new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey));
            _client = azureClient.GetChatClient(_model).AsIChatClient();
        }
        else
        {
            // Standard OpenAI
            var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey));
            _client = openAiClient.GetChatClient(_model).AsIChatClient();
        }
    }

    private static bool IsAzureEndpoint(string? baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl)) return false;
        return baseUrl.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase);
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
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<TestCase> existingTests,
        int requestedCount,
        CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = GroundedPromptBuilder.BuildSystemPrompt();
            var userPrompt = GroundedPromptBuilder.BuildUserPrompt(prompt, documents, existingTests, requestedCount);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var options = new ChatOptions
            {
                Temperature = 0.7f,
                MaxOutputTokens = GetMaxTokensForModel(_model)
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
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    "OpenAI rate limit exceeded.",
                    "Retry: Wait a few minutes and try again, or reduce --count."
                ]
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("5"))
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    $"OpenAI service error: {ex.Message}",
                    "Retry: The service may be temporarily unavailable. Try again in a few minutes."
                ]
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
                Errors = [$"OpenAI API error: {ex.Message}"]
            };
        }
    }

    private static string GetApiKey(ProviderConfig provider)
    {
        // Determine default env var based on endpoint type
        var defaultEnv = IsAzureEndpoint(provider.BaseUrl) ? "AZURE_OPENAI_API_KEY" : "OPENAI_API_KEY";
        var envVar = provider.ApiKeyEnv ?? defaultEnv;
        return Environment.GetEnvironmentVariable(envVar) ?? string.Empty;
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

        // GPT-5.x models
        if (modelLower.Contains("gpt-5") || modelLower.Contains("gpt5"))
            return 128000;

        // o1 models have high limits
        if (modelLower.Contains("o1"))
            return 100000;

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

        // Default fallback - safe for most models
        return 4096;
    }
}
