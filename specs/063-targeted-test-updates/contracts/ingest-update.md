# CLI Contract: `spectra ai ingest-update`

Fail-loud boundary. Validates the model's edited test, deterministically protects invariants, runs the drift guard, and persists via `TestPersistenceService` — **without allocating a new id**. Mirrors `ingest-tests` (`IngestTestsCommand` + `GeneratedTestIngestor`).

## Synopsis

```
spectra ai ingest-update <suite> --test-id <id> [--from <file>] [--output-format json|human]
```

## Arguments & options

| Name | Required | Description |
|------|----------|-------------|
| `suite` (positional) | yes | Suite the edited test belongs to. |
| `--test-id` | yes | Id of the original test being edited (the invariant id; the model's id is ignored). |
| `--from` | no | File containing the model's edited-test JSON. Omit to read stdin. |
| `--output-format` | no | `human` (default) or `json`. |

## Behavior

1. Load `spectra.config.json` → else error (exit 1).
2. Read edited content (`--from` file or stdin).
3. Load the original test (`test-cases/<suite>/<id>.md`); if absent → error (exit 1).
4. `ParseAndValidate(content)` — reuse the generation parse pipeline + `TestValidator`. Fail-loud on `EMPTY_CONTENT` / `MALFORMED_JSON` / `TRUNCATED` / `NO_TESTS` (exit 5) or `SCHEMA_INVALID` (exit 6); nothing persisted.
5. **Invariant protection** (deterministic, model never trusted):
   - id ← original id; file path ← original file path.
   - If original `Grounding.Verdict == Manual` (and/or human note present) → re-assert that grounding/note onto the candidate.
6. **Drift guard**: compare candidate vs original. If any protected/out-of-scope field changed → fail-loud `DRIFT_DETECTED` (exit 5); nothing persisted; the drift entries are reported.
7. **Persist**: through `TestPersistenceService.PersistAsync` with the suite's full set (original replaced by edited); regenerates `_index.json`.
8. Report success (persisted id).

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Edited test validated, invariants protected, no drift → persisted (id unchanged). |
| `5` | Content invalid (`EMPTY_CONTENT`/`MALFORMED_JSON`/`TRUNCATED`/`NO_TESTS`/`DRIFT_DETECTED`) — nothing persisted. |
| `6` | Schema invalid — nothing persisted. |
| `1` | Error — config/I/O, or original test not found. |

## Result payloads (`--output-format json`)

Success:
```json
{ "success": true, "persisted": 1, "id": "TC-104" }
```

Fail-loud (drift):
```json
{
  "success": false,
  "error_code": "DRIFT_DETECTED",
  "errors": [
    "priority: original 'high' -> edited 'low' (out-of-scope change not implicated by the doc update)"
  ]
}
```

## Guarantees

- **Id never reallocated**: edit, not create — no `PersistentTestIdAllocator` call.
- **Manual fields preserved** regardless of model output.
- **No silent drift**: any out-of-scope field change fails loud, nothing persisted.
- **No partial writes**: on any failure, `test-cases/` and every `_index.json` are byte-for-byte unchanged.
- **Index parity**: success always regenerates the suite `_index.json` (FR-008) via the single persist path.
