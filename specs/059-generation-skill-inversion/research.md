# Research: Generation-skill inversion + completion (Spec 059)

This is the planning mini-investigation the spec called for — the seam shape for the from-description and analyze-only flows (a surface distinct from the batch generation covered by investigation `01`), plus the removal strategy for the in-process generator. Grounded in a code read of the live flows and the existing 053/054/055 seam commands.

## D1 — From-description over the seam: EXTEND `compile-prompt`, reuse `ingest-tests`

**Decision**: Add `--from-description "<text>"` and `--context "<text>"` options to the existing `compile-prompt` command. When `--from-description` is present, the command forces `count = 1`, builds the user-prompt from the description+context (the same shaping `UserDescribedGenerator.BuildPrompt` does today), resolves criteria via the unchanged `GenerateHandler.LoadCriteriaContextAsync`, and emits the grounded single-test prompt to stdout. Persistence reuses `ingest-tests` **verbatim** (it already accepts 1..N tests and runs the full fail-loud validation + `TestPersistenceService` write+index path).

**Rationale**:
- The from-description prompt differs from bulk only in (a) the user-prompt text and (b) `count = 1`. Everything downstream — criteria injection (Spec 050 mandatory-mapping block lives in `PromptCompiler.Assemble`/`Compile`), validation, persistence (Spec 049 single write+index) — is identical. A whole new command would duplicate the compile→stdout contract for no behavioral gain.
- YAGNI / Constitution Principle V: from-description is the *second* use of "compile a grounded prompt for a suite", not the third — extend, don't abstract a new command.
- The Spec 050 criteria-injection contract is preserved automatically: `LoadCriteriaContextAsync` already returns the criteria context and `PromptCompiler` already injects the mandatory mapping block when criteria are present.

**Behavior change (intended, an improvement)**: today from-description records a `manual` grounding verdict and is excluded from grounded stats (it ran no critic). On the seam, the rewritten skill runs the **mandatory** `spectra-critic` subagent step (FR-002) on the persisted test, so from-description tests now get a real critic verdict like every other flow. US2 AS3 lists the surfaced fields (id/title, suite, linked criteria, duplicate warnings, notes) and deliberately does not pin the verdict to `manual`.

**Alternatives considered**:
- *New `compile-description-prompt` + `ingest-description` pair* — rejected: duplicates the test compile/ingest contract; from-description is not a distinct artifact type (it produces a `TestCase`, same as bulk).
- *Keep from-description in-process* — rejected: it reads `ai.providers` and calls the agent layer (`UserDescribedGenerator.GenerateAsync` → `agent.GenerateTestsAsync`), which FR-004 deletes.

## D2 — Analyze-only over the seam: NEW sibling pair `compile-analysis-prompt` + `ingest-analysis`

**Decision**: Add a new compile/ingest pair following the established 053/054/055 pattern.
- `compile-analysis-prompt` (model-free, deterministic): resolves the doc-suite documents + existing tests + focus and emits the behavior-analysis prompt to stdout. Relocates the prompt-building half of `BehaviorAnalyzer` into a new `AnalysisPromptCompiler.Compile(...)`.
- `ingest-analysis` (fail-loud): reads the agent's behavior JSON from stdin/`--from`, runs the **deterministic** post-processing — coverage dedup (via `CoverageSnapshot`/title-similarity), category breakdown, ISTQB technique breakdown, and `RecommendedCount = max(0, total − alreadyCovered)` — and emits the recommendation JSON the skill presents. Relocates `BehaviorAnalyzer` lines ~158–172 and the `BehaviorAnalysisResult` shape into a new `AnalysisRecommendationBuilder` + `AnalysisRecommendation` result.

**Rationale**:
- The analyze step's output is **not a test** — it is a recommendation DTO (already-covered count, recommended count, category + technique breakdowns). It cannot reuse `ingest-tests`, which parses/validates/persists `TestCase` objects. A distinct ingest is required.
- The valuable, reliability-critical part of analysis (dedup against real coverage, the recommended-count math, the breakdowns) is **already deterministic** in `BehaviorAnalyzer` — only the behavior *extraction* needs a model. Splitting at the seam puts the model work in-session (Claude lists behaviors) and keeps the deterministic accounting in `ingest-analysis`. This matches Principle II.
- Mirrors the exact compile-*/ingest-* contract (stdout prompt with refuse-to-emit exit 4; stdin/`--from` ingest with fail-loud exit 5/6), so the skill choreography is uniform across all flows.

**Output contract**: `ingest-analysis` emits the same fields the skill already renders from `analysis.*` today — `already_covered`, `recommended`, `breakdown{}`, `technique_breakdown{}` — so the skill's "present recommendation, STOP for approval" step is a near-verbatim port.

**Alternatives considered**:
- *Fold analysis into `compile-prompt` with an `--analyze` mode* — rejected: the ingest side is fundamentally different (recommendation vs. tests); a shared compile with divergent ingest is more confusing than two clean pairs.
- *Make analysis fully deterministic (no model)* — rejected: behavior *identification* from prose genuinely needs a model; only the accounting is deterministic.

## D3 — Removal strategy: delete the agent/provider layer; keep deterministic helpers

