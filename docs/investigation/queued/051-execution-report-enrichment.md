# Queued feature 051 — Execution report enrichment

> **Investigation-only.** No production code, specs, configs, or skills were modified.
> Every claim about current behavior is cited `file:line`. Hypotheses are marked `INFERRED`
> with what would confirm them.

## Two preliminaries

- **No draft file exists** for this feature. Searches for "report enrichment" and "execution report"
  surfaced no draft spec and no `queued/`/`backlog/`/`draft/` directory. Intent below is
  reconstructed from the one-line summary: *"richer execution reports."*
- **Numbering collision.** The repo already ships an *implemented, unrelated* spec
  `specs/051-filter-schema-alignment` (v1.52.6). The number `051` here is only a working label for
  this conceptual feature.

## Reconstructed intent

Add more useful information to the execution reports produced after a manual-test run — e.g. test
priority/tags/component, linked acceptance criteria or source docs, environment/CI metadata, timing
breakdowns — across the JSON / Markdown / HTML outputs.

## 1. Does the problem still exist? — **Yes**

Report contents are a fixed set of fields, so "richer" means adding fields and rendering them. The
fields are defined and exhaustive today:

- Run-level shape — `ExecutionReport`: `run_id`, `suite`, `environment`, `started_at`,
  `completed_at`, `duration_minutes` (computed), `executed_by`, `status`, `summary`, `results`,
  `filters` (`src/Spectra.Core/Models/Execution/ExecutionReport.cs:8-68`).
- Per-test shape — `TestResultEntry`: `test_id`, `title`, `status`, `attempt`, `duration_ms`,
  `notes`, `blocked_by`, `preconditions`, `steps`, `expected_result`, `test_data`,
  `screenshot_paths` (`src/Spectra.Core/Models/Execution/TestResultEntry.cs:8-66`).

Notably **absent** from `TestResultEntry`: priority, tags, component, linked criteria IDs, and
source-doc refs — all of which exist on the underlying `TestCase` and would be natural enrichments.
So the gap is real.

The reports are assembled and rendered by:

- `ReportGenerator.Generate(run, results, titles, testCases)` — maps each result into a
  `TestResultEntry`, pulling preconditions/steps/expected-result/test-data/screenshots from the test
  case (`src/Spectra.MCP/Reports/ReportGenerator.cs:14-69`). This is where new per-test fields would
  be populated; the `testCases` dictionary it already receives carries the extra `TestCase` data.
- `ReportWriter` — emits all three formats: JSON via snake-case serialization
  (`src/Spectra.MCP/Reports/ReportWriter.cs:56-60`), Markdown
  (`ReportWriter.cs:111-243`), and HTML (`ReportWriter.cs:262-788`, with per-test rendering in
  `RenderTestContent()` around `ReportWriter.cs:793-884`). JSON picks up new properties
  automatically; MD/HTML need explicit rendering edits.

## 2. Where is the seam now?

Entirely inside the **MCP execution server**, which the migration reused unchanged and which is
client-agnostic by construction — it runs no model and has no Copilot dependency
(`docs/investigation/04-execution.md:11-18`). The owning components:

- **Schema:** `src/Spectra.Core/Models/Execution/ExecutionReport.cs` and `TestResultEntry.cs`
  (additive properties; `[JsonIgnore(WhenWritingNull)]` is the established pattern for optional
  fields).
- **Population:** `src/Spectra.MCP/Reports/ReportGenerator.cs:14-69`.
- **Rendering:** `src/Spectra.MCP/Reports/ReportWriter.cs` (JSON auto; MD ~`:111-243`; HTML
  ~`:262-788` + `RenderTestContent` ~`:793-884`).

This is the same seam the original pre-migration feature would have targeted — the report pipeline
never depended on the model layer.

## 3. Verdict — **SURVIVES UNCHANGED**

Reports were always produced by the deterministic MCP server, not by any model path, so the
migration neither removed nor relocated this feature's ground. It is an additive
schema-plus-rendering change, implementable against current code as originally intended.

## 4. Dependencies / risk

- **Regression net:** touches the MCP engine (`Spectra.MCP/Reports`) and the shared
  `Spectra.Core/Models/Execution` records. The records are `required`-heavy `record` types; new
  fields should be optional/nullable to avoid breaking existing construction sites and report
  fixtures. JSON consumers (the dashboard) tolerate additive fields, but golden-file/report tests
  will need updating.
- **No provider/SDK entanglement.** Independent of the still-pending provider/SDK retirement.
- **Fully independent** of the other three queued features — different files, no shared seam.
