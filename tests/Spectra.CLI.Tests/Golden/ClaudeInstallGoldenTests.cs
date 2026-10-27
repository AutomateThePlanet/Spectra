using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Golden;

/// <summary>
/// Spec 104 (golden rule): <c>spectra init</c> without <c>--agent</c> — the Claude install — stays byte-identical while
/// the other agents are added. Every file init writes is pinned by path and SHA-256, captured on 61ea574 (before 104).
/// </summary>
/// <remarks>
/// <para>Hashes ignore CRLF/LF, because the bundled content carries the checkout's line endings (CRLF on a Windows
/// checkout, LF on CI); everything else is compared byte for byte. <c>.spectra/skills-manifest.json</c> records hashes of
/// that raw content, so each recorded hash is first checked against the file on disk and then masked, which pins the
/// manifest's keys, order and format on every platform.</para>
/// <para>Capture: set <c>SPECTRA_GOLDEN_RECORD_104</c> to <c>1</c> and run this class; it rewrites <c>Golden/104/</c>.
/// Re-capture only for a deliberate change to the Claude install.</para>
/// </remarks>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class ClaudeInstallGoldenTests : IDisposable
{
    private const string RecordVariable = "SPECTRA_GOLDEN_RECORD_104";
    private const string SkillsManifest = ".spectra/skills-manifest.json";
    private const string ManifestHashMask = "<sha256 of the file on disk>";

    /// <summary>A pre-existing settings file of the user's: init merges its three entries and keeps everything else.</summary>
    private const string ForeignSettings = """
        {
          "model": "opus",
          "permissions": {
            "allow": ["Bash(npm test)", "Bash(spectra *)"],
            "deny": ["Read(./.env)"]
          },
          "hooks": {}
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "spectra-104-golden-" + Guid.NewGuid().ToString("N"));

    public ClaudeInstallGoldenTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Init_WithoutAgent_MatchesTheClaudeGolden()
    {
        await InitAsync();

        AssertMatchesGolden("init-fresh.txt");
    }

    [Fact]
    public async Task Init_OverForeignClaudeSettings_MatchesTheClaudeGolden()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".claude"));
        File.WriteAllText(Path.Combine(_root, ".claude", "settings.json"), ForeignSettings.Replace("\r\n", "\n"));

        await InitAsync();

        AssertMatchesGolden("init-foreign-settings.txt");
    }

    private async Task InitAsync()
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        Assert.Equal(ExitCodes.Success, await new InitHandler(logger, _root).HandleAsync(force: false));
    }

    private void AssertMatchesGolden(string goldenFile)
    {
        var actual = Snapshot(_root);
        var path = Path.Combine(GoldenDirectory(), goldenFile);

        if (Environment.GetEnvironmentVariable(RecordVariable) == "1")
        {
            Directory.CreateDirectory(GoldenDirectory());
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"golden file missing: {path} (capture with {RecordVariable}=1)");
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
    }

    /// <summary>One <c>&lt;sha256&gt;  &lt;path&gt;</c> line per file under <paramref name="root"/>, sorted by path.</summary>
    internal static string Snapshot(string root)
    {
        var lines = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(full => (Relative: Path.GetRelativePath(root, full).Replace('\\', '/'), Full: full))
            .OrderBy(f => f.Relative, StringComparer.Ordinal)
            .Select(f => $"{HashOf(root, f.Relative, f.Full)}  {f.Relative}");
        return string.Join("\n", lines) + "\n";
    }

    private static string HashOf(string root, string relative, string full)
    {
        var text = File.ReadAllText(full);
        if (relative == SkillsManifest)
        {
            text = MaskManifestHashes(root, text);
        }

        return Sha256(text.Replace("\r\n", "\n"));
    }

    /// <summary>Checks each recorded hash against the file on disk, then masks it (its value depends on line endings).</summary>
    private static string MaskManifestHashes(string root, string manifestText)
    {
        var files = JsonNode.Parse(manifestText)!["files"]!.AsObject();
        foreach (var (key, value) in files)
        {
            var recorded = value!.GetValue<string>();
            var onDisk = FileHasher.ComputeHash(File.ReadAllText(Path.Combine(root, key)));
            Assert.True(recorded == onDisk, $"skills manifest hash for '{key}' does not match the file on disk");
            manifestText = manifestText.Replace(recorded, ManifestHashMask, StringComparison.Ordinal);
        }

        return manifestText;
    }

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string GoldenDirectory() => Path.Combine(RepoRoot(), "tests", "Spectra.CLI.Tests", "Golden", "104");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("not inside the repository");
    }
}
