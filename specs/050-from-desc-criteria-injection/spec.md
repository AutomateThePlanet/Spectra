# Feature Specification: From-Description Criteria Injection

**Feature Branch**: `050-from-desc-criteria-injection`
**Created**: 2026-06-02
**Status**: Draft
**Input**: User description: "Fix the from-description generation flow so the loaded acceptance criteria are passed through to the generation agent as the MANDATORY criteria-mapping instruction the batch flow already uses, instead of being passed as `null`. Keep `grounding.verdict = manual` for this flow as a deliberate, documented decision."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - From-description tests participate in acceptance-criteria coverage (Priority: P1)

A test author runs `spectra ai generate --suite checkout --from-description "Verify guest checkout works when cart contains a single item" --context "..."` against a workspace that has acceptance criteria extracted into `docs/criteria/`. Today, the generated `.md` file is written to disk but its `criteria:` frontmatter field is inconsistently populated — sometimes empty, sometimes partial — because the model never received the explicit "you MUST map each test case to matching acceptance criteria" instruction that the batch flow gets. After the fix, the same command produces a test whose `criteria:` field reliably lists the matching criteria IDs the model identified from the suite's criteria context, so the test appears in subsequent acceptance-criteria coverage reports.

**Why this priority**: This is the user-visible bug the spec exists to fix. Without it, users report "extract criteria on generation is not working," and from-description tests silently fall out of acceptance-criteria coverage even though every other generation flow handles this correctly.

**Independent Test**: Run a from-description generation in a workspace with at least one extracted acceptance criterion that matches the target suite (by component / source-doc / file-name). Inspect the generated `.md`'s YAML frontmatter — `criteria:` MUST contain at least one matching criterion ID. Re-run `spectra ai analyze --coverage` and confirm the new test is counted in acceptance-criteria coverage.

**Acceptance Scenarios**:

1. **Given** a workspace with extracted acceptance criteria matching a target suite, **When** the user runs `spectra ai generate --suite X --from-description "..."`, **Then** the prompt sent to the generation agent contains the MANDATORY criteria-mapping instruction (the same "You MUST map each test case to matching acceptance criteria" block the batch flow uses) and the resulting test file's `criteria:` frontmatter is populated with the IDs the model mapped.
2. **Given** a workspace with no acceptance criteria matching the target suite, **When** the user runs `spectra ai generate --suite X --from-description "..."`, **Then** the prompt contains no criteria-mapping instruction and no loose criteria section, and the resulting test file's `criteria:` frontmatter is empty — matching today's behavior in the "no criteria" case.
3. **Given** the same input as scenario 1, **When** the test is generated, **Then** the criteria content appears exactly once in the outbound prompt (no duplication between an instructional block and a loose body section).

---

### User Story 2 - Verdict semantics remain trustworthy (Priority: P1)

A test reviewer reads `grounding.verdict` on any generated test to know whether an independent critic confirmed the test is grounded in the source documentation. Today, the from-description flow correctly reports `verdict: manual` because it runs no critic. After the criteria-injection fix, a from-description test will have a populated `criteria:` field — but it must continue to report `verdict: manual`, because populating a field is not the same as critic-verified grounding. The reviewer can therefore continue to use `verdict` as a reliable verification signal without re-learning its meaning.

**Why this priority**: Verdict integrity is foundational to how reviewers triage tests. Quietly upgrading the verdict because criteria were populated would assert verification that never happened, breaking the contract the rest of the system depends on.

**Independent Test**: Generate a from-description test in a workspace with matching criteria. Inspect the resulting test file — `grounding.verdict` MUST be `manual`, even when `criteria:` is populated. Run `spectra ai analyze --coverage` — the test MUST be counted in acceptance-criteria coverage but MUST NOT be counted in grounded coverage.

**Acceptance Scenarios**:

1. **Given** a from-description generation that successfully maps criteria, **When** the test file is written, **Then** `grounding.verdict` is `manual`.
2. **Given** any from-description-generated test (criteria mapped or not), **When** coverage is computed, **Then** the test is excluded from "grounded" statistics on verdict alone, regardless of whether `criteria:` is populated.
3. **Given** documentation that describes the from-description flow, **When** a reviewer reads it, **Then** the docs explicitly state that criteria population does not imply verification and that verdict remains `manual` by design.

---

### User Story 3 - Behavior when no criteria match the suite (Priority: P2)

