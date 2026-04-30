# Fixture: large_single_suite

**Purpose**: Drives `SpilloverWriterTests` and `BehaviorAnalyzer_UsesSpilloverFilesTests` (Phase 5 of branch 045 — exclusion patterns + spillover).

**Spec**: see `specs/045-doc-index-restructure/spec.md` §5.1 ("200 synthetic docs all under one suite directory").

## Shape

200 documents under a single directory tree, totaling ~95K estimated tokens — enough to exceed the default `coverage.max_suite_tokens` of 80K and trigger the per-doc spillover code path.

## Generation

The fixture is **not** checked in as static files. It is created on demand by the test helper `tests/Spectra.CLI.Tests/TestHelpers/LargeSingleSuiteFixtureBuilder.cs` (added in Phase 5 task T066). The helper builds a temp directory of 200 synthetic Markdown docs under one suite and yields its root path to the test.

This README exists in Phase 1 as a placeholder so the directory is tracked in git and the test helper (Phase 5) has a deterministic location to anchor against. Phase 1+2 work does not exercise this fixture.
