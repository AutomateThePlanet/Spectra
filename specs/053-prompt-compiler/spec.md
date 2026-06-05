# Feature Specification: Prompt-compiler + generation handoff inversion

**Feature Branch**: `053-prompt-compiler`
**Created**: 2026-06-05
**Status**: Draft
**Conceptual ID**: Spec 052 (migration series 052–057, foundation — number 053 assigned because 052 was already in use)
**Grounds**: `docs/investigation/01-generation-seam.md`, `docs/investigation/03-deterministic-core.md`
**Input**: Invert the generation handoff — the CLI compiles a grounded prompt, the interactive Claude Code agent performs the generative turn, and the CLI re-enters at a fail-loud validation boundary with a new choreography-driven retry.

## Overview

Today the CLI owns the entire generation act: it assembles a grounded prompt, calls a model itself, then parses, validates, and persists the result. This feature **inverts the handoff**. The CLI stops calling any model during generation. Instead it becomes a deterministic **prompt-compiler** that emits a complete grounded prompt; the developer's interactive Claude Code session performs the generative turn in its own context (on the user's existing subscription); and the CLI re-enters only at the **parse/validate boundary**, now hardened to fail loud and backed by a **bounded, choreography-driven retry** that generation has never had.

This is the foundation of the migration series. It defines the prompt-compile → generate → validate contract that later specs (criteria re-homing, critic, orchestration skill) reuse.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deterministic prompt-compiler (Priority: P1)

A developer (or an automating skill) needs a grounded generation prompt assembled from a document, its extracted criteria, the active generation profile, and Testimize inputs — without any model call and without any token spend.

**Why this priority**: This is the foundation. Nothing else in the inverted handoff works until the prompt can be compiled deterministically as a standalone, model-free artifact. It is the single most reusable output of this feature and the entry point every downstream spec depends on.

**Independent Test**: Run the prompt-compiler CLI command against a fixture document with valid criteria, profile, and config. Confirm it prints a complete grounded prompt to its output channel, writes nothing to disk, and produces byte-identical output across repeated runs with the same inputs — with no network/model activity.

**Acceptance Scenarios**:

1. **Given** a valid document, criteria context, generation profile, and config, **When** the prompt-compiler runs, **Then** it emits a complete grounded prompt containing the document, criteria, profile, and Testimize sections, and writes nothing to disk.
2. **Given** the same set of valid inputs run twice, **When** the prompt-compiler runs each time, **Then** both runs produce identical prompt output (determinism).
3. **Given** any valid inputs, **When** the prompt-compiler runs, **Then** it performs no model call and incurs no token spend.

---

### User Story 2 - Refuse-to-emit on missing input (Priority: P1)

A developer runs the prompt-compiler but one of its required inputs is absent or null (for example, the criteria context — the class introduced in Spec 045 — never resolved). The compiler must refuse to produce a prompt and say exactly which input was missing, at compile time.

**Why this priority**: A degraded prompt with a silently-missing section produces silently-degraded tests. Catching the missing input at the moment of compilation — rather than three layers downstream after a model has already been driven — is the core reliability promise of making the compiler a standalone, validated artifact.

**Independent Test**: Run the prompt-compiler with one required input deliberately omitted/null. Confirm it does not emit a prompt, returns a non-success result that names the missing input, and writes nothing.

**Acceptance Scenarios**:

1. **Given** a required input is null or absent (e.g. criteria context), **When** the prompt-compiler runs, **Then** it refuses to emit any prompt and reports which input failed.
2. **Given** a required input is missing, **When** the compiler refuses, **Then** it does NOT emit a partial or degraded prompt with the missing section omitted.
3. **Given** the refusal, **When** the caller inspects the result, **Then** the failure is reported in a form a skill can act on (identifies the specific missing input).

---

### User Story 3 - Fail-loud validation boundary (Priority: P1)

The interactive agent has generated test content and hands it back. The developer (via the skill) passes that content to a CLI boundary entry point. The boundary parses and validates the content before anything touches disk: valid content is persisted; malformed, incomplete, or schema-violating content fails loud with a specific error and persists nothing.

**Why this priority**: With the model call removed from C#, boundary validation is now the *only* reliability net between agent output and the test corpus. It must never silently repair, substitute defaults, or salvage truncated output — those behaviors would let bad tests reach disk undetected.

