# CLI Contract Changes: Spec 091

Only additions and the refusals below; every other field, code and exit code keeps its 090 meaning.

## `spectra ai compile-critic-prompt`

- The fingerprint record gains `compiled_at` (UTC, ISO 8601). Still written only for compiled tests, never on a
  refusal; nothing is deleted or renamed; no verdict is written; no model is called.

## `spectra ai ingest-verdict`

- `--from .spectra/verdicts/critic-verdict-<id>.json`: also stale when the file was last written more than 2 s before
  the record's `compiled_at` → exit 4, shared failure shape:

```json
{"command":"ingest-verdict","success":false,"error_code":"VERDICT_STALE","errors":["The verdict for TC-101 is older than the last critic prompt for TC-101 (verdict written 2026-10-09T07:00:00Z, prompt compiled 2026-10-09T08:15:02Z); run the critic for TC-101 again."],"warnings":[]}
```

- `--suite S --all`: such verdicts are listed under `stale` / counted in `skipped_stale` (090 fields).
- A record without `compiled_at` (090) → fingerprint rule only. No record, or stdin → accepted.

## `spectra ai ingest-grounding`

New option:

| Option | Description |
|---|---|
| `--ids a,b` | With `--all`: consider only these tests. Required with `--all --repaired`. |

Refusals:

| Situation | Exit | Output |
|---|---|---|
| `--all --repaired` without (or with an empty) `--ids` | 4 | shared failure shape, `error_code: "IDS_REQUIRED"`, nothing read or written |
| `--ids` without `--all` | 1 | stderr `--ids is only valid with --all.`, nothing on stdout, nothing written |
| `--test` together with `--all` (was: `--all` won) | 1 | stderr `Use either --test <id> or --all, not both.`, nothing on stdout, nothing written |
| `--test`: verdict older than the last critic prompt | 4 | `VERDICT_STALE` (090 code, new message) |

Argument checks run in this order, before config, index or verdicts are read: `--test` with `--all`; `--ids` without
`--all`; neither `--test` nor `--all` (existing "Either --test <id> or --all must be specified."); `IDS_REQUIRED`.

Batch JSON (stdout), additions in bold:

```json
{"command":"ingest-grounding","mode":"batch","suite":"storage","written":1,"skipped_already_written":0,
 "skipped_partial_pre_repair":0,"errors":0,"skipped_manual":0,"manual":[],"skipped_stale":0,"stale":[],
 "skipped_hallucinated":1,"hallucinated":["TC-140"],"not_found":["TC-999"]}
```

- **`skipped_hallucinated`** / **`hallucinated`**: tests whose file exists and whose verdict is hallucinated — kept for
  review, not written, not an error.
- **`not_found`**: `--ids` entries not in the suite index (always present; `[]` without `--ids`). Doesn't change the
  exit code.
- `errors` keeps counting unreadable/empty verdict files and refused writes.

Human summary (stdout in human mode, stderr in JSON mode): the existing counts line, then e.g.

```
TC-140: hallucinated, waiting for review
TC-999: not found in suite 'storage'
```

## Skills

- `spectra-generate` Step 8b: `spectra ai ingest-grounding --suite {suite} --all --repaired --repair-attempts 1 --ids {ids} --output-format json` (ids = re-critiqued this round, minus dropped; skip when empty). `skipped_hallucinated` is not a failure.
- `spectra-update` Step 5a: `… --all --repaired --repair-attempts 1 --ids <id> …`.
- `spectra-review-flagged`: unchanged.
- `spectra-critic` agent step 1: the compile writes only the fingerprint record (with `compiled_at`), never the
  verdict, and calls no model.
