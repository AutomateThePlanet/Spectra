# CLI Contract: Spec 088

## `spectra ai compile-update-prompt --suite <s> (--test-id <id> | --group <gid>)`

Read-only: writes nothing, takes no lock. Config: the 083 read rule (`source`, `tests`; a missing file means defaults).

**Output**: the prompt on stdout. Warnings (outdated custom template) on stderr in every mode.

**Prompt layout** (built-in template, in this order): instructions → `## SOURCE SECTIONS` (`source_diff`) →
`## ACCEPTANCE CRITERIA` (linked only) → `## TESTS TO REVIEW` (`### TEST <id>` blocks) → `## OUTPUT FORMAT`.

A section block:

```
#### S1 — docs/storage.md#Quotas  (tests: TC-101, TC-102)
State: changed (LOAD) — numbers: 30→45
Current section:
~~~~markdown
## Quotas

Each user gets 45GB of storage space.
~~~~
Previous version (normalised: lowercased, markup removed):
~~~~text
## quotas each user gets 30gb of storage space.
~~~~
What changed: removed "30gb"; added "45gb"
```

Other states:
- `State: unchanged` with the current section only.
- `State: not baselined — previous version unavailable` with the current section.
- `State: changed (LOAD) — no diff: snapshot missing`, with `Previous version: unavailable (no snapshot)`.
- `State: REMOVED — not found; no rename found at 90% similarity` (or `anchor not found`, the submodule reason, …), no
  text.
- `State: RELINK — renamed to docs/guides/storage.md`, no text.

Criteria: `- AC-STORAGE-001 (changed since baseline): <text>`, `- AC-STORAGE-002: <text>`, `- AC-STORAGE-009 (removed)`.

A test block:

````
### TEST TC-101
Sections: S1, S2
Criteria: AC-STORAGE-001
```json
{ …the test, as today… }
```
````

**Refusals** (one JSON document on stderr in the shared shape `{command, success:false, error_code, errors, warnings}`;
exit 4 unless noted):

| Code | When |
|---|---|
| `INVALID_ARGUMENTS` | No `--suite`; neither or both of `--test-id` / `--group` |
| `SUITE_NOT_FOUND` | No such suite folder |
| `TEST_NOT_FOUND` | `--test-id` names no test in the suite |
| `TRIAGE_MISSING` / `GROUP_NOT_FOUND` / `TRIAGE_STALE` | As `update-ack --group` |
| `NOTHING_TO_RECONCILE` | A test with no resolvable ref and no existing linked criterion (names the tests) |
| `TEMPLATE_OUTDATED` | `--group` with a custom `test-update` template lacking `{{tests}}` / `{{source_diff}}` (hint: `spectra prompts reset test-update`) |
| `PROMPT_TOO_LARGE` | Estimated tokens over `ai.analysis.max_prompt_tokens`; names both numbers; suggests `--test-id` or a smaller `update.group_size` |
| `CONFIG_INVALID` / `BASELINE_UNREADABLE` | exit 1 |

## `spectra ai ingest-update <suite> --all [--keep-baseline]`

Per staging file `.spectra/updates/<suite>/updated-<id>.json` (ordinal order):

| Outcome | Written | Baseline | Staging file |
|---|---|---|---|
| `written` | the test + index | rebuilt (kept with `--keep-baseline`) | deleted |
| `unchanged` | nothing | rebuilt from current content (none with `--keep-baseline`) | deleted |
| `failed` | nothing | — | kept |

`failed` codes: the ingest codes (`EMPTY`, `MALFORMED`, `TRUNCATED`, `NO_TESTS`, `SCHEMA_INVALID`, `DRIFT_DETECTED`),
`NO_ORIGINAL` (no test with that id), `ID_MISMATCH` (the staged test's id differs from the file name), `UNREADABLE`.

Staging files are deleted only after the batch persisted and recorded its baseline; on a baseline write failure none is
deleted.

```json
{"command":"ingest-update","mode":"batch","suite":"storage","written":1,"unchanged":1,"skipped_no_original":0,"errors":1,
 "results":[{"test":"TC-101","file":".spectra/updates/storage/updated-TC-101.json","outcome":"written"},
            {"test":"TC-102","file":".spectra/updates/storage/updated-TC-102.json","outcome":"unchanged"},
            {"test":"TC-103","file":".spectra/updates/storage/updated-TC-103.json","outcome":"failed","error_code":"MALFORMED","errors":["…"]}],
 "baseline":{…},"warnings":[]}
```

The human summary lists each test with its outcome, in every mode (stderr in JSON mode). Exit 0 for a completed batch
(failures are per file).

## `spectra ai ingest-update <suite> --test-id <id> [--from <file>] [--keep-baseline]`

An edit identical to the original in every editable field: nothing written, baseline rebuilt (unless
`--keep-baseline`), exit 0, JSON `{"success":true,"persisted":0,"unchanged":true,"id":"TC-101",…}`; human
`Test 'TC-101' needs no change; its baseline was refreshed.`
