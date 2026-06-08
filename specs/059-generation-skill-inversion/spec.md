# Feature Specification: Generation-skill inversion + completion

**Feature Branch**: `059-generation-skill-inversion`
**Created**: 2026-06-05
**Status**: Draft
**Conceptual ID**: Spec 059 (migration series — repo spec 7 of 8; completes the partial retirement deferred by 058)
**Grounds**: `docs/investigation/01-generation-seam.md`; Spec 053 (CLI surface); Spec 056 (orchestration)
**Input**: Switch the generation skill off the in-process `ai generate --count` onto the compile-prompt → in-session generate → ingest-tests seam, then remove the in-process generation path, provider chain, and Copilot SDK entirely.

## Overview

Spec 053 delivered the prompt-compiler **CLI surface** (`compile-prompt` / `ingest-tests`) and Spec 056 ported the orchestration skills, but the shipped `spectra-generate` skill was **never switched onto that seam**. Steps 2/5 of the skill still drive bulk generation in-process via `spectra ai generate --analyze-only` and `spectra ai generate --count N`, which still reach the provider chain and the GitHub Copilot SDK. Only the critic was re-homed (055). Spec 058 deliberately narrowed provider retirement to avoid breaking that still-live path.

This spec finishes the migration: it is the real "handoff inversion" that 053's title promised but deferred. First it **rewrites the generation skill** so every generation flow — bulk, from-description, and behavior-analysis — runs through the compile → in-session-generate → ingest seam with the critic subagent as a mandatory step. Only **then**, with nothing left calling the in-process generator, does it **remove** the generator, the provider chain, and the GitHub.Copilot.SDK dependency, and retire `ai.providers`. In-process generation has always been transitional — this spec ends it. After this lands, generation runs on the developer's interactive subscription path, which is the migration's core goal.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Bulk generation runs on the seam (Priority: P1)

