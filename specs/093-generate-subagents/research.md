# Research: Generate Skill as Orchestrator (Phase 5b)

All facts verified on `20ac624`. The spec has no open questions.

## R1 — Per-test verdicts from the batch verdict ingest (FR-016–FR-018)

**Decision**: `IngestVerdictCommand` batch mode keeps its loop and counts and adds:
- `results`: one entry per **classified** verdict file of the suite, in the loop's (sorted file) order:
  `{ "id": "TC-101", "verdict": "grounded|partial|hallucinated|manual", "score": 0.92, "gate": "pass|drop" }` — `gate`
  is the single form's `drop` flag (`hallucinated` → `drop`, else `pass`). Stale, unreadable and unparseable files stay
  in `stale` / `errors` and have no `results` entry.
- `--ids a,b`: parsed by the same helper as `ingest-grounding --ids` (091: split on `,`, trim, drop empties, distinct,
  case-insensitive). Only verdict files of listed ids are considered; listed ids not in the suite's index →
  `not_found` (always present, `[]` without `--ids`), reported even when `.spectra/verdicts/` is missing.
- Argument checks: the existing `--from and --all are mutually exclusive.`, then `--ids` without `--all` → stderr
  `--ids is only valid with --all.` (exit 1), the same message as 091's.
- The human summary (counts line, then one line per result `TC-101: grounded 0.92 (pass)` and per unknown id `TC-999:
  not found in suite 's'`) goes to stdout in human mode and to stderr in JSON mode (091's rule for the grounding batch).

**Rationale**: the skills need the verdict the CLI classified, per test, without reading files; the command already
classifies each file, so this is output only. `ingest-verdict` writes nothing, so the change has no persistence effect.

**Alternatives considered**: routing by the critic's line (the 092 review finding C3 rejects it); a new command
(rejected: FR-020, and the batch form already does the work); including unparseable files in `results` with an
`error` verdict (rejected: `results` stays "classified only", errors keep their count).

**Shared parsing**: `IngestGroundingCommand.ParseIds` moves to a small shared helper (`Commands/Generate/IdList.cs`:
`Parse`, the two usage messages) used by both commands; `ingest-grounding` behaviour is unchanged (its tests guard it).

## R2 — Analyst agent (FR-002, FR-003)

**Decision**: frontmatter as 092 (`tools: [Bash, Read, Write]`, `model: inherit`, `disable-model-invocation: true`,
`context: fork`). Procedure = today's Steps 1–3: `compile-analysis-prompt --suite --doc-suite [--focus]
--output-format json` → analyse in its own context → Write `.spectra/analysis.json` → `ingest-analysis --suite --from
.spectra/analysis.json --output-format json`; exit 5/6 → re-analyse, at most 2 retries.
- **Batches**: from its own behaviour list (the ingest reports counts only): group by `source`, at most 8 behaviours
  per batch in the document's order; each batch's count is its share of `recommended` (proportional to its behaviour
  count, largest-remainder rounding, batches with count 0 dropped), so the counts add up to `recommended`. The focus is
  `<source doc>: <behaviour title>; <behaviour title>; …`.
- **Refusal** (exit 4, pre-flight budget): return `refused: <error_code>` and the message's candidate doc suites and
  remedies as `refusal:` lines, verbatim — no recommendation.
- `recommended == 0` → no batch lines.

## R3 — Generator agent (FR-004)

**Decision**: frontmatter as R2. Procedure = today's Steps 5–7 for one batch: `compile-prompt --suite --count
--focus --output-format json` → generate in its own context, reading documents under `docs/` as needed → Write
`.spectra/generated.json` → `ingest-tests <suite> --from .spectra/generated.json --output-format json`; exit 5/6 →
regenerate for the specific error, at most 2 retries; exit 1 → `failed <code>` (with the persisted ids first if the
failure says some were persisted).
- **Returns**: `<id> | <title>` per persisted id (titles from its own generated JSON), a `baseline: <summary>` line,
  and the ingest's `warnings` as `warning:` lines. The ingest has no `notes` field (the request's "notes" are its
  `warnings` and baseline summary — what the user saw before).

## R4 — Repairer change (FR-013, the 092 C1 finding)

**Decision**: the final message has one line per entry in `results` over its ingest calls (a retry replaces the earlier
line), so leftover `repaired-*` files are reported by id; plus a `failed <code>` line for an input id whose compile was
refused (it has no `results` entry). Every input id therefore still has a line.

## R5 — Generation skill rewrite (FR-006–FR-012)

**Decision**:
- **Context budget contract** after the overview (as 092): never reads docs, compiled prompts, test files, generated /
  staged JSON or verdict files; main's calls are `spectra` commands with small JSON (incl. `ingest-verdict --all
  --ids`, `ingest-grounding`, `audit-grounding`, `record-drop`, `spectra delete`), progress writes, Task calls.
- **NON-STOP contract (Steps 7–9)** and the **written==0 guard** stay verbatim (075 gate); the contract text is
  re-read during implementation and any line that tells the main session to do delegated work is moved, not reworded,
  only if a guard test allows it — otherwise reported as a deviation.
- **Steps 1–3** → Task `spectra-analyst` {suite, doc_suite, focus}; a `refused:` answer → ask the user (narrow the doc
  suite / raise the budget), as today. **Step 4** → present from the lines; STOP.
- **Steps 5–7** → per batch, in order, Task `spectra-generator` {suite, focus: "<user focus>; <batch focus>", count};
  an explicit user count is split over the batches proportionally (same rounding as R2). Collect ids and titles;
  report every line; a `failed` batch is reported and the next runs.
- **8a** → critic per created id (Task; its line is progress only); then `ingest-verdict --suite S --all --ids <created
  ids> --output-format json`; route by `results`: grounded → kept (kept-grounded count), partial → repair list,
  hallucinated → `record-drop --from .spectra/verdicts/critic-verdict-{id}.json` + `spectra delete` (the CLI reads the
  file; main doesn't); no result, or a critic line that disagrees → unverified. Then the existing `ingest-grounding
  --all` and the guard, verbatim.
- **8b** → Task `spectra-repairer` {suite, partial ids}; landed ids (`written` / `unchanged`) → critic per id →
  `ingest-verdict --all --ids <landed>` → hallucinated dropped → `ingest-grounding --all --repaired --repair-attempts 1
  --ids <landed minus dropped>`; `failed` → reported, unverified; any other `written` line (a leftover) → the same
  re-critic path (FR-014).
- **Resume** → `audit-grounding --suite S` → ids with `action_needed: repair` → the repairer.
- **Step 9** → from the lines and CLI results: every created test by id and title with its outcome.
- The CLI reference table loses "Read `test-cases/{suite}/{id}.md` directly" and `compile-repair-batch`'s row moves to
  "not used by this skill"; `ingest-verdict --all` gains `--ids` / `results`.
- The from-description flow is unchanged.

## R5a — A test the critic could not judge (decision 2026-10-09, analysis C1)

**Decision**: the pre-093 rule in 8a step 5 ("regenerate that single test (bounded 2), never keep an unverified
test") is replaced. A test with no CLI verdict (the critic's compile refused, its verdict JSON still damaged, or no
`results` entry) gets the critic once more; if there is still no CLI verdict, it is kept as **unverified**: never
counted as kept-grounded, never given a grounding block, never regenerated or deleted, and listed in Step 9 under
"Unverified (critic could not judge)" with id, title, reason and the next step (the critic step again for these ids,
or `audit-grounding`, where it shows as `none` / `critic`).

**Rationale**: since 090 an unverified test is visible (audit-grounding lists it); a damaged verdict is the critic's
failure, not the test's; regenerating in the main session would break the context budget contract.

**Alternatives considered**: regenerating through the generator (more steps, and it replaces a test the critic never
judged); keeping the old rule (contradicts FR-015 and the contract).

## R6 — Update skill changes (FR-014, FR-015)

**Decision**: Step 5a routes by `ingest-verdict --suite S --all --ids <ids sent to the critic>` `results` (not the
critic's line; disagreement or no result → unverified). The re-verify set is every `written` line (not `manual verdict
kept`) received during the suite's run — from a group, a leftover attributed by id, an "edit again", or a re-edit after
"still hallucinated" — each re-verified when it arrives, or listed unverified if the user declined re-verification.

## R7 — Moved assertions

**Decision**: as in 092 R8 — generate rows that describe work now done by an agent are retargeted to that agent with
the same strings (whitespace-normalised); rows about main-session behaviour stay on the skill; the written==0 guard and
NON-STOP rows stay verbatim on the skill. `AgentContent_HasAllAgents` 4 → 6. Inventory in tasks.md.

## R8 — Verification (083 lesson)

**Decision**: install rows via the real `spectra init` (with skills); a pre-093 workspace = a real init with the two new
agent files and manifest entries removed and a user-modified `spectra-generate` SKILL.md → `update-skills` adds the two
agents and skips the skill. CLI rows for `ingest-verdict` run on `VerdictWorkspace` from the real init.

## R9 — Manual acceptance (SC-001, SC-002)

**Decision**: `acceptance/setup-workspace.sh` builds a scratch workspace from the real init with docs rich enough for
≥ 24 tests in ≥ 3 batches; the user runs the generation in Claude Code with `/context` before/after, and compares the
grounded rate with the same run on the 092 build (same model). These runs need a live session.
