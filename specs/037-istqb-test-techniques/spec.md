# Feature Specification: ISTQB Test Design Techniques in Prompt Templates

**Feature Branch**: `037-istqb-test-techniques`
**Created**: 2026-04-10
**Status**: Draft
**Input**: User description: "Feature Spec 035: ISTQB Test Design Techniques in Prompt Templates — Embed formal ISTQB black-box test design techniques (Equivalence Partitioning, Boundary Value Analysis, Decision Table, State Transition, Error Guessing, Use Case) into SPECTRA's behavior-analysis, test-generation, test-update, critic-verification, and criteria-extraction prompt templates so that AI-driven analysis and generation produces systematic, well-distributed test cases instead of generic happy-path scenarios."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Technique-Driven Behavior Analysis (Priority: P1)

A test engineer runs `spectra ai generate <suite>` against documentation that defines a numeric input field with a documented range (e.g., "accepts 1–100"). Instead of producing a single generic "enter a valid value" behavior, SPECTRA's analysis identifies behaviors at every boundary (0, 1, 2, 99, 100, 101) and flags each with the test design technique that produced it (BVA). The breakdown shown to the user contains both a category breakdown (happy_path, boundary, negative, etc.) and a technique breakdown (BVA, EP, DT, ST, EG, UC).

**Why this priority**: This is the core problem the feature solves. Without technique-driven analysis, every other downstream improvement (generation, update, critic) has nothing to work with. Delivering this story alone shifts the distribution of identified behaviors away from happy-path-heavy and toward systematic boundary/negative coverage — which is the primary user-visible outcome.

**Independent Test**: Run `spectra ai generate --analyze-only` on a fixture document that contains a numeric range and a multi-condition rule. Verify the resulting analysis (in `.spectra-result.json` and the progress page) contains: (1) at least 4 BVA behaviors for the range, (2) at least one Decision Table behavior for the rule, (3) a `technique_breakdown` section, and (4) no more than 40% of behaviors in any single category.

**Acceptance Scenarios**:

1. **Given** documentation describing a field that accepts 3–20 characters, **When** the user runs analysis, **Then** the result includes behaviors testing 2, 3, 4, 19, 20, and 21 characters, each tagged with technique `BVA`.
2. **Given** documentation describing "discount applies if member AND order > $100", **When** the user runs analysis, **Then** the result includes at least one behavior per condition combination, each tagged with technique `DT`.
3. **Given** documentation describing a workflow with states A → B → C, **When** the user runs analysis, **Then** the result includes both valid transition behaviors and at least one invalid transition behavior, each tagged with technique `ST`.
4. **Given** any analyzed document, **When** the user views the analysis output, **Then** the technique breakdown section lists counts for each ISTQB technique used, alongside the existing category breakdown.

---

### User Story 2 - Technique-Aware Test Step Writing (Priority: P2)

When SPECTRA generates the actual test case content for a behavior tagged with a technique, the resulting test steps reference the concrete values from that technique rather than vague descriptions. A boundary-tagged test says "Enter a username with exactly 21 characters" rather than "Enter a very long username". A decision-table test states all condition values explicitly in preconditions.

**Why this priority**: Identifying the right behaviors (Story 1) is necessary but not sufficient — if the generated test still says "enter invalid input", the boundary discipline is lost. This story closes the loop between analysis intent and generated test content. It depends on Story 1 (technique tags must exist before generation can use them).

**Independent Test**: Provide a synthetic behavior set with a `technique: BVA` entry titled "Field rejects 21 chars (max 20)". Run generation. Verify the generated test step text contains the literal value "21" and references the maximum "20", not generic phrasing like "very long".

**Acceptance Scenarios**:

1. **Given** a behavior tagged with technique `BVA`, **When** the test is generated, **Then** the steps contain the exact boundary numeric value, not a generic description.
2. **Given** a behavior tagged with technique `EP`, **When** the test is generated, **Then** the steps name the equivalence class explicitly.
3. **Given** a behavior tagged with technique `DT`, **When** the test is generated, **Then** preconditions state all condition values explicitly.
4. **Given** a behavior tagged with technique `ST`, **When** the test is generated, **Then** the steps state the starting state, the action, and the resulting state.

---

### User Story 3 - Technique-Aware Update Classification (Priority: P3)

A test engineer has an existing test suite. The source documentation is updated so that a previously documented range "1–100" becomes "1–200". When the engineer runs `spectra ai update`, tests that use generic mid-range values (e.g., "Enter 50") for the changed range are flagged as OUTDATED with a proposal to either add new boundary tests at the new max or update existing tests to use the new boundary value.

**Why this priority**: Update is a slower-burn benefit — it only matters once users have an existing suite and documentation evolves. Delivering Stories 1 and 2 first gives immediate value to new generation; Story 3 protects that value over time.

