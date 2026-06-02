# Implementation Plan: Resilient Criteria Extraction

**Branch**: `047-resilient-criteria-extraction` | **Date**: 2026-05-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/047-resilient-criteria-extraction/spec.md`

## Summary

Fix two confirmed defects in acceptance-criteria extraction that surface only at scale.

**Defect 1 (cache poisoning)**: `CriteriaExtractor.ParseResponse` collapses five structurally different outcomes into an empty list. The caller cannot tell a real "no criteria found" from a parse failure or an empty AI response, so it caches the document hash uniformly and the document is silently skipped on every subsequent non-`--force` run. Fix: replace the bare `IReadOnlyList<AcceptanceCriterion>` return with a typed result `CriteriaExtractionResult(ExtractionOutcome, IReadOnlyList<AcceptanceCriterion>)` carrying one of three outcomes (`Extracted`, `EmptyResponse`, `ParseFailure`); the caller writes a hash only on `Extracted` and retries the non-cacheable outcomes up to 2 attempts with a short backoff. The catch-all `catch { return []; }` becomes `catch (Exception ex) { ErrorLogger.Write(...); return ParseFailure; }` — the reason is logged, not swallowed.

**Defect 2 (corpus-wide deadline on `docs index`)**: `DocsIndexHandler.TryExtractCriteriaAsync` wraps the entire batched `RequirementsExtractor.ExtractAsync` call in a single 60-second deadline that almost always fires on real-sized projects, aborting extraction wholesale with one warning line. Fix: add a per-document method on `RequirementsExtractor` and iterate documents in `DocsIndexHandler` with a 2-minute deadline per document (matching the analyze-path policy). One slow document no longer kills the whole run.

Both fixes are scoped: no token-budget enforcement, no truncation, no merging of the two extractor classes — all deferred to a future spec, as called out in the input.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: GitHub Copilot SDK (existing — extraction call is unchanged), System.Text.Json (existing — `JsonSerializer.Deserialize` in `ParseResponse`), xUnit (test framework — existing)
**Storage**: File-based YAML index (`_criteria_index.yaml`) and per-doc `.criteria.yaml` files. **No persisted shape changes in this spec** — only changes are *when* the cache record is written.
**Testing**: xUnit in `tests/Spectra.CLI.Tests/`. New tests will live in `tests/Spectra.CLI.Tests/Commands/` and `tests/Spectra.CLI.Tests/Agent/Copilot/` (creating the `Agent/Copilot/` subfolder for the parser-classification tests). Retry-helper timing is exercised through an injectable `IExtractionDelayProvider` seam to avoid wall-clock waits.
**Target Platform**: Cross-platform CLI (`win-x64`, `linux-x64`, `osx-x64` per existing build targets).
**Project Type**: CLI tool — single project layout under `src/` and `tests/`.
**Performance Goals**: Bounded — at most 2 AI calls per document per run via this code path (was 1 with a silent-cache hazard). On the `docs index` path, the change converts a 60-second hard cap on the whole corpus into N × ≤2 min per-document caps; total time grows with corpus size but is no longer artificially truncated.
**Constraints**: Must preserve the existing CLI result-JSON shape and exit-code convention for `spectra ai analyze --extract-criteria` and `spectra docs index`; CI consumers depend on `documents_failed`, `failed_documents`, and exit codes. Must not change the on-disk YAML schemas.
**Scale/Scope**: Three modified source files, one new internal enum + one new internal record + one new internal interface; one new method on `RequirementsExtractor`; ≈14 new test cases. Net diff is small (low hundreds of lines) but covers two distinct command paths.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from `.specify/memory/constitution.md` (v1.1.0). Re-evaluated after Phase 1 design — no new violations introduced.

| Principle | Status | Notes |
|---|---|---|
| I. GitHub as Source of Truth | ✅ Pass | No new external storage; existing file-based cache shape unchanged. |
| II. Deterministic Execution | ✅ Pass | Retry is bounded (max 2 attempts); thrown exceptions and timeouts are NOT subject to retry, preserving today's deterministic "log-and-continue" behaviour. The new typed result has three discrete states with explicit mapping rules. |
| III. Orchestrator-Agnostic Design | ✅ Pass | No MCP API change; no provider-chain change. The fix is internal to the CLI's extraction call site. |
| IV. CLI-First Interface | ✅ Pass | No new flags, no new commands, no UI. Existing `--force`, `--dry-run`, `--output-format json` semantics unchanged. Result-JSON shape unchanged. |
| V. Simplicity (YAGNI) | ✅ Pass | One bespoke record + one enum + one test-seam interface — no generic `Result<T,E>`, no configurable retry knob, no new logging surface. Re-uses existing `ErrorLogger` and existing `documents_failed`/`failed_documents` reporting. **Tension noted**: adding a per-doc method to `RequirementsExtractor` (D3) reduces sharing with `CriteriaExtractor`'s identical concern. The deeper unification is explicitly out of scope per the spec — accepted; logged in Complexity Tracking. |

**Quality Gates** (`spectra validate`): unchanged. No schema/ID/index/dependency/priority changes.

**Test-Required Discipline**: New code paths (typed result mapping, retry helper, per-doc deadline) are unit-testable with stubs. Test plan in `spec.md` enumerates 14 cases; Phase 2 (`/speckit.tasks`) will dispatch them.

## Project Structure

### Documentation (this feature)

```text
specs/047-resilient-criteria-extraction/
├── plan.md                       # This file
├── spec.md                       # Feature spec (already written)
├── research.md                   # Phase 0 — decisions D1–D8
├── data-model.md                 # Phase 1 — new in-process types
├── quickstart.md                 # Phase 1 — local validation walkthrough
├── contracts/
│   ├── criteria-extractor.md     # Public surface change on CriteriaExtractor
│   ├── analyze-handler.md        # Per-doc loop change in AnalyzeHandler
│   └── docs-index-handler.md     # Per-doc timeout change in DocsIndexHandler
├── checklists/
│   └── requirements.md           # Spec quality checklist (already written; all items pass)
└── tasks.md                      # Phase 2 — generated by /speckit.tasks (NOT this command)
```

### Source Code (repository root)

This is a CLI tool; the project uses the existing single-project layout under `src/` and `tests/`. Files touched and added by this spec:

```text
src/
├── Spectra.CLI/
│   ├── Agent/
│   │   └── Copilot/
│   │       ├── CriteriaExtractor.cs           # MODIFY — return CriteriaExtractionResult; map 5 sites
│   │       ├── CriteriaExtractionResult.cs    # ADD    — record + enum (new file; could co-locate but file-per-public-type is the convention here)
│   │       ├── RequirementsExtractor.cs       # MODIFY — add ExtractFromDocumentAsync(DocumentEntry, ...)
│   │       └── IExtractionDelayProvider.cs    # ADD    — internal test seam (default Task.Delay)
│   └── Commands/
│       ├── Analyze/
│       │   └── AnalyzeHandler.cs              # MODIFY — ExtractWithRetryAsync; hash-on-IsCacheable; failed-doc reporting
│       └── Docs/
│           └── DocsIndexHandler.cs            # MODIFY — drop 60s corpus deadline; per-doc 2-min deadline loop

