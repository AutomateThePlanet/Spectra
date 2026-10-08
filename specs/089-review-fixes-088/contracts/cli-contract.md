# CLI Contract: Review Fixes for Spec 088

All JSON changes are additive; existing fields keep their meaning.

## `spectra ai ingest-update <suite> --all [--keep-baseline]`

| Form | Reads | Never reads |
|---|---|---|
| `--all` | `.spectra/updates/<suite>/updated-<id>.json` | `.spectra/repairs/` |
| `--all --keep-baseline` | `.spectra/repairs/<suite>/repaired-<id>.json` | `.spectra/updates/` |

Per file:

| Outcome | Written | Baseline | Staging file |
|---|---|---|---|
| `written` | test + index | rebuilt (kept with `--keep-baseline`) | deleted |
| `unchanged` | nothing | rebuilt (nothing with `--keep-baseline`) | deleted |
| `unchanged_pending` | nothing | untouched | deleted |
| `failed` | nothing | — | kept |

- `unchanged_pending` happens only without `--keep-baseline`. It applies to a test whose baseline entry exists and
  whose current bucket is LOAD, CRITERIA_CHANGED, REMOVED, CRITERIA_REMOVED or RELINK, classified exactly as
  `update-triage` would.
- `failed` codes: `EMPTY_CONTENT`, `MALFORMED_JSON`, `TRUNCATED`, `NO_TESTS`, `SCHEMA_INVALID`, `DRIFT_DETECTED`,
  `MULTIPLE_TESTS` (new: the file holds more than one test object), `NO_ORIGINAL`, `UNREADABLE`, `ID_MISMATCH`,
  `PERSIST_FAILED`.

```json
{"command":"ingest-update","mode":"batch","suite":"storage",
 "written":1,"unchanged":1,"unchanged_pending":1,"skipped_no_original":0,"errors":1,
 "results":[
   {"test":"TC-101","file":".spectra/updates/storage/updated-TC-101.json","outcome":"written"},
   {"test":"TC-102","file":".spectra/updates/storage/updated-TC-102.json","outcome":"unchanged"},
   {"test":"TC-103","file":".spectra/updates/storage/updated-TC-103.json","outcome":"unchanged_pending",
    "bucket":"LOAD","why":["numbers: 30gb→45gb"]},
   {"test":"TC-104","file":".spectra/updates/storage/updated-TC-104.json","outcome":"failed",
    "error_code":"MULTIPLE_TESTS","errors":["The edit holds 3 test objects (TC-104, TC-105, TC-106); a staged file holds exactly one test."]}],
 "baseline":{…},"warnings":[]}
```

The human summary is printed in every mode: on stdout, or on stderr in JSON mode.

```
Batch update-ingest for suite 'storage': written=1 unchanged=1 unchanged_pending=1 skipped_no_original=0 errors=1
  TC-101: updated
  TC-102: unchanged (reviewed, no edit needed)
  TC-103: returned unchanged — LOAD change still pending (acknowledge with update-ack or edit again)
  TC-104: failed [MULTIPLE_TESTS] The edit holds 3 test objects (…) (kept .spectra/updates/storage/updated-TC-104.json)
```

**Exit codes (batch):**
- `0`: no file failed (`unchanged_pending` is not a failure).
- `8`: at least one file failed, including when every file failed. The JSON and the deletions are the same as for 0.
- `1`: a whole-command error (config, lock, `BASELINE_UNREADABLE`, `BASELINE_WRITE_FAILED`), with the shared failure
  shape on stderr. No staging file is deleted.

A missing folder gives the counts at 0, empty `results`, and exit 0.

## `spectra ai ingest-update <suite> --test-id <id> [--from <file>] [--keep-baseline]`

The success JSON gains `outcome`: `written`, `unchanged` or `unchanged_pending`.

```json
{"success":true,"outcome":"unchanged_pending","persisted":0,"unchanged":true,"id":"TC-103",
 "bucket":"LOAD","why":["numbers: 30gb→45gb"],"baseline":{…},"warnings":[]}
```

The human output is `Test 'TC-103' returned unchanged — LOAD change still pending (acknowledge with update-ack or
edit again).`

**Exit codes (single):**
- `0`: `written`, `unchanged` or `unchanged_pending`.
- `5`: a content error, including the new `MULTIPLE_TESTS` and `DRIFT_DETECTED`.
- `6`: `SCHEMA_INVALID`.
- `1`: an error. Nothing is persisted on a non-zero exit.

## `spectra ai compile-update-prompt`

A linked criterion with no baseline value renders as
`- AC-1 (not baselined — compare the test with the current text): <text>`. Everything else is as in 088.

## `spectra init` / `spectra update-skills`

Both apply the same idempotent gitignore list, which now includes `.spectra/updates/` and `.spectra/repairs/`.
- `update-skills` lists `.gitignore` under "Updated:" when it added entries.
- Plain `init` on an existing workspace still refuses (exit 1).
