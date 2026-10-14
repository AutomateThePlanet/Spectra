# CLI Contracts: Verification Hardening (Phase 6, Part 2)

## `spectra ai audit-grounding --suite S [--action A]… [--ids a,b]` (changed)

- Lists only tests in S's index. A verdict file of an id not in the index is ignored.
- `--action`: `critic` | `repair` | `review` | `none`, repeatable and/or comma-separated, case-insensitive. Unknown →
  exit 1, plain stderr `--action must be one of critic, repair, review, none (got '<v>').`
- `--ids`: 091 parsing; empty after trimming = not given. Ids not in S → `not_found` (JSON) /
  `<id>: not found in suite '<S>'` (human; on stderr in JSON mode, where stdout stays the JSON document).
- A verdict-file entry whose verdict is stale (090/091 rule, 097 reference clock): `"stale": true`,
  `"action_needed": "critic"`. Human Action column: `critic (stale)`.
- `summary.stale` new; every summary count covers the whole suite. Exit 0 (also when the filter leaves nothing).

```json
{"command":"audit-grounding","status":"success","suite":"storage",
 "tests":[{"id":"TC-102","verdict":"partial","score":0.7,"grounding_written":false,"flagged_for_review":false,
           "action_needed":"critic","file":"test-cases/storage/TC-102.md","stale":true}],
 "summary":{"total":12,"grounding_written":9,"partial_pending_repair":1,"flagged_for_review":1,"unverified":1,"stale":1},
 "not_found":["TC-999"]}
```

## `spectra ai compile-repair-prompt --suite S --test T [--from F]` (changed)

Stale verdict → stderr `VERDICT_STALE: <message>` (the ingest message), exit 4, no prompt. No record → as today.

## `spectra ai compile-repair-batch --suite S` (changed)

Stdout: the JSON array of prompts, unchanged, without stale tests. When any test is stale, stderr gets
`{"command":"compile-repair-batch","stale":[{"id":"TC-102","reason":"…"}]}` and one human line per stale test.

## `spectra ai ingest-grounding --suite S (--test T | --all [--ids a,b] [--repaired --repair-attempts N]) [--dry-run]`

- `--ids` empty after trimming = not given; `--all --repaired` with no ids → `IDS_REQUIRED` (exit 4).
- `--all --dry-run`: the same JSON, human summary and exit code as a real run, plus `"dry_run": true`; no test file
  written.
- `--test T --dry-run` → exit 1, plain stderr `--dry-run requires --all`.

## `spectra ai ingest-verdict (--from F | --suite S --all [--ids a,b]) [--dry-run]`

`--ids` empty after trimming = not given. `--dry-run` accepted in both forms (the command writes nothing) and adds
`"dry_run": true`.

## `spectra ai ingest-analysis --suite S [--from F] [--focus …] [--dry-run]`

`--dry-run`: the same JSON, summary and exit code (incl. config refusals), plus `"dry_run": true`; no plan file written.

## Config `ai.critic.model` (new, optional)

Default `sonnet`. `^[A-Za-z0-9][A-Za-z0-9._:\[\]-]{0,199}$` (starts with a letter or digit). Invalid →
`CONFIG_INVALID` (`ai.critic.model must be one word (an alias such as sonnet, a model id, or inherit)`) in the commands
that require a valid config; `update-skills` only warns (below).

## `spectra init` / `spectra update-skills` (changed)

- The installed `spectra-critic` agent's frontmatter `model:` is the effective `ai.critic.model`.
- `init` writes `"ai": {"critic": {"model": "sonnet"}, …}` in the config template.
- `update-skills` loads the config only for the critic model:
  - missing → `sonnet`;
  - valid → the configured or default model;
  - invalid for any reason → one warning `spectra.config.json is invalid (<error_code>: <first error>); the critic
    agent's model was not changed`. The critic keeps its installed model line, everything else is refreshed, exit 0.

  An unedited critic agent is re-rendered when the model changed; an edited one is reported under
  "Skipped (customized by user)".

## Skills

- `spectra-generate` 8a: `spectra ai ingest-grounding --suite {suite} --all --ids {kept-grounded ids} --output-format json`
  — skipped when there are none. Resume: `spectra ai audit-grounding --suite {suite} --action repair --output-format json`.
- `spectra-update` 5a: `spectra ai ingest-grounding --suite <suite> --all --ids <grounded ids> --output-format json` —
  skipped when there are none.
- The 075 guard text and the NON-STOP contract: unchanged.
