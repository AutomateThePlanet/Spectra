# Feature Specification: Review Fixes for 101 and the Critic Prompt Template

**Feature Branch**: `102-critic-template-review-fixes`
**Created**: 2026-10-19
**Status**: Draft
**Input**: User description: "Spec 102 — review fixes for 101 and the critic prompt template. Spec number from the repo
auto-detect script; branch from fix/doc-change-update-workflow (26bfe88). Every fact below was checked against the code;
re-check before planning." (F1–F9, scope and acceptance as quoted in the request.)

## Context

A review of 101 found two correctness bugs in what the critic sees, two in how the authoring agents report and route
critic results, and five smaller cleanups. The largest one predates 101: the critic has never used its prompt template.

Facts (re-checked on `26bfe88` while writing this spec):
- **F1 — the critic never gets its template.**
  - `compile-critic-prompt` calls `CriticPromptCompiler.Compile(test, docs)` with no template loader, in both the
    `--suite` form (`CompileCriticPromptCommand.cs` line 255) and the legacy `--test <path>` form (line 108).
  - With no loader, `CriticPromptBuilder.BuildSystemPrompt` returns its hard-coded string. That string lacks the
    "Technique Verification" section (037) and the "Arithmetic Verification" mandate (075) that the bundled
    `src/Spectra.CLI/Prompts/Content/critic-verification.md` has.
  - An edited `.spectra/prompts/critic-verification.md` is ignored, although `docs/customization.md` and the
    `spectra-prompts` skill say it is used. This has been true since 055 (`2f1112b`); 101 listed it as out of scope.
  - Every other compile command builds a template loader for the workspace (`compile-prompt`,
    `compile-analysis-prompt`, `compile-extraction-prompt`, the batch prompt, `compile-update-prompt`).
  - The template declares `test_case`, `source_document` and `acceptance_criteria` placeholders; its body uses none.
  - Besides the command, only tests (`VerificationIntegrationTests`) build a `CriticPromptBuilder` without a loader.
  - `docs/customization.md` also says to change the critic model by editing `model:` in the agent file; since 097 the
    setting is `ai.critic.model`, which `init` / `update-skills` write into the installed agent.
- **F2 — mixed-case refs to one file.** `CriticSourceLoader` groups resolved refs with an ordinal `FullPath ==`
  comparison, while `SourceRefResolver` deduplicates files case-insensitively on every OS except Linux, and each
  resolved ref keeps its own casing. On Windows, `docs/Auth.md#login` + `docs/auth.md#logout` give the critic only the
  Login section, and an anchor-less `docs/auth.md` next to `docs/Auth.md#login` never triggers the whole-file rule.
- **F3 — unchanged repairs re-run the critic.** `spectra-repairer` step 5 starts the critic for every `written` and
  `unchanged` id, but `ingest-update` removes verdict files only for `written` outcomes, so an unchanged test still has
  a fresh verdict for exactly its current content. `spectra-generate` 8b and `spectra-update`'s repair section describe
  the same "verdict line per `written` / `unchanged` test".
- **F4 — line kinds collide.** The generator returns `<id> | <title>` per created test and the verdict lines
  `<id> | verdict …`, `<id> | unverified: …`, `<id> | critic-disagrees …`. A title such as "Verdict banner is shown" or
  "Unverified: …" reads as a verdict line.
- **F5 — a misplaced warning.** `CriticSourceLoader` warns that "more than one heading shares the anchor key" for a ref
  even when its section is absorbed into another cited section and so is not given on its own.
- **F6 — dead parameter.** `CriticPromptCompiler.Compile` / `Assemble` still take `criteriaContext`, which no caller
  passes since 101; `using Spectra.CLI.Criteria;` in `CompileCriticPromptCommand.cs` is unused.
- **F7 — repeated matching.** `CriticSourceLoader` runs the section matcher about n² times per document (covered
  headings per pair of refs), and extraction re-matches and re-scans line starts for each kept section.
- **F8 — three copies.** The ~25-line "Verify with the critic" step is copied into `spectra-generator`,
  `spectra-updater` and `spectra-repairer`; agents cannot include a shared file, and nothing checks the copies agree.
- **F9 — two names for one tool.** The agents list the subagent tool as `Agent`; the skills' tool list
  (`SkillResourceLoader.GenerateToolsList`) says `Task`, skill text says "with the Task tool", and the loader's comment
  still describes "the context:fork critic subagent".

## Decisions

