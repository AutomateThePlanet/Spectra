---

description: "Task list for Spec 040 — Test Lifecycle & Process Control (branch 046-test-lifecycle-control)"
---

# Tasks: Test Lifecycle & Process Control

**Input**: Design documents from `/specs/046-test-lifecycle-control/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Included. The spec's Test Plan Summary explicitly requests ~74 new tests. Test tasks are interleaved with implementation.

**Organization**: Tasks are grouped by user story (US1=ID allocator, US2=Cancellation, US3=Delete, US4=Suite ops) so each story can be implemented, tested, and deployed independently.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story this task belongs to (US1, US2, US3, US4)
- All paths are repository-relative (rooted at `C:/SourceCode/Spectra`)

## Path Conventions

- Production code: `src/Spectra.Core/`, `src/Spectra.CLI/`
- Tests: `tests/Spectra.Core.Tests/`, `tests/Spectra.CLI.Tests/`
- SKILL templates: `src/Spectra.CLI/Skills/Content/Skills/`, `src/Spectra.CLI/Skills/Content/Agents/`
- Docs: `CLAUDE.md`, `CHANGELOG.md`, `docs/cli-reference.md`, `docs/test-format.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Version bump and directory scaffolding before any code lands.

- [X] T001 Bump version `1.51.4` → `1.52.0` in `Directory.Build.props` (line 19)
- [X] T002 [P] Create directory `src/Spectra.Core/IdAllocation/` with placeholder `.gitkeep`
- [X] T003 [P] Create directory `src/Spectra.Core/Models/Lifecycle/` with placeholder `.gitkeep`
- [X] T004 [P] Create directory `src/Spectra.CLI/Cancellation/` with placeholder `.gitkeep`
- [X] T005 [P] Create directories for new commands: `src/Spectra.CLI/Commands/Delete/`, `src/Spectra.CLI/Commands/Suite/`, `src/Spectra.CLI/Commands/Cancel/`, `src/Spectra.CLI/Commands/Doctor/` (each with `.gitkeep`)
- [X] T006 [P] Create test directories: `tests/Spectra.Core.Tests/IdAllocation/`, `tests/Spectra.CLI.Tests/Cancellation/`, `tests/Spectra.CLI.Tests/Commands/Delete/`, `tests/Spectra.CLI.Tests/Commands/Suite/`, `tests/Spectra.CLI.Tests/Commands/Cancel/`, `tests/Spectra.CLI.Tests/Commands/Doctor/`, `tests/Spectra.CLI.Tests/Integration/Cancellation/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Result models and shared helpers used by all four user stories. Must complete before any user story phase begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 [P] Create `DeleteResult` record in `src/Spectra.Core/Models/Lifecycle/DeleteResult.cs` per `contracts/result-json.md` §2 (inherits `CommandResult`, fields: `dry_run`, `deleted[]`, `dependency_cleanup[]`, `skipped[]`, `errors[]`)
- [X] T008 [P] Create `SuiteListResult`, `SuiteRenameResult`, `SuiteDeleteResult` records in `src/Spectra.Core/Models/Lifecycle/SuiteResults.cs` per `contracts/result-json.md` §§3–5
- [X] T009 [P] Create `CancelResult` record in `src/Spectra.Core/Models/Lifecycle/CancelResult.cs` per `contracts/result-json.md` §6 (fields: `target_pid?`, `target_command?`, `shutdown_path`, `elapsed_seconds`, `force`)
- [X] T010 [P] Create `DoctorIdsResult` record in `src/Spectra.Core/Models/Lifecycle/DoctorIdsResult.cs` per `contracts/result-json.md` §7 (fields: `fix_applied`, `total_tests`, `unique_ids`, `duplicates[]`, `index_mismatches[]`, `high_water_mark`, `next_id`, `renumbered[]`, `unfixable_references[]`)
- [X] T011 Add stable status-string constants `"cancelled"` and `"no_active_run"` (XML doc comment block) at top of `src/Spectra.CLI/Results/CommandResult.cs` — pure documentation, no code-level enum (per Decision 6 in research.md)
- [X] T012 [P] Create test helper `tests/Spectra.CLI.Tests/TestFixtures/TempWorkspace.cs` that builds a disposable temp workspace with `test-cases/`, `spectra.config.json`, and `.spectra/` directories — used by lifecycle and cancellation tests
- [X] T013 [P] ~~Create test helper `tests/Spectra.Core.Tests/TestFixtures/TestCaseBuilder.cs`~~ — folded into `TempWorkspace.AddTestCase()` from T012 (no separate Core.Tests builder needed; the existing frontmatter parser is already covered by Core tests)

**Checkpoint**: Foundation ready — all four user stories can now begin in parallel.

---

## Phase 3: User Story 1 - Prevent duplicate test IDs (Priority: P1) 🎯 MVP

**Goal**: Globally unique test IDs even under concurrent generation; on-demand audit and repair via `spectra doctor ids`.

**Independent Test**: Run two generation operations in parallel against an empty workspace; confirm zero duplicate IDs and a monotonic high-water-mark in `.spectra/id-allocator.json`.

### Tests for User Story 1 ⚠️

> Write tests FIRST (xUnit conventions: structured-result assertions, never throw on validation errors).

- [X] T014 [P] [US1] `HighWaterMarkStoreTests.cs` in `tests/Spectra.Core.Tests/IdAllocation/` — round-trip read/write, missing file → returns 0, corrupted file → re-seed, version mismatch → re-seed, atomic temp+rename
- [X] T015 [P] [US1] `FileLockHandleTests.cs` in `tests/Spectra.Core.Tests/IdAllocation/` — held-lock contention, release on dispose, retry-with-backoff, timeout after 10 s throws `TimeoutException`
- [X] T016 [P] [US1] `TestCaseFrontmatterScannerTests.cs` in `tests/Spectra.Core.Tests/IdAllocation/` — empty workspace, malformed frontmatter (skipped + warned), stale-index detection, returns max numeric ID
- [X] T017 [P] [US1] `PersistentTestIdAllocatorTests.cs` in `tests/Spectra.Core.Tests/IdAllocation/` — concurrent allocation across two threads (real lock file in temp workspace) yields disjoint ranges; HWM monotonicity; deleted-ID-never-reused; `id_start` floor honored on first allocation; lock timeout surfaces as exception
- [X] T018 [P] [US1] `DoctorIdsHandlerTests.cs` in `tests/Spectra.CLI.Tests/Commands/Doctor/` — read-only path (zero duplicates, two duplicates, index mismatch reporting); `--fix` keeps oldest, renumbers later occurrences, updates `depends_on` references, lists `unfixable_references` for non-literal automation refs
- [X] T019 [P] [US1] CLI smoke test `DoctorIdsCommandTests.cs` in `tests/Spectra.CLI.Tests/Commands/Doctor/` — exit codes 0 (clean), 9 (`DUPLICATES_FOUND` with `--no-interaction`), 0 (after `--fix`)

### Implementation for User Story 1

- [X] T020 [P] [US1] Implement `HighWaterMarkStore` in `src/Spectra.Core/IdAllocation/HighWaterMarkStore.cs` — `ReadAsync()`, `WriteAsync(int newHwm, string command)` with atomic temp-and-rename; treats unreadable/version-mismatched file as absent and logs warning (per research.md Decision 3)
- [X] T021 [P] [US1] Implement `FileLockHandle : IDisposable` in `src/Spectra.Core/IdAllocation/FileLockHandle.cs` — `AcquireAsync(string lockPath, TimeSpan timeout, CancellationToken ct)` using `FileShare.None`, exponential backoff 50 ms→1 s, throws `TimeoutException` after 10 s (per research.md Decision 2)
- [X] T022 [P] [US1] Implement `TestCaseFrontmatterScanner` in `src/Spectra.Core/IdAllocation/TestCaseFrontmatterScanner.cs` — async filesystem walk under `test-cases/**/*.md`, parses frontmatter via existing `Spectra.Core.Parsing` parser, returns max numeric ID; per-process `Lazy<Task<int>>` cache
- [X] T023 [US1] Implement `PersistentTestIdAllocator` in `src/Spectra.Core/IdAllocation/PersistentTestIdAllocator.cs` — async wrapper that orchestrates HWM read → lock acquire → filesystem+index scan → delegate to existing in-memory `Spectra.Core.Index.TestIdAllocator` for ID formatting → HWM write → lock release. Public API: `Task<IReadOnlyList<string>> AllocateAsync(int count, string idPrefix, int idStart, CancellationToken ct)`. (Depends on T020, T021, T022.)
- [X] T024 [US1] Wire `PersistentTestIdAllocator` into `src/Spectra.CLI/Agent/Tools/GetNextTestIdsTool.cs` — replace direct `TestIdAllocator.AllocateMany()` call with `PersistentTestIdAllocator.AllocateAsync()`. Preserve the existing 1–100 count guard.
- [X] T025 [US1] Wire `PersistentTestIdAllocator` into `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` and any agent code path that allocates IDs (search for `TestIdAllocator.FromExistingTests`, `TestIdAllocator.FromExistingIds`, `AllocateMany` usages outside tests). Replace with persistent-allocator calls; pass through `CancellationToken`.
- [X] T026 [US1] On first run, emit one-time info log `Initialized ID allocator: high water mark = TC-NNN` from `PersistentTestIdAllocator` when `HighWaterMarkStore` reports an absent file (per FR-009)
- [X] T027 [P] [US1] Create `DoctorCommand` parent in `src/Spectra.CLI/Commands/Doctor/DoctorCommand.cs` and register `ids` subcommand
- [X] T028 [US1] Implement `DoctorIdsHandler` in `src/Spectra.CLI/Commands/Doctor/DoctorIdsHandler.cs` — read-only path: walk `test-cases/`, build all-IDs inventory, detect duplicates, detect index-vs-disk mismatches, read HWM, compute next ID, emit `DoctorIdsResult` to `.spectra-result.json`. Exit 9 (`DUPLICATES_FOUND`) when `--no-interaction` and duplicates exist without `--fix`. (Depends on T010, T020, T022.)
- [X] T029 [US1] Extend `DoctorIdsHandler` with `--fix` path — order duplicate occurrences by Git history (`git log --diff-filter=A --reverse -- <path>`) with mtime tiebreaker per research.md Decision 9; renumber via `PersistentTestIdAllocator`; update `depends_on` arrays workspace-wide; best-effort literal-string update of `[TestCase("TC-NNN")]` patterns in `automated_by` files; populate `renumbered[]` and `unfixable_references[]`. (Depends on T028.)
- [X] T030 [US1] Register `DoctorCommand` in `src/Spectra.CLI/Program.cs::CreateRootCommand()` (alongside existing `ValidateCommand`, `DashboardCommand`, etc.)
- [X] T031 [US1] Extend `src/Spectra.CLI/Commands/Init/InitHandler.cs::UpdateGitIgnoreAsync()` (line ~715) to add `.spectra/.pid`, `.spectra/.cancel`, `.spectra/id-allocator.lock`, `.spectra/id-allocator.json` to the existing `# SPECTRA` block (idempotent — checks for each entry before appending)

