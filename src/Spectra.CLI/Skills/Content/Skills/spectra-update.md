---
name: spectra-update
description: Update existing test cases after documentation or acceptance criteria changes.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Update SKILL

You help users update existing test cases when documentation or acceptance
criteria have changed. The CLI records, for every test, a fingerprint of each doc
section it references (`source_refs`) and each criterion it links (`criteria`), and
`update-triage` reports exactly what changed since — deterministic and model-free; git is
only ever read, to find doc files that were renamed. You then **edit the affected tests in
this session** through the update seam:

```
update-triage (CLI)  →  relink-sources for renamed docs (CLI, after the user's yes)
  →  you choose buckets with the user  →  compile-update-prompt (CLI)
  →  you edit in-session  →  ingest-update (CLI, fail-loud)  →  update-ack for tests kept as they are
```

`ingest-update` keeps the **original id**, re-asserts any **manual verdict / notes**,
**fails loud on drift** (an out-of-scope change to priority, component or tags), and
records the edited test's new baseline. The CLI never rewrites a test for you.

## NON-STOP CONTRACT

Every step below is a single `Bash(spectra *)` call, a `Read`, or a `Write` to
`.spectra/updated.json` / `.spectra/progress.json`. If no single `spectra` call covers
a step, **STOP and report a missing affordance** — never work around it. Prohibited:
shell loops over per-test verbs, piping spectra output to `python` / `jq` / any
interpreter, editing a test's `.md` file yourself, and rewriting a baseline file
(`_source-baseline.json`) by hand.

## Update test cases for a suite

**Step 0** — Set up the live progress monitor.

Run with the Bash tool:
```
spectra ai init-seam-progress
```

Write `.spectra/progress.json` with the Write tool:
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":0}
```

**Step 1** — Triage (deterministic, no model).

Run with the Bash tool:
```
spectra ai update-triage --suite <suite> --output-format json --verbosity quiet
```
Replace `<suite>` with the suite the user mentions (e.g., "checkout", "login", "payments").
While it runs, do NOTHING — don't poll the terminal, list directories, or read files; just
wait for it to complete. Read the JSON on stdout. COSMETIC changes (markup, whitespace or
case only) were already acknowledged automatically (`auto_acked`).

**Step 2** — Show the summary: per suite the bucket counts from `suites[].counts`, then each
group from `groups` (`id`, `bucket`, the `refs` / `criteria`, how many tests, and `why`,
e.g. `numbers: 30→45`). Show `notes` and `warnings`. If `unbaselined` is not empty, say
those tests have no baseline yet and can't be triaged. If there is no `RELINK` group, offer
`spectra ai source-baseline --init --suite <suite> --output-format json --verbosity quiet`
here (it adopts the current docs as the baseline — only after the user agrees); if there is a
`RELINK` group, don't offer it yet — Step 2a offers it after relinking. If `no_tests`
is not empty, list them as sections of changed docs that no test covers (generation hints
for the `spectra-generate` skill). If every count except `unchanged` is 0, say "Tests are up
to date" and STOP.

**Step 2a** — RELINK: doc files that were renamed or moved. Only when a group has the bucket
`RELINK`; otherwise go on with Step 3. Each RELINK ref's file is missing, and git history
names the file it was renamed to (`why`: `renamed to <path>`).

1. Preview the relink with the Bash tool:
   ```
   spectra ai relink-sources --suite <suite> --dry-run --output-format json --verbosity quiet
   ```
2. Show, per test, each `relinked` ref as `from` → `to` (only the path changes; the `#anchor`
   is kept), whether `orphan_cleared` is true, and every `not_relinked` ref with its `reason`.
3. Ask the user whether to relink these tests. Relink only after the user says yes.
4. On yes, run it without `--dry-run`:
   ```
   spectra ai relink-sources --suite <suite> --output-format json --verbosity quiet
   ```
   Then re-run triage (the Step 1 command) and show the summary again before Step 3. Each moved
   baseline entry keeps its old fingerprint, so content the rename also changed now shows as
   `LOAD` or `LIGHT` in the right section.
5. On no, nothing is relinked: report the RELINK tests as left for later and go on with the
   other buckets (Step 3).

