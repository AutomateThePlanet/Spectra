using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using OpenAI;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// OpenAI-based critic implementation using gpt-4o-mini.
/// </summary>
public sealed class OpenAiCritic : ICriticRuntime
{
    private readonly CriticConfig _config;
    private readonly IChatClient _client;
    private readonly CriticPromptBuilder _promptBuilder;
    private readonly CriticResponseParser _responseParser;

    public string ModelName { get; }

    public OpenAiCritic(CriticConfig config, string apiKey)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ArgumentException.ThrowIfNullOrEmpty(apiKey);

        ModelName = config.GetEffectiveModel();

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey));
        _client = openAiClient.GetChatClient(ModelName).AsIChatClient();

        _promptBuilder = new CriticPromptBuilder();
        _responseParser = new CriticResponseParser();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetResponseAsync("Say 'ok'", cancellationToken: ct);
            return response != null;
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

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var options = new ChatOptions
            {
                Temperature = 0.3f,
                MaxOutputTokens = 2048
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            var response = await _client.GetResponseAsync(messages, options, cts.Token);
            stopwatch.Stop();

            if (response?.Text is null)
            {
                return VerificationResult.Unverified(ModelName, "No response from OpenAI");
            }

            return _responseParser.Parse(response.Text, ModelName, stopwatch.Elapsed);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return VerificationResult.Unverified(ModelName, "Request timed out");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            return VerificationResult.Unverified(ModelName, "Rate limit exceeded. Try again later.");
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
}
