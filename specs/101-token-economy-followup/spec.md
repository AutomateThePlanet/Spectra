# Feature Specification: Token Economy Follow-up

**Feature Branch**: `101-token-economy-followup`
**Created**: 2026-10-19
**Status**: Draft
**Input**: User description: "101-token-economy-followup: A follow-up beyond the 078–100 plan. It reduces the tokens the
authoring flows spend, based on a final audit and Claude Code's documented subagent and skill behaviour. Any behaviour
change is limited to what the critic sees and how the skills are packaged. The critic must judge as well as before:
proven by a measured comparison, not assumed. Each change needs a test that fails on 6a4c41a."
(Background, F1–F4, constraints and scope as quoted in the request.)

## Context

The authoring flows (generate, update) spend most of their tokens in three places: the critic's input, the skill bodies
the main session carries for the whole run, and the critic results the main session collects once per test. A fourth
finding is not about tokens: the bundled agent files carry fields Claude Code doesn't read, so a guarantee SPECTRA states
("never auto-invoked") is not enforced.

Facts (verified on `6a4c41a` and against Claude Code's subagent and skill documentation):
- **The critic reads the start of whole documents, and no criteria.** (Corrected while planning; a probe on the built
  CLI confirmed both points.)
  - `compile-critic-prompt --suite S --test ID` resolves the test's `source_refs` and inlines each referenced file; a
    `doc.md#anchor` ref loses its anchor. Each document is cut at 8,000 characters (at most 5 documents), so a test
    costs up to ~2,000 tokens per cited document, not ~7,500.
  - The cut hides text: a test citing `## Section 30` of a 14 KB document got the document only up to Section 23, so
    the critic never saw the section it had to check.
  - The command loads the suite's criteria and then drops them: no template loader is passed, and the built-in system
    prompt ignores the criteria. The critic has never seen criteria; `.spectra/prompts/critic-verification.md` is not
    used by this command either.
  - It runs once per generated or edited test.
  - Since 094, tests cite section refs; 088's update prompt and 094's batch prompt already inline sections.
  - The fingerprint the critic's freshness check uses (090/091) covers the test's content only, not the documents.
- **The skills are long.**
  - `spectra-generate` is ~37,200 characters (~9K tokens, 406 lines) and `spectra-update` ~22,900 (~5.7K tokens, 378
    lines).
  - A skill body stays in the main session's context for the whole run. After auto-compaction Claude Code re-attaches
    only the first 5,000 tokens of each invoked skill (25,000 tokens for all re-attached skills together, most recent
    first), so a long generation run can lose its later steps (critic, repair, report).
  - Claude Code's docs advise keeping a skill body under 500 lines and moving reference material to separate files in
    the skill's folder, linked from SKILL.md so Claude knows when to read them.
  - In `spectra-generate`, the "CLI flags reference" (~5.3 KB) lists commands that only the subagents run since 093;
    Step 8 is ~8.6 KB.
  - Each skill installs as one file, `.claude/skills/<name>/SKILL.md`, recorded in the skills manifest (096 rules).
- **The main session runs the critic once per test.**
  - 092/093 assumed a subagent cannot start a subagent. Claude Code allows nesting (three layers below the main
    conversation by default; `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1` turns it off). A subagent at the limit doesn't get
    the Agent tool at all, and an `Agent(type)` list in a subagent's `tools` is ignored.
  - The work agents (`spectra-generator`, `spectra-updater`, `spectra-repairer`) have `tools: [Bash, Read, Write]` —
    no Agent tool.
  - So each test adds a critic call, its final line and its routing to the main session.
- **Agent fields Claude Code ignores.**
  - All six bundled agents carry `disable-model-invocation: true` and `context: fork`. These are skill fields; Claude
    Code ignores a subagent field it doesn't recognise, without an error (`claude --debug` logs "does not recognize or
    apply" from v2.1.296). No subagent field stops auto-delegation; the documented levers are the description and a
    settings `deny` rule, which would also block the skills.
  - Their descriptions say "never auto-invoked", but nothing enforces it: Claude may delegate to, say, `spectra-updater`
    straight from its description and skip the update skill's triage.
  - The generate skill's text calls the critic "a fresh, isolated `context: fork`".
- **Who runs the critic today.** The generate skill (Step 8 and its from-description flow, Step 4), the update skill
  (Step 5a and its repair re-check) and the review-flagged skill. The repairer is used by both the generate and update
  skills.

## Clarifications

### Session 2026-10-19

- Q: The critic prompt has never contained criteria (loaded, then dropped). Should section mode add the test's linked
  criteria? → A: No. Section mode changes only the documents; the dead criteria load is removed. The critic still sees
  no criteria in either mode.
- Q: The 8,000-character cut means full-docs mode sometimes never shows the cited section, so section mode can judge
  differently because it is better. How should the gate count that? → A: Split the gate: ≥ 90% verdict agreement on
  the tests whose cited text full-docs mode showed in full; the other tests are reported, and on them section mode
  must not be less grounded. Section mode's overall grounded rate stays ≥ full-docs' minus 5 points.

### Analysis remediation 2026-10-19

- The update skill's cap is 16,000 characters, like the generate skill's (was 18,000): at 3.5 characters per token,
  18,000 could exceed the 5,000 tokens re-attached after compaction. Both token counts are measured.
- When the critic gives no final line, it counts as agreeing (the 093 rule): the subagent reports the CLI's verdict.

## Decisions

Made while writing this spec (open to change at `/speckit.clarify`):

- **Order.** F1 lands first and its quality gate is measured before F2 and F3 change any skill. If the gate fails, work
  stops and the result is reported. F4 doesn't change what the critic sees or how a skill runs, so it doesn't wait on
  the gate.
- **The escape hatch reaches the critic agent.** The `spectra-critic` agent accepts an optional `full_docs: true` input
  and then compiles with `--full-docs`. The skills never pass it by default. This is how the gate's comparison runs and
  how a user forces whole documents for one test.
- **Retries stay with the subagent.** Under F3 the work subagent also does today's "no `results` entry → run the critic
  once more and ingest again" step. A test still without an entry comes back as `unverified: <reason>`, so the main
  session's routing table is unchanged.
- **The update skill's re-verify question moves earlier.** Today the update skill asks "Re-verify the edited tests with
  the critic now? (recommended)" the first time a `written` line arrives. Under F3 the updater runs the critic, so the
  answer has to exist before the updater starts. The question is asked once per run, before the first updater call, and
  the answer goes to every updater and repairer call as `verify: yes|no`. With `no`, they run no critic, and the edited
  tests are reported as unverified, as today. The wording stays verbatim. The alternative was to keep the question's
  timing and run the first group's critic in the main session (the fallback path); it was rejected because the first
  group would then cost the per-test tokens this spec removes.
- **The cut stays.** Each document item keeps today's 8,000-character cut and the 5-document limit in both modes. In
  section mode one item holds a document's cited sections, so the cut only bites when a document's cited text alone is
  over 8,000 characters; the quickstart records how often that happens.
- **Allowed agent fields.** `name`, `description`, `tools`, `model`. No other field is needed today. Adding one later
  means changing the guard's list on purpose.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The critic reads the sections the test cites (Priority: P1)

A QA engineer generates tests from a long feature document. Each test cites the section it covers. The critic checks
each test against that section, not the first 8,000 characters of the whole document, and its verdicts are at least as
good as before.

**Why this priority**: The critic runs once per test, so its input is the largest repeated cost of a run. It is also
where a quality loss would hurt most, which is why the comparison is measured. Today the cut can hide the very section
a test cites.

**Independent Test**: A workspace with a long document and tests citing `doc.md#section` refs, one of them past the
8,000th character. Compile each test's critic prompt with and without `--full-docs`. Section mode holds only the cited
sections, including the one past the cut. `--full-docs` gives the same bytes as `6a4c41a`. Two compiles of the same
input give identical prompts.

**Acceptance Scenarios**:

1. **Given** a test whose refs are `docs/a.md#login` and `docs/a.md#logout`, **When** its critic prompt is compiled,
   **Then** it holds those two sections (each with its subsections, the way 094's batch prompt inlines a section) in
   first-reference order, and no other text of `docs/a.md`.
2. **Given** a test whose ref is `docs/b.md` (no anchor), **Then** the prompt holds the whole of `docs/b.md`.
3. **Given** a test with refs `docs/a.md` and `docs/a.md#login`, **Then** the prompt holds `docs/a.md` once, whole.
4. **Given** a ref `docs/a.md#removed-heading` whose anchor no longer matches a heading (the 085 matcher), **Then** the
   prompt holds the whole of `docs/a.md` and one line saying that anchor `#removed-heading` was not found, so the whole
   document is given.
5. **Given** a test citing `docs/long.md#section-30`, where that section starts after the 8,000th character, **Then**
   section mode's prompt holds Section 30, and the `--full-docs` prompt doesn't (as today).
6. **Given** either mode, **Then** the prompt holds no acceptance criteria (as today).
7. **Given** `--full-docs`, **Then** the prompt is byte-identical to the one `6a4c41a` compiles for the same workspace.
8. **Given** `--docs <path>`, **Then** the given documents are used whole, as today, in either mode.
9. **Given** the same test and workspace, **When** the prompt is compiled twice, **Then** both prompts are
   byte-identical.

---

### User Story 2 - Proof that the critic judges as well as before (Priority: P1)

The team lead wants evidence, not an assumption, that the smaller critic input doesn't change what the critic decides.

**Why this priority**: The whole spec rests on "the critic must judge as well as before". If the gate fails, the
section mode doesn't ship as the default.

**Independent Test**: The quickstart's gate procedure. Run the critic twice on the same ≥ 30 tests with the same critic
model, once per mode, and record verdicts, grounded rates and prompt sizes in the tasks' Notes.

**Acceptance Scenarios**:

1. **Given** the same ≥ 30 tests and the same `ai.critic.model`, each test classed as "seen" (full-docs mode showed all
   its cited text) or "cut" (some cited text was past the cut or the 5-document limit), **When** the critic runs in
   section mode and in `--full-docs` mode, **Then** the verdicts agree for ≥ 90% of the "seen" tests.
2. **Given** the "cut" tests, **Then** section mode rates at least as many of them grounded as full-docs mode does, and
   each one is listed with both verdicts.
3. **Given** the same runs, **Then** section mode's overall grounded rate is at least full-docs' rate minus 5
   percentage points.
4. **Given** the same runs, **Then** the Notes record the prompt size per test (median and max, in characters) for both
   modes, and list each test whose verdict differs.
5. **Given** any condition fails, **Then** work stops before F2 and F3, and the result is reported with the differing
   tests.
6. **Given** fewer than 30 "seen" tests are available, **Then** the Notes say so and give the agreement over the "seen"
   tests that exist; the gate needs at least 20 of them.

---

### User Story 3 - A long run keeps all its steps (Priority: P1)

A QA engineer generates 30 tests in one run. The run is long enough for Claude Code to compact the conversation. After
compaction the main session still knows how to do the critic routing, the repair and the report.

**Why this priority**: Today only the first 5,000 tokens of a ~9,000-token skill come back after compaction, which is
exactly where Steps 8–9 live. A run that loses them ends without verification or a report.

**Independent Test**: Character counts of the bundled skills (a guard test), plus the quickstart's long-run check.

**Acceptance Scenarios**:

1. **Given** the bundled `spectra-generate` and `spectra-update` skills, **Then** each SKILL.md has at most 16,000
   characters, and its measured token count is under 5,000.
2. **Given** either SKILL.md, **Then** the NON-STOP contract and the 075 `written == 0` guard text are in it verbatim,
   with every decision point and STOP.
3. **Given** material the main session rarely needs (the command table of commands only subagents run, long examples,
   the exit-code and bucket tables), **Then** it is in a reference file in the same skill folder, and SKILL.md names
   that file and says when to read it.
4. **Given** a generation run long enough to compact, **When** it continues, **Then** it completes Steps 8 and 9
   (critic routing, repair, report) as an uncompacted run does.

---

### User Story 4 - The main session stays small per test (Priority: P2)

A QA engineer updates a suite of 40 edited tests in 4 groups. The critic checks each edited test, but the main session
only sees one line per test from the subagent that edited it.

**Why this priority**: 092/093 set a budget of about 300 tokens of main-session growth per test. The per-test critic
round trip is the largest item the main session still pays.

**Independent Test**: Read the installed agents and skills: the work agents have the Agent tool, run the critic for
their tests, run `ingest-verdict --all --ids`, and return the verdict lines; the skills route by those lines and run
the critic themselves only after `critic: not run`.

**Acceptance Scenarios**:

1. **Given** a generator batch that created tests G1–G4, **When** it finishes, **Then** it has run the critic once per
   test (each in its own fresh context, on `ai.critic.model`), then
   `spectra ai ingest-verdict --suite S --all --ids G1,G2,G3,G4`, and returns one line per test: `<id> | verdict <v>
   <score>` (the CLI's verdict), `<id> | critic-disagrees …`, or `<id> | unverified: <reason>`.
2. **Given** those lines, **When** the main session routes, **Then** it routes exactly as today: grounded → kept;
   partial → one repair; hallucinated → `record-drop` and delete in the generate flow (only when the critic's line
   agrees), one repair in the update flow (never a drop), as today; disagreement or unverified → kept as unverified; then `ingest-grounding --all --ids <kept>` and the 075 check, run by the main
   session.
3. **Given** the repairer patched tests, **Then** it runs the critic for each test it wrote, then the verdict ingest,
   and returns the same kind of lines; the main session runs `ingest-grounding --all --repaired --ids …`.
4. **Given** the updater edited a group with `verify: yes`, **Then** it runs the critic for each test it reported
   `written` and returns the same lines; tests kept with a manual verdict are never sent to the critic.
5. **Given** the subagent cannot start the critic (nesting disabled with `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1`, or
   no Agent tool), **Then** it returns `critic: not run`, and the main session runs the critic per test as today.
6. **Given** a run of ≥ 24 generated tests or ≥ 3 update groups, **Then** main-session growth per test, measured with
   `/context`, is within the 092/093 target of 300 tokens and below the same run on `6a4c41a`.

---

### User Story 5 - SPECTRA's subagents are only reached through their skills (Priority: P2)

A user types "update the login tests". Claude must start the update skill, which runs triage first, and not delegate
straight to `spectra-updater`, whose description sounds like a match.

**Why this priority**: Delegating to a work agent directly skips triage, analysis or approval steps the skills enforce.
Today nothing stops it; the fields meant to stop it are ignored.

**Independent Test**: A guard test over the bundled agents' frontmatter: only the allowed fields, and every description
starts with the skill-only phrase.

**Acceptance Scenarios**:

1. **Given** any bundled agent, **Then** its frontmatter has only `name`, `description`, `tools` and `model`.
2. **Given** any bundled agent, **Then** its description starts with "Only for use by the " followed by the skill or
   skills that use it, and says not to delegate to it directly.
3. **Given** a workspace from the real `spectra init`, **When** Claude Code starts with `claude --debug`, **Then** the
   log has no "does not recognize or apply" line for a SPECTRA agent.
4. **Given** the skills' text, **Then** no skill describes a subagent as running in `context: fork` or as never
   auto-invoked by a field.

---

### User Story 6 - Existing workspaces get the new files without losing edits (Priority: P2)

A team upgrades SPECTRA in a workspace where they edited `spectra-update`'s SKILL.md. `update-skills` adds the new
reference files and refreshes everything they didn't edit, and leaves their edited skill alone.

**Why this priority**: 096 promised that `update-skills` never overwrites an edit. Reference files are a new kind of
installed file, so that promise has to cover them too.

**Independent Test**: `init` and `update-skills` on a fresh workspace and on a pre-101 workspace with an edited skill
and an edited agent, checking files, manifest entries and the report.

**Acceptance Scenarios**:

1. **Given** a new workspace, **When** `spectra init` runs, **Then** each skill that has a reference file gets it under
   `.claude/skills/<name>/`, recorded in the skills manifest.
2. **Given** a pre-101 workspace, **When** `update-skills` runs, **Then** the reference files are written and listed
   under "Updated" (the 096 report lists every newly written file there), and unedited skills and agents are refreshed.
3. **Given** the user edited a SKILL.md or an agent, **Then** that file is not touched and is reported as customized; its
   reference file is still added or refreshed (a pre-101 skill doesn't read it, so nothing breaks).
4. **Given** the user edited a reference file, **Then** `update-skills` doesn't overwrite it and reports it as
   customized.
5. **Given** `spectra init --skip-skills`, **Then** no reference file is installed.

### Edge Cases

- A test has no `source_refs`: the prompt is compiled from no documents, as today (same refusal, if any).
- A ref resolves outside the repository and the configured source folder, or to a missing file: the same refusal as
  today, in both modes.
- Two refs name the same section (e.g. different anchor spellings the matcher maps to one heading): the section appears
  once.
- An anchor matches a heading that appears twice in the document: the 085 matcher's rule decides, the same way baseline
  and triage decide.
- A cited section is empty below its heading: the heading is given; nothing is added from the rest of the document.
- A malformed criteria file: today the dropped criteria load could fail the command; with the load removed, the
  critic prompt no longer depends on the criteria files at all.
- A customised `critic-verification.md` template: not used by `compile-critic-prompt`, as today (out of scope).
- A document whose cited sections together exceed 8,000 characters: cut as today; the quickstart counts such items.
- The legacy path form (`--test <path>` without `--suite`): unchanged; it reads `--docs` whole.
- `--full-docs` together with `--docs`: `--docs` decides, as today; `--full-docs` changes nothing more.
- The critic prompt is compiled again for a test whose documents changed but whose content didn't: freshness behaves as
  today (the fingerprint covers the test only).
- Nesting works for some tests and the critic fails to start for one: that test comes back `unverified: <reason>`
  after the one retry; `critic: not run` is only for a subagent that couldn't start the critic at all.
- A subagent returns no verdict line for a test it wrote: the main session treats it as `critic: not run` for that test
  and runs the critic for it, so no test loses its verification silently.
- The from-description flow (generate Step 4) and the review-flagged skill have no work subagent: the main session keeps
  running the critic, with the section-scoped input.
- The user answers "no" to the re-verify question: no critic runs in any updater or repairer call, and the edited tests
  are reported as unverified, as today.
- A pre-101 edited SKILL.md stays self-contained and keeps working; it never reads a reference file.
- A pre-101 unedited agent that still has the ignored fields: `update-skills` replaces it with the new one.

## Requirements *(mandatory)*

### Functional Requirements

**F1 — section-scoped critic input**
- **FR-001**: In suite mode, when no `--docs` is given, `compile-critic-prompt` MUST give each `source_ref` as follows:
  an anchor ref → the section the 084/085 resolver and matcher find, with its subsections; a whole-file ref → the whole
  file. Each section or file appears once, in first-reference order; a whole-file ref to a document replaces that
  document's sections.
- **FR-002**: An anchor that doesn't match a heading MUST fall back to the whole document, with one line in the prompt
  naming the anchor and saying the whole document is given.
- **FR-003**: The critic prompt MUST hold no acceptance criteria in either mode (as today); the criteria load whose
  result the prompt never used MUST be removed from `compile-critic-prompt`.
- **FR-004**: `--full-docs` MUST give today's input: the referenced files under today's cut and document limit,
  byte-identical to `6a4c41a` for the same workspace.
- **FR-005**: In each mode, compiling again with the same test and workspace MUST give a byte-identical prompt (no
  timestamps or unordered output).
- **FR-006**: Freshness (090/091/097), every refusal, exit code and the JSON shape of `compile-critic-prompt` MUST stay
  unchanged; `--docs` and the legacy path form MUST behave as today.
- **FR-007**: The `spectra-critic` agent MUST accept an optional `full_docs: true` input that adds `--full-docs` to its
  compile call; skills MUST NOT pass it by default.
- **FR-008**: The F1 quality gate (User Story 2) MUST be run and recorded in the tasks' Notes before F2 or F3 changes
  a skill; a failed gate MUST stop the work with a report.

**F2 — lean skills with reference files**
- **FR-009**: The bundled `spectra-generate` and `spectra-update` SKILL.md MUST each have at most 16,000 characters
  (≈ 4,600 tokens at 3.5 characters per token), and each one's measured token count MUST be under 5,000; if either is
  over, more reference material moves out.
- **FR-010**: Reference material the main session rarely needs MUST move to a reference file in the same skill folder
  (e.g. `.claude/skills/spectra-generate/reference.md`). SKILL.md MUST name the file and say when to read it.
- **FR-011**: The NON-STOP contract and the 075 `written == 0` guard text MUST stay verbatim in SKILL.md, as MUST every
  decision point and STOP; a regression guard MUST check this.
- **FR-012**: `init` and `update-skills` MUST install, record in the skills manifest and refresh reference files under
  the 096 rules: an edited file (SKILL.md, reference file or agent) is never overwritten and is reported as customized;
  a newly written file is listed under "Updated"; `--skip-skills` installs none.
- **FR-013**: Every existing skill text guard that pointed at moved text MUST be retargeted to the reference file, and
  each retargeted guard MUST be listed in the tasks' Notes.

**F3 — the critic inside the work subagents**
- **FR-014**: `spectra-generator`, `spectra-updater` and `spectra-repairer` MUST have the Agent tool and MUST run
  `spectra-critic` once per test they wrote, each in a fresh context on `ai.critic.model`, then
  `spectra ai ingest-verdict --suite S --all --ids <their ids>`, including today's one retry for a test with no
  `results` entry.
- **FR-015**: They MUST return one line per such test: `<id> | verdict <v> <score>` (the CLI's verdict),
  `<id> | critic-disagrees <cli verdict> (critic: <critic's line>)`, or `<id> | unverified: <reason>`, alongside the lines
  they return today.
- **FR-016**: The skills MUST route by those lines exactly as today (drop only when the critic's line agrees, one
  repair, `ingest-grounding --all --ids` and `--repaired`, the 075 guard). Grounding writes, `record-drop` and deletes
  MUST stay in the main session's CLI calls.
- **FR-017**: When a subagent can't start the critic, it MUST return `critic: not run`, and the skill MUST then run the
  critic per test as today. A test the subagent wrote but returned no verdict line for MUST be treated the same way.
- **FR-018**: The update skill MUST ask its re-verify question once per run, before the first updater call, with the
  same wording, and pass the answer as `verify: yes|no` to the updater and repairer; with `no`, no critic runs and the
  edited tests are reported as unverified. Tests kept with a manual verdict MUST never go to the critic.
- **FR-019**: The from-description flow and the review-flagged skill MUST keep running the critic in the main session.

**F4 — agent frontmatter Claude Code reads**
- **FR-020**: The six bundled agents MUST NOT carry `disable-model-invocation` or `context`.
- **FR-021**: Each agent's description MUST start with "Only for use by the " and name the skill or skills that use it
  (and, for the critic, their subagents), and MUST say not to delegate to it directly.
- **FR-022**: A guard test MUST allow only `name`, `description`, `tools` and `model` in bundled agents' frontmatter
  (source and installed form).
- **FR-023**: Skill text that describes `context: fork` or "never auto-invoked" as an agent property MUST be corrected.

**Cross-cutting**
- **FR-024**: Every change MUST have a test that fails on `6a4c41a` (skill and agent text guards included). The F1 gate,
  the `/context` measurements and the `claude --debug` check are manual and recorded.
- **FR-025**: No change to verdict, grounding, baseline, test or criteria file formats; no config section becomes
  required (083); human summaries stay on stderr in JSON mode (constitution III).
- **FR-026**: Generation and update outcomes MUST be unchanged apart from the critic's input.
- **FR-027**: The test suite MUST stay fully green, verified in a fresh checkout (099 lesson), against a workspace made by
  the real `spectra init`, and against a pre-101 workspace with an edited skill and an edited agent.
- **FR-028**: Docs: `usage.md`, `cli-reference.md` (`compile-critic-prompt --full-docs`, section-scoped input),
  `CHANGELOG.md`, and a CLAUDE.md Recent Changes row.

### Key Entities

- **Critic prompt**: the text `compile-critic-prompt` emits for one test: the test, its cited sections (or whole
  documents), with no criteria.
- **Cited section**: the heading an anchor ref names, with its body and subsections, as the 084/085 matcher finds it.
- **Reference file**: a file next to a skill's SKILL.md, installed and recorded like it, that the skill reads only when
  needed.
- **Verdict line**: one line per test a work subagent returns after its critic runs: the CLI's verdict, a
  disagreement, or unverified.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the same ≥ 30 tests and critic model, section mode and `--full-docs` agree on ≥ 90% of the verdicts of
  the "seen" tests; on the "cut" tests section mode rates at least as many grounded; overall, section mode's grounded
  rate is no more than 5 points below full-docs'.
- **SC-002**: On the gate's tests, the median critic prompt in section mode is no larger than in `--full-docs` mode, and
  100% of the "cut" tests get their cited text in section mode; median and max of both modes are recorded.
- **SC-003**: Both authoring SKILL.md bodies fit within the 5,000 tokens Claude Code re-attaches after compaction
  (≤ 16,000 characters each, and a measured token count under 5,000 each), and a run long enough to compact completes
  Steps 8–9.
- **SC-004**: A generation of ≥ 24 tests and an update of ≥ 3 groups each grow the main session by no more than 300
  tokens per test, and by less than the same run on `6a4c41a`.
- **SC-005**: 100% of the tests a run creates or edits end with the same outcome categories as today (grounded, repaired,
  dropped, unverified, manual verdict kept), each listed in the report by id.
- **SC-006**: 0 bundled agents carry a field outside the allowed list, and `claude --debug` shows 0 "does not recognize
  or apply" lines for SPECTRA agents.
- **SC-007**: A pre-101 workspace with edited files ends with 0 edited files overwritten and every reference file
  installed.
- **SC-008**: Every change has a test that fails on `6a4c41a`, and the whole suite passes with 0 failures in a fresh
  checkout.

## Assumptions

- Claude Code's documented behaviour holds for the versions SPECTRA supports: nested subagents up to three levels by
  default, unknown subagent fields ignored, skill files next to SKILL.md readable on demand, and the first 5,000 tokens
  of each invoked skill re-attached after compaction.
- Command-heavy Markdown can run at about 3.5 characters per token, so the character cap (deterministic, guarded by a
  test) is set with that ratio, and the token count is measured once by hand to confirm it.
- The work subagents can read the verdict ingest's `results` and the critic's final line the same way the main session
  does today; no new CLI output is needed.
- The 084/085 section extraction used by baseline and triage, and 094's rule that an outer cited section absorbs the
  refs inside it, are correct for the critic too; this spec reuses them and doesn't change them.
- The progress page keeps its phases; the critic runs of a batch show under that batch's step.
- The gate's ≥ 30 tests come from a real suite with section refs (the project's own fixture suite or a
  workspace the team uses), run on the default `ai.critic.model` unless the Notes say otherwise.

## Out of Scope

- The ImageSharp upgrade.
- Batching several tests into one critic call (it would break the fresh-context-per-test contract).
- Section-scoped input for repair prompts (`compile-repair-prompt`, `compile-repair-batch`); they keep reading whole
  documents.
- A config setting that makes `--full-docs` the default.
- Any other feature.
