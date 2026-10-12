# Contracts: Spec 093

## CLI: `spectra ai ingest-verdict` (the one CLI change)

| Option | Description |
|---|---|
| `--ids a,b` | Spec 093: with `--suite S --all`, classify only these tests; unknown ids → `not_found`. 091 parsing (comma-separated, trimmed, de-duplicated, case-insensitive) |

Batch JSON adds `results: [{id, verdict, score, gate}]` and `not_found` (data-model.md). Existing fields, counts and
exit codes unchanged. `--ids` without `--all` → `--ids is only valid with --all.`, exit 1. Single `--from` / stdin
form unchanged. The batch human summary goes to stderr in JSON mode.

## Agents

### spectra-analyst
- **Invoked by**: spectra-generate (Steps 1–3), via Task. **Input**: `suite`, `doc_suite`, optional `focus`.
- **Does**: `spectra ai compile-analysis-prompt --suite <suite> --doc-suite <doc_suite> [--focus "<focus>"]
  --output-format json` (exit 4 → `refused:` + `refusal:` lines, stop); analyse in its own context; Write
  `.spectra/analysis.json`; `spectra ai ingest-analysis --suite <suite> --from .spectra/analysis.json --output-format
  json`; exit 5/6 → re-analyse, at most 2 retries; then batches (research R2).
- **Returns**: data-model "analyst". **Never**: generates tests, asks the user, starts a subagent.

### spectra-generator
- **Invoked by**: spectra-generate (Steps 5–7), per batch, via Task. **Input**: `suite`, `focus`, `count`.
- **Does**: `spectra ai compile-prompt --suite <suite> --count <count> --focus "<focus>" --output-format json`;
  generate, reading documents under `docs/` in its own context; Write `.spectra/generated.json`; `spectra ai
  ingest-tests <suite> --from .spectra/generated.json --output-format json`; exit 5/6 → regenerate, at most 2 retries.
- **Returns**: data-model "generator". **Never**: runs the critic, asks the user, starts a subagent, edits a `.md`.

### spectra-repairer (092; return changed — FR-013)
- One line per `results` entry over its ingest calls (leftovers included, by id), plus a `failed` line for an input
  id whose compile was refused.

### spectra-critic (unchanged)
- Its final line stays; the skills use it for progress only.

## Skills

### spectra-generate (main)
- **Context budget contract** (research R5).
- Steps 1–3 → Task `spectra-analyst`; Step 4 from its lines → STOP.
- Steps 5–7 → per batch Task `spectra-generator` (`focus: "<user focus>; <batch focus>"`, `count`).
- 8a → critic per id; `spectra ai ingest-verdict --suite {suite} --all --ids <ids> --output-format json`; route by
  `results`; hallucinated → record-drop + delete only when the critic's line agrees or there is none (a disagreement →
  unverified); no `results` entry → the critic once more, then unverified ("Unverified (critic could not judge)" in
  Step 9; never regenerated or deleted); `ingest-grounding --all`; written==0 guard verbatim.
- 8b → Task `spectra-repairer`; landed ids → critic, `ingest-verdict --all --ids`, drops, `ingest-grounding --all
  --repaired --repair-attempts 1 --ids <…>`; resume via `audit-grounding` (`action_needed: repair`).
- Step 9 → every test by id and title with its outcome.

### spectra-update (main) — FR-014, FR-015
- Step 5a routes by `ingest-verdict --suite <suite> --all --ids <ids>` `results`; disagreement / no result →
  unverified; every `written` line (group, leftover, edit again, re-edit) is re-verified or listed unverified.
