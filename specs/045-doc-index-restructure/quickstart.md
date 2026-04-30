# Quickstart — Document Index Restructure

**Branch**: `045-doc-index-restructure`
**Audience**: Engineer implementing this feature.

This is a fast onboarding for someone picking up the branch. Read `spec.md` for the user-facing feature, `plan.md` for the project-level plan, `research.md` for the design decisions, `data-model.md` for the C# shapes, and `contracts/` for the file/CLI formats. This document tells you how to build the smallest end-to-end slice and how to verify it.

## What you're building

A four-PR sequence that replaces a 5,000-line `docs/_index.md` with three artifacts:

```
docs/_index/
├── _manifest.yaml          ← always loaded into AI prompts (~2-5K tokens)
├── _checksums.json         ← never sent to AI; used for incremental detection
└── groups/
    ├── checkout.index.md   ← lazy-loaded; one per suite
    ├── payments.index.md
    └── ...
```

…and updates every consumer (`BehaviorAnalyzer`, `RequirementsExtractor`, `DocumentationCoverageAnalyzer`, dashboard `DataCollector`, `--from-description` context loader) to load the manifest first and only fetch suite files they actually need. Pre-flight token budget fails fast with an actionable error rather than letting the model overflow.

## Phase ordering (one PR per phase)

1. **Phase 1 (this PR)** — New layout reading/writing + auto-migration. Consumers still use the legacy reader.
2. **Phase 2** — Migrate consumers. Pre-flight check goes live. Legacy `DocumentIndexReader`/`Writer` deleted.
3. **Phase 3** — Exclusion patterns, frontmatter overrides, spillover.
4. **Phase 4** — SKILL/agent/dashboard polish + introspection commands.

Don't skip ahead. Phase 2 is the bug fix; Phase 1 is the safety net that lets Phase 2 land cleanly.

## Phase 1 minimum viable slice

1. Add models: `DocIndexManifest`, `DocSuiteEntry`, `ChecksumStore`, `SuiteIndexFile` in `src/Spectra.Core/Models/Index/`.
2. Add readers/writers: `DocIndexManifestReader`/`Writer` (YAML), `ChecksumStoreReader`/`Writer` (JSON), `SuiteIndexFileReader`/`Writer` (Markdown). All writers write-to-temp + atomic rename. Helper: `Spectra.Core/Index/AtomicFileWriter.cs`.
3. Add `SuiteResolver` in `src/Spectra.CLI/Source/`. Implements §3.5 + R-009 (single-doc-directory rollup).
4. Add `LegacyIndexMigrator` in `src/Spectra.CLI/Index/`. `NeedsMigration()` and `MigrateAsync()` per the data-model state machine.
5. Modify `DocsIndexHandler` to call the migrator first, then write manifest + per-suite + checksums via the new writers. Keep `DocumentIndexWriter` writing the legacy `_index.md` for now too — Phase 1 is non-breaking.
6. Modify `DocsIndexResult` to include the new fields (`suites[]`, `manifest`, `migration`).
7. Add tests:
   - `SuiteResolverTests` (~10) — frontmatter override, config override, directory default, root fallback, single-doc directories.
   - `DocIndexManifestRoundTripTests` (~6) — write/read parity.
   - `ChecksumStoreRoundTripTests` (~4).
   - `SuiteIndexFileRoundTripTests` (~3).
   - `LegacyIndexMigratorTests` (~9) — uses the real 541-doc fixture.
   - `DocsIndexHandler_NewLayoutTests` (~7) — full pipeline against a synthetic doc tree.

## Test fixture setup

Drop the user-reported file at `tests/TestFixtures/legacy_index_541docs/_index.md` (378 KB). Add a sibling `README.md` documenting provenance. The migration test reads it, runs `MigrateAsync`, and asserts:
- New layout exists at the expected path.
- Manifest lists 12 suites.
- `SM_GSG_Topics` index file has 145 entries.
- Checksum store has 541 entries.
- Legacy file renamed to `_index.md.bak`.

## Verifying the bug-fix end-to-end (Phase 2 milestone)

Once Phase 2 is in:

```bash
# Migrate a real 541-doc project
spectra docs index --output-format json | jq '.migration'
# {"performed": true, "suitesCreated": 12, ...}

# Run analyzer scoped to one suite
spectra ai generate --suite POS_UG_Topics --analyze-only --output-format json

# Verify analyzer prompt size (Phase 2 logs this)
grep "ANALYSIS START" .spectra/logs/*.log
# ANALYSIS START documents=89 tokens=~7400 budget=96000
```

The analyzer prompt should be well under the 128K context window — no more 400 token-limit errors.

## Verifying pre-flight budget violation (Phase 2)

```bash
# No --suite filter on a 541-doc project; should fail fast
spectra ai generate --no-interaction; echo "exit=$?"
# exit=2  (or whatever code we settle on)
```

Expected stderr names every suite, sorted by token cost, and suggests `--suite <id>`.

## Constitution checkpoints

Before each PR:
- **Schema validation** (`spectra validate`) still passes — the new layout adds artifacts, doesn't break existing ones.
- **Index currency** check is updated to also verify `_manifest.yaml` reflects on-disk groups.
- **Test counts** stay above existing baseline + new tests for the phase.

## Things to NOT do in Phase 1

- Don't delete `DocumentIndexReader` / `DocumentIndexWriter`. Phase 2 deletes them.
- Don't change `BehaviorAnalyzer`, `RequirementsExtractor`, or any consumer. Phase 2 changes them.
- Don't add `--include-archived`. Phase 3 adds it.
- Don't add introspection commands. Phase 4 adds them.

## Useful greps

```bash
grep -rn "_index.md" src/    # callers that will need Phase 2 updates
grep -rn "DocumentIndexReader" src/
grep -rn "DocumentIndexWriter" src/
grep -rn "DocumentIndex" src/Spectra.CLI/   # for dashboard/coverage/criteria callers
```

## When you finish a phase

Run from repo root:

```bash
dotnet build
dotnet test
spectra validate
```

…and verify the manual steps in `spec.md` §5.3 for the phase's done-when criteria.
