---
name: spectra-update
description: Update existing test cases after documentation or acceptance criteria changes.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Update SKILL

You help users update existing test cases when documentation or acceptance criteria have changed. The CLI records a
fingerprint of each doc section a test references (`source_refs`) and each criterion it links (`criteria`), and
`update-triage` reports exactly what changed since — deterministic and model-free; git is only read, to find renamed
doc files. The edits run in **subagents** (Specs 092, 101): this session orchestrates, asks the user, and reports;
`spectra-updater` edits a group and has `spectra-critic` re-verify each edited test, and `spectra-repairer` repairs
what the critic flagged the same way — each returns a few lines.

```
update-triage (CLI)  →  relink-sources for renamed docs (CLI, after the user's yes)
  →  you choose buckets and ask once about re-verifying  →  per group: spectra-updater subagent
       (compile-update-prompt --group → edit → ingest-update --all → spectra-critic per written test)
  →  you route by the verdict lines  →  partial / hallucinated: spectra-repairer subagent
  →  update-ack for tests reviewed without an edit prompt
```

`ingest-update` keeps the **original id**, re-asserts any **manual verdict / notes**, **fails loud on drift** (an
out-of-scope change to priority, component or tags), and records the edited test's new baseline; a test returned
unchanged is recorded as reviewed. The CLI never rewrites a test for you.

## Additional resources

[reference.md](reference.md) in this skill's folder explains what this session needs only now and then. Reading it is
looking up this skill's own instructions, not a workflow step; read only the part you need: **Exit codes** (when a
command exits non-zero), **Buckets** (when you present the triage), **Submodules and first-time setup** (when triage
notes a submodule or unbaselined tests), **Clearing REMOVED entries** (Step 3), **Suggest next steps** and **Example
user requests**.

## Context budget contract

This session **never reads** docs, compiled prompts, test files, staging JSON or verdict files — the named subagent does
that work and returns a few lines. Its own calls: `spectra` commands with small JSON output (triage, relink,
`update-ack`, `ingest-grounding --all`, and `ingest-verdict --all` on the fallback), writes to `.spectra/progress.json`,
and Task calls to `spectra-updater`, `spectra-repairer` — and to `spectra-critic` only on the fallback (Step 5a).

The user's decisions stay here: which buckets to process, whether to re-verify, the REMOVED review, relinking, a test
returned unchanged while its change still needs an edit, and a test that stays hallucinated. Report every line a
subagent returns, as returned — this session's report is how the user learns what happened.

## NON-STOP CONTRACT

Every step below is a single `Bash(spectra *)` call, a Task call to the `spectra-updater`,
`spectra-repairer` or `spectra-critic` subagent, or a `Write` only to `.spectra/progress.json`. If no
single `spectra` call covers a step, **STOP and report a missing affordance** — never work around it.
Prohibited: shell loops over per-test verbs, piping spectra output to `python` / `jq` / any
interpreter, reading docs, prompts, tests, staging or verdict files, editing a test's `.md` file
yourself, and rewriting a baseline file (`_source-baseline.json`) by hand.

## Update test cases for a suite

**Step 0** — Run `spectra ai init-seam-progress`, then write `.spectra/progress.json`:
```json
{"phases":["Step 1 — Triage","Step 2 — Review","Step 5 — Edit tests","Step 7 — Report"],"active":0}
```

**Step 1** — Triage (deterministic, no model):
```
spectra ai update-triage --suite <suite> --output-format json --verbosity quiet
```
`<suite>` is the suite the user mentions. While it runs, do NOTHING — no polling, no listing, no reading files. COSMETIC
changes (markup, whitespace or case only) were already acknowledged automatically (`auto_acked`).

**Step 2** — Show the summary: per suite the bucket counts from `suites[].counts`, then each group from `groups` (`id`,
`bucket`, the `refs` / `criteria`, how many tests, and `why`, e.g. `numbers: 30→45`), the `notes` and `warnings`. List
`unbaselined` tests (no baseline yet, can't be triaged) and `no_tests` (sections of changed docs no test covers —
hints for the `spectra-generate` skill). If there is no `RELINK` group, offer
`spectra ai source-baseline --init --suite <suite> --output-format json --verbosity quiet` (it adopts the current docs as
the baseline — only after the user agrees); with a `RELINK` group, Step 2a offers it after relinking. A submodule note,
or unbaselined tests: follow reference.md (**Submodules and first-time setup**). If every count except `unchanged` is
0, say "Tests are up to date" and STOP.

**Step 2a** — RELINK (doc files renamed or moved; `why`: `renamed to <path>`). Only when a group has the bucket
`RELINK`; otherwise go on with Step 3.
1. Preview: `spectra ai relink-sources --suite <suite> --dry-run --output-format json --verbosity quiet`
2. Show, per test, each `relinked` ref as `from` → `to` (the `#anchor` is kept), whether `orphan_cleared` is true, and
   every `not_relinked` ref with its `reason`.
