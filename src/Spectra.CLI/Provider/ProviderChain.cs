using Spectra.CLI.Agent;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Provider;

/// <summary>
/// Manages a chain of AI providers with automatic fallback.
/// </summary>
public sealed class ProviderChain
{
    private readonly IReadOnlyList<ProviderConfig> _providers;
    private readonly RecoverableErrorDetector _errorDetector;
    private readonly Func<ProviderConfig, IAgentRuntime> _agentFactory;

    public ProviderChain(
        IReadOnlyList<ProviderConfig> providers,
        Func<ProviderConfig, IAgentRuntime>? agentFactory = null)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _errorDetector = new RecoverableErrorDetector();
        _agentFactory = agentFactory ?? (config => AgentFactory.CreateFromProvider(config));
    }

    /// <summary>
    /// Event raised when falling back to next provider.
    /// </summary>
    public event Action<string, string, string>? OnFallback;

    /// <summary>
    /// Executes an operation with automatic provider fallback.
    /// </summary>
    public async Task<ChainResult<T>> ExecuteAsync<T>(
        Func<IAgentRuntime, CancellationToken, Task<T>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_providers.Count == 0)
        {
            return ChainResult<T>.Failure("No providers configured");
        }

        var attempts = new List<ProviderAttempt>();

        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();

            var agent = _agentFactory(provider);
            var attempt = new ProviderAttempt
            {
                ProviderName = provider.Name,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                if (!await agent.IsAvailableAsync(ct))
                {
                    attempt.Success = false;
                    attempt.Error = "Provider not available";
                    attempts.Add(attempt);
                    continue;
                }

                var result = await operation(agent, ct);
                attempt.Success = true;
                attempts.Add(attempt);

                return ChainResult<T>.Success(result, provider.Name, attempts);
            }
            catch (Exception ex)
            {
                attempt.Success = false;
                attempt.Error = ex.Message;
                attempts.Add(attempt);

                var errorResult = _errorDetector.Analyze(ex);

                if (!errorResult.IsRecoverable)
                {
                    return ChainResult<T>.Failure($"Unrecoverable error: {ex.Message}", attempts);
                }

                // Notify about fallback
                var nextProvider = _providers.SkipWhile(p => p != provider).Skip(1).FirstOrDefault();
                if (nextProvider is not null)
                {
                    OnFallback?.Invoke(provider.Name, nextProvider.Name, errorResult.Reason);

                    // Wait before retry if suggested
                    if (errorResult.SuggestedWait > TimeSpan.Zero)
                    {
                        await Task.Delay(errorResult.SuggestedWait, ct);
                    }
                }
            }
        }

        return ChainResult<T>.Failure("All providers failed", attempts);
    }
}

/// <summary>
/// Result of a provider chain operation.
/// </summary>
public sealed record ChainResult<T>
{
    public required bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? UsedProvider { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<ProviderAttempt> Attempts { get; init; } = [];

    public static ChainResult<T> Success(T value, string provider, IReadOnlyList<ProviderAttempt> attempts)
    {
        return new ChainResult<T>
        {
            IsSuccess = true,
            Value = value,
            UsedProvider = provider,
            Attempts = attempts
        };
    }

    public static ChainResult<T> Failure(string error, IReadOnlyList<ProviderAttempt>? attempts = null)
    {
        return new ChainResult<T>
        {
            IsSuccess = false,
            Error = error,
            Attempts = attempts ?? []
        };
    }
}

/// <summary>
/// Record of a provider attempt.
/// </summary>
public sealed record ProviderAttempt
{
    public required string ProviderName { get; init; }
    public required DateTime StartedAt { get; init; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
