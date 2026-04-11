# Implementation Plan: Generation & Verification Progress Bars

**Branch**: `041-progress-bars` | **Date**: 2026-04-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/041-progress-bars/spec.md`

## Summary

Add real-time progress bars to `spectra ai generate` and `spectra ai update` runs on two surfaces: (1) terminal via Spectre.Console `AnsiConsole.Progress()` showing per-batch generation progress and per-test critic verification progress; (2) browser progress page (`.spectra-progress.html`) by adding a `progress` object to `.spectra-result.json` that the existing 2-second auto-refresh picks up. Suppression rules wire into existing `OutputFormat`/`VerbosityLevel` enums. No new dependencies.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: Spectre.Console (already in use — `ProgressReporter.ProgressAsync` at `src/Spectra.CLI/Output/ProgressReporter.cs:64`), System.Text.Json
**Storage**: File system — `.spectra-result.json` (existing, NTFS-flushed via `ProgressManager.FlushWriteFile` at `src/Spectra.CLI/Progress/ProgressManager.cs:198`) and `.spectra-progress.html` (existing, atomic temp+move via `ProgressPageWriter` at `src/Spectra.CLI/Progress/ProgressPageWriter.cs:26`)
**Testing**: xUnit. Existing homes: `tests/Spectra.CLI.Tests/Progress/`, `tests/Spectra.CLI.Tests/Output/`, `tests/Spectra.CLI.Tests/Results/`
**Target Platform**: Cross-platform CLI (Windows primary, also Linux/macOS)
**Project Type**: CLI application (single project — `src/Spectra.CLI`)
**Performance Goals**: Progress bar repaint cost negligible vs. 4–6s critic call latency. Per-test result file write during verification adds ≪50ms vs. ≥4s critic call.
**Constraints**: Zero ANSI bytes on stdout when `--output-format json` or `--verbosity quiet` or non-TTY. No new disk writes during generation phase beyond existing per-batch result file writes.
**Scale/Scope**: Typical run is 30–100 generated tests; max ~5–10 batches; per-test critic loop runs once per generated test.

### Key Code Surfaces (Discovered)

| Surface | File | Lines |
|---|---|---|
| Generate batch loop | `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | 564–680 |
| `effectiveCount` & `totalBatches` calculation | same | 559–562 |
| Critic per-test loop (`VerifyTestsAsync`) | same | 1794–1832 |
| In-progress result write (`UpdateProgress` / `WriteInProgressResultFile`) | same | 1968–1990 |
| Verification progress write (`WriteVerificationProgress`) | same | 1992–2015 |
| Update classification (no chunks — single batch) | `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` | 269 |
| Update apply-changes proposal loop | same | 579–631 |
| `ProgressManager` (state holder + file writer) | `src/Spectra.CLI/Progress/ProgressManager.cs` | 1–206 |
| `ProgressPhases` (generate/update phase arrays) | `src/Spectra.CLI/Progress/ProgressPhases.cs` | 8–12 |
| `ProgressPageWriter` (HTML string template, phase stepper) | `src/Spectra.CLI/Progress/ProgressPageWriter.cs` | 56–340 |
| `JsonResultWriter` | `src/Spectra.CLI/Output/JsonResultWriter.cs` | 10–36 |
| `GenerateResult` model | `src/Spectra.CLI/Results/GenerateResult.cs` | 5–47 |
| `UpdateResult` model | `src/Spectra.CLI/Results/UpdateResult.cs` | 8–59 |
| `VerbosityLevel` enum | `src/Spectra.CLI/Infrastructure/VerbosityLevel.cs` | 6–32 |
| `OutputFormat` enum | `src/Spectra.CLI/Infrastructure/OutputFormat.cs` | 6–17 |
| Existing Spectre Progress wrapper | `src/Spectra.CLI/Output/ProgressReporter.cs` | 64–91 |

### Surprises Worth Calling Out

- **Critic is per-test, not batched.** Per-test progress increments are natural. Each `critic.VerifyTestAsync` call (`GenerateHandler.cs:1821`) is a discrete unit.
- **`UpdateHandler` has no "chunks".** Spec text mentioned chunks; the actual code does a single classification batch (`classifier.ClassifyBatch`) followed by a per-proposal apply loop. Plan adapts: progress bar in update mode tracks the **per-proposal apply loop** (`UpdateHandler.cs:579–631`), not chunks. FR-005 still satisfied; spec language updated in `data-model.md` glossary.
- **Both handlers already own a `ProgressReporter`** (`_progress`). The Spectre wrapper pattern is established — plan reuses it rather than introducing a parallel mechanism.
- **`.spectra-progress.html` is a `$$"""..."""` raw-string interpolation in C#**, not an external template. New CSS / DOM lives inline in `ProgressPageWriter`.

## Constitution Check

| Principle | Applies? | Status |
|---|---|---|
| I. GitHub as Source of Truth | Indirect — touches result/progress files which are *not* committed (gitignored) | ✅ No new committed state |
| II. Deterministic Execution | Doesn't touch MCP execution engine | ✅ N/A |
| III. Orchestrator-Agnostic Design | Progress bars are display-only; suppressed in JSON mode so SKILLs/CI are unaffected | ✅ Pass |
| IV. CLI-First Interface | Pure CLI feature; no new commands; no new flags | ✅ Pass |
| V. Simplicity (YAGNI) | Reuses existing `ProgressReporter`, `ProgressManager`, Spectre patterns; no new abstractions; ETA/cancel deliberately out of scope | ✅ Pass |

