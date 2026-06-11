# Tasks: Remove the Spectra.MCP Execution Adapter

**Input**: Design documents from `/specs/070-remove-mcp-adapter/`
**Prerequisites**: plan.md, spec.md, research.md (R4 disposition table), data-model.md, contracts/removed-mcp-tools.md, quickstart.md

**Tests**: This feature is fundamentally about test-coverage preservation, so test tasks are first-class
(spec FR-011/FR-012, SC-005/SC-006). "Port" tasks rewrite MCP-tool tests to engine-direct tests.

**Organization**: By user story. Sequencing keeps the solution **green at every phase boundary** — the
test migration (Phase 2) runs while `Spectra.MCP` still exists, so nothing loses its source before its
equivalent is in place; deletion (US4) comes only after.

## Path Conventions

Multi-project .NET solution. Source under `src/`, tests under `tests/`. All paths absolute from repo root
`C:/SourceCode/Spectra/`.

---

## Phase 1: Setup & Baseline

**Purpose**: Establish a green baseline and the relocation target structure.

- [x] T001 Run `dotnet build Spectra.slnx` and `dotnet test Spectra.slnx`; record the baseline pass count per project (especially `Spectra.MCP.Tests`, `Spectra.Core.Tests`, `Spectra.Execution.Tests`) in the PR description as the pre-change green snapshot.
- [x] T002 [P] Create relocation target folders under `tests/Spectra.Execution.Tests/`: `Execution/`, `Storage/`, `Models/`, `Helpers/`, `Reports/`, `Integration/`, `Engine/`.
- [x] T003 [P] Confirm `tests/Spectra.Execution.Tests/Spectra.Execution.Tests.csproj` references `Spectra.Execution` (it does) and add a `Spectra.Core` reference only if a relocated/ported test needs `Spectra.Core` types not already transitively available (verify by build in T010).

**Checkpoint**: Baseline green recorded; targets exist.

---

## Phase 2: Foundational — Test Coverage Migration (BLOCKING for US4)

**Purpose**: Move/port all `Spectra.MCP.Tests` coverage to surviving projects **before** any deletion, so
no behavior loses its source. Satisfies FR-011/FR-012, SC-005/SC-006. `Spectra.MCP` stays present and the
whole solution keeps compiling throughout this phase.

⚠️ **Blocks US4 (deletion).** Do not delete `Spectra.MCP.Tests` until every research.md R4 row is resolved.

### Relocate pure-ENGINE tests (verbatim moves — no `using` edits; namespaces resolve from `Spectra.Execution`)

- [x] T004 [P] Move `tests/Spectra.MCP.Tests/Execution/{DependencyResolverTests,StateMachineTests,TestQueueFilterTests,TestQueueReconstructionTests,ReconstructionParityTests,ReconstructionFailLoudTests,ReconstructionBlockingParityTests,ReconstructionOrderingParityTests}.cs` → `tests/Spectra.Execution.Tests/Execution/` (R4 rows 1–8).
- [x] T005 [P] Move `tests/Spectra.MCP.Tests/Storage/{ExecutionDbTests,RunRepositoryTests}.cs` → `tests/Spectra.Execution.Tests/Storage/` (R4 rows 9–10).
- [x] T006 [P] Move `tests/Spectra.MCP.Tests/Models/TestHandleTests.cs` → `tests/Spectra.Execution.Tests/Models/` (R4 row 11).
- [x] T007 [P] Move `tests/Spectra.MCP.Tests/Helpers/PathHelperTests.cs` → `tests/Spectra.Execution.Tests/Helpers/` (R4 row 12).
- [x] T008 [P] Move `tests/Spectra.MCP.Tests/Reports/{ReportGeneratorTests,ReportGeneratorFixesTests,ReportWriterEnrichmentTests}.cs` → `tests/Spectra.Execution.Tests/Reports/` (R4 rows 13–15).
- [x] T009 [P] Move `tests/Spectra.MCP.Tests/Integration/{ConcurrentUsersTests,ReconstructedExecutionFlowTests}.cs` → `tests/Spectra.Execution.Tests/Integration/` (R4 rows 16–17). These are already engine-direct (no `*Tool` usage) — verify no `Spectra.MCP.Tools.*`/`Spectra.MCP.Server.*` using remains; if any, treat as PORT instead.
- [x] T010 Build `tests/Spectra.Execution.Tests`; confirm relocated files compile (namespaces `Spectra.MCP.Execution/.Storage/.Reports/...` resolve from `Spectra.Execution`). Fix the csproj per T003 if needed.

