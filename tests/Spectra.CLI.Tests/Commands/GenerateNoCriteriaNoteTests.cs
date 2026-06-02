using System.Text.Json;
using Spectra.CLI.Commands.Generate;
using Spectra.CLI.Results;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 048 test plan rows 6-9. Exercises:
/// <list type="bullet">
///   <item><see cref="GenerateHandler.BuildNoCriteriaNote"/> — the gated formatter
///         that returns the note string when no suite-relevant criteria matched.</item>
///   <item><see cref="GenerateHandler.LoadCriteriaContextAsync"/> — the refactored
///         loader returning the <see cref="GenerateHandler.CriteriaContextResult"/>
///         record with the suite-match count.</item>
///   <item>Serialization of <see cref="GenerateResult.Notes"/> — the invariant
///         that the JSON channel is verbosity-independent.</item>
/// </list>
/// Both result-build sites in the handler (direct mode + from-description) wire
/// these two helpers together with a literal <c>Notes = ...</c> assignment;
/// integration verification of those wirings lives in quickstart.md §C-E.
/// </summary>
public class GenerateNoCriteriaNoteTests
{
    // ── BuildNoCriteriaNote ────────────────────────────────────────────────

    [Fact]
    public void Generate_NoMatchedCriteria_FormatsNoteWithSuiteAndCommand()
    {
        var note = GenerateHandler.BuildNoCriteriaNote(suiteMatchedCount: 0, suite: "checkout");

        Assert.NotNull(note);
        Assert.Contains("'checkout'", note);
        Assert.Contains("acceptance-criteria coverage will not include them", note);
        Assert.Contains("spectra ai analyze --extract-criteria", note);
    }

    [Fact]
    public void Generate_WithMatchedCriteria_NoNote()
    {
        // Any positive match suppresses the note regardless of suite name.
        Assert.Null(GenerateHandler.BuildNoCriteriaNote(suiteMatchedCount: 1, suite: "checkout"));
        Assert.Null(GenerateHandler.BuildNoCriteriaNote(suiteMatchedCount: 42, suite: "any-suite"));
    }

    // ── Notes serialization (FR-010) ───────────────────────────────────────

    [Fact]
    public void Generate_Note_PresentInJson_EvenWhenQuiet()
    {
        // The JSON serialization is a property of the result object — it is
        // verbosity-independent by construction. Verbosity only gates the
        // console echo emitted before the JSON write.
        var result = new GenerateResult
        {
            Command = "generate",
            Status = "completed",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsGenerated = 1,
                TestsWritten = 1,
                TestsRejectedByCritic = 0,
            },
            FilesCreated = [],
            Notes = new[]
            {
                "No acceptance criteria matched suite 'checkout'. " +
                "Generated tests have no criteria linkage; acceptance-criteria coverage will not include them. " +
                "Run 'spectra ai analyze --extract-criteria' if criteria are expected.",
            },
        };

        var json = JsonSerializer.Serialize(result);

        // The notes key MUST be present in the JSON regardless of verbosity.
        Assert.Contains("\"notes\":", json);
        Assert.Contains("spectra ai analyze --extract-criteria", json);
    }

    [Fact]
    public void Generate_NoNotes_KeyAbsentFromJson()
    {
        // The presence-check contract: when Notes is null, the key MUST be
        // absent (not null, not []) so consumers can distinguish via key
        // presence alone.
        var result = new GenerateResult
        {
            Command = "generate",
            Status = "completed",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsGenerated = 1,
                TestsWritten = 1,
                TestsRejectedByCritic = 0,
            },
            FilesCreated = [],
            Notes = null,
        };

        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("\"notes\"", json);
    }

    // ── LoadCriteriaContextAsync ───────────────────────────────────────────

    [Fact]
    public async Task LoadCriteriaContext_NoCriteriaDirectory_ZeroMatchedCount()
    {
        // No criteria/ directory at all → SuiteMatchedCount == 0, Context == null,
        // TotalCriteriaCount == 0. This is the cleanest "no match" condition.
        using var sandbox = new TempSandbox();
        var config = MakeConfig(sandbox.Path);

        var result = await GenerateHandler.LoadCriteriaContextAsync(
            basePath: sandbox.Path,
            suiteName: "checkout",
            config: config,
            ct: CancellationToken.None);

        Assert.Null(result.Context);
        Assert.Equal(0, result.SuiteMatchedCount);
        Assert.Equal(0, result.TotalCriteriaCount);
    }

    [Fact]
    public async Task LoadCriteriaContext_SuiteComponentMatches_PositiveCount()
    {
        // A criterion with component == suite name → SuiteMatchedCount > 0.
        using var sandbox = new TempSandbox();
        var config = MakeConfig(sandbox.Path);
        WriteCriteriaFile(sandbox.Path, "checkout.criteria.yaml", component: "checkout", sourceDoc: "docs/checkout.md");

        var result = await GenerateHandler.LoadCriteriaContextAsync(
            basePath: sandbox.Path,
            suiteName: "checkout",
            config: config,
            ct: CancellationToken.None);

        Assert.NotNull(result.Context);
        Assert.True(result.SuiteMatchedCount > 0);
        Assert.True(result.TotalCriteriaCount >= result.SuiteMatchedCount);
        Assert.Equal(0, GenerateHandler.BuildNoCriteriaNote(result.SuiteMatchedCount, "checkout") is null ? 0 : 1);
    }

    [Fact]
    public async Task LoadCriteriaContext_NoSuiteMatch_LastResortFallback_SuiteMatchedCountStaysZero()
    {
        // Criteria exist on disk but none match the suite by component, source-
        // doc, or file-name. The function falls back to "all criteria" for the
        // Context (so the prompt has something), but SuiteMatchedCount must
        // remain 0 — this is the case Spec 048's note specifically targets.
        using var sandbox = new TempSandbox();
        var config = MakeConfig(sandbox.Path);
        WriteCriteriaFile(sandbox.Path, "billing.criteria.yaml", component: "billing", sourceDoc: "docs/billing.md");

        var result = await GenerateHandler.LoadCriteriaContextAsync(
            basePath: sandbox.Path,
            suiteName: "checkout",
            config: config,
            ct: CancellationToken.None);

        // Last-resort fallback engaged: Context is non-null (carrying the billing
        // criterion) but SuiteMatchedCount is 0 — no SUITE-specific match.
        Assert.NotNull(result.Context);
        Assert.Equal(0, result.SuiteMatchedCount);
        Assert.Equal(1, result.TotalCriteriaCount);

        // And the note formatter agrees this is a no-match condition.
        Assert.NotNull(GenerateHandler.BuildNoCriteriaNote(result.SuiteMatchedCount, "checkout"));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static SpectraConfig MakeConfig(string basePath) => new()
    {
        Source = new SourceConfig(),
        Tests = new TestsConfig(),
        Ai = new AiConfig { Providers = Array.Empty<ProviderConfig>() },
        Coverage = new CoverageConfig
        {
            CriteriaDir = "docs/criteria",
        },
    };

    private static void WriteCriteriaFile(string basePath, string fileName, string component, string sourceDoc)
    {
        var dir = Path.Combine(basePath, "docs", "criteria");
        Directory.CreateDirectory(dir);
        var yaml = $@"criteria:
  - id: AC-001
    text: 'Criterion text for {component}'
    rfc2119: MUST
    component: {component}
    source_doc: {sourceDoc}
    source_type: document
";
        File.WriteAllText(Path.Combine(dir, fileName), yaml);
    }

    private sealed class TempSandbox : IDisposable
    {
        public string Path { get; }

        public TempSandbox()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "spectra-spec048-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ }
        }
    }
}
