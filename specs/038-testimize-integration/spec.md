# Feature Specification: Testimize Integration — Algorithmic Test Data Optimization

**Feature Branch**: `038-testimize-integration`
**Created**: 2026-04-10
**Status**: Draft
**Input**: User description: "Feature Spec 036: Testimize Integration — Optional MCP-based integration with the Testimize.MCP.Server tool that gives SPECTRA's AI generation pipeline access to algorithmic test data optimization (BVA, EP, Pairwise, ABC). Disabled by default, fully graceful degradation, no NuGet coupling."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mathematically Optimal Boundary Values (Priority: P1)

A test engineer has SPECTRA configured to generate test cases for a form with documented numeric range "0–999999999". With Testimize disabled, SPECTRA's AI approximates the boundary set (typically 3–4 values). With Testimize enabled, the AI calls the Testimize tool, receives a mathematically computed boundary set including min−1, min, min+1, max−1, max, max+1, plus invalid type cases ("abc", empty, decimal, infinity), and writes test cases that use those exact values verbatim.

**Why this priority**: This is the core promise of the feature — replace AI-approximated boundary values with algorithmically optimal ones. Delivering this story alone gives users measurably better boundary coverage on numeric fields, which is where most input-validation defects live.

**Independent Test**: With Testimize installed and enabled, run `spectra ai generate --suite checkout` against a fixture document containing a numeric input field with range 0–100. Verify the resulting test cases include values 0, 1, 99, 100, −1, 101 verbatim in step text, not approximations.

**Acceptance Scenarios**:

1. **Given** Testimize is enabled and installed, **When** the AI generates tests for a doc with numeric range, **Then** the produced tests include the exact boundary values returned by Testimize.
2. **Given** Testimize is enabled and installed, **When** the AI generates tests for a form with three+ independent dropdown fields, **Then** the produced tests use a pairwise covering array (fewer combinations than the full Cartesian product) supplied by Testimize.
3. **Given** Testimize is enabled and installed, **When** the AI generates tests for an email field, **Then** the produced tests include real-world invalid patterns (e.g., `user@domain..com`, `plainaddress`, `@missing.com`) supplied by Testimize, not AI inventions.

---

### User Story 2 - Zero-Friction Default (Disabled) (Priority: P1)

A new SPECTRA user installs SPECTRA without Testimize. They run `spectra init` then `spectra ai generate` and everything works exactly as before — no errors, no warnings about a missing tool, no extra configuration steps. The default `spectra.config.json` contains a `testimize` section with `enabled: false`.

**Why this priority**: This story protects every existing user. The feature must not break or burden anyone who doesn't opt in. It is a P1 because the regression risk dominates the upside; if any default-path user sees a Testimize-related error, the feature is unshippable.

**Independent Test**: On a fresh project, run `spectra init` then `spectra ai generate --suite x --analyze-only`. Verify no Testimize-related errors, no warning messages, and zero child processes spawned for `testimize-mcp`.

**Acceptance Scenarios**:

1. **Given** a fresh SPECTRA project (`testimize.enabled = false`), **When** any generation command runs, **Then** no Testimize MCP process is started and no Testimize-related output appears.
2. **Given** `testimize.enabled = false`, **When** the user runs `spectra ai generate`, **Then** the AI continues to apply spec 035 ISTQB techniques and produces test cases as before.
3. **Given** a default-init project, **When** the user inspects `spectra.config.json`, **Then** the `testimize` section exists with `enabled: false`.

---

### User Story 3 - Graceful Degradation When Tool Is Missing (Priority: P1)

A user enables Testimize in config but has not installed `Testimize.MCP.Server`. When they run `spectra ai generate`, SPECTRA detects the missing tool, prints a warning explaining how to install it, and falls back to AI-only generation. The command succeeds (exit code 0). It does not abort.

**Why this priority**: Without graceful degradation, enabling the feature becomes a footgun. Users who flip the switch without installing the tool would see a hard failure on every generation run. This story makes the opt-in safe.

**Independent Test**: On a project with `testimize.enabled = true` but no `testimize-mcp` installed, run `spectra ai generate --suite x --analyze-only`. Verify exit code 0, a non-fatal warning naming the missing tool, and that generation completes using AI-approximated values.

**Acceptance Scenarios**:

1. **Given** `testimize.enabled = true` and the tool is not installed, **When** generation runs, **Then** a warning is shown, the command exits with code 0, and tests are produced using AI fallback values.
2. **Given** `testimize.enabled = true` and the tool starts but crashes mid-generation, **When** the AI calls the tool, **Then** SPECTRA catches the failure and continues with AI-approximated values for remaining test cases.
3. **Given** `testimize.enabled = true` and the tool returns an empty result for a request, **When** the AI processes the response, **Then** SPECTRA falls back to AI-approximated values without raising an error.
4. **Given** `testimize.enabled = true` and the tool takes longer than the per-call timeout, **When** the timeout elapses, **Then** SPECTRA cancels the call and falls back to AI-approximated values.

