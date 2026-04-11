# Feature Specification: Token Usage Tracking & Model/Provider Logging

**Feature Branch**: `040-token-usage-tracking`
**Created**: 2026-04-11
**Status**: Draft
**Input**: User description: Spec 040 — Token Usage Tracking & Model/Provider Logging. Capture per-call token usage and elapsed time across all AI phases (analysis, generation, critic, update, criteria), surface model/provider on every AI debug log line, render a Run Summary panel after generate/update commands, include `run_summary` and `token_usage` in `--output-format json` and `.spectra-result.json`, estimate cost from a hardcoded BYOK rate table (github-models excluded), make `.spectra-debug.log` opt-in via a new `debug` config section with a `--verbosity diagnostic` override, and update the progress page to show live token totals.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See cost and token usage after a generation run (Priority: P1)

A test author runs `spectra ai generate checkout --count 20` and, when it finishes, immediately sees how many tokens were consumed per phase (analysis, generation, critic), the model that handled each phase, the elapsed AI time, and an estimated USD cost (or "included in Copilot plan" for github-models). They can compare this between runs to decide whether to switch model, batch size, or skip the critic.

**Why this priority**: The whole point of the spec is cost visibility. Without this, every other piece (debug log, progress page, JSON) is just plumbing. This is the user-facing payoff.

**Independent Test**: Run `spectra ai generate <suite> --count N` against a real config and confirm the Run Summary table appears before the final status line, with non-zero token counts and a cost estimate (or "included in Copilot plan" message) appropriate to the configured provider.

**Acceptance Scenarios**:

1. **Given** a configured generation run with provider `github-models`, **When** the run completes, **Then** the terminal shows a Run Summary panel with per-phase token counts, a TOTAL row, and the message "Estimated cost: included in Copilot plan (rate limits apply)".
2. **Given** a configured generation run with provider `azure-openai` and a known model, **When** the run completes, **Then** the Run Summary shows a USD cost computed from the hardcoded rate table.
3. **Given** `--verbosity quiet`, **When** the run completes, **Then** the Run Summary panel is suppressed but token data is still recorded internally.
4. **Given** `--output-format json`, **When** the run completes, **Then** the JSON result on stdout contains `run_summary` and `token_usage` fields and no terminal table is rendered.

---

### User Story 2 - Identify which model handled each AI call from the debug log (Priority: P1)

A developer troubleshooting a slow batch opens `.spectra-debug.log` and immediately sees which model and provider handled each call, plus tokens in/out. They can correlate elapsed time to model choice without guessing.

**Why this priority**: Debug logs without model context are nearly useless when comparing providers. This is a small change with high diagnostic value and unblocks every later optimization conversation.

**Independent Test**: Enable debug logging, run any AI command, open `.spectra-debug.log`, and verify every AI call line ends with `model=<name> provider=<name> tokens_in=<n> tokens_out=<n>`.

**Acceptance Scenarios**:

1. **Given** debug logging enabled and a generation run, **When** the log is inspected, **Then** every `BATCH OK`, `CRITIC OK`, `ANALYSIS OK`, `UPDATE OK`, and criteria-extraction line includes `model=` and `provider=` suffixes.
2. **Given** an AI response that omits the `usage` object, **When** the log is written, **Then** the line still appears with `tokens_in=? tokens_out=?` so it remains grep-able.
3. **Given** a non-AI line such as `TESTIMIZE HEALTHY`, **When** the log is inspected, **Then** the line is unchanged (no model/provider suffix).

---

### User Story 3 - Opt in or out of debug logging without editing code (Priority: P1)

An operator running Spectra in CI does not want stale `.spectra-debug.log` files written on every run. They set `debug.enabled: false` in `spectra.config.json` (the default) and no debug file is created. When troubleshooting a single bad run, they re-run with `--verbosity diagnostic` to force debug output for that run only.

**Why this priority**: Today the debug log is always on with no off switch. Production users want it off; troubleshooters want a one-shot override. This is a behavior change visible to every user.

**Independent Test**: With `debug.enabled: false`, run any AI command and confirm no `.spectra-debug.log` is created. Re-run with `--verbosity diagnostic` and confirm the file is created and populated.

**Acceptance Scenarios**:

1. **Given** a config without a `debug` section, **When** any AI command runs, **Then** no debug log file is created (default disabled) and no disk I/O for debug logging occurs.
2. **Given** `debug.enabled: true` in config, **When** any AI command runs, **Then** the debug log is written to the configured `log_file` path.
3. **Given** `debug.enabled: false` and `--verbosity diagnostic` on the command line, **When** the command runs, **Then** debug logging is force-enabled for that run regardless of config.
4. **Given** `spectra init` is run on a fresh project, **When** the resulting `spectra.config.json` is inspected, **Then** it contains a `debug` section with `enabled: false`.

---

### User Story 4 - SKILLs and progress page report token usage (Priority: P2)

A user watching the live progress page during a long generation sees token totals climbing as batches complete. After the run, the SKILL summary message includes "89K tokens in 2m48s. Cost: included in Copilot plan." pulled directly from `.spectra-result.json`.

