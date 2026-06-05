# Phase 0 Research: Critic as a `context: fork` Subagent

All "unknowns" here are decisions about *how* to apply the established series pattern to the
critic seam, not open technical questions — the seam is fully characterized in
`docs/investigation/02-critic.md` with file+line evidence. Each decision is recorded in
Decision / Rationale / Alternatives form.

## R1 — Additive delivery (scope of FR-001/FR-003), matching the series precedent

**Decision**: Ship the model-free verification surface (compile + ingest), the `context: fork`
critic subagent skill, and the FR-004/FR-008 cleanup **additively**, while leaving the in-process
critic model call (`CopilotCritic.VerifyTestAsync` → `session.SendAsync`, `GroundingAgent.cs:124`)
in place and working. The literal removal of that call, and the swap of `spectra ai generate`'s
batch verification to invoke the subagent, are deferred to the subsequent wiring spec.

**Rationale**: This is exactly what the two preceding series specs shipped. 053 delivered
`compile-prompt` + `ingest-tests` and left `CopilotGenerationAgent.SendAndWaitAsync` in place
(commit `a204605`, tagged "CLI surface"). 054 delivered `compile-extraction-prompt` +
`ingest-criteria` and kept both extractors' in-process calls working (commit `fe4a60a`, tagged
"CLI surface"). Removing the critic's in-process call now would leave `spectra ai generate` with no
critic until the generation skill is rewired — a broken intermediate state the series explicitly
avoids. The spec's own dependency note states the subagent skill "MUST exist before the next spec
can wire its mandatory invocation," confirming this ordering.

**Alternatives considered**:
- *Literal removal now* — rejected: breaks the working generate flow; contradicts the 053/054
  precedent and the spec's prerequisite ordering.
- *Gating cleanup only (FR-005–FR-008), defer the subagent* — rejected: leaves FR-002/FR-003
  undelivered, so the subsequent spec has nothing to wire; delivers under half the spec.

## R2 — Reuse `CriticPromptBuilder` behind a validated compiler (FR-002)

**Decision**: The new `CriticPromptCompiler.Assemble(test, docs, templateLoader)` delegates to the
**reused-verbatim** `CriticPromptBuilder` (`BuildSystemPrompt` + `BuildUserPrompt`, joined
`{system}\n\n---\n\n{user}` exactly as `GroundingAgent.cs:77–79`). `Compile(...)` wraps `Assemble`
with a refuse-to-emit guard that fails when a required input is missing (null test, or a test with
no id/title — the artifact the critic must verify).

**Rationale**: The builder's isolation already matches the v2 target (artifact + ≤5 selected docs,
`CriticPromptBuilder.cs:76–141`, `MaxDocuments=5`, `MaxDocumentChars=8000`) and the spec lists it
as "reused verbatim — do not modify." Mirroring 053's `PromptCompiler` (lenient `Assemble` + a
validated `Compile`) and 054's `ExtractionPromptCompiler` gives the series a third identical shape:
the assembly is the single source of prompt truth; the command calls the validated entry. The
builder is already deterministic (no timestamps/GUIDs; document order is the input order).

**Alternatives considered**:
- *Relocate the builder body into the compiler (as 053/054 did with their `BuildFullPrompt` /
  `BuildExtractionPrompt`)* — rejected here: the spec pins `CriticPromptBuilder` as reused
  verbatim, and it is a public class with a template-loader hook already consumed by the runtime;
  wrapping (not relocating) avoids touching a do-not-modify type while still giving a standalone,
  testable compile artifact.

## R3 — Fail-loud verdict ingest as a typed outcome (FR-006, FR-007)

**Decision**: Add `VerdictIngestor.Classify(criticJson)` returning a typed `VerdictIngestResult`
with outcome `Verdict | EmptyResponse | ParseFailure`. A missing or unparseable `verdict` or
`score` yields `ParseFailure` with a specific error — **never** the current silent `Partial` /
`0.5` coercion (`CriticResponseParser.cs:95, 110`). Empty/whitespace input yields `EmptyResponse`.
A well-formed response yields `Verdict` carrying a `VerificationResult` (reused-verbatim type) plus
a derived gate decision (drop iff `Hallucinated`). `Classify` never throws.

**Rationale**: This is the "fail loud on damage" half of the gating split. The reused-verbatim
`VerificationResult` / `VerificationVerdict` cannot itself distinguish *damage* (malformed but
returned) from *failure* (call threw/timed out) — today both flow through `CreateErrorResult` to
`Partial` + `Errors` (`GroundingAgent.cs:209`, `CriticResponseParser.cs:175`), which is exactly the
conflation FR-006/FR-007 remove. A typed outcome at the new boundary makes damage loud while
leaving the runtime's *failure* path (Unverified-style `Partial` + `Errors`) untouched and
distinct — satisfying FR-007 without mutating any do-not-touch type. The parse *structure*
(`ExtractJson` fence-stripping + `{…}` slicing, `CriticResponseParser.cs:50`) is reused; only the
missing-field *defaults* change, as the spec's "Reused Verbatim" clause permits.

**Alternatives considered**:
- *Edit `CriticResponseParser.ParseVerdict`/`ParseScore` defaults in place* — rejected: the parser
  is shared by the retained in-process path (`GroundingAgent.cs:139`); changing its defaults there
  would alter the live path's behavior in this additive spec. The new strict classification lives
  in the new boundary; the parser's shape is reused, its defaults are re-decided at the boundary.
