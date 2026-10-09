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
only ever read, to find doc files that were renamed. The edits themselves run in **subagents**
(Spec 092): this session orchestrates, asks the user, and reports; the `spectra-updater`,
`spectra-critic` and `spectra-repairer` subagents do the heavy work in their own contexts and return a
few lines each.

```
update-triage (CLI)  →  relink-sources for renamed docs (CLI, after the user's yes)
  →  you choose buckets with the user  →  per group: spectra-updater subagent
       (compile-update-prompt --group → edit → one staging file per test → ingest-update --all)
  →  per written test: spectra-critic subagent  →  partial / hallucinated: spectra-repairer subagent
  →  update-ack for tests reviewed without an edit prompt
```

The edit prompt carries each test's doc sections (as they are now, the previous version and
what changed) and only the criteria the tests link. `ingest-update` keeps the **original id**,
re-asserts any **manual verdict / notes**, **fails loud on drift** (an out-of-scope change to
priority, component or tags), and records the edited test's new baseline; a test returned
unchanged is recorded as reviewed. The CLI never rewrites a test for you.

## Context budget contract

This session holds only what it needs to orchestrate. It **never reads** docs, compiled prompts,
test files, staging JSON or verdict files — not to check a result, not to explain one. Any step
that would need them is done by the named subagent, which returns a few lines. This session's own
tool calls are:
- `spectra` commands with small JSON output (triage, relink, `update-ack`, `ingest-verdict --all`,
  `ingest-grounding --all`);
- writes to `.spectra/progress.json`;
- Task calls to `spectra-updater`, `spectra-critic` and `spectra-repairer`.

The user's decisions stay here: which buckets to process, the REMOVED review, relinking, what to
do with a test returned unchanged while its change still needs an edit, and what to do with a test
that stays hallucinated. Report every line a subagent returns, as returned — the user doesn't see
a subagent's own output, so this session's report is how they learn what happened.

## NON-STOP CONTRACT

Every step below is a single `Bash(spectra *)` call, a Task call to the `spectra-updater`,
`spectra-repairer` or `spectra-critic` subagent, or a `Write` only to `.spectra/progress.json`. If no
single `spectra` call covers a step, **STOP and report a missing affordance** — never work around it.
Prohibited: shell loops over per-test verbs, piping spectra output to `python` / `jq` / any
interpreter, reading docs, prompts, tests, staging or verdict files, editing a test's `.md` file
yourself, and rewriting a baseline file (`_source-baseline.json`) by hand.

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

**Submodule not checked out** — when triage's `notes` contain a line like `N reference(s)
from M test(s) point into submodule <path>, which isn't checked out (run git submodule update
--init)`, that submodule's docs are missing in this clone: it is a setup problem, not a doc
change. Tell the user to run git submodule update --init in the repository themselves (never
run it from this skill), then re-run triage. While that note is present, don't offer
`source-baseline --init` — a baseline taken now would leave every ref into the submodule out.

**First-time setup** — when the suite has unbaselined tests, offer the baseline only now,
after the relink step (whether the user relinked or not):
`spectra ai source-baseline --init --suite <suite> --from-date <YYYY-MM-DD> --output-format json --verbosity quiet`,
with the date the tests were written or last reviewed. It reads each doc as it was on that
date — following renames back to the old path — so doc changes made since then show at the
next triage. If there is no git, or the user doesn't know a date, plain
`spectra ai source-baseline --init --suite <suite> --output-format json --verbosity quiet`
adopts the current docs instead. Run either only after the user agrees, then re-run triage.

**Step 3** — REMOVED and CRITERIA_REMOVED (review only).

First, set aside every entry whose reason starts with `not found; submodule not checked out`:
the doc isn't gone, its submodule isn't checked out in this clone. Name each submodule once
(the triage group lists it), tell the user to run git submodule update --init themselves
and re-run triage, and do not list those tests for review — no fix, no ack, no delete.

List every other entry of `removed`: the test, the ref or criterion that is gone, and its
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

Update `.spectra/progress.json` — G = number of selected groups:
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":2,"loop":{"current":0,"total":G,"label":"starting"}}
```

