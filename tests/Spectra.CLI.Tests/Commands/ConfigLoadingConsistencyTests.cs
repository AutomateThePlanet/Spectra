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

    // Spec 082 A4: only the two repair compilers keep a plain line — they have no JSON mode. compile-critic-prompt and
    // docs changed now report config failures as the shared failure document (see ConfigErrorCodes_JsonMode).
    [Theory]
    [InlineData("ai compile-repair-prompt --suite s --test TC-1")]
    [InlineData("ai compile-repair-batch --suite s")]
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

    // Spec 082 A1: replaces 081's WriteCommand_MissingTests_Exits1 — `tests` only feeds the referencing-test counts,
    // so a missing `tests` is a warning for the criteria writes, never a refusal.
    [Theory]
    [InlineData("ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json")]
    [InlineData("docs retire --doc docs/gone.md")]
    public async Task WriteCommand_MissingTests_Warns_AndSucceeds(string command)
    {
        if (command.StartsWith("docs retire", StringComparison.Ordinal))
        {
            // An indexed document that has since been deleted (seeded with the full config).
            _ws.WriteDoc("docs/gone.md", "# Gone\n\nGone MUST work.\n");
            var (seedExit, _, seedErr) = await _ws.IngestAsync("docs/gone.md", new { text = "Gone MUST work", rfc2119 = "MUST" });
            Assert.True(seedExit == 0, seedErr);
            File.Delete(_ws.PathOf("docs/gone.md"));
        }
        WriteConfig(SourceJson, CoverageJson);

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.True(exit == 0, $"exit {exit}: {err}");
        var warnings = (output + "\n" + err).Split('\n')
            .Where(l => l.TrimStart().StartsWith("Warning:", StringComparison.Ordinal) && l.Contains("'tests'"))
            .ToList();
        Assert.Single(warnings);
    }

    public static TheoryData<string, bool> WriteCommandsWithDryRun => new()
    {
        { "ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json", false },
        { "ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json", true },
        { "docs retire --doc docs/gone.md", false },
        { "docs retire --doc docs/gone.md", true },
        { "docs apply-move --from docs/gone.md --to docs/a.md", false },
        { "docs apply-move --from docs/gone.md --to docs/a.md", true },
        { "docs migrate-criteria", false },
        { "docs migrate-criteria", true },
        { "ai analyze --import-criteria import.yaml", false },
        { "ai analyze --import-criteria import.yaml", true }
    };

    /// <summary>
    /// Seeds, with the full config, whatever a write command needs to succeed: an indexed-then-deleted
    /// <c>docs/gone.md</c> for retire, and one with <c>docs/a.md</c>'s content for apply-move (so the move is valid).
    /// </summary>
    private async Task SeedForAsync(string command)
    {
        if (command.StartsWith("docs retire", StringComparison.Ordinal) || command.StartsWith("docs apply-move", StringComparison.Ordinal))
        {
            var content = command.StartsWith("docs apply-move", StringComparison.Ordinal)
                ? File.ReadAllText(_ws.PathOf("docs/a.md"))
                : "# Gone\n\nGone MUST work.\n";
            _ws.WriteDoc("docs/gone.md", content);
            var (seedExit, _, seedErr) = await _ws.IngestAsync("docs/gone.md", new { text = "Gone MUST work", rfc2119 = "MUST" });
            Assert.True(seedExit == 0, seedErr);
            File.Delete(_ws.PathOf("docs/gone.md"));
        }
    }

    // Spec 083: replaces 082's WriteCommand_MissingCoverage_Exits1_AndWritesNothing. No section is ever required —
    // requiring `coverage` broke every workspace `spectra init` had created (its template had no `coverage`).
    [Theory]
    [MemberData(nameof(WriteCommandsWithDryRun))]
    public async Task WriteCommand_MissingCoverage_Succeeds_WithoutWarning(string command, bool dryRun)
    {
        await SeedForAsync(command);
        WriteConfig(SourceJson, TestsJson);
        var before = _ws.Snapshot();

        var (exit, output, err) = await RunAsync(dryRun ? command + " --dry-run" : command, json: false);

        Assert.True(exit == 0, $"exit {exit}: {output} {err}");
        Assert.DoesNotContain("missing 'coverage'", output + err);
        Assert.DoesNotContain("'coverage'", output + err);
        if (dryRun)
            Assert.Equal(before, _ws.Snapshot());
        else if (command.StartsWith("ai ingest-criteria", StringComparison.Ordinal))
            Assert.True(File.Exists(_ws.PathOf("docs/criteria/docs/a.criteria.yaml")));
    }

    // Spec 083: replaces 082's ReadCommand_MissingCoverage_UsesDefaults_AndWarns — `coverage` never warns.
    [Theory]
    [InlineData("ai compile-extraction-prompt --doc docs/a.md")]
    [InlineData("ai compile-critic-prompt --suite s --test TC-1")]
    [InlineData("docs changed")]
    public async Task ReadCommand_MissingCoverage_NoWarning(string command)
    {
        WriteConfig(SourceJson, TestsJson);

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.True(exit == 0, $"exit {exit}: {err}");
        Assert.DoesNotContain("'coverage'", output + err);
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

    // ---------- Spec 082 A3: null sections behave like missing ones ----------

    [Fact]
    public async Task NullAi_IsAccepted()
    {
        WriteConfig(SourceJson, TestsJson, CoverageJson, "\"ai\": null");

        var (exit, _, err) = await RunAsync("docs changed", json: true);

        Assert.True(exit == 0, err);
    }

    // Spec 083: replaces 082's NullCoverage_RefusesAWrite — a null `coverage` means its built-in defaults, silently.
    [Fact]
    public async Task NullCoverage_WriteSucceeds_WithoutWarning()
    {
        WriteConfig(SourceJson, TestsJson, "\"coverage\": null");

        var (exit, output, err) = await RunAsync("ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json", json: false);

        Assert.True(exit == 0, $"exit {exit}: {err}");
        Assert.DoesNotContain("'coverage'", output + err);
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/docs/a.criteria.yaml")));
    }

    [Fact]
    public async Task NullSource_OnARead_UsesDefaults_AndWarns()
    {
        WriteConfig("\"source\": null", TestsJson, CoverageJson);

        var (exit, output, err) = await RunAsync("docs changed", json: true);

        Assert.True(exit == 0, err);
        var warnings = CriteriaTestWorkspace.Json(output).GetProperty("warnings").EnumerateArray().Select(w => w.GetString());
        Assert.Contains(warnings, w => w!.Contains("'source'"));
    }

    // ---------- Spec 082 A4: named config error codes in every JSON failure document ----------

    public static TheoryData<string, string, string, string> ConfigFailures => new()
    {
        // command, config state, expected code, stream
        { "ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json", "none", "CONFIG_MISSING", "stderr" },
        { "docs retire --doc docs/gone.md", "none", "CONFIG_MISSING", "stderr" },
        { "docs apply-move --from docs/gone.md --to docs/a.md", "none", "CONFIG_MISSING", "stderr" },
        { "docs migrate-criteria", "none", "CONFIG_MISSING", "stderr" },
        { "ai analyze --import-criteria import.yaml", "none", "CONFIG_MISSING", "stdout" },
        { "ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json", "malformed", "CONFIG_INVALID", "stderr" },
        { "docs retire --doc docs/gone.md", "malformed", "CONFIG_INVALID", "stderr" },
        { "docs apply-move --from docs/gone.md --to docs/a.md", "malformed", "CONFIG_INVALID", "stderr" },
        { "docs migrate-criteria", "malformed", "CONFIG_INVALID", "stderr" },
        { "ai compile-extraction-prompt --doc docs/a.md", "malformed", "CONFIG_INVALID", "stderr" },
        { "ai compile-critic-prompt --suite s --test TC-1", "malformed", "CONFIG_INVALID", "stderr" },
        { "docs changed", "malformed", "CONFIG_INVALID", "stderr" },
        { "ai analyze --import-criteria import.yaml", "malformed", "CONFIG_INVALID", "stdout" },

    };

    [Theory]
    [MemberData(nameof(ConfigFailures))]
    public async Task ConfigErrorCodes_JsonMode(string command, string state, string code, string stream)
    {
        switch (state)
        {
            case "none": RemoveConfig(); break;
            case "malformed": BreakConfig(); break;
            default: throw new ArgumentOutOfRangeException(nameof(state), state, "Spec 083: no section is ever required.");
        }

        var (exit, output, err) = await RunAsync(command, json: true);

        Assert.Equal(1, exit);
        var failure = CriteriaTestWorkspace.Json(stream == "stdout" ? output : err);
        Assert.Equal(code, failure.GetProperty("error_code").GetString());
        Assert.True(failure.TryGetProperty("command", out _));
    }

    // ---------- Spec 083 F3 / SC-004: every in-scope command rejects case-variant duplicate keys ----------

    [Theory]
    [MemberData(nameof(ReadCommands))]
    [MemberData(nameof(WriteCommands))]
    public async Task CaseVariantDuplicateKey_IsRejected_ByEveryCommand(string command)
    {
        WriteConfig(SourceJson, TestsJson, CoverageJson, "\"Tests\": null");

        var (exit, output, err) = await RunAsync(command, json: false);

        Assert.Equal(1, exit);
        Assert.Contains(ConfigLoader.InvalidConfigPrefix, output + err);
        Assert.Contains("'tests'", output + err);
        Assert.Contains("'Tests'", output + err);
    }
}
