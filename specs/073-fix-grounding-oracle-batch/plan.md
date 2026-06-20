# Implementation Plan: Grounding Oracle Fix + Batch Grounding-Ingest (Spec 073 / 072 Amendment)

**Branch**: `073-fix-grounding-oracle-batch` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

## Summary

Fixes two independent path-construction bugs in `AuditGroundingHandler` and `CompileRepairBatchCommand` (same root cause: using `testsPath` instead of `suitePath` to resolve `entry.File`), adds `ingest-grounding --suite --all` batch form to eliminate per-test shell loops, and re-lands Step 8 skill routing with explicit prohibitions on shell improvisation. Root causes confirmed with file+line evidence before any code changes.

---

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: System.CommandLine, System.Text.Json, Spectre.Console (existing), YamlDotNet, Spectra.Core.Parsing.TestCaseParser  
**Storage**: File-based: `test-cases/{suite}/{id}.md`, `_index.json`, `.spectra/verdicts/`  
**Testing**: xUnit (existing), `[Collection("WorkingDirectory")]` isolation pattern  
**Target Platform**: Windows / Linux CLI tool  
**Project Type**: CLI — all changes go in `Spectra.CLI`; `Spectra.Core` untouched  
**Constraints**: No new projects, no MCP, no model calls, no shell improvisation, no duplicated write logic

---

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✓ Pass | No new storage; fixing `.md` detection |
| II. Deterministic Execution | ✓ Pass | All changes are model-free, deterministic |
| III. Orchestrator-Agnostic | ✓ Pass | No model calls, CLI-first |
| IV. CLI-First Interface | ✓ Pass | `--all` added as CLI flag |
| V. Simplicity (YAGNI) | ✓ Pass | Smallest fix that closes the bugs; batch reuses existing `GroundingWriteBackService.WriteAsync` |

No gate violations. No Complexity Tracking table needed.

---

## Root Cause — CONFIRMED (file+line investigation complete)

### Bug 1: Oracle always reports `grounding_written: false`
**File**: `src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs:91`  
**Buggy**: `var testFilePath = Path.Combine(testsPath, testEntry.File);`  
**Why**: `testEntry.File` is set to `"TC-100.md"` by `GeneratedTestIngestor.ParseTestCase` (`src/Spectra.CLI/Generation/GeneratedTestIngestor.cs:237`: `FilePath = $"{id}.md"`). Combining with `testsPath` gives `test-cases/TC-100.md` — missing the suite subdir. `File.Exists("test-cases/TC-100.md")` = false → inner block skipped → `groundingWritten` stays `false`.  
**Fix**: `var testFilePath = Path.Combine(suitePath, testEntry.File);` (`suitePath` defined on line 29)

### Bug 2: `compile-repair-batch` returns `[]` for all tests
**File**: `src/Spectra.CLI/Commands/Generate/CompileRepairBatchCommand.cs:95`  
**Buggy**: `var testFilePath = Path.Combine(testsPath, testEntry.File);`  
**Why**: Same pattern — `testEntry.File = "TC-100.md"`, `testsPath = "test-cases"` → `test-cases/TC-100.md` → `File.Exists` = false → `continue` on line 96 → ALL tests skipped → manifest stays empty. **Independent from Bug 1** — not downstream of the oracle.  
**Fix**: `var testFilePath = Path.Combine(suitePath, testEntry.File);` (`suitePath` defined on line 45)

### Why existing tests passed with the wrong format
Both `AuditGroundingCommandTests.WriteTestMd:54` and `CompileRepairBatchCommandTests.WriteTestMd:56` write `_index.json` with `"file":"smoke/TC-300.md"` (suite/id.md). `Path.Combine("test-cases", "smoke/TC-300.md")` = `test-cases/smoke/TC-300.md` ✓ — accidentally correct under the buggy code. `IngestGroundingCommandTests.WriteTestMd:58` uses `"file":"{{id}}.md"` (correct production format) because it works with `IngestGroundingCommand`'s `suitePath`-based combine — that command was already correct.