**Independent Test**: Feed the boundary entry point three classes of agent content — well-formed, malformed/truncated, and schema-violating — and confirm: the well-formed case parses, validates, and persists through the existing persistence path; both bad cases return a specific machine-readable error and leave the corpus and index unchanged.

**Acceptance Scenarios**:

1. **Given** well-formed agent-generated content, **When** it is ingested at the boundary, **Then** it parses, validates, and persists through the unchanged persistence + index path, producing the same on-disk frontmatter as today.
2. **Given** malformed or incomplete content, **When** it is ingested at the boundary, **Then** validation fails with a specific, machine-readable error and nothing is persisted.
3. **Given** schema-violating content, **When** it is ingested, **Then** validation fails loud and the boundary does not silently repair it, substitute defaults, or salvage a truncated array.
4. **Given** any boundary failure, **When** it returns, **Then** the test corpus and indexes are byte-for-byte unchanged from before the call.

---

### User Story 4 - Bounded choreography-driven retry (Priority: P2)

When the boundary rejects content, the skill receives the specific error, instructs the agent to regenerate addressing that exact error, and re-submits — repeating up to a configured maximum, after which it stops and reports failure. The developer never has to re-drive the flow manually.

**Why this priority**: The retry is net-new value — generation has never had one — but it depends on User Stories 1–3 existing first (a compilable prompt, a re-submittable boundary, and a specific error to act on). It is the choreography that turns a fail-loud boundary into a self-correcting loop. It lives in skill/agent choreography, not in C#.

**Independent Test**: Simulate the choreography: submit invalid content, confirm a specific error is surfaced, confirm the next attempt is driven by that error, and confirm the loop stops and reports failure once the configured attempt limit is reached.

**Acceptance Scenarios**:

1. **Given** a boundary rejection, **When** the skill handles it, **Then** the specific error is surfaced back to the agent as the basis for regeneration.
2. **Given** a surfaced error, **When** the agent regenerates and re-submits, **Then** the boundary re-validates the new content as an independent attempt.
3. **Given** repeated rejections, **When** the configured maximum attempts is reached, **Then** the choreography stops and reports failure rather than looping unbounded.
4. **Given** a successful re-validation before the limit, **When** content passes, **Then** it persists normally and the loop ends.

---

### Edge Cases

