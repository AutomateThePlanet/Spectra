# Phase 4 partial handoff — branch 045-doc-index-restructure

**Date**: 2026-04-30
**Status**: **Bug fix is functionally complete.** Headline 204K-token overflow is now structurally prevented before any AI call. Phase 4 cleanup tasks (T043, T044, T045, T046, T049) remain.

## What's done

**38 of 93 tasks** marked `[X]`. Phase 4 partial: **5 of 17** Phase-4 tasks done (T034, T041, T042, T047, T048). Test count: **1809 passing, 0 failing, 0 regressions** (Phase 3 baseline 1792 + 17 new tests).

### The bug fix in one paragraph

When `spectra ai generate --suite POS_UG_Topics` runs:

1. `DocsIndexService.EnsureNewLayoutAsync` writes the v2 manifest before the analyzer runs.
2. After document load, `AnalyzerInputBuilder.BuildAsync` reads the manifest, runs `DocSuiteSelector` with the user's `--suite` arg, and filters the `IReadOnlyList<SourceDocument>` to only the docs whose path is in the selected suite's index file.
3. The builder calls `PreFlightTokenChecker.EnforceBudget` against the filtered list. If the estimated prompt would exceed `ai.analysis.max_prompt_tokens` (default 96,000), it throws `PreFlightBudgetExceededException` with an actionable message naming every selected suite + tokens + suggested narrowing flags.
4. `GenerateHandler` catches that exception, prints the message, and returns `ExitCodes.PreFlightBudget` (4). No raw 400 token-limit error from the model.
5. On the happy path: filtered documents flow into `BehaviorAnalyzer.AnalyzeAsync` exactly as before, but with the corpus narrowed to the user's intent.

### Production code added

| File | Purpose |
|---|---|
| `src/Spectra.CLI/Agent/Copilot/DocSuiteSelector.cs` | Maps `--suite`/`--focus`/no-filter → `DocSuiteEntry[]`. Renamed from `SuiteSelector` to avoid collision with `Spectra.CLI.Interactive.SuiteSelector`. |
| `src/Spectra.CLI/Agent/Copilot/AnalyzerInputBuilder.cs` | The bridge. Manifest read → suite select → doc filter → pre-flight enforce. |

### Production code modified

