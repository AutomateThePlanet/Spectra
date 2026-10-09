using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Spec 089 T6 (F6, FR-022, FR-022a): both staging folders are gitignored. A new workspace gets them from the real
/// <c>spectra init</c>; an existing (pre-089) one from <c>spectra update-skills</c>, which applies the same idempotent list
/// and keeps every other line. Plain <c>init</c> on an existing workspace still refuses. On 6ea7520 the entries were
/// missing and <c>update-skills</c> never touched <c>.gitignore</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class StagingGitIgnoreTests : IDisposable
{
    private static readonly string[] New = [".spectra/updates/", ".spectra/repairs/"];

    // The list spectra init wrote before 089 (InitHandler at 6ea7520).
    private static readonly string[] Pre089 =
    [
        ".execution/*.db", ".execution/*.db-*", ".spectra.lock", ".spectra/.pid", ".spectra/.cancel",
        ".spectra/id-allocator.lock", ".spectra/id-allocator.json", ".spectra/progress.json", ".spectra/analysis.json",
        ".spectra/generated.json", ".spectra/updated.json", ".spectra/criteria.json", ".spectra/critic-verdict.json",
        ".spectra/seam-progress.html", ".spectra/skills-manifest.json", ".spectra-result.json", ".spectra-progress.html",
        ".spectra/criteria.lock", ".spectra/baseline.lock", ".spectra/update-triage.json",
    ];

    private static readonly string Pre089GitIgnore =
        "bin/\nobj/\n# my notes\nlocal-only.txt\n\n# SPECTRA\n" + string.Concat(Pre089.Select(p => p + "\n"));

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    private string GitIgnorePath => _ws.PathOf(".gitignore");

    private string GitIgnore => File.ReadAllText(GitIgnorePath);

    private static int Lines(string text, string line) =>
        text.Replace("\r\n", "\n").Split('\n').Count(l => l.Trim() == line);

    private async Task<int> InitAsync(bool skipSkills)
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        return await new InitHandler(logger, _ws.Root).HandleAsync(force: false, skipSkills: skipSkills);
    }

    [Fact]
    public async Task TheRealInit_GitignoresBothStagingFolders_AndKeepsThePre089Entries()
    {
        await _ws.InitWithRealTemplateAsync();

        var text = GitIgnore;
        Assert.All(New, p => Assert.Equal(1, Lines(text, p)));
        Assert.All(Pre089, p => Assert.Equal(1, Lines(text, p)));
    }

    [Fact]
    public async Task UpdateSkills_OnAPre089Workspace_AddsBothOnce_AndKeepsEveryOtherLine()
    {
        Assert.Equal(ExitCodes.Success, await InitAsync(skipSkills: false));
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 30GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        File.WriteAllText(GitIgnorePath, Pre089GitIgnore);
        // Spec 090: today's init records the offered gitignore patterns; a real pre-089 workspace has no such record.
        var manifestPath = _ws.PathOf(".spectra/skills-manifest.json");
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest.Remove("gitignore_offered");
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var (exit, _, error) = await _ws.RunAsync("update-skills");

        Assert.True(exit == 0, error);
        var after = GitIgnore.Replace("\r\n", "\n");
        Assert.StartsWith(Pre089GitIgnore.TrimEnd('\n'), after);
        Assert.All(New, p => Assert.Equal(1, Lines(after, p)));
        Assert.All(Pre089, p => Assert.Equal(1, Lines(after, p)));
        Assert.Equal(1, Lines(after, "local-only.txt"));

        var (again, _, againError) = await _ws.RunAsync("update-skills");

        Assert.True(again == 0, againError);
        Assert.Equal(after, GitIgnore.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task PlainInit_OnAnExistingWorkspace_StillRefuses_AndLeavesTheGitignoreAlone()
    {
        Assert.Equal(ExitCodes.Success, await InitAsync(skipSkills: true));
        File.WriteAllText(GitIgnorePath, Pre089GitIgnore);

        Assert.Equal(ExitCodes.Error, await InitAsync(skipSkills: true));

        Assert.Equal(Pre089GitIgnore, GitIgnore);
    }
}