### Port engine-substance ADAPTER tests (rewrite to call `ExecutionEngine`/repositories directly; drop the `*Tool.ExecuteAsync(json)` + JSON-shape layer)

- [x] T011 [P] [US4] Port `Tools/AddTestNoteTests.cs` + `Tools/SkipTestCaseTests.cs` → `tests/Spectra.Execution.Tests/Engine/AdvanceSkipNoteEngineTests.cs` (add-note persists; skip records SKIPPED and cascades to dependents). R4 rows 25–26.
- [x] T012 [P] [US4] Port `Tools/BulkRecordResultsTests.cs` → `tests/Spectra.Execution.Tests/Engine/BulkRecordEngineTests.cs` — **GAP**: bulk by `--remaining`, by `test_ids`, with `reason`; verify each path against `ExecutionEngine.BulkRecordResultsAsync`. R4 row 27.
- [x] T013 [P] [US4] Port `Tools/CancelExecutionRunTests.cs` + `Tools/CancelAllActiveRunsToolTests.cs` → `tests/Spectra.Execution.Tests/Engine/CancelEngineTests.cs` — cancel → terminal state; cancel-all across multiple active runs (**GAP**). R4 rows 33–34.
- [x] T014 [P] [US4] Port `Tools/GetExecutionStatusTests.cs` + `Tools/GetTestCaseDetailsTests.cs` → `tests/Spectra.Execution.Tests/Engine/StatusDetailsEngineTests.cs` — status progress/counts/next; details from queue+index. R4 rows 29–30.
- [x] T015 [P] [US4] Port `Tools/GetRunHistoryTests.cs` + `Tools/GetExecutionSummaryTests.cs` + `Tools/ListActiveRunsToolTests.cs` + `Tools/ListAvailableSuitesTests.cs` → `tests/Spectra.Execution.Tests/Engine/HistorySummaryListingEngineTests.cs` — run-history (**GAP**), summary aggregation, list-active (**GAP**), list-suites. R4 rows 36–39.
- [x] T016 [P] [US4] Port `Tools/FinalizeExecutionRunTests.cs` → `tests/Spectra.Execution.Tests/Engine/FinalizeEngineTests.cs` — finalize + report generation + pending/force nuance (only assertions not already in `RunLoopSmokeTests`). R4 row 35.
- [x] T017 [P] [US4] Port `Integration/{ExecutionFlowTests,BlockingCascadeTests,FilteredExecutionTests}.cs` → `tests/Spectra.Execution.Tests/Integration/ExecutionFlowEngineTests.cs` — full start→advance→finalize, multi-level dependency cascade, priority/tag/component filtered execution, engine-direct. R4 rows 18–20.
- [x] T018 [P] [US4] Port `Integration/{PauseResumeTests,SmartSelectionFlowTests,RetestFlowTests}.cs` + `Tools/{PauseExecutionRunTests,ResumeExecutionRunTests}.cs` → `tests/Spectra.Execution.Tests/Integration/PauseResumeSelectionEngineTests.cs` — pause/resume state machine, saved-selection start, retest requeue (only assertions beyond `ParityTests.Retest_*`). R4 rows 21–23, 31–32.

### Verify MAP rows (behavior already covered — confirm, do not duplicate)

- [x] T019 [US4] Verify each MAP row in research.md R4 has a passing equivalent and annotate the table with the exact test name: advance/blocking/ordering/retest → `tests/Spectra.CLI.Tests/Commands/Run/ParityTests.cs`; guardrails → `GuardrailTests.cs`; finalize+report+no-MCP loop → `RunLoopSmokeTests.cs`; from-description discovery (R4 row 47) → `tests/Spectra.CLI.Tests/Commands/Generate/GenerateHandlerFromDescriptionIndexTests.cs`. Note residual unique edges and fold them into the relevant T011–T018 port.

### Triage Data-tool tests (port operation-level behavior to its CLI command test; retire JSON/param-shape residue)

