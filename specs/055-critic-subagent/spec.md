# Feature Specification: Critic as a `context: fork` Subagent (+ Gating Semantics)

**Feature Branch**: `055-critic-subagent`
**Created**: 2026-06-05
**Status**: Draft
**Input**: User description: "Spec 054 — Critic as a `context: fork` subagent (+ gating semantics). Move the critic from an in-process C# model call to a fresh-context subagent invoked as a mandatory step, and make its damage paths fail loud while its verdict stays advisory."

> **Series note**: This is migration spec **3 of 6** (the 052–057 series). The conceptual numbering in the source material calls this "Spec 054"; in the repository it lives in directory `055-` because of the established one-step offset (conceptual spec *N* → directory *N+1*). It is pattern-aligned with the two preceding specs in the series (the prompt-compiler / generation handoff and the criteria-extraction re-homing), which both shipped **additively** — the model-free CLI surface and skill were delivered, while the literal removal of the in-process model call was deferred to the wiring spec. This spec follows that same precedent (see Assumptions). The critic subagent skill delivered here MUST exist before the next spec can wire its mandatory invocation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Critic runs as a fresh-context subagent, not an in-process model call (Priority: P1)

During generation, each candidate test is verified by a critic. After this change, the critic runs as a `context: fork` subagent skill — a fresh, isolated context that receives only the test artifact and its selected source documents, never the generator's prompt, reasoning, tool calls, or token usage. The deterministic verdict-ingest boundary (parse the critic's JSON verdict, write it back onto the test) remains in the CLI. The critic's model is selected by configuration (`ai.critic.model`, target value Sonnet 4.6).

**Why this priority**: This is the core re-homing the spec exists to deliver. It moves the model turn out of in-process C# and into a subagent, formalizing an isolation property the critic already has by construction. Without the subagent skill, the later wiring spec has nothing to invoke. It is the foundation every other story builds on.

**Independent Test**: Confirm the net-new critic subagent skill exists and is declared `context: fork`, and that its instruction set restricts the critic's input to the test artifact plus selected source documents (no generator state). Confirm the verdict-ingest boundary classifies a representative critic JSON response into a verdict and writes it back, with no model/provider session opened by that boundary.

**Acceptance Scenarios**:

1. **Given** a generated test, **When** verification runs via the new path, **Then** the critic operates in a fresh, isolated context seeing only the artifact and selected source documents.
2. **Given** a critic JSON response, **When** the CLI ingests it, **Then** the verdict-ingest boundary parses and applies the verdict deterministically without itself opening a model/provider session.
3. **Given** critic model configuration, **When** the critic runs, **Then** its model is taken from `ai.critic.model` (target: Sonnet 4.6), not from a hard-coded default.

---

### User Story 2 - Verdict gating stays advisory and unchanged (Priority: P1)

A reviewer relies on the critic to drop clearly hallucinated tests while letting grounded, partial, unverified, and manually-marked tests pass. After this change, that gating contract is byte-for-byte unchanged: a `hallucinated` verdict drops the test from the persisted set; `grounded` / `partial` / `unverified` / `manual` pass through; and a pre-existing `Manual` verdict is preserved and skipped (never re-verified, never overwritten).

**Why this priority**: The verdict's *advisory-gating* role is the user-visible contract of the whole critic feature. The re-homing must not change which tests survive verification. It is P1 because any drift here is a silent correctness regression in the generated test corpus, and because a protected regression-net of gating tests pins exactly this behavior.

**Independent Test**: Drive the verdict-application path with each verdict value and assert the drop/pass decision is unchanged from today (hallucinated drops; the rest pass). Drive it with a pre-existing `Manual` verdict and assert it is preserved and skipped.

**Acceptance Scenarios**:

1. **Given** a critic verdict of `hallucinated`, **When** the verdict is applied, **Then** the test is dropped from the persisted set.
2. **Given** a critic verdict of `grounded` or `partial`, **When** the verdict is applied, **Then** the test passes through unchanged.
3. **Given** a verdict of `unverified`, **When** the verdict is applied, **Then** the test passes through (failure/abstention never blocks).
4. **Given** a test that already carries a `Manual` verdict, **When** verification runs, **Then** that verdict is preserved and the test is skipped (not re-verified, not overwritten).

---

### User Story 3 - Damage fails loud; failure and parse-miss are recorded distinctly (Priority: P1)