---

### User Story 4 - Health Check Command (Priority: P2)

A user wants to verify their Testimize setup before running real generation. They run `spectra testimize check` and see a status report listing whether Testimize is enabled, whether the tool is installed, whether it answers a health probe, the configured mode, the configured strategy, and whether the optional settings file exists. The same command supports `--output-format json` for CI/SKILL consumption.

**Why this priority**: The health check is the user's diagnostic tool. Without it, debugging "why isn't Testimize being called?" requires running a full generation. P2 because Stories 1–3 deliver value without it; this story makes troubleshooting fast.

**Independent Test**: With Testimize installed and enabled, run `spectra testimize check`. Verify the output shows `Enabled: true`, the installed version, `healthy`, the mode, and the strategy. Then run `spectra testimize check --output-format json` and verify it parses as JSON with at least the fields `enabled`, `installed`, `healthy`.

**Acceptance Scenarios**:

1. **Given** Testimize is enabled and installed, **When** the user runs `spectra testimize check`, **Then** the report shows enabled=true, installed=true, healthy=true, plus mode/strategy/settings status.
2. **Given** Testimize is enabled but not installed, **When** the user runs the check, **Then** the report shows installed=false and includes a one-line install instruction.
3. **Given** Testimize is disabled, **When** the user runs the check, **Then** the report shows disabled status and does not attempt to start the MCP process.
4. **Given** any state, **When** the user runs the check with `--output-format json`, **Then** the output is valid JSON containing at minimum `enabled`, `installed`, `healthy` boolean fields.

---

### User Story 5 - Init Detects Installed Tool (Priority: P3)

A user has already installed `Testimize.MCP.Server` globally before initializing SPECTRA. When they run `spectra init`, SPECTRA detects the installed tool and offers to enable Testimize during initialization. If they accept, the generated `spectra.config.json` has `testimize.enabled = true`.

**Why this priority**: Convenience for early adopters. Without it, every user must manually edit config or run `spectra config set` after `init`. P3 because Stories 1–4 work fine without it; this just smooths the on-ramp.

**Independent Test**: Install `Testimize.MCP.Server` globally. Run `spectra init` interactively and accept the Testimize prompt. Verify the resulting `spectra.config.json` has `testimize.enabled = true`.

**Acceptance Scenarios**:

1. **Given** `Testimize.MCP.Server` is installed globally, **When** the user runs `spectra init` interactively, **Then** they are prompted whether to enable Testimize.
2. **Given** the user accepts the prompt, **When** init writes the config, **Then** `testimize.enabled` is set to true.
3. **Given** the user declines (or runs with `--no-interaction`), **When** init writes the config, **Then** `testimize.enabled` is left at false.
4. **Given** Testimize is not installed, **When** the user runs `spectra init`, **Then** no Testimize prompt appears and the config has `testimize.enabled = false`.

---

### User Story 6 - Re-init Preserves Existing Settings (Priority: P3)

A user has a project with custom Testimize settings (`enabled: true`, custom strategy, custom ABC settings). They re-run `spectra init` after upgrading SPECTRA. Their existing `testimize` section is preserved unchanged.

**Why this priority**: Protects power users from losing their tuning when they re-run init. Pairs naturally with the existing init-preservation behavior for other config sections.

**Independent Test**: In a project with a customized `testimize` section in `spectra.config.json`, run `spectra init`. Verify the section is unchanged byte-for-byte.

**Acceptance Scenarios**:

1. **Given** an existing `testimize` section with custom values, **When** the user re-runs `spectra init`, **Then** the existing section is preserved unchanged.

---

### Edge Cases

- **Testimize tool installed but a different/incompatible version**: SPECTRA does not pin a version. Health check reports the version it sees; generation uses whatever the installed tool exposes. If the tool's MCP API is incompatible, the call fails and SPECTRA falls back to AI values.
- **Multiple concurrent generation runs**: Each generation run starts and disposes its own MCP client. No shared global state.
- **Generation cancelled by Ctrl-C**: The MCP client is disposed in a `finally` so the child process is killed even on cancellation.
- **Generation throws after MCP client started**: Same — `finally` block ensures disposal.
- **Settings file path is set but file does not exist**: Health check warns "not found"; generation proceeds without the custom settings (Testimize uses its built-in defaults).
- **AI calls Testimize but Testimize returns categories SPECTRA does not understand**: Unknown categories are passed through to test titles as-is; they do not crash the generator.
- **Pre-existing user-edited prompt template lacks the `{{#if testimize_enabled}}` block**: The conditional prompt section simply isn't included. AI still applies spec 035 techniques. No error.
- **`mcp.command` resolves to a path with spaces**: Process is started with proper argument quoting on the host OS.

