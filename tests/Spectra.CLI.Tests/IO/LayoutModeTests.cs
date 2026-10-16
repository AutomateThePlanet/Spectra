using Spectra.CLI.Generation;
using Spectra.CLI.IO;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Diagnostics;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.IO;

/// <summary>
/// Spec 099 F3 (FR-009–FR-012, SC-004): read-only commands parse without the 078 layout; every writer path keeps it;
/// the writer refuses a test that was parsed without it, instead of writing canonical output that would lose the file's
/// unknown keys, comments and sections. Relink (<c>RelinkSourcesTests.OnlyTheRefValuesChange_InTheirOwnQuotingStyle</c>),
/// legacy orphan marking (<c>UpdateHandlerFilePathTests</c>), review accept and grounding write-back
/// (<c>WriterPathPreservationTests</c>) have their own lossless rows, which now also fail loudly on a layout-less parse.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class LayoutModeTests : IDisposable
{
    private const string Suite = "storage";
    private const string Quotas = "docs/storage.md#Quotas";

    /// <summary>A hand-edited test: CRLF, a column-0 comment, an unknown key and an unknown body section.</summary>
    private static string HandEdited(string id, string extra = "") =>
        ("---\n"
         + $"id: {id}\n"
         + "priority: high\n"
         + "# reviewed by QA\n"
         + "owner: team-a\n"
         + extra
         + $"source_refs:\n  - \"{Quotas}\"\n"
         + "---\n\n"
         + $"# Quota test {id}\n\n"
         + "## Steps\n\n1. Upload\n\n"
         + "## Expected Result\n\nIt works\n\n"
         + "## Notes\n\nKeep this section.\n").Replace("\n", "\r\n");

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public LayoutModeTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteCriteriaFile("docs/storage.criteria.yaml",
            "criteria:\n  - id: AC-Q1\n    text: \"Quota MUST be 30GB\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/storage.md\n    source_section: Quotas\n    component: storage\n");
        Directory.CreateDirectory(_ws.PathOf($"test-cases/{Suite}"));
        File.WriteAllText(_ws.TestPath(Suite, "TC-101"), HandEdited("TC-101"));
        File.WriteAllText(_ws.TestPath(Suite, "TC-102"), HandEdited("TC-102", "depends_on: TC-101\n"));
        File.WriteAllText(_ws.TestPath(Suite, "TC-103"), HandEdited("TC-103"));
        _ws.RebuildIndex(Suite);
    }

    public void Dispose() => _ws.Dispose();

    private static void AssertHandEditsKept(string text)
    {
        Assert.Contains("# reviewed by QA\r\n", text);
        Assert.Contains("owner: team-a\r\n", text);
        Assert.Contains("## Notes\r\n\r\nKeep this section.\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
    }

    // ------------------------------------------------------------------ (a) the writer

    [Fact]
    public async Task Writer_RefusesATestParsedWithoutLayout_AndWritesNothing()
    {
        var path = _ws.TestPath(Suite, "TC-101");
        var before = File.ReadAllBytes(path);
        var test = new TestCaseParser(captureLayout: false).Parse(File.ReadAllText(path), "TC-101.md").Value!;

        var format = Assert.Throws<InvalidOperationException>(() => new TestFileWriter().FormatTestCase(test));
        Assert.Contains("TC-101", format.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TestFileWriter().WriteAsync(path, test with { Title = "Edited" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TestFileWriter().WriteAsync(_ws.PathOf("test-cases/new/TC-101.md"), test));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(Directory.Exists(_ws.PathOf("test-cases/new")));
    }

    [Fact]
    public void Writer_ANewTestBuiltInMemory_IsWrittenCanonically()
    {
        var test = new TestCase { Id = "TC-9", Title = "New", Priority = Priority.High, Steps = ["Do"], ExpectedResult = "Done", FilePath = "TC-9.md" };

        Assert.Null(test.Layout);
        Assert.False(test.LayoutOmitted);
        Assert.StartsWith("---\nid: TC-9\n", new TestFileWriter().FormatTestCase(test));
    }

    [Fact]
    public void Parser_DefaultCapturesLayout_ReadOnlyModeMarksTheTest()
    {
        var text = File.ReadAllText(_ws.TestPath(Suite, "TC-101"));

        var writable = new TestCaseParser().Parse(text, "TC-101.md").Value!;
        var readOnly = new TestCaseParser(captureLayout: false).Parse(text, "TC-101.md").Value!;

        Assert.NotNull(writable.Layout);
        Assert.False(writable.LayoutOmitted);
        Assert.Null(readOnly.Layout);
        Assert.True(readOnly.LayoutOmitted);
        TestCaseEquivalence.AssertEquivalent(writable, readOnly, includeLayout: false);
    }

    // ------------------------------------------------------------------ (b) writer paths keep the layout

    [Fact]
    public async Task IngestUpdateEdit_KeepsTheHandEdits()
    {
        var edit = _ws.PathOf(".spectra/edit.json");
        File.WriteAllText(edit, "[\n" + UpdatePromptCompiler.SerializeTest(_ws.Load(Suite, "TC-101") with { ExpectedResult = "Quota enforced" }) + "\n]\n");

        var (exit, output, error) = await _ws.RunAsync("ai", "ingest-update", Suite, "--test-id", "TC-101", "--from", edit, "--output-format", "json");

        Assert.True(exit == 0, output + error);
        var text = File.ReadAllText(_ws.TestPath(Suite, "TC-101"));
        Assert.Contains("Quota enforced", text);
        AssertHandEditsKept(text);
    }

    [Fact]
    public async Task IngestGroundingAll_KeepsTheHandEdits()
    {
        _ws.WriteVerdict("TC-101", "grounded");

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", Suite, "--all", "--ids", "TC-101");

        Assert.True(exit == 0, output + error);
        var text = File.ReadAllText(_ws.TestPath(Suite, "TC-101"));
        Assert.Contains("grounding:", text);
        AssertHandEditsKept(text);
    }

    [Fact]
    public async Task DeleteOfADependency_KeepsTheDependentsHandEdits()
    {
        var (exit, output, error) = await _ws.RunAsync("delete", "TC-101", "--force", "--output-format", "json");

        Assert.True(exit == 0, output + error);
        var text = File.ReadAllText(_ws.TestPath(Suite, "TC-102"));
        Assert.DoesNotContain("depends_on", text);
        AssertHandEditsKept(text);
    }

    // ------------------------------------------------------------------ (c) read-only commands capture no layout

    public static TheoryData<string[]> ReadOnlyCommands => new()
    {
        new[] { "validate" },
        new[] { "show", "TC-101" },
        new[] { "index" },
        new[] { "ai", "update-triage", "--suite", Suite, "--dry-run" },
        new[] { "ai", "audit-grounding", "--suite", Suite },
        new[] { "ai", "analyze", "--coverage", "--format", "json" },
        new[] { "dashboard", "--output", "site", "--dry-run" },
        new[] { "ai", "compile-critic-prompt", "--suite", Suite, "--test", "TC-101" },
        new[] { "ai", "compile-repair-batch", "--suite", Suite },
        new[] { "ai", "compile-prompt", "--suite", Suite, "--from-description", "A quota test" },
        new[] { "ai", "compile-analysis-prompt", "--suite", Suite },
        new[] { "ai", "ingest-analysis", "--suite", Suite, "--from", ".spectra/analysis.json" },
        new[] { "ai", "ingest-verdict", "--suite", Suite, "--all" },
        new[] { "ai", "review-flagged", "--suite", Suite, "--no-interaction" },
    };

    [Theory]
    [MemberData(nameof(ReadOnlyCommands))]
    public async Task ReadOnlyCommand_ParsesTests_WithoutCapturingLayout(string[] command)
    {
        await _ws.InitBaselineAsync(Suite);
        _ws.WriteVerdict("TC-101", "partial");
        await _ws.WriteCriticGroundingAsync(Suite, "TC-103", Spectra.Core.Models.Grounding.VerificationVerdict.Partial, flagged: true);
        await _ws.PlanAsync(GenerationPlanWorkspace.AnalysisJson(("Quota is enforced", Quotas)), Suite);
        File.WriteAllText(_ws.PathOf(".spectra/analysis.json"), GenerationPlanWorkspace.AnalysisJson(("Quota is enforced", Quotas)));

        using var scope = ParseCounter.Begin();
        await _ws.RunAsync([.. command, "--output-format", "json"]);

        Assert.True(scope.FullParses > 0, $"{string.Join(' ', command)} parsed no test (the row proves nothing)");
        Assert.Equal(0, scope.LayoutCaptures);
    }
}