Made while writing this spec (open to change at `/speckit.clarify`):

- **One critic prompt.** Every critic prompt `compile-critic-prompt` emits is built from the `critic-verification`
  template. Whether the hard-coded string in `CriticPromptBuilder` is still reachable is decided in planning; if no
  production path reaches it, it is removed, and tests that used it build their prompt through the template. Two prompts
  are never kept side by side.
- **Placeholders stay declared.** The template keeps its three placeholders so `spectra prompts validate` and customised
  copies that use them keep working; the command passes empty values, as the loaded-template path does today.
- **Unchanged repairs keep their verdict (F3).** An `unchanged` repair is not sent to the critic. Its id still goes to
  the same `ingest-verdict --suite <s> --all --ids …` call, so it gets a verdict line from its existing verdict. If that
  call gives it no `results` entry, the existing retry rule applies (the critic once, then the ingest again, then
  `unverified: <reason>`). With `verify: no` nothing changes: no critic, no verdict lines. The fallback follows the same
  rule: when the repairer returns `critic: not run`, the main session starts the critic only for the `written` ids; the
  `unchanged` ids go straight into the same `ingest-verdict --suite <s> --all --ids …` call and are routed by their
  existing verdict.
- **The `--repaired` grounding list keeps `unchanged` ids.** The repair was attempted, so `--repaired --repair-attempts 1`
  stays true for them; only the wording that calls these ids "re-critiqued" changes. This is a deliberate change to 101
  FR-026 ("outcomes unchanged apart from the critic's input") for unchanged repairs only: their outcome now comes from
  the verdict the critic gave before the repair attempt, not from a second critic run on identical content.
- **Line keyword (F4).** The generator's title line becomes `<id> | created: <title>`. Verdict lines stay exactly as
  they are in all three agents.
- **Tool name (F9).** The canonical name of Claude Code's subagent tool is taken from the current Claude Code docs at
  planning time; the expected result is `Agent`, with `Task` an accepted alias. Both the agents and the skills use the
  canonical name.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The critic uses the verification template (Priority: P1)

A QA lead customises `.spectra/prompts/critic-verification.md` to tighten the verdict rules. The next critic run uses
the customised prompt. Without a customised copy, the critic uses the bundled template, including its technique and
arithmetic checks.

**Why this priority**: Since 055 the critic has run without the technique and arithmetic checks the bundled template
asks for, and the documented customisation has never worked. Every verdict in every run is affected.

**Independent Test**: In a workspace made by `spectra init`, compile a critic prompt with `--suite X --test ID`, with
and without `--full-docs`, and with the legacy `--test <path>` form; check the system part. Edit the workspace template
and compile again.

**Acceptance Scenarios**:

1. **Given** no workspace copy of the template, **When** `compile-critic-prompt --suite X --test ID` runs, **Then** the
   prompt holds the bundled template's "Technique Verification" and "Arithmetic Verification" sections.
2. **Given** an edited `.spectra/prompts/critic-verification.md`, **When** the prompt is compiled, **Then** the edited
   text appears in it and the bundled text it replaced does not.
3. **Given** `--full-docs`, the cited-section mode and the legacy `--test <path>` form, **Then** all three build their
   system part from the same template.
4. **Given** `--full-docs`, **Then** the user part of the prompt (test and documents) is byte-identical to the user part
   of `full-docs-6a4c41a.txt`, which stays as the pre-101 record; only the system part differs, and the whole output is
   recorded as a new golden `full-docs-102.txt`.
5. **Given** `--output-format json`, **Then** stdout is only the JSON document.
6. **Given** the bundled template, **Then** `spectra prompts validate` reports it valid.
7. **Given** `docs/customization.md`, **Then** it says that edits to the critic template are used and that the critic
   model is set with `ai.critic.model`.

---

### User Story 2 - Every cited section reaches the critic, whatever its path casing (Priority: P1)

A QA engineer on Windows has tests whose refs spell the same document with different casing. The critic gets every
section those refs cite, and the whole document when one of them has no anchor.

**Why this priority**: A dropped section makes the critic judge a test against less than it cites, which can turn a
grounded test into a partial or hallucinated one and trigger a needless repair or drop.

**Independent Test**: Load critic sources for refs that differ only in case, with the case-insensitive path comparer
injected so the test behaves the same on every OS.

**Acceptance Scenarios**:

1. **Given** refs `docs/Auth.md#login` and `docs/auth.md#logout` to one file, under the case-insensitive comparer,
   **Then** the critic gets both sections, once each, in first-reference order.
