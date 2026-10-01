using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 083 T2/T3 (FR-015, FR-016, FR-021, SC-001, SC-002): the guard 080–082 lacked. The criteria workflow runs
/// against the config the REAL <c>spectra init</c> writes (the embedded template), and against that template with
/// <c>coverage</c> removed — what every workspace created before 083 has. Both must work with zero config warnings.
/// Any future config rule must keep these green.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class InitTemplateWorkspaceTests : IDisposable
{
    private const string ConfigWarning = "spectra.config.json has no";
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    /// <summary>
    /// Runs the real <c>spectra init</c> in the workspace. <c>skipSkills</c> only skips installing skill files; the
    /// config is written by the same <c>CreateConfigFileAsync</c> from the same embedded template either way.
    /// </summary>
    private async Task InitAsync()
    {
        File.Delete(_ws.PathOf("spectra.config.json")); // init refuses to overwrite an existing config
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        var exit = await new InitHandler(logger, _ws.Root).HandleAsync(force: false, skipSkills: true);
        Assert.Equal(ExitCodes.Success, exit);
    }

    private static int Occurrences(string text) =>
        (text.Length - text.Replace(ConfigWarning, "").Length) / ConfigWarning.Length;

    /// <summary>docs changed → compile → ingest (3 docs) → retire a deleted doc → apply a move. Returns the config warnings.</summary>
    private async Task<int> RunWorkflowAsync()
    {
        _ws.WriteDoc("docs/a.md", "# A\n\nUsers MUST see A.\n");
        _ws.WriteDoc("docs/gone.md", "# Gone\n\nGone MUST work.\n");
        _ws.WriteDoc("docs/move.md", "# Move\n\nMove MUST work.\n");
        var warnings = 0;

        async Task Step(string label, params string[] args)
        {
            var (exit, output, err) = await _ws.RunAsync(args);
            Assert.True(exit == 0, $"{label}: exit {exit}: {output} {err}");
            warnings += Occurrences(output) + Occurrences(err);
        }

        await Step("docs changed", "docs", "changed", "--output-format", "json");
        await Step("compile", "ai", "compile-extraction-prompt", "--doc", "docs/a.md");
        foreach (var (doc, text) in new[] { ("docs/a.md", "Users MUST see A"), ("docs/gone.md", "Gone MUST work"), ("docs/move.md", "Move MUST work") })
        {
            _ws.Response(new { text, rfc2119 = "MUST" });
            await Step($"ingest {doc}", "ai", "ingest-criteria", "--doc", doc, "--from", ".spectra/criteria.json", "--output-format", "json");
        }

        File.Delete(_ws.PathOf("docs/gone.md"));
        await Step("retire", "docs", "retire", "--doc", "docs/gone.md", "--output-format", "json");

        File.Move(_ws.PathOf("docs/move.md"), _ws.PathOf("docs/moved.md"));
        await Step("apply-move", "docs", "apply-move", "--from", "docs/move.md", "--to", "docs/moved.md", "--output-format", "json");

        return warnings;
    }

    // T2 — the test that would have caught 082.
    [Fact]
    public async Task RealTemplate_WorkflowSucceeds_WithoutConfigWarnings()
    {
        await InitAsync();

        var warnings = await RunWorkflowAsync();

        Assert.Equal(0, warnings);
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/docs/a.criteria.yaml")));
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/docs/moved.criteria.yaml")));
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/docs/gone.criteria.yaml")));
    }

    // T3 — every workspace created before 083: the init template without `coverage`. No config edit is needed.
    [Fact]
    public async Task PreExistingWorkspace_WithoutCoverage_WorkflowSucceeds_WithoutConfigWarnings()
    {
        await InitAsync();
        var configPath = _ws.PathOf("spectra.config.json");
        var config = JsonNode.Parse(File.ReadAllText(configPath), documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        })!.AsObject();
        Assert.True(config.Remove("coverage"), "the init template must have a coverage section to remove");
        File.WriteAllText(configPath, config.ToJsonString());

        var warnings = await RunWorkflowAsync();

        Assert.Equal(0, warnings);
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/docs/a.criteria.yaml")));
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/_criteria_index.yaml")));
    }
}