**Independent Test**: Take an existing test that uses value `50` for range `1–100`. Change the documentation to range `1–200`. Run `spectra ai update`. Verify the test is classified as OUTDATED with a reason citing the changed boundary, and the proposed update references either `200` or `201`.

**Acceptance Scenarios**:

1. **Given** a documented numeric range whose max value changes, **When** update runs, **Then** tests targeting that range are flagged OUTDATED with a boundary-specific reason.
2. **Given** new business rule conditions added to documentation, **When** update runs, **Then** missing decision table coverage is flagged OUTDATED.
3. **Given** documentation adds a new workflow state, **When** update runs, **Then** missing state transition coverage is flagged OUTDATED.

---

### User Story 4 - Critic Verification of Technique Claims (Priority: P3)

When the critic verifies a generated test, it does not just check that the test is grounded in the documentation overall — it specifically verifies that boundary values, equivalence classes, and state transitions claimed by the test match the documented values. A test claiming "Enter 21 chars (above 20-char max)" against documentation stating max is 25 receives a PARTIAL verdict with a specific unverified claim message.

**Why this priority**: The critic catches hallucinated boundary values that would otherwise produce confidently wrong tests. This is most valuable once Stories 1 and 2 are producing technique-tagged tests — without them, the critic has nothing technique-specific to verify.

**Independent Test**: Feed the critic a test claiming `21` is above a 20-char maximum, alongside source docs stating the actual maximum is 25. Verify the verdict is PARTIAL and the unverified claim references the boundary mismatch.

**Acceptance Scenarios**:

1. **Given** a BVA test whose boundary value does not match the documented range, **When** the critic runs, **Then** the verdict is PARTIAL with a boundary-mismatch claim.
2. **Given** a DT test using a condition not present in documentation, **When** the critic runs, **Then** the verdict is HALLUCINATED.
3. **Given** an ST test claiming a transition path that the documentation does not support, **When** the critic runs, **Then** the verdict is PARTIAL.

---

### User Story 5 - Technique Hints on Acceptance Criteria (Priority: P3)

When SPECTRA extracts acceptance criteria from documentation, criteria mentioning numeric ranges, multi-condition rules, workflows, or input categories carry an optional `technique_hint` field (BVA, DT, ST, EP). These hints flow into the generation prompt, helping the AI choose the right technique when generating tests for each criterion.

**Why this priority**: Acceptance criteria are an indirect path to better generation. Stories 1–4 already deliver direct generation/update/critic improvements; this story makes the criteria-to-test pipeline more precise but is not a prerequisite for the others.

**Independent Test**: Run `spectra ai analyze --extract-criteria` on a document containing "Username must be 3-20 characters". Verify the resulting criterion has `technique_hint: BVA`. Then run generation against that criterion and verify the produced test uses boundary values.

**Acceptance Scenarios**:

1. **Given** a criterion describing a numeric range, **When** extraction runs, **Then** the criterion has `technique_hint: BVA`.
2. **Given** a criterion describing multiple conditions and outcomes, **When** extraction runs, **Then** the criterion has `technique_hint: DT`.
3. **Given** an existing criteria file without `technique_hint`, **When** it is read by any tool, **Then** the file parses successfully with `technique_hint` treated as null.

---

### User Story 6 - Migration of Existing Prompt Templates (Priority: P2)

A user with an existing SPECTRA project has customized (or simply has the previous default of) `.spectra/prompts/behavior-analysis.md`. When a new SPECTRA version ships with the ISTQB-enhanced templates, the user's existing file is NOT silently overwritten. The user can opt in by running `spectra prompts reset behavior-analysis` (or `--all`) to receive the new template, or `spectra update-skills` to be offered an update for unmodified files.

**Why this priority**: Without a migration story, existing users get no benefit from the new templates. This is required for adoption parity between new and existing projects.

**Independent Test**: In a project with a customized `behavior-analysis.md`, install the new version. Verify the file is unchanged. Run `spectra prompts reset behavior-analysis`. Verify the file now contains the ISTQB technique sections.

**Acceptance Scenarios**:

1. **Given** an existing project with a user-edited `behavior-analysis.md`, **When** SPECTRA is upgraded, **Then** the file is preserved unchanged.
2. **Given** an existing project with an unmodified built-in `behavior-analysis.md`, **When** the user runs `spectra update-skills`, **Then** they are offered an update to the new template.
3. **Given** any project, **When** the user runs `spectra prompts reset behavior-analysis`, **Then** the file is restored to the new ISTQB-enhanced default.
4. **Given** a freshly initialized project (`spectra init`), **When** prompts are first created, **Then** all five templates contain ISTQB technique guidance from the start.

---

### Edge Cases

