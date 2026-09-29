# Quickstart: verifying Spec 079

Run everything from a scratch git workspace. Use `spectra` as `dotnet run --no-build --project <repo>/src/Spectra.CLI --`.

## 1. A reworded criterion keeps its ID (US1, US3)

1. Create `docs/checkout.md` with three requirements.
2. Run `spectra ai compile-extraction-prompt --doc docs/checkout.md`, then write a response to
   `.spectra/criteria.json` (3 criteria, no `id`), then run
   `spectra ai ingest-criteria --doc docs/checkout.md --from .spectra/criteria.json --output-format json`.
   The IDs come out as `AC-CHECKOUT-001` to `AC-CHECKOUT-003`.
3. Re-compile the prompt. It now contains `Existing criteria:` with those three IDs, and compiling twice gives identical
   bytes.
4. Ingest a response that:
   - returns `AC-CHECKOUT-001` with reworded text
   - changes only `**MUST**` to `MUST` on 002, with no `id`
   - drops 003
   - adds one new criterion

   Expected changeset:
   - 001 modified (old and new text)
   - 002 unchanged (the text fallback matched)
   - 003 removed
   - `AC-CHECKOUT-004` added

   The human summary goes to stderr, and stdout is pure JSON.
5. Re-ingest the same response. The changeset is empty, and `git status` shows the criteria file unchanged.

## 2. IDs are never reused and are unique (US2)

1. Drop `AC-CHECKOUT-004`, then add a new criterion. It gets `AC-CHECKOUT-005`.
2. Delete `docs/criteria/**` from a clone of the index, then re-ingest. The committed `id_high_water_marks` keeps the next
   number above 005.
3. `docs/a/login.md` and `docs/b/login.md` get distinct IDs: `AC-LOGIN-001..` for one, the continuation for the other.
4. Two parallel ingests (`&`) finish with both index entries present and no shared ID.
5. A response containing `"id": "AC-LOGIN-001"` for `docs/checkout.md` exits 7 and writes nothing.

## 3. Per-doc storage and migration (US4)

1. In a workspace with base-name files (the pre-079 layout), run `spectra docs changed`. It warns that migration is
   needed, and `git status` is clean.
2. Run `spectra docs migrate-criteria --dry-run`, then `spectra docs migrate-criteria`. The files move to
   `docs/criteria/docs/...` and every ID is preserved. A second run reports `already_migrated: true`.
3. Ingest with `--doc docs\checkout.md`. The next `docs changed` reports it as unchanged.

## 4. Deleted and moved docs (US5)

1. Run `git mv docs/refunds.md docs/billing/refunds.md`, then `spectra docs changed`. It reports `moved` (from → to).
   `spectra docs apply-move --from docs/refunds.md --to docs/billing/refunds.md` keeps every ID, and no prompt is
   compiled.
2. Delete `docs/old.md`, then run `spectra docs changed`. It reports `deleted`.
   `spectra docs retire --doc docs/old.md --dry-run` lists every criterion as removed, with the tests that reference them.
   Without `--dry-run`, it removes the entry and the file, and coverage totals drop.
3. `spectra docs retire --doc docs/checkout.md` (a doc that exists) exits 4 with `DOC_STILL_EXISTS`.
