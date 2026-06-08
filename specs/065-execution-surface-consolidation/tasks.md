---
description: "Task list for Execution Surface Consolidation (CLI run + MCP-as-adapter)"
---

# Tasks: Execution Surface Consolidation (CLI run + MCP-as-adapter)

**Input**: Design documents from `/specs/065-execution-surface-consolidation/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/run-cli.md

**Tests**: Requested by the spec (net-new tests) — test tasks included.

**Organization**: Grouped by user story. Foundational (Phase 2) is the extraction and BLOCKS every story.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Create `src/Spectra.Execution/Spectra.Execution.csproj` (net8.0; `ImplicitUsings`/`Nullable` enable; PackageRefs `Microsoft.Data.Sqlite`, `SixLabors.ImageSharp`; ProjectReference `..\Spectra.Core\Spectra.Core.csproj`; `InternalsVisibleTo` for `Spectra.Execution.Tests`, `Spectra.MCP.Tests`, `Spectra.CLI.Tests`, `Spectra.Integration.Tests`).
- [X] T002 Add `Spectra.Execution` to the solution file (`Spectra.sln`).
- [X] T003 [P] Create `tests/Spectra.Execution.Tests/Spectra.Execution.Tests.csproj` (xUnit; ProjectReference `Spectra.Execution`) and add to the solution.

---

## Phase 2: Foundational (Blocking Prerequisites) — the extraction

**⚠️ CRITICAL**: No user-story work begins until this phase builds green. Namespaces are PRESERVED on every moved file (research R1) — move files, do not edit their `namespace` lines, do not edit any `using`.

- [X] T004 Move the 5 execution files `ExecutionEngine.cs`, `TestQueue.cs`, `DependencyResolver.cs`, `StateMachine.cs`, `QueueReconstructionException.cs` from `src/Spectra.MCP/Execution/` to `src/Spectra.Execution/Execution/` — keep `namespace Spectra.MCP.Execution`.
- [X] T005 Move the 4 storage files `ExecutionDb.cs`, `RunRepository.cs`, `ResultRepository.cs`, `QueueSnapshotRepository.cs` from `src/Spectra.MCP/Storage/` to `src/Spectra.Execution/Storage/` — keep `namespace Spectra.MCP.Storage`.
- [X] T006 Move `UserIdentityResolver.cs` from `src/Spectra.MCP/Identity/` to `src/Spectra.Execution/Identity/` — keep `namespace Spectra.MCP.Identity`.
- [X] T007 Move `McpConfig.cs` (carries the `LogLevel` enum) from `src/Spectra.MCP/Infrastructure/` to `src/Spectra.Execution/Infrastructure/` — keep `namespace Spectra.MCP.Infrastructure`. Leave `McpLogging.cs` in `src/Spectra.MCP/Infrastructure/` (it consumes `LogLevel` across the new reference).
- [X] T008 Add `<ProjectReference Include="..\Spectra.Execution\Spectra.Execution.csproj" />` to `src/Spectra.MCP/Spectra.MCP.csproj` (keep its existing `Microsoft.Data.Sqlite`/`SixLabors.ImageSharp` refs for now).
- [X] T009 Add `<ProjectReference Include="..\Spectra.Execution\Spectra.Execution.csproj" />` to `src/Spectra.CLI/Spectra.CLI.csproj`.
- [X] T010 `dotnet build` the solution; resolve any reference fallout WITHOUT editing test files or `using` directives. Confirm `Spectra.MCP`, `Spectra.CLI`, and all four test projects compile.
- [X] T011 [P] Add a relocated-assembly smoke test in `tests/Spectra.Execution.Tests/ExtractionSmokeTests.cs` asserting `typeof(ExecutionEngine).Assembly.GetName().Name == "Spectra.Execution"` and a start→advance→finalize round-trip over a temp `ExecutionDb`.

**Checkpoint**: Engine lives in `Spectra.Execution`; both executables reference it; everything builds.

---

## Phase 3: User Story 3 — Networked MCP clients keep working (Priority: P1)

**Goal**: Prove the extraction preserved MCP behavior. **Independent Test**: the MCP transport/tool corpus passes unchanged.

- [X] T012 [US3] Run `dotnet test tests/Spectra.MCP.Tests` and confirm the full transport/tool/integration corpus passes with zero edits to those files (SC-003). If any fail, the extraction changed behavior — fix the extraction, not the tests.

**Checkpoint**: MCP adapter verified behavior-identical over the relocated engine.

---

## Phase 4: User Story 1 — One install drives execution from the CLI (Priority: P1) 🎯 MVP

**Goal**: A full manual loop runs via `spectra run …` from the one tool, no MCP. **Independent Test**: start→advance(all)→finalize on a workspace with only the CLI, no MCP config.

### Implementation

- [X] T013 [US1] Create `src/Spectra.CLI/Commands/Run/RunServices.cs` — a factory building `ExecutionEngine` (+ `RunRepository`/`ResultRepository`/`QueueSnapshotRepository`/`ExecutionDb`/`UserIdentityResolver`/`McpConfig`) and the index/test-case/suite/selection loader delegates, porting `src/Spectra.MCP/Program.cs:38–130` (loaders use only `Spectra.Core` types).
- [X] T014 [US1] Create `src/Spectra.CLI/Commands/Run/RunCommand.cs` — the `run` command group (`new Command("run", …)`), registering all subcommands; follow the `DeleteCommand` → handler pattern with `GlobalOptions` (verbosity/outputFormat/noInteraction).
- [X] T015 [US1] Register `new RunCommand()` in `src/Spectra.CLI/Program.cs` `CreateRootCommand()`.
- [X] T016 [P] [US1] `Commands/Run/StartRunHandler.cs` + `start <suite> [--priorities --tags --components --selection --test-ids --environment --name]` → `StartRunAsync`; print run id + first test (json/human).
- [X] T017 [P] [US1] `Commands/Run/StatusHandler.cs` + `status [<run-id>]` → active-run resolution + `GetStatusAsync`/`GetStatusCountsAsync`; render counts + current/next.
- [X] T018 [P] [US1] `Commands/Run/ShowTestHandler.cs` + `show <run-id> [--test-id|--handle]` → `GetQueueAsync` + test-case loader; auto-resolve in-progress/next-pending handle.
- [X] T019 [P] [US1] `Commands/Run/AdvanceHandler.cs` + `advance <handle> --status <pass|fail|blocked|skip> [--notes]` → `StartTestAsync` then `AdvanceTestAsync`; print blocked + next.
- [X] T020 [P] [US1] `Commands/Run/SkipHandler.cs` + `skip <handle> --reason <text> [--blocked]` → `SkipTestAsync`.
- [X] T021 [P] [US1] `Commands/Run/FinalizeHandler.cs` + `finalize <run-id> [--force]` → `FinalizeRunAsync`; generate reports via `ReportGenerator`/`ReportWriter` (port from `FinalizeExecutionRunTool`).
- [X] T022 [P] [US1] `Commands/Run/RunLifecycleHandlers.cs` + `pause`/`resume`/`cancel <run-id>`, `cancel-all`, `list-suites`, `list-active` → `PauseRunAsync`/`ResumeRunAsync`/`CancelRunAsync`/loaders/`RunRepository`.
- [X] T023 [P] [US1] `Commands/Run/RecordHandlers.cs` + `bulk-record --status <s> [--remaining|--test-ids a,b] [--reason]`, `note <handle> --note <text>`, `retest <run-id> --test-id <id>` → `BulkRecordResultsAsync`/`AddNoteAsync`/`RetestAsync`.
- [X] T024 [P] [US1] `Commands/Run/ReportingHandlers.cs` + `history [--suite]`, `summary <run-id>`, `test-history <test-id>`, `selections` → repositories/loaders (port from reporting tools).
- [X] T025 [US1] Map a thrown `QueueReconstructionException` to a distinct non-zero exit code + clear message in the run handlers, separate from benign "run not found" (FR-008); centralize in a small `RunHandlerBase`/helper.

### Tests

- [X] T026 [US1] `tests/Spectra.CLI.Tests/Commands/Run/RunLoopSmokeTests.cs` — drive `start → advance(all) → finalize` purely through the run handlers against a temp workspace with NO MCP server/config; assert results persisted + run Completed (SC-001).

**Checkpoint**: The CLI execution loop works end-to-end from one tool.

---

## Phase 5: User Story 2 — CLI execution behaves identically to MCP (Priority: P1)

**Goal/Independent Test**: each `run` subcommand leaves the same engine/DB state as its MCP tool counterpart.

> Implemented as the 4 facts in one `tests/Spectra.CLI.Tests/Commands/Run/ParityTests.cs` (advance, blocking, ordering, retest) rather than 4 files — all passing.

- [X] T027 [P] [US2] `tests/Spectra.CLI.Tests/Commands/Run/ParityAdvanceTests.cs` — advance/skip via the run handler; assert resulting `test_results` rows (status/notes/handle/attempt) equal what `ExecutionEngine.AdvanceTestAsync` produces directly (FR-007/SC-002).
- [X] T028 [P] [US2] `tests/Spectra.CLI.Tests/Commands/Run/ParityBlockingTests.cs` — a run with `DependsOn`; failing a parent via the run handler blocks dependents identically to the engine path.
- [X] T029 [P] [US2] `tests/Spectra.CLI.Tests/Commands/Run/ParityOrderingTests.cs` — `status`/`show` next-test selection is priority-then-topological (not alphabetical), matching the engine.
- [X] T030 [P] [US2] `tests/Spectra.CLI.Tests/Commands/Run/ParityFinalizeRetestTests.cs` — finalize pending-guard + retest behave identically across a fresh (short-lived) services instance.

**Checkpoint**: Parity proven at the shared engine boundary.

---

## Phase 6: User Story 4 — Short-lived-process SQLite safety (Priority: P2)

- [X] T031 [US4] In `src/Spectra.Execution/Storage/ExecutionDb.cs` `GetConnectionAsync`, after `OpenAsync` and before schema init, execute `PRAGMA journal_mode=WAL;` and `PRAGMA busy_timeout=5000;` (research R3).
- [X] T032 [US4] `tests/Spectra.Execution.Tests/Storage/WalConcurrencyTests.cs` — N concurrent short-lived `ExecutionDb` instances over one DB file each perform a write; assert zero `SQLITE_BUSY`/locked failures (SC-005), and assert `journal_mode` reports `wal`.

**Checkpoint**: Concurrent short-lived writers are safe.

---

## Phase 7: User Story 5 — CLI execution-loop guardrails (Priority: P2)

- [X] T033 [US5] Create `src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md` — the CLI execution-loop SKILL: present one test → wait for verdict → `spectra run advance/skip` ; ask before FAIL/BLOCKED/SKIP; never fabricate; never auto-advance. Drives `spectra run …` via Bash.
- [X] T034 [US5] Add `public static string Execute => All["spectra-execute"];` to `src/Spectra.CLI/Skills/SkillContent.cs`.
- [X] T035 [US5] Re-point `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` at `spectra run …` (CLI via Bash) as the default path, porting the guardrails verbatim in spirit; retain an optional MCP path for networked clients (US3).
- [X] T036 [US5] Update the skills-manifest flag-conformance test (the per-line `--no-interaction/--output-format/--verbosity` check) to exclude `spectra-execute`'s interactive `spectra run` loop commands, mirroring the `spectra-generate`/`spectra-update` seam exclusion.
- [X] T037 [P] [US5] `tests/Spectra.CLI.Tests/Skills/ExecuteSkillTests.cs` — assert `spectra-execute` is registered/embedded, `SkillContent.Execute` resolves, and the content enforces the guardrails (present→wait→advance; "never fabricate"; explicit-verdict-only).
- [X] T038 [P] [US5] `tests/Spectra.CLI.Tests/Commands/Run/GuardrailTests.cs` — `advance` with no `--status` does not record/advance; the handler records exactly the supplied status, never an inferred one (SC-006).

**Checkpoint**: Guardrails enforced mechanically and in the SKILL/agent.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T039 Create `src/Spectra.Execution/Screenshots/ScreenshotService.cs` — extract the ImageSharp encode/resize + OS clipboard-capture shellout from `SaveScreenshotTool`/`SaveClipboardScreenshotTool` (research R5).
- [~] T040 Refactor `src/Spectra.MCP/Tools/.../SaveScreenshotTool.cs` and `SaveClipboardScreenshotTool.cs` to delegate to `ScreenshotService` (behavior-preserving; MCP screenshot tests, if any, stay green). **DEFERRED (deliberate):** MCP screenshot tools left UNCHANGED to keep the report/screenshot tests green (FR-003 priority). The shared `ScreenshotService` is delivered and used by the CLI; MCP→service delegation is a safe future cleanup.
- [X] T041 [P] Add `screenshot <handle> --file <path>` and `screenshot-clipboard <handle>` handlers under `Commands/Run/` using `ScreenshotService` + `ResultRepository`.
- [X] T042 [P] Docs — fix factually-wrong setup/deployment docs that require a separate MCP install + per-client config for execution; update getting-started/usage/cli-reference and README install to the one-install + `spectra run` path; update architecture notes on the two surfaces (FR-009/SC-007). Files: `docs/usage.md`, `docs/cli-reference.md` (if present), `README.md`, `docs/specs/ARCHITECTURE-v2.md`/architecture notes.
- [X] T043 [P] Add the Spec 065 entry to the top of `## Recent Changes` in `CLAUDE.md`.
- [X] T044 Full solution test run (`dotnet test`) — all projects green; `Spectra.Core.Tests` + `TestPersistenceServiceTests` + `Spectra.MCP.Tests` unchanged and green (SC-003/SC-004). Run `quickstart.md` validation.

---

## Dependencies & Execution Order

- **Setup (P1: T001–T003)** → **Foundational (P2: T004–T011)** blocks everything.
- **US3 (T012)** immediately after Foundational (verification).
- **US1 (T013–T026)**: T013→T014→T015 sequential (shared files); T016–T024 are [P] (separate handler files) after T014; T025 after handlers; T026 after wiring.
- **US2 (T027–T030)** [P] after US1 implementation exists.
- **US4 (T031–T032)** independent after Foundational.
- **US5 (T033–T038)** after US1 (commands must exist to drive).
- **Polish (T039–T044)** last; T044 gates completion.

### Parallel opportunities

- T016–T024 (run subcommand handlers, distinct files) in parallel after T014.
- T027–T030 (parity tests, distinct files) in parallel.
- T037/T038, T041/T042/T043 in parallel.

## Implementation Strategy

**MVP** = Phases 1–4 (one-install CLI loop over the extracted engine) + Phase 3 verification. Phases 5–8 harden parity, concurrency, guardrails, screenshots, and docs.
