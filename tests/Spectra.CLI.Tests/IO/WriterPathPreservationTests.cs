using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Commands.Review;
using Spectra.CLI.Generation;
using Spectra.CLI.IO;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Coverage;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.IO;

/// <summary>
/// Spec 078 (US1, SC-001): every path that rewrites a test file keeps every field it does not own.
/// </summary>
public sealed class WriterPathPreservationTests : IDisposable
{
    private const string Suite = "smoke";
    private readonly TempWorkspace _ws = new("spectra-078-writers");
    private readonly string _path;

    public WriterPathPreservationTests()
    {
        _ws.AddSuite(Suite);
        _path = Path.Combine(_ws.TestCasesDir, Suite, $"{FullFieldTestCase.Id}.md");
        File.WriteAllText(_path, FullFieldTestCase.Markdown);
    }

    public void Dispose() => _ws.Dispose();

    private TestCase ReadBack() => FullFieldTestCase.Parse(File.ReadAllText(_path));

    private static TestPersistenceService Persistence() =>
        new(new TestFileWriter(), new IndexGenerator(), new IndexWriter());

    /// <summary>Lines of the original file that are missing from the rewritten one.</summary>
    private string[] LostLines(string before) =>
        before.Split('\n').Except(File.ReadAllText(_path).Split('\n')).ToArray();

    [Fact]
    public async Task PersistenceService_UnchangedTest_KeepsFileContent()
    {
        var original = ReadBack();

        await Persistence().PersistAsync(_ws.TestCasesDir, Suite, [original], [original]);

        TestCaseEquivalence.AssertEquivalent(original, ReadBack(), includeLayout: false);
        Assert.Empty(LostLines(FullFieldTestCase.Markdown));
    }

    [Fact]
    public async Task IngestUpdate_StepsEdit_KeepsEverythingElse()
    {
        var original = ReadBack();
        var editJson = """
            [
              {
                "id": "TC-100",
                "title": "Card declined shows an error",
                "priority": "high",
                "component": "checkout",
                "tags": ["smoke", "checkout"],
                "preconditions": "User is logged in with a saved card.",
                "steps": ["Open checkout", "Pay with the declined card", "See the banner"],
                "expected_result": "An error banner says the card was declined.",
                "test_data": "Card: 4000 0000 0000 0002",
                "scenario_from_doc": "Card is declined by the gateway",
                "source_refs": ["docs/checkout.md#Payment"],
                "criteria": ["AC-CHECKOUT-001", "AC-CHECKOUT-002"]
              }
            ]
            """;

        var result = await new UpdatedTestIngestor(Persistence())
            .IngestAsync(editJson, _ws.TestCasesDir, Suite, original, [original]);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var written = ReadBack();
        // Spec 090 (FR-A1): everything else is kept except the critic grounding block, which judged the old content.
        Assert.NotNull(original.Grounding);
        Assert.NotEqual(Spectra.Core.Models.Grounding.VerificationVerdict.Manual, original.Grounding!.Verdict);
        TestCaseEquivalence.AssertEquivalent(
            original with { Steps = ["Open checkout", "Pay with the declined card", "See the banner"], Grounding = null },
            written,
            includeLayout: false);
        Assert.Equal(new TimeSpan(1, 30, 15), written.EstimatedDuration);

        var text = File.ReadAllText(_path);
        Assert.Contains("owner: qa-team\n", text);
        Assert.Contains("x_ticket: {id: 42, system: jira}\n", text);
        Assert.Contains("## Notes\n\nHand-written notes the parser ignores.", text);

        // Only the Steps section lines (and the moved unknown keys, which are kept) differ — and, Spec 090 (FR-A1), the
        // critic grounding block, which an edit drops.
        var groundingBlock = GroundingBlockLines(FullFieldTestCase.Markdown);
        Assert.NotEmpty(groundingBlock);
        Assert.All(LostLines(FullFieldTestCase.Markdown), line =>
            Assert.True(line.StartsWith("1. ") || line.StartsWith("2. ") || line.StartsWith("3. ") || line.StartsWith("   - ")
                    || groundingBlock.Contains(line),
                $"unexpected lost line: '{line}'"));
        Assert.DoesNotContain("grounding:", text);
    }

