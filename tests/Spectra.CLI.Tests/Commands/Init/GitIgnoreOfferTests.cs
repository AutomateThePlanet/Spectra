using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Spec 090 T11–T13 (FR-D1–FR-D3, 089-review findings 2–4): <c>spectra update-skills</c> adds only gitignore patterns
/// never offered to the workspace (recorded in the skills manifest), never creates a <c>.gitignore</c>, counts a pattern
/// as present only by whole line, and appends without changing the file's bytes. On fc82b7a it re-added removed
/// patterns, created the file, matched substrings and trimmed the file before appending.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GitIgnoreOfferTests : IDisposable
{
    private const string Updates = ".spectra/updates/";
    private const string Repairs = ".spectra/repairs/";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    private string GitIgnorePath => _ws.PathOf(".gitignore");

    private string ManifestPath => _ws.PathOf(".spectra/skills-manifest.json");

    /// <summary>The 089 list without the two staging folders: what a pre-090 workspace counts as already offered.</summary>
    private static IEnumerable<string> Pre090Offered => SpectraGitIgnore.Patterns.Where(p => p is not Updates and not Repairs);

    private static int Lines(string text, string line) =>
        text.Replace("\r\n", "\n").Split('\n').Count(l => l == line);

    /// <summary>The real init with skills (update-skills needs the skills manifest), then a pre-090 manifest.</summary>
    private async Task InitPre090Async()
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        Assert.Equal(ExitCodes.Success, await new InitHandler(logger, _ws.Root).HandleAsync(force: false, skipSkills: false));
        SetOffered(null);
    }

    private void SetOffered(IEnumerable<string>? offered)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(ManifestPath))!.AsObject();
        manifest.Remove("gitignore_offered");
        if (offered is not null)
        {
            manifest["gitignore_offered"] = new JsonArray(offered.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray());
        }

        File.WriteAllText(ManifestPath, manifest.ToJsonString());
    }

    private string[] Offered() =>
        JsonNode.Parse(File.ReadAllText(ManifestPath))!["gitignore_offered"]?.AsArray().Select(n => n!.GetValue<string>()).ToArray()
        ?? [];

    private static string GitIgnoreOf(IEnumerable<string> lines, string newline, bool finalNewline) =>
        string.Join(newline, lines) + (finalNewline ? newline : "");

    private async Task<int> UpdateSkillsAsync()
    {
        var (exit, _, error) = await _ws.RunAsync("update-skills");
        Assert.True(exit == 0, error);
        return exit;
    }

    // ---- T11 (FR-D1) ----

    [Fact]
    public async Task APatternOfferedBefore_AndRemovedByTheUser_IsNotAddedAgain()
    {
        await InitPre090Async();
        SetOffered(SpectraGitIgnore.Patterns);
        var withoutUpdates = string.Join("\n", File.ReadAllText(GitIgnorePath).Replace("\r\n", "\n").Split('\n').Where(l => l != Updates));
        File.WriteAllText(GitIgnorePath, withoutUpdates);

        await UpdateSkillsAsync();

        Assert.Equal(withoutUpdates, File.ReadAllText(GitIgnorePath));
    }

    [Fact]
    public async Task APre090Workspace_GetsOnlyTheStagingFolders_NotAnOldPatternTheUserRemoved()
    {
        await InitPre090Async();
        File.WriteAllText(GitIgnorePath,
            GitIgnoreOf(["bin/", "# SPECTRA", .. Pre090Offered.Where(p => p != ".spectra/progress.json")], "\n", finalNewline: true));

        await UpdateSkillsAsync();

        var text = File.ReadAllText(GitIgnorePath);
        Assert.Equal(0, Lines(text, ".spectra/progress.json"));
        Assert.Equal(1, Lines(text, Updates));
        Assert.Equal(1, Lines(text, Repairs));
        Assert.Equal(SpectraGitIgnore.Patterns.OrderBy(p => p), Offered().OrderBy(p => p));

        var after = File.ReadAllBytes(GitIgnorePath);
        await UpdateSkillsAsync();
        Assert.Equal(after, File.ReadAllBytes(GitIgnorePath));
    }

    [Fact]
    public async Task NoGitIgnore_IsNotCreated_AndNothingIsRecorded()
    {
        await InitPre090Async();
        File.Delete(GitIgnorePath);

        await UpdateSkillsAsync();

        Assert.False(File.Exists(GitIgnorePath));
        Assert.Empty(Offered());
    }

    // ---- T12 (FR-D2) ----

    [Theory]
    [InlineData(".spectra/updates", false)]          // the pattern without its trailing slash counts as present
    [InlineData("  .spectra/updates/  ", false)]     // whole trimmed line
    [InlineData("# .spectra/updates/", true)]        // a comment doesn't count
    [InlineData("foo/.spectra/updates/", true)]      // a longer line containing the pattern doesn't count
    public async Task Presence_IsAWholeTrimmedNonCommentLine(string line, bool added)
    {
        await InitPre090Async();
        var lines = new List<string> { "# SPECTRA" };
        lines.AddRange(Pre090Offered);
        lines.Add(line);
        lines.Add(Repairs);
        File.WriteAllText(GitIgnorePath, GitIgnoreOf(lines, "\n", finalNewline: true));

        await UpdateSkillsAsync();

        Assert.Equal(added ? 1 : 0, Lines(File.ReadAllText(GitIgnorePath), Updates));
        Assert.Contains(Updates, Offered());
    }

    [Fact]
    public async Task ANegation_IsTheUsersChoice_NotAdded_ButRecorded()
    {
        await InitPre090Async();
        var original = GitIgnoreOf(["# SPECTRA", .. Pre090Offered, "!.spectra/updates/", Repairs], "\n", finalNewline: true);
        File.WriteAllText(GitIgnorePath, original);

        await UpdateSkillsAsync();

        Assert.Equal(original, File.ReadAllText(GitIgnorePath));
        Assert.Contains(Updates, Offered());
    }

    // ---- T13 (FR-D3) ----

    [Fact]
    public async Task ACrlfFileWithoutAFinalNewline_KeepsItsBytes_AndGetsCrlfLines()
    {
        await InitPre090Async();
        var original = GitIgnoreOf(["bin/", "# SPECTRA", .. Pre090Offered], "\r\n", finalNewline: false);
        File.WriteAllText(GitIgnorePath, original);

        await UpdateSkillsAsync();

        Assert.Equal(original + "\r\n" + Updates + "\r\n" + Repairs + "\r\n", File.ReadAllText(GitIgnorePath));
    }

    [Fact]
    public async Task AnLfFileEndingInANewline_GetsLfLines_WithoutABlankSeparator()
    {
        await InitPre090Async();
        var original = GitIgnoreOf(["bin/", "# SPECTRA", .. Pre090Offered], "\n", finalNewline: true);
        File.WriteAllText(GitIgnorePath, original);

        await UpdateSkillsAsync();

        Assert.Equal(original + Updates + "\n" + Repairs + "\n", File.ReadAllText(GitIgnorePath));
    }

    [Fact]
    public async Task TrailingBlankLines_AreKept()
    {
        await InitPre090Async();
        var original = GitIgnoreOf(["bin/", "# SPECTRA", .. Pre090Offered], "\n", finalNewline: true) + "\n\n";
        File.WriteAllText(GitIgnorePath, original);

        await UpdateSkillsAsync();

        Assert.Equal(original + Updates + "\n" + Repairs + "\n", File.ReadAllText(GitIgnorePath));
    }

    [Fact]
    public async Task TheSectionHeader_IsAddedOnlyWhenAbsent()
    {
        await InitPre090Async();
        var original = GitIgnoreOf(["bin/", .. Pre090Offered], "\n", finalNewline: true);
        File.WriteAllText(GitIgnorePath, original);

        await UpdateSkillsAsync();

        Assert.Equal(original + "# SPECTRA\n" + Updates + "\n" + Repairs + "\n", File.ReadAllText(GitIgnorePath));
    }

    // ---- Real init (FR-D1: init writes the full list and records it; analysis A1) ----

    [Fact]
    public async Task TheRealInit_WritesTheFullList_AndRecordsItAsOffered()
    {
        await _ws.InitWithRealTemplateAsync(); // skipSkills: true

        var text = File.ReadAllText(GitIgnorePath);
        Assert.All(SpectraGitIgnore.Patterns, p => Assert.Equal(1, Lines(text, p)));
        Assert.True(File.Exists(ManifestPath), "init --skip-skills writes the skills manifest");
        Assert.Equal(SpectraGitIgnore.Patterns.OrderBy(p => p), Offered().OrderBy(p => p));
    }
}