## Requirements *(mandatory)*

### Functional Requirements

#### Configuration

- **FR-001**: A new `testimize` section in `spectra.config.json` MUST exist with at minimum the fields `enabled`, `mode`, `strategy`, `settings_file`, `mcp.command`, `mcp.args`, and `abc_settings`.
- **FR-002**: The `testimize.enabled` field MUST default to `false`.
- **FR-003**: The `testimize.mode` field MUST default to `"exploratory"` and accept `"exploratory"` or `"precise"`.
- **FR-004**: The `testimize.strategy` field MUST default to `"HybridArtificialBeeColony"` and accept the values `"Pairwise"`, `"Combinatorial"`, `"HybridArtificialBeeColony"`, `"PairwiseOptimized"`, `"CombinatorialOptimized"`.
- **FR-005**: The `testimize.mcp.command` field MUST default to `"testimize-mcp"` and the `mcp.args` field MUST default to `["--mcp"]`.
- **FR-006**: A config file with no `testimize` section at all MUST load successfully with all fields at their defaults (`enabled = false`).
- **FR-007**: An unknown strategy value MUST fall back to the default strategy without raising an error.
- **FR-008**: ABC algorithm settings (`total_population_generations`, `mutation_rate`, `final_population_selection_ratio`, `elite_selection_ratio`, `allow_multiple_invalid_inputs`, `seed`) MUST round-trip through config load/save when present.
- **FR-009**: A null `seed` MUST mean non-deterministic generation; a fixed integer seed MUST be forwarded to the Testimize tool unchanged.

#### MCP Client Lifecycle

- **FR-010**: When `testimize.enabled = true`, SPECTRA MUST attempt to start the Testimize MCP process using `mcp.command` + `mcp.args` once per generation run.
- **FR-011**: If the MCP process cannot be started (tool not installed, exec failure), SPECTRA MUST log a non-fatal warning explaining how to install the tool and continue with AI-only generation.
- **FR-012**: If the MCP process starts but a health probe fails, SPECTRA MUST behave as if the tool were not installed (warning + fallback).
- **FR-013**: If a tool call to the MCP server takes longer than a configured per-call timeout, SPECTRA MUST cancel the call and fall back to AI-approximated values for that request.
- **FR-014**: If the MCP process crashes mid-generation, SPECTRA MUST catch the failure, log a warning, and continue producing remaining test cases using AI-approximated values.
- **FR-015**: SPECTRA MUST dispose the MCP client (kill the child process) at the end of every generation run, including on exception or cancellation.

#### AI Tool Integration

- **FR-016**: When the MCP server is healthy, SPECTRA MUST register two additional AI tools alongside the existing generation tools: one to generate optimized test data (BVA/EP/pairwise/ABC) and one to extract field specifications from a text snippet.
- **FR-017**: When `testimize.enabled = false` or the MCP server is unhealthy, SPECTRA MUST NOT register the Testimize tools.
- **FR-018**: The Testimize tool descriptions exposed to the AI MUST mention the techniques they implement (boundary values, equivalence classes, pairwise) so the model can decide when to call them.
- **FR-019**: The Testimize tool MUST accept input field specifications including type, name, optional min, optional max, optional explicit valid/invalid value lists, and optional error-message strings, and return optimized value combinations grouped by category.

#### Prompt Templates

- **FR-020**: The behavior-analysis and test-generation prompt templates MUST include conditional sections that are rendered only when Testimize is enabled, instructing the AI to call the Testimize tools and use the returned values verbatim.
- **FR-021**: The prompt template engine MUST support a `testimize_enabled` placeholder whose value is non-empty (truthy) when `testimize.enabled = true` and empty otherwise.
- **FR-022**: When Testimize is disabled, the conditional sections MUST NOT appear in the rendered prompt and the prompt MUST behave exactly as it did before this feature.

#### Init Flow

- **FR-023**: `spectra init` MUST write a `testimize` section with `enabled = false` to a freshly created `spectra.config.json`.
- **FR-024**: `spectra init` MUST detect whether `Testimize.MCP.Server` is installed in the global tool location and, in interactive mode, MUST offer to enable Testimize when it is detected.
- **FR-025**: Re-running `spectra init` against a project that already has a `testimize` section MUST preserve the existing section unchanged.

#### Health Check Command

- **FR-026**: A new CLI command `spectra testimize check` MUST report whether Testimize is enabled, whether the tool is installed, whether the running server passes a health probe, the configured mode, the configured strategy, and whether the optional settings file exists.
- **FR-027**: `spectra testimize check` MUST support `--output-format json` and emit a JSON object containing at minimum `enabled`, `installed`, and `healthy` boolean fields.
- **FR-028**: When Testimize is disabled, `spectra testimize check` MUST NOT attempt to start the MCP process.
- **FR-029**: When the tool is not installed, `spectra testimize check` MUST include a one-line install instruction in its output.