**First-time setup** — when the suite has unbaselined tests, offer the baseline only now,
after the relink step (whether the user relinked or not):
`spectra ai source-baseline --init --suite <suite> --from-date <YYYY-MM-DD> --output-format json --verbosity quiet`,
with the date the tests were written or last reviewed. It reads each doc as it was on that
date — following renames back to the old path — so doc changes made since then show at the
next triage. If there is no git, or the user doesn't know a date, plain
`spectra ai source-baseline --init --suite <suite> --output-format json --verbosity quiet`
adopts the current docs instead. Run either only after the user agrees, then re-run triage.

**Step 3** — REMOVED and CRITERIA_REMOVED (review only).

List every entry of `removed`: the test, the ref or criterion that is gone, and its
`reason` (`anchor not found`; `not found; <why>` for a missing file — e.g. `not found; no git`,
`not found; ambiguous rename`; `not in any criteria file`). Do not compile an
edit prompt for these tests, and never edit the `.md` file yourself — there is no current
section to edit against. Tell the user how to clear each one:
- the heading was renamed or the doc moved → fix the ref in the test's `source_refs`
  (or the ID in its `criteria`), then run
  `spectra ai update-ack --suite <suite> --ids <id> --output-format json --verbosity quiet`
  to record the new baseline;
- the section is gone but the test is still valid through its other refs → drop the
  ref from `source_refs`;
- the feature itself was removed → the test can be deleted with the `spectra-delete`
  skill (`spectra delete <id> --dry-run`, then confirm). Never run the delete from this skill.

When the reason is `not found; no rename found at 90% similarity`, the doc may have been
renamed with heavier edits than triage looks for. Tell the user that
`spectra ai relink-sources --suite <suite> --min-similarity <N> --dry-run --output-format json --verbosity quiet`
(N between 50 and 100; lower finds heavier-edited renames) previews it; show the result, and
relink without `--dry-run` only after the user says yes, as in Step 2a.

These tests can't be acknowledged while the ref is still listed (`REF_REMOVED` /
`CRITERION_REMOVED`).

**Step 4** — Ask the user which buckets to process:
- **LOAD only (recommended)** — the `LOAD` and `CRITERIA_CHANGED` buckets: numbers, modal
  words (must, should, may, cannot, required, only, not) or many words changed, or a
  linked criterion was reworded;
- **LOAD + LIGHT** — those plus `LIGHT` (small wording changes);
- **choose groups** — offer only groups in the `LOAD`, `CRITERIA_CHANGED` or `LIGHT` buckets;
- **report only** — no edits; STOP after the report.

`RELINK`, `REMOVED` and `CRITERIA_REMOVED` groups never enter the edit loop, whichever option is chosen.

Update `.spectra/progress.json` — N = number of tests in the selected groups:
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":2,"loop":{"current":0,"total":N,"label":"starting"}}
```

**Step 5** — For each selected group, and each test in it (i = 1..N), update progress.json:
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":2,"loop":{"current":i,"total":N,"label":"test i/N: <id>"}}
```

Compile the edit prompt with the Bash tool:
```
spectra ai compile-update-prompt --suite <suite> --test-id <id>
```
- Exit `0` → the edit prompt is on stdout (the existing test + the source/criteria + edit instructions).
- Exit `4` → a required input is missing. Skip this test and tell the user why.

Read the prompt. The group's `why` says what changed. Edit **only** the parts that change
requires. Keep the id, the structure, the priority, component, tags, and any manual notes.
If, after reading, the test is still correct as it is, don't ingest it — note it for Step 6.
Otherwise write the whole edited test as a JSON array with a single element to
`.spectra/updated.json`, and ingest it (fail-loud) with the Bash tool:
```
spectra ai ingest-update <suite> --test-id <id> --from .spectra/updated.json --output-format json --verbosity quiet
```
- Exit `0` → persisted; the id is unchanged, the index was regenerated and the test's
  baseline was recorded (so it leaves triage).
- Exit `5` → content invalid **or** `DRIFT_DETECTED`. Read the specific error.
  - `DRIFT_DETECTED` → you changed an out-of-scope field; redo the edit touching ONLY what the doc change requires.
  - otherwise → fix the malformed/truncated JSON.
- Exit `6` → schema invalid. Read the error and fix the offending field.
- Exit `1` → an error; nothing was persisted (see "Exit codes" below).