A user runs `spectra ai generate --suite Y --from-description "..."` against a workspace that has no extracted acceptance criteria, or no criteria that match suite Y. The flow MUST behave exactly as it does today in that case: the agent receives an empty or null criteria context, the prompt contains neither the MANDATORY block nor any loose criteria section, and the test is written with an empty `criteria:` field. The fix MUST NOT regress this path or introduce a spurious instructional block when there is nothing to map.

**Why this priority**: Protects users in workspaces that have not yet extracted criteria. Important but secondary because the primary win lands when criteria exist.

**Independent Test**: In a workspace with no matching criteria for the target suite, generate via from-description. Confirm: outbound prompt has no MANDATORY criteria block, no `## Related Acceptance Criteria` section, and the resulting test's `criteria:` field is empty. Behavior matches today's output for the same input.

**Acceptance Scenarios**:

1. **Given** a workspace where no acceptance criteria match the target suite, **When** the user runs from-description generation, **Then** the agent receives an empty or null criteria context and no criteria-mapping instruction is emitted into the prompt.
2. **Given** the same condition, **When** the test file is written, **Then** the `criteria:` frontmatter field is empty (omitted or an empty list), identical to pre-fix behavior.

---

### Edge Cases

- **Criteria exist but none match the target suite** (e.g., all extracted criteria belong to other components): treated as the "no criteria" case — no MANDATORY block, no loose block, empty `criteria:` field.
- **Criteria exist but the model elects not to map any** for a particular description (e.g., the described scenario genuinely is not covered by any criterion): the prompt still contains the MANDATORY block, but the resulting test's `criteria:` field is empty. This is a legitimate model decision and not a bug.
- **`spectra ai generate` invoked without `--from-description`** (the batch flow): unaffected. The fix only changes the from-description call site; batch behavior is identical.
- **Workspace where criteria extraction has never been run**: identical to "no criteria match" — no instruction, no loose block, empty `criteria:` field.
- **Reading a from-description test in coverage reports**: appears in acceptance-criteria coverage when `criteria:` is populated; never appears in grounded statistics regardless.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The from-description generation flow MUST forward the loaded acceptance-criteria context to the generation agent on every invocation (rather than discarding it), so that the agent activates the same MANDATORY criteria-mapping instruction the batch flow uses.
- **FR-002**: When criteria matching the target suite exist, the outbound prompt for a from-description generation MUST include the MANDATORY criteria-mapping instruction (the explicit "You MUST map each test case to matching acceptance criteria" block).
- **FR-003**: The criteria content MUST appear exactly once in the outbound prompt. The pre-existing loose `## Related Acceptance Criteria` body section MUST be removed so the criteria content is not presented twice with conflicting framing.
- **FR-004**: When no acceptance criteria match the target suite, the from-description flow MUST behave identically to today — no MANDATORY block, no loose section, empty or null criteria context — and the resulting test's `criteria:` frontmatter MUST be empty.
- **FR-005**: A from-description-generated test whose model output maps one or more criterion IDs MUST persist those IDs to the written `.md` file's `criteria:` frontmatter field.
- **FR-006**: A from-description-generated test's `grounding.verdict` MUST remain `manual` regardless of whether criteria were injected or mapped. Populating the `criteria:` field MUST NOT cause the verdict to be upgraded.
- **FR-007**: The system MUST NOT introduce any new value into the verification verdict enum, MUST NOT invoke a critic for the from-description flow, and MUST NOT change rendering, coverage data models, or require any data migration as part of this fix.
- **FR-008**: Documentation MUST be updated to state explicitly that (a) from-description tests now have criteria injected as the MANDATORY mapping instruction, (b) their `criteria:` field is populated when the model maps any, (c) they count toward acceptance-criteria coverage, and (d) they are excluded from grounded statistics by design because `verdict` remains `manual` (no independent critic was run).

### Key Entities