A reviewer must be able to trust that "the test passed verification" means the critic actually rendered a usable verdict. After this change, a critic response that is missing its `verdict` or `score`, or is otherwise unparseable, MUST surface a specific error rather than silently defaulting to a soft `Partial` / `0.5` pass. Separately, a critic call that fails or times out MUST remain non-blocking (the test is marked `Unverified` and generation continues) but MUST be recorded **distinctly** from a parse-miss, so a genuinely failed critic and a malformed critic response are never conflated.

**Why this priority**: This is the "fail loud on damage, stay advisory on verdict" split that is the point of the gating-semantics half of the spec. Today a malformed response is smoothed into a soft pass, which lets a non-result masquerade as a verified test. Making damage loud — while keeping legitimate critic *failure* non-blocking but visible — is what makes the verification trustworthy. P1 because it changes a silent-correctness behavior into an observable one.

**Independent Test**: Feed the verdict-ingest boundary a response missing `verdict`, one missing `score`, and an unparseable response; assert each surfaces a specific error and that none produces a `Partial` / `0.5` default. Separately, simulate a critic call failure and a timeout; assert the test becomes `Unverified`, generation is not blocked, and the recorded outcome is distinguishable from a parse-miss.

**Acceptance Scenarios**:

1. **Given** a critic response missing `verdict`, **When** the CLI ingests it, **Then** it fails loud with a specific error and does NOT default to `Partial` / `0.5`.
2. **Given** a critic response missing `score` or otherwise unparseable, **When** the CLI ingests it, **Then** it fails loud with a specific error and does NOT default to `Partial` / `0.5`.
3. **Given** a critic call that throws or times out, **When** the result is recorded, **Then** the test is marked `Unverified`, generation continues, and the failure is recorded distinctly from a parse-miss.
4. **Given** a parse-miss and a critic-call failure in the same run, **When** their outcomes are reported, **Then** the two are not conflated into a single indistinguishable state.

---

### User Story 4 - `ai.critic.model` is the single model selector; dead and duplicated code is gone (Priority: P2)

A maintainer configuring the critic model should have exactly one place to do it. After this change, `ai.critic.model` is the single source of truth for the critic's model: the provider→default-model fallback switches that previously duplicated this decision in two places are removed as a source of truth, the unreferenced second critic factory is deleted, and the stale cross-architecture guidance comments are corrected to the same-family direction.

**Why this priority**: Collapsing two divergent default-model switches into one config selector, and removing a dead factory, eliminates drift risk and confusion. It is P2 because it is a maintainability and correctness-of-configuration improvement that builds on the model-selection behavior, rather than a user-facing capability on its own — but it is still in scope because the same code is being rewritten.

**Independent Test**: Set `ai.critic.model` and assert it is the value used. Confirm no provider→default-model fallback switch remains reachable as an alternative source of truth, that the dead second factory no longer exists, and that the cross-architecture comments have been updated to the same-family direction.

**Acceptance Scenarios**:

1. **Given** `ai.critic.model` is set, **When** the critic model is resolved, **Then** that configured value is used.
2. **Given** the resolved model configuration, **When** the code is inspected, **Then** no duplicated provider→default-model fallback switch remains as a competing source of truth.
3. **Given** the critic code surface, **When** it is inspected, **Then** the unreferenced second critic factory no longer exists and the stale cross-architecture comments reflect the same-family direction.

---

### Edge Cases