**Quality Gates** (validate, ID uniqueness, etc.) — unaffected. No new schema fields are persisted to test files.

**Result**: ✅ PASS — no violations, no Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/041-progress-bars/
├── plan.md              # This file
├── research.md          # Phase 0 — minimal (no NEEDS CLARIFICATION)
├── data-model.md        # Phase 1 — ProgressSnapshot model + JSON shape
├── quickstart.md        # Phase 1 — manual verification steps
├── contracts/
│   └── result-json-progress-schema.md   # JSON schema for `progress` object
└── tasks.md             # Phase 2 — created by /speckit.tasks
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Commands/
│   ├── Generate/
│   │   └── GenerateHandler.cs           # MODIFY: wrap batch loop + critic loop in AnsiConsole.Progress; emit ProgressSnapshot per batch + per critic call
│   └── Update/
│       └── UpdateHandler.cs             # MODIFY: wrap proposal apply loop; emit ProgressSnapshot per proposal
├── Progress/
│   ├── ProgressManager.cs               # MODIFY: accept ProgressSnapshot; serialize as `progress` field in result JSON; clear on Complete/Fail
│   ├── ProgressPageWriter.cs            # MODIFY: render new progress-section HTML + CSS below phase stepper
│   └── ProgressSnapshot.cs              # NEW: record type (phase, target, generated, verified, current_batch, total_batches, last_test_id, last_verdict)
├── Output/
│   └── ProgressReporter.cs              # (no changes — existing AnsiConsole.Progress wrapper is reused)
└── Results/
    ├── GenerateResult.cs                # MODIFY: add optional `Progress` field (cleared by Complete())
    └── UpdateResult.cs                  # MODIFY: same

tests/Spectra.CLI.Tests/
├── Progress/
│   ├── ProgressBarTests.cs              # NEW: terminal bar increment + suppression rules
│   ├── ProgressPageTests.cs             # NEW: HTML rendering (gen + verify sections)
│   └── ProgressManagerProgressFieldTests.cs  # NEW: clear-on-complete behavior
└── Results/
    └── ResultFileProgressTests.cs       # NEW: progress object written/removed correctly
```

**Structure Decision**: Single-project CLI layout already in use. New code lives entirely under `src/Spectra.CLI/` with one new file (`ProgressSnapshot.cs`) and modifications to 6 existing files. Tests added to existing `Progress/` and `Results/` test folders (no new test infrastructure).

## Phase 0 — Research

No NEEDS CLARIFICATION markers in spec; no new dependencies. `research.md` is intentionally minimal — captures three tactical decisions discovered during exploration:

1. **Reuse existing `ProgressReporter` Spectre wrapper** rather than calling `AnsiConsole.Progress()` directly. Rationale: it already centralizes the suppression check (`if (_outputFormat == OutputFormat.Json) return`).
2. **Suppression for non-TTY**: gate on `AnsiConsole.Profile.Capabilities.Interactive` in `ProgressReporter`. Rationale: existing code path; Spectre exposes this directly.
3. **Update bar tracks proposals, not chunks** (since chunks don't exist in code). Rationale: discovered during exploration; spec entity glossary updated to reflect actual unit.

## Phase 1 — Design

### Data Model (`data-model.md`)

**ProgressSnapshot** (record):

- `Phase` (enum: `Generating`, `Verifying`, `Updating`)
- `TestsTarget` (int)
- `TestsGenerated` (int)
- `TestsVerified` (int)
- `CurrentBatch` (int) — for update phase, this is `current_proposal_index`
- `TotalBatches` (int) — for update phase, this is `total_proposals`
- `LastTestId` (string?)
- `LastVerdict` (string?) — `grounded` | `partial` | `hallucinated` | null

Lifecycle: created at start of generation/update, mutated by handler after each batch and after each critic call, persisted by `ProgressManager.Update(result)` as `result.Progress`, cleared (set to null) by `ProgressManager.Complete()` and `ProgressManager.Fail()`.

JSON serialization: omitted from output when null (existing camelCase + `IgnoreCondition.WhenWritingNull` policy already handles this in `JsonResultWriter`).

### Contract (`contracts/result-json-progress-schema.md`)

Documents the `progress` field shape inside `.spectra-result.json` so SKILL authors and progress page consumers can rely on it. Schema-only; no formal JSON Schema file (overkill for one optional object).

### Quickstart (`quickstart.md`)

Manual verification recipe: (1) run `spectra ai generate <suite> --count 40`, watch terminal bar; (2) open `.spectra-progress.html` in browser, watch refresh; (3) run with `--output-format json`, confirm clean JSON on stdout (no ANSI); (4) run with `--verbosity quiet`, confirm silent.

### Agent Context Update

Run `.specify/scripts/bash/update-agent-context.sh claude` to record the new `ProgressSnapshot` model and the modified `ProgressManager`/`ProgressPageWriter` surfaces in the Claude agent context file.

### Constitution Re-check (post-design)

✅ Still passing. No new files committed to git (result/progress JSON/HTML are gitignored). No new abstractions beyond the single `ProgressSnapshot` record. No new dependencies. CLI surface unchanged.

## Complexity Tracking

*No violations to justify.*