- **Documentation has no ranges or rules**: Behavior analysis still produces use case (UC) and error guessing (EG) behaviors; technique breakdown shows zero counts for BVA/DT/ST without failing.
- **Behavior set with no technique tags** (legacy AI response): Parsing succeeds with empty technique values; analysis output shows technique breakdown with all zeros and a note that no techniques were identified.
- **Configured categories do not include "happy_path"**: The 40% distribution cap applies to whichever single category dominates, not a hardcoded category name.
- **Range with min equal to max** (e.g., "exactly 5 characters"): BVA produces three behaviors (4, 5, 6), not six.
- **Custom user-edited prompt template that lacks the technique sections**: Analysis still runs, but no technique tags appear in output; the user receives a hint pointing to `spectra prompts reset` if technique breakdown comes back empty.
- **Acceptance criteria file that pre-dates the `technique_hint` field**: Reads as null; never throws.
- **Generation prompt receives a behavior with an unknown technique value** (e.g., a hallucinated tag): Falls back to generic generation rules for that behavior; does not crash.
- **Critic asked to verify a test with no technique tag**: Falls back to standard grounding verification; does not require a technique to exist.

## Requirements *(mandatory)*

### Functional Requirements

#### Behavior Analysis

- **FR-001**: Behavior analysis MUST instruct the AI to apply six ISTQB black-box test design techniques: Equivalence Partitioning (EP), Boundary Value Analysis (BVA), Decision Table (DT), State Transition (ST), Error Guessing (EG), and Use Case (UC).
- **FR-002**: Behavior analysis MUST require that every numeric range, text length limit, date range, or collection size produces at least four BVA behaviors (min, max, min−1, max+1).
- **FR-003**: Behavior analysis MUST require that every documented multi-condition business rule produces at least one Decision Table behavior set covering the unique condition combinations.
- **FR-004**: Behavior analysis MUST require that every documented workflow or state machine produces at least one invalid state transition behavior in addition to valid transitions.
- **FR-005**: Behavior analysis MUST instruct the AI not to allocate more than 40% of total behaviors to any single configured category.
- **FR-006**: Each identified behavior MUST carry a `technique` field whose value is one of: EP, BVA, DT, ST, EG, UC, or empty (for backward compatibility with older AI responses).
- **FR-007**: The legacy hardcoded fallback prompt (used when the template loader is unavailable) MUST contain a condensed version of the same technique instructions and request the same `technique` field in the JSON output.

#### Test Generation

- **FR-008**: Test generation MUST instruct the AI to use exact boundary values in test steps for behaviors tagged with technique `BVA`, not generic descriptions.
- **FR-009**: Test generation MUST instruct the AI to name the equivalence class explicitly in test steps for behaviors tagged with technique `EP`.
- **FR-010**: Test generation MUST instruct the AI to state all condition values explicitly in preconditions for behaviors tagged with technique `DT`.
- **FR-011**: Test generation MUST instruct the AI to state starting state, action, and resulting state for behaviors tagged with technique `ST`.
- **FR-012**: Test generation MUST instruct the AI to describe specific concrete error scenarios for behaviors tagged with technique `EG`.

#### Test Update

- **FR-013**: Test update analysis MUST flag a test as OUTDATED when the source documentation introduces a new numeric range that the test does not cover with BVA values.
- **FR-014**: Test update analysis MUST flag a test as OUTDATED when the source documentation introduces new business rule conditions not covered by existing decision table tests.
- **FR-015**: Test update analysis MUST flag a test as OUTDATED when the source documentation introduces new workflow states not covered by existing state transition tests.
- **FR-016**: Test update analysis MUST flag a test as OUTDATED when a documented boundary value changes (e.g., max increases) and the test still uses the prior value or a generic mid-range value.

#### Critic Verification

- **FR-017**: Critic verification MUST compare boundary values asserted by BVA-tagged tests against the boundaries documented in the source, returning a PARTIAL verdict with a specific unverified claim when they do not match.
- **FR-018**: Critic verification MUST verify that the equivalence class asserted by EP-tagged tests is documented as such in the source, returning PARTIAL when the class is not documented.
- **FR-019**: Critic verification MUST verify that state transition paths asserted by ST-tagged tests exist in the documented workflow, returning PARTIAL otherwise.
- **FR-020**: Critic verification MUST flag DT-tagged tests as HALLUCINATED when they reference conditions not mentioned in source documentation.

#### Acceptance Criteria Extraction

- **FR-021**: Criteria extraction MUST optionally tag each extracted criterion with a `technique_hint` value (BVA, EP, DT, ST, or null) based on whether the criterion text describes a numeric range, valid/invalid input categories, conditional logic, or a state transition.
- **FR-022**: The acceptance criterion model MUST treat `technique_hint` as optional; missing values MUST parse as null without error.
- **FR-023**: The generation prompt MUST receive `technique_hint` values via the acceptance criteria placeholder so that the AI can apply the corresponding technique when covering each criterion.

