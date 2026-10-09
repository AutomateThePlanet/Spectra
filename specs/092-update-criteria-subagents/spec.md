# Feature Specification: Update and Criteria Skills as Orchestrators (Phase 5)

**Feature Branch**: `092-update-criteria-subagents`
**Created**: 2026-10-09
**Status**: Draft
**Input**: User description: "092-update-criteria-subagents: Phase 5 of the doc-change → criteria → test workflow. The
spectra-update and spectra-criteria skills become orchestrators. Per-group, per-doc and per-repair work runs in forked
subagents that return a few lines each, so the main context no longer holds doc text, compiled prompts, test JSON or
verdict JSON." (FR-A–FR-F, constraints and scope as quoted in the request.)

## Context

Updating a suite after a doc change, and extracting criteria from changed docs, both run inside the user's main
assistant session today. Every compiled edit prompt (doc sections, criteria, whole tests), every staged test JSON,
every verdict file and every compiled extraction prompt (the whole document) lands in that one context. A suite with a
few dozen changed tests fills it and triggers auto-compaction, which loses the conversation's working state in the
middle of the workflow.

Phase 5 keeps the user-facing workflow and every CLI command as they are, but moves the heavy per-group, per-repair and
per-document work into forked subagents. Each returns a few lines; the main session orchestrates, asks the user the
same questions as today, and reports the same outcomes.

Facts (verified on `309174b`):
- Bundled agents are installed by one routing rule: only the critic goes to `.claude/agents/`; any other agent file
  would go to `.github/agents/`. All embedded agent files are loaded and their tool placeholders substituted
  (`{{READONLY_TOOLS}}` = Read, Grep, Glob, Bash).
- The critic agent: isolated (`context: fork`), never auto-invoked, pinned model (changing that is Phase 6). It writes
  `.spectra/verdicts/critic-verdict-<id>.json` and has no rule for its final message. Subagents cannot start
  subagents, so the critic is always started by the main session.
- The update skill's Step 5 (edit loop) and Step 5a (re-verify, repair) and the criteria skill's Step 2 (per-doc
  extraction) do all their work in the main session, as listed in the request.
- `ingest-criteria` prints a human changeset summary (stderr in JSON mode) that includes criterion text: added
  (`+ id text`), modified (`~ id "old" → "new"`), removed (`- id "old"` with referencing tests) and metadata changes
  (`* id field: old → new`), plus duplicate-id warnings.
- `ingest-update --all` prints one human line per staged file and a baseline summary; its JSON has `results`,
  `baseline` and `warnings`.

Rules that still apply:
- triage → bucket question → STOP; the critic is mandatory for every written test; bounded retries; the NON-STOP
  contract; the 089–091 staging, exit-8, `unchanged_pending` and `--ids` rules;
- human summaries are never suppressed: the user always sees what happened;
- no CLI, config, prompt-template or file-format change in this phase;
- install behaviour is checked on a workspace made by the real `spectra init` and on an existing (pre-092) workspace
  (the 083 lesson).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A large suite update finishes without the session running out of room (Priority: P1)

A QA lead updates a suite after a big doc change: 30+ tests needing edits, in several groups, then re-verification
and repairs. The session finishes the whole run without auto-compaction, asks the same questions as before, and ends
with the same per-test report.

**Why this priority**: This is the failure that motivates the phase: large updates lose their working state mid-run.

**Independent Test**: Run the update of a suite with at least 30 LOAD tests in at least 3 groups and measure the main
session's context before and after; it grows by no more than about 300 tokens per test and never compacts.

**Acceptance Scenarios**:

1. **Given** a triaged suite and the user's bucket choice, **When** the edit step runs, **Then** each group is handed to
   the updater subagent in turn, and the main session receives one line per test (written / unchanged /
   unchanged_pending with bucket and reason / failed with code and first error), a baseline summary and any warnings —
   never the prompt, the doc text or the test JSON.
