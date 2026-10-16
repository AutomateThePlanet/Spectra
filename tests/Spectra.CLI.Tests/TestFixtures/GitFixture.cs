using System.Diagnostics;
using System.Text;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 086: builds throwaway git repositories with the test's own git — every write command is allowed here. These
/// fixture commands are the test's, not SPECTRA's; SPECTRA only ever goes through <c>GitReader</c>.
/// </summary>
public static class GitFixture
{
    /// <summary>Runs git in <paramref name="dir"/> and returns stdout; throws on a non-zero exit.</summary>
    public static string Git(string dir, params string[] args) => Git(dir, null, args);

    public static string Git(string dir, IReadOnlyDictionary<string, string>? env, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        if (env is not null)
        {
            foreach (var (key, value) in env) info.Environment[key] = value;
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed in {dir}: {stderr.Result}");
        }

        return stdout.Result;
    }

    /// <summary>The identity and LF content settings every fixture repository gets.</summary>
    private const string IdentityConfig =
        "[user]\n\tname = Spectra Test\n\temail = test@example.com\n[core]\n\tautocrlf = false\n";

    /// <summary><c>git init</c> with a fixed identity, LF content and no hooks of the user's environment.</summary>
    /// <remarks>Spec 099 F7: the settings are appended to <c>.git/config</c> (the file <c>git config</c> writes) in one
    /// write instead of four <c>git config</c> processes; git merges repeated sections, so the values are the same.</remarks>
    public static void Init(string dir)
    {
        Directory.CreateDirectory(dir);
        Git(dir, "init", "-q", "-b", "main");
        AppendConfig(dir, IdentityConfig + "[commit]\n\tgpgsign = false\n");
    }

    /// <summary>Spec 099 F7: the identity and LF settings (what <c>CloneInto</c> sets), in one write.</summary>
    public static void AppendIdentityConfig(string dir) => AppendConfig(dir, IdentityConfig);

    private static void AppendConfig(string dir, string text) =>
        File.AppendAllText(Path.Combine(dir, ".git", "config"), text);

    /// <summary>Stages everything and commits, optionally at a fixed date. Returns the commit hash.</summary>
    public static string Commit(string dir, string message, DateTimeOffset? date = null)
    {
        Git(dir, "add", "-A");
        var env = date is { } d
            ? new Dictionary<string, string>
            {
                ["GIT_AUTHOR_DATE"] = d.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture),
                ["GIT_COMMITTER_DATE"] = d.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture)
            }
            : null;
        Git(dir, env, "commit", "-q", "--allow-empty", "-m", message);
        return HeadHash(dir);
    }

    /// <summary>
    /// Spec 099 F7: the commit HEAD points at, read from the loose ref git just wrote (no process); <c>git rev-parse</c>
    /// when HEAD isn't a branch with a loose ref in a <c>.git</c> folder (a packed ref, a <c>.git</c> file).
    /// </summary>
    private static string HeadHash(string dir)
    {
        var gitDir = Path.Combine(dir, ".git");
        var headFile = Path.Combine(gitDir, "HEAD");
        if (Directory.Exists(gitDir) && File.Exists(headFile))
        {
            var head = File.ReadAllText(headFile).Trim();
            if (head.StartsWith("ref: ", StringComparison.Ordinal))
            {
                var refFile = Path.Combine(gitDir, head[5..].Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(refFile))
                {
                    return File.ReadAllText(refFile).Trim();
                }
            }
        }

        return Git(dir, "rev-parse", "HEAD").Trim();
    }

    /// <summary><c>git mv</c>, creating the target folder.</summary>
    public static void Move(string dir, string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, to))!);
        Git(dir, "mv", from, to);
    }

    /// <summary>Adds <paramref name="childRepo"/> as a submodule at <paramref name="path"/> and commits.</summary>
    public static void AddSubmodule(string parent, string childRepo, string path)
    {
        Git(parent, "-c", "protocol.file.allow=always", "submodule", "add", "-q", childRepo, path);
        Commit(parent, $"add submodule {path}");
    }

    /// <summary>Every <c>index.lock</c> under any <c>.git</c> folder below <paramref name="root"/> (incl. <c>.git/modules/**</c>).</summary>
    public static IReadOnlyList<string> IndexLocks(string root) =>
        Directory.GetFiles(root, "index.lock", SearchOption.AllDirectories)
            .Where(f => f.Replace('\\', '/').Contains("/.git/", StringComparison.Ordinal))
            .ToList();

    /// <summary>Deletes a directory that may contain read-only git objects.</summary>
    public static void DeleteTree(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
