# Phase 0 — Research: Token Usage Tracking & Model/Provider Logging

## R1. Existing `DebugLogger` and `Enabled` flag

**Decision**: Repurpose the existing `DebugLogger.Enabled` static gate. Change the default from `true` to `false` and drive it from the new `DebugConfig` at startup. Keep the `Append(component, message)` signature for the non-AI call sites that already use it.

**Rationale**: The static class already has the right shape (best-effort, swallow exceptions, never blocks). Adding new methods rather than ripping it out preserves all existing call sites in `BehaviorAnalyzer`, `GenerationAgent`, `GroundingAgent`. The current default is `true` plus `ai.debug_log_enabled` config — that field becomes obsolete and is replaced by `debug.enabled`.

**Alternatives considered**:
- Convert `DebugLogger` to an injected `IDebugLogger` — rejected, every call site would change for zero benefit and the static is already a working seam.
- Keep `ai.debug_log_enabled` for back-compat — rejected, two ways to express the same setting is confusing; the new field replaces it cleanly.

**Action**: Add `DebugLogger.AppendAi(component, message, model, provider, tokensIn, tokensOut)` overload. Existing `Append` keeps working for `TESTIMIZE` lines etc.

---

## R2. Existing `TokenUsage` record at `Spectra.CLI/Agent/IAgentRuntime.cs`

**Decision**: Move/redefine `TokenUsage` into `Spectra.Core.Models` with field names `PromptTokens` and `CompletionTokens` (matching the API response shape). The existing CLI record (`InputTokens`, `OutputTokens`, `TotalTokens`) is currently unused (only one assignment, `TokenUsage = null`). Delete it from `IAgentRuntime.cs` and re-export from Core.

**Rationale**: The spec mandates the `PromptTokens`/`CompletionTokens` names. The existing CLI type has zero real call sites populating it, so renaming/moving is safe.

**Alternatives considered**: Keep both — rejected, two records called `TokenUsage` is a footgun.

---

## R3. How does the GitHub Copilot SDK return token usage?

**Decision**: The Copilot SDK wraps a chat completion response object that mirrors OpenAI's shape (`usage.prompt_tokens`, `usage.completion_tokens`, `usage.total_tokens`). For each AI call site, after the awaited call, read the `Usage` property off the response if present and convert to a `TokenUsage` record. If the property is null/absent (some providers omit it), record `null` and have the debug log emit `tokens_in=? tokens_out=?`.

**Rationale**: The CopilotService already returns a wrapper with `Data.Content`. We extend it (or read the existing wrapper) to expose `Data.Usage` if available. The "graceful unknown" path is required by FR-008 anyway.

**Action**: Inspect `CopilotService` and the SDK's response object during Phase 2 implementation. Add a `Usage` property to the `CopilotResponse` wrapper if not already there. Handlers consume `response.Data.Usage` directly.

---

## R4. Where is wall-clock duration measured?

**Decision**: Wrap each AI call with `Stopwatch.StartNew()` immediately before the awaited send and stop it immediately after the await returns. Record the elapsed `TimeSpan` as part of the `TokenUsageTracker.Record(...)` call. Run-level wall-clock time for the run-context block is measured from the handler entry to the handler return (existing pattern).

**Rationale**: Per-call elapsed = pure AI latency for the cost/perf table. Handler-level elapsed = wall-clock for the user. Showing both highlights non-AI overhead (file I/O, parsing, dedupe) — a deliberate diagnostic feature.

---

## R5. Spectre.Console rendering style

**Decision**: Build the Run Summary as a `Panel` containing a small key/value `Grid` (run-context block) followed by a `Table` (per-phase usage) followed by a final `Markup` line ("Estimated cost: ..."). Match the column alignment used by the existing `TechniqueBreakdownPresenter` so it visually fits.

**Rationale**: Spec mandates "follows the same pattern as the existing technique/category breakdown tables".

**Action**: Search for the existing technique presenter during implementation and clone its structure.

---

## R6. Verbosity / output-format gating

**Decision**: A `--verbosity diagnostic` value already does not exist in the CLI; the only documented levels are `quiet`/`normal`. Add `diagnostic` as a new value to the verbosity enum. The Run Summary panel renders at `normal` and `diagnostic`; suppressed at `quiet`. JSON output never renders the panel (FR-020). `diagnostic` additionally force-enables `DebugLogger.Enabled = true`.

**Rationale**: Spec calls out `--verbosity diagnostic` explicitly. Adding it is a small, additive enum change.

**Alternatives considered**: A separate `--debug-log` boolean flag — rejected, `diagnostic` is more conventional and doubles as future verbose-output channel.

---

## R7. Cost estimation rate table — where to live

**Decision**: Static `Dictionary<string, (decimal InputPer1M, decimal OutputPer1M)>` inside a new `CostEstimator` class in `Spectra.CLI/Services/`. Lookup is case-insensitive on the model name. The tracker calls `CostEstimator.Estimate(phases, provider)` and gets back a nullable `decimal` plus a display string.

**Rationale**: Isolating it makes it trivial to unit-test and to update prices later. Keeps `TokenUsageTracker` focused on aggregation, not pricing.

---

## R8. Aggregation key

**Decision**: Aggregate by `(phase, model, provider)` tuple. So if a single run runs analysis with `gpt-4.1` and generation with `gpt-4o-mini`, that's two rows. If two batches in generation both use `gpt-4o-mini` on `azure-openai`, that's one row with `calls=2`.

**Rationale**: Minimum information loss while still collapsing the typical N-batch case to a single row. Matches the spec's example tables.

---

## R9. ProgressManager and `.spectra-result.json`

**Decision**: Extend the existing result-file payload with `run_summary` and `token_usage` keys. ProgressManager already writes `.spectra-result.json` periodically; we add a `WriteTokenUsage(TokenUsageReport report)` method that the tracker calls after each AI call (cheap — write is debounced or just overwritten in place since the file is small).

**Rationale**: SKILLs and the progress page poll this file; making token_usage live is the same mechanism as existing phase progress.

**Caveat**: If the existing ProgressManager only flushes between phases, intra-phase live updates may need a small additional flush hook. Decision: write on each `Record(...)` call but with the existing best-effort file-write helper (already debounced via the file system itself).

---

## R10. Backwards compatibility for `ai.debug_log_enabled`

**Decision**: Remove `AiConfig.DebugLogEnabled` entirely. The new `DebugConfig.Enabled` replaces it. Existing configs that still have `ai.debug_log_enabled` set will silently ignore it (System.Text.Json default behavior with unknown fields). Document the rename in the changelog and config docs.

**Rationale**: YAGNI — keeping two settings is the kind of complexity the constitution forbids. The setting is internal-debug only; no production user has scripts depending on it.

**Alternatives considered**: Soft migration that reads the old field as a fallback — rejected as unnecessary; users who had it set will get the new default (off) which is what they want anyway.

---

## R11. Test strategy for AI call sites without real network

**Decision**: Token recording is unit-tested at the `TokenUsageTracker` level (no AI calls). Integration of the tracker with the agents is verified by a single contract test per agent that uses a fake `IAgentRuntime` returning a known `TokenUsage`. We do not stand up a real Copilot SDK call for tests.

**Rationale**: Token recording logic is the part that can break; the wiring is straightforward. The existing test suite already mocks agents at this level.

---

All NEEDS CLARIFICATION items resolved. Phase 0 complete.
