# Quickstart: verifying 099 by hand

Build once: `dotnet build --no-incremental` (expect 0 warnings from CLI and Core; Execution's ImageSharp restore
advisories are a separate task and also block `-c Release` today).

1. **Golden check**: `dotnet test tests/Spectra.CLI.Tests --filter FullyQualifiedName~GoldenOutputTests` → green.
   To re-capture (only on `c24d23e`, in a worktree): set `SPECTRA_GOLDEN_RECORD=1` and run the same filter.
2. **Counts**: `dotnet test tests/Spectra.Core.Tests --filter FullyQualifiedName~ParseCounterTests` and
   `dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~IngestParseCountTests|FullyQualifiedName~HeadingScanCountTests|FullyQualifiedName~LayoutModeTests|FullyQualifiedName~StagedJsonParseCountTests"` → green.
3. **Real workspace** (083 rule): `spectra init` in an empty folder, add a doc and a suite, run `ai ingest-tests`,
   `ai source-baseline --init --all-suites`, edit the doc, `ai update-triage --all-suites`,
   `ai compile-update-prompt --suite s --group G1`, `ai ingest-update s --all`, `ai audit-grounding --suite s`,
   `validate`, `dashboard --output ./site --dry-run`. Repeat with the `c24d23e` build on a copy of the same folder and
   diff the two folders and the captured stdout: no differences except the masked fields.
4. **Lossless writes**: hand-edit a test (comment, unknown key, CRLF), then `ai ingest-grounding`, `ai review-flagged`
   accept, `spectra delete` of a test it depends on, `ai relink-sources` → the hand edits survive.
5. **Warnings as errors**: add an unused variable to a CLI file → `dotnet build` fails; revert.
6. **Dead code**: `git grep -w <name> -- src` for each deleted row of research R5 → no hits.
7. **Test duration**: `dotnet test tests/Spectra.CLI.Tests --no-build --logger trx` on an idle machine; aggregate per
   class (`trx.py` in the Notes); compare with the `c24d23e` table in research R7.
