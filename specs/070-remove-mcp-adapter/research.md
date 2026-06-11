# Phase 0 Research: Remove the Spectra.MCP Execution Adapter

## R1 — Who actually depends on the `Spectra.MCP` *project*?

**Decision**: Only two project references exist (`grep -rln "Spectra.MCP" --include=*.csproj`):
`tests/Spectra.MCP.Tests/Spectra.MCP.Tests.csproj` and `tests/Spectra.Integration.Tests/Spectra.Integration.Tests.csproj`.
`Spectra.CLI` does **not** reference the MCP project (it references `Spectra.Core` + `Spectra.Execution`).

**Rationale**: The many `using Spectra.MCP.Execution/.Storage/.Identity/.Infrastructure/.Reports` directives
in `RunCommand.cs`, `RunHandler.cs`, `RunServices.cs`, `ConsoleEndpoints.cs`, and across `src/Spectra.Execution/*`
resolve to the **cosmetic namespaces that live inside `Spectra.Execution`** (Spec 065's "zero using edits"
design). Deleting the MCP *project* does not break them. Confirmed: `src/Spectra.Execution/Execution/ExecutionEngine.cs`,
`Storage/*`, `Reports/*`, `Identity/UserIdentityResolver.cs`, `Infrastructure/McpConfig.cs`,
`Screenshots/ScreenshotService.cs` all declare `namespace Spectra.MCP.*`.

**Consequence**: The blast radius of the project deletion is exactly: the solution file, the two test
projects, init's MCP emissions, and the skill/agent text. The CLI execution surface needs **zero** code
edits to keep compiling. The cosmetic-namespace rename is **out of scope** (FR-017).

## R2 — Screenshot path has a CLI home already

**Decision**: `ScreenshotService` already lives in `src/Spectra.Execution/Screenshots/ScreenshotService.cs`
(shared encode + OS clipboard capture). The CLI `spectra run screenshot` / `screenshot-clipboard` already
use it, with coverage in `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleScreenshotTests.cs`. The
MCP `SaveScreenshotTool`/`SaveClipboardScreenshotTool` are thin wrappers being deleted with the project.

**Rationale**: Confirms spec grounding that the local CLI host is a viable (better) screenshot host with no MCP server.

## R3 — Existing CLI/engine behavior coverage (the MAP target)

**Decision**: The surviving surfaces already carry meaningful coverage:
- `tests/Spectra.CLI.Tests/Commands/Run/ParityTests.cs` — engine-vs-handler parity for **advance** (row state, notes, attempt), **dependency blocking**, **priority-then-topological ordering**, **retest across fresh process**.
- `…/Run/GuardrailTests.cs` — advance-without-status rejected, exact-verdict-only, fail-without-notes rejected.
- `…/Run/RunLoopSmokeTests.cs` — full start→advance→finalize with **no MCP config present**; finalize pending-guard.
- `…/Run/WebConsole/ConsoleParityTests.cs`, `ConsoleGuardrailTests.cs`, `ConsoleScreenshotTests.cs`.
- `…/Skills/ExecuteSkillTests.cs`, `ExecutionAgentPortTests.cs` — bundled skill/agent content contracts.
- `tests/Spectra.Execution.Tests/` currently: `ExtractionSmokeTests`, `Identity/UserIdentityStabilityTests`, `Storage/WalConcurrencyTests`.

**Gap**: behaviors covered today **only** through the MCP tool layer and **not** yet on the CLI/engine
surface: skip+dependents cascade, add-note, **bulk-record**, pause, resume, cancel, **cancel-all**,
finalize+report-generation, get-status JSON contents, get-details, **get-run-history**, get-summary,
**list-active-runs**, list-available-suites, smart-selection flow. These drive the PORT list.

## R4 — Per-file disposition table for `tests/Spectra.MCP.Tests/` (satisfies FR-011)

Categories: **ENGINE** = touches only `Spectra.Execution` types → RELOCATE verbatim. **ADAPTER** =
references `Spectra.MCP.Tools.*` / `Spectra.MCP.Server.*`. Disposition: RELOCATE | PORT (rewrite to engine
or CLI) | RETIRE (transport-only; equivalent named).

