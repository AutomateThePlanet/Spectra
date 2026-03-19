using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Google Gemini-based critic implementation.
/// </summary>
public sealed class GoogleCritic : ICriticRuntime
{
    private readonly CriticConfig _config;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly CriticPromptBuilder _promptBuilder;
    private readonly CriticResponseParser _responseParser;

    public string ModelName { get; }

    public GoogleCritic(CriticConfig config, string apiKey)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        ModelName = config.GetEffectiveModel();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        _promptBuilder = new CriticPromptBuilder();
        _responseParser = new CriticResponseParser();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var url = BuildApiUrl();
            var request = BuildRequest("Say 'ok'");
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

            var url = BuildApiUrl();
            var request = BuildRequest(systemPrompt + "\n\n" + userPrompt);

            var response = await _httpClient.PostAsync(url, request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return VerificationResult.Unverified(ModelName,
                    $"Gemini API error: {response.StatusCode} - {responseContent}");
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

    private string BuildApiUrl()
    {
        var baseUrl = _config.BaseUrl ?? "https://generativelanguage.googleapis.com/v1beta";
        return $"{baseUrl}/models/{ModelName}:generateContent?key={_apiKey}";
    }

    private static StringContent BuildRequest(string prompt)
    {
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 2048
            }
        };

        var json = JsonSerializer.Serialize(request);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string ExtractTextFromResponse(string response)
    {
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                var firstPart = parts[0];
                if (firstPart.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "";
                }
            }
        }

        return "";
    }
}