- [x] T020 [US4] Triage `Tools/Data/ValidateTestsToolTests.cs` (R4 row 44): confirm `spectra validate` behavior coverage in `tests/Spectra.CLI.Tests/` (locate existing validate tests); port any unique operation assertion to a CLI validate test; mark JSON-shape residue RETIRE in the table.
- [x] T021 [US4] Triage `Tools/Data/RebuildIndexesToolTests.cs` (R4 row 45): confirm index-rebuild behavior coverage (`spectra index`/`docs index`) in CLI tests; port gap; mark residue RETIRE.
- [x] T022 [US4] Triage `Tools/Data/AnalyzeCoverageGapsToolTests.cs` (R4 row 46): confirm `spectra ai analyze --coverage` behavior coverage in CLI tests; port gap; mark residue RETIRE.
- [x] T023 [US4] Record RETIRE justification + named equivalent in research.md R4 for the transport-only files: `Server/McpProtocolStrictTests.cs`, `Tools/ReconstructionErrorSurfaceTests.cs`, `Tools/ActiveRunResolverTests.cs`, `Tools/FindTestCasesActionableErrorTests.cs`, `Tools/Data/QuickstartScenariosTests.cs`, `Tools/Data/PerformanceTests.cs`, `Tools/Data/FromDescriptionDiscoveryTests.cs` (rows 40–43, 47–49).
- [x] T024 Build + run `tests/Spectra.Execution.Tests` and `tests/Spectra.CLI.Tests`; confirm all relocated + ported tests pass and `Spectra.MCP.Tests` still compiles/passes (it loses the moved files but its csproj globs the rest). **Coverage table has zero unresolved rows (SC-005).**

**Checkpoint**: All MCP-test behavior now lives in (or is confirmed present in) surviving projects; solution still fully green WITH `Spectra.MCP` still present.

---

## Phase 3 (US4, P1): Remove `Spectra.MCP` — solution builds & green with MCP gone

**Goal**: Delete the adapter and prove SC-004/SC-005/SC-006/SC-007.
**Independent test**: `dotnet build` + `dotnet test` green with `Spectra.MCP` deleted; canary unmodified; no orphaned behavior.
**Depends on**: Phase 2 complete (T024).

- [x] T025 [US4] Re-point `tests/Spectra.Integration.Tests/Support/IntegrationWorkspace.cs`: replace `BuildStartTool()` (→ `StartExecutionRunTool`) with an engine-direct starter (`new ExecutionEngine(...).StartRunAsync(suite, entries)` via an on-disk index loader mirroring `RunServices.IndexLoader`/`ParityTests`), and replace `BuildFindTool()` (→ `FindTestCasesTool`) discovery with the index-loader path. Remove `using Spectra.MCP.Tools.*`. (research.md R5)
- [x] T026 [US4] Update `tests/Spectra.Integration.Tests/EndToEndScenarios.cs`: re-point the start/find assertions to the new engine path; remove the two `McpInvalidParamsException` assertions (`:116,:120`) and the `using Spectra.MCP.Server;` (transport-only, removed with the adapter). Keep the cross-spec generation→persistence→index→start coverage. Update `OriginalBugRegression.cs` likewise if it uses `BuildStartTool`.
- [x] T027 [US4] Remove the `Spectra.MCP` ProjectReference from `tests/Spectra.Integration.Tests/Spectra.Integration.Tests.csproj`; add `Spectra.Execution` ProjectReference if engine types are now used directly. Build `Spectra.Integration.Tests` (still with `Spectra.MCP` present) to confirm it compiles off the tools.
- [x] T028 [US4] Delete the `Spectra.MCP` project directory `src/Spectra.MCP/` in full (Server/, Tools/, Program.cs, Infrastructure/McpLogging.cs, Spectra.MCP.csproj, nupkg) — this also drops the `spectra-mcp` dotnet-tool packaging (FR-001/FR-002).
- [x] T029 [US4] Delete the now-migrated `tests/Spectra.MCP.Tests/` project directory in full (all files resolved by Phase 2).
- [x] T030 [US4] Edit `Spectra.slnx`: remove the `<Project Path="src/Spectra.MCP/Spectra.MCP.csproj" />` and `<Project Path="tests/Spectra.MCP.Tests/Spectra.MCP.Tests.csproj" />` entries (FR-003).
- [x] T031 [US4] `dotnet build Spectra.slnx` — confirm success, no dangling references. Then `dotnet test Spectra.slnx` — confirm green. Confirm via `git status` that `Spectra.Core.Tests`, `TestPersistenceService` tests, and the **pre-existing** `Spectra.Execution.Tests` files are unmodified (SC-007 canary).

