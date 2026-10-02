# CLI Contract: Spec 084

All three new commands are model-free. They take the global `--output-format json|human` and `--verbosity`.

- **Exit codes:** 0 success; 1 environment error; 4 refusal.
- **Failures:** one JSON document on stderr in JSON mode, `{command, success:false, error_code, errors[], warnings[], …}`.
  In human mode, a plain message ending `[CODE]`.
- **Human summary:** always printed; on stderr in JSON mode, on stdout otherwise.

## `spectra ai update-triage (--suite <s> | --all-suites) [--no-auto-ack] [--dry-run]`

**Writes** (not on `--dry-run`):
- the baseline files and snapshots, for COSMETIC auto-acks, unless `--no-auto-ack` is given;
- `.spectra/update-triage.json`.

**Stdout (JSON mode)**, one compact line:

```json
{"command":"update-triage","success":true,"scope":{"suite":"storage"},"dry_run":false,
 "suites":[{"suite":"storage","tests":42,
   "counts":{"removed":1,"criteria_removed":0,"load":3,"criteria_changed":1,"light":2,"cosmetic":4,"unchanged":28,
             "unbaselined":3,"partial":1}}],
 "groups":[{"id":"G1","suite":"storage","bucket":"REMOVED","refs":[{"ref":"docs/storage.md#Legacy","state":"REMOVED"}],
            "criteria":[],"tests":["TC-140"],"why":["anchor not found"]},
           {"id":"G2","suite":"storage","bucket":"LOAD","refs":[{"ref":"docs/storage.md#Quotas","state":"LOAD"}],
            "criteria":[],"tests":["TC-101","TC-102","TC-117"],"why":["numbers: 10gb→50gb"]}],
 "removed":[{"suite":"storage","test":"TC-140","ref":"docs/storage.md#Legacy","reason":"anchor not found"}],
 "unbaselined":[{"suite":"storage","full":3,"partial":1}],
 "no_tests":[{"doc":"docs/storage.md","heading":"Archive tier"}],
 "notes":["docs/storage.md#Limits: 2 headings share the anchor key 'limits'; the first was used"],
 "auto_acked":4,"detail_file":".spectra/update-triage.json","warnings":[]}
```

- Implementation note (SC-004): `counts` lists only non-zero buckets (plus `unbaselined` / `partial` when non-zero);
  a missing bucket is 0. A group's `refs` / `criteria` appear only when non-empty. The example above shows the full
  shape for readability.
- `bucket` values: `REMOVED`, `CRITERIA_REMOVED`, `LOAD`, `CRITERIA_CHANGED`, `LIGHT`.
- Ref `state` values: `REMOVED`, `LOAD`, `LIGHT`, and `COSMETIC` (only with `--no-auto-ack` or `--dry-run`).
- Criterion `state` values: `CRITERIA_CHANGED`, `CRITERIA_REMOVED`.
- With `--no-auto-ack` or `--dry-run`, COSMETIC tests are counted but not grouped. `auto_acked` counts the refs that
  were refreshed; it is 0 on `--dry-run`, which reports `would_auto_ack` instead.
- Exit 0 whenever the triage completes, whatever it finds.

**Human summary**

```
Update triage — storage (42 tests)
  REMOVED 1 · CRITERIA_REMOVED 0 · LOAD 3 · CRITERIA_CHANGED 1 · LIGHT 2 · COSMETIC 4 (auto-acked) · UNCHANGED 28
  UNBASELINED 3 (1 partial) — run 'spectra ai source-baseline --init --suite storage'
  G1 REMOVED  1 test   docs/storage.md#Legacy   anchor not found
  G2 LOAD     3 tests  docs/storage.md#Quotas   numbers: 10gb→50gb
  …
  Not referenced by any test (changed docs): docs/storage.md › Archive tier
```

## `spectra ai update-ack --suite <s> (--ids a,b | --group <gid>) [--dry-run]`

