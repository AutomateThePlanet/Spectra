# Contract: `spectra ai ingest-criteria` (Spec 079)

```bash
spectra ai ingest-criteria --doc <path> [--component <c>] [--from <file>] [--dry-run] [--output-format json]
```

## Input: the model response

It is a JSON array. The fields per item are unchanged, except that `id` is new and optional:

```json
[
  { "id": "AC-CHECKOUT-003", "text": "...", "rfc2119": "MUST", "source_section": "Payment",
    "priority": "high", "tags": ["payment"], "technique_hint": "BVA" },
  { "text": "a new requirement ...", "rfc2119": "SHOULD", "source_section": "Refunds", "priority": "medium", "tags": [] }
]
```

`source_section` is now actually read (F4).

## Processing order (all under `.spectra/criteria.lock`)

1. Normalise `--doc`. It must be inside the repository, otherwise exit 1 with `INVALID_DOC_PATH`.
2. Classify the response. An empty response → exit 5; a parse failure → exit 6. Both are unchanged.
3. If the layout is old and this is not a dry run, auto-migrate, then report it under `migration`.
4. Load the previous criteria through the index `file` entry. An unreadable file → exit 1 with `CRITERIA_FILE_UNREADABLE`.
5. Validate explicit IDs. An unknown, foreign or duplicated ID → **exit 7** with `INVALID_CRITERION_ID` and the list of
   offending IDs. Nothing is written.
6. Match and allocate (data-model.md), then build the changeset and the referencing-test counts.
7. Unless this is a dry run, apply the no-op rule, write the criteria file and the index (with high-water marks), and
   release the lock.

## Success output

stdout contains exactly this JSON, in both output modes.

```json
{
  "success": true,
  "outcome": "Extracted",
  "doc": "docs/checkout.md",
  "persisted": 5,
  "ids": ["AC-CHECKOUT-001", "..."],
  "dry_run": false,
  "written": true,
  "changeset": {
    "added":    [{ "id": "AC-CHECKOUT-014", "text": "..." }],
    "modified": [{ "id": "AC-CHECKOUT-003", "old_text": "...", "new_text": "..." }],
    "removed":  [{ "id": "AC-CHECKOUT-007", "old_text": "...", "referencing_tests": 2, "test_ids": ["TC-101","TC-140"] }],
    "metadata": [{ "id": "AC-CHECKOUT-002", "field": "priority", "old": "medium", "new": "high" }],
    "is_empty": false
  },
  "duplicate_ids": [],
  "migration": null
}
```

- `written` is `false` on a dry run, or when the no-op rule wrote nothing.
- `duplicate_ids` lists workspace-wide duplicates as `[{ "id", "files": [...] }]`. They are reported, never renumbered.
- `migration` is `null`, or the migration summary when the ingest auto-migrated.

**Human summary.** In JSON mode it goes to **stderr**; in human mode it goes to stdout. It is never suppressed:

```text
Criteria changeset for docs/checkout.md
  + AC-CHECKOUT-014  a new requirement ...
  ~ AC-CHECKOUT-003  "old text" → "new text"
  - AC-CHECKOUT-007  "old text"  (still referenced by 2 tests: TC-101, TC-140)
  * AC-CHECKOUT-002  priority: medium → high
No changes.                                   (when is_empty)
```

## Failure output

This is unchanged from today: a machine-readable JSON object on stderr, `{ success:false, outcome, error_code?, errors[] }`,
with no human summary mixed in.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (including dry run and no-op) |
| 1 | Error: config, `--from` missing, `INVALID_DOC_PATH`, `CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION`, `LOCK_TIMEOUT` |
| 5 | Empty response |
| 6 | Parse failure |
| 7 | `INVALID_CRITERION_ID` |
