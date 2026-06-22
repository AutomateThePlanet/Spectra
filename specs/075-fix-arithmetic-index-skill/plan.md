# Implementation Plan: Critic Arithmetic Mandate, Index-Writer Path Fix, Skill Failure-Branch Guard

**Branch**: `075-fix-arithmetic-index-skill` | **Date**: 2026-06-22 | **Spec**: [spec.md](spec.md)

## Summary

Three independent root causes confirmed in a live unit-converter audit. All three have narrow, precise fixes:

1. **`critic-verification.md` prompt gap** — add arithmetic mandate section (no code change)
2. **Two writer lines** — `IngestTestsCommand:153` and `IngestUpdateCommand:166` both call `Path.GetRelativePath(testsPath, file)` which yields `unit-converter\TC-100.md`; change both to `Path.GetRelativePath(suitePath, file)` to yield bare `TC-100.md`
3. **`spectra-generate.md` skill gap** — add failure-branch check after post-8a batch grounding-ingest (no code change)

Cleanup: regenerate the poisoned unit-converter index after fix #2; sweep 56 grounded tests for TC-107-class arithmetic errors after fix #1.

---

## Technical Context

**Language/Version**: C# 12, .NET 8+  
**Primary Dependencies**: System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet  
**Storage**: File-based (`test-cases/{suite}/_index.json`, `.spectra/verdicts/`, test `.md` files)  
**Testing**: xUnit, `Spectra.CLI.Tests` (~1262 tests), `Spectra.Core.Tests` (~568 tests)  
**Target Platform**: Windows/Linux CLI  
**Project Type**: CLI tool  
**Performance Goals**: N/A — file I/O only, no throughput concern  
**Constraints**: Hard regression gate — `Spectra.Core.Tests` and `TestPersistenceService` tests pass unmodified; three 073 consumer fixes (`AuditGroundingHandler:91`, `CompileRepairBatchCommand:95`, `IngestGroundingCommand:115`) stay UNCHANGED  
**Scale/Scope**: Two C# lines + two prompt/skill files + index regeneration + re-critic sweep

---

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Index `_index.json` committed; fix ensures `file` fields are bare, preserving deterministic git state |
| II. Deterministic Execution | PASS | Writer fix is a path-normalization change; idempotent index regeneration |
| III. Orchestrator-Agnostic Design | PASS | All fixes are CLI commands or prompt files; no new model dependency |
| IV. CLI-First Interface | PASS | Writer fix is internal to CLI command handlers; skill guard is prompt-only |
| V. Simplicity (YAGNI) | PASS | Two C# lines, two markdown files — no new abstractions, no new dependencies |

**Quality Gates:**
- `spectra validate` passes after writer fix and index regeneration
- Existing test suite passes unmodified (hard gate per spec)
- No new dependencies introduced

**GATE: PASS** — proceed to implementation.

---

## Project Structure

### Documentation (this feature)

```text
specs/075-fix-arithmetic-index-skill/
├── plan.md              # This file
├── research.md          # Phase 0 output (below)
├── data-model.md        # Phase 1 output (below)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (affected files only)

```text
src/Spectra.CLI/
  Commands/Generate/
    IngestTestsCommand.cs         # FR2: line 153 — GetRelativePath(testsPath → suitePath)
    IngestUpdateCommand.cs        # FR2: line 166 — GetRelativePath(testsPath → suitePath)
  Prompts/Content/
    critic-verification.md        # FR1: add arithmetic mandate section
  Skills/Content/Skills/
    spectra-generate.md           # FR3: add failure-branch guard after post-8a batch call

tests/Spectra.CLI.Tests/
  Commands/Generate/
    IngestTestsSecondRoundTests.cs  # New: FR2 regression test
    IngestUpdateSecondRoundTests.cs # New: FR2 regression test (update path)