| # | File | Category | Disposition | Equivalent / Target |
|---|------|----------|-------------|---------------------|
| 1 | Execution/DependencyResolverTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ (verbatim) |
| 2 | Execution/StateMachineTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ |
| 3 | Execution/TestQueueFilterTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ |
| 4 | Execution/TestQueueReconstructionTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ |
| 5 | Execution/ReconstructionParityTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ |
| 6 | Execution/ReconstructionFailLoudTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ (also covers QueueReconstructionException fail-loud → see #41) |
| 7 | Execution/ReconstructionBlockingParityTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ |
| 8 | Execution/ReconstructionOrderingParityTests.cs | ENGINE | RELOCATE | Execution.Tests/Execution/ |
| 9 | Storage/ExecutionDbTests.cs | ENGINE | RELOCATE | Execution.Tests/Storage/ |
| 10 | Storage/RunRepositoryTests.cs | ENGINE | RELOCATE | Execution.Tests/Storage/ |
| 11 | Models/TestHandleTests.cs | ENGINE | RELOCATE | Execution.Tests/Models/ |
| 12 | Helpers/PathHelperTests.cs | ENGINE | RELOCATE | Execution.Tests/Helpers/ |
| 13 | Reports/ReportGeneratorTests.cs | ENGINE | RELOCATE | Execution.Tests/Reports/ |
| 14 | Reports/ReportGeneratorFixesTests.cs | ENGINE | RELOCATE | Execution.Tests/Reports/ |
| 15 | Reports/ReportWriterEnrichmentTests.cs | ENGINE | RELOCATE | Execution.Tests/Reports/ |
| 16 | Integration/ConcurrentUsersTests.cs | ENGINE | RELOCATE | Execution.Tests/Integration/ (engine-direct WAL concurrency) |
| 17 | Integration/ReconstructedExecutionFlowTests.cs | ENGINE | RELOCATE | Execution.Tests/Integration/ (engine-direct) |
| 18 | Integration/ExecutionFlowTests.cs | ADAPTER | PORT→engine | Rewrite to ExecutionEngine start→advance→finalize; Execution.Tests/Integration/ |
| 19 | Integration/BlockingCascadeTests.cs | ADAPTER | PORT→engine (partial dup of ParityTests.Blocking) | Execution.Tests/Integration/ ; covers multi-level cascade |
| 20 | Integration/FilteredExecutionTests.cs | ADAPTER | PORT→engine | Execution.Tests/Integration/ (priority/tag/component filter + execute) |
| 21 | Integration/PauseResumeTests.cs | ADAPTER | PORT→engine | Execution.Tests/Integration/ (pause/resume state machine) |
| 22 | Integration/RetestFlowTests.cs | ADAPTER | PORT→engine (overlaps ParityTests.Retest) | Execution.Tests/Integration/ |
| 23 | Integration/SmartSelectionFlowTests.cs | ADAPTER | PORT→engine | Execution.Tests/Integration/ (saved-selection start) |
| 24 | Tools/AdvanceTestCaseTests.cs | ADAPTER | MAP (covered) + PORT residue | Existing: ParityTests.Advance_*, GuardrailTests; port any unique edge to engine |
| 25 | Tools/SkipTestCaseTests.cs | ADAPTER | PORT→engine | Execution.Tests (skip + dependents cascade) — not covered on CLI |
| 26 | Tools/AddTestNoteTests.cs | ADAPTER | PORT→engine | Execution.Tests (add-note persists) |
| 27 | Tools/BulkRecordResultsTests.cs | ADAPTER | PORT→engine | Execution.Tests (bulk: remaining / by-ids / reason) — **gap** |
| 28 | Tools/RetestTestCaseTests.cs | ADAPTER | MAP (covered) | ParityTests.Retest_AcrossFreshServices; port unique edge if any |
| 29 | Tools/GetTestCaseDetailsTests.cs | ADAPTER | PORT→engine | Execution.Tests (details from queue/index) |
| 30 | Tools/GetExecutionStatusTests.cs | ADAPTER | PORT→engine | Execution.Tests (status: progress/next/counts) |
| 31 | Tools/PauseExecutionRunTests.cs | ADAPTER | PORT→engine (see #21) | Execution.Tests |
| 32 | Tools/ResumeExecutionRunTests.cs | ADAPTER | PORT→engine (see #21) | Execution.Tests |
| 33 | Tools/CancelExecutionRunTests.cs | ADAPTER | PORT→engine | Execution.Tests (cancel terminal state) |
| 34 | Tools/CancelAllActiveRunsToolTests.cs | ADAPTER | PORT→engine | Execution.Tests (cancel-all) — **gap** |
| 35 | Tools/FinalizeExecutionRunTests.cs | ADAPTER | MAP+PORT | RunLoopSmokeTests covers finalize+report; port pending-guard/force nuance if unique |
| 36 | Tools/ListAvailableSuitesTests.cs | ADAPTER | PORT→engine/CLI | Execution.Tests or CLI list-suites |
| 37 | Tools/ListActiveRunsToolTests.cs | ADAPTER | PORT→engine | Execution.Tests (active-run listing) — **gap** |
| 38 | Tools/GetRunHistoryTests.cs | ADAPTER | PORT→engine | Execution.Tests (history via RunRepository) — **gap** |
| 39 | Tools/GetExecutionSummaryTests.cs | ADAPTER | PORT→engine | Execution.Tests (summary aggregation) |
| 40 | Tools/ActiveRunResolverTests.cs | ADAPTER | RETIRE | `ActiveRunResolver` is an MCP tool-layer helper, deleted with the project; auto-resolution on CLI is covered by ParityTests/RunLoopSmokeTests (advance with `handle: null`) |
| 41 | Tools/ReconstructionErrorSurfaceTests.cs | ADAPTER | RETIRE | Asserts `ToolRegistry` maps the engine exception to `RECONSTRUCTION_FAILED`; the *engine* fail-loud is preserved by #6. CLI surfaces it as its own outcome (RunHandler) — covered by retained CLI behavior. The error-CODE string is transport-specific. |
| 42 | Tools/FindTestCasesActionableErrorTests.cs | ADAPTER | RETIRE | Tool param-shape validation (Spec 051) for a deleted Data tool; transport-only |
| 43 | Server/McpProtocolStrictTests.cs | ADAPTER | RETIRE | `McpProtocol.DeserializeParams<T>` unknown-field strictness — pure JSON-RPC transport, deleted with the server |
| 44 | Tools/Data/ValidateTestsToolTests.cs | ADAPTER | TRIAGE→CLI | Operation == `spectra validate`; confirm CLI test coverage in CLI.Tests, port gap; retire JSON-shape residue |
| 45 | Tools/Data/RebuildIndexesToolTests.cs | ADAPTER | TRIAGE→CLI | Operation == index rebuild (`spectra index`/`docs index`); confirm CLI coverage, port gap |
| 46 | Tools/Data/AnalyzeCoverageGapsToolTests.cs | ADAPTER | TRIAGE→CLI | Operation == `spectra ai analyze --coverage`; confirm CLI coverage, port gap |
| 47 | Tools/Data/FromDescriptionDiscoveryTests.cs | ADAPTER | MAP | Spec 049 from-description index parity — covered by `Spectra.CLI.Tests/Commands/Generate/GenerateHandlerFromDescriptionIndexTests.cs` |
| 48 | Tools/Data/QuickstartScenariosTests.cs | ADAPTER | RETIRE/TRIAGE | Tool E2E quickstart narrative; behaviors covered by relocated engine + CLI tests — retire if no unique op |
| 49 | Tools/Data/PerformanceTests.cs | ADAPTER | RETIRE | Tool-layer micro-benchmark; not a behavior assertion (no SC depends on it) |

**Note on "gap" rows (#27, #34, #37, #38)**: these are behaviors with **no** current CLI/engine equivalent
— they MUST be ported before their source is deleted, or coverage regresses (FR-011 / SC-005).

## R5 — Re-pointing `tests/Spectra.Integration.Tests`

**Decision**: `Support/IntegrationWorkspace.cs` exposes `BuildStartTool()` → `StartExecutionRunTool` and
`BuildFindTool()` → `FindTestCasesTool`, and `EndToEndScenarios.cs` asserts `McpInvalidParamsException` on
bad params. Re-point by:
- Replacing `BuildStartTool()` with an engine-direct starter: `new ExecutionEngine(...).StartRunAsync(suite, entries)` using an on-disk index loader (mirroring `RunServices.IndexLoader` / `ParityTests`), or by invoking `RunHandler.StartAsync`.
- Replacing `BuildFindTool()` discovery with the engine/index loader path (the cross-spec intent is "generated+persisted tests are discoverable/startable" — assert via the index loader + `StartRunAsync`).
- Dropping the two `McpInvalidParamsException` assertions (`EndToEndScenarios.cs:116,120`): they assert MCP JSON-RPC param strictness, a transport concern removed with the adapter. The cross-spec *generation→persistence→index→start* coverage is preserved.
- Removing the `Spectra.MCP` ProjectReference from `Spectra.Integration.Tests.csproj` (it keeps `Spectra.CLI` + `Spectra.Core`; add `Spectra.Execution` if the engine types are used directly).

**Rationale**: Preserves the cross-spec flow (generation/persistence visible to execution) without the
deleted transport. Confirmed no integration scenario is unique to JSON-RPC transport.

## R6 — Init MCP emissions

**Decision**: Remove from `InitHandler.cs`: the `VsCodeMcpPath` const (`:25`), `CreateVsCodeMcpConfigAsync`
(`:87-88,381-396`), the `ClaudeSettingsInstaller.EnsureInstalledAsync` call (`:70-72`), and the two success
log lines naming the allowlist + mcp.json (`:105,115`). Delete `VsCodeMcpConfigInstaller.cs` and
`ClaudeSettingsInstaller.cs`. Verify no remaining caller of either installer (grep) before deletion. A peer
`.vscode/mcp.json` is left untouched because init simply no longer writes the file.

**Open item for implementation**: confirm `.claude/settings.json` is not *also* written for a non-MCP
reason elsewhere in init; if it is, keep that path and only drop the `mcp__spectra__*` entry. (Per spec
Assumption: only the MCP allowlist entry is removed, not necessarily the whole file.)

## R7 — Skill / agent re-homing

**Decision**: `spectra-execute.md` already says "No MCP server required" and uses `spectra run` throughout —
remove any residual MCP mention so it is CLI-only. `spectra-execution.agent.md:19-20` carries the optional
"Networked/remote setups may instead drive execution over the SPECTRA MCP server" fallback — remove it.
Preserve the **separate** SUT-driving MCP reference (BELLATRIX/Nova) and the bug-logging MCP reference
(Azure DevOps). Update/extend `ExecuteSkillTests.cs` / `ExecutionAgentPortTests.cs` to assert the absence of
the SPECTRA-MCP execution path (the existing `ExecutionAgent_DrivesCli_NotMcpToolsByDefault` test is the hook).

## R8 — Final disposition resolution (implementation outcome; satisfies FR-011 / SC-005)

Every `tests/Spectra.MCP.Tests/` file is now resolved — zero unresolved rows:

- **RELOCATED verbatim (16 files)** → `Spectra.Execution.Tests/{Execution,Storage,Models,Helpers,Reports,Integration}/` (rows 1–15, 17). All green in their new home (Execution.Tests 6 → 196).
- **PORTED to engine-direct (reclassified from "ENGINE")**: `Integration/ConcurrentUsersTests.cs` (row 16) actually referenced `Spectra.MCP.Tools.RunManagement` → rewritten to call `ExecutionEngine`/`CancelRunAsync`/`FinalizeRunAsync` directly (per-user active-run isolation).
- **PORTED to engine-direct (adapter substance)** → `Spectra.Execution.Tests/Engine/` and `…/Integration/`:
  - `AdvanceSkipNoteEngineTests.cs` ← Skip + AddTestNote (rows 24–26)
  - `BulkRecordEngineTests.cs` ← BulkRecordResults (row 27, **GAP closed**)
  - `CancelEngineTests.cs` ← Cancel + CancelAll (rows 33–34, **cancel-all GAP closed**)
  - `StatusDetailsEngineTests.cs` ← GetExecutionStatus + GetTestCaseDetails (rows 29–30)
  - `HistorySummaryListingEngineTests.cs` ← GetRunHistory + GetExecutionSummary + ListActiveRuns + ListAvailableSuites (rows 36–39, **run-history & list-active GAPs closed**; list-suites noted as CLI-layer suite-loader)
  - `FinalizeEngineTests.cs` ← FinalizeExecutionRun pending-guard/force (row 35; report-file gen stays in `RunLoopSmokeTests` + relocated Report tests)
  - `ExecutionFlowEngineTests.cs` ← ExecutionFlow + BlockingCascade + FilteredExecution (rows 18–20)
  - `PauseResumeRetestEngineTests.cs` ← PauseResume + Retest + (engine part of) SmartSelection (rows 21–23, 31–32)
  - Execution.Tests total: 196 → **228**.
- **MAP confirmed (already covered, no duplication)** (rows 24/28/35/47): advance/blocking/ordering/retest → `tests/Spectra.CLI.Tests/Commands/Run/ParityTests.cs`; guardrails → `GuardrailTests.cs`; finalize+report+no-MCP loop → `RunLoopSmokeTests.cs`; from-description discovery → `tests/Spectra.CLI.Tests/Commands/Generate/GenerateHandlerFromDescriptionIndexTests.cs` (all present and green at baseline).
- **Selection-by-name resolution** (SmartSelection, row 23): the name→`SavedSelectionConfig`→`RunFilters` resolution lives in CLI `RunHandler.StartAsync`/`RunServices.SelectionsLoader` — a CLI-layer concern. Covered by a new CLI test in Phase 5 (`--selection` start); the engine filter-application part is covered by `ExecutionFlowEngineTests` (test-ids/combined filters).
- **RETIRED — transport-only, with named survivor** (rows 40–46, 48–49):
  - `Server/McpProtocolStrictTests.cs` — JSON-RPC unknown-field strictness; transport, deleted with the server.
  - `Tools/ReconstructionErrorSurfaceTests.cs` — `ToolRegistry` error-CODE mapping (`RECONSTRUCTION_FAILED`); engine fail-loud preserved by relocated `ReconstructionFailLoudTests`; CLI surfaces its own outcome.
  - `Tools/ActiveRunResolverTests.cs` — MCP tool-layer helper, deleted; CLI handle auto-resolution covered by `ParityTests`/`RunLoopSmokeTests` (advance with null handle).
  - `Tools/FindTestCasesActionableErrorTests.cs` — tool param-shape validation (Spec 051); transport.
  - `Tools/Data/{ValidateTestsToolTests,RebuildIndexesToolTests,AnalyzeCoverageGapsToolTests}.cs` — operation independently covered by `Spectra.CLI.Tests/Commands/ValidateCommandTests`, `Spectra.Core.Tests/Index/*` (IndexGenerator/round-trips), `Spectra.Core.Tests/Coverage/*` (CoverageCalculator, Documentation/RequirementsCoverageAnalyzer, AutoLinkService). Only the tool-JSON wrapper is retired.
  - `Tools/Data/{QuickstartScenariosTests,PerformanceTests}.cs` — tool E2E narrative / micro-benchmark; no unique behavior assertion; no SC depends on them.

## Decisions summary

- **Engine untouched; rename out of scope** (R1, FR-009/FR-017).
- **Coverage preserved by relocate + port-before-delete**, transport-only tests retired with named equivalents (R3/R4, FR-011/FR-012).
- **Integration tests re-pointed to engine/CLI**, JSON-RPC param-strictness assertions dropped (R5, FR-013).
- **Init MCP emissions removed; peer files untouched** (R6, FR-004/FR-005/FR-006).
- **Skill/agent CLI-only; SUT-MCP preserved** (R7, FR-007/FR-008).
- **Constitution II/III stale wording left for a separate amendment** (plan Complexity Tracking).
