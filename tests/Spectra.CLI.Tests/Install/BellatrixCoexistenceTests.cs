using System.Diagnostics;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Install;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Install;

/// <summary>
/// Spec 104 (SC-003): SPECTRA and BELLATRIX Agent installed for the same agents in one repo, in both orders — neither
/// changes the other's files, the marker blocks are distinct, and every skill and subagent name is unique.
/// </summary>
/// <remarks>
/// <para>The fixture <c>TestFixtures/BellatrixInit/</c> is the recorded output of
/// <c>bellatrix init --agent claude,copilot,cursor,devin,codex,gemini,antigravity,kiro,opencode --language csharp --domains web</c>
/// (bellatrix.cli 3.1.1, see <c>BellatrixInit.recorded.txt</c>). "BELLATRIX after SPECTRA" replays it the way
/// <c>bellatrix init</c> writes: its own files, plus one appended block / line in the human-owned host files.</para>
/// <para>The <see cref="LiveBellatrixFact"/> tests run the real tool instead, when <c>bellatrix</c> is on PATH.</para>
/// </remarks>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class BellatrixCoexistenceTests : IDisposable
{
    private const string Agents = "claude,copilot,cursor,devin,codex,gemini,antigravity,kiro,opencode";
    private static readonly string[] HostFiles = ["AGENTS.md", "GEMINI.md", "CLAUDE.md"];

    private readonly AgentWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void Fixture_SharesNoPathWithSpectra_ExceptTheHumanOwnedHostFiles()
    {
        var bellatrix = FixtureFiles().ToHashSet(StringComparer.Ordinal);
        Assert.Equal(70, bellatrix.Count);
        Assert.All(bellatrix.Where(p => !HostFiles.Contains(p)), p =>
            Assert.True(p.Contains("bellatrix", StringComparison.Ordinal), $"fixture path '{p}' is outside BELLATRIX's namespace"));
    }

    [Fact]
    public async Task BellatrixFirst_ThenSpectra_LeavesEveryBellatrixFileAndBlockIntact()
    {
        CopyFixture();
        var bellatrixBefore = HashesOf(FixtureFiles());

        var (exit, _, notices) = await _ws.AgentsAsync(Agents);

        Assert.True(exit == ExitCodes.Success, notices);
        foreach (var (path, hash) in bellatrixBefore.Where(kv => !HostFiles.Contains(kv.Key)))
        {
            Assert.True(hash == AgentManifest.HashOfFile(_ws.PathOf(path)), $"SPECTRA changed BELLATRIX's '{path}'");
        }

        foreach (var host in new[] { "AGENTS.md", "GEMINI.md" })
        {
            Assert.StartsWith(Fixture(host), _ws.Read(host), StringComparison.Ordinal); // BELLATRIX's bytes come first, untouched
            Assert.Contains(MarkerBlock.Open, _ws.Read(host), StringComparison.Ordinal);
        }

        Assert.Equal(Fixture("CLAUDE.md"), _ws.Read("CLAUDE.md")); // SPECTRA never writes CLAUDE.md
        AssertNamesUnique();
    }

    [Fact]
    public async Task SpectraFirst_ThenBellatrix_LeavesEverySpectraFileAndBlockIntact()
    {
        await _ws.AgentsAsync(Agents);
        var spectra = SpectraOwnedHashes();

        ReplayBellatrixInit();

        foreach (var (path, hash) in spectra)
        {
            Assert.True(hash == AgentManifest.HashOfFile(_ws.PathOf(path)), $"BELLATRIX's init changed SPECTRA's '{path}'");
        }

        foreach (var host in new[] { "AGENTS.md", "GEMINI.md" })
        {
            var text = _ws.Read(host);
            Assert.Equal(1, Count(text, MarkerBlock.Open));
            Assert.Equal(1, Count(text, "<!-- >>> bellatrix >>> -->"));
        }

        // A SPECTRA re-run after BELLATRIX's init changes nothing at all.
        var before = _ws.Snapshot();
        await _ws.AgentsAsync(Agents);
        Assert.Equal(before, _ws.Snapshot());
        AssertNamesUnique();
    }

    [LiveBellatrixFact]
    public async Task Live_BellatrixFirst_ThenSpectra()
    {
        RunBellatrix();
        var bellatrix = HashesOf(_ws.Files().Where(p => p.Contains("bellatrix", StringComparison.Ordinal)));

        await _ws.AgentsAsync(Agents);

        foreach (var (path, hash) in bellatrix)
        {
            Assert.True(hash == AgentManifest.HashOfFile(_ws.PathOf(path)), $"SPECTRA changed BELLATRIX's '{path}'");
        }

        AssertNamesUnique();
    }

    [LiveBellatrixFact]
    public async Task Live_SpectraFirst_ThenBellatrix()
    {
        await _ws.AgentsAsync(Agents);
        var spectra = SpectraOwnedHashes();

        RunBellatrix();

        foreach (var (path, hash) in spectra)
        {
            Assert.True(hash == AgentManifest.HashOfFile(_ws.PathOf(path)), $"bellatrix init changed SPECTRA's '{path}'");
        }

        AssertNamesUnique();
    }

    /// <summary>Every skill folder and subagent across both tools has a unique name; the two marker blocks differ.</summary>
    private void AssertNamesUnique()
    {
        var skillNames = _ws.Files()
            .Where(p => p.EndsWith("/SKILL.md", StringComparison.Ordinal))
            .Select(p => p.Split('/')[^2])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.All(skillNames, n => Assert.True(
            n.StartsWith("spectra-", StringComparison.Ordinal) ^ n.StartsWith("bellatrix-", StringComparison.Ordinal),
            $"skill '{n}' is in neither namespace"));

        var subagentFiles = _ws.Files().Where(p => p.Contains("/agents/", StringComparison.Ordinal)).ToArray();
        Assert.Contains(subagentFiles, p => p.Contains("spectra-critic", StringComparison.Ordinal));
        Assert.Contains(subagentFiles, p => p.Contains("bellatrix-web-locator-specialist", StringComparison.Ordinal));
        Assert.DoesNotContain(subagentFiles, p => p.Contains("spectra-", StringComparison.Ordinal) && p.Contains("bellatrix-", StringComparison.Ordinal));
        Assert.NotEqual(MarkerBlock.Open, "<!-- >>> bellatrix >>> -->");
    }

    private Dictionary<string, string> SpectraOwnedHashes()
    {
        var paths = AgentSelection.Names.Where(n => n != "claude")
            .SelectMany(n => AgentManifest.Load(_ws.Root, n)?.Files.Select(f => f.Path) ?? [])
            .Concat(_ws.Files().Where(p => p.StartsWith(".claude/", StringComparison.Ordinal) && p.Contains("spectra-", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal);
        return HashesOf(paths);
    }

    private Dictionary<string, string> HashesOf(IEnumerable<string> paths) =>
        paths.ToDictionary(p => p, p => AgentManifest.HashOfFile(_ws.PathOf(p)), StringComparer.Ordinal);

    private void CopyFixture()
    {
        foreach (var path in FixtureFiles())
        {
            _ws.Write(path, Fixture(path));
        }
    }

    /// <summary>Replays <c>bellatrix init</c> over a workspace: its own files overwrite, host files get its block appended.</summary>
    private void ReplayBellatrixInit()
    {
        foreach (var path in FixtureFiles())
        {
            var content = Fixture(path);
            if (!HostFiles.Contains(path) || !_ws.Exists(path))
            {
                _ws.Write(path, content);
                continue;
            }

            var existing = _ws.Read(path);
            if (!existing.Contains(content.Trim(), StringComparison.Ordinal))
            {
                _ws.Write(path, existing + (existing.EndsWith('\n') ? "\n" : "\n\n") + content);
            }
        }
    }

    private void RunBellatrix()
    {
        var info = new ProcessStartInfo(LiveBellatrixFact.Executable!)
        {
            WorkingDirectory = _ws.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "init", "--agent", Agents, "--language", "csharp", "--domains", "web" })
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"bellatrix init failed: {stderr.Result}");
    }

    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static string FixtureRoot() =>
        Path.Combine(AgentInstallTests.RepoRoot(), "tests", "Spectra.CLI.Tests", "TestFixtures", "BellatrixInit");

    private static IEnumerable<string> FixtureFiles() =>
        Directory.EnumerateFiles(FixtureRoot(), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(FixtureRoot(), f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

    private static string Fixture(string relative) => File.ReadAllText(Path.Combine(FixtureRoot(), relative));
}

/// <summary>Spec 104: runs only where the BELLATRIX Agent CLI (<c>bellatrix</c>) is installed and on PATH.</summary>
public sealed class LiveBellatrixFact : FactAttribute
{
    public static readonly string? Executable = Find();

    public LiveBellatrixFact()
    {
        if (Executable is null)
        {
            Skip = "bellatrix is not on PATH (the recorded fixture covers coexistence)";
        }
    }

    private static string? Find()
    {
        var names = OperatingSystem.IsWindows() ? new[] { "bellatrix.exe" } : ["bellatrix"];
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(dir => names.Select(n => Path.Combine(dir, n)))
            .FirstOrDefault(File.Exists);
    }
}