**Why this priority**: Important for SKILL/CI users but built on top of P1 plumbing. The terminal summary is the primary surface; this is the secondary surface.

**Independent Test**: Run a long generation, open `.spectra-progress.html` mid-run, confirm the Token Usage row updates. After completion, parse `.spectra-result.json` and confirm `token_usage.total.tokens_in/out`, `token_usage.phases[]`, and `run_summary` fields are present.

**Acceptance Scenarios**:

1. **Given** a generation run in progress, **When** the progress page is refreshed, **Then** the Token Usage section shows current running totals.
2. **Given** a completed run, **When** `.spectra-result.json` is parsed, **Then** it contains `run_summary` and `token_usage` matching what was rendered to the terminal.

---

### Edge Cases

- AI response missing `usage` object → log `tokens_in=? tokens_out=?`, exclude from totals (do not crash).
- A run where the critic is skipped (`--skip-critic`) → Run Summary still renders with no `Critic` row.
- Concurrent batch calls writing to the tracker → recording is thread-safe; aggregation merges entries by `(phase, model, provider)`.
- Cost lookup misses (model not in table) → show "Cost estimate unavailable for {model}", `estimated_cost_usd: null` in JSON.
- Provider is `github-models` even with a known model → still emit "Included in Copilot plan", `estimated_cost_usd: null`.
- Existing `spectra.config.json` files without a `debug` section → load with default `debug.enabled = false` (no breaking change).
- A run that fails partway → Run Summary still shows tokens consumed up to the failure point.
- `.spectra-result.json` is updated incrementally by ProgressManager so the progress page can poll partial token data.

## Requirements *(mandatory)*

### Functional Requirements

#### Debug log configuration

- **FR-001**: System MUST support a new `debug` section in `spectra.config.json` with `enabled` (bool, default `false`) and `log_file` (string, default `.spectra-debug.log`).
- **FR-002**: System MUST treat the absence of a `debug` section in existing configs as equivalent to the default (disabled), with no migration step required.
- **FR-003**: System MUST write to the debug log file only when `debug.enabled` is true OR `--verbosity diagnostic` is passed; otherwise the debug logger MUST be a no-op with zero disk I/O.
- **FR-004**: `--verbosity diagnostic` MUST force-enable debug logging for the duration of the command regardless of config.
- **FR-005**: `spectra init` MUST write a `debug` section with `enabled: false` into the new `spectra.config.json`.

#### Model/Provider in debug log

- **FR-006**: Every AI-call debug log line (analysis, generation, critic, update, criteria-extraction) MUST end with `model=<name> provider=<name>`.
- **FR-007**: Non-AI debug log lines (e.g., `TESTIMIZE HEALTHY`, file I/O) MUST remain unchanged.
- **FR-008**: When the AI response omits token usage, the log line MUST still be written with `tokens_in=? tokens_out=?` so it remains grep-able.

#### Token capture

- **FR-009**: System MUST capture `prompt_tokens` and `completion_tokens` from every AI completion response across all phases: behavior analysis, generation, critic verification, test update, criteria extraction.
- **FR-010**: System MUST record per-call token usage along with phase, model, provider, and elapsed wall-clock time of the call.
- **FR-011**: Token recording MUST be thread-safe so concurrent batch calls cannot corrupt the tracker.
- **FR-012**: System MUST aggregate recorded entries by `(phase, model, provider)` so the same phase using two models appears as two summary rows.
- **FR-013**: System MUST compute a grand total across all phases including total calls, total tokens in/out, and total elapsed AI time.

#### Run Summary rendering

- **FR-014**: After every `spectra ai generate` and `spectra ai update` run, the system MUST render a Run Summary panel containing a run-context block and a token-usage table, before the final status line.
- **FR-015**: For `generate`, the run-context block MUST include: documents processed, behaviors identified, tests generated (with verdict breakdown grounded/partial/rejected), batch size and batch count, wall-clock duration.
- **FR-016**: For `update`, the run-context block MUST include: tests scanned, tests updated (with classification breakdown), tests unchanged, chunk count, wall-clock duration.
- **FR-017**: The token-usage table MUST contain columns Phase, Model, Calls, Tokens In, Tokens Out, Total, Time, with one row per `(phase, model, provider)` aggregate plus a TOTAL row.
- **FR-018**: The token-usage table MUST display an "Estimated cost" line below the TOTAL row.
- **FR-019**: `--verbosity quiet` MUST suppress the Run Summary panel without disabling internal recording.
- **FR-020**: `--output-format json` MUST suppress the terminal panel and instead include the data in the JSON output.

#### JSON and result file