3. Ask the user whether to relink these tests. Relink only after the user says yes.
4. On yes, run `spectra ai relink-sources --suite <suite> --output-format json --verbosity quiet`, then re-run triage
   (Step 1) and show the summary again before Step 3.
5. On no, nothing is relinked: report the RELINK tests as left for later and go on with the
   other buckets (Step 3).

**Step 3** — REMOVED and CRITERIA_REMOVED (review only). Entries whose reason starts with
`not found; submodule not checked out` are not removals: name the submodule once, tell the user to run
git submodule update --init themselves and re-run triage, and do not list those tests for review. List every other entry of `removed`: the test, the ref or
criterion that is gone, and its `reason`. Do not compile an edit prompt for these tests, and never edit the `.md` file
yourself. Tell the user how to clear each one (reference.md, **Clearing REMOVED entries**); never run a delete from
this skill. These tests can't be acknowledged while the ref is still listed (`REF_REMOVED` / `CRITERION_REMOVED`).

**Step 4** — Ask the user which buckets to process:
- **LOAD only (recommended)** — the `LOAD` and `CRITERIA_CHANGED` buckets;
- **LOAD + LIGHT** — those plus `LIGHT` (small wording changes);
- **choose groups** — offer only groups in the `LOAD`, `CRITERIA_CHANGED` or `LIGHT` buckets;
- **report only** — no edits; STOP after the report.

`RELINK`, `REMOVED` and `CRITERIA_REMOVED` groups never enter the edit loop, whichever option is chosen.

Then, before the first group is edited, ask once: "Re-verify the edited tests with the critic now? (recommended)". The
answer (`verify: yes` or `verify: no`) goes to every `spectra-updater` and `spectra-repairer` call of this run; don't
ask again. With `no`, the edited tests are collected as **unverified** for the report.

Update `.spectra/progress.json` (G = number of selected groups):
`{"phases":[…as above…],"active":2,"loop":{"current":0,"total":G,"label":"starting"}}`

**Step 5** — For each selected group (g = 1..G), one at a time — never in parallel — set the loop to
`{"current":g,"total":G,"label":"group g/G: <group>"}`, then invoke the `spectra-updater` subagent with the Task tool,
passing `suite: <suite>`, `group: <group>` and `verify: yes|no` — nothing else. It edits, stages and ingests the tests
(retrying a failed test at most twice), has `spectra-critic` re-verify each `written` test (with `verify: yes`), and
returns one line per test:
- `<id> | written` — updated, baseline recorded. `<id> | written (manual verdict kept)` (`manual_verdict_kept`) — the
  same, and the test keeps its Manual verdict: never sent to the critic; reported as "manual verdict kept — review by
  hand";
- `<id> | unchanged` — acknowledged: reviewed, no edit needed; baseline recorded;
- `<id> | unchanged_pending <BUCKET>: <why>` — returned unchanged, but the change still needs an edit (below);
- `<id> | failed <error_code>: <first error>` — still failing after the retries; nothing written;

then `baseline: …` and `warning: …` lines, and (with `verify: yes`) one verdict line per `written` test or
`critic: not run`. Report every line, as returned. A failed test is reported, not redone here. A line may name a test
of an earlier group (a leftover staging file): attribute it by id. A test of the group with no line is "outcome
unknown"; do not read files to find out. A `stop: triage is stale` line → re-run Step 1, then continue with the
buckets the user chose; any other `stop:` → report it and stop for this suite.

**Returned unchanged, still pending.** List each `unchanged_pending` test with its bucket and reason, then:
- `LOAD` or `CRITERIA_CHANGED` → ask the user, per group, whether to **acknowledge** it (the change needs no edit) or
  **edit again**. Never acknowledge without the user's
  answer.
  - acknowledge → `spectra ai update-ack --suite <suite> --ids <id1>,<id2> --output-format json --verbosity quiet`;
  - edit again → invoke the `spectra-updater` subagent with `suite: <suite>` and `ids: <id>` (and `verify: yes|no`),
    and report its lines. Offer this at most twice per test.
- `REMOVED`, `CRITERIA_REMOVED` or `RELINK` → the documentation changed again since triage: say so,
  offer no acknowledgement (`update-ack` refuses them: `REF_REMOVED`, `CRITERION_REMOVED`), and tell the user to
  re-run `spectra ai update-triage`.