- **Failure vs. parse-miss ambiguity**: A critic exception/timeout (legitimate *failure* → `Unverified`, non-blocking) must be recorded distinctly from a malformed-but-returned response (*damage* → loud error). These two must never collapse into one indistinguishable "soft pass" state.
- **Missing one field but not the other**: A response with a valid `verdict` but missing `score` (or vice versa) is damage and must fail loud — partial structural validity is not a pass.
- **Pre-existing `Manual` verdict**: A test already marked `Manual` is skipped entirely — neither the subagent nor the ingest boundary may overwrite it, regardless of what any new verification would say.
- **Disabled or absent critic**: When the critic is disabled or unavailable, every test is `Unverified` and passes through (non-blocking) — this is failure-shaped, not damage, and must not fail loud.
- **Verdict gating boundary values**: An unknown/unexpected verdict string is damage (fail loud), not a silent coercion to `Partial` — but a well-formed `unverified` is a legitimate pass-through, not damage.
- **Subagent isolation leak**: The critic must never receive generator state; an input that includes generator reasoning would violate the isolation contract the subagent formalizes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The critic MUST stop routing verification through the in-process C# model call; the critic skill provides the model turn instead. (Per the series' additive precedent — see Assumptions — this spec delivers the `context: fork` subagent skill and the deterministic ingest surface it hands back to; the literal removal of the in-process call from the live generation path is wired by the subsequent spec.)
- **FR-002**: The critic MUST run as a net-new `context: fork` subagent skill, receiving only the test artifact and the selected source documents — preserving the existing isolation in which the critic never sees the generator's prompt, reasoning, tool calls, or token usage.
- **FR-003**: The critic MUST be invokable as a **mandatory explicit step inside the generation skill's procedure**, never via auto-invocation. (The actual wiring of that invocation lands in the subsequent spec; this spec delivers the subagent skill that the step invokes, designed for explicit invocation.)
- **FR-004**: The critic model MUST be selectable via `ai.critic.model` (target value: Sonnet 4.6), and `ai.critic.model` MUST be the single source of truth for the critic model. The duplicated provider→default-model fallback switches MUST be removed as a competing source of truth. Stale cross-architecture critic comments MUST be updated to the same-family direction.
- **FR-005**: Verdict gating MUST be unchanged: a `hallucinated` verdict drops the test; `grounded` / `partial` / `unverified` / `manual` pass through; and a pre-existing `Manual` verdict is preserved and skipped.
- **FR-006**: Verdict ingest MUST fail loud on damage: a missing or unparseable `verdict` or `score` MUST surface a specific error and MUST NOT silently default to `Partial` / `0.5`.
- **FR-007**: A critic call failure or timeout MUST remain non-blocking — the test is marked `Unverified` and generation continues — but MUST be recorded distinctly from a parse-miss, so a failed critic and a malformed critic response are never conflated.
- **FR-008**: Dead and duplicated code MUST be removed: the unreferenced second critic factory MUST be deleted, and the duplicated provider→default-model switch MUST be collapsed to the single config selector (FR-004).

### Reused Verbatim *(must not be modified)*

- **The critic prompt builder**: its isolation already matches the v2 target (artifact + ≤5 selected source documents, no generator state). Its content is reused unchanged.
- **The critic response JSON contract *shape***: the `{ verdict, score, findings }` wire format is unchanged. Only the *defaults* on a missing field change (per FR-006: damage fails loud instead of defaulting); the parse *structure* is not redefined.
- **The grounding write-back and `Manual`-preservation logic**: how a successful verdict becomes grounding metadata attached to the test, and how a pre-existing `Manual` verdict is preserved and skipped, are unchanged.
- **`VerificationVerdict` / `GroundingMetadata` in `Spectra.Core`**: the verdict enum and grounding model are unchanged and remain the deterministic boundary types this spec builds on.

### Key Entities

- **Critic subagent skill**: The net-new `context: fork` skill that performs the critic's model turn in a fresh, isolated context, receiving only the test artifact and selected source documents and returning a verdict in the existing JSON shape.
- **Critic verdict**: The deterministic outcome of verification — one of `grounded`, `partial`, `hallucinated`, `unverified`, or a preserved `manual` — that gates whether a test is persisted.
- **Verdict-ingest boundary**: The deterministic CLI step that parses the critic's JSON, classifies it (including failing loud on damage), and writes a successful verdict back onto the test as grounding metadata.
- **Critic model selector**: The single configuration value (`ai.critic.model`) that determines which model the critic subagent uses; no competing default-switch source of truth.
- **Grounding metadata**: The record attached to a verified test capturing its verdict, score, and findings; written only for a successful verification, never for a failure or a `Manual`-preserved test.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The critic runs as a `context: fork` subagent skill that receives only the artifact and selected source documents — in 100% of invocations, zero generator state (prompt, reasoning, tool calls, token usage) reaches the critic.
- **SC-002**: Verdict gating is unchanged: for every verdict value, the drop/pass decision matches today's behavior (hallucinated drops; grounded/partial/unverified/manual pass), and a pre-existing `Manual` verdict is preserved in 100% of cases.
- **SC-003**: Damage fails loud: a missing/unparseable `verdict` or `score` surfaces a specific error in 100% of cases and produces a `Partial` / `0.5` soft-pass default in 0% of cases.
- **SC-004**: Failure and parse-miss are distinct: a critic call failure/timeout yields `Unverified` (non-blocking) and is recorded as a distinct outcome from a parse-miss in 100% of cases — the two are never reported as the same state.
- **SC-005**: `ai.critic.model` is the single model selector: the configured value is honored in 100% of cases, and 0 alternative provider→default-model fallback switches remain reachable as a competing source of truth.
- **SC-006**: Dead/duplicated code is gone: the unreferenced second critic factory and the duplicated default-model switch no longer exist in the codebase (0 occurrences).
- **SC-007**: The protected regression net is unchanged and green: all `Spectra.Core` grounding-model tests and all verdict-gating tests (which pin drop-vs-pass) pass without modification.

