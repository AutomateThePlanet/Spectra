# Implementation Plan: From-Description Write & Index Parity

**Branch**: `049-from-description-index-parity` | **Date**: 2026-06-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/049-from-description-index-parity/spec.md`

## Summary

Two generation flows currently write test files: the batch path (`ExecuteDirectModeAsync`) and the from-description path (`ExecuteFromDescriptionAsync`). The batch path writes the file *and* regenerates the suite's `_index.json` from the full set of existing+new tests; the from-description path writes the file and stops. Because every MCP discovery and execution tool reads only from `_index.json`, from-description tests are silently invisible to `find_test_cases`, `start_execution_run`, `list_available_suites`, and all saved-selection counts. The high-priority-filter symptom is the same problem viewed from a filter call site: the filter is correct, but its data source is incomplete.

The fix has three pieces:

1. **Centralize** the "write file + regenerate suite index" sequence into a new `TestPersistenceService` (CLI layer, depends on the existing `TestFileWriter`, `IndexGenerator`, `IndexWriter`). This becomes the single public write entry point for generation flows.
2. **Re-route** both `ExecuteDirectModeAsync` (batch) and `ExecuteFromDescriptionAsync` (from-description) through `TestPersistenceService.PersistAsync(...)`. The batch refactor is a behavior-preserving extraction; the from-description rewire is the actual bug fix.
3. **Backfill via `spectra index --rebuild`**, which already parses `.md` files of record and regenerates indexes — verify the path is correct for unindexed files and add a regression test. No structural change expected; the existing `IndexHandler` flow already does the right thing.

Scope is explicitly confined to the CLI write/orchestration layer. The filter logic, the index data model, and the MCP tools that consume the index are not modified.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine (CLI), Spectre.Console (progress UI), System.Text.Json (index serialization), YamlDotNet (frontmatter). No new dependencies introduced.
**Storage**: File-based. `test-cases/{suite}/{id}.md` (test files of record), `test-cases/{suite}/_index.json` (per-suite metadata index consumed by MCP discovery/execution tools).
**Testing**: xUnit. Unit tests in `tests/Spectra.Core.Tests/`; CLI integration tests in `tests/Spectra.CLI.Tests/`; MCP server tests in `tests/Spectra.MCP.Tests/`. Structured-result style: tests assert on returned `CommandResult` types, not exceptions, except where exceptions are the contract.
**Target Platform**: Cross-platform .NET 8 (Windows-first development host; CI runs Linux). File paths via `Path.Combine`/`Path.GetRelativePath`.
**Project Type**: Multi-project .NET solution. Changes land in `Spectra.CLI` (orchestration + new service) and `tests/Spectra.CLI.Tests` + `tests/Spectra.MCP.Tests` (regression + integration). `Spectra.Core` is unchanged (existing `IndexGenerator`/`IndexWriter`/`TestCase` consumed as-is).
**Performance Goals**: Match the existing batch path. From-description is a single-test write; index regeneration over the full suite is O(N) in suite size with N typically ≤ a few hundred — well below any user-perceptible threshold.
**Constraints**: Index write must reflect the full suite (existing+new), not partial — this is how the batch path already behaves and how `IndexHandler` already rebuilds. Index entries must be byte-for-byte equivalent to today's batch output for the same inputs (regression-guarded). No changes to `TestFileWriter.FormatTestCase`, `IndexGenerator.CreateEntry`, or the `MetadataIndex` JSON shape.
**Scale/Scope**: Single suite, single test added per from-description invocation; suite cardinality observed in this repo is in the tens-to-low-hundreds per suite. No throughput concern.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ Pass | All artifacts remain in Git: test files in `test-cases/{suite}/`, indexes in `_index.json`. The fix *strengthens* this principle by ensuring every generated test is registered in the committed index. |
| II. Deterministic Execution | ✅ Pass | The MCP execution engine state machine is not touched. The index becomes more reliable (more tests visible to it), which improves determinism — same generation inputs now produce the same discoverable set across orchestrators. |
| III. Orchestrator-Agnostic Design | ✅ Pass | No MCP tool surface changes. Discovery/execution responses become correct for all orchestrators uniformly (the bug affected every orchestrator equally). |
| IV. CLI-First Interface | ✅ Pass | Fix is entirely in the CLI layer. No new commands or flags added; `spectra index --rebuild` is the existing recovery path. Behavior is fully deterministic and CI-friendly (exit codes unchanged). |
| V. Simplicity (YAGNI) | ✅ Pass | One new internal service, two call-site rewires, one verification of an existing flow. No new abstractions beyond what is required to eliminate the duplicated write+index sequence. The service exists *because* there are two call sites today — extraction is justified on the "third use case" rule by counting "batch + from-description + future generation flows" but more directly by the bug itself: drift is the cost of duplication. |

**Quality Gates (from constitution Quality Gates table)**: Schema Validation, ID Uniqueness, and Priority Enum gates are untouched (the test files written are identical in shape to today). Index Currency gate now correctly reflects from-description additions. Dependency Resolution gate is unaffected (no change to `depends_on`).

**Gate decision**: PASS. No Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/049-from-description-index-parity/
├── plan.md              # This file
├── spec.md              # Feature spec (already generated)
├── research.md          # Phase 0 output — design decisions & alternatives
├── data-model.md        # Phase 1 output — types touched/added
├── quickstart.md        # Phase 1 output — manual verification path
├── contracts/
│   └── test-persistence-service.md   # Internal contract for TestPersistenceService
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── IO/
│   │   ├── TestFileWriter.cs                        # Existing; unchanged
│   │   └── TestPersistenceService.cs                # NEW — file write + index regen entry point
│   ├── Commands/
│   │   ├── Generate/
│   │   │   └── GenerateHandler.cs                   # MODIFY — both flows route through TestPersistenceService
│   │   └── Index/
│   │       ├── IndexCommand.cs                      # No change
│   │       └── IndexHandler.cs                      # Verify --rebuild path; no change expected
│   └── Program.cs                                   # MODIFY (if DI) — register TestPersistenceService (handler may continue to instantiate directly; see research)
└── Spectra.Core/
    ├── Index/
    │   ├── IndexGenerator.cs                        # Existing; unchanged
    │   └── IndexWriter.cs                           # Existing; unchanged
    └── Models/
        ├── TestCase.cs                              # Existing; unchanged
        ├── MetadataIndex.cs                         # Existing; unchanged
        └── TestIndexEntry.cs                        # Existing; unchanged

tests/
├── Spectra.CLI.Tests/
│   ├── IO/
│   │   └── TestPersistenceServiceTests.cs           # NEW — unit tests for the service
│   ├── Commands/
│   │   ├── Generate/
│   │   │   └── GenerateHandlerFromDescriptionIndexTests.cs   # NEW — from-description CLI tests
│   │   └── Index/
│   │       └── IndexHandlerRebuildTests.cs          # NEW (or extend) — regression for rebuild
│   └── Regression/
│       └── BatchIndexEquivalenceTests.cs            # NEW — byte-equivalence guard
└── Spectra.MCP.Tests/
    └── Tools/
        └── FromDescriptionDiscoveryTests.cs         # NEW — find_test_cases / saved-selection integration
```

**Structure Decision**: Use the existing multi-project layout (CLI / Core / MCP / GitHub). The new `TestPersistenceService` lives in `src/Spectra.CLI/IO/` next to `TestFileWriter` because it is an orchestration concern (write-then-index) consumed by CLI handlers, not a core domain primitive — `Spectra.Core` retains its current responsibility for the index data model and the `IndexGenerator`/`IndexWriter` primitives. Tests follow the existing project split: unit tests in `Spectra.CLI.Tests`, MCP integration in `Spectra.MCP.Tests`.

## Complexity Tracking

> No Constitution Check violations. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| *(none)* | | |
