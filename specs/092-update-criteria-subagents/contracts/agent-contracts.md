# Agent and Skill Contracts: Spec 092

No CLI contract changes. These are the contracts between the main session (the skills) and the bundled agents.

## spectra-updater

- **Invoked by**: spectra-update Step 5 (per group) and "edit again" (per id list), via Task.
- **Input**: `suite: <suite>` + `group: <G…>` | `ids: <id1>,<id2>`.
- **Does**:
  1. `spectra ai compile-update-prompt --suite <suite> --group <group>` (or `--test-id <id>` per id). Exit 4:
     `PROMPT_TOO_LARGE` → per test; `TEMPLATE_OUTDATED` → per test + `warning:` naming `spectra update-skills` /
     `spectra prompts reset test-update`; `NOTHING_TO_RECONCILE` → `warning:` and go on; `TRIAGE_STALE` → `stop:
     triage is stale, re-run update-triage`; other codes → `stop: <code>: <errors[0]>`.
  2. Edit only what the change requires; keep ids, structure, priority, component, tags, manual notes; a correct test
     goes back unchanged.
  3. Write one `.spectra/updates/<suite>/updated-<id>.json` per test (JSON array, one element).
  4. `spectra ai ingest-update <suite> --all --output-format json --verbosity quiet`; exit 0/8 → read `results`;
     exit 1 → `stop: ingest-update: <error_code>`.
  5. Each `failed` test: re-edit per `error_code`, write a fresh staging file, run the same ingest — at most twice.
- **Returns**: data-model.md "updater". Nothing else.
- **Never**: asks the user, acknowledges (`update-ack`), runs the critic, edits a `.md`, starts a subagent.

## spectra-repairer

- **Invoked by**: spectra-update Step 5a (and spectra-generate in Phase 5b), via Task.
- **Input**: `suite: <suite>`, `ids: <id1>,<id2>`.
- **Does**: per id `spectra ai compile-repair-prompt --suite <suite> --test <id>`, patch only what the critic flagged,
  Write `.spectra/repairs/<suite>/repaired-<id>.json`; then `spectra ai ingest-update <suite> --all --keep-baseline
  --output-format json --verbosity quiet`; each `failed` id re-patched per `error_code` and re-ingested, at most twice
  (the same repair attempt). A refused compile (exit 4) → `<id> | failed <error_code>: <errors[0]>`.
- **Returns**: data-model.md "repairer".
- **Never**: runs the critic, writes grounding, asks the user, starts a subagent, edits a `.md`.

## spectra-extractor

- **Invoked by**: spectra-criteria Step 2, per changed document, via Task.
- **Input**: `doc: <path>`, `component: <component>`.
- **Does**: the former Step 2a–2c: `spectra ai compile-extraction-prompt --doc <doc> --component <component>
  --output-format json` (short-circuit empty doc → `unchanged` + note; exit 4 / exit 1 → `failed <code>`); extract;
  Write `.spectra/criteria.json`; `spectra ai ingest-criteria --doc <doc> --component <component> --from
  .spectra/criteria.json --output-format json`; exit 5/6/7 → regenerate, at most 2 attempts; exit 1 `LOCK_TIMEOUT` →
  retry once; other exit 1 → `failed <code>`.
- **Returns**: data-model.md "extractor" — the changeset lines copied verbatim from the ingest's stderr summary.
- **Never**: retires, moves or migrates criteria; reads the document separately; asks the user; starts a subagent.

## spectra-critic (change only)

- **Adds**: "Your last message is exactly one line: `<id> | <verdict> <score>`, or `<id> | failed: <reason>` when you
  could not write a verdict." Everything else unchanged.

## spectra-update (main)

- **Context budget contract** (near the top): never reads docs, compiled prompts, test files, staging JSON or verdict
  files; delegates any step that would need them; its own tool calls are `spectra` commands with small JSON output,
  `update-ack`, writes to `.spectra/progress.json`, and Task calls.
- **Step 5**: per group, in order: progress write → Task `spectra-updater` {suite, group} → report every returned line
  → `unchanged_pending` LOAD / CRITERIA_CHANGED → ask: acknowledge (`update-ack`) or edit again (Task
  `spectra-updater` {suite, ids}, at most twice per test); REMOVED / CRITERIA_REMOVED / RELINK → re-run triage. A
  `stop:` line → report it and stop for this suite.
- **Step 5a**: research R5.
- **Report (Step 7)**: built from the returned lines; every outcome, baseline line and warning appears.

## spectra-criteria (main)

- **Step 2**: per document, in order: progress write → Task `spectra-extractor` {doc, component} → report every
  returned line.
- **Step 3**: the summary from the returned lines: per document its outcome and changeset lines; removed criteria still
  referenced by tests; moves, retirements, skipped and failed documents.
