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
    // PROCESS-WIDE cache. The identity is stable for the lifetime of the process, so every
    // UserIdentityResolver instance returns the same value. This matters for correctness, not just
    // perf: each `spectra run` handler call builds a fresh RunServices (hence a fresh resolver), and
    // the run is stored/looked-up by identity (RunRepository.GetActiveRunByUserAsync). Resolving
    // per-instance meant a slow/blocked `git config user.name` spawn could make one call return the
    // git name and a later call fall back to Environment.UserName — two different identities in one
    // logical session — so `start` then `advance` would see "no active run". Caching once removes that
    // divergence and also collapses thousands of redundant `git` spawns (which an antivirus may
    // intercept) down to one. Real CLI runs are single-shot, so behaviour there is unchanged.
    private static string? _cachedIdentity;
    private static readonly object Gate = new();

    /// <summary>
    /// Gets the current user identity (git config user.name, else OS username). Resolved once per
    /// process and cached.
    /// </summary>
    public string GetCurrentUser()
    {
        if (_cachedIdentity is not null)
        {
            return _cachedIdentity;
        }

        lock (Gate)
        {
            if (_cachedIdentity is not null)
            {
                return _cachedIdentity;
            }

            var gitUser = GetGitUserName();
            _cachedIdentity = !string.IsNullOrWhiteSpace(gitUser) ? gitUser : Environment.UserName;
            return _cachedIdentity;
        }
    }

    /// <summary>
    /// Clears the cached identity (useful for testing).
    /// </summary>
    public void ClearCache()
    {
        lock (Gate)
        {
            _cachedIdentity = null;
        }
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

            // ReadToEnd already blocks until stdout closes; this generous wait only guards a process
            // that lingers after closing its pipe. On timeout, treat git as unavailable (kill + null)
            // rather than reading ExitCode (which would throw on a still-running process).
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { /* best-effort */ }
                return null;
            }

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
