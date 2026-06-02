# Implementation Plan: Criteria Coverage Guards

**Branch**: `048-criteria-coverage-guards` | **Date**: 2026-06-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/048-criteria-coverage-guards/spec.md`

## Summary

Add three small non-blocking guards on top of Spec 047 so users are never silently left without acceptance criteria.

1. **Persist the extraction outcome per record.** Add an additive `outcome` field to `CriteriaSource` (default `"extracted"`). Because Spec 047 already guarantees only genuine extraction results reach the cache (`AnalyzeHandler.RunExtractCriteriaAsync` — `IsCacheable` gate at the typed-result level), every record this code writes from now on carries `outcome: "extracted"`. Legacy entries lacking the field deserialize as `extracted` by default — no migration step. Future guards rely on **"entry present ⇒ affirmed extracted"** rather than re-deriving the distinction.

2. **`docs index` zero-criteria corpus warning.** After the per-document extraction loop in `DocsIndexHandler.TryExtractCriteriaAsync`, if `documentsIndexed > 0 && criteriaExtractedTotal == 0`, emit a `_progress.Warning` naming the recovery command and surface the same message as a new optional `criteria_warning` field on `DocsIndexResult`. The command still exits success (`ExitCodes.Success`). Suppressed when `--skip-criteria` is passed (the warning conditional sits inside the `!_skipCriteria` branch).

3. **Generate no-criteria note.** Refactor `GenerateHandler.LoadCriteriaContextAsync` to return both the context string AND whether any suite-specific match happened (today it falls back to "all criteria" on zero match, obscuring the signal). Both `ExecuteDirectModeAsync` and `ExecuteFromDescriptionAsync` then add a non-blocking note to a new `GenerateResult.Notes` collection when the suite-match count is zero, naming the suite and the recovery command. The note is present in the JSON regardless of verbosity; the human-facing console echo is suppressed under `--verbosity quiet`.

All three guards are output-only: no prompts, no exit-code changes, no new commands or flags. SKILL renderings (`spectra-generate.md`, `spectra-docs.md`) surface the new fields.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: YamlDotNet (existing — `CriteriaSource` already uses `[YamlMember]`), System.Text.Json (existing — result classes use `[JsonPropertyName]`/`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`), xUnit (test framework — existing).
**Storage**: File-based YAML master criteria index (`docs/criteria/_criteria_index.yaml`). The single schema change is an **additive optional field** (`outcome`) with a documented default — no schema version bump. JSON result schemas (`DocsIndexResult`, `GenerateResult`) gain additive optional fields with `WhenWritingNull` so existing CI consumers are unaffected.
**Testing**: xUnit in `tests/Spectra.CLI.Tests/` for handler-level tests and `tests/Spectra.Core.Tests/` for the YAML roundtrip on `CriteriaSource`. New test classes live alongside existing 047 tests (`tests/Spectra.CLI.Tests/Commands/`, `tests/Spectra.CLI.Tests/Agent/Copilot/`).
**Target Platform**: Cross-platform CLI (`win-x64`, `linux-x64`, `osx-x64`).
**Project Type**: CLI tool — single-project layout under `src/` and `tests/` (matches 047).
**Performance Goals**: Negligible cost. The zero-criteria-warning check is `O(1)` (sum already computed). The suite-match count is `O(N)` over the criteria list — same complexity class as the existing match filter that already runs.
**Constraints**: Must preserve existing JSON result shapes — only additive optional fields. Must preserve existing `_criteria_index.yaml` file format — only an additive optional field. Must not introduce a blocking prompt anywhere (the whole point of the spec). Must not change exit codes — every condition is success-status.
**Scale/Scope**: One field added to `CriteriaSource` (`Spectra.Core`). Three handlers modified (`AnalyzeHandler` upsert sites, `DocsIndexHandler` extraction tail, `GenerateHandler` criteria-load + result-build sites). Two result classes gain one field each. Two SKILL files updated. ≈9 new test cases per the spec's Test Plan table.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from `.specify/memory/constitution.md` (v1.1.0). Re-evaluated after Phase 1 design — no new violations.

| Principle | Status | Notes |
|---|---|---|
| I. GitHub as Source of Truth | ✅ Pass | The on-disk file (`_criteria_index.yaml`) is extended additively with one optional field. Backward-compatible read of legacy entries codified in FR-002 — verified by `CriteriaSource_Roundtrip_PreservesOutcome` test. No new external storage. |
| II. Deterministic Execution | ✅ Pass | No new asynchrony, no model-dependent retry, no new state. The three guards are pure projections from inputs (`documentsIndexed`, `criteriaExtractedTotal`, suite-match count) to output strings. Identical inputs produce identical warnings. |
| III. Orchestrator-Agnostic Design | ✅ Pass | Zero MCP/tool surface change. Zero provider-chain change. Internal to the CLI's docs-index and generate command paths. |
| IV. CLI-First Interface | ✅ Pass | No new flags, no new subcommands, no interactive prompts (explicit FR-012). All signals flow through the existing structured-result JSON channel and the existing `_progress.Warning` console channel. Exit codes unchanged (FR-007, FR-011). |
| V. Simplicity (YAGNI) | ⚠️ Pass with tension | The `Outcome` field accepts one valid value (`"extracted"`) at write-time today. The spec acknowledges this: "intentionally minimal: because Spec 047 already prevents non-Extracted outcomes from being persisted, the field mostly documents and future-proofs the invariant." Logged in Complexity Tracking. The `LoadCriteriaContextAsync` refactor (returning a small record instead of `string?`) adds a tiny amount of surface but is the minimum needed to distinguish "no suite match" from "fell back to all criteria" — the existing function has a "last resort: use all criteria" fallback (`GenerateHandler.cs:2485-2486`) that conflates the two states. |

**Quality Gates** (`spectra validate`): unchanged. No schema/ID/index/dependency/priority changes.

**Test-Required Discipline**: All new code paths are unit-testable without hitting the AI: the criteria-roundtrip test uses YamlDotNet directly; handler-level tests stub the extractor delegate (matching the 047 pattern); the `LoadCriteriaContextAsync` refactor is exercised through fixture criteria files. Test plan in `spec.md` lists 9 named cases; Phase 2 (`/speckit.tasks`) dispatches them.

## Project Structure

### Documentation (this feature)

```text
specs/048-criteria-coverage-guards/
├── plan.md                       # This file
├── spec.md                       # Feature spec (already written)
├── research.md                   # Phase 0 — decisions D1–D6
├── data-model.md                 # Phase 1 — additive field + result-class additions
├── quickstart.md                 # Phase 1 — local validation walkthrough
├── contracts/
│   ├── criteria-source.md        # YAML schema additive change
│   ├── docs-index-handler.md     # Zero-criteria warning contract
│   ├── generate-handler.md       # No-match note contract + LoadCriteriaContextAsync refactor
│   └── skill-rendering.md        # SKILL surface contract for the new fields
├── checklists/
│   └── requirements.md           # Spec quality checklist (already written; all items pass)
└── tasks.md                      # Phase 2 — generated by /speckit.tasks (NOT this command)
```

### Source Code (repository root)

CLI tool, existing single-project layout under `src/` and `tests/`. Files touched and added by this spec:

```text
src/
├── Spectra.Core/
│   └── Models/
│       └── Coverage/
│           └── CriteriaSource.cs                # MODIFY — add Outcome property (additive, default "extracted")
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── Analyze/
│   │   │   └── AnalyzeHandler.cs                # MODIFY — set Outcome on both CriteriaSource upsert sites (extract path :638-657, import path :1145-) and on the existing-source-update branch
│   │   ├── Docs/
│   │   │   └── DocsIndexHandler.cs              # MODIFY — zero-criteria corpus warning between :199 and :216; thread criteria_warning through to DocsIndexResult
│   │   └── Generate/
│   │       └── GenerateHandler.cs               # MODIFY — refactor LoadCriteriaContextAsync to return (string? Context, int SuiteMatchedCount); add Notes to both ExecuteDirectModeAsync (final result :985-1013) and ExecuteFromDescriptionAsync (:1852-1867) when SuiteMatchedCount == 0
│   ├── Results/
│   │   ├── DocsIndexResult.cs                   # MODIFY — add CriteriaWarning string? with WhenWritingNull
│   │   └── GenerateResult.cs                    # MODIFY — add Notes IReadOnlyList<string>? with WhenWritingNull
│   └── Skills/
│       └── Content/
│           └── Skills/
│               ├── spectra-docs.md              # MODIFY — render criteria_warning after results
│               └── spectra-generate.md          # MODIFY — render notes after results

