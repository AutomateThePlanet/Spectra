using System.Collections.Concurrent;
using Spectra.Core.Git;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>One recorded git call: the full argument list and the environment SPECTRA set.</summary>
public sealed record RecordedGitCall(string Dir, IReadOnlyList<string> Args, IReadOnlyDictionary<string, string> Env)
{
    /// <summary>The subcommand: the first argument after <c>-C dir -c core.quotepath=false</c>.</summary>
    public string Subcommand => Args.Count > 4 ? Args[4] : "";
}

/// <summary>Spec 086: wraps a runner (the real one by default) and records every call.</summary>
public sealed class RecordingGitRunner(IGitProcessRunner? inner = null) : IGitProcessRunner
{
    private readonly IGitProcessRunner _inner = inner ?? new ProcessGitRunner();

    public ConcurrentQueue<RecordedGitCall> Calls { get; } = new();

    public Task<GitProcessResult> RunAsync(
        string workingDir, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, byte[]? stdin, CancellationToken ct)
    {
        Calls.Enqueue(new RecordedGitCall(workingDir, args.ToList(), new Dictionary<string, string>(env)));
        return _inner.RunAsync(workingDir, args, env, stdin, ct);
    }

    /// <summary>How many rename scans (<c>log … --diff-filter=R</c> over all of HEAD's history) ran.</summary>
    public int RenameScans => Calls.Count(c => c.Subcommand == "log" && c.Args.Contains("--diff-filter=R") && c.Args[^1] == "HEAD");
}

/// <summary>Spec 086: a runner that answers every call (or the calls for one path) with a fixed failure.</summary>
public sealed class FakeGitRunner(Func<string, IReadOnlyList<string>, GitProcessResult?> answer, IGitProcessRunner? fallback = null) : IGitProcessRunner
{
    public static FakeGitRunner NotInstalled() => new((_, _) => new GitProcessResult(-1, [], "", true));

    public const string DubiousOwnershipStderr =
        "fatal: detected dubious ownership in repository at '{0}'\nTo add an exception for this directory, call:\n\n\tgit config --global --add safe.directory {0}\n";

    public Task<GitProcessResult> RunAsync(
        string workingDir, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, byte[]? stdin, CancellationToken ct) =>
        answer(workingDir, args) is { } result
            ? Task.FromResult(result)
            : (fallback ?? new ProcessGitRunner()).RunAsync(workingDir, args, env, stdin, ct);
}
