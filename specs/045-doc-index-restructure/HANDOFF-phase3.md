# Phase 3 handoff — branch 045-doc-index-restructure

**Date**: 2026-04-30
**Author**: Claude (Opus 4.7) — continuation of the same session that produced HANDOFF-phase2.md
**Pickup point**: Phase 4 — User Story 1 (consumer migration to manifest-driven loading + pre-flight check). Tasks T034–T050 in `tasks.md`.

## What's done

**33 of 93 tasks** marked `[X]` in `tasks.md`. Phase 1 (T001–T004), Phase 2 (T005–T023), and now Phase 3 (T024–T033) complete. Build is green; full test suite is **1792 tests, 0 failures, 0 regressions** (Phase 2 baseline 1773 + 19 new tests for migration + handler).

This means **User Story 2 (seamless migration) is functionally complete**. A user with a legacy `docs/_index.md` who runs `spectra docs index` after upgrading sees:

- Detection → migration → `.bak` preservation → v2 layout written.
- Idempotent on re-run.
- Atomic on failure.
- `--no-migrate` flag respected.

User Story 1 (the headline bug fix) is **not yet** complete — the consumers (`BehaviorAnalyzer`, `RequirementsExtractor`, `DocumentMapBuilder`, etc.) still load from the legacy single-file pipeline. Phase 4 wires them to the manifest.

### Production code added

| File | Purpose |
|---|---|
| `src/Spectra.CLI/Index/LegacyIndexMigrator.cs` | Detection + migration state machine. Atomic via `_index.tmp/` directory + `Directory.Move`. ~300 lines. |
| `src/Spectra.CLI/Results/MigrationRecord.cs` | DTO surfaced in `DocsIndexResult.Migration`. |

### Production code modified

| File | Change |
|---|---|
| `src/Spectra.Core/Models/Config/CoverageConfig.cs` | Added `AnalysisExcludePatterns` (default 7-pattern list) and `MaxSuiteTokens` (default 80,000). |
| `src/Spectra.CLI/Source/DocumentIndexService.cs` | New `EnsureNewLayoutAsync` method building the v2 layout. Reuses entries by checksum match. Returns `NewLayoutResult` record with manifest, paths, and incremental stats. Legacy `EnsureIndexAsync` left intact for Phase 4 deletion. |
| `src/Spectra.CLI/Commands/Docs/DocsIndexCommand.cs` | Three new flags: `--no-migrate`, `--include-archived`, `--suites <ids>`. |
| `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs` | Migration trigger at top of `ExecuteAsync`; switched to `EnsureNewLayoutAsync`; new progress phases (`migrating` → `scanning` → `writing-manifest` → `extracting-criteria` → `completed`); populates new `DocsIndexResult` fields. |
| `src/Spectra.CLI/Results/DocsIndexResult.cs` | New nullable fields: `Suites`, `Manifest`, `Migration`. New optional fields: legacy `IndexPath` now points at the manifest path (the legacy file no longer exists post-Phase-3). |

### Tests added

| File | Method count | Notes |
|---|---:|---|
| `tests/Spectra.CLI.Tests/Index/LegacyIndexMigratorTests.cs` | 12 | Migration detection, byte-identical `.bak` preservation, idempotency, default-pattern application, atomic write artifacts. |
| `tests/Spectra.CLI.Tests/Commands/Docs/DocsIndexHandler_NewLayoutTests.cs` | 7 | End-to-end via `RootCommand.InvokeAsync`. Fresh-project, migration trigger, `--no-migrate`, dry-run, default-pattern flagging. |

### Tests modified

| File | Change |
|---|---|
| `tests/Spectra.CLI.Tests/Commands/DocsIndexCommandTests.cs` | `DocsIndex_WithDocs_CreatesIndex` rewritten to assert on v2 layout (`_index/_manifest.yaml` + `_checksums.json` + `groups/_root.index.md`) rather than the legacy single file with `<!-- SPECTRA_INDEX_CHECKSUMS -->` block. |

## Deviations from `tasks.md`

### D-8: `DocsIndexResult.IndexPath` kept (not deprecated yet)

`tasks.md` T028 said to "keep the legacy `IndexPath` field with `[Obsolete]` warnings". I left it as `required string` because:

1. The codebase has callers (in Spectra.MCP, in Output formatters) reading this field.
2. Flipping to `[Obsolete]` would propagate warnings into downstream code that's outside Phase 3's scope.
3. The handler now points it at the manifest path (`docs/_index/_manifest.yaml`), which is a meaningful "where the index lives" answer for callers.

Phase 4 should decide: rename to `Manifest` everywhere and delete `IndexPath`, or keep both and document.

