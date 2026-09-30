using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Config;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 080 A5 (FR-005, FR-005a, FR-005b, SC-006a): every in-scope command loads config through one loader. A missing
/// config gives defaults to read commands and an error to write commands; a malformed or invalid config is an error
/// (exit 1, one shared message) for all of them, and no command ever proceeds on defaults after failing to parse it.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class ConfigLoadingConsistencyTests : IDisposable
{
    private const string Malformed = "{\"source\": {";
    private const string PartialVerdict =
        """{"verdict":"partial","score":0.72,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Expected Result","claim":"ok","status":"unverified","reason":"not in docs"}]}""";

    private readonly CriteriaTestWorkspace _ws = new();

    public ConfigLoadingConsistencyTests()
    {
        // Laid out for the default paths (docs/, test-cases/, docs/criteria/) so read commands succeed without a config.
        _ws.WriteDoc("docs/a.md", "# A\n\nUsers MUST see A.\n");
        _ws.WriteDoc("test-cases/s/TC-1.md",
            "---\nid: TC-1\npriority: medium\nsource_refs:\n  - docs/a.md\n---\n\n# See A\n\n## Steps\n\n1. open\n\n## Expected Result\n\n- ok\n");
        _ws.WriteDoc("test-cases/s/_index.json",
            "{\"suite\":\"s\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[{\"id\":\"TC-1\",\"file\":\"TC-1.md\",\"title\":\"See A\",\"priority\":\"medium\"}]}");
        _ws.WriteDoc(".spectra/verdicts/critic-verdict-TC-1.json", PartialVerdict);
        _ws.WriteDoc(".spectra/criteria.json", "[{\"text\":\"Users MUST see A\",\"rfc2119\":\"MUST\"}]");
        _ws.WriteDoc("import.yaml", "criteria:\n- text: Refund MUST be issued\n  source: checkout\n");
    }

    public void Dispose() => _ws.Dispose();

    public static TheoryData<string> ReadCommands => new()
    {
        "ai compile-extraction-prompt --doc docs/a.md",
        "ai compile-critic-prompt --suite s --test TC-1",
        "ai compile-repair-prompt --suite s --test TC-1",
        "ai compile-repair-batch --suite s",
        "docs changed"
    };

    public static TheoryData<string> WriteCommands => new()
    {
        "ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json",
        "docs retire --doc docs/gone.md",
        "docs apply-move --from docs/gone.md --to docs/a.md",
        "docs migrate-criteria",
        "ai analyze --import-criteria import.yaml"
    };

    private Task<(int Exit, string Out, string Err)> RunAsync(string command, bool json) =>
        _ws.RunAsync([.. command.Split(' '), .. (json ? new[] { "--output-format", "json" } : [])]);

    private void BreakConfig() => File.WriteAllText(_ws.PathOf("spectra.config.json"), Malformed);

    private void RemoveConfig() => File.Delete(_ws.PathOf("spectra.config.json"));

    // ---------- (a) malformed config: exit 1 with the shared message, in both modes ----------

    [Theory]
    [MemberData(nameof(ReadCommands))]
    [MemberData(nameof(WriteCommands))]
    public async Task MalformedConfig_HumanMode_Exits1_WithTheSharedMessage(string command)
    {
        BreakConfig();

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.Equal(1, exit);
        Assert.Contains(ConfigLoader.InvalidConfigPrefix, output + err);
    }

    [Theory]
    [InlineData("ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json")]
    [InlineData("docs retire --doc docs/gone.md")]
    [InlineData("docs apply-move --from docs/gone.md --to docs/a.md")]
    [InlineData("docs migrate-criteria")]
    public async Task MalformedConfig_JsonMode_CriteriaWriteCommands_StderrIsOneFailureDocument(string command)
    {
        BreakConfig();

        var (exit, _, err) = await RunAsync(command, json: true);

        Assert.Equal(1, exit);
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.False(failure.GetProperty("success").GetBoolean());
        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, failure.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task MalformedConfig_JsonMode_ImportCriteria_StdoutIsOneErrorResult()
    {
        BreakConfig();

        var (exit, output, _) = await RunAsync("ai analyze --import-criteria import.yaml", json: true);

        Assert.Equal(1, exit);
        var failure = CriteriaTestWorkspace.Json(output);
        Assert.Equal("failed", failure.GetProperty("status").GetString());
        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, failure.GetProperty("error").GetString());
    }

    [Fact]
    public async Task MalformedConfig_JsonMode_CompileExtraction_StderrIsOneFailureDocument()
    {
        BreakConfig();

        var (exit, _, err) = await RunAsync("ai compile-extraction-prompt --doc docs/a.md", json: true);

        Assert.Equal(1, exit);
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, failure.GetProperty("errors")[0].GetString());
        Assert.Equal(0, failure.GetProperty("warnings").GetArrayLength());
    }

    [Theory]
    [InlineData("ai compile-critic-prompt --suite s --test TC-1")]
    [InlineData("ai compile-repair-prompt --suite s --test TC-1")]
    [InlineData("ai compile-repair-batch --suite s")]
    [InlineData("docs changed")]
    public async Task MalformedConfig_JsonMode_OtherReadCommands_ReportThePlainMessage(string command)
    {
        BreakConfig();

        var (exit, _, err) = await RunAsync(command, json: true);

        Assert.Equal(1, exit);
        Assert.Contains(err.Split('\n'), l => l.TrimEnd('\r').StartsWith(ConfigLoader.InvalidConfigPrefix, StringComparison.Ordinal));
    }

    // ---------- (b) missing config: write commands refuse ----------

    [Theory]
    [MemberData(nameof(WriteCommands))]
    public async Task MissingConfig_WriteCommands_Exit1_WithTheMissingMessage(string command)
    {
        RemoveConfig();

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.Equal(1, exit);
        Assert.Contains(ConfigLoader.MissingConfigMessage, output + err);
    }

    // ---------- (c) missing config: read commands run on the defaults ----------

    [Theory]
    [MemberData(nameof(ReadCommands))]
    public async Task MissingConfig_ReadCommands_RunOnDefaults(string command)
    {
        RemoveConfig();

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.True(exit == 0, $"exit {exit}: {err}");
        Assert.DoesNotContain(ConfigLoader.MissingConfigMessage, output + err);
        Assert.DoesNotContain(ConfigLoader.InvalidConfigPrefix, output + err);
    }

    // ---------- (d) malformed config: no write command modifies anything ----------

    [Theory]
    [MemberData(nameof(WriteCommands))]
    public async Task MalformedConfig_WriteCommands_ModifyNothing(string command)
    {
        BreakConfig();
        var before = _ws.Snapshot();

        await RunAsync(command, json: false);

        Assert.Equal(before, _ws.Snapshot());
    }

    // ---------- (e) ingest-criteria's early errors are one JSON document too (SC-006) ----------

    // `--doc` is a required option, so System.CommandLine rejects its absence before the handler runs; the handler's
    // own --doc check is defensive only and not reachable from the CLI.
    [Theory]
    [InlineData("ai ingest-criteria --doc docs/a.md --from missing.json")]
    public async Task IngestCriteria_EarlyErrors_JsonMode_AreOneFailureDocument(string command)
    {
        var (exit, output, err) = await RunAsync(command, json: true);

        Assert.Equal(1, exit);
        Assert.Equal(string.Empty, output.Trim());
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.False(failure.GetProperty("success").GetBoolean());
        Assert.False(failure.TryGetProperty("outcome", out _));
    }

    // ---------- Spec 081 C2: only the sections a command reads are checked ----------

    private const string SourceJson = "\"source\": { \"local_dir\": \"docs/\" }";
    private const string TestsJson = "\"tests\": { \"dir\": \"test-cases/\" }";
    private const string CoverageJson = "\"coverage\": { \"criteria_dir\": \"docs/criteria\", \"criteria_file\": \"docs/criteria/_criteria_index.yaml\" }";

    private void WriteConfig(params string[] sections) =>
        File.WriteAllText(_ws.PathOf("spectra.config.json"), "{ " + string.Join(", ", sections) + " }");

    [Theory]
    [MemberData(nameof(ReadCommands))]
    [MemberData(nameof(WriteCommands))]
    public async Task NoAiSection_IsAcceptedByEveryCommand(string command)
    {
        WriteConfig(SourceJson, TestsJson, CoverageJson);

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.DoesNotContain(ConfigLoader.InvalidConfigPrefix, output + err);
        Assert.DoesNotContain(ConfigLoader.MissingConfigMessage, output + err);
        Assert.NotEqual(1, exit);
    }

    [Fact]
    public async Task ReadCommand_MissingTests_UsesDefaults_AndWarns()
    {
        WriteConfig(SourceJson, CoverageJson);

        var (exit, _, err) = await RunAsync("ai compile-critic-prompt --suite s --test TC-1", json: false);

        Assert.True(exit == 0, err);
        Assert.Contains(err.Split('\n'), l => l.StartsWith("Warning:", StringComparison.Ordinal) && l.Contains("'tests'"));
    }

    [Theory]
    [InlineData("ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json")]
    [InlineData("docs retire --doc docs/gone.md")]
    public async Task WriteCommand_MissingTests_Exits1_NamingTheSection_AndWritesNothing(string command)
    {
        WriteConfig(SourceJson, CoverageJson);
        var before = _ws.Snapshot();

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.Equal(1, exit);
        Assert.Contains("spectra.config.json is invalid: missing 'tests' section", output + err);
        Assert.Equal(before, _ws.Snapshot());
    }

    [Theory]
    [InlineData("docs apply-move --from docs/gone.md --to docs/a.md")]
    [InlineData("docs migrate-criteria")]
    [InlineData("ai analyze --import-criteria import.yaml")]
    public async Task WriteCommand_ThatReadsNeither_IgnoresMissingSourceAndTests(string command)
    {
        WriteConfig(CoverageJson);

        var (_, output, err) = await RunAsync(command, json: false);

        Assert.DoesNotContain("missing 'source' section", output + err);
        Assert.DoesNotContain("missing 'tests' section", output + err);
        Assert.DoesNotContain(ConfigLoader.InvalidConfigPrefix, output + err);
    }

    [Fact]
    public async Task DocsChanged_MissingSource_WarnsInItsJson()
    {
        WriteConfig(TestsJson, CoverageJson);

        var (exit, output, err) = await RunAsync("docs changed", json: true);

        Assert.True(exit == 0, err);
        var warnings = CriteriaTestWorkspace.Json(output).GetProperty("warnings").EnumerateArray().Select(w => w.GetString());
        Assert.Contains(warnings, w => w!.Contains("'source'"));
    }
}