2. **Given** a test returned `unchanged_pending`, **When** the main session reports it, **Then** it asks the user as
   today; "edit again" hands just that test back to the updater, "acknowledge" runs the acknowledge command in the main
   session.
3. **Given** a test still failed after the updater's bounded retries, **When** the group finishes, **Then** the failure
   is reported with its code and first error and is not redone in the main session.
4. **Given** written tests to re-verify, **When** the critic runs per test, **Then** the main session routes each test by
   the critic's one-line answer, without reading verdict files; partial or hallucinated tests go to the repairer
   subagent; only tests whose repair landed are re-critiqued and written as repaired (the 091 `--ids` rule); a failed
   repair is reported as "repair failed: <code>"; a still-hallucinated test is the user's choice, as today.
5. **Given** a critic answer that is missing or reports a failure, **When** the main session routes the test, **Then**
   the test is reported as unverified and the main session does not read the verdict file to find out more.
6. **Given** "update all suites", **When** it runs, **Then** each suite is processed the same way, one at a time.

---

### User Story 2 - Criteria extraction never pulls whole documents into the session (Priority: P1)

A QA lead extracts criteria after five or more docs changed. Each document is handled by an extractor subagent; the
main session confirms moves and retirements with the user as before and ends with the full changeset.

**Why this priority**: Every compiled extraction prompt contains the whole document; a handful of long docs is enough
to exhaust the main context.

**Independent Test**: Extract criteria for at least 5 changed docs; the main transcript contains no document text and
no compiled prompt, and the final summary lists every document's changeset.

**Acceptance Scenarios**:

1. **Given** the work-list from the changed-docs step, **When** extraction runs, **Then** each document is handed to the
   extractor subagent in turn, and the main session receives the document's outcome (extracted / unchanged / rejected /
   failed with code) and its changeset lines.
2. **Given** moved, deleted or unmigrated documents, **When** the changed-docs step runs, **Then** moves, retirements and
   migration are still confirmed with the user and done in the main session; the extractor never retires, moves or
   migrates.
3. **Given** all documents processed, **When** the summary step runs, **Then** it is built from the returned lines and
   includes every added, modified and removed criterion (removed ones with their referencing tests), duplicate-id
   warnings and per-document failures.
4. **Given** the import and list flows, **When** they run, **Then** they behave exactly as before.

---

### User Story 3 - The user still sees everything that happened (Priority: P1)

Whatever the CLI printed for the user before (per-test outcomes, baseline summaries, warnings, criteria changesets),
the user still sees it in the conversation — now relayed by the main session from the subagents' returns, because a
subagent's own tool output is not shown to the user.

**Why this priority**: The project rule: human summaries are never hidden; delegating the work must not hide its
results.

**Independent Test**: Compare the information shown to the user in an update run and an extraction run before and
after this phase: every per-test outcome, baseline summary, warning and changeset entry is present after it too.

**Acceptance Scenarios**:

1. **Given** an update run, **When** it ends, **Then** the user has seen every test's outcome, each group's baseline
   summary and every warning.
2. **Given** an extraction run, **When** it ends, **Then** the user has seen each document's changeset at the level of
   detail defined in FR-019.

---

### User Story 4 - Existing and new workspaces get the new agents in the right place (Priority: P2)

`spectra init` installs all bundled agents under `.claude/agents/`. `spectra update-skills` adds the three new agents to
an existing workspace that has only the critic, without touching files the user modified.

**Why this priority**: The orchestrated skills can't run without their agents; an agent installed in the wrong folder
is invisible to the assistant.

**Independent Test**: On a fresh `spectra init` workspace and on a pre-092 workspace (critic only, a user-modified
skill file), run init / update-skills and check where every agent file lands and that the modified file is untouched.

**Acceptance Scenarios**:

1. **Given** a new workspace, **When** `spectra init` runs, **Then** the critic, updater, repairer and extractor agents
   are under `.claude/agents/` and nothing is written to `.github/agents/`.
2. **Given** a pre-092 workspace with only the critic installed, **When** `spectra update-skills` runs, **Then** the three
   new agents are added under `.claude/agents/` and a user-modified skill or agent file is reported as skipped and left
   unchanged.