Refreshes the named tests' entries (refs and criteria) to current content and writes the snapshots. It never writes
a test file.

```json
{"command":"update-ack","success":true,"suite":"storage","dry_run":false,"tests":["TC-101","TC-102"],
 "baseline":{"created":0,"updated":2,"unchanged":0,"snapshots_written":1},"warnings":[]}
```

## `spectra ai source-baseline --init (--suite <s> | --all-suites) [--force] [--dry-run]`

```json
{"command":"source-baseline","success":true,"scope":{"all_suites":true},"dry_run":false,"force":false,
 "suites":[{"suite":"storage","created":39,"completed":1,"unchanged":2,"recomputed":0}],
 "snapshots_written":57,"warnings":[]}
```

- `completed` counts partial entries that gained missing keys.
- `recomputed` counts entries rewritten under `--force`.

## Changes to existing commands

| Command | Change |
|---|---|
| `ai ingest-tests` | It takes `.spectra/baseline.lock` and loads the suite baseline strictly **before** persisting, then records entries for the persisted tests. Success JSON gains `"baseline":{created,updated,unchanged,snapshots_written}`, and the human line gains `Baseline: …`. New failures (exit 1, nothing persisted): `LOCK_TIMEOUT`, `BASELINE_UNREADABLE`. If the save fails after the tests are persisted: exit 1, the failure document with `persisted:[ids]`, naming the recovery command. Baseline failures use the shared failure shape; the existing content and schema failures (exit 5/6) keep their current `{success, error_code, errors}` shape, unchanged by this phase. |
| `ai ingest-update` (single and `--all`) | The same, under one lock for the batch. The entry for each test it writes is replaced. |
| `init` | `.gitignore` SPECTRA section `+ .spectra/baseline.lock`, `+ .spectra/update-triage.json`. |
| `ai update` | Unchanged. Documented as legacy, with `update-triage` as the replacement. |

## Error codes

| Code | Exit | Commands | When |
|---|---|---|---|
| `CONFIG_MISSING` | 1 | triage, ack, baseline | No `spectra.config.json` |
| `CONFIG_INVALID` | 1 | all three | Malformed config, case-variant keys, `update.group_size` < 1 or `update.light_threshold_words` < 0, or a non-integer value |
| `BASELINE_UNREADABLE` | 1 | triage, ack, baseline, ingest-tests, ingest-update | A baseline file that is invalid JSON or the wrong shape (the message names the file) |
| `LOCK_TIMEOUT` | 1 | every writer | `.spectra/baseline.lock` is held for more than 10 s |
| `BASELINE_WRITE_FAILED` | 1 | ingest-tests, ingest-update | The tests were persisted but writing their baseline or snapshots failed (I/O); the document carries `persisted:[ids]` and names the recovery command |
| `INVALID_ARGUMENTS` | 4 | all three | Both or neither of `--suite`/`--all-suites`; both or neither of `--ids`/`--group`; `source-baseline` without `--init` |
| `SUITE_NOT_FOUND` | 4 | all three | `--suite` names no suite folder |
| `TEST_NOT_FOUND` | 4 | ack | An `--ids` entry is not a test in the suite |
| `TRIAGE_MISSING` | 4 | ack `--group` | No detail file, or it doesn't cover the suite |
| `GROUP_NOT_FOUND` | 4 | ack `--group` | The group ID is not in the detail file for that suite |
| `TRIAGE_STALE` | 4 | ack `--group` | A member's current fingerprints differ from what triage recorded |
| `REF_REMOVED` | 4 | ack | A named test, or a group member, has a ref in the REMOVED state (names the test and refs) |
| `CRITERION_REMOVED` | 4 | ack | A named test, or a group member, has a criterion in the CRITERIA_REMOVED state |

## Config

```json
"update": { "group_size": 10, "light_threshold_words": 12 }
```

Both keys are optional. A missing or `null` `update` section or key → the defaults, silently. They are not in the
init template.
