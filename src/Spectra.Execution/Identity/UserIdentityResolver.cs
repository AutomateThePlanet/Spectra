using System.Diagnostics;

namespace Spectra.MCP.Identity;

/// <summary>
/// Interface for resolving user identity.
/// </summary>
public interface IUserIdentityResolver
{
    /// <summary>
    /// Gets the current user identity.
    /// </summary>
    string GetCurrentUser();
}

/// <summary>
/// Resolves user identity from git config or OS username.
/// Priority: git config user.name > OS username
/// </summary>
public sealed class UserIdentityResolver : IUserIdentityResolver
{
    private string? _cachedIdentity;

    /// <summary>
    /// Gets the current user identity.
    /// </summary>
    public string GetCurrentUser()
    {
        if (_cachedIdentity is not null)
        {
            return _cachedIdentity;
        }

        // Try git config first
        var gitUser = GetGitUserName();
        if (!string.IsNullOrWhiteSpace(gitUser))
        {
            _cachedIdentity = gitUser;
            return _cachedIdentity;
        }

        // Fall back to OS username
        _cachedIdentity = Environment.UserName;
        return _cachedIdentity;
    }

    /// <summary>
    /// Clears the cached identity (useful for testing).
    /// </summary>
    public void ClearCache()
    {
        _cachedIdentity = null;
    }

    private static string? GetGitUserName()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "config user.name",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(1000);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : null;
        }
        catch
        {
            // Git not available or failed
            return null;
        }
    }
}
