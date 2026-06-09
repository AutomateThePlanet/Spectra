# Contract: `spectra docs changed` (new, model-free — FR-005)

Surfaces the per-document SHA-256 incremental-skip state to the skill so unchanged docs are skipped
without a model turn. Pure compare; no model call, no writes.

## Invocation

```
spectra docs changed [--output-format json|human] [--include-unchanged] [--verbosity ...]
```

## Behavior

1. Enumerate source documents (same doc set `docs index` indexes — honor source config / exclusions).
2. For each doc: `current = FileHasher.ComputeFileHashAsync(doc)`; `indexed =` the matching
   `CriteriaSource.doc_hash` from `_criteria_index.yaml` (via `CriteriaIndexReader`), or `null` if no
   source entry exists.
3. Classify: `indexed == null` → `new`; `indexed != current` → `changed`; else `unchanged`.

## Output (JSON)

```json
{
  "command": "docs-changed",
  "status": "success",
  "changed": [
    { "path": "docs/checkout.md", "component": "checkout", "status": "new", "current_hash": "…", "indexed_hash": null },
    { "path": "docs/login.md", "component": "login", "status": "changed", "current_hash": "…", "indexed_hash": "…" }
  ],
  "unchanged_count": 5
}
```

- By default lists only `new|changed` (the skill's work-list); `--include-unchanged` adds the rest.
- `component` is derived the same way `CriteriaIngestor` derives it (filename slug) unless source config
  maps a component.

## Exit codes

- `0` success (even when nothing changed — empty `changed` list is success).
- `1` error (unreadable workspace / config).

## Determinism

No timestamps/GUIDs in output ordering; docs listed in a stable order (path-sorted). Identical inputs →
identical output.
