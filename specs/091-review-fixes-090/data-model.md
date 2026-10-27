# Data Model: Review Fixes for Spec 090

No test-file, index, baseline or config format changes.

## Fingerprint record (`.spectra/verdicts/critic-input-<id>.json`)

Written by `compile-critic-prompt` for each test it compiles (suite mode, or `--test <path>` with `suite: null`).

| Field | Type | Since | Notes |
|---|---|---|---|
| `id` | string | 090 | test id |
| `suite` | string \| null | 090 | null for the legacy `--test <path>` form |
| `file` | string | 090 | repo-relative test file, `/`-separated |
| `fingerprint` | string | 090 | `sha256:<hex>` of the test's content (grounding excluded) |
| `compiled_at` | string (ISO 8601, UTC) \| absent | **091** | when the record was written; absent in 090 records |

Example (091):

```json
{"id":"TC-101","suite":"storage","file":"test-cases/storage/TC-101.md","fingerprint":"sha256:5bd8…","compiled_at":"2026-10-09T08:15:02.1234567+00:00"}
```

## Freshness (per verdict being ingested)

Inputs: the record (may be absent), the test's current content, the path of the verdict file being ingested (may be
absent: stdin).

| Record | Fingerprint | Verdict file time vs `compiled_at` | Result | Message |
|---|---|---|---|---|
| none | — | — | `NoRecord` (accepted) | — |
| present | differs | — | `Stale` (`ContentChanged`) | 090: "given to different content (recorded …, now …)" |
| present, no `compiled_at` | equal | not compared | `Fresh` | — |
| present, `compiled_at` | equal | `mtime < compiled_at − 2 s` | `Stale` (`OlderThanPrompt`) | "older than the last critic prompt for <id> (verdict written …, prompt compiled …)" |
| present, `compiled_at` | equal | `mtime ≥ compiled_at − 2 s` | `Fresh` | — |
| present | equal | no verdict path (stdin) | `Fresh` | — |

Both stale reasons use `VERDICT_STALE` and the same batch lists (`stale`, `skipped_stale`).

## Grounding batch (`ingest-grounding --suite S --all`)

Per verdict file of the suite (only listed ids when `--ids` is given), in order:

1. test file missing → skipped silently (unchanged);
2. Manual block → `manual` (090);
3. any block → `skipped_already_written`;
4. stale (R1) → `stale` (090 + 091 reason);
5. unreadable or empty verdict file → `errors`;
6. parseable verdict `hallucinated` → **`hallucinated` / `skipped_hallucinated` (091)**;
7. `partial` without `--repaired` → `skipped_partial_pre_repair`;
8. write → `written`, or `errors` when refused (e.g. unparseable).

Result additions: `not_found` (listed ids not in the suite index), `skipped_hallucinated` (count), `hallucinated`
(ids). All other fields keep their names and meanings.

## Options

| Option | Valid with | Rule |
|---|---|---|
| `--ids a,b` | `--all` | trimmed, de-duplicated, case-insensitive; without `--all` → invalid combination (exit 1) |
| `--test` | not `--all` | `--test` together with `--all` → invalid combination (exit 1; was: `--all` won) |
| `--repaired` | `--test`, `--all` | with `--all`, requires a non-empty `--ids` → else `IDS_REQUIRED` (exit 4) |
