# Contract: `spectra ai record-drop`

**Spec**: 071 | **Phase**: 1

## Purpose

Appends a drop-trail entry to `.spectra/dropped-tests.json` before a hallucinated test is deleted. Must be called BEFORE `spectra delete` — the trail is written first, then the delete proceeds. Model-free and deterministic.

## Invocation

```
spectra ai record-drop --suite <suite> --test <id> --from <verdict-file> [--reason user_decided]
```

### Options

| Option | Description |
|--------|-------------|
| `--suite <suite>` | Suite name |
| `--test <id>` | Test ID being dropped (e.g., TC-138) |
| `--from <file>` | Per-test verdict JSON (`.spectra/verdicts/critic-verdict-{id}.json`) |
| `--reason <reason>` | Drop reason: `hallucinated` (default) or `user_decided` (review delete) |

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Trail entry appended |
| 1 | Error (suite/test not found, I/O error on trail file) |
| 5 | Verdict JSON missing or empty (when `--reason` = `hallucinated`) |
| 6 | Verdict JSON parse failure |

## Behaviour

1. Reads `test-cases/{suite}/_index.json` to get the test title.
2. For `--reason hallucinated` (default): reads verdict JSON, extracts the hallucinated `findings[]` entries for `contradicting_claim` and `doc_ref` (first hallucinated finding's `claim` and `evidence` / `source_refs`).
3. For `--reason user_decided`: `contradicting_claim`, `doc_ref`, `critic_model` are null.
4. Constructs a NDJSON line and appends to `.spectra/dropped-tests.json` (creates file if absent).
5. Append is atomic (write to file with line-buffered append).

## Trail entry format

One JSON object per line (NDJSON):

```json
{
  "id": "TC-138",
  "suite": "file-management",
  "title": "Verify 1 KB file size display",
  "drop_reason": "hallucinated",
  "contradicting_claim": "1 KB = 1000 bytes",
  "doc_ref": "docs/file-management/sizes.md",
  "critic_model": "claude-sonnet-4-6",
  "timestamp": "2026-06-19T10:02:00Z",
  "source": "critic"
}
```

## Output (JSON mode)

```json
{
  "success": true,
  "id": "TC-138",
  "suite": "file-management",
  "trail_file": ".spectra/dropped-tests.json",
  "entries_total": 3
}
```

## Invariants

- `record-drop` NEVER deletes anything. It only appends to the trail.
- The caller (`spectra-generate.md` skill or `ReviewFlaggedHandler`) is responsible for calling `spectra delete` after `record-drop` succeeds.
- If `record-drop` fails (e.g., I/O error), the skill should NOT proceed to delete — the trail write is a prerequisite.