**Checkpoint**: User Story 1 fully functional — concurrent generation produces no duplicate IDs; `spectra doctor ids` audits and repairs. MVP is shippable here.

---

## Phase 4: User Story 2 - Stop a runaway operation cleanly (Priority: P2)

**Goal**: `spectra cancel` halts in-progress operations cooperatively within 5 s, force-kills as fallback, preserves partial work, surfaces survivors via cancelled-status result artifact.

**Independent Test**: Start `ai generate --count 15` in process A; in process B run `spectra cancel` after 7 tests are written. Assert: process A exits within ≤ 7 s with exit 130; `.spectra-result.json` reports `status: cancelled` with `tests_written: 7`; the 7 files exist; `.spectra-progress.html` shows terminal `Cancelled` phase.

### Tests for User Story 2 ⚠️

- [X] T032 [P] [US2] `PidFileManagerTests.cs` in `tests/Spectra.CLI.Tests/Cancellation/` — write-roundtrip, stale (no such PID), stale (wrong process name allow-list `spectra`/`dotnet`/`Spectra.CLI`), live + valid
- [X] T033 [P] [US2] `SentinelWatcherTests.cs` in `tests/Spectra.CLI.Tests/Cancellation/` — sentinel appears within poll window triggers callback, removed mid-watch is observed, idempotent dispose
- [X] T034 [P] [US2] `CancellationManagerTests.cs` in `tests/Spectra.CLI.Tests/Cancellation/` — `RegisterCommandAsync` writes `.spectra/.pid` and clears stale `.spectra/.cancel`, sentinel triggers token, external Ctrl+C linked-token triggers `Token`, double-register throws, `UnregisterCommandAsync` removes both files
- [X] T035 [P] [US2] `CancelHandlerTests.cs` in `tests/Spectra.CLI.Tests/Commands/Cancel/` — `no_active_run` (absent PID file, stale PID), cooperative success (mock target process exits within 5 s), force path (immediate kill), hard-kill path (target ignores sentinel for 5 s), Windows process-tree-kill, stale PID auto-cleanup
- [X] T036 [P] [US2] `ProgressPageWriterCancelledTests.cs` in `tests/Spectra.CLI.Tests/Cancellation/` — `phase: "Cancelled"` is treated as terminal (alongside `Completed`); embedded JS auto-refresh disabled; phase-strip styling applied
- [X] T037 [P] [US2] Integration test `CancellationIntegrationTests.cs` in `tests/Spectra.CLI.Tests/Integration/Cancellation/` — one test per long-running command (`ai generate`, `ai update`, `ai analyze`, `ai analyze --extract-criteria`, `ai analyze --coverage`, `dashboard`, `docs index`). Each: start handler in-process with mocked Copilot SDK that emits N artifacts then awaits, trigger cancellation via `CancellationManager.Instance`, assert result artifact has `status: "cancelled"` and survivor count matches artifacts on disk.