#### Output and Reporting

- **FR-024**: The structured analysis result MUST include a `technique_breakdown` map alongside the existing `breakdown` (category) map, with one count per technique used.
- **FR-025**: The progress page (HTML) MUST render a "Technique Breakdown" section beneath the existing "Category Breakdown" section.
- **FR-026**: The terminal analysis output MUST render a technique breakdown listing alongside the category breakdown.

#### Migration and Defaults

- **FR-027**: `spectra init` MUST create all five prompt templates containing the ISTQB technique guidance by default.
- **FR-028**: SPECTRA MUST NOT silently overwrite an existing `.spectra/prompts/*.md` file when upgrading.
- **FR-029**: `spectra prompts reset <name>` and `spectra prompts reset --all` MUST restore the affected file(s) to the new ISTQB-enhanced defaults.
- **FR-030**: `spectra update-skills` MUST detect that the built-in template content has changed and offer to update unmodified user copies.

#### Custom Categories Compatibility

- **FR-031**: The 40% distribution cap MUST reference the configured set of categories rather than hardcoding any category name.
- **FR-032**: ISTQB technique tagging MUST function alongside any custom categories configured under `analysis.categories`; categories and techniques are independent dimensions.

### Key Entities

- **Identified Behavior**: A discrete testable scenario produced by behavior analysis. Attributes: category (one of the configured categories), title (short description), source (originating document), technique (one of EP/BVA/DT/ST/EG/UC or empty).
- **Acceptance Criterion**: An extracted requirement statement. Existing attributes plus an optional technique hint indicating which test design technique best applies.
- **Technique Breakdown**: A count of identified behaviors grouped by technique, exposed in structured output, terminal output, and the progress page alongside the existing category breakdown.
- **Prompt Template**: A markdown file under `.spectra/prompts/` controlling AI behavior for one of: behavior analysis, test generation, test update, critic verification, criteria extraction. Each template has a built-in default and may be customized per project.

## Assumptions

- The existing `analysis.categories` config (introduced in spec 030/034) is the source of truth for category definitions; this feature does not introduce a parallel "techniques" config and treats the six ISTQB techniques as a fixed enumeration.
- The `technique` field on identified behaviors is informational only — it does not change the on-disk test file schema beyond what already exists.
- Backward compatibility for older AI responses (no `technique` field) is required; new analyses are expected to populate it but old data must still parse.
- The Copilot SDK runtime supports the existing prompt template placeholders (`{{#each}}`, `{{#if}}`, `{{placeholder}}`); no template engine changes are required.
- "40%" in the distribution cap is treated as guidance to the AI inside the prompt, not a hard post-processing filter; SPECTRA does not reject or rewrite responses that violate it.
- The Windows Calculator distribution figures cited in the source spec (e.g., "~141 behaviors after, ~40 happy path") are illustrative targets, not contractual numbers; the success criteria use them only as reference shapes.
- ISTQB technique tags are reported with their short codes (EP, BVA, DT, ST, EG, UC) in structured output; human-readable labels are used in terminal and HTML rendering.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For documentation containing at least one numeric range, technique-driven analysis produces at least four BVA behaviors per range, where the previous prompt produced zero.
- **SC-002**: For a representative sample of analyzed documents (e.g., the Windows Calculator standard mode), no single category accounts for more than 40% of identified behaviors.
- **SC-003**: For the same representative sample, the proportion of behaviors classified as boundary, negative, edge case, or error handling combined exceeds 50%, where it was below 30% before.
- **SC-004**: 100% of identified behaviors carry a non-empty technique tag (EP, BVA, DT, ST, EG, or UC) when produced by the new template; legacy AI responses without the field still parse without error.
- **SC-005**: For BVA-tagged behaviors, generated test steps contain the exact documented boundary numeric values (verifiable by string presence in step text) at least 90% of the time.
- **SC-006**: After a documented boundary value changes, `spectra ai update` flags affected tests as OUTDATED with a boundary-specific reason in 100% of seeded test cases used for verification.
- **SC-007**: The structured analysis output contains a `technique_breakdown` section in 100% of analysis runs against documents with at least one identified behavior.
- **SC-008**: Existing user-customized prompt template files are preserved unchanged across upgrades in 100% of cases (no silent overwrite).
- **SC-009**: A user can move an existing project to the new templates with a single command (`spectra prompts reset --all`) without manually editing any file.
- **SC-010**: Test suites covering the five updated templates plus the new model fields pass; the project gains at least 15 net new tests covering technique behavior without regressing existing tests.
