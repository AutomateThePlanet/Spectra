using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Anthropic Claude-based critic implementation.
/// </summary>
public sealed class AnthropicCritic : ICriticRuntime
{
    private const string DefaultApiUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    private readonly CriticConfig _config;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly CriticPromptBuilder _promptBuilder;
    private readonly CriticResponseParser _responseParser;

    public string ModelName { get; }

    public AnthropicCritic(CriticConfig config, string apiKey)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        ModelName = config.GetEffectiveModel();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);

        _promptBuilder = new CriticPromptBuilder();
        _responseParser = new CriticResponseParser();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var request = BuildRequest("system", "Say 'ok'");
            var url = _config.BaseUrl ?? DefaultApiUrl;
            var response = await _httpClient.PostAsync(url, request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<VerificationResult> VerifyTestAsync(
        TestCase test,
        IReadOnlyList<SourceDocument> relevantDocs,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var systemPrompt = _promptBuilder.BuildSystemPrompt();
            var userPrompt = _promptBuilder.BuildUserPrompt(test, relevantDocs);

            var request = BuildRequest(systemPrompt, userPrompt);
            var url = _config.BaseUrl ?? DefaultApiUrl;

            var response = await _httpClient.PostAsync(url, request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return VerificationResult.Unverified(ModelName,
                    $"Anthropic API error: {response.StatusCode} - {responseContent}");
            }

            var text = ExtractTextFromResponse(responseContent);
            stopwatch.Stop();

            return _responseParser.Parse(text, ModelName, stopwatch.Elapsed);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return VerificationResult.Unverified(ModelName, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            return VerificationResult.Unverified(ModelName, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return VerificationResult.Unverified(ModelName, $"Verification failed: {ex.Message}");
        }
    }

    private StringContent BuildRequest(string systemPrompt, string userPrompt)
    {
        var request = new
        {
            model = ModelName,
            max_tokens = 2048,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return content;
    }

    private static string ExtractTextFromResponse(string response)
    {
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("content", out var content) &&
            content.GetArrayLength() > 0)
        {
            var firstContent = content[0];
            if (firstContent.TryGetProperty("text", out var text))
            {
                return text.GetString() ?? "";
            }
        }

        return "";
    }
}