**After the fix**, the two test fixtures must change to use `"file":"{{id}}.md"` so they match the production format and work with `suitePath`-based path construction.

---

## Project Structure

### Documentation
```text
specs/073-fix-grounding-oracle-batch/
├── plan.md              # This file
├── research.md          # Inline below (no unknowns; code inspection resolved everything)
├── spec.md
├── checklists/requirements.md
└── tasks.md             # Phase 2 output
```

### Source Code (files to change)
```text
src/Spectra.CLI/
├── Commands/Review/
│   └── AuditGroundingHandler.cs          # Fix line 91: testsPath → suitePath
├── Commands/Generate/
│   ├── CompileRepairBatchCommand.cs       # Fix line 95: testsPath → suitePath + comment
│   └── IngestGroundingCommand.cs         # Add --all flag, make --test optional
├── Skills/Content/Skills/
│   └── spectra-generate.md               # Rewrite Step 8 batch calls + routing prohibitions

tests/Spectra.CLI.Tests/Commands/
├── AuditGroundingCommandTests.cs         # Fix WriteTestMd:54, MixedSuite consolidated index
├── CompileRepairBatchCommandTests.cs     # Fix WriteTestMd:56, AppendTestToIndex:67
└── IngestGroundingCommandTests.cs        # Add --all batch tests (new test methods)

docs/
├── cli-reference.md                      # Add --all mode documentation
└── usage.md                              # Update Repair Batch section
```

---

## Research (inline — no unknowns)

All decisions resolved by code inspection before writing this plan:

| Decision | Choice | Rationale |
|----------|--------|-----------|
| `entry.File` format | `"TC-100.md"` (no suite prefix) | Set by `GeneratedTestIngestor.ParseTestCase:237` |
| Correct path construction | `Path.Combine(suitePath, entry.File)` | Matches `IngestGroundingCommand:76` which was already correct |
| Skip filter for `--all` (pre-repair) | Skip `partial` verdicts unless `--repaired` is set | Prevents writing partial blocks before the repair loop runs (Spec 071 requires one repair attempt first) |
| Batch `--all` verdict processing | If `--repaired`: write for all ungrounded (grounded and partial); if not `--repaired`: write only for grounded verdicts | Enables two-call batch: `--all` for grounded pass, `--all --repaired --repair-attempts 1` for repair pass |
| Skill Step 8 restructure | NO per-test `ingest-grounding` in 8a loop; ONE batch call after 8a; NO per-test in 8b; ONE batch after 8b | Eliminates all shell loops for grounding ingest; replaces 45+28 individual calls with 2 batch calls |
| `--test` in `IngestGroundingCommand` | Remove `IsRequired = true`; validate in handler: error if neither `--test` nor `--all` | Clean option group |
| Duplicated write logic | `--all` reuses `GroundingWriteBackService.WriteAsync` (same as per-test form) | No duplication |

---

## Phase Plan

### Phase 1 — Fix Both Path Bugs (FR-A1, FR-A2) + Test Fixture Correction

**Work**:
1. `AuditGroundingHandler.cs:91`: `testsPath` → `suitePath`
2. `CompileRepairBatchCommand.cs:95`: `testsPath` → `suitePath`; fix misleading comment on line 93
3. `AuditGroundingCommandTests.cs`: Change `WriteTestMd` index to `"file":"{{id}}.md"` format; update `MixedSuite_SummaryCounts` consolidated index to same format
4. `CompileRepairBatchCommandTests.cs`: Change `WriteTestMd` index to `"file":"{{id}}.md"` format; change `AppendTestToIndex` to same format
5. `dotnet build` → clean; `dotnet test` → all pass

**Gate**: 
- `dotnet test` all pass (21 + existing audit-grounding + compile-repair-batch tests)
- REPACK + REINSTALL + verify git-hash matches HEAD commit

### Phase 2 — Batch Grounding-Ingest + Skill Routing (FR-A3, FR-A4)

**Work**:
1. `IngestGroundingCommand.cs`: Make `--test` optional; add `--all` flag; add batch handler logic
   - Batch logic: scan `.spectra/verdicts/`, filter to suite index, skip already-grounded, filter partials by `--repaired` flag, call `GroundingWriteBackService.WriteAsync` per entry