- **Empty agent content**: the boundary treats empty/whitespace-only content as a fail-loud validation error, not as "zero tests successfully generated".
- **Truncated array**: previously-salvaged truncation now fails loud and triggers a retry attempt rather than persisting a partial set. (Reconciles the prior truncation-salvage behavior.)
- **Partially valid batch**: if some test objects in a batch validate and others do not, the boundary fails loud for the batch and persists nothing — no partial persistence. (Assumption — see Assumptions.)
- **Retry limit reached on first attempt**: if retry is configured to a single attempt, a first-attempt failure stops immediately and reports failure.
- **Determinism vs. environment**: the compiled prompt must not embed nondeterministic values (timestamps, random ordering) that would break byte-identical reproduction.
- **Missing vs. empty input**: a required input that is present-but-empty is treated the same as absent for refuse-to-emit purposes. (Assumption — see Assumptions.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The generation path MUST NOT call any model. The existing generation model call and its enabling per-generation session/provider construction MUST be removed from the generation flow.
- **FR-002**: Prompt compilation MUST be a standalone artifact, extracted out of the agent class it is currently coupled to by location. It MUST be invocable as a CLI command that emits the compiled prompt and performs no I/O beyond reading its declared inputs.
- **FR-003**: The prompt-compiler MUST be deterministic and unit-testable without any model call or token spend — identical declared inputs MUST produce identical prompt output.
- **FR-004**: The prompt-compiler MUST refuse to emit when a required input is absent or null, MUST report which input failed, and MUST NOT emit a degraded prompt with a missing section.
- **FR-005**: The CLI MUST provide a boundary entry point that ingests agent-generated content and validates it before any persistence, reusing the existing parse pipeline.
- **FR-006**: Boundary validation MUST fail loud: on malformed, incomplete, or schema-violating content it MUST return a specific, machine-readable error and persist nothing. It MUST NOT silently repair content or substitute defaults. Truncation MUST trigger a retry signal, not silent salvage.
- **FR-007**: A bounded retry MUST exist as skill/agent choreography (not in the C# CLI): an invalid boundary result MUST return the specific error to the skill, which instructs the agent to regenerate addressing it, up to a configured maximum, after which it stops and reports failure.
- **FR-008**: Valid content MUST persist through the unchanged persistence write+index path; the on-disk frontmatter MUST be produced deterministically at persist time exactly as today.
- **FR-009**: The persistence, index, and parse machinery, the prompt template content and grounding logic (relocated, not rewritten), and the grounded-persist path MUST be reused verbatim and MUST NOT be modified in behavior.
- **FR-010**: The existing `Spectra.Core` test corpus and all persistence-service tests MUST remain unchanged and green; any failure among them during this work MUST be treated as a regression to investigate, not a test to update.

### Key Entities *(include if feature involves data)*

- **Compiled prompt**: the deterministic, fully-grounded text artifact emitted by the prompt-compiler. Composed of document content, criteria context, generation profile, and Testimize inputs. Contains no nondeterministic values; written nowhere on disk by the compiler.
- **Compiler inputs**: the declared set of required inputs (document, criteria context, generation profile, config). Each is individually checkable for presence; absence of any required input causes refuse-to-emit.
- **Boundary result**: the outcome of ingesting agent content — either a success (parsed, validated, persisted) or a fail-loud error carrying a specific, machine-readable reason a skill can act on.
- **Retry choreography state**: the skill-held attempt count and the most recent specific error, bounded by a configured maximum attempts. Lives outside the CLI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero model calls occur in the generation path — verifiable by the absence of any session/provider invocation in the generation flow and by the prompt-compiler running fully offline.
- **SC-002**: The prompt-compiler is unit-tested without any token spend, and repeated runs on identical inputs produce identical output in 100% of cases.
- **SC-003**: For every missing-required-input case, the compiler refuses to emit and names the failing input — 0% of refusals emit a partial prompt.
- **SC-004**: For every malformed, incomplete, or schema-violating boundary input, validation fails loud and the test corpus + indexes are unchanged — 0% of invalid inputs result in any persistence.
- **SC-005**: The choreography retry regenerates against a specific error and stops exactly at the configured attempt limit — it never loops unbounded.
- **SC-006**: The entire `Spectra.Core` and persistence-service test corpus remains unchanged and green after the feature lands.

## Assumptions

- **Required inputs**: the required inputs for refuse-to-emit are the document, criteria context, generation profile, and config — the same inputs the current prompt assembly consumes. Optional sections (e.g. user focus) remain optional and do not trigger refusal.
- **Present-but-empty**: a required input that is present but empty is treated the same as absent for refuse-to-emit purposes.
- **Batch atomicity**: when a batch of generated tests is ingested, validation is all-or-nothing for that batch — a single invalid test fails the whole batch and persists none of it. (No partial persistence.)
- **Retry maximum**: the retry attempt maximum is a configurable value owned by the skill choreography, with a sensible small default; the exact default is an implementation detail to be settled in planning.
- **Boundary input transport**: agent-generated content is handed to the boundary entry point as content the CLI ingests (e.g. via a file or stdin); the precise transport is an implementation detail for planning and does not change the validation contract.
- **`response_format`/structured-output**: none exists today, so its "retirement" is a no-op and out of scope.

## Out of Scope

- Criteria extraction re-homing (Spec 053 in the series).
- Critic / dual-model verification (Spec 054).
- Authoring the orchestration / generation skill that *invokes* this flow (Spec 055) — this feature delivers the CLI surface the skill will call, not the skill itself.
- Provider chain retirement (Spec 057).
- Any change to `BatchWriteTestsTool` (separate AI-discretion path) or to the update flow's apply-changes path.
- `response_format` retirement (no-op — none exists).

## Dependencies

None. This is the foundation spec. It defines the prompt-compile → generate → validate contract that the criteria, critic, and orchestration specs reuse.

## Documentation Impact

- **Stale (update)**: the generation workflow page; ARCHITECTURE-v2 references to the generation handoff.
- **Factually wrong (must fix)**: none yet — provider-facing documentation is handled in Spec 057.