2. **Given** refs `docs/Auth.md#login` and `docs/auth.md` (no anchor), **Then** the critic gets the whole file, once.
3. **Given** the case-sensitive comparer (Linux), **Then** the behaviour is as today.
4. **Given** the grouping, **Then** it uses the same comparer the resolver uses to deduplicate files (one source of
   truth).

---

### User Story 3 - A repair that changed nothing is not judged again (Priority: P2)

A QA engineer runs a generation; the repairer leaves two partial tests unchanged. The run does not spend two critic
calls on content the critic has just judged, and the two tests end as they would have before.

**Why this priority**: Each critic call is a fresh subagent with the test and its sections; an unchanged test gains
nothing from a second call.

**Independent Test**: Content tests on `spectra-repairer`, `spectra-generate` (and its reference file) and
`spectra-update`, plus an `ingest-verdict --all --ids` run on an unchanged repaired test with its existing verdict file.

**Acceptance Scenarios**:

1. **Given** a repair batch with `written` and `unchanged` ids and `verify: yes`, **Then** the repairer starts the
   critic only for the `written` ids and passes both sets to one `ingest-verdict --suite <s> --all --ids …` call.
2. **Given** an unchanged id whose existing verdict is partial, **Then** it returns `<id> | verdict partial <score>` and
   the skills route it as flagged-partial (generate) or flagged for review / unverified (update), as today.
3. **Given** an unchanged id whose existing verdict is hallucinated, **Then** it follows the existing hallucinated path
   of the calling skill.
4. **Given** an unchanged id with no `results` entry, **Then** the existing retry rule applies, ending in
   `unverified: <reason>` if still none.
5. **Given** `verify: no`, **Then** no critic runs and no verdict lines are returned, as today.
6. **Given** the `--repaired` grounding call, **Then** its `--ids` still lists the `written` and `unchanged` repairs
   minus any dropped, never a failed one.
7. **Given** the repairer returned `critic: not run` with one `written` and one `unchanged` id, **When** the main
   session runs the fallback, **Then** one critic runs (for the `written` id) and both ids are routed from the ingest
   results.

---

### User Story 4 - A test title can never be mistaken for a verdict (Priority: P2)

A generated test is titled "Verdict banner is shown after save". The main session reads it as a created test, not as a
verdict line.

**Why this priority**: A misread line can skip a test's verification or route it as dropped.

**Independent Test**: Content tests on `spectra-generator` and `spectra-generate`.

**Acceptance Scenarios**:

1. **Given** the generator's output contract, **Then** each created test is reported as `<id> | created: <title>`, and
   every line kind starts, after `<id> | `, with a fixed keyword (`created:`, `verdict`, `critic-disagrees`,
   `unverified:`).
2. **Given** `spectra-generate`, **Then** its Step 7 line list and routing read `created:` lines as created tests, and
   the from-description flow does the same if it reads these lines.
3. **Given** the updater and repairer, **Then** their verdict lines are unchanged.

---

### User Story 5 - Smaller fixes and guards (Priority: P3)

Maintainers get correct warnings, no dead code, less repeated work and a guard that keeps the three critic steps in step.

**Independent Test**: Unit and content tests per item.

**Acceptance Scenarios**:

1. **(F5)** **Given** a ref whose anchor key matches more than one heading, **When** its section is given, **Then** the
   warning is emitted; **When** its section is absorbed into another cited section, **Then** it is not.
2. **(F6)** **Given** the critic compiler, **Then** it no longer takes a criteria parameter, and the command has no
   unused import.
3. **(F7)** **Given** a document with n cited refs, **Then** the section matcher runs at most once per ref and the line
   starts are scanned once per document, proved by counts; the existing loader tests and the golden's user part stay
   identical.
4. **(F8)** **Given** the three work agents, **Then** a content test checks that the "Verify with the critic" block (from
   the Agent call through "routes each test by your verdict line.") is identical in all three, except for an explicit list of
   allowed differences: the step number, which ids are verified, and the `verify: no` rule.
5. **(F9)** **Given** the agents and skills, **Then** both name the subagent tool by its canonical name, the loader's
   comment describes the current critic setup, and installed skills update under the 096 rules (an unedited copy is
   updated, an edited one is kept and reported as customised).

### Edge Cases

- The workspace template is missing, unreadable or invalid: the same loader rules as the other compile commands
  (workspace copy, else the bundled one).