| File | Change |
|---|---|
| `src/Spectra.Core/Models/Config/AnalysisConfig.cs` | Added `MaxPromptTokens` (default 96,000). |
| `src/Spectra.CLI/Infrastructure/ExitCodes.cs` | Added `PreFlightBudget = 4`. |
| `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs` | Added `--include-archived` flag, threaded through to `GenerateHandler`. |
| `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | New `_includeArchived` field. Replaced both `EnsureIndexAsync` calls with `EnsureNewLayoutAsync` (so manifest exists before the analyzer reads it). Both document-load sites now run `AnalyzerInputBuilder.BuildAsync` for filter + pre-flight. `PreFlightBudgetExceededException` caught and returned as exit code 4. |

### Tests added

| File | Methods | What |
|---|---:|---|
| `tests/Spectra.CLI.Tests/Agent/Copilot/DocSuiteSelectorTests.cs` | 10 | Exact match, unknown filter, skip-archived, includeArchived, focus packing, empty manifest, ordering. |
| `tests/Spectra.CLI.Tests/Agent/Copilot/AnalyzerInputBuilderTests.cs` | 7 | Suite filter narrows docs, no-filter loads non-archived, includeArchived flag, **budget overflow throws actionable exception**, unknown-suite warns + falls back, manifest-absent passes through, EstimatePromptTokens math. |

## Deviations from `tasks.md`

### D-16: Renamed `SuiteSelector` → `DocSuiteSelector`

Naming collision with the existing `Spectra.CLI.Interactive.SuiteSelector` (interactive picker for test suites). Renamed both my class and its result type. The `tasks.md` references to "SuiteSelector" should be read as `DocSuiteSelector`.

### D-17: T042 deferred direct `BehaviorAnalyzer` modification

Original T042 plan said to modify `BehaviorAnalyzer` to load the manifest itself. I instead created `AnalyzerInputBuilder` upstream in `GenerateHandler`. Reasoning:

- `BehaviorAnalyzer` currently has no filesystem awareness — it takes `IReadOnlyList<SourceDocument>` and produces a JSON parse. Adding manifest-loading would couple the AI service to filesystem layout decisions.
- The bug fix needs filter+pre-flight before the analyzer is *called*, not inside the analyzer. The composition point is the handler.
- This keeps the analyzer test surface unchanged and isolates Phase-4 changes to a single new class + a single existing handler file.

Tests T035, T036, T037 (which all named `BehaviorAnalyzer_*Tests`) are functionally satisfied by `AnalyzerInputBuilderTests` since that's where the behavior they describe actually lives. Task entries for T035–T037 still show as `[ ]`; the next session should mark them `[X]` with a "subsumed by T034 deliverable" note, or write thin re-tests against `BehaviorAnalyzer` if the test names matter.

### D-18: T048 partial — `--include-archived` not yet wired into `RequirementsExtractor`

The flag is on `GenerateCommand` and reaches `BehaviorAnalyzer`'s input filter via `AnalyzerInputBuilder`. It does **not yet** affect `RequirementsExtractor` (which extracts acceptance criteria during `spectra docs index`). That wiring is part of T043 (still open).

## Phase 4 task status

| Task | Status | Notes |
|---|---|---|
| T034 SuiteSelectorTests | ✅ Done (renamed `DocSuiteSelectorTests`, +`AnalyzerInputBuilderTests`) |
| T035 BehaviorAnalyzer_ManifestDriven | ⚪ Subsumed by `AnalyzerInputBuilderTests` (D-17) |
| T036 BehaviorAnalyzer_PreFlightTokenLimit | ⚪ Subsumed by `AnalyzerInputBuilderTests.BuildAsync_BudgetExceeded_ThrowsActionableException` |
| T037 BehaviorAnalyzer_SuiteResolution | ⚪ Subsumed by `DocSuiteSelectorTests` |
| T038 RequirementsExtractor_ManifestDriven | ⏳ Pending — depends on T043 |
| T039 DocumentationCoverage_FromManifest | ⏳ Pending — depends on T045 |
| T040 DataCollector_LazySuiteLoad | ⏳ Pending — depends on T046 |
| T041 SuiteSelector | ✅ Done (as `DocSuiteSelector`) |
| T042 BehaviorAnalyzer rewire | ✅ Done (as `AnalyzerInputBuilder`) |
| T043 RequirementsExtractor manifest + skip | ⏳ Pending |
| T044 DocumentMapBuilder reads manifest | ⏳ Pending |
| T045 DocumentationCoverageAnalyzer walks manifest | ⏳ Pending |
| T046 DataCollector lazy-load | ⏳ Pending |
| T047 MaxPromptTokens config | ✅ Done |
| T048 --include-archived flag | 🟡 Partial (BehaviorAnalyzer yes, RequirementsExtractor no) |
| T049 Delete legacy reader/writer + shim | ⏳ Pending — risky; do last in Phase 4 |
| T050 Fill in test bodies | ⚪ Subsumed by tests delivered with T034 |

## What's next

The bug fix is shippable as-is — the cleanup tasks below are correctness improvements (consistency, future-spec readiness) but they don't block the bug-fix release.

**If continuing Phase 4 in next session, recommended order:**

1. **T043** — `RequirementsExtractor` reads from manifest, honors `skip_analysis`, accepts `includeArchived: bool`. Wire flag through to `DocsIndexHandler` and `AnalyzeHandler`. ~1-2 hours.
2. **T044** — `DocumentMapBuilder.BuildAsync` switches to manifest-driven loading. Verify all callers still work (search for `BuildAsync(`). ~1 hour.
3. **T045** — `DocumentationCoverageAnalyzer` walks manifest. Find current callers; one-method change. ~30 min.
4. **T046** — Dashboard `DataCollector` reads manifest for top-level stats. ~1 hour.
5. **T049** — **Last.** Delete `DocumentIndexReader`, `DocumentIndexWriter`, `EnsureIndexAsync`, and the transition shim in `EnsureNewLayoutAsync`. Move legacy parser inline as a private static helper inside `LegacyIndexMigrator`. Update any failing tests. ~1-2 hours.

**Total remaining Phase 4 work**: ~5-7 dev-hours.

## Constitution gates

| Gate | Status |
|---|---|
| Build green | ✅ |
| All tests pass (1809) | ✅ |
| Schema validation | unaffected |
| ID uniqueness | unaffected |
| Index currency | unaffected |
| Dependency resolution | unaffected |

## Suggested commit boundary

This is a coherent shippable unit on its own:

> `feat(045): pre-flight budget check + suite-aware analyzer input (Spec 040 Phase 4 bug fix)`
>
> Adds `DocSuiteSelector` and `AnalyzerInputBuilder` between document discovery and the behavior analyzer. The builder reads the v2 manifest, narrows the document list to the user's `--suite` filter, and runs the pre-flight token budget check before any AI call. Overflows now fail fast with an actionable error naming every candidate suite + tokens, rather than the raw `400 prompt token count exceeds the limit` from the model.
>
> New CLI flag: `--include-archived` (skip-analysis suites included in the selector).
> New config: `ai.analysis.max_prompt_tokens` (default 96,000).
> New exit code: `4` for budget violations.
>
> Tests: +17 methods (`DocSuiteSelectorTests`, `AnalyzerInputBuilderTests`), all passing. 0 regressions in the 1792-test baseline.
>
> Phase 4 cleanup (T043, T044, T045, T046, T049 — non-blocking) tracked in `HANDOFF-phase4-partial.md`.

## Files NOT touched (deliberate scope boundary)

These remain on the legacy single-file path. Phase-4 follow-up work targets each:

- `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs` — T043
- `src/Spectra.CLI/Source/DocumentMapBuilder.cs` — T044
- `src/Spectra.Core/Coverage/*` — T045 (find current walker)
- `src/Spectra.CLI/Dashboard/DataCollector.cs` — T046
- `src/Spectra.Core/Index/DocumentIndexReader.cs` — T049 (delete)
- `src/Spectra.Core/Index/DocumentIndexWriter.cs` — T049 (delete)
- The transition shim in `EnsureNewLayoutAsync` — T049 (remove)
