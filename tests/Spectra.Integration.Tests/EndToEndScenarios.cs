using System.Text.Json;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Generation;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Integration.Tests.Support;
using Spectra.MCP.Server;

// ExtractCriteriaLoopAsync aggregates the (obsolete) RequirementDefinition type;
// these tests must speak that API until the production signature changes.
#pragma warning disable CS0618

namespace Spectra.Integration.Tests;

/// <summary>
/// Spec 052 Part A — cross-spec end-to-end scenarios. Each test drives the real
/// production services across a temporary workspace and asserts user-observable
/// outcomes spanning two or more of specs 047–051. Generation is hermetic via the
/// <c>agentFactory</c> seam (no live Copilot); the MCP tools read the REAL on-disk
/// <c>_index.json</c> the generation flow produced.
/// </summary>
public sealed class EndToEndScenarios
{
    // 049 + 050 + 051
    [Fact]
    public async Task FromDescriptionHighPriority_RunsViaFilter_EndToEnd()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.SeedCriteriaAsync("checkout", ("AC-001", "Totals update on quantity change"));

        // 050: criteria loaded for the suite (forwarding into the prompt is unit-tested on the seam).
        var (_, matched) = await ws.LoadCriteriaAsync("checkout");
        Assert.True(matched > 0);
        // Spec 059: generation runs in-session; the integration boundary persists the produced test
        // exactly as `ingest-tests` does, then asserts the cross-seam (index → MCP filter).
        var produced = TestFactory.Make("TC-900", "Checkout high via description", Priority.High,
            component: "checkout", criteria: ["AC-001"], filePath: "checkout/TC-900.md");
        Assert.NotEmpty(produced.Criteria);

        // 049: registered in the index.
        await ws.PersistAsync("checkout", new[] { produced });
        Assert.Contains(ws.ReadIndex("checkout").Tests, e => e.Id == "TC-900");

        // 051: a high-priority filtered run enqueues exactly the new test.
        var start = ws.BuildStartTool();
        var data = JsonDocument.Parse(await start.ExecuteAsync(
            JsonDocument.Parse("""{"suite":"checkout","priorities":["high"]}""").RootElement))
            .RootElement.GetProperty("data");