#### Category Mapping

- **FR-030**: When the AI uses Testimize-supplied test data, the resulting test cases MUST map Testimize value categories (`Valid`, `BoundaryValid`, `BoundaryInvalid`, `Invalid`) to SPECTRA categories so the existing analysis breakdown reflects them: `Valid → happy_path`, `BoundaryValid` and `BoundaryInvalid → boundary`, `Invalid → negative` or `error_handling`.

#### Scope

- **FR-031**: SPECTRA MUST NOT take a NuGet dependency on any Testimize package. The integration MUST be exclusively over the MCP protocol.
- **FR-032**: This feature MUST only affect `spectra ai generate`. `spectra ai update` and other commands MUST NOT call Testimize.

### Key Entities

- **Testimize Configuration**: A new section under the project's SPECTRA config that controls whether the integration is active and how it behaves. Fields: enabled flag, mode, strategy, optional settings file path, MCP command + args, optional ABC algorithm tuning.
- **MCP Client**: An in-memory wrapper around the Testimize child process. Owns process startup, health probing, JSON-RPC tool calls, and disposal.
- **Testimize AI Tools**: Two AI-callable tools exposed to the generation model when Testimize is healthy. One generates optimized test data from field specs; one extracts field specs from a text snippet.
- **Testimize Settings File**: Optional Testimize-native JSON file (`testimizeSettings.json`) that overrides per-input-type equivalence classes (e.g., custom invalid email patterns). Loaded by the Testimize server, not by SPECTRA.

## Assumptions

- The Testimize MCP Server is published as a standalone .NET global tool with a stdio MCP mode. SPECTRA does not bundle it.
- Testimize value categories are stable strings (`Valid`, `BoundaryValid`, `BoundaryInvalid`, `Invalid`) that do not change between Testimize versions.
- A 30-second per-call timeout is acceptable for Testimize tool calls. Generation latency is dominated by the AI provider, not Testimize.
- The existing 7 generation AI tools (documentation reading, index, dedupe, ID allocation) are unchanged by this feature; Testimize tools are added alongside them.
- The behavior-analysis and test-generation prompt templates already support conditional `{{#if}}` blocks (as of spec 030/035). This feature reuses that mechanism.
- Existing user-edited prompt templates that lack the new conditional block will still work — they simply will not opt in to Testimize-aware instructions until the user resets to the new defaults.
- This feature does not introduce any state that needs to migrate; all changes are additive on disk (one new config section, one new prompt block).
- The integration applies only to `spectra ai generate`. Other commands (`update`, `analyze`, `dashboard`) do not need Testimize.
- "Mathematically optimal" in the context of this spec means the boundary set, pairwise covering array, or ABC-optimized test set produced by the Testimize algorithms, accepted as authoritative by SPECTRA without further filtering.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With Testimize enabled, for any documentation containing a numeric range, the produced test cases include the exact min, max, min−1, and max+1 values verbatim in step text in 100% of cases.
- **SC-002**: With Testimize enabled, for any documentation containing 3+ independent dropdown/parameter fields, the produced test cases use a pairwise covering array whose count is strictly less than the full Cartesian product.
- **SC-003**: With Testimize disabled (the default), generation behavior is byte-identical to the pre-feature behavior on the same inputs (same prompts rendered, same AI tool list, no MCP child process spawned).
- **SC-004**: When Testimize is enabled but not installed, `spectra ai generate` exits with code 0 and produces test cases via the AI fallback in 100% of runs.
- **SC-005**: When Testimize crashes mid-generation, the generation run still completes successfully with the partial Testimize data plus AI fallback for remaining requests, in 100% of seeded fault-injection scenarios.
- **SC-006**: The MCP child process is terminated before the SPECTRA CLI exits in 100% of runs, including normal completion, exception, and Ctrl-C cancellation.
- **SC-007**: `spectra testimize check` returns a result within 5 seconds in 100% of runs, regardless of whether the tool is installed.
- **SC-008**: `spectra testimize check --output-format json` produces output that parses as valid JSON containing the fields `enabled`, `installed`, `healthy` in 100% of runs.
- **SC-009**: After enabling Testimize on a representative form-validation suite (multiple fields, ranges, email/phone), the count of generated tests classified as `boundary` increases by at least 50% compared to a Testimize-disabled run on the same documentation.
- **SC-010**: `spectra init` writes a `testimize` section with `enabled: false` in 100% of fresh-project initializations; re-running `init` against a project with a customized `testimize` section preserves it byte-for-byte in 100% of cases.
- **SC-011**: The project test suite gains at least 20 net new tests covering the new configuration model, MCP client lifecycle, tool registration, prompt rendering, health check command, and graceful-degradation paths, with all existing tests still passing.
