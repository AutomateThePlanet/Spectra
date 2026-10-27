# CLI Contract: Re-verify After Update (Phase 4)

All JSON changes are additive. New refusals use the shared failure shape
`{command, success:false, error_code, errors, warnings}` on stderr.

## `spectra ai ingest-update`

- **Written edits** remove the test's critic grounding block in the same write and delete
  `.spectra/verdicts/critic-verdict-<id>.json` and `critic-input-<id>.json`. A Manual verdict is kept.
  - Single-form JSON: `"manual_verdict_kept": true|false`.
  - Batch JSON: `"manual_verdict_kept": ["TC-…"]`.
  - Human output: `TC-101: updated (manual verdict kept)`.
- **Failed staging files** (`--all`) are renamed to `failed-<id>.json` in the same folder (replacing an older one), and
  `results[].file` gives that path. `failed-*` is never read. A later written, unchanged or unchanged_pending outcome
  for the id deletes its `failed-<id>.json`. Exit 8 is unchanged.
- **`--dry-run`** (single and `--all`) reports the same outcomes and exit code as the real run, with `"dry_run": true`
  and a zero `baseline` summary. Nothing is written, recorded, deleted or renamed, and no baseline lock is taken.

```json
{"command":"ingest-update","mode":"batch","suite":"storage","dry_run":true,
 "written":1,"unchanged":0,"unchanged_pending":1,"skipped_no_original":0,"errors":1,
 "manual_verdict_kept":[],"results":[…],"baseline":{"created":0,"updated":0,"unchanged":0,"snapshots_written":0,"mode":"rebuild"},"warnings":[]}
```

## `spectra ai compile-critic-prompt`

For each test it compiles successfully (suite mode or legacy `--test <path>`), it writes
`.spectra/verdicts/critic-input-<id>.json` = `{"id","suite","file","fingerprint"}`. It writes nothing on a refusal.
Its stdout is unchanged.

## `spectra ai ingest-verdict`

| Form | Stale verdict | No record |
|---|---|---|
| `--from .spectra/verdicts/critic-verdict-<id>.json` | exit 4, `VERDICT_STALE` (names the test and both fingerprints) | accepted |
| `--from <other path>` / stdin | — | accepted |
| `--suite <s> --all` | not classified; `"skipped_stale": N, "stale": ["TC-…"]` | accepted |

## `spectra ai ingest-grounding`

| Form | Manual verdict on the test | Stale verdict |
|---|---|---|
| `--suite <s> --test <id>` | exit 4, `MANUAL_VERDICT`; nothing written | exit 4, `VERDICT_STALE`; nothing written |
| `--suite <s> --all` | `"skipped_manual": N, "manual": [...]` (checked before the already-written skip) | `"skipped_stale": N, "stale": [...]` |

## `spectra ai compile-repair-prompt` (FR-A6)

It accepts `partial` and `hallucinated` verdicts and refuses `grounded` (exit 4, the existing refusal shape).

## `spectra ai audit-grounding --suite <s>` (FR-A7)

Entries for tests with a verdict file are unchanged. Every indexed test without a verdict file is added:

| Test file | `verdict` | `grounding_written` | `action_needed` |
|---|---|---|---|
| critic grounding block | the block's verdict (and score) | `true` | as today for that verdict (`review` if flagged, else `none`) |
| Manual block | `manual` | `true` | `none` |
| no block | `none` | `false` | `critic` |

The summary gains `unverified` (entries with verdict `none`). The exit code is unchanged.

## `spectra delete <ids…> [--dry-run]`

- A dependent with a scalar `depends_on: <deleted id>` has the key dropped through the lossless writer. Nothing else in
  its file changes, except a comment line directly above `depends_on:`, which goes with the key.
- A dependent with a block or flow list is left unchanged and gets a warning:
  `TC-203: depends_on is a list (not supported) and names TC-101; not changed`.
- The deleted tests' baseline entries are removed under the baseline lock, along with their staging files (both
  folders, all three prefixes) and their verdict and record files. Snapshots are kept.
- JSON additions: `baseline_entries_removed`, `staging_files_removed`, `verdict_files_removed` (lists) and `warnings`.
  `--dry-run` fills them with what would happen.
- Exit codes are unchanged.

## `spectra ai update` (legacy)

Its `ToDelete` proposals are deleted through the same service. Its classifier, flags and output otherwise stay the same.

## `spectra update-skills`

- It adds only patterns never offered to this workspace (recorded in `.spectra/skills-manifest.json` →
  `gitignore_offered`). A workspace from before 090 counts the 089 list, minus `.spectra/updates/` and
  `.spectra/repairs/`, as offered.
- With no `.gitignore` it creates none and warns `no .gitignore; add: <patterns>`.
- It reports `Updated: .gitignore (added: <patterns>)`.
- **Presence check:** a pattern counts as present when some whole trimmed, non-comment line equals the pattern or the
  pattern without its trailing `/`. A `!` negation of the pattern counts as the user's choice: the pattern is not added,
  and is recorded as offered.
- **Append:** existing bytes are unchanged. One newline in the file's own style (LF or CRLF) is added first only if the
  file doesn't end with one.

## `spectra init`

It writes the full list (creating `.gitignore` when absent), appends missing patterns with the same byte-preserving
append, and records every pattern as offered.
