# Implementation Plan: Execution Report Enrichment

**Branch**: `061-execution-report-enrichment` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/061-execution-report-enrichment/spec.md`

## Summary

Enrich the post-run execution reports with information that already exists on the underlying
`TestCase` but is currently dropped — **priority, tags, component, linked acceptance-criteria IDs,
and source-doc references** per test — plus a minimal run-level **timing breakdown**, across all three
output formats (JSON, Markdown, HTML).

The change is a schema-plus-rendering addition confined to the deterministic MCP report path. The data
is already in hand: `FinalizeExecutionRunTool` loads each result's full `TestCase` into the
`testCases` dictionary and passes it to `ReportGenerator.Generate`
(`FinalizeExecutionRunTool.cs:89-104`), which already maps preconditions/steps/expected-result/
test-data from it. We add five optional properties to `TestResultEntry`, populate them in
`ReportGenerator`, and render them in `ReportWriter` (JSON auto via snake-case serialization; Markdown
and HTML explicitly). No new data plumbing, no engine/tool/state-machine changes.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.Text.Json (snake-case serialization, `JsonIgnore(WhenWritingNull)`). No new dependencies.
**Storage**: File-based report output under `.execution/reports/` (JSON/MD/HTML). SQLite execution DB is read upstream, untouched here.
**Testing**: xUnit — `Spectra.MCP.Tests` (`Reports/ReportGeneratorTests.cs`, `Reports/ReportGeneratorFixesTests.cs`, `Tools/FinalizeExecutionRunTests.cs`).
**Target Platform**: Cross-platform .NET CLI + MCP server (developed on Windows).
**Project Type**: Single solution, multi-project (`Spectra.Core`, `Spectra.CLI`, `Spectra.MCP`). This feature touches `Spectra.Core` (models) and `Spectra.MCP` (report generation/rendering) only.
**Performance Goals**: No regression. Report generation is per-run, off the execution hot path; the additions are O(n) over results already in memory.
**Constraints**: Purely additive (no existing field renamed/removed); every new field optional/nullable and omitted when empty; engine stays client-agnostic with no behavior change.
**Scale/Scope**: A report covers one run's results (typically tens to a few hundred entries). Two model files edited, two MCP renderer/generator files edited, plus tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. GitHub as Source of Truth** | ✅ No new storage. New fields are sourced from test-case files already in Git; reports remain generated artifacts. |
| **II. Deterministic Execution** | ✅ Reports are produced by the deterministic MCP server with no model involvement. Additions are pure functions of run results + test cases already loaded. No state-machine change. |
| **III. Orchestrator-Agnostic Design** | ✅ No MCP tool contract change. Report payload grows by optional fields only; orchestrators that ignore them are unaffected. No bidirectional sync. |
| **IV. CLI-First Interface** | ✅ No CLI surface change. Output flows through the existing validated report writer; the AI never writes files directly. |
| **V. Simplicity (YAGNI)** | ✅ No new abstractions, no feature flags, no backward-compat shims (additive optional fields are the compat mechanism). Reuses the established `JsonIgnore(WhenWritingNull)` pattern. Run-level enrichment kept to a single minimal timing breakdown derived from data already present. |

**Result: PASS** — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/061-execution-report-enrichment/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── report-schema.md # Phase 1 output — report field contract (additions)
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/
│       └── Execution/
│           ├── TestResultEntry.cs   # EDIT: +5 optional per-test fields
│           └── ReportTiming.cs      # NEW: optional run-level timing record
│           └── ExecutionReport.cs   # EDIT: +1 optional `timing` field
└── Spectra.MCP/
    └── Reports/
        ├── ReportGenerator.cs       # EDIT: populate new fields from testCases; compute timing
        └── ReportWriter.cs          # EDIT: render new fields in Markdown + HTML (JSON auto)

tests/
└── Spectra.MCP.Tests/
    └── Reports/
        ├── ReportGeneratorTests.cs        # EDIT: +tests for population & timing
        ├── ReportGeneratorFixesTests.cs   # (unchanged unless convenient)
        └── ReportWriterEnrichmentTests.cs # NEW: MD/HTML/JSON rendering + omit-when-empty
```

**Structure Decision**: Single-solution multi-project layout (existing). All changes land in
`Spectra.Core/Models/Execution` (schema) and `Spectra.MCP/Reports` (population + rendering), with new
tests in `Spectra.MCP.Tests/Reports`. No changes to `Spectra.MCP/Execution` (engine), `Tools` surface,
or any CLI project.

## Rendering Altitude Decision (key design choice)

The existing report **already** renders rich per-test content (preconditions, steps, expected-result,
test-data, screenshots) only for **non-passing** tests in Markdown/HTML, while **JSON carries it for
every test** (`ReportWriter.cs` Failed/Skipped/Blocked sections + `RenderTestContent` ~:793; JSON via
serialization). The new fields follow this exact, established altitude:

- **JSON** — all five per-test fields + run-level timing serialize automatically for **every** entry
  (snake-case, omitted when null/empty). This is the complete machine-readable surface (the dashboard
  consumes JSON).
- **Markdown / HTML** — the list-valued fields (tags, component, criteria, source-refs) render inside
  the existing per-test detail blocks (consistent with steps/preconditions today), omitted when empty.
  **Priority** additionally gets a dedicated column in the "All Results" table in both MD and HTML so
  it surfaces for **every** test, including passing ones (priority is the primary triage scalar in
  User Story 1).
- **Run-level timing** renders in the MD header block and the HTML `meta-info` header, omitted when
  unavailable.

This keeps the change minimal and self-consistent rather than restructuring passing-row rendering.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