**Bounded retry:** on exit `5`/`6`, re-edit and re-ingest **at most twice** (3 attempts
total) for the same test. If it still fails, stop and report the specific error — do not
loop. On any non-zero exit, nothing was persisted; the original test and indexes are untouched.

**Step 6** — Acknowledge the tests the user reviewed and kept as they are (no edit needed),
in one call:
```
spectra ai update-ack --suite <suite> --ids <id1>,<id2> --output-format json --verbosity quiet
```
Only tests the user actually reviewed. A test that is neither edited nor acknowledged stays
in triage and is reported again next time — nothing is deferred silently.

**Step 7** — Update progress.json to mark complete, then report.
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":3}
```
For the suite, summarize: the bucket counts, COSMETIC auto-acknowledged, tests edited (with
ids), tests acknowledged (with ids), REMOVED / CRITERIA_REMOVED left for the user, tests
skipped or failed after retries, and any unbaselined tests.

Write `.spectra/progress.json`: `{"active":4}` (terminal — seam-progress page renders Complete).

## Update all suites

When the user says "update all", run ONE triage over every suite:
```
spectra ai update-triage --all-suites --output-format json --verbosity quiet
```
Then follow Steps 2–7 with the groups of every suite (each group names its `suite`; pass
that suite to `compile-update-prompt`, `ingest-update` and `update-ack`). In Step 2a, preview
and relink every suite in one call each: `spectra ai relink-sources --all-suites --dry-run …`,
then, after the user's yes, the same without `--dry-run`.

## Exit codes (triage, relink, ack, baseline)

- Exit `0` → success; read the JSON on stdout. Triage and `relink-sources` exit 0 whatever they
  find (no git, or no rename, is a `reason`, not an error).
- Exit `4` → refused; nothing was written. Show `errors[0]` and the `error_code`
  (`SUITE_NOT_FOUND`, `TEST_NOT_FOUND`, `REF_REMOVED`, `CRITERION_REMOVED`, `TRIAGE_MISSING`,
  `GROUP_NOT_FOUND`, `TRIAGE_STALE`, `INVALID_ARGUMENTS`) and stop for that suite.
- Exit `1` → an error; nothing was written. On `LOCK_TIMEOUT` (another SPECTRA command is
  writing baselines), retry the same command once. Otherwise — or if the retry also fails —
  report the `error_code` (`BASELINE_UNREADABLE` names the file to fix; `CONFIG_MISSING` /
  `CONFIG_INVALID` for a config problem — fix `spectra.config.json` first) and stop for that suite.

---

## Buckets

When presenting results, explain the buckets (a test's bucket is its worst change):
- **RELINK** — a referenced doc file was renamed or moved (found in git history); cleared by `relink-sources` after the user's yes
- **REMOVED** — a referenced section (or its file) is gone; review only, cleared by fixing the ref
- **CRITERIA_REMOVED** — a linked criterion no longer exists; review only
- **LOAD** — a number, a modal word (must/should/may/cannot/required/only/not) or many words changed, or no diff was available
- **CRITERIA_CHANGED** — a linked criterion's text changed
- **LIGHT** — a small wording change
- **COSMETIC** — markup, whitespace or case only; acknowledged automatically
- **UNCHANGED** — nothing changed since the baseline
- **UNBASELINED** — no baseline yet (never counted as changed); `source-baseline --init` adopts the current docs

---

## Suggest next steps

After updating:
- "Run coverage analysis?" → use spectra-coverage SKILL
- "Validate the updated test cases?" → use spectra-validate SKILL
- "Generate test cases for the sections no test covers?" → use spectra-generate SKILL
- "View the updated test cases?" → use spectra-list SKILL

---

## Example user requests

- "Update test cases for the checkout suite"
- "My docs changed, update the test cases"
- "Check if any test cases are outdated"
- "Edit the outdated login test cases to match the new docs"
- "Update all test suites"
- "Which tests reference sections that were removed?"
- "Refresh test cases after the API docs update"

---

## Stop

If the user says "stop", "cancel" or "stop updating": finish or abandon the current test
(each `ingest-update` either persists the whole edit or nothing), don't start the next one,
and report what was edited, acknowledged and left so far. Tests not yet processed stay in
triage and are reported again next time.
