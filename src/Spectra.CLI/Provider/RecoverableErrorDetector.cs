namespace Spectra.CLI.Provider;

/// <summary>
/// Detects recoverable errors that warrant provider fallback.
/// </summary>
public sealed class RecoverableErrorDetector
{
    private static readonly string[] RateLimitPatterns =
    [
        "rate limit",
        "too many requests",
        "429",
        "quota exceeded"
    ];

    private static readonly string[] AuthErrorPatterns =
    [
        "unauthorized",
        "401",
        "403",
        "invalid api key",
        "authentication failed"
    ];

    private static readonly string[] ServiceUnavailablePatterns =
    [
        "service unavailable",
        "503",
        "502",
        "gateway timeout",
        "504"
    ];

    /// <summary>
    /// Determines if an error is recoverable (should trigger fallback).
    /// </summary>
    public RecoverableErrorResult Analyze(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var message = exception.Message.ToLowerInvariant();

        // Check for rate limiting
        if (MatchesAny(message, RateLimitPatterns))
        {
            return new RecoverableErrorResult
            {
                IsRecoverable = true,
                ErrorType = RecoverableErrorType.RateLimited,
                SuggestedWait = TimeSpan.FromSeconds(30),
                Reason = "Rate limit exceeded"
            };
        }

        // Check for auth errors
        if (MatchesAny(message, AuthErrorPatterns))
        {
            return new RecoverableErrorResult
            {
                IsRecoverable = true,
                ErrorType = RecoverableErrorType.AuthenticationFailed,
                SuggestedWait = TimeSpan.Zero,
                Reason = "Authentication failed"
            };
        }

        // Check for service unavailable
        if (MatchesAny(message, ServiceUnavailablePatterns))
        {
            return new RecoverableErrorResult
            {
                IsRecoverable = true,
                ErrorType = RecoverableErrorType.ServiceUnavailable,
                SuggestedWait = TimeSpan.FromSeconds(10),
                Reason = "Service temporarily unavailable"
            };
        }

        // Check for timeout
        if (exception is TimeoutException or TaskCanceledException { InnerException: TimeoutException })
        {
            return new RecoverableErrorResult
            {
                IsRecoverable = true,
                ErrorType = RecoverableErrorType.Timeout,
                SuggestedWait = TimeSpan.FromSeconds(5),
                Reason = "Request timed out"
            };
        }

        // Not recoverable
        return new RecoverableErrorResult
        {
            IsRecoverable = false,
            ErrorType = RecoverableErrorType.None,
            Reason = exception.Message
        };
    }

    private static bool MatchesAny(string message, string[] patterns)
    {
        return patterns.Any(p => message.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Result of recoverable error analysis.
/// </summary>
public sealed record RecoverableErrorResult
{
    public required bool IsRecoverable { get; init; }
    public required RecoverableErrorType ErrorType { get; init; }
    public TimeSpan SuggestedWait { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// Type of recoverable error.
/// </summary>
public enum RecoverableErrorType
{
    None,
    RateLimited,
    AuthenticationFailed,
    ServiceUnavailable,
    Timeout
}