- A customised template uses `{{acceptance_criteria}}`, `{{test_case}}` or `{{source_document}}` in its body: each
  resolves to empty, as the loaded-template path does today; the test and documents stay in the user part.
- A refusal (missing test, missing source, missing input): unchanged; a template problem never hides the existing
  refusal reasons.
- `--docs <path>` given: the documents are used whole, as today; the system part still comes from the template.
- Two refs differ only in case and one of them names a missing anchor: the whole file is given once, with the existing
  "anchor not found" note.
- On Linux, `docs/Auth.md` and `docs/auth.md` are two files: they stay two documents, as today.
- An unchanged repair whose verdict file was removed by hand: no `results` entry, so the retry rule runs the critic once.
- A generated test title starts with `created:`: still unambiguous, since the keyword comes first and the title is
  everything after it.
- An installed skill or agent edited by the user before 102: kept and reported as customised; the user can run
  `update-skills --force` to take the new text.

## Requirements *(mandatory)*

### Functional Requirements

**F1 — the critic prompt template**
- **FR-001**: `compile-critic-prompt` MUST build the system part of every prompt from the `critic-verification`
  template, in the `--suite` and legacy `--test <path>` forms and in cited-section and `--full-docs` modes.
- **FR-002**: It MUST load the template with the same rules as the other compile commands: the workspace copy in
  `.spectra/prompts/`, else the bundled one.
- **FR-003**: There MUST be one critic system prompt. If the hard-coded string in `CriticPromptBuilder` is unreachable
  from production code, it MUST be removed and its test uses switched to the template; the planning decision is
  recorded in research.
- **FR-004**: The bundled template's frontmatter MUST stay valid for `spectra prompts validate`; its placeholders are
  resolved with empty values.
- **FR-005**: A new golden `full-docs-102.txt` MUST be recorded from the `--full-docs` output; `full-docs-6a4c41a.txt`
  stays unchanged as the pre-101 record, and a test MUST assert that the output's user part (test and documents) is
  byte-identical to that file's user part.
- **FR-006**: `docs/customization.md` MUST describe the critic template as used by `compile-critic-prompt`, and MUST say
  the critic model is set with `ai.critic.model` (rendered into the agent by `init` / `update-skills`), not by editing
  the agent's `model:`.

**F2 — path casing**
- **FR-007**: `CriticSourceLoader` MUST group resolved refs by file with the same comparer `SourceRefResolver` uses to
  deduplicate files.
- **FR-008**: Tests MUST cover mixed-case anchored refs (both sections) and a mixed-case anchor-less ref (whole file)
  with the case-insensitive comparer on every OS.

**F3 — unchanged repairs**
- **FR-009**: `spectra-repairer` MUST start the critic only for `written` ids, and MUST pass `written` and `unchanged`
  ids to one `ingest-verdict --suite <s> --all --ids …` call, returning a verdict line for each.
- **FR-010**: `spectra-generate` (8b, re-critic routing, the `--repaired --ids` list and its reference file where it
  describes this) and `spectra-update` (repair section) MUST route an unchanged repair by its existing verdict: partial →
  flagged-partial / unverified as today, hallucinated → the existing hallucinated path. When the repairer returns
  `critic: not run`, their fallback MUST start the critic only for the `written` ids and pass the `unchanged` ids
  straight into the same `ingest-verdict --suite <s> --all --ids …` call, routing them by their existing verdict.
- **FR-011**: The spec and the CHANGELOG MUST state that this changes 101 FR-026 for unchanged repairs only.

**F4 — line kinds**
- **FR-012**: `spectra-generator` MUST report each created test as `<id> | created: <title>`; verdict lines in the three
  agents MUST stay as they are.
- **FR-013**: `spectra-generate` (Step 7 line list, routing) and the from-description flow, if it reads these lines,
  MUST read the new form; content tests MUST pin it.

**F5–F9 — cleanups and guards**
- **FR-014**: The duplicate-anchor warning MUST be emitted only for sections that are given.
- **FR-015**: The critic compiler MUST NOT take a criteria parameter; the unused import MUST be removed. This lands after
  FR-001.
- **FR-016**: `CriticSourceLoader` MUST compute each match's covered heading range once from the matches and heading
  levels and slice each document once, with the same output; a test MUST prove the reduced matcher and scan counts.
- **FR-017**: A content test MUST check that the shared critic-verification block is identical in the three work agents,
  apart from an explicitly listed set of differences.