### Implementation for User Story 2

- [X] T038 [P] [US2] Implement `PidFileManager` in `src/Spectra.CLI/Cancellation/PidFileManager.cs` — `WriteAsync(int pid, string command, string processName)`, `ReadAsync()`, `IsStale()` (per research.md Decision 11: validate `Process.GetProcessById` + `ProcessName` allow-list), `DeleteAsync()`
- [X] T039 [P] [US2] Implement `SentinelWatcher` in `src/Spectra.CLI/Cancellation/SentinelWatcher.cs` — start a 200 ms polling task on a `CancellationToken`-managed loop that triggers a callback when `.spectra/.cancel` appears (per research.md Decision 4)
- [X] T040 [US2] Implement `CancellationManager` singleton in `src/Spectra.CLI/Cancellation/CancellationManager.cs` — `Instance`, `RegisterCommandAsync(string commandName, CancellationToken externalToken)` returns disposable registration that links external token + sentinel → internal `CancellationTokenSource`; `Token` property; `ThrowIfCancellationRequested()`; `UnregisterCommandAsync()`. Idempotent error if already registered. (Depends on T038, T039.)
- [X] T041 [P] [US2] Create `CancelCommand` in `src/Spectra.CLI/Commands/Cancel/CancelCommand.cs` with `--force` option
- [X] T042 [US2] Implement `CancelHandler` in `src/Spectra.CLI/Commands/Cancel/CancelHandler.cs` per state machine in `data-model.md` §9b — read PID, validate liveness, write sentinel, poll 5 s (skip on `--force`), force kill via `Process.Kill(entireProcessTree: true)`, wait 2 s, clean up, emit `CancelResult`. (Depends on T009, T038, T040.)
- [X] T043 [US2] Register `CancelCommand` in `src/Spectra.CLI/Program.cs::CreateRootCommand()`
- [X] T044 [US2] Extend `src/Spectra.CLI/Progress/ProgressPageWriter.cs` — add `Cancelled` to the terminal-phase set used by the embedded JS; phase-strip styling for the cancelled phase (yellow/striped); preserve existing `Completed` behavior
- [X] T045 [US2] Wire `CancellationManager` into `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` — `using var registration = await CancellationManager.Instance.RegisterCommandAsync("ai generate", ct);` at start; `CancellationManager.Instance.ThrowIfCancellationRequested()` at the per-test batch boundary inside the generation loop; `catch (OperationCanceledException)` writes cancelled `GenerateResult` (with `cancelled_at`, `phase`, `phase_progress`, `tests_written`, `files`, `message`) and cancelled progress page; return exit code 130; `finally` unregisters
- [X] T046 [US2] Wire `CancellationManager` into `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` (per-doc batch boundaries for behavior analysis; per-suite for coverage; per-doc for criteria extraction). Extend `AnalyzeResult`, `AnalyzeCriteriaResult`, `AnalyzeCoverageResult` with the cancelled-extension fields.
- [X] T047 [P] [US2] Wire `CancellationManager` into `src/Spectra.CLI/Commands/Dashboard/DashboardHandler.cs` (per-step boundary). Extend `DashboardResult` with cancelled-extension fields.
- [X] T048 [P] [US2] Wire `CancellationManager` into `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs` (per-doc boundary). Extend `DocsIndexResult` with cancelled-extension fields.
- [X] T049 [P] [US2] Wire `CancellationManager` into the `ai update` handler (locate via `src/Spectra.CLI/Commands/Update/`). Extend its result class with cancelled-extension fields.