        Assert.Equal(1, data.GetProperty("test_count").GetInt32());
        Assert.Equal("TC-900", data.GetProperty("first_test").GetProperty("test_id").GetString());
    }

    // 047 + 050 (criteria flow into the generation prompt — now a seam concern)
    [Fact]
    public async Task BatchGeneration_FromExtractedCriteria_ForwardsCriteriaIntoPrompt()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.SeedCriteriaAsync("payments", ("AC-010", "Card declined shows message"), ("AC-011", "Refund within 30 days"));

        var (context, matched) = await ws.LoadCriteriaAsync("payments");
        Assert.True(matched >= 2);

        // Spec 059: the loaded criteria are forwarded into the compiled generation prompt (the
        // mandatory-mapping block) — the in-session agent then maps them. Assert on the seam output.
        var userPrompt = DescriptionPromptBuilder.Build("Pay with card", null, "payments", []);
        var prompt = PromptCompiler.Assemble(userPrompt, requestedCount: 1, criteriaContext: context);
        Assert.Contains("ACCEPTANCE CRITERIA — MANDATORY", prompt);
        Assert.Contains("AC-010", prompt);
        Assert.Contains("AC-011", prompt);
    }

    // 047
    [Fact]
    public async Task LargeCorpusExtraction_NoSilentSkip_AfterPartialFailure()
    {
        var docs = TestFactory.SyntheticCorpus(5);
        var failingDoc = docs[2].Path;
        var firstPassFailed = true;

        Func<DocumentEntry, CancellationToken, Task<RequirementsExtractionResult>> extract =
            (doc, ct) =>
            {
                if (doc.Path == failingDoc && firstPassFailed)
                    throw new InvalidOperationException("transient extraction failure");
                return Task.FromResult(new RequirementsExtractionResult(
                    ExtractionOutcome.Extracted, TestFactory.OneRequirement(doc.Path)));
            };

        // First pass: the failing doc is reported as failed (NOT cached/skipped silently).
        var first = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            docs, existing: [], extractPerDoc: extract,
            perDocDeadline: TimeSpan.FromSeconds(5),
            onSlowDoc: null, onDocFailure: null, ct: CancellationToken.None);
        Assert.Contains(failingDoc, first.FailedDocuments);
        Assert.Equal(docs.Count - 1, first.Aggregated.Count);

        // Re-run: the previously-failed doc is re-attempted and now succeeds.
        firstPassFailed = false;
        var second = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            docs, existing: [], extractPerDoc: extract,
            perDocDeadline: TimeSpan.FromSeconds(5),
            onSlowDoc: null, onDocFailure: null, ct: CancellationToken.None);
        Assert.Empty(second.FailedDocuments);
        Assert.Equal(docs.Count, second.Aggregated.Count);
    }

    // 049
    [Fact]
    public async Task IndexDeployed_AfterFromDescription_FindTestCasesReturnsIt()
    {
        await using var ws = new IntegrationWorkspace();
        var produced = TestFactory.Make("TC-777", "Search returns results", Priority.Medium,
            tags: ["search"], component: "search", filePath: "search/TC-777.md");
        await ws.PersistAsync("search", new[] { produced });

        var find = ws.BuildFindTool();
        var data = JsonDocument.Parse(await find.ExecuteAsync(
            JsonDocument.Parse("""{"suites":["search"]}""").RootElement)).RootElement.GetProperty("data");

        Assert.True(data.GetProperty("matched").GetInt32() >= 1);
        var ids = data.GetProperty("tests").EnumerateArray().Select(t => t.GetProperty("id").GetString()).ToList();
        Assert.Contains("TC-777", ids);
    }

    // 051
    [Fact]
    public async Task FilterSilentDrop_NoLongerOccurs()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.PersistAsync("checkout", new[]
        {
            TestFactory.Make("TC-001", "High A", Priority.High, component: "checkout", filePath: "checkout/TC-001.md"),
            TestFactory.Make("TC-002", "Medium B", Priority.Medium, component: "checkout", filePath: "checkout/TC-002.md"),
            TestFactory.Make("TC-003", "High C", Priority.High, component: "checkout", filePath: "checkout/TC-003.md"),
            TestFactory.Make("TC-004", "Low D", Priority.Low, component: "checkout", filePath: "checkout/TC-004.md"),
        });
        const int fullSuiteSize = 4;

        // Path C #2 — find_test_cases shape (canonical post-051): filters correctly.
        var start = ws.BuildStartTool();
        var count = JsonDocument.Parse(await start.ExecuteAsync(
                JsonDocument.Parse("""{"suite":"checkout","priorities":["high"]}""").RootElement))
            .RootElement.GetProperty("data").GetProperty("test_count").GetInt32();
        Assert.Equal(2, count);
        Assert.NotEqual(fullSuiteSize, count);

        // Path C #1 — top-level singular 'priority': actionable error, not whole-suite.
        await Assert.ThrowsAsync<McpInvalidParamsException>(() => ws.BuildStartTool().ExecuteAsync(
            JsonDocument.Parse("""{"suite":"checkout","priority":"high"}""").RootElement));

        // Path C #3 — plural nested under legacy 'filters': actionable error, not whole-suite.
        await Assert.ThrowsAsync<McpInvalidParamsException>(() => ws.BuildStartTool().ExecuteAsync(
            JsonDocument.Parse("""{"suite":"checkout","filters":{"priorities":["high"]}}""").RootElement));
    }

    // 048
    [Fact]
    public async Task CoverageGuards_FireOnRealisticZeroCorpus()
    {
        // A realistic corpus where every document extracts to nothing (inconclusive).
        var docs = TestFactory.SyntheticCorpus(8);
        // A genuine empty extraction is (Extracted, []) — cacheable, not a failure (parity with
        // the criteria path), so the corpus zero-criteria guard fires without marking docs failed.
        Func<DocumentEntry, CancellationToken, Task<RequirementsExtractionResult>> extractNothing =
            (_, _) => Task.FromResult(new RequirementsExtractionResult(
                ExtractionOutcome.Extracted, Array.Empty<RequirementDefinition>()));

        var loop = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            docs, existing: [], extractPerDoc: extractNothing,
            perDocDeadline: TimeSpan.FromSeconds(5),
            onSlowDoc: null, onDocFailure: null, ct: CancellationToken.None);

        // Documents indexed, zero criteria → the non-blocking guard fires (success, with warning).
        Assert.Empty(loop.FailedDocuments);
        Assert.Empty(loop.Aggregated);
        var warning = DocsIndexHandler.ComputeCriteriaWarning(docs.Count, loop.Aggregated.Count);
        Assert.NotNull(warning);
        Assert.Contains("extract-criteria", warning);
    }

    // 048 (criteria suite-match accounting — the basis for the no-criteria note)
    [Fact]
    public async Task CriteriaMatch_CountsZero_WhenNoCriteriaMatchSuite()
    {
        await using var ws = new IntegrationWorkspace();
        // Criteria exist for a DIFFERENT suite, so the target suite has no match.
        await ws.SeedCriteriaAsync("auth", ("AC-001", "Login requires password"));

        // Spec 059: the suite-match count drives the upstream "no criteria matched" signal.
        var (_, matched) = await ws.LoadCriteriaAsync("checkout");
        Assert.Equal(0, matched);

        // When criteria DO match, the count is positive.
        var (_, matchedAuth) = await ws.LoadCriteriaAsync("auth");
        Assert.True(matchedAuth > 0);
    }
}