**Decision**: Delete `CopilotGenerationAgent` (`GenerationAgent.cs`), `CopilotService`, `ProviderMapping`, `ProviderChain`, and `AgentFactory.CreateAgentAsync` (incl. the hardcoded `github-models`/`gpt-4o` fallback). Remove the model-calling halves of `BehaviorAnalyzer` and `UserDescribedGenerator`. Remove the model-calling execution methods from `GenerateHandler` (the batch/interactive generate loop, the from-description model call, the analyze model path) and remove the `spectra ai generate` command's model flows. **Preserve** the static deterministic helpers the seam depends on — `GenerateHandler.LoadCriteriaContextAsync` (called by `compile-prompt`), `ProfileFormatLoader`, `PromptTemplateLoader`, `PromptCompiler`, `TestPersistenceService` — and all of `Spectra.Core`.

**Rationale**:
- The agent layer's 4 `CreateAgentAsync` call sites (GenerateHandler ×2, `UserDescribedGenerator`, `ProviderChain`) all disappear once the three flows are on the seam, so the layer becomes unreferenced and deletable — satisfying FR-004/FR-006 ("deleted here, not deferred").
- `compile-prompt` already calls `GenerateHandler.LoadCriteriaContextAsync` as a **static** helper; that and the profile/template loaders are pure and reused, so they must survive. Fully deleting `GenerateHandler` is therefore wrong — reduce it to its surviving deterministic helpers instead.
- `BehaviorAnalyzer` and `UserDescribedGenerator` are split, not wholly deleted: their deterministic logic (analysis post-processing; description prompt shaping) relocates into the seam compilers, their model calls are removed.

**Disposition of `spectra ai generate`**: the command's model-calling flows (`--count`, `--from-description`, `--analyze-only`, interactive, `--from-suggestions`, `--auto-complete`) are removed because nothing drives them anymore — the skill drives the seam. The deterministic helpers they called are retained as standalone/static. Any thin remaining command surface that is purely a model-call entry point is unregistered from `AiCommand`. (Exact line-level disposition enumerated in tasks; the test blast-radius is the generate/provider/analyzer test files, which are rewritten.)

**Alternatives considered**:
- *Gut-but-keep (Spec 058 `ShouldVerify => false` style scaffolding)* — rejected here: FR-006 explicitly forbids deferring; 058 used gutting precisely because it was the *narrow* spec and 059 was where deletion would happen. This is that spec.
- *Relocate helpers into a brand-new `CriteriaContextLoader` class* — deferred: keeping them as surviving static members of `GenerateHandler` is lower-risk and avoids touching the working `compile-prompt` call site; a later cleanup can rename.

## D4 — `ai.providers` retirement: ignore-with-notice (Spec 058 pattern)

**Decision**: Retire `ai.providers` by (a) dropping the `required` modifier on `AiConfig.Providers` (make it optional/nullable with a default empty list) and the `MISSING_PROVIDERS` validation in `ConfigLoader`, and (b) adding `ai.providers` to `ConfigLoader.DeprecatedKeyPaths` so a legacy config carrying it surfaces a non-blocking notice via the existing `DetectDeprecatedKeys`. A config with no `ai.providers` validates; a config with it validates with a notice. Surviving model/cost levers (`ai.critic.model`, `analysis.max_prompt_tokens`, `debug.enabled`) are untouched.

**Rationale**: This is the exact pattern Spec 058 established for the retired critic keys (`DetectDeprecatedKeys`, non-silent). Reusing it keeps the migration consistent and guarantees FR-005's "not a silent drop, not a hard failure". The Spec 058 `ProviderRetirementTests` explicitly asserted `ai.providers` was *not yet* flagged ("RETAINED — generator"); that assertion is now updated to expect it flagged.

**Rationale for not hard-removing the model member**: System.Text.Json ignores unmapped members, so even fully removing the C# `Providers` property keeps legacy configs loading. But keeping the property as optional + flagged gives a *named* notice (better UX) and avoids a churny removal of a property other read sites (config print, etc.) still reference. Optional+flagged is the minimal, consistent choice.

**Alternatives considered**:
- *Hard-remove `Providers` from `AiConfig`* — rejected: more churn (multiple read sites), and the ignore-with-notice path already satisfies the requirement with a clearer message.
- *Reject legacy configs with guidance* — rejected: violates FR-005 ("not a hard failure") and the established non-silent-but-non-blocking norm.

## D5 — Sequencing & regression safety

**Decision**: Implement Phase A (seam coverage + skill rewrite) fully and green before starting Phase B (removal). Within Phase B, remove call sites before their targets (compiler/handler edits → then delete `Agent/` classes → then drop the package ref → then retire config). Run `dotnet build` + targeted test projects after each removal cluster.

**Rationale**: Phase A is additive and keeps the in-process command working as a safety net until the skill is proven on the seam. Removing call sites first means each `Agent/` deletion compiles cleanly. The `Spectra.Core` and 053/055 corpora are the regression net — any red there is a regression to investigate, never a test to "fix" (FR-007).

## Resolved unknowns

| Unknown (from spec Assumptions) | Resolution |
|---|---|
| Seam shape for from-description | D1 — extend `compile-prompt` (`--from-description`/`--context`, count=1); reuse `ingest-tests`. |
| Seam shape for analyze-only | D2 — new `compile-analysis-prompt` + `ingest-analysis` pair; deterministic post-processing relocated. |
| `ai.providers` disposition | D4 — optional + ignore-with-notice via `DetectDeprecatedKeys` (Spec 058 pattern). |
| How much of `GenerateHandler` survives | D3 — keep static deterministic helpers (`LoadCriteriaContextAsync` et al.); remove model-calling methods. |
| Retry maximum | Unchanged — skill-held 053 default (2 attempts). |