- **FR-018**: The subagent tool MUST have one name — the canonical one from the current Claude Code docs — in the
  agents' `tools`, the skills' tool list and skill text (unless a test pins the old text, which is then updated), and
  the loader comment MUST describe the current critic setup. Installed skills MUST update under the 096 manifest rules,
  covered by the existing `update-skills` tests.

**Cross-cutting**
- **FR-019**: Every change F1–F5 and F7–F9 MUST have a test that fails on `26bfe88` (F7's count test and F8's guard
  included). F6 (the removed parameter and `using`) is checked by the compiler (TreatWarningsAsErrors); no test is added
  for it.
- **FR-020**: No change to verdict, grounding, baseline, test or criteria file formats; no config section becomes
  required (083); in JSON mode stdout is only the JSON document (constitution III). The skill size and placement rules
  in Assumptions (SKILL.md ≤ 16,000 characters; Steps 7–9 routing stays in SKILL.md) hold after every skill edit.
- **FR-021**: The build MUST have 0 C# warnings, and the full suite MUST pass, also in a fresh `git worktree` checkout
  with `core.autocrlf=true`.
- **FR-022**: Docs: `CHANGELOG.md`, the CLAUDE.md Recent Changes row, `docs/customization.md`,
  `docs/grounding-verification.md` and `docs/cli-reference.md` where they describe the critic prompt or repair
  re-verification.

### Key Entities

- **Critic verification template**: `critic-verification.md`, bundled and optionally customised in `.spectra/prompts/`;
  the only source of the critic's system prompt.
- **Critic prompt**: the system part (from the template) and the user part (the test and its cited sections or whole
  documents).
- **Path comparer**: the resolver's rule for whether two ref paths name one file (case-insensitive except on Linux).
- **Agent result line**: one `<id> | <keyword> …` line a work agent returns: `created:`, `written`, `unchanged`,
  `failed`, `verdict`, `critic-disagrees`, `unverified:`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of critic prompts compiled in any form or mode contain the template's Technique Verification and
  Arithmetic Verification sections, and an edit to the workspace template appears in 100% of them.
- **SC-002**: The `--full-docs` user part is byte-identical to 101's for the golden workspace.
- **SC-003**: With mixed-case refs to one file, the critic gets 100% of the cited sections (or the whole file when one ref
  has no anchor).
- **SC-004**: The bundled repairer starts the critic only for `written` ids, and its `unchanged` ids appear in the one
  `ingest-verdict --all --ids` call, proved by the agent and skill text rows (the merge criterion). In a live run, a
  repair batch with k unchanged ids starts k fewer critic subagents than on `26bfe88`; this is an optional manual check
  (quickstart §3), recorded as open if not run, and not a merge blocker.
- **SC-005**: 0 created-test lines can be read as a verdict line, whatever the title.
- **SC-006**: For a document with n cited refs, the section matcher runs at most n times (down from about n²).
- **SC-007**: 0 C# warnings; 0 test failures, in the working checkout and in a fresh worktree.

## Assumptions

- The current Claude Code docs name the subagent tool `Agent` and accept `Task` as an alias (confirmed while planning:
  renamed in v2.1.63; research R9).
- The bundled template is the intended critic prompt: its body (comments aside) contains every line of the hard-coded
  string, plus the 037 and 075 sections (checked on `26bfe88`).
  Re-recording the golden's system part is expected; the critic's judgement may change because of the added checks,
  which is the point of F1.
- Both authoring SKILL.md bodies (`spectra-generate`, `spectra-update`) stay ≤ 16,000 characters, LF-normalised (101
  FR-009, `LeanSkillTests`). Steps 7–9 and all routing in them stay in SKILL.md, and reference.md is never read during
  Steps 7–9, so the steps survive compaction whole.
- An unchanged repair's verdict file is fresh (090/091 fingerprint over the unchanged content), so `ingest-verdict`
  accepts it without `VERDICT_STALE`.
- The 101 F1 quality gate (101 tasks T016–T019) runs after 102, by hand, because F1 changes the critic prompt; the 101
  decision rule stands (if section mode loses, `compile-critic-prompt` defaults to `--full-docs`).

## Out of Scope

- The 101 F1 quality gate (101 tasks T016–T019).
- The ImageSharp upgrade.
- Skills' `tools:` vs `allowed-tools:` frontmatter.
- Changes to the review-flagged skill, except its rendered `tools:` line (F9).
- Any other behaviour change.