**Checkpoint**: `Spectra.MCP` gone; solution builds; full suite green; canary intact.

---

## Phase 4 (US2, P1): `spectra init` emits no MCP wiring

**Goal**: Fresh `init` produces no `.vscode/mcp.json` and no `mcp__spectra__*` allowlist; nothing references `spectra-mcp`.
**Independent test**: Run `spectra init` in an empty dir → assert no mcp.json, no allowlist entry, peer mcp.json untouched.
**Depends on**: none (independent of deletion; init never referenced the MCP project).

- [x] T032 [US2] Grep for callers of `VsCodeMcpConfigInstaller` and `ClaudeSettingsInstaller` across `src/` to confirm `InitHandler` is the only caller (research.md R6 open item — also confirm `.claude/settings.json` is not written for a non-MCP reason elsewhere).
- [x] T033 [US2] Edit `src/Spectra.CLI/Commands/Init/InitHandler.cs`: remove the `VsCodeMcpPath` const (`:25`), the `CreateVsCodeMcpConfigAsync` method + its call site (`:87-88,:381-396`), the `ClaudeSettingsInstaller.EnsureInstalledAsync` call (`:70-72`), and the two success log lines naming the allowlist + mcp.json (`:105,:115`). Leave any non-MCP `.claude/settings.json` write intact (drop only the `mcp__spectra__*` entry) per the spec Assumption.
- [x] T034 [P] [US2] Delete `src/Spectra.CLI/Skills/VsCodeMcpConfigInstaller.cs` and `src/Spectra.CLI/Skills/ClaudeSettingsInstaller.cs` (FR-004/FR-005), only after T032 confirms no other caller.
- [x] T035 [US2] Update/remove init tests: find tests asserting `.vscode/mcp.json` creation or the `mcp__spectra__*` allowlist (e.g. installer unit tests + any `InitHandler` test) under `tests/Spectra.CLI.Tests/`; delete the installer-specific tests and update init tests to assert **absence** of mcp.json + allowlist and **preservation** of a peer `.vscode/mcp.json` (US2 acceptance scenarios 1–3, FR-006).
- [x] T036 [US2] Build + test; run `spectra init` in a scratch dir per quickstart §4 to manually confirm.

**Checkpoint**: init is MCP-free; peer files preserved.

---

## Phase 5 (US1, P1): Full execution lifecycle with no MCP server

**Goal**: start → advance → pause → resume → screenshot → bulk-record → finalize, all via `spectra run`, no MCP present.
**Independent test**: drive the full lifecycle on a workspace with no MCP config; report produced.
**Depends on**: deletion (US4) for the "no MCP present" guarantee, though the behavior already works.

- [x] T037 [US1] Extend `tests/Spectra.CLI.Tests/Commands/Run/RunLoopSmokeTests.cs` (or add `LifecycleSmokeTests.cs`) with a test that exercises pause → resume → bulk-record(`--remaining`) → finalize through `RunHandler` on a no-MCP-config workspace (asserts `!ws.HasMcpConfig`), complementing the existing start/advance/finalize loop (SC-001). Screenshot path is already covered by `WebConsole/ConsoleScreenshotTests.cs` — reference, don't duplicate.
- [x] T038 [US1] Run quickstart §3 manually (scratch workspace, only `spectra`); confirm each lifecycle step succeeds and an HTML report lands in `.execution/reports/` with no MCP process.

**Checkpoint**: lifecycle proven MCP-free.

---

## Phase 6 (US3, P2): Execution skill & agent reference only `spectra run`

**Goal**: skill/agent describe execution only via `spectra run`; no SPECTRA-MCP fallback; SUT/bug-log MCP preserved.
**Independent test**: read skill+agent content → no SPECTRA-MCP execution path; `ExecuteSkillTests`/`ExecutionAgentPortTests` green.
**Depends on**: none.

