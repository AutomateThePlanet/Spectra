# Implementation Plan: Boundary-coverage gap detection (analysis phase)

**Branch**: `062-boundary-coverage-gaps` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/062-boundary-coverage-gaps/spec.md`

## Summary

Surface boundary/edge-case coverage gaps (min/max, off-by-one, empty/null, overflow, timeout) at **analysis time**, carried in the typed analysis recommendation alongside the existing `technique_breakdown`, validated fail-loud at the `ingest-analysis` boundary, and presented as advisory output. The implementation extends the existing model-free analysis seam (`behavior-analysis.md` prompt → in-session model → `AnalysisRecommendationBuilder` → `IngestAnalysisCommand`) with one additive top-level `boundary_gaps` array — mirroring exactly how `field_specs` already rides alongside `behaviors`. The grounding critic (`spectra-critic.agent.md`, `CriticPromptBuilder`, `VerdictIngestor`) is **untouched**.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine, System.Text.Json (no new dependencies)
**Storage**: File-based — agent emits JSON to `.spectra/analysis.json`; nothing new persisted (output is advisory)
**Testing**: xUnit (`tests/Spectra.CLI.Tests`), structured results — never throw on validation
**Target Platform**: Cross-platform CLI (.NET); analysis model step runs in the interactive session
**Project Type**: CLI tool (single solution, `Spectra.CLI` + `Spectra.Core` + `Spectra.MCP`)
**Performance Goals**: No new model call on the CLI side; ingest stays deterministic (sub-second parse of one JSON document)
**Constraints**: Additive/backward-compatible (legacy analysis JSON without `boundary_gaps` ingests cleanly); fail-loud on malformed boundary-gap payload (exit 6); advisory-only (never mutates tests, never blocks generation); critic path byte-for-byte unchanged
**Scale/Scope**: One new model record, one parser extension, one additive field on `AnalysisRecommendation`, ingest output additions, one prompt section, one agent-doc paragraph, ~2 doc updates; ~6 production files, ~3 test files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ PASS | No external storage. Output is advisory; nothing persisted beyond the existing `.spectra/analysis.json` agent file (already gitignored working artifact). |
| II. Deterministic Execution | ✅ PASS | The CLI side is model-free and deterministic: same `boundary_gaps` JSON → same typed result. Fail-loud classification mirrors the existing `AnalysisIngestOutcome` contract. |
| III. Orchestrator-Agnostic Design | ✅ PASS | Boundary gaps are emitted by whatever LLM runs the in-session analysis; the CLI only parses JSON. Ingest output stays minimal and self-contained. |
| IV. CLI-First Interface | ✅ PASS | No new command; extends `ingest-analysis` output. Deterministic exit codes preserved (0 success, 5 empty, 6 parse/malformed). AI never writes files directly — output flows through the validated ingest handler. |
| V. Simplicity (YAGNI) | ✅ PASS | Reuses the proven `field_specs`-alongside-`behaviors` pattern; one additive field, no new abstraction, no config flag, no feature toggle. `kind` stays a free-form string (like `technique`) rather than a brittle enum. |

**Gate result: PASS** — no violations; Complexity Tracking table not required.

## Project Structure

### Documentation (this feature)

```text
specs/062-boundary-coverage-gaps/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── ingest-analysis-output.md     # Updated ingest-analysis JSON contract
│   └── boundary-gaps.schema.json     # JSON schema for the boundary_gaps payload
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Agent/Analysis/
│   ├── BoundaryGap.cs                 # NEW — typed agent-output record (field, kind, description, source)
│   ├── IdentifiedBehavior.cs          # unchanged
│   └── BehaviorAnalysisResult.cs      # (reference only; ingest path doesn't use it)
├── Generation/
│   ├── AnalysisRecommendation.cs      # MODIFY — add additive BoundaryGaps field + factory wiring
│   └── AnalysisRecommendationBuilder.cs # MODIFY — parse + fail-loud-validate boundary_gaps
├── Commands/Generate/
│   └── IngestAnalysisCommand.cs       # MODIFY — emit boundary_gaps in JSON + human output
├── Prompts/Content/
│   └── behavior-analysis.md           # MODIFY — instruct model to emit boundary_gaps (extend BVA)
└── Skills/Content/Agents/
    └── spectra-generation.agent.md    # MODIFY — present boundary gaps with the breakdowns

# UNTOUCHED (regression net — verifies seam (b) was chosen correctly):
src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md
src/Spectra.CLI/Agent/Critic/CriticPromptBuilder.cs
src/Spectra.CLI/Verification/VerdictIngestor.cs
src/Spectra.Core/**                    # no changes anywhere in Core

tests/Spectra.CLI.Tests/
├── Generation/
│   ├── BoundaryGapParsingTests.cs            # NEW — detection + fail-loud + legacy/empty
│   └── AnalysisRecommendationBuilderTests.cs # MODIFY — boundary_gaps carried alongside technique_breakdown
├── Commands/  (or Generate/)
│   └── IngestAnalysisBoundaryGapTests.cs     # NEW — ingest emits gaps / fails loud on malformed
└── (regression, unchanged) Verification/VerdictIngestorTests.cs, Agent/BehaviorAnalysisResultTechniqueTests.cs
```

**Structure Decision**: Single-solution CLI tool. All changes land in `Spectra.CLI` (the analysis seam lives entirely there); `Spectra.Core` and the critic trio are untouched, which is the regression guarantee that proves seam (b) (analysis phase) rather than seam (a) (critic) was implemented.

## Complexity Tracking

> No Constitution Check violations. Table intentionally empty.