2. `IngestGroundingCommandTests.cs`: Add ~7 tests for `--all` mode (no verdict dir, grounded batch, skip-already-written, skip-partial-without-repaired, write-partial-with-repaired, mixed suite, missing-suite error)
3. `spectra-generate.md`:
   - Step 8a: remove per-test `ingest-grounding` call from inside the critic loop
   - After 8a loop: add `ingest-grounding --suite {suite} --all`
   - Step 8b: remove per-test `ingest-grounding` from step 8b.5 (grounded and partial cases)
   - After 8b loop: add `ingest-grounding --suite {suite} --all --repaired --repair-attempts 1`
   - Add prohibition block: test paths, state oracle, config — use verbs not shell
4. `dotnet build` → clean; `dotnet test` → all pass

**Gate**:
- REPACK + REINSTALL + verify git-hash matches HEAD commit

### Phase 3 — Docs + CLAUDE.md (FR-A5)

**Work**:
1. `docs/cli-reference.md`: Add `--all` mode entry for `ingest-grounding`; note `grounding_written` semantics
2. `docs/usage.md`: Update "Repair Batch & Resume" section with two-call batch pattern
3. `CLAUDE.md`: Add 073 to Recent Changes
4. Full `dotnet test` run to confirm no regressions

**Gate**: Full test suite green; REPACK + REINSTALL for final delivery.

---

## Data Model

No new entities. `GroundingMetadata`, `TestIndexEntry`, `AuditGroundingEntry`, `AuditGroundingResult` — all unchanged. The oracle fix is purely a path construction change, not a data model change.

---

## Contracts (CLI interface additions)

### `spectra ai ingest-grounding` — new `--all` flag

```
spectra ai ingest-grounding --suite <s> --all [--repaired] [--repair-attempts N] [--output-format json]
```

- `--suite` (required): suite name
- `--all` (new): batch mode — write grounding for all eligible tests in the suite
- `--test` (now optional): per-test mode — still works unchanged
- `--repaired` (existing, now applies to batch): mark as repaired in grounding block
- `--repair-attempts N` (existing, now applies to batch): number of repair attempts
- Exit 0: success (with N written, M skipped)
- Exit 1: error (suite not found, etc.)

**Batch verdict filter**:
- Without `--repaired`: writes grounding for `grounded` verdicts only; skips `partial` verdicts (pre-repair filter)
- With `--repaired`: writes for all ungrounded tests (both `grounded` and `partial` verdicts)
- Always skips tests whose `.md` already has a grounding block (idempotent)
- Always skips `hallucinated` verdicts (test file deleted, `File.Exists` = false)

**JSON output (--output-format json)**:
```json
{
  "command": "ingest-grounding",
  "mode": "batch",
  "suite": "unit-converter",
  "written": 45,
  "skipped_already_written": 0,
  "skipped_partial_pre_repair": 28,
  "errors": 0
}
```

---

## Quickstart Scenarios

### Scenario A: Oracle reports correct state after fix

```bash
spectra ai audit-grounding --suite unit-converter --output-format json
# Before fix: grounding_written: 0 / Not written: 73
# After fix:  grounding_written: 45 / Not written: 28 / pending repair: 28
```

### Scenario B: compile-repair-batch produces manifest after fix

```bash
spectra ai compile-repair-batch --suite unit-converter --output-format json
# Before fix: []
# After fix: [{id: "TC-105", prompt: "...", file: "..."}, ... (28 entries)]
```

### Scenario C: Batch grounding-ingest — one call per phase

```bash
# After 8a critic loop (all tests critiqued, grounded tests have verdict files):
spectra ai ingest-grounding --suite unit-converter --all
# → writes 45 grounded blocks; skips 28 partial tests

# After 8b repair loop (repairs complete, all verdict files in final state):
spectra ai ingest-grounding --suite unit-converter --all --repaired --repair-attempts 1
# → writes 28 blocks for repaired/flagged tests; skips already-written from first call
```
