using System.Diagnostics;
using System.Text;
using GitHub.Copilot.SDK;
using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Critic agent for verifying tests using the Copilot SDK with a separate session.
/// Uses a fast/cheap model for efficient verification.
/// </summary>
public sealed class CopilotCritic : ICriticRuntime
{
    private readonly CriticConfig _criticConfig;
    private readonly CriticPromptBuilder _promptBuilder;
    private readonly CriticResponseParser _responseParser;

    public string ModelName { get; }

    public CopilotCritic(CriticConfig criticConfig)
    {
        _criticConfig = criticConfig ?? throw new ArgumentNullException(nameof(criticConfig));
        _promptBuilder = new CriticPromptBuilder();
        _responseParser = new CriticResponseParser();

        ModelName = GetEffectiveModel(criticConfig);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var (available, error) = await CopilotService.CheckAvailabilityAsync(ct);
            return available;
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
        ArgumentNullException.ThrowIfNull(test);
        var testId = test.Id ?? "?";

        // v1.43.0: per-call critic timeout is now driven by
        // critic.timeout_seconds (was hardcoded 2 min, ignoring the config).
        // Surface model + timeout in the debug log so cost can be attributed
        // — every critic call is one billable API call to the critic provider.
        var timeoutSeconds = Math.Max(30, _criticConfig.TimeoutSeconds);
        Spectra.CLI.Infrastructure.DebugLogger.Append("critic ",
            $"CRITIC START test_id={testId} model={ModelName} timeout={timeoutSeconds}s");

        try
        {
            var service = await CopilotService.GetInstanceAsync(ct);

            // Create session
            await using var session = await service.CreateCriticSessionAsync(_criticConfig, ct);

            // Build prompts using existing builder
            var systemPrompt = _promptBuilder.BuildSystemPrompt();
            var userPrompt = _promptBuilder.BuildUserPrompt(test, relevantDocs);

            // Combine system and user prompts
            var fullPrompt = $"{systemPrompt}\n\n---\n\n{userPrompt}";

            // Collect response through events
            var responseBuilder = new StringBuilder();
            var completionSource = new TaskCompletionSource<string>();

            using var subscription = session.On(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        responseBuilder.Append(delta.Data?.DeltaContent ?? "");
                        break;
                    case AssistantMessageEvent message:
                        responseBuilder.Append(message.Data?.Content ?? "");
                        break;
                    case SessionIdleEvent:
                        if (!completionSource.Task.IsCompleted)
                        {
                            completionSource.TrySetResult(responseBuilder.ToString());
                        }
                        break;
                    case SessionErrorEvent error:
                        if (!completionSource.Task.IsCompleted)
                        {
                            completionSource.TrySetException(
                                new Exception(error.Data?.Message ?? "Unknown error"));
                        }
                        break;
                }
            });

            // Send verification request
            await session.SendAsync(new MessageOptions { Prompt = fullPrompt }, ct);

            // Wait for completion with timeout (driven by critic.timeout_seconds).
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var responseText = await completionSource.Task.WaitAsync(linkedCts.Token);

            stopwatch.Stop();

            // Parse the response using existing parser
            var result = _responseParser.Parse(responseText, ModelName, stopwatch.Elapsed);
            Spectra.CLI.Infrastructure.DebugLogger.Append("critic ",
                $"CRITIC OK    test_id={testId} verdict={result.Verdict} score={result.Score:F2} elapsed={stopwatch.Elapsed.TotalSeconds:F1}s");
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Spectra.CLI.Infrastructure.DebugLogger.Append("critic ",
                $"CRITIC TIMEOUT test_id={testId} model={ModelName} configured_timeout={timeoutSeconds}s elapsed={stopwatch.Elapsed.TotalSeconds:F1}s");
            return CreateErrorResult($"Verification timed out after {timeoutSeconds}s — bump critic.timeout_seconds in spectra.config.json", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Spectra.CLI.Infrastructure.DebugLogger.Append("critic ",
                $"CRITIC ERROR test_id={testId} exception={ex.GetType().Name} message=\"{ex.Message}\" elapsed={stopwatch.Elapsed.TotalSeconds:F1}s");
            return CreateErrorResult(ex.Message, stopwatch.Elapsed);
        }
    }

    private static string GetEffectiveModel(CriticConfig config)
    {
        if (!string.IsNullOrEmpty(config.Model))
            return config.Model;

        // Default to fast/cheap models for verification
        var provider = config.Provider?.ToLowerInvariant() ?? "github-models";
        return provider switch
        {
            "anthropic" or "azure-anthropic" => "claude-haiku-4-5-20250514",
            "azure-deepseek" => "DeepSeek-V3-0324",
            "openai" or "azure-openai" => "gpt-4o-mini",
            "google" => "gemini-2.0-flash",
            _ => "gpt-4o-mini"
        };
    }

    private VerificationResult CreateErrorResult(string error, TimeSpan duration)
    {
        return new VerificationResult
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.0,
            Findings = [],
            CriticModel = ModelName,
            Duration = duration,
            Errors = [$"Copilot SDK error: {error}"]
        };
    }
}

/// <summary>
/// Factory for creating Copilot SDK-based critics.
/// </summary>
public static class CopilotCriticFactory
{
    /// <summary>
    /// Result of creating a critic.
    /// </summary>
    public sealed record CreateResult(
        bool Success,
        ICriticRuntime? Critic,
        string? ErrorMessage);

    /// <summary>
    /// Tries to create a Copilot SDK critic from configuration.
    /// </summary>
    public static async Task<CreateResult> TryCreateAsync(
        CriticConfig? config,
        CancellationToken ct = default)
    {
        if (config is null || !config.Enabled)
        {
            return new CreateResult(false, null, "Critic not configured or disabled");
        }

        // Check Copilot availability
        var (available, error) = await CopilotService.CheckAvailabilityAsync(ct);
        if (!available)
        {
            return new CreateResult(false, null, error ?? "Copilot SDK not available");
        }

        var critic = new CopilotCritic(config);
        return new CreateResult(true, critic, null);
    }

    /// <summary>
    /// Creates a critic synchronously (validation only, no async check).
    /// </summary>
    public static CreateResult TryCreate(CriticConfig? config)
    {
        if (config is null || !config.Enabled)
        {
            return new CreateResult(false, null, "Critic not configured or disabled");
        }

        // Create without async validation - will fail on first use if unavailable
        var critic = new CopilotCritic(config);
        return new CreateResult(true, critic, null);
    }
}
