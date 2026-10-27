---
title: Usage Guide
parent: User Guide
nav_order: 9
---

# SPECTRA Usage Guide

This guide shows how to use SPECTRA through Claude Code. Every workflow below shows what to say and what to expect. For configuration and customization details, see [Customization](customization.md).

## Prerequisites

1. [Claude Code](https://claude.com/claude-code), installed and signed in.
2. `spectra init` completed in your project (creates SKILL files in `.claude/skills/` and the subagents in `.claude/agents/`: the critic; the updater, repairer and extractor the update and criteria skills delegate to; and the analyst and generator the generate skill delegates to). On an existing project, `spectra update-skills` adds any that are missing; a customised generate, update or criteria skill keeps its older, in-session flow until you refresh it.
3. Documentation files present in `docs/` (the source material for test generation and acceptance criteria).

## Getting Started

Open Claude Code in your project and just ask. The bundled skills match natural-language requests to the right workflow automatically, so there's no menu to pick from. If you don't know where to start, ask "help me get started" and the bundled `spectra-quickstart` skill will walk you through the options.

---

## Generating Test Cases

The most common workflow. Three levels of specificity:

**Basic:**

> "Generate test cases for checkout"

**With count and focus:**

> "Generate 15 test cases for checkout focusing on error handling"

**Analysis only (review before generating):**

> "Analyze the checkout docs and tell me what test cases you'd generate"

### What to expect

1. The analysis phase identifies testable behaviors — each with the doc section it comes from — and shows existing coverage, plus the batch plan the CLI saved: each batch groups up to `generation.batch_size` (default 8) behaviors from neighbouring sections of one document, every batch gets at least one test, and behaviors whose source couldn't be found are listed and generated in an `unsourced` batch. Behaviors an existing test already covers (its title is at least `generation.duplicate_threshold`, default 0.6, similar) are listed as "already covered — not generated", with the covering test, and are left out of every batch; the recommended count is the uncovered behaviors.
2. You approve the count, and generation runs batch by batch. Each batch's prompt already contains its doc sections and their acceptance criteria, so the generator doesn't browse the docs. If a batch's sections changed after the analysis (`PLAN_STALE`) or are too large for one prompt (`PROMPT_TOO_LARGE`), the assistant reports the batch and offers to re-run the analysis; you approve the new plan before anything more is generated. A customised `test-generation.md` from before this change still works: those batches are compiled the old way (the generator reads the docs) and the assistant tells you once to run `spectra prompts reset test-generation` (or `spectra update-skills` for an unedited copy).
3. Each generated test is verified by the `spectra-critic` subagent before it's accepted; partial tests get one repair. The critic sees the sections each test cites, not whole documents (Spec 101); for one test the sections don't explain, `spectra ai compile-critic-prompt --suite <s> --test <id> --full-docs` shows it the whole documents.
4. The results list every test created, by id and title, with its outcome (kept, repaired, flagged, dropped, or unverified when the critic couldn't judge it).
   The analysis, each batch and the repairs run in subagents (`spectra-analyst`, `spectra-generator`, `spectra-repairer`), so the documents, prompts and test text never fill your conversation; the assistant relays every line they return. Since Spec 101 the generator and the repairer also start the critic for the tests they wrote and return one verdict line per test, so the per-test critic runs stay out of your conversation too. Each test's route follows the verdict the CLI classified (`ingest-verdict --all --ids`), not the critic's own summary. If nested subagents are turned off (`CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1`), the subagent reports `critic: not run` and the assistant runs the critic per test itself.
5. Optionally, suggestions for additional coverage areas are included.

### Follow-up prompts

After generating, you can continue:

> "Generate the suggested test cases"
> "Generate suggestions 1 and 3"
> "Create a test case for user session timeout after 30 minutes"

---

## Extracting Acceptance Criteria

> "Extract acceptance criteria from my documentation"
> "Re-extract all criteria (force full extraction)"

### What to expect

1. Each document is processed individually (no truncation), and only the documents `spectra docs changed` reports as new or changed since the last extraction are re-processed (SHA-256 content hashing).
2. Results are reported per document with criteria counts grouped by RFC 2119 level (MUST/SHOULD/MAY).
3. Files created: one criteria file per document, mirroring its path (`docs/a/login.md` → `docs/criteria/docs/a/login.criteria.yaml`), and a master `docs/criteria/_criteria_index.yaml`.
4. **IDs stay stable.** When a document changes, the assistant sees its existing criteria and keeps each criterion's ID when it still expresses the same requirement, even if reworded. A removed criterion's ID is never reused for something else.
5. **You see what changed.** Each document's refresh reports criteria added, modified (old → new text) and removed, and for each removed criterion the tests that still reference it.
   Each changed document is extracted by a `spectra-extractor` subagent in its own context, so the documents never fill your conversation; the assistant relays every line it returns — the outcome and the full changeset — and builds the summary from them.
6. **Deleted and moved documents.** A document moved without changes keeps its criteria and IDs. A deleted document is shown with the criteria that would be removed; its criteria are only retired after you confirm.
7. **Inconclusive extractions are retried, not recorded.** If a document's extraction comes back empty or unparseable, `ingest-criteria` writes nothing (exit 5 or 6) and the assistant retries it once. A document that stays inconclusive is reported and shows as changed again next time instead of being silently skipped. A genuine "no criteria found" result (a real, valid empty list) is recorded normally.
8. To re-extract every document, ask for a full re-extraction ("Re-extract all criteria"); the skill then includes unchanged documents (`spectra docs changed --include-unchanged`).

### Troubleshooting

`spectra docs index` only indexes; it never extracts criteria. If generation refuses with "No acceptance-criteria context was resolved for this suite", extract the criteria first ("Extract acceptance criteria from my documentation"), then generate again. A generation batch whose doc sections have no criteria of their own uses all criteria of its documents, with a warning.

### Follow-up prompts

> "List the high priority criteria for checkout"
> "Which MUST criteria don't have test cases yet?"

---

## Importing Criteria from External Tools

> "Import acceptance criteria from jira-export.csv"
> "Import criteria from sprint-42.yaml and replace existing"

Supported formats: CSV (with auto-detection of Jira/ADO column layouts), YAML, and JSON. Imports merge by default; pass replace mode if you want to overwrite the target file.

---

## Coverage Analysis

> "Show me coverage gaps"
> "Analyze coverage and link test cases to automation code"

Three dimensions are reported:

- Documentation coverage measures docs ↔ test cases linkage.
- Acceptance criteria coverage measures criteria ↔ test cases linkage.
- Automation coverage measures test cases ↔ code linkage via the `automated_by` frontmatter field.

The optional auto-link mode writes `automated_by` back into your test case files when it can resolve a match.

---

## Generating Dashboard

> "Generate a coverage dashboard"
> "Generate a dashboard preview with our branding"

Opens an interactive HTML dashboard with suite stats, coverage visualizations, and drill-down details. The preview mode renders sample data so you can verify branding and theme settings before running it on real data.

---

## Validating Test Cases

> "Validate my test case files"
> "Validate the checkout suite"

Checks: required frontmatter fields, unique test IDs, valid priority enum values, valid tags, dependency resolution.

---

## When documentation changes

> "The payment docs changed — update the checkout tests"

This is the whole flow from "the docs changed" to tests that are edited, re-verified and acknowledged. Two skills
drive it: `spectra-criteria` brings the acceptance criteria up to date, then `spectra-update` brings the tests up to
date. Everything that decides *what* changed is a deterministic command with no model; the model only edits the tests
a change actually touches.

SPECTRA keeps, for every test, a fingerprint of each doc section it references (`source_refs`, e.g.
`docs/checkout.md#Payment`) and each acceptance criterion it links (`criteria`), in
`test-cases/{suite}/_source-baseline.json`. They are recorded automatically when a test is generated
(`ingest-tests`) or edited (`ingest-update`), and refreshed when you acknowledge a test (`update-ack`).

1. **Docs changed** — `spectra docs changed` lists the documents that are new, changed, moved (same content, new
   path) or deleted since their criteria were last extracted. It only reads; nothing is written.
2. **Criteria** — ask "Extract acceptance criteria for the changed docs". The `spectra-criteria` skill applies moves
   (`spectra docs apply-move --from <old> --to <new>`: the criteria and their IDs follow the document), shows each
   deleted document with the criteria that would go and retires them only after you confirm (`spectra docs retire
   --doc <path>`), and re-extracts each changed document in a `spectra-extractor` subagent. Re-extraction keeps each
   criterion's ID when it still expresses the same requirement, even reworded, and never reuses a removed ID. Each
   document's changeset (added, modified old → new, removed with the tests that still reference them) is shown to you.
3. **Triage** — ask "Update the checkout tests" (or "update all suites"). The `spectra-update` skill runs
   `spectra ai update-triage --suite checkout`, which compares each test's sections and criteria with its baseline,
   with no model (git is only read, to find renamed docs). Every test lands in a bucket:
   - **RELINK** — a referenced doc file was renamed or moved, and git history says where to;
   - **LOAD** — a number, a modal word (must, should, may, cannot, required, only, not) or many words changed;
   - **CRITERIA_CHANGED** — a linked criterion was reworded;
   - **LIGHT** — a small wording change;
   - **COSMETIC** — markup, whitespace or case only; acknowledged automatically (`--no-auto-ack` to report them);
   - **REMOVED / CRITERIA_REMOVED** — the referenced section or criterion is gone;
   - **UNCHANGED**, and **UNBASELINED** for tests with no baseline yet.

   Tests that share the same changes are grouped (`G1`, `G2`, …), and each group says why (`numbers: 5000→3000`).
   Sections of a changed doc that no test references are listed as generation hints.
4. **Relink** — for RELINK tests the assistant previews `spectra ai relink-sources --suite checkout --dry-run`
   (old → new ref per test), asks you, and only on your yes runs it for real, then triages again. Only the ref's path
   changes (the `#anchor` is kept); the baseline entry moves with its old fingerprint, so anything the rename commit
   also changed shows as LOAD or LIGHT in the right section. `--min-similarity 60` (50–100) follows renames that came
   with heavier edits. A ref into a git submodule that isn't checked out is reported as REMOVED with `submodule not
   checked out: <path> (run git submodule update --init)` — run that yourself, then triage again.
5. **REMOVED review** — the assistant lists REMOVED and CRITERIA_REMOVED tests for you to decide: point the ref at the
   new section, drop the ref, or delete the test if the feature is gone (`spectra delete <ids> --dry-run`, then
   without `--dry-run`; it also cleans `depends_on`, baseline entries and staging files). It never edits or deletes
   those tests on its own.
6. **Group edits** — you choose what to edit: LOAD only (recommended; includes CRITERIA_CHANGED), LOAD + LIGHT, chosen
   groups, or report only. Each group goes to a `spectra-updater` subagent, so prompts and test text never fill your
   conversation. It runs `spectra ai compile-update-prompt --suite checkout --group G1` — one prompt with each
   referenced doc section once (as it is now, the previous version and the words that changed), only the criteria the
   tests link (marked when changed, not baselined or removed), then each test — edits the tests, stages one file per
   test (`.spectra/updates/checkout/updated-<id>.json`, exactly one test each) and runs
   `spectra ai ingest-update checkout --all`. The ingest keeps each test's id and manual fields, fails loud on drift
   the doc change doesn't explain, and records the new baseline. A failed file is kept as `failed-<id>.json` and
   retried at most twice; a batch with a failed file exits 8. A group too big for one prompt (`PROMPT_TOO_LARGE`, over
   `analysis.max_prompt_tokens`) is edited test by test. `ingest-update checkout --all --dry-run` shows what a batch
   would do without writing anything.
7. **Unchanged and pending** — a test the model returns unchanged is not written. After a small change (LIGHT,
   COSMETIC) it counts as reviewed and its baseline is rebuilt (`unchanged`). After a LOAD or CRITERIA_CHANGED change it
   is reported as `unchanged_pending`: nothing is recorded, and the assistant asks you whether to acknowledge it or
   edit again — it never acknowledges on its own. A REMOVED, CRITERIA_REMOVED or RELINK change seen at this point means
   the docs changed again since triage; the assistant asks you to re-run triage.
8. **Re-verify** — an edit removes the test's critic verdict (a Manual verdict is kept and reported), so an edited test
   never carries a verdict given to different content; `audit-grounding` shows it as unverified until it is checked.
   The assistant asks once per run, before the first group is edited, whether to re-verify (default yes); the
   `spectra-updater` subagent then runs the `spectra-critic` subagent on each test it wrote and classifies the verdicts
   with `spectra ai ingest-verdict --suite checkout --all --ids <ids>`. The route follows the CLI's classification, not
   the critic's own summary: grounded is done (`spectra ai
   ingest-grounding --suite checkout --all --ids <grounded ids>` writes the blocks); partial gets one repair by a
   `spectra-repairer` subagent and is flagged for review if still partial; hallucinated gets one repair, and if still
   hallucinated the assistant stops for that test and asks you — edit again, restore it from git, or delete it. A
   verdict for content that changed after the critic saw it is refused (`VERDICT_STALE`). The update flow never drops
   or deletes a test on its own.
9. **Acknowledge** — tests you reviewed and keep as they are get `spectra ai update-ack --suite checkout --group G2`
   (or `--ids TC-104,TC-107`), which refreshes their baseline so they leave triage. A change you neither edit nor
   acknowledge is reported again next time; git is the undo for a wrong acknowledgement.
10. **Committed vs scratch** — commit the files that record the result; the rest is scratch that `spectra init` (new
    workspaces) and `spectra update-skills` (existing ones) gitignore:

    | Path | Committed? |
    |------|-----------|
    | `test-cases/<suite>/*.md`, `_index.json`, `_source-baseline.json` | yes |
    | `docs/criteria/**` (incl. `_criteria_index.yaml`), `docs/_index/**` | yes |
    | `.spectra/source-snapshots/` (lets triage show word diffs) | yes |
    | `.spectra/update-triage.json` | no (scratch) |
    | `.spectra/updates/`, `.spectra/repairs/` | no (scratch) |
    | `.spectra/verdicts/`, `.spectra/dropped-tests.json` | no (scratch) |
    | `.spectra/generation-plan/` | no (scratch) |
    | `.spectra/baseline.lock`, `.spectra/criteria.lock` | no (scratch) |

    Keep the `# spectra-offered: …` lines in `.gitignore` when you commit it: they record which patterns SPECTRA
    already offered, so one your team removed stays removed in every clone.

An edited `USAGE.md` / `CUSTOMIZATION.md` is never overwritten by an upgrade; to get the new version, delete it and run
`spectra update-skills`. An edited prompt template is refreshed with `spectra prompts reset <id>` (e.g.
`spectra prompts reset test-update`).

### Worked example

The checkout docs changed in three ways: a limit in `docs/payments/checkout.md` went from 5000 to 3000 EUR, the
refunds page was renamed in git (`git mv docs/payments/refunds.md docs/payments/refund-policy.md`), and the "Gift
cards" section was deleted. Outputs are abbreviated.

```bash
spectra docs changed
```

```
  [changed  ] docs/payments/checkout.md  (component: checkout)
  [moved    ] docs/payments/refund-policy.md  (from: docs/payments/refunds.md)
```

The criteria skill applies the move, then re-extracts the changed document (a model turn in a `spectra-extractor`
subagent between the compile and the ingest):

```bash
spectra docs apply-move --from docs/payments/refunds.md --to docs/payments/refund-policy.md --dry-run
spectra docs apply-move --from docs/payments/refunds.md --to docs/payments/refund-policy.md
spectra ai compile-extraction-prompt --doc docs/payments/checkout.md
spectra ai ingest-criteria --doc docs/payments/checkout.md --from .spectra/criteria.json
```

```
Persisted 1 criterion(s) for 'docs/payments/checkout.md': AC-CHECKOUT-001
Criteria changeset for docs/payments/checkout.md
  ~ AC-CHECKOUT-001  "A payment above 5000 EUR must be confirmed." → "A payment above 3000 EUR must be confirmed."
  - AC-CHECKOUT-002  "Gift cards can pay for any order."  (still referenced by 1 test: TC-109)
```

Then the update skill triages the suite:

```bash
spectra ai update-triage --suite checkout
```

```
Update triage — checkout (4 tests)
  RELINK 1 · REMOVED 1 · CRITERIA_REMOVED 0 · LOAD 1 · CRITERIA_CHANGED 0 · LIGHT 0 · COSMETIC 1 (auto-acked) · UNCHANGED 0
  G1   RELINK           1 test    docs/payments/refunds.md#partial-refunds   renamed to docs/payments/refund-policy.md
  G2   REMOVED          1 test    AC-CHECKOUT-002, docs/payments/checkout.md#gift-cards   anchor not found; criterion removed: AC-CHECKOUT-002
  G3   LOAD             1 test    AC-CHECKOUT-001, docs/payments/checkout.md#cards   numbers: 5000→3000; criterion changed: AC-CHECKOUT-001
  Auto-acknowledged 1 cosmetic ref(s).
```

(TC-107's `#limits` section only lost its trailing blank line when the section below it was deleted — COSMETIC,
acknowledged automatically.)

It previews and (on your yes) applies the relink (`checkout/TC-104  docs/payments/refunds.md#partial-refunds →
docs/payments/refund-policy.md#partial-refunds`), then triages again — TC-104 is now UNCHANGED:

```bash
spectra ai relink-sources --suite checkout --dry-run
spectra ai relink-sources --suite checkout
spectra ai update-triage --suite checkout
```

You decide TC-109 (the gift-card test) is obsolete and delete it:

```bash
spectra delete TC-109 --dry-run
spectra delete TC-109
```

After the delete, the triage has one group left — `G1 LOAD` for TC-101. You choose "LOAD only". A `spectra-updater`
subagent compiles the group's prompt, edits TC-101 and stages it, then ingests the group:

```bash
spectra ai compile-update-prompt --suite checkout --group G1
spectra ai ingest-update checkout --all
```

The staged file holds exactly one test, in the generation format (abbreviated):

```json
[{ "id": "TC-101", "title": "Card payment above 3000 EUR requires confirmation", "priority": "high",
   "steps": ["Pay 3001 EUR by card"], "expected_result": "A confirmation step is shown",
   "source_refs": ["docs/payments/checkout.md#cards"], "criteria": ["AC-CHECKOUT-001"] }]
```

```
Batch update-ingest for suite 'checkout': written=1 unchanged=0 unchanged_pending=0 skipped_no_original=0 errors=0
  TC-101: updated
```

The critic re-verifies TC-101 (its verdict file `.spectra/verdicts/critic-verdict-TC-101.json` holds the verdict,
score and findings, e.g. `{"verdict": "grounded", "score": 0.93, "findings": [...]}`), and the CLI classifies it and
writes the grounding block:

```bash
spectra ai compile-critic-prompt --suite checkout --test TC-101 --docs docs/payments
spectra ai ingest-verdict --suite checkout --all --ids TC-101
spectra ai ingest-grounding --suite checkout --all --ids TC-101
```

```
Batch verdict-ingest for suite 'checkout': grounded=1 partial=0 hallucinated=0 errors=0
TC-101: grounded 0.93 (pass)
Batch grounding-ingest for suite 'checkout': written=1 skipped_already_written=0 skipped_partial_pre_repair=0 errors=0
```

Nothing is left for review, so the run ends. Had you kept a test as it is, `spectra ai update-ack --suite checkout
--ids TC-107` would have taken it out of triage. You commit `test-cases/checkout/`, `docs/criteria/` and
`.spectra/source-snapshots/`; the staging files and verdicts stay local.

### Before the first update

**Existing workspaces.** A workspace from before baselines existed has none, so every test shows as UNBASELINED.
Run `spectra ai source-baseline --init --all-suites` once: it adopts the current docs as the baseline.

**Adopting with history.** Adopting today's docs hides every doc change made since the tests were written. In a git
repository you can start the baselines in the past instead:

```bash
spectra ai relink-sources --all-suites --dry-run       # first: refs to docs that were renamed since
spectra ai relink-sources --all-suites
spectra ai source-baseline --init --all-suites --from-date 2026-03-01
spectra ai update-triage --all-suites                  # changes since 2026-03-01 show as LOAD / LIGHT
```

Relink first, so the bootstrap follows each doc back through its renames to the old content. Add
`--min-similarity 60` (50–100) to follow renames that came with heavier edits; if git's history can't be read, the
affected refs stay unbaselined with `git history unavailable: …` instead of being read from the wrong path.
`--from-date` uses each repository's (and submodule's) last commit at or before the date; `--from-ref <rev> --owner
<path>` pins one repository to a commit instead. It only fills missing entries; refs whose file or section didn't
exist then stay unbaselined, with the reason.

**Where are my docs?** `spectra docs topology` lists the repositories (including submodules) that own the referenced
docs, and the docs in none — the reason relinking or a history bootstrap isn't available for them. SPECTRA only ever
reads git; without git everything works as before and missing files are REMOVED with `not found; no git`.

**Repairs during generation.** Critic repairs during generation or flagged-test review keep the test's baseline
(`ingest-update --keep-baseline`), so a pending doc change is still in triage afterwards. They are staged in
`.spectra/repairs/<suite>/` and ingested with `ingest-update <suite> --all --keep-baseline`, which reads only that
folder; the update edit step reads only `.spectra/updates/<suite>/`.

### Legacy word-overlap classifier

`spectra ai update` (legacy) classifies every test case in the suite as **UP_TO_DATE**, **OUTDATED**, **ORPHANED**, or
**REDUNDANT** by word overlap with the *current* docs (deterministic, no model call). It has no record of what the docs
said when a test was written, so a change that keeps the vocabulary (`10GB` → `50GB`) stays UP_TO_DATE — which is why
triage replaced it. No skill uses it any more. With `--dry-run`, or `--no-interaction` alone, it only classifies and
reports; to mark orphaned tests from a script, add `--no-review` (marking changes only a test's `status`,
`orphaned_reason` and `orphaned_date`).

---

## Verdict Disposition

The critic assigns every generated test a verdict of **grounded**, **partial**, or **hallucinated**, and those verdicts are made durable and visible, not just used to accept/reject in the moment.

### What happens automatically (in-batch)

| Verdict | Automatic action |
|---------|-----------------|
| **grounded** | Condensed `grounding:` block written into the test's `.md` frontmatter (`verdict: grounded`, `score`, `verified_at`). Full JSON in `.spectra/verdicts/critic-verdict-{id}.json`. |
| **partial** | One bounded repair attempt (compile-repair → agent patches → re-critic). If upgraded to grounded: grounded block + `repaired: true`. If still partial: partial block with condensed findings + `flagged_for_review: true`. Batch continues without prompting. |
| **hallucinated** | Trail entry appended to `.spectra/dropped-tests.json` (`id`, `reason`, `contradicting_claim`, `timestamp`). Then clean three-phase delete (index entry removed, `depends_on` stripped, file deleted). |

The batch report shows four counts: kept-grounded, repaired-to-grounded, flagged-partial, dropped-hallucinated.

### Human review phase (for flagged tests)

After a batch run, review tests still marked `flagged_for_review: true`:

> "Review flagged tests for the checkout suite"

The `spectra-review-flagged` skill lists each flagged test with its condensed verdict and lets you choose per test:
- Accept as-is, which clears `flagged_for_review` and keeps the partial block as acknowledged.
- Retry repair, which runs one more bounded repair cycle.
- Delete, which writes a `user_decided` trail entry and then performs the clean three-phase delete.

To list flagged tests without an interactive session:

```bash
spectra ai review-flagged --suite checkout --no-interaction --output-format json
```

### What gets persisted where

| Store | grounded | partial | hallucinated |
|-------|----------|---------|--------------|
| `test-cases/<suite>/<id>.md` | block added | block added (flagged) | file deleted |
| `_index.json` entry | kept | kept | removed |
| `.spectra/verdicts/critic-verdict-{id}.json` | written | written | written (then test deleted) |
| `.spectra/dropped-tests.json` trail | — | — | entry appended |

`.spectra/verdicts/` and `.spectra/dropped-tests.json` are gitignored scratch, meaning they survive the run but are not committed.

### Repair Batch & Resume

When a batch has many partials, the `spectra-generate` skill runs a **manifest-driven** repair loop that completes reliably across sessions, batching the mechanical steps instead of looping per test:

1. `spectra ai compile-repair-batch --suite <s>` reads all partial verdict JSONs, filters to those still missing a grounding block, and compiles every repair prompt into one JSON manifest, deterministically and with no model call. A test whose verdict is stale (it changed since the critic saw it, or the critic prompt was compiled again) gets no prompt; it is listed on stderr and needs the critic again (Spec 097).
2. For each manifest entry, the assistant patches the test in-session and writes the corrected test JSON to `.spectra/repairs/<suite>/repaired-<id>.json`.
3. Once all repairs are staged, they're ingested in one batch call: `spectra ai ingest-update <suite> --all --keep-baseline`. A repair that fails is kept as `failed-<id>.json` and retried at most twice.
4. Each repaired test that landed (`written` or `unchanged`) is re-verified by the `spectra-critic` subagent (started by the `spectra-repairer` subagent since Spec 101), then all resulting verdicts are classified in one batch call: `spectra ai ingest-verdict --suite <s> --all --ids <ids>`.
5. The grounding blocks of exactly those re-critiqued tests are written in one batch call: `spectra ai ingest-grounding --suite <s> --all --repaired --ids <ids>`. `--ids` is required with `--repaired`, so a test whose repair failed, or a partial verdict left over from an interrupted run, is never marked repaired.
6. If interrupted or the session runs out, re-running from step 1 picks up exactly where it left off. The grounding block already written into a test's `.md` frontmatter is the resume checkpoint, so nothing is double-processed or restarted from scratch.

To check resume state or inspect grounding status directly:

```bash
spectra ai audit-grounding --suite smoke --action repair --output-format json   # only the tests still to repair
spectra ai audit-grounding --suite smoke                                         # the whole suite, stale verdicts marked
spectra ai compile-repair-batch --suite smoke                                    # re-run: emits only the remaining ungrounded partials
```

The audit lists only the suite's own tests, and marks a verdict the ingest would refuse as `critic (stale)` — run the
critic again for it. The skills write grounding blocks only for the tests the critic confirmed (`--ids`), so a test
reported "unverified" never gets one (Spec 097).

What each test needs comes from its verdict (Spec 098): a confirmed (`grounded`) test whose block isn't written yet is
`ground` — it only needs `ingest-grounding --all --ids`, never a repair; a `partial` test is `repair`; a `hallucinated`
test is `review` — it waits for your decision and is never repaired automatically. When the generate skill resumes an
interrupted run it asks the audit for `--action ground,repair,review` and handles each list that way:

```bash
spectra ai audit-grounding --suite smoke --action ground,repair,review --output-format json
```

**Choosing the critic's model.** The critic runs on `ai.critic.model` (default `sonnet`), independent of the model you
generate with. Set it in `spectra.config.json` (an alias such as `opus`, a model id, or `inherit` for your session's
model) and run `spectra update-skills`; an agent file you edited by hand is never overwritten.

---

## Executing Tests

> "Run the smoke test selection"
> "Execute the checkout suite"

The execution skill orchestrates: it starts the run, launches the local web console, and hands you the URL, and you record each PASS/FAIL/SKIP/BLOCKED verdict in the browser console (see [Driving a Run from the Web Console](#driving-a-run-from-the-web-console) below); finalize then generates an HTML report. Saved selections (like `smoke`) live in `spectra.config.json` and let you run a curated subset without naming individual test IDs.

Execution is CLI-only (`spectra run`, driving `.execution/spectra.db`), so there is no server to run or configure.

---

## Driving a Run from the Web Console

> "Open the execution console"

For hands-on manual runs, `spectra run console` serves a **local** web page (in Spectra's report
styling) where you drive the run from the browser: click PASS / FAIL / BLOCKED, add a comment, drop or
paste a screenshot, and the page advances. SQLite is the source of truth, so a refresh loses nothing.

```bash
spectra run start checkout          # start (or use an active run)
spectra run console                 # prints a http://127.0.0.1:<port>/ URL — open it
# … record verdicts in the browser; click Finalize when done …
spectra run console --stop          # stop the detached server
```

No server config, no tokens, no network beyond localhost. The console runs detached (survives closing the
launching terminal) and the page is ephemeral/gitignored. See [CLI Reference](cli-reference.md) for the full command and
[Deployment](deployment.md) for how it differs from the static dashboard.

---

## Creating a Custom Profile

> "Create a test generation profile for our API team"
> "Set up a custom test format with request/response fields"

Generation profiles control test output format, including frontmatter fields, step structure, and custom keys. The bundled init flow seeds `profiles/_default.yaml`; you can copy it to a named profile and customize it. Set the active profile in `spectra.config.json` under `ai.profile`. See [Customization](customization.md) for the full reference.

---

## Indexing Documentation

> "Index all documentation"
> "Rebuild the docs index from scratch"

Catalogs every document in `docs/` with metadata (sections, entities, tokens, content hashes). Required before first generation; run it again after the docs change (it is incremental). The force flag rebuilds from scratch ignoring hashes. Indexing never extracts acceptance criteria — that is the criteria workflow above.

---

## Customizing SPECTRA

> "How do I customize test generation quality?"
> "Show me how to focus on security testing"

There are four customization surfaces:

1. Prompt templates in `.spectra/prompts/*.md` control AI reasoning for analysis, generation, criteria extraction, critic verification, and updates.
2. Generation profiles in `profiles/` control test output format.
3. Behavior categories in `spectra.config.json` control what kinds of behaviors the analyzer considers.
4. Focus filtering lets you pass runtime focus via natural language, such as "focusing on security and error handling".

For the complete customization reference, see [Customization](customization.md).

---

## Troubleshooting

### A command appears stuck or waiting for input

All bundled SKILLs pass non-interactive flags. If you see a hang, your installed SKILL files may be out of date, so refresh them with `spectra update-skills`. Customizations you've made to SKILL files are preserved by hash tracking. `update-skills` never overwrites a file you edited — a skill, agent, prompt template, `profiles/_default.yaml` or `CUSTOMIZATION.md` — and a file it has no record of is only refreshed when it already equals the bundled copy; otherwise it says `skipped (no record — it may have been edited)` and how to get the new version (`spectra prompts reset <id>`, or delete the file and run `update-skills` again).

### Stale terminology in your installed SKILLs

Same fix: refresh installed SKILL files with `spectra update-skills`.

### Dashboard coverage section empty

Run a coverage analysis first, then re-generate the dashboard. The dashboard reads coverage data; without a recent run it has nothing to show.

### No acceptance criteria found

Index the documentation first, then extract criteria. The order matters because extraction reads the index.

### Test cases aren't linked to acceptance criteria

Re-generate after extracting criteria. Generation auto-loads related criteria as prompt context and writes the linkage into the test case frontmatter.

### Filter ignored / the whole suite ran

If you asked to run only high-priority (or tagged/component) tests but the whole suite started, you were almost certainly sending the filter in the wrong shape. Use the **canonical top-level arrays** shape:

```json
{ "suite": "checkout", "priorities": ["high"], "tags": ["smoke"], "components": ["payments"] }
```

Values within one array are OR'd; different arrays are AND'd. A misplaced field (top-level singular `priority`, or a nested `filters: { ... }` wrapper) no longer slips through silently. It returns an error that names the field and suggests the correct one, so the next attempt lands on the right shape. The legacy nested `filters` object is still accepted but deprecated.

> Keep in mind that run-mode tag matching is **OR-within-array** (any listed tag), so if you need "match all of these tags," split it into separate runs or filter the results afterward.

---

## Complete Pipeline: Start to Finish

For a brand-new project, the recommended sequence is:

1. Initialize SPECTRA by running `spectra init`, which bootstraps config, SKILLs, agents, and profiles.
2. Index the documentation to build the doc catalog.
3. Extract acceptance criteria to pull testable criteria out of the docs.
4. Generate test cases, asking the assistant to generate and review for each suite.
5. Validate the test cases to catch frontmatter and ID issues.
6. Ask for coverage to identify gaps across docs, criteria, and automation.
7. Generate a dashboard for a visual report.
8. Execute a suite with an interactive run that reports PASS/FAIL/SKIP/BLOCKED.

Each step is independent and can be re-run as the underlying docs and test cases evolve.