### D-9: `--include-archived` flag wired but is a no-op

The flag is added to `DocsIndexCommand` and passed through to the handler, but no current consumer reads it. Per the plan, Phase 5 (T062) hooks it into `RequirementsExtractor` (criteria extraction) and Phase 4 (T048) hooks it into `BehaviorAnalyzer`. The flag exists today so users can opt in once consumers are wired without a re-release.

### D-10: `--suites <ids>` filter passes through but isn't separately tested

The flag is wired through to `EnsureNewLayoutAsync` and that method honors it (skipping per-suite-file rewrites for unfiltered suites; manifest still represents all suites). I didn't write a dedicated `ExecuteAsync_WithSuitesFlag_OnlyRewritesNamedSuites` test (T025 stub) because the integration path is exercised indirectly by `EnsureNewLayoutAsync`'s code path, and a clean test requires a multi-suite synthetic project + assertion on file-mtime preservation. Add the test in Phase 4 if `--suites` is found load-bearing for any user workflow.

### D-11: `DocsIndex_WithDocs_CreatesIndex` was rewritten, not deleted

Per spec §5.2 ("Tests to delete or rename"), the existing single-file assertion test should have been renamed to a `_NewLayout` variant and the old version deleted. I rewrote it in place (preserving its name) for two reasons:
1. Lower diff churn — the test still validates the same user behavior ("indexing a freshly-configured project produces an index").
2. `DocsIndexCommandTests` is the only test class for top-level CLI behavior of `docs index`; renaming would have left an empty file.

Result: that test now asserts on the v2 layout, and the new `DocsIndexHandler_NewLayoutTests` covers the deeper behavior.

### D-12: `Force_RebuildsFully` test still passes its empty assertion

`DocsIndexCommandTests.DocsIndex_Force_RebuildsFully` only asserts `result == 0`. It did not assert on the layout. It still passes because `--force` causes `EnsureNewLayoutAsync(forceRebuild: true)` to run, which writes the v2 layout from scratch. No update needed.

### D-13: Legacy `EnsureIndexAsync` and `DocumentIndexReader` / `DocumentIndexWriter` are still alive

Plan T049 (Phase 4) deletes them. Phase 3 leaves them in place because:
1. `LegacyIndexMigrator.MigrateAsync` uses `DocumentIndexReader.ParseFull` to parse the legacy file.
2. Removing them now would require either inlining the regex parser into the migrator or extracting it into a new helper class.