## Assumptions

- **Additive surface precedent (scope of FR-001)**: This spec is delivered **additively**, exactly as the two preceding specs in this series were (both shipped tagged "CLI surface"): it ships the net-new `context: fork` critic subagent skill, the fail-loud verdict-ingest boundary, the single-selector config behavior, and the dead/duplicate-code removal — while the literal removal of the in-process critic model call from the live generation batch loop is deferred to the subsequent wiring spec, which makes the subagent the invoked critic. This keeps the existing in-process critic working until its replacement is wired, avoiding a window where generation has no critic. FR-001 and FR-003 are therefore satisfied here by *delivering and enabling* the subagent path, with the swap of the live invocation completed by the next spec.
- **Critic model target value**: The intended `ai.critic.model` value is Sonnet 4.6, but the spec only makes the model a config value; the actual value is set in configuration, not hard-coded. Whether same-family Sonnet outperforms a cross-family critic is validated by a post-migration defect-injection bake-off, which is explicitly out of scope here.
- **Verdict contract shape is stable**: Only the missing-field *defaults* change (fail loud vs. soft default). The `{ verdict, score, findings }` wire shape, the verdict enum, and the grounding write-back are unchanged.
- **Non-blocking failure is intended behavior**: A critic failure/timeout marking a test `Unverified` and not blocking generation is correct and preserved; this spec makes it *distinguishable* from a parse-miss, it does not make failure blocking.
- **No new persisted data model**: The grounding metadata, verdict enum, and criteria/test on-disk formats are unchanged.

## Out of Scope

- The generation skill that *invokes* the critic as a mandatory step (the subsequent spec) — this spec ships the subagent it will call.
- The generation handoff and the criteria-extraction re-homing (the two preceding specs in the series) — this spec is pattern-aligned with them but does not re-implement them.
- Provider/runtime retirement (a later spec in the series).
- The **defect-injection bake-off** validating same-family Sonnet 4.6 against a cross-family critic — that is post-migration validation, not this spec. This spec only makes the model a config value so the bake-off can flip it without a code change.
- Any change to the verdict enum, the grounding metadata model, the critic prompt content, or `Spectra.Core` grounding types.

## Dependencies

- **Subagent mechanics**: The `context: fork` subagent capability is verified available (investigation §1) and is the same mechanism the two preceding series specs rely on.
- **Preceding series specs (handoff + extraction re-homing)**: This spec is pattern-aligned with their prompt-compile / deterministic-boundary / skill-choreography shape and follows their additive-delivery precedent.
- **Subsequent wiring spec**: The critic subagent skill delivered here is a prerequisite for the next spec, which wires its mandatory invocation into the generation skill's procedure.

## Documentation Impact

- **Factually wrong (must fix)**: Cross-architecture critic guidance that describes the critic as deliberately using a different model family from the generator; `ai.critic` configuration documentation describing the provider/fallback model-selection behavior; and the grounding-verification documentation where it states the failure/parse-miss behavior (which now fails loud on damage).
- **Stale (update)**: The grounding-verification workflow narrative must be updated to describe the critic as a `context: fork` subagent step with fail-loud damage handling and distinct failure recording.

## Tests

- **Rewrite (covers deleted/dead behavior)**: Tests asserting the in-process critic model call; tests for the dead second critic factory; provider→default-model fallback tests, now collapsed to the single config selector.
- **Do not touch (regression net)**: All `Spectra.Core` grounding-model tests; all verdict-gating tests that pin which verdicts drop vs. pass (FR-005 keeps this contract). If one of these breaks, it signals a regression to investigate, not a test to update.
- **Net-new**:
  - Fail-loud parse-miss tests: a missing/unparseable `verdict` or `score` surfaces a specific error and produces no `Partial` / `0.5` default (FR-006).
  - Critic-failure-distinct tests: a failure/timeout yields `Unverified`, non-blocking, recorded distinctly from a parse-miss (FR-007).
  - Single-selector tests: `ai.critic.model` is the only model source; no fallback switch remains reachable; the dead factory is gone (FR-004, FR-008).
  - Subagent-isolation test: the critic subagent skill is `context: fork` and its input is restricted to the artifact plus selected source documents, with no generator state (FR-002).