- **Criteria context**: The string of extracted acceptance criteria for a target suite, produced by the same loader the batch flow already uses. Already loaded today; the fix is to pass it through the call site instead of discarding it.
- **From-description generation invocation**: A single-test generation request initiated by `spectra ai generate ... --from-description "..."`. Distinguished from batch generation by skipping the analyze phase and bypassing the critic.
- **Generated test file**: A markdown file with YAML frontmatter. Relevant fields for this spec: `criteria:` (list of mapped criterion IDs) and `grounding.verdict` (whose value remains `manual` for this flow by deliberate decision).
- **MANDATORY criteria-mapping instruction**: The existing prompt block, already emitted by the shared generation-agent code path when the criteria-context parameter is non-empty, that tells the model "You MUST map each test case to matching acceptance criteria."

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a workspace with at least one acceptance criterion matching the target suite, 100% of from-description generations whose model output identifies at least one mapping produce a test file whose `criteria:` frontmatter contains those mapped IDs.
- **SC-002**: In a workspace with no matching criteria for the target suite, 100% of from-description generations produce an outbound prompt containing zero criteria-related sections (no MANDATORY block, no loose section), matching pre-fix behavior exactly.
- **SC-003**: The outbound prompt for a from-description generation contains the criteria content at most once — never duplicated between an instructional block and a loose body section.
- **SC-004**: 100% of from-description-generated tests report `grounding.verdict = manual`, regardless of whether `criteria:` is populated.
- **SC-005**: From-description tests with a populated `criteria:` field appear in acceptance-criteria coverage reports (counted toward criteria-coverage percentage) and continue to be excluded from grounded-coverage statistics.
- **SC-006**: Users who previously reported "extract criteria on generation is not working" via the from-description path can no longer reproduce the empty-`criteria:` symptom in workspaces where matching criteria exist.
- **SC-007**: No new public command flags, MCP tool surfaces, frontmatter fields, enum values, or data migrations are introduced by this fix. Existing batch-flow behavior is byte-identical to pre-fix output for the same inputs.

## Assumptions

- The criteria-loading helper used by the from-description code path is already correct — the same loader the batch flow uses, returning the same shape of result. The defect is the discard at the SDK call, not the load.
- The shared generation-agent code path already emits the MANDATORY criteria-mapping instruction when its criteria-context parameter is non-empty. The fix activates that existing path; it does not author a new instruction.
- The model populates `criteria:` reliably once it receives the MANDATORY mapping instruction. This is the behavior observed in the batch flow today and is treated as the baseline.
- Coverage reporting already distinguishes between "criteria populated" (counts toward acceptance-criteria coverage) and "verdict grounded" (counts toward grounded coverage). No coverage-model change is required to make from-description tests appear correctly in the former and stay absent from the latter.
- Spec 049 (which ensures from-description tests are written to `_index.json`) is independent. Each spec can land without the other; together they make from-description tests fully participate in acceptance-criteria coverage end-to-end.

## Decisions

The following decisions are recorded here so they are not re-litigated during planning or review:

- **Verdict stays `manual` for from-description tests.** `grounding.verdict` means "an independent critic (different model family) verified the test is grounded." The from-description flow runs no critic. Claiming `grounded` or `partial` would assert verification that never happened — exactly the failure the dual-model design exists to prevent. Populating `criteria:` is not verification.
- **No new enum value is introduced.** A third value such as `criteria-mapped` would add legend, report, docs, and skill-parser surface for negligible user-visible gain. Declined as defensive scope.
- **No critic invocation is added to the from-description flow.** Doing so would erase the only advantage of from-description (fast, deterministic, single-test, no analyze phase). The flow's value proposition stays intact.
- **The loose body section is removed, not kept alongside the instructional block.** Presenting criteria twice with two different framings weakens the instruction. One canonical placement — the MANDATORY block from the shared generation-agent code path — is the right answer.

## Out of Scope

- Updating the verification verdict enum.
- Running a critic in the from-description flow.
- Any changes to the batch generation flow's prompt, criteria handling, or output.
- Changes to coverage data models, rendering, or report formats.
- Any data migration of existing test files.
- The independent secondary finding that `grounding.verdict` is hard-coded to `manual` — confirmed by investigation to be independent of the criteria bug and a deliberate design choice (see Decisions).

## Documentation Update Checklist

Documentation updates that land with this fix:

- `skills-integration.md` — the from-description note: criteria are now injected as the MANDATORY mapping instruction, `criteria:` is populated, verdict stays `manual` by design.
- `coverage.md` — from-description tests count toward acceptance-criteria coverage but not grounded statistics (verdict `manual`).
- `spectra-generate.md` SKILL — adjust the from-description result presentation if it currently implies criteria are not linked.
- `PROJECT-KNOWLEDGE.md` — Spec 050 row; record the verdict-stays-manual decision.

## Dependencies & Sequencing

- Independent of Spec 047 (merged), Spec 048 (Coverage Guards), Spec 049 (From-Description Write & Index Parity), and Spec 051 (Filter Schema Alignment). May land in parallel with any of them.
- Note interaction with Spec 049: 049 ensures from-description tests are written to `_index.json`; this spec ensures their `criteria:` frontmatter field is populated. Both are needed for from-description tests to participate fully in acceptance-criteria coverage. They are independent at the code level — landing one without the other still leaves the other half working as before.
