using Spectra.CLI.Generation;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.Integration.Tests.Support;

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

        // 051: a high-priority filtered run enqueues exactly the new test (engine reads the on-disk index).
        var engine = ws.BuildEngine();
        var (_, queue) = await engine.StartRunAsync(
            "checkout", ws.IndexLoader("checkout"), filters: new RunFilters { Priorities = ["high"] });

        Assert.Equal(1, queue.TotalCount);
        Assert.Equal("TC-900", queue.GetNext()!.TestId);
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


    // 049
    [Fact]
    public async Task IndexDeployed_AfterFromDescription_FindTestCasesReturnsIt()
    {
        await using var ws = new IntegrationWorkspace();
        var produced = TestFactory.Make("TC-777", "Search returns results", Priority.Medium,
            tags: ["search"], component: "search", filePath: "search/TC-777.md");
        await ws.PersistAsync("search", new[] { produced });

        // The persisted test is discoverable through the same on-disk index the run surface loads from.
        var ids = ws.IndexLoader("search").Select(e => e.Id).ToList();
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

        // A priority-filtered run enqueues exactly the high-priority tests — no silent whole-suite fallback.
        var engine = ws.BuildEngine();
        var (_, queue) = await engine.StartRunAsync(
            "checkout", ws.IndexLoader("checkout"), filters: new RunFilters { Priorities = ["high"] });
        Assert.Equal(2, queue.TotalCount);
        Assert.NotEqual(fullSuiteSize, queue.TotalCount);

        // (The MCP JSON-RPC param-shape strictness that rejected singular 'priority' / nested 'filters'
        // was transport-only and was retired with the adapter in Spec 070; the engine takes a typed
        // RunFilters, so those malformed-JSON paths no longer exist on the surviving surface.)
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
