using System.Diagnostics;
using System.Text;
using GitHub.Copilot.SDK;
using Spectra.CLI.Agent.Critic;
using Spectra.CLI.Services;
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
    private readonly TokenUsageTracker? _tracker;
    private readonly RunErrorTracker? _errorTracker;

    public string ModelName { get; }

    public string ProviderName => _criticConfig.Provider ?? "github-models";

    public CopilotCritic(
        CriticConfig criticConfig,
        TokenUsageTracker? tracker = null,
        RunErrorTracker? errorTracker = null)
    {
        _criticConfig = criticConfig ?? throw new ArgumentNullException(nameof(criticConfig));
        _promptBuilder = new CriticPromptBuilder();
        _responseParser = new CriticResponseParser();
        _tracker = tracker;
        _errorTracker = errorTracker;

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
        Spectra.CLI.Infrastructure.DebugLogger.AppendAi("critic ",
            $"CRITIC START test_id={testId} timeout={timeoutSeconds}s",
            ModelName, ProviderName, null, null);

        // Spec 040 follow-up: observer captures provider-reported token
        // counts from AssistantUsageEvent, with text.Length/4 fallback.
        var observer = new CopilotUsageObserver();
        // Built before try so the catch blocks can reference it for the fallback.
        var systemPromptForFallback = _promptBuilder.BuildSystemPrompt();
        var userPromptForFallback = _promptBuilder.BuildUserPrompt(test, relevantDocs);
        var fullPromptForFallback = $"{systemPromptForFallback}\n\n---\n\n{userPromptForFallback}";

        try
        {
            var service = await CopilotService.GetInstanceAsync(ct);

            // Create session
            await using var session = await service.CreateCriticSessionAsync(_criticConfig, ct);

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
                    case AssistantUsageEvent usage when usage.Data is { } usageData:
                        observer.RecordUsage(
                            (int)(usageData.InputTokens ?? 0),
                            (int)(usageData.OutputTokens ?? 0));
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
            await session.SendAsync(new MessageOptions { Prompt = fullPromptForFallback }, ct);

            // Wait for completion with timeout (driven by critic.timeout_seconds).
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var responseText = await completionSource.Task.WaitAsync(linkedCts.Token);

            stopwatch.Stop();

            // Grace window for AssistantUsageEvent ordering.
            await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(200), ct);
            var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(fullPromptForFallback, responseText);

            // Parse the response using existing parser
            var result = _responseParser.Parse(responseText, ModelName, stopwatch.Elapsed);
            _tracker?.Record("critic", ModelName, ProviderName, tokensIn, tokensOut, stopwatch.Elapsed, estimated);
            Spectra.CLI.Infrastructure.DebugLogger.AppendAi("critic ",
                $"CRITIC OK    test_id={testId} verdict={result.Verdict} score={result.Score:F2} elapsed={stopwatch.Elapsed.TotalSeconds:F1}s",
                ModelName, ProviderName, tokensIn, tokensOut, estimated);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(fullPromptForFallback, "");
            _tracker?.Record("critic", ModelName, ProviderName, tokensIn, tokensOut, stopwatch.Elapsed, estimated);

            // Spec 043: capture full timeout context to the error log + bump
            // the per-run error counter. The debug log line gets a see=
            // cross-reference when error logging is enabled.
            _errorTracker?.RecordError();
            Spectra.CLI.Infrastructure.ErrorLogger.Write(
                "critic",
                $"test_id={testId} configured_timeout={timeoutSeconds}s elapsed={stopwatch.Elapsed.TotalSeconds:F1}s",
                ex);
            Spectra.CLI.Infrastructure.DebugLogger.AppendAi("critic ",
                $"CRITIC TIMEOUT test_id={testId} configured_timeout={timeoutSeconds}s elapsed={stopwatch.Elapsed.TotalSeconds:F1}s"
                    + (Spectra.CLI.Infrastructure.ErrorLogger.Enabled ? $" see={Spectra.CLI.Infrastructure.ErrorLogger.LogFile}" : ""),
                ModelName, ProviderName, tokensIn, tokensOut, estimated);
            return CreateErrorResult($"Verification timed out after {timeoutSeconds}s — bump critic.timeout_seconds in spectra.config.json", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(fullPromptForFallback, "");
            _tracker?.Record("critic", ModelName, ProviderName, tokensIn, tokensOut, stopwatch.Elapsed, estimated);

            // Spec 043: classify the failure (rate limit vs. other) and
            // capture full exception context (type, message, stack, response
            // body when available) to the dedicated error log.
            _errorTracker?.Record(ex);
            var responseBody = ex.Data["ResponseBody"] as string;
            var retryAfter = ex.Data["RetryAfter"] as string;
            Spectra.CLI.Infrastructure.ErrorLogger.Write(
                "critic", $"test_id={testId} elapsed={stopwatch.Elapsed.TotalSeconds:F1}s",
                ex, responseBody, retryAfter);

            var isRateLimit = Spectra.CLI.Infrastructure.ErrorLogger.IsRateLimit(ex);
            var debugTag = isRateLimit ? "CRITIC RATE_LIMITED" : "CRITIC ERROR";
            Spectra.CLI.Infrastructure.DebugLogger.AppendAi("critic ",
                $"{debugTag} test_id={testId} exception={ex.GetType().Name} message=\"{ex.Message}\" elapsed={stopwatch.Elapsed.TotalSeconds:F1}s"
                    + (Spectra.CLI.Infrastructure.ErrorLogger.Enabled ? $" see={Spectra.CLI.Infrastructure.ErrorLogger.LogFile}" : ""),
                ModelName, ProviderName, tokensIn, tokensOut, estimated);
            return CreateErrorResult(ex.Message, stopwatch.Elapsed);
        }
    }

    private static string GetEffectiveModel(CriticConfig config)
    {
        if (!string.IsNullOrEmpty(config.Model))
            return config.Model;

        // Spec 041: default critic models. Cross-architecture when possible
        // so the critic catches hallucinations the generator can't see.
        var provider = config.Provider?.ToLowerInvariant() ?? "github-models";
        return provider switch
        {
            "anthropic" or "azure-anthropic" => "claude-haiku-4-5",
            "azure-deepseek" => "DeepSeek-V3-0324",
            "openai" or "azure-openai" => "gpt-5-mini",
            _ => "gpt-5-mini"
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