**Checkpoint**: User Story 2 fully functional — `spectra cancel` works for all six long-running commands; partial results are preserved; progress page shows cancelled state.

---

## Phase 5: User Story 3 - Delete tests safely (Priority: P3)

**Goal**: `spectra delete <ids…>` removes test cases atomically with automation and dependency safety, preview support, and the `spectra-delete` SKILL.

**Independent Test**: Create three test cases TC-100, TC-101, TC-102 where TC-101 has `automated_by: tests/foo.cs`, TC-102 has `depends_on: [TC-100]`. Run `spectra delete TC-100 TC-101 TC-102 --dry-run` → preview lists everything, exit 5 (TC-101 has automation). Add `--force`. Verify all three files gone, `_index.json` updated, no orphan references, exit 0. `git status` shows clean diff.

### Tests for User Story 3 ⚠️

- [X] T050 [P] [US3] `DeleteHandlerTests.cs` in `tests/Spectra.CLI.Tests/Commands/Delete/` — covers: single-test happy path, bulk-delete, missing ID (exit 4 + `skipped[]`), automation-linked refusal (exit 5, no filesystem changes), automation-linked + `--force` (deletes, reports stranded), `depends_on` cascade cleanup atomically across all suites, `--dry-run` produces zero filesystem changes (verified via `git status` clean assertion), mixed valid+invalid IDs (partial completion + clear errors)