```

---

## Phase 0: Research Findings

All root causes were confirmed at file+line level by prior investigation. No external dependencies or new patterns are required. Research findings document the confirmed state:

### FR1 — Critic arithmetic gap

- **Decision**: Prompt-only fix to `critic-verification.md`
- **Rationale**: Investigation (OQ1.1) confirmed the root cause is a missing prompt section, not model unreliability. The model computes arithmetic correctly when asked; it simply wasn't asked.
- **Confirmed at**: `src/Spectra.CLI/Prompts/Content/critic-verification.md` — the existing "Technique Verification" section covers BVA/EP/ST/DT but has no arithmetic mandate. The new mandate section must be additive (both doc-presence AND arithmetic correctness are required for `grounded`).
- **Alternatives considered**: Out-of-model numeric checker — rejected per OQ1.1 (out of scope).

### FR2 — Index-writer path poisoning

- **Decision**: Two writer lines, `testsPath → suitePath` in `GetRelativePath`
- **Rationale**: `testsPath` = `test-cases/`, `suitePath` = `test-cases/unit-converter/`. `GetRelativePath(testsPath, "test-cases/unit-converter/TC-100.md")` = `unit-converter\TC-100.md` (suite-prefixed). `GetRelativePath(suitePath, "test-cases/unit-converter/TC-100.md")` = `TC-100.md` (bare). The fix is two characters different from the fresh-generation convention (`ParseTestCase:237` sets `FilePath = "{id}.md"` directly).
- **Confirmed at**:
  - `IngestTestsCommand.cs:153`: `var relativePath = Path.GetRelativePath(testsPath, file);`
  - `IngestUpdateCommand.cs:166`: `var relativePath = Path.GetRelativePath(testsPath, file);`
- **Read-only caller verdict** (7 sites, all confirmed read-only, none regenerate `_index.json` with new path entries):
  - `CompileRepairBatchCommand:102` — READ-ONLY. Calls `ReadAsync` on index; builds repair manifest for stdout. No `WriteAsync`.
  - `AuditGroundingHandler:99` — READ-ONLY. Passes `rel` to `parser.Parse` for file-content reading only. No index write.
  - `IngestAnalysisCommand:207` — READ-ONLY. `LoadExistingTestsAsync` loads tests for dedup check. No `WriteAsync`.
  - `ReviewFlaggedHandler:67` — READ-ONLY (scanner). `DeleteAsync` at line 156 calls `WriteAsync` but only REMOVES an entry from the existing list — never creates a new path entry. Preserves whatever paths are already in the index.
  - `ValidateHandler:127` — READ-ONLY. Validates test files; no index write.
  - `CompileAnalysisPromptCommand:206` — READ-ONLY. Reads tests for prompt context. No index write.
  - `CompileUpdatePromptCommand:128` — READ-ONLY. Reads tests for prompt context. No index write.
- **Note for future cleanup**: These 7 read-only callers all use `GetRelativePath(testsPath, file)` — the resulting `rel` is passed only to `parser.Parse` as display metadata (not stored in the index). They are safe. A future centralized path-helper would make the convention explicit; deferred per spec.
- **Alternatives considered**: Shared path-resolver helper — rejected (two writer lines fix the root cause; no third similar case justifies an abstraction per YAGNI).

### FR3 — Skill failure-branch gap

- **Decision**: Prompt-only addition to `spectra-generate.md` after the post-8a batch call
- **Confirmed at**: `spectra-generate.md` lines 221–226 — the batch call has no post-result check. Existing failure branches exist at lines 186 and 219 for `ingest-tests` and `ingest-verdict` exits 5/6. The new guard mirrors those.
- **Signal logic**: `written == 0 AND kept-grounded > 0` → STOP+report. `written != kept-grounded` → surface as warning. Rationale: `written: 0` alone is benign when kept-grounded is also 0 (no grounded tests in this batch — not an error).

### FR4 — Index regeneration (cleanup)

- **Decision**: Run `spectra docs index` on unit-converter after FR2 lands
- **Confirmed at**: `test-cases/unit-converter/_index.json` currently has suite-prefixed `file` fields from prior second-round re-ingest. `ApplyEdit:129` re-persists `FilePath = original.FilePath` — it does not self-correct.

### FR5 — Arithmetic sweep (cleanup)

- **Decision**: Re-critic the 56 grounded tests under new mandate; flag any TC-107-class errors
- **Confirmed**: TC-107 is the known reproduction case (`1×10⁻⁹ km → 1E-9 nm`, correct `1000 nm`). Sweep is in-session (no code); uses `compile-critic-prompt` + `ingest-verdict`.

---

## Phase 1: Data Model & Contracts

### Data Model

This feature has no new data models. It modifies existing artifacts:

**`_index.json` test entry `file` field** (existing contract, tightened):

```json
{
  "id": "TC-100",
  "title": "...",
  "file": "TC-100.md"
}
```

- **MUST be**: bare filename only — no directory prefix, no suite segment
- **Convention source**: `GeneratedTestIngestor.ParseTestCase:237` (fresh generation)
- **Writers that MUST produce bare filenames**: `IngestTestsCommand` (FR2 fix), `IngestUpdateCommand` (FR2 fix)
- **Writers that preserve existing paths**: `ReviewFlaggedHandler.DeleteAsync` (removes entries only — passthrough)

**Critic verdict `findings[].status` values** (existing schema, no change):

```json
{
  "verdict": "grounded" | "partial" | "hallucinated",
  "score": 0.0-1.0,
  "findings": [
    {
      "element": "Expected Result",
      "claim": "1×10⁻⁹ km → 1E-9 nm",
      "status": "unverified",
      "evidence": null,
      "reason": "Arithmetic error: 1×10⁻⁹ km = 1E-6 m = 1E+6 nm, not 1E-9 nm"
    }
  ]
}
```

The arithmetic mandate adds a new reason class to `findings[].reason` (free-text, no schema change required).

**`ingest-grounding --all` batch result** (existing schema, no change):

```json
{
  "written": 3,
  "skipped": 12,
  "eligible": 15
}
```

The skill guard checks `written` and compares to `kept-grounded` count tracked by the skill. The schema is not changed — the guard is a skill-level check, not a CLI-level change.

### Contracts

No new CLI commands or public interfaces introduced. This feature modifies:
- Internal writer behavior (path normalization — same command, corrected output)
- A prompt file (critic verification mandate)
- A skill file (grounding failure-branch guard)

No contracts/ directory needed.

---

## Complexity Tracking

No constitution violations. All changes are minimal, targeted, and justified by confirmed root causes.

---

## Implementation Checklist (pre-tasks)

- [x] FR1 confirmed: `critic-verification.md` has no arithmetic mandate — prompt addition only
- [x] FR2 confirmed: both writer lines at exact file+line, fix is `testsPath → suitePath` in both
- [x] FR2 confirmed: 7 read-only callers enumerated — none write new path entries to index
- [x] FR3 confirmed: `spectra-generate.md` post-8a batch call has no result check — addition only
- [x] FR4 confirmed: unit-converter index has suite-prefixed paths; `docs index` regenerates it
- [x] FR5 confirmed: 56 grounded tests to sweep; TC-107 is the known reproduction case
- [x] Hard regression gate: 073 consumer fixes stay UNCHANGED; `Spectra.Core` tests pass unmodified
- [x] Constitution check: all principles pass; no new dependencies; no new abstractions
