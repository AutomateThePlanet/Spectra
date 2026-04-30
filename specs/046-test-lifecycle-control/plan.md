# Implementation Plan: Test Lifecycle & Process Control

**Branch**: `046-test-lifecycle-control` | **Date**: 2026-04-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/046-test-lifecycle-control/spec.md`

## Summary

Ship four lifecycle gaps in one branch:

1. **ID-allocation correctness** — wrap the existing in-memory `Spectra.Core.Index.TestIdAllocator` with a new persistent, cross-process-safe layer that owns a high-water-mark file (`.spectra/id-allocator.json`) and a file lock (`.spectra/id-allocator.lock`). Add `spectra doctor ids [--fix]` for diagnosis and repair.
2. **Cooperative cancellation** — introduce `CancellationManager` in `Spectra.CLI/Cancellation/` that owns the process `CancellationTokenSource`, watches a `.spectra/.cancel` sentinel file, and writes `.spectra/.pid`. Add `spectra cancel`. Wire token+sentinel checks into the six long-running command handlers at their batch boundaries. Extend `ProgressPageWriter` with a terminal `Cancelled` phase. Add `cancelled` to the `CommandResult.Status` enum convention.
3. **Test deletion** — add `spectra delete <ids…>` with preview/force/automation-guard semantics. Cascade `depends_on` cleanup atomically across the workspace.
4. **Suite ops** — add `spectra suite list|rename|delete`. Rename updates the suite directory, suite-index `suite` field, saved selections in `spectra.config.json`, and any per-suite config block. Delete cascades cross-suite `depends_on` cleanup before recursive directory removal.

Two new SKILLs (`spectra-delete`, `spectra-suite`) bundled under `src/Spectra.CLI/Skills/Content/Skills/` (15 → 17 skill files). Six existing long-running SKILLs gain a "Cancel the current run" recipe. `spectra-quickstart` gets a "Stop a running operation" workflow. `spectra-help` gets a "Diagnose test ID issues" recipe.

`Directory.Build.props` version bumps `1.51.4` → `1.52.0`.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI), Spectre.Console (interactive prompts), System.Text.Json, YamlDotNet (frontmatter), Microsoft.Extensions.FileSystemGlobbing (test-case file walks)
**Storage**: File-based — Markdown + YAML frontmatter under `test-cases/<suite>/`, per-suite `_index.json`, workspace config `spectra.config.json`, doc-index v2 (Spec 040 layout) under `docs/_index/`. Lifecycle additions: `.spectra/id-allocator.json` (high-water-mark), `.spectra/id-allocator.lock` (cross-process mutex), `.spectra/.pid`, `.spectra/.cancel`
**Testing**: xUnit (`tests/Spectra.Core.Tests`, `tests/Spectra.CLI.Tests`, `tests/Spectra.MCP.Tests`); structured-result assertions (no exceptions on validation errors)
**Target Platform**: Cross-platform CLI (Windows / macOS / Linux). Cancellation must use `Process.Kill(entireProcessTree: true)` on Windows; on Unix, `Process.Kill(Signal.SIGTERM)` then `SIGKILL`. .NET 8 `Process.Kill` already wraps both; extra signal handling not needed.
**Project Type**: Multi-project CLI tool (single-repo) — `Spectra.Core` (library), `Spectra.CLI` (CLI app), `Spectra.MCP` (MCP server), `Spectra.GitHub` (future)
**Performance Goals**:
- Allocator filesystem walk on a 10K-test workspace: < 1 s (cached per process)
- File-lock acquisition timeout: 10 s with retry-with-backoff
- Cooperative cancellation grace window: ≤ 5 s; total cancel latency: ≤ 7 s end-to-end
- Diagnostic (`doctor ids`) on 10K tests: < 5 s (per spec SC-006)
**Constraints**:
- No background threads in CLI handlers — all work on the request path
- Result JSON files must be atomically replaced (write-temp + rename), never partial
- Allocator lock must release on process crash (use `FileShare.None` + holding handle, OS releases on exit)
- PID file must be self-validating (verify the recorded PID matches a live `spectra` process before any kill action)
**Scale/Scope**: ~74 new tests, 4 new top-level commands (`delete`, `suite`, `cancel`, `doctor`), 2 new SKILLs, 6 SKILL recipe additions, 1 quickstart workflow, ~15 new C# files

## Constitution Check

*GATE: Pre-Phase-0 verification of the SPECTRA Constitution v1.1.0.*

| Principle | Compliance | Notes |
|-----------|-----------|-------|
| **I. GitHub as Source of Truth** | ✅ Pass | All test/suite state remains in Git. New `.spectra/*` files are workspace-local and gitignored (high-water-mark, lock, PID, sentinel) — they are derived state, not source. |
| **II. Deterministic Execution** | ✅ Pass | Allocator is deterministic given the same persisted high-water-mark and filesystem state. Cancellation is explicit (sentinel file + token) — no implicit timeouts or background actors. Result artifacts use stable status strings. |
| **III. Orchestrator-Agnostic Design** | ✅ Pass | All new commands emit machine-readable JSON via the existing `CommandResult` contract. New SKILLs follow the standard `--no-interaction --output-format json --verbosity quiet` pattern. No LLM-vendor-specific code. |
| **IV. CLI-First Interface** | ✅ Pass | Every feature lands as a CLI command first (`spectra delete`, `spectra suite …`, `spectra cancel`, `spectra doctor ids`). SKILLs only wrap and confirm; they never write files directly. All commands support `--dry-run` (preview), `--no-interaction`, deterministic exit codes. |
| **V. Simplicity (YAGNI)** | ✅ Pass with one justified addition | New abstractions: `PersistentTestIdAllocator` (wrapper around the existing in-memory `TestIdAllocator`), `CancellationManager` (singleton). Both pass the YAGNI test — see Complexity Tracking. |

### Quality Gates Impact

The new `delete`, `suite delete`, `suite rename`, and `doctor ids --fix` operations all rewrite `_index.json` and may rewrite `depends_on` arrays. After every such operation:

- Schema Validation must continue to pass (frontmatter remains valid).
- ID Uniqueness must hold (post-delete: no orphan IDs in `depends_on`; post-rename: IDs unchanged; post-doctor-fix: zero duplicates).
- Index Currency must hold (the operation updates the affected `_index.json` itself; users do not need to follow up with `rebuild_indexes`).
- Dependency Resolution must hold (`depends_on` cascade cleanup in delete and suite-delete is explicitly part of the operation).

## Project Structure

### Documentation (this feature)

```text
specs/046-test-lifecycle-control/
├── plan.md              # this file
├── research.md          # Phase 0: design decisions and alternatives
├── data-model.md        # Phase 1: entities, state, file formats
├── quickstart.md        # Phase 1: developer + user walkthroughs
├── contracts/
│   ├── cli-commands.md  # Phase 1: CLI surface (flags, exit codes, examples)
│   └── result-json.md   # Phase 1: result-artifact schemas (delete, cancel, doctor, suite)
├── checklists/
│   └── requirements.md  # /speckit.specify quality checklist (✅ all pass)
└── tasks.md             # Phase 2: /speckit.tasks output (NOT created here)
```

### Source Code (repository root)

Existing layout extended in place — no new top-level project, no new external dependencies.

```text
src/
├── Spectra.Core/
│   ├── IdAllocation/                          # NEW — persistence + lock layer
│   │   ├── PersistentTestIdAllocator.cs       # NEW — async wrapper, file lock, HWM
│   │   ├── HighWaterMarkStore.cs              # NEW — read/write .spectra/id-allocator.json
│   │   ├── TestCaseFrontmatterScanner.cs      # NEW — filesystem walk → max ID
│   │   └── FileLockHandle.cs                  # NEW — IDisposable wrapper around FileStream
│   ├── Index/
│   │   └── TestIdAllocator.cs                 # KEEP AS-IS — in-memory pure piece
│   └── Models/
│       └── Lifecycle/                         # NEW — result models
│           ├── DeleteResult.cs                # NEW
│           ├── SuiteListResult.cs             # NEW
│           ├── SuiteRenameResult.cs           # NEW
│           ├── SuiteDeleteResult.cs           # NEW
│           ├── CancelResult.cs                # NEW
│           └── DoctorIdsResult.cs             # NEW
│
├── Spectra.CLI/
│   ├── Cancellation/                          # NEW
│   │   ├── CancellationManager.cs             # NEW — singleton, owns CTS + sentinel watcher
│   │   ├── SentinelWatcher.cs                 # NEW — polls .spectra/.cancel
│   │   └── PidFileManager.cs                  # NEW — writes/validates/cleans .spectra/.pid
│   ├── Commands/
│   │   ├── Delete/
│   │   │   ├── DeleteCommand.cs               # NEW
│   │   │   └── DeleteHandler.cs               # NEW
│   │   ├── Suite/
│   │   │   ├── SuiteCommand.cs                # NEW (parent)
│   │   │   ├── SuiteListHandler.cs            # NEW
│   │   │   ├── SuiteRenameHandler.cs          # NEW
│   │   │   └── SuiteDeleteHandler.cs          # NEW
│   │   ├── Cancel/
│   │   │   ├── CancelCommand.cs               # NEW
│   │   │   └── CancelHandler.cs               # NEW
│   │   ├── Doctor/
│   │   │   ├── DoctorCommand.cs               # NEW (parent — `doctor ids` subcommand)
│   │   │   └── DoctorIdsHandler.cs            # NEW
│   │   ├── Generate/GenerateHandler.cs        # MODIFIED — register with CancellationManager
│   │   ├── Analyze/AnalyzeHandler.cs          # MODIFIED — register, batch-boundary checks
│   │   ├── Dashboard/DashboardHandler.cs      # MODIFIED — register, per-step checks
│   │   ├── Docs/DocsIndexHandler.cs           # MODIFIED — register, per-doc checks
│   │   └── Init/InitHandler.cs                # MODIFIED — gitignore additions, new SKILLs
│   ├── Progress/ProgressPageWriter.cs         # MODIFIED — terminal "Cancelled" phase
│   ├── Skills/
│   │   ├── Content/Skills/
│   │   │   ├── spectra-delete.md              # NEW (16th SKILL)
│   │   │   ├── spectra-suite.md               # NEW (17th SKILL)
│   │   │   ├── spectra-generate.md            # MODIFIED — "Cancel the current run" recipe
│   │   │   ├── spectra-update.md              # MODIFIED — same
│   │   │   ├── spectra-coverage.md            # MODIFIED — same
│   │   │   ├── spectra-criteria.md            # MODIFIED — same
│   │   │   ├── spectra-docs.md                # MODIFIED — same
│   │   │   ├── spectra-dashboard.md           # MODIFIED — same
│   │   │   ├── spectra-quickstart.md          # MODIFIED — "Stop a running operation" workflow
│   │   │   └── spectra-help.md                # MODIFIED — "Diagnose test ID issues" recipe
│   │   └── Content/Agents/
│   │       └── spectra-generation.agent.md    # MODIFIED — delegation table for delete/suite/cancel/doctor
│   └── Program.cs                             # MODIFIED — register Delete, Suite, Cancel, Doctor commands
│
└── Spectra.Core.Tests, Spectra.CLI.Tests      # NEW test files mirror command/service tree

tests/
├── Spectra.Core.Tests/
│   └── IdAllocation/
│       ├── PersistentTestIdAllocatorTests.cs
│       ├── HighWaterMarkStoreTests.cs
│       └── TestCaseFrontmatterScannerTests.cs
└── Spectra.CLI.Tests/
    ├── Cancellation/
    │   ├── CancellationManagerTests.cs
    │   ├── PidFileManagerTests.cs
    │   └── SentinelWatcherTests.cs
    └── Commands/
        ├── Delete/DeleteHandlerTests.cs
        ├── Suite/SuiteListHandlerTests.cs
        ├── Suite/SuiteRenameHandlerTests.cs
        ├── Suite/SuiteDeleteHandlerTests.cs
        ├── Cancel/CancelHandlerTests.cs
        ├── Doctor/DoctorIdsHandlerTests.cs
        └── Integration/CancellationIntegrationTests.cs   # one test per long-running command
```

**Structure Decision**: Single-repo, multi-project layout (already in place). New code lands under existing project boundaries — no new csproj. Lifecycle commands belong in `Spectra.CLI/Commands/`; the persistent ID allocator and lifecycle result models belong in `Spectra.Core` (so `Spectra.MCP` can also adopt the persistent allocator if/when it gains test creation).

## Complexity Tracking

| Addition | Why Needed | Simpler Alternative Rejected Because |
|----------|------------|-------------------------------------|
| `PersistentTestIdAllocator` (separate from existing `TestIdAllocator`) | Existing allocator is a pure in-memory, sync class with no I/O. Cross-process locking and async file I/O can't be retrofitted without changing every test that constructs it directly. | Modifying `TestIdAllocator` in place would force ~30 sync test-allocation sites to become async or own a lock they don't need. Wrapping cleanly separates the pure allocation algorithm (kept testable in isolation) from the persistence/locking concerns. |
| `CancellationManager` singleton | Six handlers need to share one process-level token, register with one PID file, and observe one sentinel file. Existing pattern passes `CancellationToken` per call, which can't be reached from another process. | Per-handler tokens cannot be triggered from a second `spectra cancel` process. A singleton manager is the minimum surface that gives us cross-process cancellation without scattering PID/sentinel logic across six handlers. |
| Two new SKILL files (`spectra-delete`, `spectra-suite`) instead of folding into existing skills | Per Open Question 2 in the spec: suite ops will grow (rename, delete, eventually create, merge) and don't share semantics with single-test deletion. | Folding suite ops into `spectra-delete` couples two semantically distinct surfaces. Folding everything into `spectra-quickstart` produces a giant, unfocused SKILL. Two small, focused SKILLs match the existing project pattern (12 today, 14 after this feature). |

No constitution violations. The two new abstractions both pass YAGNI (each is the minimum required for cross-process correctness, not speculative future-proofing).