**Step 5** — For each selected group (g = 1..G), in order — one at a time, never in parallel —
update progress.json:
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":2,"loop":{"current":g,"total":G,"label":"group g/G: <group>"}}
```

Then invoke the `spectra-updater` subagent with the Task tool, passing `suite: <suite>` and
`group: <group>` — nothing else (no prompt text, no test content). It compiles the group's edit
prompt, edits the tests, stages one file per test, ingests them, retries a failed test at most
twice, and returns one line per test:
- `<id> | written` — updated; its baseline was recorded (it leaves triage). `<id> | written
  (manual verdict kept)` — the same, and the test keeps its Manual verdict (Step 5a skips it);
- `<id> | unchanged` — acknowledged: reviewed, no edit needed; its baseline was recorded;
- `<id> | unchanged_pending <BUCKET>: <why>` — returned unchanged, but the change still needs an
  edit; nothing was recorded (below);
- `<id> | failed <error_code>: <first error>` — still failing after the subagent's retries;
  nothing was written for it;

then a `baseline: …` line and any `warning: …` lines. Report every line, as returned, to the user.
A test that failed is reported — it is not redone in this session. A line may name a test of an
earlier group (a staging file left from before): attribute it by id. A test of the group with no
line is reported as "outcome unknown"; do not read files to find out.

A `stop: …` line means the subagent hit something no single command covers:
- `stop: triage is stale` → a doc or test changed after the triage: re-run Step 1, then continue
  with the buckets the user chose;
- any other `stop:` → report it and stop for this suite.

**Returned unchanged, still pending.** List each `unchanged_pending` test with its bucket and
reason, then:
- `LOAD` or `CRITERIA_CHANGED` → ask the user, per group, whether to **acknowledge** it (the
  change needs no edit to the test) or **edit again**. Never acknowledge without the user's
  answer.
  - acknowledge → `spectra ai update-ack --suite <suite> --ids <id1>,<id2> --output-format json --verbosity quiet`;
  - edit again → invoke the `spectra-updater` subagent with `suite: <suite>` and `ids: <id>` and
    report its lines. Offer this at most twice per test (the same bound as a failure).
- `REMOVED`, `CRITERIA_REMOVED` or `RELINK` → edit groups never contain these (Step 4), so the
  documentation changed again since triage. Say so, offer no acknowledgement (`update-ack`
  refuses them: `REF_REMOVED` for `REMOVED` and `RELINK`, `CRITERION_REMOVED` for
  `CRITERIA_REMOVED`), and tell the user to re-run `spectra ai update-triage`; Step 2a
  (`RELINK`) or Step 3 (`REMOVED` / `CRITERIA_REMOVED`) then handles the test.

**Step 5a** — Re-verify the edited tests (Spec 090). An edit removes the test's old critic
verdict (it judged the old content), so each test reported `written` is unverified until the
critic checks it again. Tests reported `written (manual verdict kept)` keep their Manual verdict
(`manual_verdict_kept`): never send them to the critic; list them in the report as "manual
verdict kept — review by hand".

The first time this happens in a run, ask once: "Re-verify the edited tests with the critic
now? (recommended)". If the user declines, don't ask again; collect the edited tests as
**unverified** for the report.

Otherwise, for each `written` test of the group, invoke the `spectra-critic` subagent with the
Task tool — suite + test id only, never a hand-built file path (the same procedure as the
generation skill's critic step). The critic compiles its prompt and writes its verdict file; its
final line is `<id> | <verdict> <score>` or `<id> | failed: <reason>`. Route the test by that final
line — never read the verdict file:
- `grounded` → done;
- `partial` or `hallucinated` → collect the id for one repair attempt (below);
- `failed: …`, or no such line → the test is **unverified**; report the reason. Never read the
  verdict file to find out more.

When every test of the group has been through the critic, run with the Bash tool:
```
spectra ai ingest-verdict --suite <suite> --all --output-format json
spectra ai ingest-grounding --suite <suite> --all --output-format json
```

**Repair.** If any ids were collected, invoke the `spectra-repairer` subagent once with the Task
tool, passing `suite: <suite>` and `ids: <id1>,<id2>`. It patches only what the critic flagged,
ingests the repairs (keeping each test's baseline), retries a failed repair ingest at most twice,
and returns one line per id: `<id> | written`, `<id> | unchanged` or `<id> | failed <error_code>:
<first error>`. Report every line. Then:
- only for the ids it returned `written` or `unchanged` — the repairs that landed: invoke the
  `spectra-critic` subagent again per id, then
  `spectra ai ingest-verdict --suite <suite> --all --output-format json` and
  `spectra ai ingest-grounding --suite <suite> --all --repaired --repair-attempts 1 --ids <id1>,<id2> --output-format json`
  (Spec 091: `--ids` names exactly the re-critiqued tests, so nothing else is marked repaired);
- **repair failed** — a `failed` line: report "repair failed: <error_code>", run no critic and
  write no repaired grounding, and list the test as **unverified** if it was partial, or as
  **waiting for the user** if it was hallucinated — with the same choices as a test that stays
  hallucinated (below);
- an id with no line → report "repair outcome unknown", leave it unverified, and do not
  re-critique it.

After the repair:
- **still partial** (the re-critic's final line) → it is flagged for review (its block says so);
  report it.
- **still hallucinated** → stop for that test and ask the user to choose: edit it again (the
  `spectra-updater` subagent with `ids: <id>`), restore the previous version from git (the user
  runs that — never run git yourself), or delete it through the `spectra-delete` skill. This skill
  never runs `spectra ai record-drop` or `spectra delete` — the test existed before the edit, so
  dropping it is the user's decision.

The critic and repair steps are never skipped silently: a test that isn't re-verified is
reported as unverified.

**Step 6** — Acknowledge the tests the user reviewed and kept as they are without an edit
prompt (a test returned unchanged in Step 5 is already acknowledged), in one call:
```
spectra ai update-ack --suite <suite> --ids <id1>,<id2> --output-format json --verbosity quiet
```
Only tests the user actually reviewed. A test that is neither edited nor acknowledged stays
in triage and is reported again next time — nothing is deferred silently.

**Step 7** — Update progress.json to mark complete, then report.
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":3}
```
For the suite, summarize from the returned lines and the commands this session ran: the bucket
counts, COSMETIC auto-acknowledged, tests edited (with ids), tests returned unchanged and tests
acknowledged (with ids), tests returned unchanged with a change still pending (with bucket),
REMOVED / CRITERIA_REMOVED left for the user, tests skipped or failed after retries (with their
error codes), each group's `baseline:` line, every `warning:` line, and any unbaselined tests. For the re-verification
(Step 5a), give the counts with ids: grounded, partial (flagged for review), hallucinated
(waiting for the user's choice), unverified (declined or not run), and manual verdict kept.

Write `.spectra/progress.json`: `{"active":4}` (terminal — seam-progress page renders Complete).

## Update all suites

When the user says "update all", run ONE triage over every suite:
```
spectra ai update-triage --all-suites --output-format json --verbosity quiet
```
Then follow Steps 2–7 one suite at a time, with the groups of that suite (each group names its
`suite`; pass that suite to the subagents and to `update-ack`). In Step 2a, preview
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
  (`not found; submodule not checked out` is not a removal: initialise the submodule and re-run triage)
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

If the user says "stop", "cancel" or "stop updating": finish or abandon the current group
(`ingest-update` persists each staged test whole or not at all), don't start the next one,
and report what was edited, acknowledged and left so far. Tests not yet processed stay in
triage and are reported again next time.
