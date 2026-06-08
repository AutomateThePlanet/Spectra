# Quickstart: Execution Report Enrichment

## What changes

Execution reports now carry, per test, the test's **priority, tags, component, linked
acceptance-criteria IDs, and source-doc references**, plus a run-level **timing breakdown** — across
JSON, Markdown, and HTML. All additions are optional and disappear when the underlying data is absent.

## Build & test

```bash
dotnet build
dotnet test tests/Spectra.MCP.Tests       # report generation + rendering tests live here
```

## See it end-to-end

Reports are produced when a run is finalized (the `finalize_execution_run` MCP tool), written to
`.execution/reports/` as `<timestamp>_<author>_<suite>.{json,md,html}`.

1. Run a suite whose test cases have priority/tags/component and link criteria/source docs.
2. Finalize the run.
3. Inspect the generated report:
   - **JSON** — every `results[]` entry includes `priority`, and includes `tags`/`component`/
     `criteria`/`source_refs` when present; the top object includes `timing` when any test has a
     duration.
   - **Markdown** — the "All Results" table has a `Priority` column; failed-test detail blocks list
     component/tags/criteria/source docs; the header shows total/avg test time.
   - **HTML** — non-passing test cards/rows show the new fields; the "All Results" table has a
     `Priority` column; the header shows a timing item.

## Verifying backward compatibility

- A report generated without test-case data (e.g. `testCases` is null) still validates: the new
  per-test keys are simply omitted and `timing` reflects only available durations.
- Existing JSON consumers (the dashboard) are unaffected — the additions are optional keys.

## Key files

- Schema: `src/Spectra.Core/Models/Execution/TestResultEntry.cs`, `ReportTiming.cs` (new),
  `ExecutionReport.cs`
- Population + timing: `src/Spectra.MCP/Reports/ReportGenerator.cs`
- Rendering: `src/Spectra.MCP/Reports/ReportWriter.cs`
- Tests: `tests/Spectra.MCP.Tests/Reports/`
