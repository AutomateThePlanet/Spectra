# Feature Specification: Generate Skill as Orchestrator (Phase 5b)

**Feature Branch**: `093-generate-subagents`
**Created**: 2026-10-09
**Status**: Draft
**Input**: User description: "093-generate-subagents: Phase 5b of the doc-change → criteria → test workflow. The
spectra-generate skill becomes an orchestrator, like 092 did for update and criteria. Analysis, generation and repair
run in forked subagents, so the main context never holds doc text, compiled prompts, test JSON or verdict JSON. Also
takes the three deferred 092 review findings. One small CLI addition (per-test verdicts from `ingest-verdict --all`)."
(FR-A–FR-D, constraints and scope as quoted in the request.)

## Context

Spec 092 moved the update and criteria workflows' heavy work into forked subagents. Test generation still runs entirely
in the user's main session: the analysis prompt (which inlines documentation up to the analysis token budget), the
in-session behaviour analysis, the generation prompt and the docs the model reads to generate, the generated test JSON,
one verdict file read per test, and the whole repair manifest. A generation run of a few dozen tests can fill the
context and trigger auto-compaction mid-run.

This phase applies the 092 pattern to generation, closes three findings left open by the 092 review, and adds the one
CLI capability the orchestrator needs to route tests by the verdict the CLI actually classified: per-test results from
the batch verdict ingest.

Facts (verified on `20ac624`):
- The generation skill holds in the main session: the analysis prompt and analysis (Steps 1–3), the generation prompt,
  the doc reading and the generated JSON (Steps 5–7), one verdict-file read per test (8a), and the whole repair manifest
  with a patch per entry (8b). Its CLI reference table tells the model to read test files directly.
- 092 added the updater, repairer and extractor agents, the critic's one-line final message, and installs every
  bundled agent under `.claude/agents/`. The repairer returns one line per **input** id.
- The batch verdict ingest (`ingest-verdict --suite S --all`) reports only counts (grounded / partial / hallucinated /
  errors) and stale ids — no per-test verdicts. Since 092 the update flow routes by the critic's self-reported line.
- The analysis ingest reports counts only (already covered, recommended, category and technique breakdowns, boundary
  gaps); it never says which behaviours are covered. Each behaviour in the analysis has a category, title, source
  document and technique, but no id. The generation prompt does not read the analysis.

Rules that still apply:
- analyze → present → **STOP for approval** → generate; the critic is mandatory for every generated test; bounded
  retries; the NON-STOP contract and the written==0 guard text verbatim (075 gate); the 089–092 staging, exit-8,
  `--ids` and `unchanged_pending` rules;
- human summaries are never suppressed: the user sees everything they saw before (092 FR-F);
- no CLI, config, prompt-template or file-format change beyond FR-D;
- install behaviour is checked on a workspace from the real `spectra init` and on an existing (pre-093) workspace.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A large generation run finishes without the session running out of room (Priority: P1)

A QA engineer asks for tests for a documentation area. The assistant analyses, proposes how many tests to write and in
which batches, waits for approval, generates batch by batch, verifies every test and repairs the partial ones — and the
main session never holds the documents, prompts, test JSON or verdicts.

**Why this priority**: The motivating failure: generation runs lose their working state to auto-compaction.

**Independent Test**: Generate at least 24 tests in at least 3 batches and measure the main session's context before
and after: it grows by no more than about 300 tokens per test, never compacts, and contains no doc text, compiled
prompt, test JSON or verdict JSON.

**Acceptance Scenarios**:

1. **Given** a request to generate tests for an area, **When** the analysis runs, **Then** the analyst subagent returns
   the recommendation (already covered, recommended, category and technique breakdowns, boundary gaps) and the proposed
   batches, and the main session presents them and stops for approval exactly as today.
2. **Given** the analysis prompt would exceed the token budget, **When** the analyst runs, **Then** it returns the
   refusal (the candidate doc suites) and the main session asks the user to narrow or raise the budget, as today.
