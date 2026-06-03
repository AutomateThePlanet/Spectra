# Phase 0 Research: From-Description Criteria Injection

**Branch**: `050-from-desc-criteria-injection`
**Date**: 2026-06-02
**Spec**: [spec.md](./spec.md)

## Status

No `NEEDS CLARIFICATION` markers in the Technical Context. This research file consolidates the decisions implied by the spec so they are not re-litigated during planning, tasks, or review.

## Decisions

### D1 — Forward `criteriaContext` at the SDK call site (the fix)

**Decision**: In `UserDescribedGenerator.GenerateAsync`, change the `agent.GenerateTestsAsync(... criteriaContext: null, ...)` call to pass the loaded `criteriaContext` parameter through.

**Rationale**: The MANDATORY criteria-mapping instruction inside `GenerationAgent` (the shared agent code path used by both the batch flow and the from-description flow) is gated on a non-empty `criteriaContext` argument to `GenerateTestsAsync`. The from-description call site has been discarding the value despite loading it correctly upstream, so the model has been receiving criteria as inert prose instead of as an explicit mapping instruction.

**Alternatives considered**:
- **Author a parallel MANDATORY block inside `UserDescribedGenerator.BuildPrompt`** — rejected. Two independent copies of the same instruction would drift; the batch flow already owns the canonical wording in `GenerationAgent`.
- **Rename or split the `criteriaContext` parameter on `IAgentRuntime`** — rejected. The parameter is already correctly named and documented; the defect is at the caller, not the interface.

### D2 — Remove the loose `## Related Acceptance Criteria` block from `BuildPrompt`

**Decision**: Delete the body section that today appears in `BuildPrompt` when `criteriaContext` is non-empty. After D1, the criteria content reaches the model via the MANDATORY block inside `GenerationAgent`; keeping the loose copy would duplicate the content with conflicting framing ("if maps, include" vs. "you MUST map").

**Rationale**: One canonical presentation is stronger than two with different verbs. Prompt-quality research consistently favors a single, unambiguous instruction over redundant, softer copies — especially when one is presented as optional and the other as mandatory.

**Alternatives considered**:
- **Keep both** — rejected. The loose block weakens the MANDATORY instruction by implying the mapping is optional ("if the user's test maps to any of these criteria…").
- **Keep only the loose block and drop the MANDATORY one for from-description** — rejected. The MANDATORY block is the cross-flow standard; from-description must align with batch, not diverge from it.

### D3 — `grounding.verdict` remains `manual`

**Decision**: Leave `VerificationVerdict.Manual` hard-coded in `UserDescribedGenerator.GenerateAsync` (line ~149).

**Rationale**: `verdict` means "an independent critic (different model family) verified the test is grounded." The from-description flow runs no critic; claiming `grounded` or `partial` would assert verification that never happened — exactly the failure the dual-model design exists to prevent. Populating `criteria:` is not verification.

**Documented consequence**: From-description tests with populated `criteria:` participate in **acceptance-criteria coverage** but remain excluded from **grounded** statistics. This is correct; it must be stated in the docs to avoid reading as an inconsistency.

**Alternatives considered**:
- **Introduce a new enum value `criteria-mapped`** — rejected. Adds legend, report, doc, and SKILL-parser surface for negligible user-visible gain.
- **Run the critic in the from-description flow** — rejected. Erases the only advantage of from-description (fast, deterministic, single-test, no analyze phase).
- **Set verdict to `partial` when criteria are populated** — rejected. Verdict is a verification claim, not a population indicator.

### D4 — Testing seam: factory delegate parameter on `GenerateAsync`

**Decision**: Add an optional `Func<...,Task<IAgentRuntime?>>` factory parameter to `UserDescribedGenerator.GenerateAsync` (default: the existing `AgentFactory.CreateAgentAsync`). xUnit tests inject a fake `IAgentRuntime` that captures the `prompt` and `criteriaContext` arguments and returns a stub `GenerationResult`.

**Rationale**: The spec's test plan requires assertions on (a) what the agent received as `criteriaContext`, (b) what the prompt contained, (c) what the returned test's frontmatter looked like. These cannot be made through `BuildPrompt` alone (it doesn't observe `GenerateTestsAsync`), so a small seam is unavoidable. A single-delegate parameter is the minimum surface required and keeps production call sites untouched (the parameter defaults to today's behavior).

**Alternatives considered**:
- **Constructor injection via DI container** — rejected. `UserDescribedGenerator` is currently a stateless type instantiated ad-hoc by `GenerateHandler`; introducing DI is over-engineering for one method.
- **Internal partial method or `internal` overload** — rejected. Less explicit than a defaulted parameter; harder to discover from the call site.
- **`InternalsVisibleTo` + private mock of `AgentFactory`** — rejected. `AgentFactory` is `static`; the cleaner answer is to make the dependency parameterizable on the consumer rather than reach around a static.
- **Test only via end-to-end CLI runs** — rejected. Slow, requires AI access, and provides poor diagnostics for the prompt-content assertions.

### D5 — One test-file change (no new files)

**Decision**: Add the six new tests from the spec § Test Plan to the existing `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs`. Adapt the existing `BuildPrompt_WithCriteriaContext_IncludesAcceptanceCriteriaHeader` and `BuildPrompt_WithBothContexts_IncludesBoth` tests because they assert on the loose body block that this spec removes.

**Rationale**: The new tests are conceptually a continuation of the existing suite (same SUT, same fixture conventions). Keeping them co-located preserves discoverability and avoids file proliferation.

**Alternatives considered**:
- **Create a new `UserDescribedGeneratorCriteriaInjectionTests.cs` file** — rejected. Splitting tests for one method across multiple files reduces locality without justification.

### D6 — Documentation scope: four files, markdown-only

**Decision**: Update `docs/skills-integration.md`, `docs/coverage.md`, `docs/PROJECT-KNOWLEDGE.md`, and the `spectra-generate` SKILL content per spec § Documentation Update Checklist. No restructuring; targeted paragraphs only.

**Rationale**: Each touch corresponds to a distinct reader audience (skill integrators, coverage-report readers, project historians, SKILL operators). All four edits land alongside the code change so the docs and behavior stay aligned.

**Alternatives considered**:
- **Single consolidated doc page for from-description semantics** — rejected. Out of scope; the existing surface area is already where readers look, and creating a new page risks orphaning the information.

## Non-decisions (explicitly out of scope)

These were considered and confirmed out of scope by the spec's Decisions and Out of Scope sections; they are not re-opened here:

- Changing the verification verdict enum.
- Invoking a critic in the from-description flow.
- Modifying `GenerationAgent` (line 527 already activates correctly when `criteriaContext` is non-empty).
- Changing `TestFileWriter`, the `criteria:` frontmatter shape, or any persistence path (Spec 049 already routes from-description through `TestPersistenceService`).
- Touching the batch generation flow.
- Migrating existing test files.

## Unknowns

None.