### Implementation for User Story 3

- [X] T051 [P] [US3] Create `DeleteCommand` in `src/Spectra.CLI/Commands/Delete/DeleteCommand.cs` with options `--suite`, `--dry-run`, `--force`, `--no-automation-check`; positional `<test-id>` arity 0+
- [X] T052 [US3] Implement `DeleteHandler` in `src/Spectra.CLI/Commands/Delete/DeleteHandler.cs` per research.md Decision 7: (a) resolve phase via existing index reader + filesystem fallback; (b) pre-flight checks (TEST_NOT_FOUND → exit 4, AUTOMATION_LINKED w/o override → exit 5, gather dependents); (c) interactive confirmation (skip on `--force`/`--no-interaction`); (d) write phase: rewrite affected `_index.json` (atomic temp+rename), rewrite each dependent's `depends_on`, then delete test files; (e) emit `DeleteResult`. (Depends on T007.)
- [X] T053 [US3] When `--suite <name>` is supplied, `DeleteHandler` delegates to `SuiteDeleteHandler` (forward-reference: this task is touched again in Phase 6 once `SuiteDeleteHandler` exists; the alias is wired here as a thin pass-through)
- [X] T054 [US3] Register `DeleteCommand` in `src/Spectra.CLI/Program.cs::CreateRootCommand()`
- [X] T055 [P] [US3] Create new SKILL `src/Spectra.CLI/Skills/Content/Skills/spectra-delete.md` — content per spec.md §"New SKILL: spectra-delete" (preview-then-confirm flow, trigger phrases, refuse-to-do list)
- [X] T056 [US3] Add `spectra-delete` to `SkillContent.All` in `src/Spectra.CLI/Skills/SkillContent.cs` (or wherever the bundled-skills registry is) so `InitHandler.CreateBundledSkillFilesAsync()` and `update-skills` pick it up

