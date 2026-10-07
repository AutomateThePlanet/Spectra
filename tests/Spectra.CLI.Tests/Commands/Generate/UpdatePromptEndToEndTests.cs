using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 088 T8 (SC-005): the whole edit step on the config the REAL <c>spectra init</c> writes — ingest, change the doc,
/// triage, compile the group prompt, stage an edit, ingest the batch — leaves the test UNCHANGED and no staging file.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdatePromptEndToEndTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task RealTemplate_TriageCompileGroupStageIngestAll_LeavesTheTestUnchanged()
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        Assert.Equal(ExitCodes.Success, await new InitHandler(logger, _ws.Root).HandleAsync(force: false, skipSkills: true));
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n");

        async Task<string> Step(params string[] args)
        {
            var (exit, output, error) = await _ws.RunAsync([.. args, "--output-format", "json"]);
            Assert.True(exit == 0, $"{string.Join(' ', args)}: exit {exit}: {output} {error}");
            return output;
        }

        await Step("ai", "ingest-tests", "storage", "--from", _ws.GeneratedPayload(("TC-100", ["docs/storage.md#Quotas"], [])));
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 50GB.\n");

        var triage = BaselineTestWorkspace.Json(await Step("ai", "update-triage", "--suite", "storage"));
        var group = BaselineTestWorkspace.GroupOf(triage, "TC-100");
        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());

        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--group", group.GetProperty("id").GetString()!);
        Assert.True(exit == 0, error);
        Assert.Contains("Each user gets 50GB.", prompt);
        Assert.Contains("### TEST TC-100", prompt);

        var original = _ws.Load("storage", "TC-100");
        _ws.StageEdit("storage", "TC-100", original with { ExpectedResult = "The user's quota is 50GB" });
        var batch = BaselineTestWorkspace.Json(await Step("ai", "ingest-update", "storage", "--all"));
        Assert.Equal(1, batch.GetProperty("written").GetInt32());

        var after = BaselineTestWorkspace.Json(await Step("ai", "update-triage", "--suite", "storage"));
        Assert.Equal(1, BaselineTestWorkspace.Count(after, "storage", "unchanged"));
        Assert.Empty(_ws.StagedFiles("storage"));
        Assert.Contains("The user's quota is 50GB", File.ReadAllText(_ws.PathOf("test-cases/storage/TC-100.md")));
    }
}