- *Add an `Unverified` value to `VerificationVerdict`* — rejected: mutates a do-not-touch enum;
  "unverified" is already modeled as `Partial` + `Errors` and passes the gate (`!= Hallucinated`).

## R4 — Failure vs. parse-miss distinctness (FR-007)

**Decision**: Keep the runtime's *call-failure* path producing the Unverified-style result
(`VerificationResult.Unverified` / `CreateErrorResult` → `Partial` + `Errors`, non-blocking) and
make the *parse-miss* path a distinct typed `ParseFailure` at the new ingest boundary. The two are
never routed through the same classification, so they are recorded distinctly by construction.

**Rationale**: FR-007 requires failure to stay non-blocking (`Unverified`, test passes) while being
*distinguishable* from a malformed response. Since failure is a runtime/transport event (exception,
timeout) and parse-miss is a content event, they naturally separate: failure never reaches the
strict ingest as a "verdict"; only a *returned* response is classified, and a malformed returned
response is `ParseFailure` (loud), not `Unverified`. The gate (`Verdict != Hallucinated`) keeps
both non-blocking for generation.

**Alternatives considered**:
- *One shared error code for both* — rejected: that is the present-day conflation (investigation
  F-3) the spec exists to remove.

## R5 — Single critic-model selector + dead-code removal (FR-004, FR-008)

**Decision**: Introduce `CriticModelResolver.Resolve(CriticConfig)` as the single source of truth:
`config.Model` when set, else one same-family default constant (§32 direction; target Sonnet 4.6).
Route both `CopilotCritic.GetEffectiveModel` (`GroundingAgent.cs:192`) and
`CopilotService.GetCriticModel` (`CopilotService.cs:319`) through it, deleting the duplicated
provider→default switch from both. Delete the unreferenced `CopilotCriticFactory`
(`GroundingAgent.cs:226`, investigation F-1). Update the stale cross-architecture comments
(`GroundingAgent.cs:197`, `CopilotService.cs:324`) to the §32 same-family direction.

**Rationale**: Investigation F-1 confirms `CopilotCriticFactory` is referenced only in its own file
(the live factory is `CriticFactory`). F-2 confirms the default-model switch is duplicated across
two files that "currently agree but could drift." Collapsing to one resolver makes
`ai.critic.model` the single selector (FR-004) and removes the drift risk (FR-008) — a net
reduction in complexity. The same-family default is the §32 decision the config keeps open for the
post-migration bake-off (out of scope here).

**Alternatives considered**:
- *Keep a provider-keyed default switch but dedupe it* — rejected: FR-004 explicitly requires the
  provider→default fallback be removed as a *source of truth*; a single same-family default plus
  the `ai.critic.model` override is the literal reading.
- *Delete the default entirely (require `ai.critic.model`)* — rejected: the retained in-process
  path must still resolve a model when the key is unset; a single default preserves that without a
  switch.

## R6 — `context: fork` critic subagent skill (FR-002, FR-003)

**Decision**: Add `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md`, a net-new
subagent declared with fresh/forked-context isolation, whose instruction restricts its input to the
test artifact + selected source documents and whose procedure produces the `{ verdict, score,
findings }` JSON the `ingest-verdict` boundary consumes. It is authored for **explicit** invocation
as a mandatory step (`disable-model-invocation: true`, mirroring the existing agents), never
auto-invocation. Register it in `SkillsManifest` and `AgentContent`.

**Rationale**: The existing agent files (`spectra-generation.agent.md`,
`spectra-execution.agent.md`) already use frontmatter (`name`, `description`, `tools`, `model`,
`disable-model-invocation: true`) and are the established authoring surface. The critic's isolation
already exists by construction (the builder never includes generator state); the skill formalizes
it as `context: fork`. `disable-model-invocation: true` is exactly the "never auto-invocation"
property FR-003 requires; the mandatory-step *wiring* into the generation procedure is the
subsequent spec.

**Alternatives considered**:
- *Author the skill but auto-invoke it* — rejected: FR-003 forbids auto-invocation; the skill must
  be an explicit step.
- *Defer the skill to the wiring spec* — rejected: FR-002 makes the subagent skill a deliverable of
  *this* spec, and the next spec depends on it existing.

## R7 — Exit-code contract for the new commands

**Decision**: `compile-critic-prompt` → exit 4 on refuse (missing required artifact), 0 on emit.
`ingest-verdict` → exit 0 on `Verdict`, 5 on `EmptyResponse`, 6 on `ParseFailure`, 1 on environment
error (no config). Matches the 054 extraction commands' contract exactly.

**Rationale**: Consistency across the series: 053 `ingest-tests` uses 5 (content-invalid) / 6
(schema-invalid); 054 `ingest-criteria` uses 5 (empty) / 6 (parse). Reusing 4/5/6 keeps CI
scripting uniform and the distinct codes preserve FR-006/FR-007 distinctness at the process
boundary.

**Alternatives considered**:
- *Collapse empty and parse into one non-zero code* — rejected: erases the FR-006/FR-007
  distinction at the exit-code boundary that CI relies on.
