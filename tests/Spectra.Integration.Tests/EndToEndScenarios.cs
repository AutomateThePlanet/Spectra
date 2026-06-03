using System.Text.Json;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Commands.Generate;
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

        // 050: criteria loaded and forwarded; resulting test carries them.
        var (context, matched) = await ws.LoadCriteriaAsync("checkout");
        Assert.True(matched > 0);
        var produced = TestFactory.Make("TC-900", "Checkout high via description", Priority.High,
            component: "checkout", criteria: ["AC-001"], filePath: "checkout/TC-900.md");
        var (test, agent) = await ws.GenerateFromDescriptionAsync("checkout", produced, context);
        Assert.NotNull(test);
        Assert.True(agent.ReceivedCriteria);
        Assert.NotEmpty(test!.Criteria);

        // 049: registered in the index.
        await ws.PersistAsync("checkout", new[] { test! });
        Assert.Contains(ws.ReadIndex("checkout").Tests, e => e.Id == "TC-900");

        // 051: a high-priority filtered run enqueues exactly the new test.
        var start = ws.BuildStartTool();
        var data = JsonDocument.Parse(await start.ExecuteAsync(
            JsonDocument.Parse("""{"suite":"checkout","priorities":["high"]}""").RootElement))
            .RootElement.GetProperty("data");

        Assert.Equal(1, data.GetProperty("test_count").GetInt32());
        Assert.Equal("TC-900", data.GetProperty("first_test").GetProperty("test_id").GetString());
    }

    // 047 + 050 (batch path criteria contract)
    [Fact]
    public async Task BatchGeneration_FromExtractedCriteria_PopulatesCriteriaField()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.SeedCriteriaAsync("payments", ("AC-010", "Card declined shows message"), ("AC-011", "Refund within 30 days"));

        var (context, matched) = await ws.LoadCriteriaAsync("payments");
        Assert.True(matched >= 2);

        var produced = TestFactory.Make("TC-500", "Pay with card", Priority.High,
            component: "payments", criteria: ["AC-010", "AC-011"], filePath: "payments/TC-500.md");
        var (test, agent) = await ws.GenerateFromDescriptionAsync("payments", produced, context);

        Assert.NotNull(test);
        Assert.True(agent.ReceivedCriteria);
        Assert.Equal(new[] { "AC-010", "AC-011" }, test!.Criteria.ToArray());
    }

    // 047
    [Fact]
    public async Task LargeCorpusExtraction_NoSilentSkip_AfterPartialFailure()
    {
        var docs = TestFactory.SyntheticCorpus(5);
        var failingDoc = docs[2].Path;
        var firstPassFailed = true;

        Func<DocumentEntry, CancellationToken, Task<IReadOnlyList<RequirementDefinition>>> extract =
            (doc, ct) =>
            {
                if (doc.Path == failingDoc && firstPassFailed)
                    throw new InvalidOperationException("transient extraction failure");
                return Task.FromResult(TestFactory.OneRequirement(doc.Path));
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
        var (test, _) = await ws.GenerateFromDescriptionAsync("search", produced, criteriaContext: null);
        await ws.PersistAsync("search", new[] { test! });

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
        Func<DocumentEntry, CancellationToken, Task<IReadOnlyList<RequirementDefinition>>> extractNothing =
            (_, _) => Task.FromResult<IReadOnlyList<RequirementDefinition>>(Array.Empty<RequirementDefinition>());

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

    // 048
    [Fact]
    public async Task GenerationNote_AppearsWhenNoCriteriaMatch()
    {
        await using var ws = new IntegrationWorkspace();
        // Criteria exist for a DIFFERENT suite, so the target suite has no match.
        await ws.SeedCriteriaAsync("auth", ("AC-001", "Login requires password"));

        var (_, matched) = await ws.LoadCriteriaAsync("checkout");
        Assert.Equal(0, matched);

        // The note is a result-level value (not gated on verbosity); it is present regardless.
        var note = GenerateHandler.BuildNoCriteriaNote(matched, "checkout");
        Assert.NotNull(note);
        Assert.Contains("checkout", note);

        // When criteria DO match, no note.
        var (_, matchedAuth) = await ws.LoadCriteriaAsync("auth");
        Assert.True(matchedAuth > 0);
        Assert.Null(GenerateHandler.BuildNoCriteriaNote(matchedAuth, "auth"));
    }
}
