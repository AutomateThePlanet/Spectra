using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Models.Messages;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent;

/// <summary>
/// Anthropic Claude agent implementation using the official SDK.
/// Supports direct Anthropic API or Azure AI-hosted Claude models.
/// </summary>
public sealed class AnthropicAgent : IAgentRuntime
{
    private readonly ProviderConfig _provider;
    private readonly AnthropicClient? _anthropicClient;
    private readonly HttpClient? _azureHttpClient;
    private readonly string? _azureApiKey;
    private readonly bool _isAzure;

    public string ProviderName => _isAzure ? "azure-anthropic" : "anthropic";

    public AnthropicAgent(ProviderConfig provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _isAzure = IsAzureEndpoint(provider.BaseUrl);

        var apiKey = GetApiKey(provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            var defaultEnv = _isAzure ? "AZURE_ANTHROPIC_API_KEY" : "ANTHROPIC_API_KEY";
            throw new InvalidOperationException(
                $"API key not found. Set the {provider.ApiKeyEnv ?? defaultEnv} environment variable.");
        }

        if (_isAzure)
        {
            // Azure AI-hosted Claude - use direct HTTP with Anthropic Messages API format
            _azureApiKey = apiKey;
            _azureHttpClient = new HttpClient();

            // Azure Claude uses x-api-key header (per Azure docs)
            _azureHttpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

            // Anthropic API requires version header
            _azureHttpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }
        else
        {
            // Direct Anthropic API
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", apiKey);
            _anthropicClient = new AnthropicClient();
        }
    }

    private static bool IsAzureEndpoint(string? baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl)) return false;
        return baseUrl.Contains(".azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".inference.ai.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var apiKey = GetApiKey(_provider);
        return await Task.FromResult(!string.IsNullOrEmpty(apiKey));
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

            var model = _provider.Model ?? "claude-sonnet-4-5-20250514";
            var maxTokens = GetMaxTokensForModel(model);

            if (_isAzure)
            {
                return await GenerateWithAzureAsync(systemPrompt, userPrompt, model, maxTokens, ct);
            }
            else
            {
                return await GenerateWithAnthropicAsync(systemPrompt, userPrompt, model, maxTokens, ct);
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    "Anthropic rate limit exceeded.",
                    "Retry: Wait a few minutes and try again, or reduce --count."
                ]
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("529") || ex.Message.Contains("overloaded"))
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    "Anthropic API is overloaded.",
                    "Retry: Wait a few minutes and try again."
                ]
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("5"))
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    $"Anthropic service error: {ex.Message}",
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
                Errors = [$"Anthropic API error: {ex.Message}"]
            };
        }
    }

    private async Task<GenerationResult> GenerateWithAnthropicAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        int maxTokens,
        CancellationToken ct)
    {
        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = maxTokens,
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

        var response = await _anthropicClient!.Messages.Create(parameters, cancellationToken: ct);

        if (response.Content is null || response.Content.Count == 0)
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = ["No response received from Anthropic"]
            };
        }

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

    private async Task<GenerationResult> GenerateWithAzureAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        int maxTokens,
        CancellationToken ct)
    {
        // Azure AI-hosted Claude uses Anthropic's Messages API format
        var requestBody = new
        {
            model = model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody, jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        // Determine the endpoint URL
        // If base_url already contains /messages, use it directly
        // Otherwise, append the standard path
        var baseUrl = _provider.BaseUrl!.TrimEnd('/');
        string endpoint;
        if (baseUrl.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = baseUrl;
        }
        else if (baseUrl.Contains("/anthropic/", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = baseUrl + "/messages";
        }
        else
        {
            endpoint = baseUrl + "/anthropic/v1/messages";
        }

        var response = await _azureHttpClient!.PostAsync(endpoint, content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var keyPreview = !string.IsNullOrEmpty(_azureApiKey) && _azureApiKey.Length > 8
                ? $"{_azureApiKey[..4]}...{_azureApiKey[^4..]}"
                : "(not set)";

            return new GenerationResult
            {
                Tests = [],
                Errors = [
                    $"Azure Claude API error: {response.StatusCode}",
                    $"Endpoint: {endpoint}",
                    $"API Key: {keyPreview}",
                    $"Response: {responseContent}"
                ]
            };
        }

        // Parse Anthropic Messages API response
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        string? responseText = null;
        if (root.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
        {
            var firstContent = contentArray[0];
            if (firstContent.TryGetProperty("text", out var textProp))
            {
                responseText = textProp.GetString();
            }
        }

        if (string.IsNullOrEmpty(responseText))
        {
            return new GenerationResult
            {
                Tests = [],
                Errors = ["No response received from Azure Claude"]
            };
        }

        var tests = ParseTestsFromResponse(responseText);

        // Parse usage if available
        TokenUsage? tokenUsage = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            var inputTokens = usage.TryGetProperty("input_tokens", out var inp) ? inp.GetInt32() : 0;
            var outputTokens = usage.TryGetProperty("output_tokens", out var outp) ? outp.GetInt32() : 0;
            tokenUsage = new TokenUsage(inputTokens, outputTokens, inputTokens + outputTokens);
        }

        return new GenerationResult
        {
            Tests = tests,
            TokenUsage = tokenUsage
        };
    }

    private string GetApiKey(ProviderConfig provider)
    {
        var defaultEnv = _isAzure ? "AZURE_ANTHROPIC_API_KEY" : "ANTHROPIC_API_KEY";
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

        // Claude Opus 4.6
        if (modelLower.Contains("claude-opus-4.6") || modelLower.Contains("opus-4.6") || modelLower.Contains("opus4.6"))
            return 128000;

        // Claude Sonnet 4.6
        if (modelLower.Contains("claude-sonnet-4.6") || modelLower.Contains("sonnet-4.6") || modelLower.Contains("sonnet4.6"))
            return 64000;

        // Claude Haiku 4.5
        if (modelLower.Contains("claude-haiku-4.5") || modelLower.Contains("haiku-4.5") || modelLower.Contains("haiku4.5"))
            return 8192;

        // Claude 3.5 Sonnet and Claude 4 models (older)
        if (modelLower.Contains("claude-sonnet-4") || modelLower.Contains("claude-3-5-sonnet") || modelLower.Contains("claude-3.5-sonnet"))
            return 8192;

        // Claude 3 Opus
        if (modelLower.Contains("claude-3-opus") || modelLower.Contains("claude-opus"))
            return 4096;

        // Claude 3 Sonnet
        if (modelLower.Contains("claude-3-sonnet"))
            return 4096;

        // Claude 3 Haiku
        if (modelLower.Contains("claude-3-haiku") || modelLower.Contains("claude-haiku"))
            return 4096;

        // Claude 2.x
        if (modelLower.Contains("claude-2"))
            return 4096;

        // Default fallback
        return 4096;
    }
}
