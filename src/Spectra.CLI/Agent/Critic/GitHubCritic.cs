using System.Diagnostics;
using Azure;
using Azure.AI.Inference;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// GitHub Models-based critic implementation.
/// </summary>
public sealed class GitHubCritic : ICriticRuntime
{
    private const string BaseUrl = "https://models.inference.ai.azure.com";

    private readonly CriticConfig _config;
    private readonly ChatCompletionsClient _client;
    private readonly CriticPromptBuilder _promptBuilder;
    private readonly CriticResponseParser _responseParser;

    public string ModelName { get; }

    public GitHubCritic(CriticConfig config, string token)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ArgumentException.ThrowIfNullOrEmpty(token);

        ModelName = config.GetEffectiveModel();

        var endpoint = new Uri(config.BaseUrl ?? BaseUrl);
        _client = new ChatCompletionsClient(endpoint, new AzureKeyCredential(token));

        _promptBuilder = new CriticPromptBuilder();
        _responseParser = new CriticResponseParser();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var requestOptions = new ChatCompletionsOptions
            {
                Model = ModelName,
                Messages = { new ChatRequestUserMessage("Say 'ok'") },
                MaxTokens = 10
            };

            var response = await _client.CompleteAsync(requestOptions, ct);
            return response?.Value != null;
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

            var requestOptions = new ChatCompletionsOptions
            {
                Model = ModelName,
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                Temperature = 0.3f,
                MaxTokens = 2048
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            var response = await _client.CompleteAsync(requestOptions, cts.Token);
            stopwatch.Stop();

            var completion = response?.Value;
            if (completion is null || string.IsNullOrEmpty(completion.Content))
            {
                return VerificationResult.Unverified(ModelName, "No response from GitHub Models");
            }

            return _responseParser.Parse(completion.Content, ModelName, stopwatch.Elapsed);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return VerificationResult.Unverified(ModelName, "Request timed out");
        }
        catch (RequestFailedException ex)
        {
            return VerificationResult.Unverified(ModelName, $"API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return VerificationResult.Unverified(ModelName, $"Verification failed: {ex.Message}");
        }
    }
}