tests/
└── Spectra.CLI.Tests/
    ├── Agent/
    │   └── Copilot/
    │       ├── CriteriaExtractorParseTests.cs   # ADD — Parse_* cases (5)
    │       └── CriteriaExtractorExtractTests.cs # ADD — Extract_* cases (2)
    └── Commands/
        ├── AnalyzeHandlerRetryTests.cs          # ADD — Caller_* + Retry_* cases (5)
        └── DocsIndexCriteriaTimeoutTests.cs     # ADD — DocsIndex_* cases (2)
```

**Structure Decision**: Single CLI project layout (matches the rest of the repo — `src/Spectra.CLI`, `src/Spectra.Core`, `src/Spectra.MCP`, `src/Spectra.GitHub`). No new project, no new top-level directory. The spec touches `Spectra.CLI` only; `Spectra.Core` is unchanged because the affected types (`CriteriaExtractor`, `RequirementsExtractor`, `AnalyzeHandler`, `DocsIndexHandler`) all live in `Spectra.CLI` per current layout.

## Complexity Tracking

> Filled because Constitution V (Simplicity / YAGNI) was noted with tension above.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Two extractor classes (`CriteriaExtractor` + `RequirementsExtractor`) gaining parallel per-doc methods instead of being unified | Spec input explicitly defers the merge to a future spec — the two classes have different output models (`AcceptanceCriterion` vs `RequirementDefinition`), different downstream writers, and the merge would touch the analyze and docs-index command pipelines simultaneously, expanding the blast radius beyond a bug-fix change | Unifying now would mean either (a) converting `RequirementsExtractor` output to `AcceptanceCriterion` and rewiring `RequirementsWriter` → `CriteriaFileWriter`+`CriteriaIndexWriter` on the `docs index` path, or (b) the inverse on the analyze path. Either is a real refactor with cross-command regression surface and is rejected for piggy-backing on a bug fix |
| Adding a dedicated `IExtractionDelayProvider` interface for retry-backoff | Test budget — without a seam, the 5+ retry-path tests each wait 1.5 s of real wall-clock time, adding ~10 s to the test suite and making CI flake-prone | The simpler alternative is `Func<TimeSpan, CancellationToken, Task>` — equivalent but loses an explicit name. Both are acceptable; chose the named interface for readability at the call site. Effectively zero added complexity (one-line interface, default static impl) |

No other deviations from the constitution. No new dependencies, no new MCP tools, no architecture changes.