tests/
├── Spectra.Core.Tests/
│   └── Models/
│       └── Coverage/
│           └── CriteriaSourceTests.cs           # ADD — YAML roundtrip + legacy-entry default test
└── Spectra.CLI.Tests/
    ├── Agent/
    │   └── Copilot/
    │       └── CriteriaExtractorOutcomeTests.cs # ADD — Extract_RealEmpty_RecordsOutcomeExtracted via AnalyzeHandler-level test (uses existing 047 stub seams)
    └── Commands/
        ├── DocsIndexZeroCriteriaTests.cs        # ADD — DocsIndex_* cases (3): zero/found/skip-criteria
        └── GenerateNoCriteriaNoteTests.cs       # ADD — Generate_* cases (4): batch no-match, from-description no-match, with-match (negative), notes-in-json-when-quiet
```

**Structure Decision**: Single CLI project layout, mirroring 047. The `Outcome` field lives in `Spectra.Core` (the model assembly) because `CriteriaSource` is already there; the field's lone writer is `AnalyzeHandler` in `Spectra.CLI`. Result classes stay in `Spectra.CLI.Results`. SKILLs stay in `Spectra.CLI/Skills/Content/Skills/` (the bundled skills location; **note**: the user's input referenced `.github/skills/` — that path is the install target in consuming projects, not the source-of-truth bundle path in this repo).

## Complexity Tracking

> Filled because Constitution V (Simplicity / YAGNI) was flagged with tension.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Adding `CriteriaSource.Outcome` field whose only valid write-value today is `"extracted"` | Distinguishes "affirmed empty" from "inconclusive" at the record level. Without it, any future guard that warns on `criteria_count: 0` either cries wolf on legitimately-empty docs or has to re-derive the distinction heuristically. Spec 047 already guarantees the invariant — the field documents and enforces it on the wire. | Skipping the field entirely and relying on the implicit invariant: rejected because (a) the implicit invariant is not visible to consumers reading the YAML, (b) any future writer that broke the invariant would silently corrupt the cache, and (c) future states (`migrated`, `imported_with_caveats`) can be added without a second schema rev. |
| Changing `LoadCriteriaContextAsync` return type from `string?` to a small internal record `(string? Context, int SuiteMatchedCount)` | The current function has a "last resort: use all criteria" fallback at `GenerateHandler.cs:2485-2486` that collapses two semantically distinct states ("matched the suite" vs "fell back because no match"). The no-criteria note specifically targets the latter — the existing return type can't express it. | Re-deriving the suite-match count in the caller by re-loading and re-filtering the criteria files: rejected as obvious duplication. Threading an `out int matchedCount` parameter: equivalent but less readable. |

No other deviations. No new dependencies, no new MCP tools, no architecture changes. All three SKILL-rendering changes are non-code.
