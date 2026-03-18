using System.Diagnostics;

namespace Spectra.CLI.Agent;

/// <summary>
/// Provides GitHub token retrieval with automatic fallback to GitHub CLI.
/// </summary>
public static class GitHubCliTokenProvider
{
    private const string GitHubTokenEnvVar = "GITHUB_TOKEN";
    private const string GhAuthTokenCommand = "gh";
    private const string GhAuthTokenArgs = "auth token";

    /// <summary>
    /// Attempts to get a GitHub token from environment variable or GitHub CLI.
    /// </summary>
    /// <param name="customEnvVar">Optional custom environment variable name to check first.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An AuthResult indicating success or failure with instructions.</returns>
    public static async Task<AuthResult> TryGetTokenAsync(string? customEnvVar = null, CancellationToken ct = default)
    {
        // 1. Check custom environment variable if specified
        if (!string.IsNullOrEmpty(customEnvVar))
        {
            var customToken = Environment.GetEnvironmentVariable(customEnvVar);
            if (!string.IsNullOrEmpty(customToken))
            {
                return AuthResult.Success(customToken, $"environment ({customEnvVar})");
            }
        }

        // 2. Check GITHUB_TOKEN environment variable
        var envToken = Environment.GetEnvironmentVariable(GitHubTokenEnvVar);
        if (!string.IsNullOrEmpty(envToken))
        {
            return AuthResult.Success(envToken, "environment (GITHUB_TOKEN)");
        }

        // 3. Try GitHub CLI fallback
        var ghResult = await TryGetTokenFromGitHubCliAsync(ct);
        if (ghResult.IsAuthenticated)
        {
            return ghResult;
        }

        // 4. Return failure with setup instructions
        return AuthResult.Failure(
            "GitHub token not found",
            "Option 1: Set GITHUB_TOKEN environment variable",
            "Option 2: Install GitHub CLI and run: gh auth login",
            "",
            "For more information: spectra auth --help"
        );
    }

    /// <summary>
    /// Checks if GitHub CLI authentication is available without retrieving the token.
    /// </summary>
    public static async Task<bool> IsGitHubCliAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GhAuthTokenCommand,
                    Arguments = "auth status",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<AuthResult> TryGetTokenFromGitHubCliAsync(CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GhAuthTokenCommand,
                    Arguments = GhAuthTokenArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var tokenTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0)
            {
                var token = (await tokenTask).Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    return AuthResult.Success(token, "gh-cli");
                }
            }

            // gh CLI is installed but not authenticated
            var errorOutput = await errorTask;
            if (!string.IsNullOrEmpty(errorOutput) && errorOutput.Contains("not logged in", StringComparison.OrdinalIgnoreCase))
            {
                return AuthResult.Failure(
                    "GitHub CLI is installed but not authenticated",
                    "Run: gh auth login"
                );
            }

            return AuthResult.Failure("GitHub CLI failed to provide token");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // gh CLI is not installed
            return AuthResult.Failure("GitHub CLI is not installed");
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Failed to get token from GitHub CLI: {ex.Message}");
        }
    }
}
