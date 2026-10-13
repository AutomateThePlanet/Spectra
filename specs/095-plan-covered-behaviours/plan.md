# Implementation Plan: Plan Only Uncovered Behaviours (094 Review Fix)

**Branch**: `095-plan-covered-behaviours` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/095-plan-covered-behaviours/spec.md`

## Summary

One per-behaviour coverage rule — title similarity to an existing suite test of at least
`generation.duplicate_threshold`, with `covered_by` naming the matching test — replaces the "suite test count" figure.
`already_covered`, `recommended` and the 094 generation plan all derive from it. The plan keeps every behaviour but
batches only the uncovered ones, the analyst and skill show the covered ones as "already covered — not generated", and a
094 plan still compiles with a re-run warning.

## Technical Context

**Language/Version**: C# 12, .NET 8; Markdown agent and skill content.

**Primary Dependencies**:
- `DuplicateDetector` (measure, exposed normaliser);
- `AnalysisRecommendationBuilder`;
- `IngestAnalysisCommand`;
- `GenerationPlan` / `GenerationPlanBuilder` (094);
- `CompileBatchPrompt` (094);
- the analyst agent and the generate skill.

No new packages.

**Storage**: the 094 plan file gains the optional `duplicate_threshold` and `covered_by` fields; no other change.

**Testing**: xUnit 2.9.3. Core rows (the measure, performance), CLI rows on a real-init `BaselineTestWorkspace`
(`CriteriaCommands` collection), text guards. Baseline `db20415`: Core 1228 / CLI 2167 (one known failure) /
Execution 228 / Integration 8; 4 warnings.

**Target Platform**: Windows and Linux.

**Project Type**: CLI with bundled skills and agents.

**Performance Goals**: coverage for 2,500 tests × 100 behaviours < 2 s (R2).

**Constraints**:
- no new feature, no required config section, no new range check;
- human summaries on stderr in JSON mode;
- byte-identical plans;
- each fix fails on `db20415` (except the 094-plan compatibility guard);
- verified on real init and a pre-095 workspace.

**Scale/Scope**:
- one new Core class (`TitleCoverage`);
- edits to `DuplicateDetector` (expose the normaliser), `AnalysisRecommendation(Builder)`, `IngestAnalysisCommand`,
  `GenerationPlan`, `GenerationPlanBuilder`, `CompileBatchPrompt`;
- 2 Markdown files (analyst, generate skill);
- 4 docs;
- ~5 test classes plus the replaced 044 row.

## Constitution Check

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | No committed format changes; the plan is scratch | ✅ |
| II. Deterministic | The rule is deterministic: fixed tie order, byte-identical plan | ✅ |
| III. Orchestrator-agnostic, minimal output | One small `covered` array; lines relayed as before | ✅ |
| IV. CLI-first; `--dry-run` | No new writes; the 094 tracked gap (`ingest-analysis` has no `--dry-run`) unchanged | ⚠ tracked (094) |
| V. Simplicity | Reuses the existing measure and threshold; drops the snapshot-based count | ✅ |
| Test-required discipline | T1–T5, each proven against `db20415` | ✅ |

**Post-design re-check**: no new violations. Deliberate changes: the meaning of `already_covered` / `recommended`
(CHANGELOG note), and the replaced 044 assertion.

## Project Structure

```text
src/Spectra.Core/Validation/
├── TitleCoverage.cs                         # NEW: per-behaviour coverage, same measure, pruned (R1, R2)
└── DuplicateDetector.cs                     # NormalizeTitle exposed (no behaviour change)
src/Spectra.CLI/
├── Generation/AnalysisRecommendation.cs     # CoveredBy per behaviour
├── Generation/AnalysisRecommendationBuilder.cs   # threshold param; counts from TitleCoverage; snapshot ignored
├── Commands/Generate/IngestAnalysisCommand.cs    # threshold from config; no snapshot; covered JSON/lines; plan input
├── Generation/Plan/GenerationPlan.cs        # duplicate_threshold, covered_by, CoverageKnown
├── Generation/Plan/GenerationPlanBuilder.cs # batches/counts over uncovered only
├── Commands/Generate/CompileBatchPrompt.cs  # 094-plan warning
└── Skills/Content/{Agents/spectra-analyst.agent.md, Skills/spectra-generate.md}

tests/Spectra.Core.Tests/Validation/TitleCoverageTests.cs                 # T1 (+ performance row)
tests/Spectra.CLI.Tests/Generation/AnalysisRecommendationBuilderTests.cs  # T2 (044 row replaced)
tests/Spectra.CLI.Tests/Generation/Plan/GenerationPlanBuilderTests.cs     # builder rows (uncovered only)
tests/Spectra.CLI.Tests/Commands/Generate/PlanCoverageIngestTests.cs      # T3
tests/Spectra.CLI.Tests/Commands/Generate/PlanCoverageCompileTests.cs     # T4
tests/Spectra.CLI.Tests/Skills/CoveredLinesTextTests.cs                   # T5
```

## Implementation Order

1. Baseline on `db20415`; write T1–T5; proof on the untouched `src`.
2. `TitleCoverage` and the exposed normaliser.
3. Builder and recommendation (threshold, per-behaviour coverage, snapshot ignored); replace the 044 row.
4. Plan model and builder (covered_by, threshold, uncovered-only batches).
5. `ingest-analysis` (config threshold, no snapshot, `covered` JSON and lines).
6. `compile-prompt --batch` 094-plan warning.
7. Analyst and skill text.
8. Docs: cli-reference, usage, CHANGELOG, CLAUDE.md.
9. Full run; built-CLI checks (quickstart) on real init and pre-095; Notes. Ask before committing and merging; never
   push.

## Risks

| Risk | Mitigation |
|---|---|
| The pruned measure differs from `CalculateTitleSimilarity` | The T1 equality row over 2,000 random pairs; pruning only skips pairs that cannot reach or beat the current result |
| The performance row is flaky on a loaded machine | It runs in a class of its own; the time is written to the output; the 2 s bound has ample margin if the measured time is well under it |
| Other tests relied on the snapshot count | Inventory in tasks (T-inventory); the known one is the 044 row |
| Title similarity misses reworded covered behaviours | Unchanged from today's no-snapshot path; documented; semantic coverage out of scope |

## Complexity Tracking

No new violations; the 094 IV gap stays tracked.