**Step 5a** — Route the re-verified tests (Specs 090, 093, 101). An edit removes the test's old critic verdict, so
**every `written` line goes through this step** — a group's, a leftover's, an "edit again", or a re-edit after "still
hallucinated". Nothing loses its verification silently: a `written` test that isn't re-verified is reported as
**unverified**. Route by the updater's verdict lines (the verdict the CLI classified):
- `verdict grounded` → add the id to the group's grounded ids;
- `verdict partial` or `verdict hallucinated` → collect the id for one repair attempt (below);
- `unverified: <reason>` → **unverified** (critic could not judge): report it with the reason and the next step (run
  this step again for it, or `spectra ai audit-grounding --suite <suite>`, where it shows as `none` / `critic`);
- `critic-disagrees` → **unverified**; report both verdicts.

Fallback — `critic: not run`, or a `written` test with no verdict line (and `verify: yes`): for each such test invoke
the `spectra-critic` subagent with the Task tool — suite + test id only, never a hand-built file path; compare its
final line, never read the verdict file — then `spectra ai ingest-verdict --suite <suite> --all --ids <id1>,<id2> --output-format json` and route
by `results` as above (no entry → the critic once more and the ingest again; still none → unverified; the critic's
line names another verdict → unverified).

Then write the grounding blocks of the group's grounded tests only (Spec 097) — skip this call when no test of the
group is `grounded`:
```
spectra ai ingest-grounding --suite <suite> --all --ids <grounded ids> --output-format json
```
An **unverified** test is never in `--ids`, so it never gets a block.

**Repair.** If any ids were collected, invoke the `spectra-repairer` subagent once with the Task tool, passing
`suite: <suite>`, `ids: <id1>,<id2>` and `verify: yes`. It patches only what the critic flagged, ingests the repairs
(keeping each test's baseline), retries a failed repair ingest at most twice, has `spectra-critic` re-verify each
repair that landed, and returns `<id> | written`, `<id> | unchanged` or `<id> | failed <error_code>: <first error>` per
ingested file, then a verdict line per `written` / `unchanged` test (or `critic: not run`). Report every line. Then:
- only for the ids it returned `written` or `unchanged` (a leftover included): route by their verdict lines as above (the
  fallback for a missing line), then
  `spectra ai ingest-grounding --suite <suite> --all --repaired --repair-attempts 1 --ids <id1>,<id2> --output-format json`
  (Spec 091: `--ids` names exactly the re-critiqued tests, so nothing else is marked repaired);
- **repair failed** — a `failed` line: report "repair failed: <error_code>", write no repaired grounding, and list the
  test as **unverified** if it was partial, or as **waiting for the user** if it was hallucinated (the choices below);
- an id with no line → report "repair outcome unknown" and leave it unverified.

After the repair, by the re-critic's verdict:
- **still partial** → it is flagged for review (its block says so); report it.
- **still hallucinated** → stop for that test and ask the user to choose: edit it again (the
  `spectra-updater` subagent with `ids: <id>`), restore the previous version from git (the user
  runs that — never run git yourself), or delete it through the `spectra-delete` skill. This skill
  never runs `spectra ai record-drop` or `spectra delete` — the test existed before the edit, so
  dropping it is the user's decision.

The critic and repair steps are never skipped silently: a test that isn't re-verified is reported as unverified.

**Step 6** — Acknowledge the tests the user reviewed and kept as they are without an edit prompt (a test returned
unchanged in Step 5 is already acknowledged), in one call:
```
spectra ai update-ack --suite <suite> --ids <id1>,<id2> --output-format json --verbosity quiet
```
Only tests the user actually reviewed. A test neither edited nor acknowledged stays in triage — nothing is deferred
silently.

**Step 7** — Set `"active":3`, then report for the suite, from the returned lines and the commands you ran: the bucket counts, COSMETIC auto-acknowledged, tests edited,
returned unchanged, acknowledged and still pending (with ids and bucket), REMOVED / CRITERIA_REMOVED left for the user,
tests failed after retries (with error codes), each `baseline:` and `warning:` line, and unbaselined tests. For the
re-verification (Step 5a), the counts with ids: grounded, partial (flagged for review), hallucinated (waiting for the
user's choice), unverified (declined or not run), and manual verdict kept.

Write `.spectra/progress.json`: `{"active":4}` (terminal — seam-progress page renders Complete).

## Update all suites

When the user says "update all", run ONE triage over every suite:
```
spectra ai update-triage --all-suites --output-format json --verbosity quiet
```
Then follow Steps 2–7 one suite at a time, with the groups of that suite (each group names its `suite`; pass that suite
to the subagents and to `update-ack`). In Step 2a, preview and relink every suite in one call each:
`spectra ai relink-sources --all-suites --dry-run …`, then, after the user's yes, the same without `--dry-run`.

## Stop

If the user says "stop", "cancel" or "stop updating": finish or abandon the current group
(`ingest-update` persists each staged test whole or not at all), don't start the next one,
and report what was edited, acknowledged and left so far. Tests not yet processed stay in
triage and are reported again next time.