**Checkpoint**: User Story 3 fully functional — single and bulk test deletion with all guards; SKILL surface working in Copilot Chat.

---

## Phase 6: User Story 4 - Suite rename / delete (Priority: P3)

**Goal**: `spectra suite list|rename|delete` with atomic config and selection updates, naming-rule validation, rollback on partial failure, and the `spectra-suite` SKILL.

**Independent Test**: Set up suite `checkout` with 2 tests, a saved selection `smoke` referencing it, and a `suites.checkout` config block. Run `spectra suite rename checkout payments` → exit 0; `test-cases/payments/` exists with both tests intact, IDs unchanged; `selections.smoke.suites = ["payments"]`; `suites.payments` config block exists. Then `spectra suite delete payments --force` → exit 0; directory gone, references cleaned.

### Tests for User Story 4 ⚠️

- [X] T057 [P] [US4] `SuiteListHandlerTests.cs` in `tests/Spectra.CLI.Tests/Commands/Suite/` — empty workspace, multiple suites, automation count correct
- [X] T058 [P] [US4] `SuiteRenameHandlerTests.cs` in `tests/Spectra.CLI.Tests/Commands/Suite/` — happy path, `SUITE_NOT_FOUND` (exit 4), `SUITE_ALREADY_EXISTS` (exit 6), `INVALID_SUITE_NAME` (exit 7), selections updated, suites config block re-keyed, test IDs unchanged, rollback on partial failure (simulate config-write failure → directory + index reverted)
- [X] T059 [P] [US4] `SuiteDeleteHandlerTests.cs` in `tests/Spectra.CLI.Tests/Commands/Suite/` — happy path, `AUTOMATION_LINKED` guard (exit 5), `EXTERNAL_DEPENDENCIES` guard (exit 8), `--force` proceeds and cascades cross-suite `depends_on` cleanup, `--dry-run` produces zero filesystem changes, selections + config block updated

### Implementation for User Story 4

- [X] T060 [P] [US4] Create `SuiteCommand` parent in `src/Spectra.CLI/Commands/Suite/SuiteCommand.cs` with `list`, `rename`, `delete` subcommands
- [X] T061 [P] [US4] Implement `SuiteListHandler` in `src/Spectra.CLI/Commands/Suite/SuiteListHandler.cs` — enumerate `test-cases/*/`, count `*.md` files (excluding `_index.json`), count automated entries, emit `SuiteListResult`. (Depends on T008.)
- [X] T062 [US4] Implement `SuiteRenameHandler` in `src/Spectra.CLI/Commands/Suite/SuiteRenameHandler.cs` per research.md Decision 8: validate name regex `^[a-z0-9][a-z0-9_-]*$`, snapshot config in memory, atomic `Directory.Move`, atomic `_index.json` rewrite (`suite` field), atomic `spectra.config.json` rewrite (selections + suite block), best-effort rollback on any step failure, emit `SuiteRenameResult`. (Depends on T008.)
- [X] T063 [US4] Implement `SuiteDeleteHandler` in `src/Spectra.CLI/Commands/Suite/SuiteDeleteHandler.cs` — pre-flight (existence, automation count, external `depends_on` count); interactive confirmation; cascade cross-suite `depends_on` cleanup (reuse logic from `DeleteHandler`); recursive `Directory.Delete`; rewrite `spectra.config.json` (remove from selections, drop config block); emit `SuiteDeleteResult`. (Depends on T008, T052 for cascade helper extraction.)
- [X] T064 [US4] Register `SuiteCommand` in `src/Spectra.CLI/Program.cs::CreateRootCommand()`
- [X] T065 [US4] Update T053's `--suite` alias in `DeleteHandler` to forward to the now-existing `SuiteDeleteHandler` (replace the placeholder pass-through with the real wiring)
- [X] T066 [P] [US4] Create new SKILL `src/Spectra.CLI/Skills/Content/Skills/spectra-suite.md` — content per spec.md §"New SKILL: spectra-suite" (rename and delete recipes with preview-first, trigger phrases)
- [X] T067 [US4] Add `spectra-suite` to `SkillContent.All` in `src/Spectra.CLI/Skills/SkillContent.cs`

