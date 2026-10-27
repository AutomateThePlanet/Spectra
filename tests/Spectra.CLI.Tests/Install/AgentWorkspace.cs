using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Install;
using Spectra.CLI.Tests.Golden;

namespace Spectra.CLI.Tests.Install;

/// <summary>Spec 104: a throwaway repo root that runs <c>spectra init</c> (plain or with <c>--agent</c>) in-process.</summary>
internal sealed class AgentWorkspace : IDisposable
{
    public AgentWorkspace() => Directory.CreateDirectory(Root);

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "spectra-104-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary><c>spectra init</c> with no new flag — the Claude install.</summary>
    public async Task<int> InitAsync(bool force = false) =>
        await new InitHandler(Logger(), Root).HandleAsync(force);

    /// <summary><c>spectra init --agent &lt;agents&gt;</c>; returns the exit code and what it printed.</summary>
    public async Task<(int Exit, string Output, string Notices)> AgentsAsync(
        string agents, bool force = false, bool regenerate = false, bool dryRun = false, bool skipSkills = false)
    {
        var output = new StringWriter();
        var notices = new StringWriter();
        var exit = await AgentInitRunner.RunAsync(new InitHandler(Logger(), Root), Root, agents, force, skipSkills, regenerate,
            dryRun, output, notices, CancellationToken.None);
        return (exit, output.ToString(), notices.ToString());
    }

    public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public bool Exists(string relative) => File.Exists(PathOf(relative));

    public string Read(string relative) => File.ReadAllText(PathOf(relative));

    public void Write(string relative, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(relative))!);
        File.WriteAllText(PathOf(relative), content);
    }

    /// <summary>Every file, as <c>&lt;sha256&gt;  &lt;path&gt;</c> lines (line-ending-normalised hashes).</summary>
    public string Snapshot() => ClaudeInstallGoldenTests.Snapshot(Root);

    public IReadOnlyList<string> SnapshotLines() => Snapshot().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Repo-relative paths of every file in the workspace.</summary>
    public IReadOnlyList<string> Files() =>
        Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static ILogger<InitHandler> Logger() =>
        LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
}
