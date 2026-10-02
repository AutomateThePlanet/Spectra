using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 084 T6/T7 (FR-029, FR-030, SC-006): the guard 080–082 lacked, applied to the new commands. The whole flow runs
/// against the config the REAL <c>spectra init</c> writes (which has no <c>update</c> section), and a workspace from
/// before this spec (tests, no baselines) needs no config edit.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class BaselineInitTemplateTests : IDisposable
{
    private const string ConfigWarning = "spectra.config.json has no";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    private async Task InitAsync()
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        Assert.Equal(ExitCodes.Success, await new InitHandler(logger, _ws.Root).HandleAsync(force: false, skipSkills: true));
        Assert.DoesNotContain("\"update\"", File.ReadAllText(_ws.PathOf("spectra.config.json")));
    }

    private static int Warnings(string text) => (text.Length - text.Replace(ConfigWarning, "").Length) / ConfigWarning.Length;

    // T6
    [Fact]
    public async Task RealTemplate_IngestEditTriageAck_WithoutConfigWarnings()
    {
        await InitAsync();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n");
        var warnings = 0;

        async Task<string> Step(params string[] args)
        {
            var (exit, output, error) = await _ws.RunAsync([.. args, "--output-format", "json"]);
            Assert.True(exit == 0, $"{string.Join(' ', args)}: exit {exit}: {output} {error}");
            warnings += Warnings(output) + Warnings(error);
            return output;
        }

        await Step("ai", "ingest-tests", "storage", "--from", _ws.GeneratedPayload(("TC-100", ["docs/storage.md#Quotas"], [])));
        Assert.NotNull(_ws.ReadBaseline("storage"));

        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 50GB.\n");
        var triage = BaselineTestWorkspace.Json(await Step("ai", "update-triage", "--suite", "storage"));
        var group = BaselineTestWorkspace.GroupOf(triage, "TC-100");
        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());

        await Step("ai", "update-ack", "--suite", "storage", "--ids", "TC-100");
        var after = BaselineTestWorkspace.Json(await Step("ai", "update-triage", "--suite", "storage"));
        Assert.Equal(1, BaselineTestWorkspace.Count(after, "storage", "unchanged"));

        Assert.Equal(0, warnings);
    }

    // T7
    [Fact]
    public async Task PreExistingWorkspace_AllUnbaselined_ThenInitGivesACleanTriage()
    {
        await InitAsync();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md"]);
        _ws.WriteTest("billing", "TC-201", ["docs/storage.md#Quotas"]);
        var config = File.ReadAllText(_ws.PathOf("spectra.config.json"));
        var before = _ws.Snapshot();

        var triage = await _ws.RunJsonAsync("ai", "update-triage", "--all-suites");

        foreach (var suite in new[] { "billing", "storage" })
        {
            var counts = triage.GetProperty("suites").EnumerateArray().Single(s => s.GetProperty("suite").GetString() == suite).GetProperty("counts");
            foreach (var count in counts.EnumerateObject().Where(c => c.Name != "unbaselined"))
            {
                Assert.True(count.Value.GetInt32() == 0, $"{suite} {count.Name} = {count.Value}");
            }
        }

        Assert.Equal(3, new[] { "billing", "storage" }.Sum(s => BaselineTestWorkspace.Count(triage, s, "unbaselined")));
        Assert.Empty(triage.GetProperty("groups").EnumerateArray());
        var after = _ws.Snapshot();
        Assert.Equal(before.Keys.Append(".spectra/update-triage.json").OrderBy(k => k, StringComparer.Ordinal), after.Keys);
        Assert.All(before, kv => Assert.Equal(kv.Value, after[kv.Key]));

        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        var clean = await _ws.RunJsonAsync("ai", "update-triage", "--all-suites");

        Assert.Equal(3, new[] { "billing", "storage" }.Sum(s => BaselineTestWorkspace.Count(clean, s, "unchanged")));
        Assert.Empty(clean.GetProperty("unbaselined").EnumerateArray());
        Assert.Equal(config, File.ReadAllText(_ws.PathOf("spectra.config.json")));
    }
}