3. **Given** approval, **When** generation runs, **Then** each batch is handed to the generator subagent in turn with its
   focus and count; the main session collects the created ids and titles; a failed batch is reported and the next one
   runs.
4. **Given** the generated tests, **When** verification runs, **Then** the critic runs per test, the main session gets
   each test's verdict from the CLI's batch verdict ingest (not from the critic's own line, not from a file), keeps the
   grounded, drops the hallucinated (as today) and repairs the partial through the repairer subagent; the grounding
   write and the written==0 guard behave exactly as before.
5. **Given** an interrupted run, **When** the user resumes, **Then** the partial tests still needing a repair are found
   from the CLI's grounding audit and handed to the repairer.
6. **Given** the run ends, **When** the report is shown, **Then** every created, kept, repaired, flagged, dropped and
   failed test is listed by id with its title.

---

### User Story 2 - The verdict the CLI classified decides a test's route (Priority: P1)

Whatever the critic says about itself, the route of a test (kept, repaired, dropped, unverified) follows the verdict the
CLI classified from the verdict file — in generation and in update.

**Why this priority**: The 092 review found that routing by a self-reported line lets a wrong or missing line send a
test down the wrong path; the CLI's classification is the source of truth.

**Independent Test**: Make the critic's line disagree with its verdict file: the test is reported unverified, not
routed by the line.

**Acceptance Scenarios**:

1. **Given** a batch of verdict files, **When** the batch verdict ingest runs with a list of ids, **Then** it returns
   one result per classified listed test (id, verdict, score, gate), lists unknown ids as not found, and its existing
   counts and lists are unchanged.
2. **Given** a critic line that says one verdict while the CLI classified another, or the CLI has no result for the
   test, **When** the skill routes it, **Then** the test is reported as unverified.
3. **Given** the update flow's re-verification, **When** it routes edited tests, **Then** it uses the same per-test CLI
   results.

---

### User Story 3 - No edited or repaired test loses its verification silently (Priority: P2)

Every test the updater or repairer reports as written — including one from an earlier group's leftover staging file, a
re-edit after "still hallucinated", or an "edit again" — is re-verified or listed as unverified.

**Why this priority**: A written edit drops the test's old verdict (090); an untracked write leaves a test without
grounding and nobody notices.

**Independent Test**: In both skills, the text requires every `written` line to go through re-verification or the
unverified list; the repairer reports leftovers by id.

**Acceptance Scenarios**:

1. **Given** a leftover `repaired-<id>.json` from an earlier run, **When** the repairer ingests, **Then** it returns a
   line for that id too, and the skill attributes it by id.
2. **Given** any `written` line arriving outside the main loop, **When** the skill continues, **Then** that test is
   re-verified or reported unverified.

---

### User Story 4 - The user still sees everything (Priority: P2)

Everything the user saw during a generation run before this phase (the recommendation, the created tests, the ingest
baseline summary and warnings, the verification and repair outcomes, the final report) still reaches them, relayed by the main
session from the subagents' returned lines and the CLI's results.

**Why this priority**: The project rule: delegating work must not hide its results.

**Independent Test**: Compare what a generation run shows before and after: every item is still present.

**Acceptance Scenarios**:

1. **Given** a generation run, **When** it ends, **Then** the user has seen the recommendation, every created test (id
   and title), every baseline line and warning, every verification and repair outcome, and the final report.

---

### User Story 5 - The two new agents are installed (Priority: P3)

`spectra init` installs the analyst and generator agents under `.claude/agents/`; `spectra update-skills` adds them to
an existing workspace without touching user-modified files.

**Why this priority**: Required for the skill to run; the mechanism exists since 092.

**Independent Test**: A real init and a pre-093 workspace (092 agents, a user-modified skill) both end with all six
agents under `.claude/agents/`, and the modified file untouched.

**Acceptance Scenarios**:

1. **Given** a new or pre-093 workspace, **When** init / update-skills runs, **Then** the analyst and generator agents are
   under `.claude/agents/` and a user-modified file is skipped.

---

### Edge Cases

- The user gave an explicit count: it overrides the recommendation and is split over the proposed batches.
- The recommendation is 0: the main session says every behaviour is covered and generates nothing (as today).
- A generator batch fails after its bounded retries: reported with its code and first error; the other batches run;
  the main session never generates the batch itself.
- A test gets no CLI verdict (the critic's prompt compile was refused, its verdict JSON stayed damaged, or the batch
  verdict ingest has no `results` entry — stale, unreadable, not found): the main session invokes the critic once more for that test; if there is still no CLI verdict, the test is kept as
  **unverified**: never counted as kept-grounded, never given a grounding block, never regenerated or deleted, and
  listed in the final report under its own heading "Unverified (critic could not judge)" with id, title, reason and
  the next step (run the generation skill's critic step again for these ids, or `spectra ai audit-grounding --suite
  <s>`, where it shows as `none` / `critic`). This replaces the
  pre-093 rule "regenerate that single test (bounded 2), never keep an unverified test" (decision 2026-10-09).
- The critic's line disagrees with the CLI's verdict: the test is kept as unverified and reported, never dropped; a
  test kept with a `hallucinated` verdict is later listed under `skipped_hallucinated` by the grounding batch (091),
  which is expected, not a failure.
- A repair that fails after the repairer's retries: reported and flagged unverified.
- A still-hallucinated test after the repair in the generation flow: dropped (record-drop + delete), as today; in the
  update flow: the user's choice, as today.
- The from-description single-test flow: unchanged.
- `--ids` given to the batch verdict ingest without `--all`: an invalid option combination, like 091's rule for the
  grounding ingest.
- A user-modified generation skill in a pre-093 workspace: update-skills skips it; the old in-session flow stays until
  the user refreshes it.

## Requirements *(mandatory)*

### Functional Requirements

**FR-A — new bundled agents**

- **FR-001**: Two new bundled agents MUST be added: an **analyst** and a **generator**. Like 092's agents they run
  isolated, inherit the session's model, are never auto-invoked, have only the tools they need, never ask the user,
  never start another subagent, never edit test files directly, follow the NON-STOP contract and end with a `stop:
  <reason>` line when no single `spectra` command covers a step.
- **FR-002**: The **analyst** takes a suite, a doc suite and an optional focus. It compiles the analysis prompt, performs
  the analysis in its own context, writes the analysis file, ingests it, and retries a damaged analysis at most twice.
  Its final message MUST contain only (data-model.md grammar): the recommendation (already covered, recommended,
  documents analyzed, the category breakdown, the technique breakdown, the boundary gaps — each as compact lines) and
  the proposed batches, one line each, `batch <n> | count <k> | focus <text>`; or, on a refusal, the `refused:` and
  `refusal:` lines; and a `stop:` line when it had to stop. A batch groups at most 8 behaviours from the same source document; its focus names that
  document and those behaviours.
- **FR-003**: On the analysis pre-flight budget refusal, the analyst MUST return the refusal lines (the candidate doc
  suites and the two remedies) instead of a recommendation, so the main session can ask the user.
- **FR-004**: The **generator** takes a suite, a focus and a count. It compiles the generation prompt, generates in its
  own context (reading documentation there as today), writes the generated file, ingests it, and retries a damaged
  batch (empty or invalid) at most twice. Its final message MUST contain only one line per created test, `<id> |
  <title>`, a `baseline:` line (the ingest's baseline summary) and the ingest's `warnings` as `warning:` lines — or
  `failed <error_code>: <first error>` — and a `stop:` line when it had to stop. (The tests ingest has no `notes`
  field; research R3.)
- **FR-005**: The 092 **repairer** is reused; FR-C1 changes what it returns.

**FR-B — the generation skill as orchestrator**

- **FR-006**: The generation skill MUST state a **context budget contract** (as 092's): the main session never reads
  docs, compiled prompts, test files, generated / staged JSON or verdict files; such steps are delegated to the named
  subagent; the main session's own tool calls are `spectra` commands with small JSON output, progress writes,
  record-drop / delete, and subagent calls.
- **FR-007**: Analysis is delegated to the analyst. Step 4 (present the recommendation and **STOP** for approval) is
  unchanged in substance and is built from the analyst's lines.
- **FR-008**: After approval, each proposed batch is handed to the generator in order (one at a time: the id allocator
  and the suite index have a single writer), with focus "<user focus>; <batch focus>" and the batch's count. A count the
  user gave explicitly overrides the recommendation and is split over the batches. The main session collects the
  created ids and titles; a failed batch is reported and the next batch runs; the main session never generates a batch
  itself and never regenerates a single test (FR-015).
- **FR-009**: Verification (8a): the main session runs the critic per created id, then the batch verdict ingest limited
  to this round's ids, and routes each test by the CLI's per-test result: grounded → kept; partial → repair list;
  hallucinated → dropped (record-drop + delete, as today) — only when the critic's line agrees or there is no line.
  The critic's own line is used only for progress. Then the
  batch grounding write and the 075 written==0 guard, verbatim; the kept-grounded count comes from the CLI results.
- **FR-010**: Repair (8b): the partial ids are handed to the repairer. For ids it returns `written` or `unchanged`: the
  critic again per id, the batch verdict ingest limited to those ids, the still-hallucinated dropped, then the repaired
  grounding write limited to the re-critiqued ids minus the dropped ones (091). A `failed` repair is reported and
  flagged unverified. The repair-batch command is no longer used by the skill (the command stays); the resume path finds
  the partial tests still needing a repair from the grounding audit (`action_needed: repair`) and hands them to the
  repairer.
- **FR-011**: The final report (Step 9) is built from the returned lines and the CLI results and lists every created,
  kept, repaired, flagged, dropped and failed test by id with its title.
- **FR-012**: The from-description flow's generation steps are unchanged; its critic step follows the new Step 8
  (CLI-result routing, repairer, drops, unverified). The skill's CLI reference no longer tells the model to read test
  files directly.

**FR-C — the 092 review findings**

- **FR-013** (C1): The repairer MUST return one line per entry in its ingest results — including leftover repair
  staging files — attributed by id, as the updater does.
- **FR-014** (C2): In both the update and the generation skill, every `written` line from the updater or repairer,
  whenever it arrives (a leftover from an earlier group, a re-edit after "still hallucinated", an "edit again"), MUST go
  through re-verification (update Step 5a / generation 8a) or be listed as unverified in the report.
- **FR-015** (C3): Both skills MUST route by the CLI's per-test verdicts (FR-D), never by the critic's self-reported
  line. A disagreement — the line says one verdict and the CLI another — makes the test unverified, and it is
  reported, never dropped. A test with no CLI verdict: the main session invokes the critic once more for that test; if there is still no CLI verdict, the test is kept as
  **unverified**: never counted as kept-grounded, never given a grounding block, never regenerated or deleted, and
  listed in the final report under its own heading "Unverified (critic could not judge)" with id, title, reason and
  the next step (run the generation skill's critic step again for these ids, or `spectra ai audit-grounding --suite
  <s>`, where it shows as `none` / `critic`).

**FR-D — per-test verdicts from the batch verdict ingest (the one CLI addition)**

- **FR-016**: `ingest-verdict --suite S --all` MUST add `results`: one entry per classified test, `{id, verdict, score,
  gate}`. Existing counts and lists are unchanged (the addition is additive).
- **FR-017**: `ingest-verdict --suite S --all` MUST accept `--ids a,b` (only these tests; unknown ids listed under
  `not_found`), with 091's parsing rules for the grounding ingest's `--ids` (comma-separated, trimmed, de-duplicated,
  case-insensitive); `--ids` without `--all` is an invalid option combination.
- **FR-018**: The single-file form (`--from`) is unchanged.

**Cross-cutting**

- **FR-019**: Batches run sequentially; no parallel subagents.
- **FR-020**: No other CLI, config, template or format change. If one turns out to be necessary, the work stops and it
  is reported instead of added.
- **FR-021**: All existing contracts hold (Context, "Rules that still apply").
- **FR-022**: The user sees everything they saw before this phase (092 FR-F), built from the returned lines and the
  CLI's results.
- **FR-023**: Install: `spectra init` and `spectra update-skills` install the two new agents under `.claude/agents/`
  (092's general rule); a user-modified file is never overwritten.
- **FR-024**: Docs: `docs/usage.md`, `docs/cli-reference.md` (the batch verdict ingest's `results` and `--ids`),
  `CHANGELOG.md`, a short CLAUDE.md "Recent Changes" entry.

### Required Tests

Each guard must fail on `20ac624` and pass after the change.

| Test | Covers | Fails on 20ac624 because |
|---|---|---|
| T1 batch verdict ingest: `results`, `--ids`, `not_found`, counts unchanged, `--ids` without `--all` | FR-016–FR-018 | no `results`, no `--ids` |
| T2 install: analyst and generator under `.claude/agents/` (real init; pre-093 workspace, a modified file skipped) | FR-023 | the agents don't exist |
| T3 agent frontmatter and return contracts (analyst, generator, repairer's per-result lines) | FR-001–FR-005, FR-013 | the agents don't exist; the repairer returns per input id |
| T4 generation skill guards: no analysis / generation / repair-batch compile or tests ingest in the main session; no read of verdict, test or generated / staged files; subagent calls; routing by the batch verdict ingest's results with `--ids`; the written==0 guard verbatim | FR-006–FR-012, FR-015 | the main session does the work and reads verdict files |
| T5 update skill guards: routing by the CLI's per-test results; every `written` line re-verified or listed unverified | FR-014, FR-015 | Step 5a routes by the critic's line |

### Key Entities

- **Analyst agent**: in {suite, doc_suite, focus?}; out: the recommendation lines, the batch lines, or the refusal.
- **Generator agent**: in {suite, focus, count}; out: one line per created test, a baseline line and warnings, or a
  failure.
- **Batch**: number, count, focus (≤ 8 behaviours from one source document).
- **Per-test verdict result**: id, verdict, score, gate — from the batch verdict ingest.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Generating at least 24 tests in at least 3 batches completes without auto-compaction; the main session's
  context grows by no more than 300 tokens per test and holds no doc text, compiled prompt, test JSON or verdict JSON
  (measured with Claude Code's `/context`).
- **SC-002**: On the same suite and model, the share of tests the critic rates grounded is not lower than in a
  main-session run before this phase.
- **SC-003**: 100% of the tests created in a run appear in the final report by id and title, with their outcome.
- **SC-004**: In both skills, 0 tests are routed by a self-reported critic line; every disagreement is reported as
  unverified.
- **SC-005**: Every guard in Required Tests fails on `20ac624`. Test totals: Core ≥ 1219, CLI ≥ 2068 with only the
  known `ProfileFormatLoaderTests` failure, Execution 228, Integration 8; zero new build warnings.

## Assumptions

- The analysis ingest doesn't identify which behaviours are covered, so the analyst forms batches from its own behaviour
  list; the batch counts add up to the recommended count (or the user's count). Duplicate protection during generation
  stays what it is today.
- The generator reads documentation in its own context as the main session does today; that context is discarded when
  the batch ends.
- The critic keeps its one-line final message (092); it is now used for progress only.
- The batch verdict ingest's `results` contain only classified tests; stale, unreadable or unknown ids keep their
  existing lists / counts.
- 300 tokens per test is measured over the whole run (analysis, generation, verification, repair) divided by the number
  of created tests.

## Out of Scope

- Package 04 part 2: persisted behaviours, `compile-prompt --batch`, inlined doc sections, dropping the on-demand block,
  `generation.batch_size` (Phase 5c).
- The critic's pinned model; parallel subagents; the Phase 6 list.