---

### User Story 5 - The critic ends with a one-line answer (Priority: P2)

The critic's last message is exactly one line with the test id and its verdict and score, or the id and why it
couldn't write a verdict. Everything else about the critic is unchanged.

**Why this priority**: The update orchestrator routes by this line instead of reading verdict files.

**Independent Test**: A text check on the bundled critic agent finds the final-line rule; the generation skill keeps
working unchanged.

**Acceptance Scenarios**:

1. **Given** the bundled critic agent, **When** it is read, **Then** it requires the final message `<id> | <verdict>
   <score>` or `<id> | failed: <reason>` and nothing else.
2. **Given** the generation skill, **When** it runs the critic, **Then** it behaves as before (it may keep reading verdict
   files until Phase 5b).

---

### Edge Cases

- The updater's `results` include a leftover staging file from an earlier group: it returns a line for that id too, and
  the main session attributes the outcome by id, not by group.
- A group too large for one prompt (`PROMPT_TOO_LARGE`): the updater falls back to one prompt per test, as today.
- A subagent hits a step no single `spectra` command covers: it stops and reports (NON-STOP contract); the main session
  reports the stop to the user and does not do the step itself.
- A subagent returns malformed or missing lines for some ids: those tests are reported as outcome unknown / unverified;
  the main session does not read files to reconstruct them.
- The criteria lock is held by another command: the extractor retries once (`LOCK_TIMEOUT`) as the skill does today,
  then reports the document as failed.
- A pre-092 workspace whose user modified the update or criteria skill: update-skills skips that skill (manifest rule),
  so the old in-session flow stays; the new agents are still installed and harmless until the skill is refreshed.
- A suite with no written tests after the edit step: Step 5a has nothing to re-verify; no critic or repairer runs.

## Requirements *(mandatory)*

### Functional Requirements

**FR-A — new bundled agents**

- **FR-001**: Three new bundled agents MUST be added next to the critic: an **updater**, a **repairer** and an
  **extractor**. Each runs isolated (forked context), inherits the session's model, is never auto-invoked, has only the
  tools it needs (running `spectra` commands, reading, writing the files named below), never asks the user anything,
  never starts another subagent, never edits test files directly, and follows the NON-STOP contract (no shell loops, no
  interpreter pipes; stop and report when no single `spectra` command covers a step).
- **FR-002**: The **updater** takes a suite and either a triage group or a list of test ids. It does the edit step's
  work: compiles the edit prompt (per group, or per test; per test on `PROMPT_TOO_LARGE`), edits in its own context,
  writes one staging file per test under `.spectra/updates/<suite>/`, ingests the batch, and retries failed tests at
  most twice (089/090 rules).