- **FR-021**: `--output-format json` results for `generate` and `update` MUST include `run_summary` and `token_usage` objects matching the terminal data.
- **FR-022**: `.spectra-result.json` for `generate` and `update` MUST include `run_summary` and `token_usage` so SKILLs and the progress page can read them.
- **FR-023**: `token_usage.phases[]` entries MUST include `phase`, `model`, `provider`, `calls`, `tokens_in`, `tokens_out`, `elapsed_seconds`.
- **FR-024**: `token_usage.total` MUST include `calls`, `tokens_in`, `tokens_out`, `total_tokens`, `elapsed_seconds`.
- **FR-025**: `token_usage.estimated_cost_usd` MUST be `null` when cost cannot be estimated (github-models, unknown model, or any phase with unknown rate).

#### Cost estimation

- **FR-026**: System MUST maintain a hardcoded lookup of input/output per-1M-token rates for known models (initial set: gpt-4o, gpt-4o-mini, gpt-4.1, gpt-4.1-mini, gpt-4.1-nano, claude-sonnet-4-20250514, claude-3-5-haiku-latest, deepseek-v3.2).
- **FR-027**: When provider is `github-models`, the system MUST display "Included in Copilot plan (rate limits apply)" instead of a dollar amount and emit `estimated_cost_usd: null` in JSON.
- **FR-028**: When the model is not in the lookup table, the system MUST display "Cost estimate unavailable for {model}" and emit `estimated_cost_usd: null` in JSON.
- **FR-029**: When provider is `azure-openai` or `azure-anthropic`, the system MUST use the same lookup rates as their direct API equivalents.

#### Progress page

- **FR-030**: `.spectra-progress.html` MUST include a Token Usage section that reads from `.spectra-result.json` and reflects current running totals during a run and final totals after completion.
- **FR-031**: ProgressManager MUST write incremental `token_usage` updates to `.spectra-result.json` as phases complete.

#### Backwards compatibility

- **FR-032**: Existing tests for generate/update/critic flows MUST continue to pass without modification; token tracking MUST be additive.

### Key Entities

- **DebugConfig**: Configuration block governing debug log behavior. Attributes: `enabled` (bool), `log_file` (path).
- **TokenUsage**: A pair of `prompt_tokens` and `completion_tokens` from one AI response, with derived `total_tokens`.
- **PhaseUsage**: An aggregate row representing one `(phase, model, provider)` combination across multiple calls. Attributes: phase name, model, provider, call count, tokens in, tokens out, summed elapsed time.
- **TokenUsageTracker**: Thread-safe collector that records individual AI call usage and produces phase aggregates and a grand total. Single instance per command run.
- **RunSummary**: Combined run-context + token-usage payload rendered to terminal, written to JSON, and persisted in `.spectra-result.json`.
- **CostRateTable**: Static dictionary of model name → (input per 1M tokens, output per 1M tokens) used by cost estimation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a `spectra ai generate --count N` run, a user can identify total tokens consumed, per-phase breakdown, and (where applicable) a USD cost estimate without opening any other file or running any other command.
- **SC-002**: For any AI debug log entry, a user can determine which model and provider handled the call within seconds of opening the log, with no cross-referencing needed.
- **SC-003**: Default Spectra runs (no config changes) produce zero `.spectra-debug.log` files, eliminating stale-file accumulation in CI environments.
- **SC-004**: A single `--verbosity diagnostic` run can produce a complete debug log for troubleshooting without requiring any config edit, restart, or persistent setting change.
- **SC-005**: SKILLs and the progress page can present token usage to users by reading only `.spectra-result.json`, with no additional CLI invocations.
- **SC-006**: 100% of generate/update CLI flows record token data when the AI response includes a `usage` object; the remaining flows degrade gracefully with `?` placeholders rather than crashing or omitting log lines.
- **SC-007**: Cost estimation correctly returns `null` (and a non-dollar message) for github-models in 100% of cases, preventing misleading dollar amounts for users on Copilot-included billing.
- **SC-008**: All existing generate, update, and critic tests continue to pass after the change with no modifications, confirming the additive nature of the feature.

## Assumptions

- The Copilot SDK exposes `usage.prompt_tokens` and `usage.completion_tokens` (or equivalent) on completion responses for the providers Spectra supports. For providers that omit these, graceful `?` fallbacks are acceptable.
- Cost rates in the hardcoded table are maintained manually; staleness is acceptable as long as the message labels them as estimates.
- Wall-clock duration in run context is measured from handler entry to exit and includes non-AI work (file I/O, parsing); the difference vs. summed phase elapsed time is intentionally visible to highlight non-AI overhead.
- A `--verbosity diagnostic` flag already exists or will be added as part of this work as the harness for the diagnostic-mode override; if it does not exist, a minimal addition is in scope.
- `RunSummary` rendering uses the existing Spectre.Console panel/table style from the technique/category breakdown so no new visual conventions are introduced.
- Progress page incremental updates rely on existing ProgressManager write cadence; no new file watcher or push mechanism is required.

## Out of Scope

- Persisting cost or token history across runs in a database.
- Budget limits or alerting thresholds (e.g., abort if estimated cost exceeds X).
- Real-time pricing API lookups; the hardcoded table is intentional.
- Per-test cost attribution (which test consumed which tokens).
- Token tracking for non-AI MCP tools (Testimize, file scanners, etc.).