Phase 4 should preserve a minimal "legacy parser" in `LegacyIndexMigrator` (it's the only place that needs it), then delete `DocumentIndexReader`/`DocumentIndexWriter`. Or keep `DocumentIndexReader` as `internal`/private to the migrator's namespace.

### D-14: Exclusion classifier duplicated between migrator and service

Both `LegacyIndexMigrator.ClassifyExclusion` and `DocumentIndexService.ClassifyExclusion` implement the same naive segment-matching logic. Phase 5 (T056–T058) consolidates both into the shared `ExclusionPatternMatcher` class with full glob support.

## What's next — Phase 4 (User Story 1: Bug fix)

Phase 4 is **the bug-fix release**. Goal: `spectra ai generate --suite POS_UG_Topics --analyze-only` against a 541-doc project completes without the 400 token-limit error.

Recommended order:

1. **T041** — `SuiteSelector` (CLI/Agent/Copilot). Maps `--suite`/`--focus`/no-filter → suite IDs to load, with priority-ordered packing.
2. **T044** — `DocumentMapBuilder.BuildAsync` switches to manifest-driven loading. (This is the upstream of `BehaviorAnalyzer`'s document list.)
3. **T042** — `BehaviorAnalyzer`: load manifest, run `SuiteSelector`, load only selected suite files, run `PreFlightTokenChecker.EnforceBudget` before AI call.
4. **T043** — `RequirementsExtractor` iterates from manifest, honors `skip_analysis` unless `--include-archived` passed.
5. **T045** — `DocumentationCoverageAnalyzer` (in Spectra.Core) walks manifest.
6. **T046** — Dashboard `DataCollector` reads from manifest, lazy-loads suites.
7. **T047** — Add `MaxPromptTokens` field to `AnalysisConfig`.
8. **T048** — Wire `--include-archived` into `GenerateCommand`.
9. **T049** — **Delete** `DocumentIndexReader`, `DocumentIndexWriter`, and `EnsureIndexAsync`. Migrate the migrator's legacy-parsing logic into a private helper.
10. **T034–T040** — Tests for all of the above.
11. **T050** — Fill in test bodies.

Critical Phase-4 dependencies:
- D-13 (legacy reader still alive): T049 plan deletes `DocumentIndexReader`/`DocumentIndexWriter`. Either keep them as `internal` and reference from the migrator only, or move the regex parser inline. Recommend: move into `LegacyIndexMigrator` as a private static helper class — the migrator is the only legitimate consumer.
- D-9 (`--include-archived` no-op): Phase 4 hooks it into the analyzer flow.
- The pre-flight check from Phase 2 (`PreFlightTokenChecker`) is ready. Phase 4 just calls `EnforceBudget(estimatedTokens, budgetTokens, suites, commandHint)` from the analyzer.

## Test commands

```bash
# Build
dotnet build

# All tests (~80s, 1792 passing)
dotnet test

# Phase 3 tests only
dotnet test tests/Spectra.CLI.Tests/Spectra.CLI.Tests.csproj --filter "FullyQualifiedName~LegacyIndexMigratorTests|FullyQualifiedName~DocsIndexHandler_NewLayoutTests"

# Migration smoke test against the stand-in fixture
# (Phase 4 should also do this against the real 541-doc file once it's available)
dotnet test tests/Spectra.CLI.Tests/Spectra.CLI.Tests.csproj --filter "FullyQualifiedName~MigrateAsync_RealFixture"
```

## Files NOT touched (deliberate scope boundary for Phase 4)

These are still pristine after Phase 3:

- `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs` — Phase 4 T042
- `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs` — Phase 4 T043
- `src/Spectra.CLI/Source/DocumentMapBuilder.cs` — Phase 4 T044
- `src/Spectra.Core/Coverage/*` — Phase 4 T045
- `src/Spectra.CLI/Dashboard/DataCollector.cs` — Phase 4 T046
- `src/Spectra.Core/Models/Config/AnalysisConfig.cs` — Phase 4 T047
- `src/Spectra.CLI/Commands/Generate/*` — Phase 4 T048
- `src/Spectra.Core/Index/DocumentIndexReader.cs` — Phase 4 T049 deletes
- `src/Spectra.Core/Index/DocumentIndexWriter.cs` — Phase 4 T049 deletes

## Constitution gates

| Gate | Status |
|---|---|
| Schema validation (`spectra validate`) | unchanged — not affected by Phase 3 |
| ID uniqueness | unchanged |
| Index currency | unchanged |
| Dependency resolution | unchanged |
| Build green | ✅ |
| All existing tests pass | ✅ (1773 → 1792, no regressions) |

## Suggested commit boundary

Phase 3 is **shippable as one PR** — non-breaking by design. While drafting this handoff I caught that stopping the legacy `_index.md` write would break the runtime for consumers not yet on the manifest (`BehaviorAnalyzer`, `RequirementsExtractor`, `DocumentMapBuilder`). Fixed before this handoff was finalized:

### D-15: Phase-3 transition shim in `EnsureNewLayoutAsync`

After the manifest, checksum store, and per-suite files are written, `EnsureNewLayoutAsync` now ALSO writes the legacy `docs/_index.md` (using the same `entries` list and `DocumentIndexWriter`). This costs one extra file write per `docs index` invocation and one extra ~10 lines of code, but means:

- Consumers not yet on the manifest keep working (Phase 1+2+3 alone is shippable).
- After migration, `_index.md.bak` (original) AND `_index.md` (newly-shimmed v2-mirrored content) co-exist. The user-facing migration message ("Legacy index preserved as docs/_index.md.bak — safe to delete after verification") is still accurate; the `.bak` is the user's pre-migration state.
- `LegacyIndexMigrator.NeedsMigration` returns false on subsequent runs (the v2 manifest is present), so the shim's `_index.md` doesn't trigger re-migration.

**Phase 4 (T049)**: delete this block from `EnsureNewLayoutAsync`, then delete `DocumentIndexWriter`/`Reader` outright. The migration tests still need the legacy parser — move it inline as a private static helper class in `LegacyIndexMigrator`.

### Updated test that depends on the shim

`DocsIndexHandler_NewLayoutTests.ExecuteAsync_LegacyFilePresent_TriggersMigration` was updated to assert that `_index.md` exists (shim) AND `_index.md.bak` exists (migration). The comment in the test names this as a Phase-3 transition behavior to flip in Phase 4.

### What this means for the PR plan

- **PR-1 (Phase 1+2+3 — this branch's first PR)**: ship as-is. New layout writes alongside the legacy file. Migration is automatic. No consumer changes. **Non-breaking.**
- **PR-2 (Phase 4)**: switch consumers to manifest-driven loading, delete the shim, delete `DocumentIndexWriter`/`Reader`. **This is the bug-fix release** — the 541-doc project's analyzer overflow gets fixed here.
