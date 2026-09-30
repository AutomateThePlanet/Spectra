# Contract: `spectra docs changed`, `retire`, `apply-move`, `migrate-criteria` (Spec 079)

## `spectra docs changed` (read-only, exit 0)

The JSON output adds fields; existing fields are unchanged:

```json
{
  "command": "docs-changed",
  "status": "success",
  "changed": [
    { "path": "docs/a/login.md", "component": "login", "status": "new", "current_hash": "...", "indexed_hash": null },
    { "path": "docs/old/refunds.md", "component": "refunds", "status": "deleted", "current_hash": null,
      "indexed_hash": "...", "criteria_count": 6 },
    { "path": "docs/billing/refunds.md", "from": "docs/refunds.md", "component": "refunds", "status": "moved",
      "current_hash": "...", "indexed_hash": "..." }
  ],
  "unchanged_count": 12,
  "duplicate_ids": [{ "id": "AC-LOGIN-002", "files": ["docs/a/login.criteria.yaml", "docs/b/login.criteria.yaml"] }],
  "migration_needed": true,
  "warnings": ["Criteria files use the pre-2.6 layout; run `spectra docs migrate-criteria` (writes also migrate automatically)."]
}
```

- **Path comparison.** Both sides are normalised (repository-relative, `/`).
- **`deleted`.** Only `source_type: document` index entries qualify.
- **`moved`.** Requires exactly one new doc and exactly one deleted entry with the same hash. It replaces both entries.
- **Human output.** Each moved or deleted row prints as `[moved    ] to  (from: from)` or `[deleted  ] path  (6 criteria)`.
  Warnings print after the rows.

## `spectra docs retire --doc <path> [--dry-run]`

**Preconditions** (each refusal exits 4 with an `error_code`):
- The normalised path is a `source_type: document` index entry. Otherwise `NOT_INDEXED`.
- Its file is absent from disk. Otherwise `DOC_STILL_EXISTS`.

**Effect** (under the lock, after auto-migration when the layout is old):
- Remove the index entry and delete its criteria file.
- High-water marks are unchanged.

**Output** is the same shape as the ingest success: `doc`, `dry_run`, `written`, `changeset` (every criterion in `removed`,
with `referencing_tests` and `test_ids`), `migration`. The human summary follows the ingest rule.

## `spectra docs apply-move --from <old> --to <new> [--dry-run]`

**Preconditions** (each refusal exits 4):
- `from` is an indexed document source and absent from disk (`NOT_INDEXED` or `DOC_STILL_EXISTS`).
- `to` exists (`TARGET_MISSING`).
- `to` is not already indexed (`TARGET_INDEXED`).
- The hash of `to` equals the recorded `doc_hash` (`CONTENT_CHANGED`: re-extract instead).

**Effect.** Move the criteria file to the derived path of `to`. Set the index `source_doc` and `file`. Set `source_doc` on
every criterion and in the `# Extracted from` header. IDs and all other fields are unchanged, and no extraction runs.

**Output:** `{ success, from, to, criteria_count, dry_run, written, migration }`.

## `spectra docs migrate-criteria [--dry-run]`

**Effect** (under the lock):
- Normalise `source_doc`.
- Move each document source's file to its derived path, and rewrite `source_doc` inside it.
- Update the index `file`.
- Seed `id_high_water_marks`.
- Report collisions and leave them untouched.

**Output:**

```json
{ "success": true, "dry_run": false, "moved": [{ "doc": "...", "from_file": "login.criteria.yaml", "to_file": "docs/a/login.criteria.yaml" }],
  "normalized_source_docs": 1, "collisions": [{ "file": "login.criteria.yaml", "docs": ["docs/a/login.md", "docs/b/login.md"],
  "recommendation": "Re-extract both docs: spectra-criteria (full re-extract)" }],
  "high_water_marks_seeded": { "AC-LOGIN": 7 }, "already_migrated": false }
```

A second run reports `already_migrated: true` with empty lists, and changes no file. Exit 0; 1 on error.

## Auto-migration

`ai ingest-criteria`, `docs retire`, `docs apply-move` and `ai analyze --import-criteria` run the migration first, inside the
same lock, when the layout is old and the operation is not a dry run. They report it under `migration`. A dry run on the
old layout reports `migration_needed: true` and plans against the resolved (old) locations.

## Lock

All of the writers above take `.spectra/criteria.lock`. If the lock can't be acquired within 10 s, they exit 1 with
`LOCK_TIMEOUT`. `spectra init` adds the lock file to `.gitignore`.
