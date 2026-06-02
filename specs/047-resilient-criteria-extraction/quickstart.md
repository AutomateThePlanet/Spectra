# Quickstart: Verifying Spec 047 (Resilient Criteria Extraction)

A short developer-facing walkthrough for confirming the spec's behaviour locally without an AI provider plugged in (unit tests only) and end-to-end against a real corpus.

## Prerequisites

- .NET 8 SDK
- Repo cloned, on branch `047-resilient-criteria-extraction`

## 1. Build and run the unit/integration tests

```bash
dotnet build
dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~CriteriaExtractor|FullyQualifiedName~CriteriaExtraction|FullyQualifiedName~DocsIndexCriteriaExtraction"
```

Expected (all should pass after implementation):

- `CriteriaExtractor` parse-classification tests cover the seven mapping rows in `contracts/criteria-extractor.md`.
- `AnalyzeHandler` caller tests prove:
  - `ParseFailure` after the retry budget → no hash recorded, doc in `failed_documents`, AI called twice.
  - `Extracted([])` → hash recorded, doc skipped on second run.
  - `EmptyResponse` → `ParseFailure` → first attempt retried, second-attempt success cached.
- `DocsIndexHandler` tests prove:
  - A corpus where the cumulative time exceeds 60s but each individual doc completes within 2 min produces a result for every doc.
  - A single doc that hangs (test stub) times out alone and the rest succeed.

The retry helper uses an injectable delay provider (`IExtractionDelayProvider`) so tests do not have to wait 1.5s per retry. Default real-world wait is 1.5s before attempt 2; no further delay since the budget is 2.

## 2. End-to-end sanity check (requires Copilot SDK provider)

Pick a project (or use `Spectra_Demo/test_app_documentation` from the memory pointer) and:

```powershell
# 2a. Reproduce the bug on current main (pre-fix) — skip if on branch with fix applied.
spectra ai analyze --extract-criteria
# Observe: 'documents_failed' count is low even on a large corpus, but next run's 'documents_skipped'
# is suspiciously high, and some criteria.yaml files are empty.

# 2b. With the fix applied, on a large corpus:
spectra docs index --no-interaction --output-format json
# Expect: 'CriteriaExtracted' is non-zero on corpora that previously hit the 60s cliff.

spectra ai analyze --extract-criteria --output-format json
# Expect: 'documents_failed' now includes inconclusive docs; 'failed_documents' lists their paths.
# Expect stderr lines: "Extraction inconclusive for <path> (ParseFailure); will retry on next run."

# 2c. Re-run without --force: failed docs from the prior run should be re-attempted, not skipped.
spectra ai analyze --extract-criteria --output-format json
# Expect: the docs in the prior 'failed_documents' list are no longer in 'documents_skipped'.
```

## 3. Diagnostic checks

- `cat .spectra/logs/ai.log` (or the configured log file) should show `CRITERIA OK` lines for successful extractions and an exception trace for any parse-class failure that previously was silently swallowed.
- `_criteria_index.yaml` should NOT contain a source record for any doc you observed in `failed_documents` for the same run.

## 4. Smoke test for non-changes

- `spectra ai analyze --extract-criteria --force` still re-extracts everything (cache bypass unchanged).
- `spectra ai analyze --import-criteria some.csv` still works (the `SplitAndNormalizeAsync` path is untouched).
- `_criteria_index.yaml` schema is unchanged (open it; it should round-trip without diff outside of changed records).
- Existing tests in `tests/Spectra.CLI.Tests/Commands/AnalyzeHandlerDocumentSkipTests.cs` and `tests/Spectra.CLI.Tests/Commands/DocsIndexCommandTests.cs` still pass.

## 5. Rollback escape hatch

If a real-world run produces too many "inconclusive" reports, the user's workaround is unchanged: `spectra ai analyze --extract-criteria --force` re-extracts every document regardless of the cache. The `--force` semantics are not modified by this spec.