- **FR-003**: The updater's final message MUST contain only: one line per entry in the batch `results` — `<id> |
  written`, `<id> | unchanged`, `<id> | unchanged_pending <BUCKET>: <why>`, or `<id> | failed <error_code>: <first
  error>` — for every id in `results` (including leftovers from an earlier group); then one `baseline: <summary>` line;
  then any `warning: …` lines.
- **FR-004**: The **repairer** takes a suite and a list of test ids. Per id it compiles the repair prompt, patches only
  what the critic flagged, writes `.spectra/repairs/<suite>/repaired-<id>.json`, ingests with the baseline kept, and
  retries failed repairs at most twice (the same repair attempt). Its final message MUST contain only one line per id:
  `<id> | written`, `<id> | unchanged`, or `<id> | failed <error_code>: <first error>`. It MUST not depend on the update
  flow, so the generation flow can reuse it in Phase 5b.
- **FR-005**: The **extractor** takes one document and its component. It does the criteria Step 2 work for that
  document: compiles the extraction prompt, extracts in its own context, writes `.spectra/criteria.json`, ingests, with
  the skill's existing retry rules (including one retry on `LOCK_TIMEOUT` and the `INVALID_CRITERION_ID` regeneration).
  It MUST never retire, move or migrate criteria.
- **FR-006**: The extractor's final message MUST contain only `<doc> | extracted`, `<doc> | unchanged`, `<doc> |
  rejected` or `<doc> | failed <code>`, followed by the changeset lines defined in FR-019, then duplicate-id and warning
  lines.

**FR-B — critic final message**

- **FR-007**: The critic's last message MUST be exactly one line: `<id> | <verdict> <score>`, or `<id> | failed:
  <reason>` when it could not write a verdict. Its isolation contract, its verdict file and its call to the verdict
  ingest stay unchanged. The generation skill is unchanged in this phase.

**FR-C — the update skill as orchestrator**

- **FR-008**: Step 5: for each selected group, in order, the main session updates progress and hands the group to the
  updater. The main session MUST NOT compile edit prompts, ingest updates, read prompts, test files or staging files, or
  write staging files. It reports every returned line.
- **FR-009**: `unchanged_pending` → the main session asks the user as today; "edit again" hands those ids to the updater;
  "acknowledge" runs the acknowledge command in the main session. A test still failed after the updater's retries is
  reported and not redone in the main session.
- **FR-010**: Step 5a: the main session starts the critic per written test (as today) and routes each test by the
  critic's one-line answer — never by reading verdict files — then runs the batch verdict and grounding ingests.
  Partial or hallucinated tests are handed to the repairer. Only ids the repairer returns `written` or `unchanged` are
  re-critiqued, then ingested with `--all --repaired --repair-attempts 1 --ids <those ids>` (091). A failed repair is
  reported as "repair failed: <code>". A still-hallucinated test is the user's choice, as today.
- **FR-011**: A critic answer that is missing or `failed:` → the test is reported as unverified; the main session MUST
  NOT read the verdict file to find out more.
- **FR-012**: "Update all suites" runs the same flow one suite at a time.
- **FR-013**: The update skill MUST state a **context budget contract** near the top: the main session never reads
  docs, compiled prompts, test files, staging JSON or verdict files; any step that would need them is delegated to the
  named subagent; the main session's own tool calls are `spectra` commands with small JSON output, the acknowledge
  command, progress writes and subagent calls. The user decisions (bucket choice, REMOVED review, relink,
  `unchanged_pending`, still-hallucinated) stay in the main session.

**FR-D — the criteria skill as orchestrator**

- **FR-014**: Step 2: for each changed document, in order (one criteria lock, one shared `.spectra/criteria.json`), the
  main session hands the document to the extractor. The main session MUST NOT compile extraction prompts, ingest
  criteria, or read the document or a compiled prompt.
- **FR-015**: Step 1 (changed docs, moves, retirement confirmation, migration) and Step 3 (summary) stay in the main
  session; the summary is built from the returned lines. The import and list flows are unchanged.

**FR-E — install layout**

- **FR-016**: Every bundled agent file MUST install to `.claude/agents/<file>` by one general rule (not a per-name
  list); nothing is installed to `.github/agents/`.
- **FR-017**: `spectra init` and `spectra update-skills` MUST install the three new agents. On an existing workspace,
  update-skills adds missing agents and never overwrites a user-modified file (existing manifest rules). A pre-092
  workspace with only the critic gets the three new files.

**FR-F — the user still sees what changed**

- **FR-018**: Every per-test outcome, baseline summary and warning that the CLI shows the user today MUST reach the user
  through the main session's report, built from the subagents' returned lines. Nothing the user saw before 092
  disappears from the conversation.
- **FR-019**: The extractor's changeset lines MUST be the same lines the CLI's changeset summary shows today (decision
  2026-10-09): `+ <id>  <text>` per added criterion, `~ <id>  "<old>" → "<new>"` per modified one, `- <id>  "<old>"`
  per removed one with its referencing tests, `* <id>  <field>: <old> → <new>` per metadata change, or `No changes.`;
  then duplicate-id warnings. Criterion lines are short, so this stays far below the document and prompt the main
  session holds today.

**Cross-cutting**

- **FR-020**: Groups, repairs and documents run sequentially; no parallel subagents.
- **FR-021**: No CLI command, flag, config key, prompt template or file format changes. If one turns out to be
  necessary, the work stops and it is reported instead of added.
- **FR-022**: All existing contracts hold (see Context, "Rules that still apply").
- **FR-023**: Docs: `docs/usage.md` (update and criteria flows), `docs/cli-reference.md` only where it describes these
  skills, `CHANGELOG.md`, a short CLAUDE.md "Recent Changes" entry.

### Required Tests

Each guard must fail on `309174b` and pass after the change.

| Test | Covers | Fails on 309174b because |
|---|---|---|
| T1 install layout (real init: every bundled agent under `.claude/agents/`, none under `.github/agents/`) | FR-016, FR-017 | the new agents don't exist; other agents would route to `.github/agents/` |
| T2 update-skills on a pre-092 workspace (critic only; a user-modified file is skipped) | FR-017 | the three agents don't exist |
| T3 agent frontmatter (inherit model, forked, not auto-invoked, tools) and return-contract text per agent | FR-001–FR-006 | the agents don't exist |
| T4 critic final-line rule | FR-007 | the critic has no final-message rule |
| T5 update skill guards: the orchestrated steps never compile edit/repair prompts, ingest updates or read verdict files; the subagent calls name the agents; the context budget contract; the relay of every returned line | FR-008–FR-013, FR-018 | Step 5/5a do the work in the main session |
| T6 criteria skill guards: Step 2 never compiles extraction prompts or ingests criteria; the extractor call; the summary from returned lines incl. the changeset | FR-014, FR-015, FR-018, FR-019 | Step 2 does the work in the main session |

### Key Entities

- **Updater agent**: in {suite, group | ids}; out: one line per `results` entry, a baseline line, warning lines.
- **Repairer agent**: in {suite, ids}; out: one line per id.
- **Extractor agent**: in {doc, component}; out: an outcome line, the changeset summary lines (text included),
  duplicate/warning lines.
- **Critic final line**: `<id> | <verdict> <score>` or `<id> | failed: <reason>`.
- **Context budget contract**: what the main session may and may not hold.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Updating a suite with at least 30 tests needing edits, in at least 3 groups, including re-verification,
  completes without auto-compaction, and the main session's context grows by no more than 300 tokens per test
  (measured with Claude Code's `/context` before and after).
- **SC-002**: Extracting criteria for at least 5 changed documents leaves no document text and no compiled prompt in
  the main transcript.
- **SC-003**: In both runs, 100% of the per-test outcomes, baseline summaries, warnings and changeset entries shown to
  the user before this phase are shown after it.
- **SC-004**: A new workspace and a pre-092 workspace both end with all four bundled agents under `.claude/agents/` and 0
  agent files under `.github/agents/`; 0 user-modified files are overwritten.
- **SC-005**: Every guard in Required Tests fails on `309174b`. Test totals: Core ≥ 1219, CLI ≥ 2046 with only the known
  `ProfileFormatLoaderTests` failure, Execution 228, Integration 8; zero new build warnings.

## Assumptions

- Claude Code runs a bundled agent in its own context and shows the main session only the agent's final message; the
  agent's tool output is not shown to the user (hence FR-018).
- The new agents keep the critic's frontmatter shape; only the model differs (inherit). The critic's own model stays
  pinned until Phase 6.
- The updater and repairer write only the staging files named here, and the extractor only `.spectra/criteria.json`;
  every persisted change goes through the existing ingest commands.
- 300 tokens per test is measured over the whole update run (edit, re-verify, repair) divided by the number of tests
  needing edits.
- A user-modified skill keeps its old (in-session) behaviour until the user refreshes it; this is the existing
  update-skills rule, not new behaviour.

## Out of Scope

- The generation skill and the analyst/generator agents (Phase 5b).
- The critic's pinned model and its configuration (Phase 6).
- Parallel subagents.
- Any CLI change; the Phase 6 list.
