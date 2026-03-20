using Spectra.CLI.Agent;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Provider;

/// <summary>
/// Manages a chain of AI providers with automatic fallback.
/// Note: With the Copilot SDK consolidation, provider fallback is handled by the SDK itself.
/// This class is kept for backwards compatibility but will use the single Copilot runtime.
/// </summary>
public sealed class ProviderChain
{
    private readonly IReadOnlyList<ProviderConfig> _providers;
    private readonly RecoverableErrorDetector _errorDetector;
    private readonly SpectraConfig? _config;

    public ProviderChain(
        IReadOnlyList<ProviderConfig> providers,
        SpectraConfig? config = null)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _errorDetector = new RecoverableErrorDetector();
        _config = config;
    }

    /// <summary>
    /// Event raised when falling back to next provider.
    /// </summary>
#pragma warning disable CS0067 // Event is never used
    public event Action<string, string, string>? OnFallback;
#pragma warning restore CS0067

    /// <summary>
    /// Executes an operation with automatic provider fallback.
    /// With Copilot SDK, this now uses a single runtime.
    /// </summary>
    public async Task<ChainResult<T>> ExecuteAsync<T>(
        Func<IAgentRuntime, CancellationToken, Task<T>> operation,
        string basePath,
        string testsPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_providers.Count == 0)
        {
            return ChainResult<T>.Failure("No providers configured");
        }

        if (_config is null)
        {
            return ChainResult<T>.Failure("SpectraConfig is required for Copilot SDK agent creation");
        }

        var attempts = new List<ProviderAttempt>();

        // With Copilot SDK, we create a single agent and try once
        var createResult = await AgentFactory.CreateAgentAsync(
            _config,
            basePath,
            testsPath,
            null,
            ct);

        var attempt = new ProviderAttempt
        {
            ProviderName = createResult.ProviderName ?? "copilot-sdk",
            StartedAt = DateTime.UtcNow
        };

        if (!createResult.Success)
        {
            attempt.Success = false;
            attempt.Error = createResult.AuthResult?.ErrorMessage ?? "Failed to create agent";
            attempts.Add(attempt);
            return ChainResult<T>.Failure(attempt.Error, attempts);
        }

        var agent = createResult.Agent!;

        try
        {
            if (!await agent.IsAvailableAsync(ct))
            {
                attempt.Success = false;
                attempt.Error = "Provider not available";
                attempts.Add(attempt);
                return ChainResult<T>.Failure("Provider not available", attempts);
            }

            var result = await operation(agent, ct);
            attempt.Success = true;
            attempts.Add(attempt);

            return ChainResult<T>.Success(result, createResult.ProviderName ?? "copilot-sdk", attempts);
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

            return ChainResult<T>.Failure($"Error: {ex.Message}", attempts);
        }
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
