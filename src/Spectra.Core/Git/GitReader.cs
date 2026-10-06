using System.Text;

namespace Spectra.Core.Git;

/// <summary>
/// The result of one git call. <see cref="Unavailable"/> is the reason git could not answer for that path (FR-003):
/// <c>no git</c>, <c>not in a git repository</c>, <c>repository not readable (dubious ownership)</c> or
/// <c>git failed: …</c>.
/// </summary>
public sealed record GitResult(bool Ok, string? Unavailable, byte[] Stdout)
{
    /// <summary>Stdout decoded as UTF-8.</summary>
    public string Text => Encoding.UTF8.GetString(Stdout);
}

/// <summary>
/// Spec 086 (D1, FR-001–FR-003, research R1): the one place SPECTRA calls git. Read-only by construction: only
/// <see cref="AllowedSubcommands"/> run, every call is <c>git -C &lt;dir&gt; -c core.quotepath=false &lt;sub&gt; …</c>
/// with optional locks off and terminal prompts off, and git problems come back as an unavailable reason, never as an
/// exception. The runner is a constructor parameter, so Core has no test hooks.
/// </summary>
public sealed class GitReader
{
    public const string NoGit = "no git";
    public const string NotARepository = "not in a git repository";
    public const string DubiousOwnership = "repository not readable (dubious ownership)";

    public static readonly IReadOnlySet<string> AllowedSubcommands =
        new HashSet<string>(StringComparer.Ordinal) { "rev-parse", "log", "show", "ls-files", "cat-file" };

    /// <summary>Set on every call: git takes no optional locks (no <c>index.lock</c>) and never prompts.</summary>
    public static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GIT_OPTIONAL_LOCKS"] = "0",
        ["GIT_TERMINAL_PROMPT"] = "0"
    };

    private readonly IGitProcessRunner _runner;

    public GitReader(IGitProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public Task<GitResult> RunAsync(string dir, string subcommand, params string[] args) =>
        RunAsync(dir, subcommand, args, stdin: null, CancellationToken.None);

    /// <summary>
    /// Runs <c>git -C <paramref name="dir"/> -c core.quotepath=false <paramref name="subcommand"/> <paramref name="args"/></c>.
    /// Throws <see cref="InvalidOperationException"/> for a subcommand outside the allow-list, before any process
    /// starts; every git problem is returned as <see cref="GitResult.Unavailable"/>.
    /// </summary>
    public async Task<GitResult> RunAsync(
        string dir, string subcommand, IReadOnlyList<string> args, byte[]? stdin, CancellationToken ct)
    {
        if (!AllowedSubcommands.Contains(subcommand))
        {
            throw new InvalidOperationException($"git {subcommand} is not allowed: SPECTRA only reads repositories.");
        }

        var all = new List<string>(args.Count + 5) { "-C", dir, "-c", "core.quotepath=false", subcommand };
        all.AddRange(args);
        var result = await _runner.RunAsync(dir, all, Environment, stdin, ct);
        if (result.NotInstalled)
        {
            return new GitResult(false, NoGit, []);
        }

        return result.ExitCode == 0
            ? new GitResult(true, null, result.Stdout)
            : new GitResult(false, Reason(result.Stderr, result.ExitCode), result.Stdout);
    }

    /// <summary>Maps git's stderr to an unavailable reason.</summary>
    public static string Reason(string stderr, int exitCode)
    {
        if (stderr.Contains("dubious ownership", StringComparison.OrdinalIgnoreCase))
        {
            return DubiousOwnership;
        }

        if (stderr.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
        {
            return NotARepository;
        }

        var first = stderr.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return $"git failed: {first ?? $"exit code {exitCode}"}";
    }
}
