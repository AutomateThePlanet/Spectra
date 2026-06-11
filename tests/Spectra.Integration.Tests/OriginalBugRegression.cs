using Spectra.CLI.Extraction;
using Spectra.CLI.Generation;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Models.Execution;
using Spectra.Integration.Tests.Support;

namespace Spectra.Integration.Tests;

/// <summary>
/// Spec 052 Part B — named regression guards. Each test's <c>DisplayName</c> is the
/// ORIGINAL user-reported symptom, so a CI failure reads as "this user bug is back"
/// rather than an internal method name. Reverting the matching 047–051 fix fails the
/// matching test (SC-003).
/// </summary>
public sealed class OriginalBugRegression
{
    // ── 050: extract-criteria on generation not working ───────────────────────

    [Fact(DisplayName = "Original bug: extract-criteria on generation not working")]
    public async Task ExtractCriteriaOnGeneration_PopulatesCriteriaField()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.SeedCriteriaAsync("checkout", ("AC-001", "Cart totals update on quantity change"));

        var (context, matched) = await ws.LoadCriteriaAsync("checkout");
        Assert.True(matched > 0, "criteria should match the suite by component");

        // Spec 059: the loaded criteria MUST flow into the compiled generation prompt (the
        // mandatory-mapping block) so the in-session agent maps them — the 050 guarantee, now on
        // the seam rather than the deleted in-process agent.
        var userPrompt = DescriptionPromptBuilder.Build("Checkout totals", null, "checkout", []);
        var prompt = PromptCompiler.Assemble(userPrompt, requestedCount: 1, criteriaContext: context);
        Assert.Contains("ACCEPTANCE CRITERIA — MANDATORY", prompt);
        Assert.Contains("AC-001", prompt);
    }

    // ── 049: from-description test missing from index / different shape ────────

    [Fact(DisplayName = "Original bug: from-description test has different format and is missing from index")]
    public async Task FromDescriptionTest_AppearsInIndexWithSameShape()
    {
        await using var ws = new IntegrationWorkspace();
        var peer = TestFactory.Make("TC-001", "Existing peer", Priority.Medium,
            tags: ["regression"], component: "checkout", filePath: "checkout/TC-001.md");

        var produced = TestFactory.Make("TC-900", "From description", Priority.High,
            tags: ["smoke"], component: "checkout", filePath: "checkout/TC-900.md");

        // Spec 059: persist the produced test as the seam's ingest-tests does, then assert index parity.
        await ws.PersistAsync("checkout", new[] { produced }, new[] { peer, produced });

        var index = ws.ReadIndex("checkout");
        var entry = Assert.Single(index.Tests, e => e.Id == "TC-900");
        var peerEntry = Assert.Single(index.Tests, e => e.Id == "TC-001");

        // Same shape as a peer entry: id-bearing, .md file, lowercase priority, fields present.
        Assert.False(string.IsNullOrWhiteSpace(entry.Id));
        Assert.EndsWith(".md", entry.File);
        Assert.Equal(entry.Priority, entry.Priority.ToLowerInvariant());
        Assert.Equal("high", entry.Priority);
        // Structural parity: both entries expose the same kind of File path shape.
        Assert.Equal(Path.GetExtension(peerEntry.File), Path.GetExtension(entry.File));
    }

    // ── 051: high-priority filter from a suite returned the whole suite ────────

    [Fact(DisplayName = "Original bug: high priority filter from a suite returns whole suite")]
    public async Task HighPriorityFilter_FromSuite_ReturnsOnlyHighPriority()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.PersistAsync("checkout", new[]
        {
            TestFactory.Make("TC-001", "High A", Priority.High, component: "checkout", filePath: "checkout/TC-001.md"),
            TestFactory.Make("TC-002", "Medium B", Priority.Medium, component: "checkout", filePath: "checkout/TC-002.md"),
            TestFactory.Make("TC-003", "High C", Priority.High, component: "checkout", filePath: "checkout/TC-003.md"),
            TestFactory.Make("TC-004", "Low D", Priority.Low, component: "checkout", filePath: "checkout/TC-004.md"),
        });

        var engine = ws.BuildEngine();
        var (_, queue) = await engine.StartRunAsync(
            "checkout", ws.IndexLoader("checkout"), filters: new RunFilters { Priorities = ["high"] });

        Assert.Equal(2, queue.TotalCount);     // exactly the two high-priority tests
        Assert.NotEqual(4, queue.TotalCount);  // NOT the whole suite (the original bug)
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static CriteriaExtractionResult Extracted(int count) =>
        new(ExtractionOutcome.Extracted,
            Enumerable.Range(1, count)
                .Select(i => new AcceptanceCriterion { Id = $"AC-{i:D3}", Text = $"Criterion {i}" })
                .ToList());

    private static CriteriaExtractionResult ParseFail() =>
        new(ExtractionOutcome.ParseFailure, Array.Empty<AcceptanceCriterion>());
}