- [x] T039 [P] [US3] Edit `src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md`: remove any residual SPECTRA-MCP mention so it is CLI-only (FR-007). Keep the "No MCP server required" framing.
- [x] T040 [P] [US3] Edit `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`: remove the optional "Networked/remote setups may instead drive execution over the SPECTRA MCP server …" fallback (`:19-20`) (FR-008). Preserve the SUT-driving MCP (BELLATRIX/Nova) and bug-logging MCP (Azure DevOps) references.
- [x] T041 [US3] Update `tests/Spectra.CLI.Tests/Skills/ExecuteSkillTests.cs` + `ExecutionAgentPortTests.cs`: strengthen `ExecutionAgent_DrivesCli_NotMcpToolsByDefault` (and add a skill counterpart) to assert the SPECTRA-MCP **execution** path is absent while the separate SUT/bug-log MCP references remain (SC-003). Build + test.

**Checkpoint**: skill/agent CLI-only; SUT-MCP preserved.

---

## Phase 7: Polish & Cross-Cutting

- [x] T042 [P] Edit `docs/architecture/ARCHITECTURE-v2.md`: drop/rewrite the "Two surfaces: CLI and MCP", "Execution → MCP, only here", "stateful session → MCP", and "Do not unify" claims (`:38,:41,:43,:99`) to reflect the single CLI execution surface (FR-015).
- [x] T043 [P] Edit `docs/specs/ARCHITECTURE-v2.md`: apply the same edits as T042 (duplicate copy).
- [x] T044 [P] Update user-facing docs that describe the MCP execution server / `spectra-mcp` tool (e.g. `docs/.../cli-reference.md`, `usage.md`, README sections, CLAUDE.md "MCP Tools" section + "Recent Changes"): point execution at `spectra run`; remove `spectra-mcp` install guidance. Keep CLAUDE.md compact (<40K).
- [x] T045 Final sweep per quickstart §1: `grep -rln "Spectra\.MCP\b\|spectra-mcp\|mcp__spectra"` across `src/ tests/ docs/ Spectra.slnx` (excluding obj/bin) — confirm remaining matches are ONLY the cosmetic `Spectra.MCP.*` namespaces inside `Spectra.Execution` and the tests referencing them (FR-014/FR-017). No shipped skill/agent/doc, init code, or project file references the adapter.
- [x] T046 Full `dotnet build Spectra.slnx` + `dotnet test Spectra.slnx`; confirm green and record final pass counts vs the T001 baseline (engine + CLI counts should be ≥ baseline minus retired transport-only tests; canary unchanged). Update the spec's mapping table status to complete.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → no deps.
- **Phase 2 (Foundational test migration)** → depends on Phase 1. **BLOCKS Phase 3 (US4 deletion).** This is the critical-path: never delete `Spectra.MCP.Tests` before its coverage is relocated/ported/confirmed.
- **Phase 3 (US4 deletion)** → depends on Phase 2 (T024) + integration re-point (T025–T027).
- **Phase 4 (US2 init)** → independent; can run any time after Phase 1 (does not touch the MCP project). Place after US4 to keep one clean build sequence, or run in parallel by a second worker.
- **Phase 5 (US1 lifecycle)** → behavior works pre-deletion; the "no MCP present" proof is strongest after US4.
- **Phase 6 (US3 skill/agent)** → independent; any time after Phase 1.
- **Phase 7 (Polish/docs)** → after US2/US3/US4 land.

### Story independence

- **US4** is the structural removal + coverage proof (the feature's core); depends only on the Phase 2 migration.
- **US2**, **US3** are independent content/wiring edits — testable on their own.
- **US1** is largely a verification slice over already-working behavior.

## Parallel Opportunities

- Phase 1: T002, T003 in parallel.
- Phase 2 relocations T004–T009 are all `[P]` (distinct files). Ports T011–T018 are `[P]` (distinct new files) once relocations land.
- Phase 4 T034 `[P]` (after T032/T033). Phase 6 T039, T040 `[P]`. Phase 7 T042, T043, T044 `[P]`.
- US2, US3 phases can be worked by a separate contributor in parallel with US4 once Phase 2 is done.

## Implementation Strategy

**MVP = Phase 2 + US4**: migrate coverage, then delete `Spectra.MCP` and prove green — that alone delivers
the feature's core (single execution surface, no adapter) without breaking coverage. Layer US2 (init), US1
(lifecycle proof), US3 (skill/agent), then Polish (docs/sweep). Keep the canary (`Spectra.Core`,
`TestPersistenceService`, pre-existing `Spectra.Execution.Tests`) unmodified throughout — a canary failure
is a stop signal, not something to "fix" by editing those tests.
