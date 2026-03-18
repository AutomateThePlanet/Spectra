namespace Spectra.CLI.Agent;

/// <summary>
/// Represents the result of an authentication attempt for an AI provider.
/// </summary>
public sealed record AuthResult
{
    /// <summary>
    /// Gets whether authentication was successful.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets the authentication token if successful.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Gets the source of the token (e.g., "environment", "gh-cli").
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the error message if authentication failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets setup instructions for the user if authentication failed.
    /// </summary>
    public IReadOnlyList<string> SetupInstructions { get; init; } = [];

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    public static AuthResult Success(string token, string source) => new()
    {
        IsAuthenticated = true,
        Token = token,
        Source = source
    };

    /// <summary>
    /// Creates a failed authentication result with instructions.
    /// </summary>
    public static AuthResult Failure(string errorMessage, params string[] instructions) => new()
    {
        IsAuthenticated = false,
        ErrorMessage = errorMessage,
        SetupInstructions = instructions
    };
}