**Checkpoint**: All four user stories independently functional. Feature complete pending polish.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: SKILL recipes for cancellation across the six long-running SKILLs, doc updates, and final validation.

- [X] T068 [P] Add "Cancel the current run" recipe section to `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` (template per spec.md §"Updates to existing SKILLs")
- [X] T069 [P] Add the same "Cancel the current run" recipe to `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md`
- [X] T070 [P] Add the same recipe to `src/Spectra.CLI/Skills/Content/Skills/spectra-coverage.md`
- [X] T071 [P] Add the same recipe to `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md`
- [X] T072 [P] Add the same recipe to `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md`
- [X] T073 [P] Add the same recipe to `src/Spectra.CLI/Skills/Content/Skills/spectra-dashboard.md`
- [X] T074 [P] Add "Workflow N: Stop a running operation" section to `src/Spectra.CLI/Skills/Content/Skills/spectra-quickstart.md`
- [X] T075 [P] Add "Diagnose test ID issues" recipe to `src/Spectra.CLI/Skills/Content/Skills/spectra-help.md`
- [X] T076 [P] Update agent delegation table in `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md` — route trigger phrases for delete / suite / cancel / doctor to the new SKILLs
- [X] T077 [P] Update `docs/cli-reference.md` — document `spectra delete`, `spectra suite list|rename|delete`, `spectra cancel`, `spectra doctor ids` per `contracts/cli-commands.md` (flags, exit codes, examples)
- [X] T078 [P] Update `docs/test-format.md` — add a section on the high-water-mark mechanism and the `.spectra/id-allocator.json` file (per FR-009)
- [X] T079 [P] Add Spec 040 migration notes to `CHANGELOG.md` — version `1.52.0` entry: new commands, ID-allocator behavior change, `spectra doctor ids --fix` workflow for existing duplicates, `.gitignore` additions, status-string additions (`cancelled`, `no_active_run`)
- [X] T080 [P] Update `PROJECT-KNOWLEDGE.md` (if present) — same release-notes summary as the CHANGELOG entry
- [X] T081 Run full local test suite: `dotnet build && dotnet test` from `C:/SourceCode/Spectra` — all ~74 new tests + existing ~1279 tests pass
- [X] T082 Run quickstart manual verification per `quickstart.md` §B.5 — fresh workspace + the full `init → doctor → generate → delete → rename → cancel → suite delete → doctor` sequence; inspect each `.spectra-result.json`; assert `git status` is clean between commands

**Checkpoint**: Feature complete, tested, documented, ready for `/ultrareview` or PR.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1. **Blocks all user-story phases.**
- **Phase 3 (US1 — ID allocator)**: Depends on Phase 2. Independent of US2/US3/US4.
- **Phase 4 (US2 — Cancellation)**: Depends on Phase 2. Independent of US1/US3/US4.
- **Phase 5 (US3 — Delete)**: Depends on Phase 2. Lightly couples to US4 (T053 placeholder, T065 wiring); otherwise independent.
- **Phase 6 (US4 — Suite ops)**: Depends on Phase 2; T063 reuses cascade helper from T052 in Phase 5.
- **Phase 7 (Polish)**: Depends on all four user-story phases.

### Within-story Critical Paths

**US1**: T020 + T021 + T022 → T023 → T024 → T025 → T026; T027 → T028 → T029. Tests T014–T019 written before respective implementations.

**US2**: T038 + T039 → T040 → T042; T044 (independent); T045–T049 wiring (T046 has internal sequence: behavior → criteria → coverage). Tests T032–T037 first.

**US3**: T051 → T052 → T053 → T054; T055 → T056. Test T050 first.

**US4**: T060 → T061/T062/T063 → T064 → T065; T066 → T067. Tests T057–T059 first.

### Parallel Opportunities