    /// <summary>The <c>grounding:</c> line and its indented lines.</summary>
    private static HashSet<string> GroundingBlockLines(string markdown)
    {
        var lines = markdown.Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("grounding:", StringComparison.Ordinal));
        return start < 0 ? [] : lines.Skip(start).TakeWhile((l, i) => i == 0 || l.StartsWith(' ')).ToHashSet();
    }

    [Fact]
    public async Task GroundingWriteBack_ChangesOnlyGrounding()
    {
        var original = ReadBack();
        const string verdict = """
            { "verdict": "grounded", "score": 0.95, "critic_model": "claude-haiku",
              "findings": [ { "element": "Step 1", "claim": "c", "status": "grounded", "evidence": "e" } ] }
            """;

        var result = await new GroundingWriteBackService(new TestFileWriter()).WriteAsync(_path, verdict);

        Assert.True(result.IsSuccess);
        var written = ReadBack();
        TestCaseEquivalence.AssertEquivalent(original with { Grounding = written.Grounding }, written, includeLayout: false);
        Assert.Equal(VerificationVerdict.Grounded, written.Grounding!.Verdict);
        Assert.All(LostLines(FullFieldTestCase.Markdown), line => Assert.StartsWith("  ", line)); // grounding block lines only
    }

    [Fact]
    public async Task ReviewAccept_ChangesOnlyGrounding()
    {
        var original = ReadBack();
        var handler = new ReviewFlaggedHandler(_ws.Root, "test-cases");
        var flagged = new ReviewFlaggedHandler.FlaggedTest
        {
            Id = FullFieldTestCase.Id,
            Suite = Suite,
            Title = original.Title,
            FilePath = _path,
            Score = 0.873,
            RepairAttempts = 1,
            CondensedFindings = original.Grounding!.CondensedFindings
        };

        Assert.True(await handler.AcceptAsync(flagged, default));

        var written = ReadBack();
        Assert.False(written.Grounding!.FlaggedForReview);
        TestCaseEquivalence.AssertEquivalent(
            original with { Grounding = original.Grounding with { FlaggedForReview = false } },
            written,
            includeLayout: false);
        Assert.Equal(["  flagged_for_review: true"], LostLines(FullFieldTestCase.Markdown));
    }

    [Fact]
    public async Task PersistenceService_NewTest_RoundTripsEveryTypedField()
    {
        // Spec 099 F5: was BatchWriteTestsTool (deleted, no production caller); new tests reach the writer through the
        // persistence service.
        // Custom holds strings only: YAML scalars read back untyped as strings.
        var inMemory = FullFieldTestCase.Parse(FullFieldTestCase.Markdown) with
        {
            Layout = null,
            Custom = new Dictionary<string, object> { ["team"] = "payments", ["count"] = "5" }
        };
        var suitePath = Path.Combine(_ws.TestCasesDir, "batch");

        await Persistence().PersistAsync(_ws.TestCasesDir, "batch", [inMemory], [inMemory]);

        var written = FullFieldTestCase.Parse(File.ReadAllText(Path.Combine(suitePath, "TC-100.md")));
        TestCaseEquivalence.AssertEquivalent(inMemory, written, includeLayout: false);
    }

    [Fact]
    public async Task GroundingWriteBack_KeepsAutomationLink()
    {
        const string verdict = """
            { "verdict": "grounded", "score": 0.9, "critic_model": "m",
              "findings": [ { "element": "Step 1", "claim": "c", "status": "grounded", "evidence": "e" } ] }
            """;
        await new GroundingWriteBackService(new TestFileWriter()).WriteAsync(_path, verdict);

        var index = new IndexGenerator().Generate(Suite, [ReadBack()]);
        var reconciled = new LinkReconciler().Reconcile(
            new Dictionary<string, MetadataIndex> { [Suite] = index },
            new Dictionary<string, AutomationFileInfo>());

        Assert.True(reconciled.TestToAutomation.ContainsKey(FullFieldTestCase.Id));
    }
}
