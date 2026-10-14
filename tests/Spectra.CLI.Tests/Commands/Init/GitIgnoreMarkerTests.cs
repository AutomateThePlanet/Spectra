using System.Text.Json.Nodes;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Spec 096 F5 (FR-008, FR-009): the shared gitignore list gains <c>.spectra/verdicts/</c> and
/// <c>.spectra/dropped-tests.json</c>; whether a pattern was offered is recorded in <c>.gitignore</c> itself
/// (<c># spectra-offered: …</c>), so a pattern the team removed is never re-added by any clone. The old per-clone record
/// is migrated once. On 23aa08c the record lived in the gitignored skills manifest.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GitIgnoreMarkerTests : IDisposable
{
    private const string Verdicts = ".spectra/verdicts/";
    private const string Dropped = ".spectra/dropped-tests.json";
    private const string Marker = "# spectra-offered:";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public GitIgnoreMarkerTests() => _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private string GitIgnorePath => _ws.PathOf(".gitignore");

    private string[] Lines() => File.ReadAllText(GitIgnorePath).Replace("\r\n", "\n").Split('\n');

    private int Count(string pattern) => Lines().Count(l => l.Trim() == pattern);

    private IReadOnlyList<string> Marked() =>
        Lines().Where(l => l.Trim().StartsWith(Marker, StringComparison.Ordinal))
            .SelectMany(l => l.Trim()[Marker.Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    private void WriteGitIgnore(IEnumerable<string> lines) =>
        File.WriteAllText(GitIgnorePath, string.Concat(lines.Select(l => l + "\n")));

    private void SetRecord(IEnumerable<string>? offered)
    {
        var path = _ws.PathOf(".spectra/skills-manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (offered is null)
        {
            manifest.Remove("gitignore_offered");
        }
        else
        {
            manifest["gitignore_offered"] = new JsonArray(offered.Select(p => (JsonNode)p!).ToArray());
        }

        File.WriteAllText(path, manifest.ToJsonString());
    }

    private async Task UpdateSkillsAsync()
    {
        var (exit, _, error) = await _ws.RunAsync("update-skills");
        Assert.True(exit == 0, error);
    }

    [Fact]
    public void TheRealInit_WritesTheNewEntries_AndOneMarkerNamingEveryPattern()
    {
        Assert.Equal(1, Count(Verdicts));
        Assert.Equal(1, Count(Dropped));
        Assert.Equal(1, Lines().Count(l => l.Trim().StartsWith(Marker, StringComparison.Ordinal)));
        Assert.All(SpectraGitIgnore.Patterns, p => Assert.Contains(p, Marked()));
    }

    [Fact]
    public async Task A093Workspace_IsOfferedTheTwoNewEntriesOnce()
    {
        WriteGitIgnore(Lines().Where(l => l.Trim() is not (Verdicts or Dropped) && !l.Trim().StartsWith(Marker, StringComparison.Ordinal)));
        SetRecord(SpectraGitIgnore.Patterns.Where(p => p is not (Verdicts or Dropped)));

        await UpdateSkillsAsync();
        var afterFirst = File.ReadAllBytes(GitIgnorePath);
        await UpdateSkillsAsync();

        Assert.Equal(1, Count(Verdicts));
        Assert.Equal(1, Count(Dropped));
        Assert.Contains(Verdicts, Marked());
        Assert.Equal(afterFirst, File.ReadAllBytes(GitIgnorePath));
    }

    [Fact]
    public async Task ATeammatesClone_NeverReAddsAPatternTheTeamRemoved()
    {
        // The team removed .spectra/generation-plan/ after it was offered; the clone has no local record.
        WriteGitIgnore(Lines().Where(l => l.Trim() != ".spectra/generation-plan/"));
        SetRecord(null);

        await UpdateSkillsAsync();

        Assert.Equal(0, Count(".spectra/generation-plan/"));
    }

    [Fact]
    public async Task AnOldPerCloneRecord_IsMigratedToAMarkerOnce()
    {
        WriteGitIgnore(Lines().Where(l => l.Trim() != ".spectra/updates/" && !l.Trim().StartsWith(Marker, StringComparison.Ordinal)));
        SetRecord(SpectraGitIgnore.Patterns);

        await UpdateSkillsAsync();

        Assert.Equal(0, Count(".spectra/updates/"));
        Assert.Contains(".spectra/updates/", Marked());
    }

    [Fact]
    public async Task WithoutAGitIgnore_NothingIsCreated()
    {
        File.Delete(GitIgnorePath);

        await UpdateSkillsAsync();

        Assert.False(File.Exists(GitIgnorePath));
    }
}
