# Quickstart: Verify Spec 051 Locally

**Branch**: `051-filter-schema-alignment`
**Audience**: Developer reviewing or hand-testing the fix
**Time**: ~5 minutes after the branch builds

## The bug, in one sentence

`start_execution_run({ suite: "checkout", priorities: ["high"] })` used to run the **whole** suite because `priorities` was not a field on the request DTO and System.Text.Json silently dropped it. After this change it runs only the high-priority tests — or, if you send a misshaped field, you get an actionable error instead of a silent whole-suite run.

## Automated verification (no live model needed)

```powershell
dotnet test tests/Spectra.MCP.Tests
```

Key tests that must pass:

- `StartExecutionRun_TopLevelPriorities_FiltersSuite` — top-level `priorities` filters; `test_count` == matched, not full suite.
- `StartExecutionRun_TopLevelMultiplePriorities_OrSemantics` — `["high","critical"]` returns the union (and `critical`, not a real priority, simply matches nothing).
- `StartExecutionRun_TopLevelMixedFilters_AndSemantics` — `priorities` + `components` intersect.
- `StartExecutionRun_LegacyNestedFilters_StillWorks` — `filters: { priority: "high" }` still filters (deprecated path).
- `StartExecutionRun_BothShapes_TopLevelWins_LogsWarning` — top-level applied, warning recorded.
- `StartExecutionRun_UnknownField_ReturnsActionableError` — top-level singular `priority` → error naming `priority`, suggesting `priorities`; no run created.
- `StartExecutionRun_NestedPluralBranch_ReturnsActionableError` — `filters: { priorities: [...] }` → error suggesting top-level `priorities`.
- `FindTestCases_NestedFiltersObject_ReturnsActionableError` — nested `filters` on `find_test_cases` → error pointing to top-level arrays.
- `DeserializerDisallow_AppliesToAllTools` — an unknown property on any tool → structured error, not a silent drop.
- `RegressionGuard_PreviousSilentDropFormsNoLongerEnqueueWholeSuite` — the prior silent-drop forms now either filter correctly or error; none enqueues the unfiltered whole suite.
- `TestQueueFilterTests` (updated) — tag matching is now **OR**; `Build_FilterByMultipleTags_*` expects any-tag union.

## What to assert in code review

1. **`RunFilters` (Spectra.Core)** — has new `Priorities`/`Components` (raw strings), `Tags` now OR, `From(...)` factory, `HasFilters` extended. Singular `Priority`/`Component` retained for back-compat.
2. **`TestQueue.ApplyFilters`** — plural OR branches added; tag `.All(...)` changed to `.Any(...)`. `HasFilters` meaning and `TestIds` dependency recursion unchanged.
3. **`StartExecutionRunTool`** — request DTO gains top-level `priorities`/`tags`/`components`; legacy `Filters` is `[Obsolete]`; `NormalizeFilters` implements top-level-wins precedence; schema shows canonical fields + `deprecated: true` nested; internal `Filters` reads wrapped in `#pragma warning disable CS0618`.
4. **`McpProtocol`** — a dedicated strict params options instance with `UnmappedMemberHandling.Disallow`; `DeserializeParams<T>(args, toolName)`; `McpInvalidParamsException`; property+type extraction; suggestion function. The JSON-RPC **envelope** options are untouched (still lenient).
5. **`ToolRegistry.InvokeAsync`** — catches `McpInvalidParamsException` → `INVALID_PARAMS` structured error, before the generic `InternalError` catch.
6. **All 19 `DeserializeParams` call sites** — each passes its tool name.
7. **Agent prompts (both copies)** — one filter shape, named-suite example present, explicit "same shape on both tools / no nested filters" callout, zero nested-filter examples. Source and bundled copies match.

## Manual smoke (optional, needs the MCP server + an indexed suite)

Send these to `start_execution_run` and confirm outcomes:

```jsonc
{ "suite": "checkout", "priorities": ["high"] }            // → only high-priority enqueued
{ "suite": "checkout", "priority": "high" }                // → INVALID_PARAMS, suggests 'priorities'
{ "suite": "checkout", "filters": { "priority": "high" } } // → filters (deprecated path, still works)
```

If no indexed suite is available, the automated tests above fully cover the boundary behavior; note the skip in the PR.

## Negative / back-compat checks

- A filter-less run (`{ "suite": "checkout" }`) still runs the whole suite — correct, because no filter was requested.
- Existing tools that send only valid fields are unaffected by strict deserialization (verified by the full `Spectra.MCP.Tests` suite passing).