A developer asks the interactive skill to generate test cases for an area. The skill compiles a grounded prompt via the CLI, Claude generates the tests in-session (on the user's subscription), the critic subagent verifies each test as a mandatory step, and the CLI ingests/validates/persists the survivors. No in-process model call, no `spectra ai generate --count`, no provider chain anywhere in the flow.

**Why this priority**: This is the convergence point of the whole migration — the single behavioral change that actually moves bulk generation onto the subscription path. Until bulk generation is on the seam, the in-process generator cannot be removed and the migration is not done. It is the MVP: shipping just this slice already delivers the migration's headline outcome.

**Independent Test**: Run the rewritten skill against a fixture suite for a bulk request. Confirm it invokes the prompt-compiler, performs the generative turn in-session, invokes the critic subagent for each produced test, and ingests through the existing boundary — and confirm it never invokes `spectra ai generate --count`.

**Acceptance Scenarios**:

1. **Given** a bulk generation request, **When** the skill runs, **Then** it calls the prompt-compiler, generates in-session, and ingests at the boundary — and it MUST NOT call `spectra ai generate --count`.
2. **Given** a test produced in the bulk flow, **When** it is about to be persisted, **Then** the critic subagent has been invoked for it as a mandatory step and only a passing/kept test reaches disk.
3. **Given** the boundary rejects ingested content (malformed, incomplete, or schema-violating), **When** the skill handles it, **Then** it drives the 053 fail-loud choreography retry against the specific error, bounded by the configured attempt limit, and never persists unverified content.

---

### User Story 2 - From-description generation runs on the seam (Priority: P2)

A developer describes a single concrete test scenario. The skill routes that request through the same compile → in-session-generate → ingest seam — preserving the from-description behaviors (acceptance-criteria injection per Spec 050, single-test output) — rather than calling the in-process `spectra ai generate --from-description`.

**Why this priority**: From-description is a distinct generation surface (single scenario, criteria-injection contract) that today runs in-process. It must move to the seam for the in-process generator to be fully removable, but it is a narrower flow than bulk and depends on the bulk seam pattern existing first.

**Independent Test**: Run the rewritten skill for a from-description request against a suite that has matching acceptance criteria. Confirm it routes through the seam, the produced test has its `criteria` field populated via the Spec 050 injection, and it never invokes the in-process `--from-description` generator.

**Acceptance Scenarios**:

1. **Given** a from-description request, **When** the skill runs, **Then** it routes through the compile → in-session-generate → ingest seam and MUST NOT call the in-process `spectra ai generate --from-description`.
2. **Given** a suite with matching acceptance criteria, **When** a from-description test is generated, **Then** the criteria-injection behavior (Spec 050) is preserved and the test's `criteria` field is populated.
3. **Given** a from-description request, **When** it completes, **Then** it produces exactly one test and the result surfaces the same fields as today (id/title, suite, linked criteria, any duplicate warnings, non-blocking notes).

---

### User Story 3 - Behavior analysis runs on the seam (Priority: P2)

A developer triggers the mandatory analyze-first step. The recommendation (already-covered count, recommended count, category and technique breakdowns) is produced through the in-session seam rather than the in-process `spectra ai generate --analyze-only` model call.

**Why this priority**: The analyze-first step is mandatory before bulk generation and is the last generation surface still making an in-process model call. It must move to the seam for `ai.providers` and the generator to be removable. It is grouped with from-description as a secondary surface beyond the headline bulk flow.

**Independent Test**: Run the rewritten skill's analyze step against a fixture suite. Confirm the recommendation is produced in-session through the seam, the skill still presents the analyze-first recommendation and STOPs for approval, and it never invokes `spectra ai generate --analyze-only` as an in-process model call.

**Acceptance Scenarios**:

1. **Given** an analyze-first request, **When** the skill runs, **Then** the behavior analysis routes through the same seam and MUST NOT call the in-process `spectra ai generate --analyze-only` model path.
2. **Given** the analysis completes, **When** the skill presents the recommendation, **Then** it still shows the already-covered count, recommended count, and category/technique breakdowns, and STOPs to wait for user approval before generating.
3. **Given** an analysis failure or timeout, **When** the skill handles it, **Then** it surfaces the condition without auto-proceeding to generation (the existing analyze-fail guard behavior is preserved).

---

### User Story 4 - In-process generator, provider chain, and Copilot SDK removed (Priority: P1)

With every generation flow on the seam, a maintainer builds the project and finds the in-process generation path, the provider chain, and the GitHub Copilot SDK gone — and a `spectra.config.json` without `ai.providers` validates and runs.

**Why this priority**: This is the completion the whole series has been building toward — closing the add-not-delete cycle for generation. It is P1 because the migration is not actually done until the dead path is removed, but it strictly **depends on** User Stories 1–3 (the skill must be fully on the seam before anything it called can be deleted).

**Independent Test**: After the skill is on the seam, build the solution and confirm it compiles with `CopilotGenerationAgent`, `CopilotService`, `ProviderMapping`, `AgentFactory.CreateAgentAsync` (and its hardcoded fallback), and the GitHub.Copilot.SDK package reference all removed and unreferenced. Load a config with no `ai.providers` and confirm it validates.

**Acceptance Scenarios**:

1. **Given** the completed cutover, **When** the project is built, **Then** `CopilotGenerationAgent`, `CopilotService`, `ProviderMapping`, `AgentFactory.CreateAgentAsync` and its hardcoded fallback, and the GitHub.Copilot.SDK dependency are gone and nothing in the build references them.
2. **Given** the cleaned config schema, **When** a `spectra.config.json` without `ai.providers` loads, **Then** it validates and generation works (the in-session seam selects the model); the surviving model/cost levers are `ai.critic.model`, `analysis.max_prompt_tokens`, and `debug.enabled`.
3. **Given** an existing config that still carries `ai.providers`, **When** it loads, **Then** the key is ignored-with-notice (consistent with the Spec 058 non-silent deprecation pattern), not a silent drop and not a hard failure.

---

### Edge Cases

- **Boundary rejection mid-flow**: malformed/truncated agent output fails loud at ingest and triggers the bounded choreography retry against the specific error; it never persists a partial batch (053 contract, reused verbatim).
- **Retry limit reached**: when the configured attempt maximum is hit, the skill STOPs and reports the failing test and its specific error rather than keeping an unverified test or looping unbounded.
- **Critic gate `drop`**: a hallucinated test is removed (not persisted); a `pass` keeps it — the 055 critic gate semantics are unchanged.
- **Analyze step fallback**: an analysis failure/timeout surfaces the condition and does not auto-proceed to generation.
- **Legacy `ai.providers` present**: ignored-with-notice; the config still validates and runs.
- **`response_format`**: none exists; its "retirement" remains a verified no-op (carried over from 053/058).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `spectra-generate` skill MUST be rewritten so bulk (`--count`), from-description, and behavior-analysis (`--analyze-only`) generation all run through the compile-prompt → in-session generate → ingest-tests seam (Spec 053). The skill MUST NOT invoke the in-process `spectra ai generate --count`, `--from-description`, or `--analyze-only` model path.
- **FR-002**: The generation skill MUST invoke the critic subagent (Spec 055) as a mandatory, explicit step before persistence and MUST drive the Spec 053 fail-loud choreography retry (invalid boundary → regenerate addressing the specific error, bounded by the configured attempt limit).
- **FR-003**: The from-description and behavior-analysis flows MUST be routable over the seam. If the existing `compile-prompt` / `ingest-tests` surface does not already cover these two flows, the CLI seam MUST be extended (additively) so it does — without reintroducing any in-process model call. (The exact seam shape for these two flows is settled by the planning mini-investigation; see Assumptions.)
- **FR-004**: Once the skill is on the seam, the in-process generation path MUST be removed: `CopilotGenerationAgent`, `CopilotService`, `ProviderMapping`, `AgentFactory.CreateAgentAsync` and its hardcoded `github-models`/`gpt-4o` fallback, plus the GitHub.Copilot.SDK dependency. Nothing in the build may reference them.
  - **As built (narrow-complete, 2026-06-05):** the generation-EXCLUSIVE code was removed — `CopilotGenerationAgent`, `BehaviorAnalyzer`, `UserDescribedGenerator`, `ProviderChain`, `AgentFactory.CreateAgentAsync` (+ fallback), and the `spectra ai generate` command. Implementation surfaced that `CopilotService`, `ProviderMapping`, and the GitHub.Copilot.SDK are **NOT generation-exclusive**: they are still used by the in-process **criteria-extraction** path (`CriteriaExtractor`/`RequirementsExtractor`, driven by `ai analyze --extract-criteria` / `docs index`) and **Copilot auth** (`AuthHandler`/`InitHandler`), both out of this spec's generation scope. Removing them would break those. So `CopilotService` + `ProviderMapping` + the SDK are **retained**; their removal is deferred to a spec that also inverts the criteria-extraction + auth paths. (User-confirmed scope decision.)
- **FR-005**: `ai.providers` MUST be retired now that no code reads it — removed, or relaxed from `required` to ignored-with-notice consistent with the Spec 058 non-silent deprecation pattern. A legacy config carrying it MUST still validate (not a silent drop, not a hard failure). The surviving model/cost config is `ai.critic.model`, `analysis.max_prompt_tokens`, and `debug.enabled`, which MUST be untouched in behavior.
  - **As built (narrow-complete):** `ai.providers` is **retained** — the criteria-extraction path and auth still read it for their provider/model selection. Retiring it is deferred together with the SDK removal in FR-004. No code in the *generation* path reads it.
- **FR-006**: This spec MUST close the add-not-delete cycle for generation: the additive CLI surface (053) becomes the only generation path; the in-process generator is deleted here, not deferred further.
- **FR-007**: The Spec 053 prompt-compiler and ingest/validate boundary, the Spec 055 critic subagent, `TestPersistenceService`, and all of `Spectra.Core` MUST be reused verbatim and MUST NOT be modified in behavior. Any failure in their existing test corpus during this work MUST be treated as a regression to investigate, not a test to update.

### Key Entities *(include if feature involves data)*

- **Generation flow**: one of three skill-driven surfaces — bulk (area/topic exploration), from-description (single concrete scenario), behavior-analysis (analyze-first recommendation). After this spec all three share the same compile → in-session-generate → ingest seam.
- **Compiled generation prompt**: the deterministic, model-free grounded prompt artifact emitted by the seam for a given flow (053). Carries no nondeterministic values; written nowhere on disk by the compiler.
- **Critic gate outcome**: the mandatory per-test verdict from the 055 critic subagent — `pass` (keep), `drop` (remove), or a fail-loud ingest/compile error that triggers a bounded regenerate-and-reverify.
- **Retired surface**: the in-process generator (`CopilotGenerationAgent`/`CopilotService`), the provider chain (`ProviderMapping`/`AgentFactory.CreateAgentAsync` + hardcoded fallback), the GitHub.Copilot.SDK dependency, and the `ai.providers` config key.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of generation flows (bulk, from-description, analyze-only) run through the compile → in-session-generate → ingest seam; the rewritten skill issues zero `spectra ai generate --count` / `--from-description` / `--analyze-only` model invocations.
- **SC-002**: For every test produced in any flow, the critic subagent is invoked as a mandatory step before persistence — no test reaches disk without a critic gate outcome.
- **SC-003**: The fail-loud choreography retry regenerates against a specific error and stops exactly at the configured attempt limit — it never loops unbounded and never persists an unverified test.
- **SC-004**: After the cutover, the build contains no reference to `CopilotGenerationAgent`, `CopilotService`, `ProviderMapping`, `AgentFactory.CreateAgentAsync`, or the GitHub.Copilot.SDK package — verifiable by a clean build with those symbols/dependency removed.
- **SC-005**: A `spectra.config.json` with no `ai.providers` validates and runs; a config still carrying `ai.providers` validates with a non-silent notice (0% silent drops, 0% hard failures).
- **SC-006**: The `Spectra.Core` test corpus and the Spec 053 prompt-compiler / boundary-validation and Spec 055 critic test corpora remain unchanged and green after the feature lands.

## Assumptions

- **Seam shape for from-description / analyze-only**: the precise CLI seam for these two flows (extend `compile-prompt` with a mode/flag vs. add sibling compile subcommands) is an implementation detail settled by a planning mini-investigation — a surface distinct from the batch generation covered by investigation `01`. The contract (no in-process model call; deterministic compile; fail-loud ingest) does not change regardless of shape.
- **`ai.providers` disposition**: retiring via ignore-with-notice (the Spec 058 `DetectDeprecatedKeys` pattern) is preferred over a hard-removal that would reject legacy configs, since a non-silent migration is the established norm. Either satisfies FR-005 as long as legacy configs validate.
- **Critic of record**: the `spectra-critic` `context: fork` subagent (055) remains the single critic of record; the in-process critic was already retired in 058 and is not revisited here.
- **Retry maximum**: the choreography retry maximum is the skill-held configurable value defined in 053 with its existing small default; this spec does not change it.
- **Demo repos**: demo `spectra.config.json` files were migrated to the cleaned schema in 058; if `ai.providers` removal requires a further demo touch-up, it is a mechanical follow-on consistent with the 058 migration, not new scope.

## Out of Scope

- Everything functional in Specs 053–058 (this spec completes what they set up; it does not revisit their behavior).
- Multi-client breadth (Spec 060).
- The defect-injection bake-off (post-migration).
- Any change to `BatchWriteTestsTool` (separate AI-discretion path) or to the update flow's apply-changes path.
- `Spectra.MCP` (untouched throughout the migration series).

## Dependencies

- **Spec 053** (the compile-prompt → ingest-tests seam and fail-loud boundary), **Spec 055** (the critic subagent), **Spec 056** (the orchestration skills), and **Spec 058** (the safe config cleanup, done first). This is the true last step of the migration — generation does not actually move onto the subscription path until this lands.
- Likely needs its own planning mini-investigation for the from-description / analyze-only flows over the seam (a different surface than the batch generation covered by investigation `01`).

## Documentation Impact

- **Factually wrong (must fix)**: the generation workflow / `spectra ai generate` reference once the in-process `--count` path is gone; any remaining provider-config docs (`ai.providers`); the `CLAUDE.md` runtime line if still stale (it currently notes the generator/SDK are retained transitionally for Spec 059 — update once removed).
- **Stale (update)**: getting-started generation snippets still showing `--count` or provider config.
