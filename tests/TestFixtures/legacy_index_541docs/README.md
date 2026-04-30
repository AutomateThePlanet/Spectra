# Fixture: legacy_index_541docs

**Purpose**: Drives `LegacyIndexMigratorTests` and `DocsIndexHandler_NewLayoutTests` (Phase 3 of branch 045).

**Spec**: see `specs/045-doc-index-restructure/spec.md` §5.1 and §5.3.

## Provenance

The canonical fixture is the **real `docs/_index.md` file from the user-reported large project** (~378 KB / 5,001 lines / 541 documents across 12 suites). That file is the load-bearing test asset for the migration logic — it exercises real-world parse edge cases (escaped pipes in section summaries, non-ASCII titles, deeply-nested paths, oversized checksum block, `RD_Topics/Old/` archived directory, etc.) that synthetic fixtures will not catch.

## Current state of this directory

The repo currently ships a **representative synthetic stand-in** (`_index.md`) — 12 suites, ~30 documents, structurally faithful to the real file (matching header format, per-doc entry shape, embedded `<!-- SPECTRA_INDEX_CHECKSUMS -->` block, and an `RD_Topics/Old/` archived subdirectory). It is sufficient to compile and shape Phase 3 tests against, but the **541-doc real file should be checked in here before Phase 3 lands** so the manual verification steps in `spec.md` §5.3 are reproducible.

## Replacing the stand-in

When you have the real file:

1. Drop it at `tests/TestFixtures/legacy_index_541docs/_index.md` (overwriting the stand-in).
2. Run `dotnet test --filter LegacyIndexMigratorTests`. Test counts in `LegacyIndexMigratorTests` are pinned to the real file's group counts (12 suites, `SM_GSG_Topics` = 145 docs, etc.) — they will fail against the stand-in. That failure is the signal that you're now exercising the real fixture.
3. Update the stand-in's expected counts in any tests that hard-code "12 suites" if the real file's count differs.

## Do not edit in place

Treat the file in this directory as **read-only**. Tests that need to mutate it must copy to `Path.GetTempPath()` first.