- **Phase 1**: T002–T006 in parallel after T001.
- **Phase 2**: T007, T008, T009, T010, T012, T013 all in parallel.
- **Phase 3 (US1) tests**: T014–T019 in parallel.
- **Phase 3 (US1) impl**: T020, T021, T022, T027, T031 in parallel; T023 then T024, T025; T028 then T029.
- **Phase 4 (US2) tests**: T032–T037 in parallel.
- **Phase 4 (US2) impl**: T038, T039, T041, T044 in parallel; then T040; then T042/T043; T045–T049 in parallel after T040.
- **Phase 5 (US3) impl**: T051, T055 in parallel; T052 then T053, T054; T056 in parallel.
- **Phase 6 (US4) tests**: T057–T059 in parallel.
- **Phase 6 (US4) impl**: T060, T066 in parallel; T061, T062 in parallel after T060; T063 after T062 (config rewrite helper) and T052 (cascade helper); T064 then T065; T067 in parallel.
- **Phase 7**: T068–T080 all in parallel; T081 sequential; T082 last.

---

## Parallel Example: User Story 1 (P1 — MVP)

```bash
# Run all US1 unit tests in parallel (different test files, no dependencies):
Task: "HighWaterMarkStoreTests in tests/Spectra.Core.Tests/IdAllocation/HighWaterMarkStoreTests.cs"
Task: "FileLockHandleTests in tests/Spectra.Core.Tests/IdAllocation/FileLockHandleTests.cs"
Task: "TestCaseFrontmatterScannerTests in tests/Spectra.Core.Tests/IdAllocation/TestCaseFrontmatterScannerTests.cs"

# After tests fail, build the three independent services in parallel:
Task: "Implement HighWaterMarkStore in src/Spectra.Core/IdAllocation/HighWaterMarkStore.cs"
Task: "Implement FileLockHandle in src/Spectra.Core/IdAllocation/FileLockHandle.cs"
Task: "Implement TestCaseFrontmatterScanner in src/Spectra.Core/IdAllocation/TestCaseFrontmatterScanner.cs"

# Then sequentially:
Task: "Implement PersistentTestIdAllocator (depends on all three above)"
Task: "Wire into GetNextTestIdsTool"
Task: "Wire into GenerateHandler"
```

---

## Implementation Strategy

### MVP First (US1 Only — ID Allocator Fix)

1. Phase 1 (Setup) — version bump, scaffolding.
2. Phase 2 (Foundational) — result models + test fixtures.
3. Phase 3 (US1) — persistent allocator + `doctor ids`.
4. **STOP and VALIDATE**: Run two parallel `ai generate` operations; confirm zero duplicate IDs. Run `spectra doctor ids` against an existing duplicate-prone workspace; confirm clean report.
5. Ship 1.52.0-rc1 if needed — this fixes the silent correctness bug ahead of the rest.

### Incremental Delivery

- **Increment 1 (MVP)**: Phases 1+2+3 → 1.52.0 ID allocator fix.
- **Increment 2**: Add Phase 4 → cancellation across all six long-running commands.
- **Increment 3**: Add Phase 5 → `spectra delete`.
- **Increment 4**: Add Phase 6 → suite operations.
- **Increment 5**: Phase 7 polish + cut 1.52.0 final.

Each increment passes its independent test, leaves the workspace in a Git-clean state, and breaks no existing functionality.

### Parallel Team Strategy

With three developers post-Foundational:

- Dev A: US1 (P1, blocks no one else but is the highest correctness priority).
- Dev B: US2 (cancellation infrastructure, touches six handlers).
- Dev C: US3 + US4 (lifecycle commands; share atomic-rewrite helpers between delete and suite delete).

Polish phase (Phase 7) is parallelizable across all three.

---

## Notes

- [P] tasks touch different files with no incomplete dependencies.
- [US1]–[US4] labels map to the four user stories in `spec.md`.
- Tests are interleaved per user story (xUnit conventions; structured-result assertions, never throw on validation errors per CLAUDE.md).
- Commit after each task or logical group; do not include `Co-Authored-By` lines per user preference.
- Per the user feedback memory: bump `Directory.Build.props` Version *before* packing the NuGet release; sync demo projects after install.
- Stop at any checkpoint to validate the story independently against the spec's acceptance scenarios.
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence (the only acknowledged cross-story coupling is T053/T065 and T063 reusing the cascade helper from T052 — this is documented in Dependencies above).
