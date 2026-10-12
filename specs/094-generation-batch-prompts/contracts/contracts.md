# Contracts: Section-Scoped Generation Batches (Phase 5c)

## `spectra ai ingest-analysis --suite S --from FILE [--focus F]` (changed)

- Config: checked loader for a write command, reading `source` (083). A missing config is exit 1 (`CONFIG_MISSING`); a
  `generation.batch_size` outside 1–50 is exit 1 (`CONFIG_INVALID`).
- On success it writes `.spectra/generation-plan/<S>.json` (data-model.md) and adds to the JSON:

```json
{"already_covered":2,"recommended":11,"breakdown":{…},"technique_breakdown":{…},"boundary_gaps":[…],
 "documents_analyzed":2,
 "plan":".spectra/generation-plan/storage.json","planned_total":11,"count_rule":"proportional",
 "batches":[{"id":"1","count":6,"source_refs":["docs/storage.md#upload-quota","docs/storage.md#quota-settings"],
             "behaviors":["Upload is refused above the 30 GB quota","Quota shown in settings"]},
            {"id":"unsourced","count":1,"source_refs":[],"behaviors":["Audit log keeps 90 days"]}],
 "unresolved":[{"id":"B7","title":"Audit log keeps 90 days","source":"docs/audit.md","reason":"not found"}],
 "notes":[]}
```

- Existing fields keep their names and meanings.
- Human summary: the existing lines, plus
  `batch <id> | count <k> | <refs, comma-separated> | <title>; <title>; …` per batch (a `|` inside a title shown as
  `/`; the JSON keeps the title),
  `unresolved: <id> <title> — <reason>` per unresolved behaviour, and
  `counts raised to one per batch (recommended R < n batches)` when `count_rule` is `one_per_batch`.
  In JSON mode the summary goes to stderr.
- A failed analysis (exit 5/6) writes nothing. A failed plan write: exit 1, `PLAN_WRITE_FAILED`, shared failure shape.

## `spectra ai compile-prompt --suite S --batch ID [--count N] [--focus F]` (new mode)

- Config: checked loader for a read command, reading `source`.
- Exit 0: the prompt on stdout. On stderr, the mode and the warnings (criteria fallback, unmatched sections, ambiguous
  anchors, the template fallback): in JSON mode one line `{"command":"compile-prompt","success":true,"mode":"batch|count_fallback|count","batch":"<id>","warnings":[…]}`; in human mode `Mode: <mode>` and
  `Warning: <text>` lines.
- Modes:
  - `batch`: the batch prompt.
  - `count_fallback`: the project's `test-generation.md` predates batch prompts and is customised or has no manifest
    record. A count-mode prompt with the user's template; focus = `<title> (<ref>); …` (+ `--focus`), count = the batch's
    count or `--count`. Warning: "custom test-generation template predates batch prompts — this batch is compiled in
    count mode (the generator reads the docs); run spectra prompts reset test-generation to use section batches".
  - `count`: `--batch unsourced`. A count-mode prompt (`read_docs`), focus = the unsourced titles joined by `; ` (+
    `--focus`), count = the batch's count or `--count`.
- An unedited pre-094 copy (hash = the skills manifest's record, under the absolute or the relative key) compiles in
  `batch` mode with the bundled template and the warning "using the bundled test-generation template; run spectra
  update-skills to refresh your copy".
- `--batch` with `--from-description`: exit 1, usage message `--batch cannot be combined with --from-description.`.

| Situation | Exit | `error_code` |
|---|---|---|
| no plan file for S, or an unreadable plan | 4 | `PLAN_MISSING` |
| no batch with that id | 4 | `BATCH_NOT_FOUND` (lists the plan's batch ids) |
| a batch section changed or no longer resolves | 4 | `PLAN_STALE` (`errors` = one entry per section) |
| neither the sections nor the batch's documents have criteria | 4 | `MISSING_INPUT` (`missing_input: "criteria_context"`) |
| the estimate exceeds `analysis.max_prompt_tokens` | 4 | `PROMPT_TOO_LARGE` |
| config missing / invalid | 1 | `CONFIG_MISSING` / `CONFIG_INVALID` |

The checks run in this order: plan, batch, staleness, criteria, size. The template decides only the mode, never a
refusal. Per mode:
- `batch`: plan → batch → staleness → the batch criteria (R6) → size.
- `count_fallback`: plan → batch → staleness → count mode's suite-match criteria; no size check.
- `count` (`--batch unsourced`): plan → batch → count mode's suite-match criteria; no template, staleness or size check.

Every refusal of a call with `--batch` uses the shared shape — the criteria refusal included (`MISSING_INPUT`,
`missing_input: "criteria_context"`). Only calls without `--batch` keep the pre-094 `{refused, missing_input, message}`
shape (analyze M1/M2, 2026-10-12). Failure shape (stderr in JSON mode):
`{"command":"compile-prompt","success":false,"error_code":…,"errors":[…],"warnings":[…]}` (+ `missing_input`).
Nothing is written.

## `spectra ai compile-prompt --suite S [--count N] [--focus F]` (count mode, changed text only)

The options, refusals and refusal shape are unchanged. The prompt no longer names `ListDocumentationFiles`,
`ReadTestIndex`, `CheckDuplicates`, `GetNextTestIds` or "ON-DEMAND". It says to read the documentation with the file
tools and that test ids are assigned when the tests are ingested. The same applies to `--from-description` and to the
built-in fallback.

## Config

`generation.batch_size`: optional int, default 8, valid 1–50.

## Gitignore

`.spectra/generation-plan/` is in the shared list. `init` writes it; `update-skills` offers it once. Pre-090 workspaces
count only the 089 list as offered.

## Agents

### spectra-analyst
- Steps 1–3 unchanged. Step 4: copy the ingest's `batch …` and `unresolved: …` lines verbatim from its summary; never
  plan batches.
- Final message: the 093 recommendation lines, then the CLI's `batch` lines and `unresolved:` lines.

### spectra-generator
- Input: `suite: <suite>`, `batch: <id>`, optional `count: <k>`.
- Compile: `spectra ai compile-prompt --suite <suite> --batch <id> [--count <k>] --output-format json`.
- The compile output is the prompt (stdout) plus, from stderr, the line starting `{"command":"compile-prompt"`: take
  `mode` and `warnings` from that line; everything else is the prompt.
- Numbered batch in `batch` mode: never Read a document; the prompt is the complete source. `unsourced`, or a batch the
  compile reports as `count_fallback`: read the docs with the file tools, and relay the compile's warning as `warning:
  <text>`.
- Any exit 4 → `failed <error_code>: <errors[0]>`. Ingest and final lines are unchanged.

## Skill — spectra-generate
- Step 4 shows the batch lines and the unresolved behaviours.
- Steps 5–7: per batch, in plan order, sequentially, Task `spectra-generator` with `suite`, `batch`, and `count` only for
  a user's explicit total (split as in 093).
- Never invoke the generator for a 0-count batch (only possible when the user's total is smaller than the number of
  batches); Step 9 lists it as "not generated (count 0 after splitting your total)".
- After the loop: `failed PLAN_STALE` / `failed PROMPT_TOO_LARGE` batches are reported, with one offer to re-run the
  analysis (invoke the analyst again). After a re-run: back to Step 4 — present the new recommendation, name the batches
  that already ran in this session with their created test ids, suggest a focus on the stale sections, STOP. Nothing is
  regenerated automatically.
- Each distinct template `warning:` relayed by the generators is reported once per run.
- Unchanged: the context budget contract, 8a/8b routing, the repairer, NON-STOP, the written==0 guard, from-description.
