# Tasks: Testimize Integration

**Feature**: 038-testimize-integration
**Branch**: `038-testimize-integration`
**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Data Model**: [data-model.md](./data-model.md)

> Tests are included alongside implementation tasks because the spec's success criteria (SC-011) require ≥20 net new tests, and the existing repo follows xUnit discipline.
>
> **MVP** = Phase 2 + Phase 3 (US1) + Phase 4 (US2) + Phase 5 (US3). Phase 4 is in MVP because US2 (zero-friction default) is the regression-protection P1 — if it breaks, the feature can't ship at all.

---

## Phase 1: Setup

No project initialization needed — feature lives entirely inside the existing solution.

---

## Phase 2: Foundational (blocking prerequisites)

These tasks must complete before any user-story phase, because every story depends on the new config model existing and being deserializable.

- [X] T001 Create `src/Spectra.Core/Models/Config/TestimizeConfig.cs` with classes `TestimizeConfig` (Enabled/Mode/Strategy/SettingsFile/Mcp/AbcSettings), `TestimizeMcpConfig` (Command/Args), and `TestimizeAbcSettings` (TotalPopulationGenerations/MutationRate/FinalPopulationSelectionRatio/EliteSelectionRatio/AllowMultipleInvalidInputs/Seed) per data-model.md, with `[JsonPropertyName]` attributes matching the spec
- [X] T002 Add `Testimize` property (`TestimizeConfig`, default `new()`, `[JsonPropertyName("testimize")]`) to `src/Spectra.Core/Models/Config/SpectraConfig.cs`
- [X] T003 [P] Verify `SpectraConfig.Default` (or equivalent factory) initializes `Testimize` to a default `TestimizeConfig` so existing config files without the section deserialize cleanly

---

## Phase 3: User Story 1 — Mathematically Optimal Boundary Values (P1)

**Goal**: With Testimize enabled and the MCP server running, the AI generation pipeline gains two extra tools (`GenerateTestData`, `AnalyzeFieldSpec`) and uses them to obtain algorithmically optimal test values.

**Independent test**: With `testimize-mcp` installed and `testimize.enabled=true`, run `spectra ai generate --suite signup` against a fixture document with a numeric range. Verify the generated test files contain the exact boundary values returned by Testimize, and that the technique-aware prompt block was rendered.

**Depends on**: Phase 2 (T001, T002).

### Implementation

- [X] T004 [US1] Create `src/Spectra.CLI/Agent/Testimize/TestimizeMcpClient.cs` — `IAsyncDisposable` wrapper with `StartAsync(TestimizeMcpConfig, CancellationToken)`, `IsHealthyAsync(CancellationToken)` (5s timeout), `CallToolAsync(string, JsonElement, CancellationToken)` (30s timeout, returns null on any failure), `DisposeAsync` (kills child process, idempotent)
- [X] T005 [US1] Implement child-process spawning in `TestimizeMcpClient.StartAsync` using `System.Diagnostics.Process` with stdio redirection; return false (no exception) on `Win32Exception`/file-not-found/immediate exit
- [X] T006 [US1] Implement minimal JSON-RPC framing for `CallToolAsync` in `TestimizeMcpClient` — write request with `Content-Length` header, read response, parse JSON, return `JsonElement?`; catch all exceptions and return null
- [X] T007 [US1] Create `src/Spectra.CLI/Agent/Testimize/TestimizeTools.cs` with `CreateGenerateTestDataTool(TestimizeMcpClient, TestimizeConfig)` factory using `AIFunctionFactory.Create`, schema matching `contracts/generate-test-data.tool.json` (description must mention "boundary values", "equivalence classes", "pairwise" per FR-018)
- [X] T008 [US1] In `TestimizeTools.CreateGenerateTestDataTool`, map the `strategy` parameter (or fall back to `config.Strategy`) to the appropriate Testimize MCP method (`generate_hybrid_test_cases`, `generate_pairwise_test_cases`, `generate_combinatorial_test_cases`); unknown strategy → use default per FR-007
- [X] T009 [US1] Add `CreateAnalyzeFieldSpecTool()` factory in `TestimizeTools.cs` — local-only `AIFunction` (no MCP call) that parses a text snippet and extracts field specs heuristically (regex for "X to Y characters", "between X and Y", "valid email", "required field")
- [X] T010 [US1] In `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs` after the existing `var tools = ...ToList()` line (~94), add a `try { if (config.Testimize.Enabled) { ... start client, probe health, AddRange Testimize tools ... } }` block; store the client in a local for disposal
- [X] T011 [US1] In `GenerationAgent.GenerateTestsAsync`, wrap the post-Testimize-startup logic in `try/finally` so the `TestimizeMcpClient` is `await using`-disposed before the method returns (success, exception, or cancellation paths) — satisfies FR-015 and SC-006
- [X] T012 [US1] In `GenerationAgent`, when Testimize starts successfully emit `_onStatus?.Invoke("Testimize connected — using algorithmic test data optimization")`; when it fails to start, emit `"Testimize not available — falling back to AI-generated test values"` (FR-011)
- [X] T013 [US1] Modify `src/Spectra.CLI/Prompts/Content/behavior-analysis.md` to add a `{{#if testimize_enabled}}` block titled "ALGORITHMIC TEST DATA (Testimize Available)" instructing the AI to call `AnalyzeFieldSpec` then `GenerateTestData` and use exact returned values
- [X] T014 [US1] Modify `src/Spectra.CLI/Prompts/Content/test-generation.md` to add a `{{#if testimize_enabled}}` block titled "USING TESTIMIZE-GENERATED VALUES" instructing the AI to use exact Testimize values verbatim
- [X] T015 [US1] In `src/Spectra.CLI/Prompts/PromptTemplateLoader.cs`, populate the `testimize_enabled` placeholder as `"true"` when `config.Testimize.Enabled` is true and `""` otherwise; thread the `SpectraConfig` through if not already available at the call site
- [X] T016 [US1] In `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs` and `GenerationAgent.cs`, ensure the `testimize_enabled` placeholder value is included in both `Dictionary<string,string>` value bags passed to `PromptTemplateLoader.Resolve`

### Tests

- [X] T017 [P] [US1] Add `tests/Spectra.CLI.Tests/Agent/Testimize/TestimizeMcpClientTests.cs` covering: `StartAsync` returns false when command not on PATH, `IsHealthyAsync` returns false on no-server, `DisposeAsync` is safe to call twice
- [X] T018 [P] [US1] Add `tests/Spectra.CLI.Tests/Agent/Testimize/TestimizeToolsTests.cs` covering: `CreateGenerateTestDataTool` produces an `AIFunction` whose description contains "boundary values", "equivalence classes", "pairwise"; `CreateAnalyzeFieldSpecTool` is non-null
- [X] T019 [P] [US1] Add `tests/Spectra.CLI.Tests/Agent/Testimize/AnalyzeFieldSpecTests.cs` covering the local heuristic extractor: "accepts 3-20 characters" → text/3/20, "between 18 and 100" → integer/18/100, "valid email address" → email, "Click the button" → empty
- [X] T020 [P] [US1] Add `tests/Spectra.CLI.Tests/Prompts/TestimizeConditionalBlockTests.cs` covering: `{{#if testimize_enabled}}` renders when value is `"true"`, hidden when value is `""`; rendered block contains "GenerateTestData"/"AnalyzeFieldSpec" (behavior-analysis) and "EXACT values from Testimize" (test-generation)

**Checkpoint**: After Phase 3, generation with Testimize enabled and installed produces test cases using algorithmic boundary values. Story is independently shippable.

---

## Phase 4: User Story 2 — Zero-Friction Default (Disabled) (P1, regression protection)

**Goal**: A fresh project with default config (`testimize.enabled = false`) behaves byte-identically to pre-feature SPECTRA — no MCP processes spawned, no extra output, no extra tools.

**Independent test**: On a fresh project, run `spectra init` then `spectra ai generate --analyze-only`. Verify `testimize.enabled = false` in the written config, no `testimize-mcp` process is spawned, and the rendered prompt has no `testimize_enabled` block.

**Depends on**: Phase 2 (T001, T002), Phase 3 (T010 — the conditional `if (config.Testimize.Enabled)` is the regression protection).

### Implementation

- [X] T021 [US2] In `src/Spectra.CLI/Commands/Init/InitHandler.cs`, ensure the default `spectra.config.json` written by `spectra init` includes the `testimize` section with `enabled: false`, `mode: "exploratory"`, `strategy: "HybridArtificialBeeColony"` and no `abc_settings` block (FR-023)

### Tests

- [X] T022 [P] [US2] Add `tests/Spectra.Core.Tests/Models/Config/TestimizeConfigTests.cs` with cases: default-constructed `TestimizeConfig` has `Enabled = false`; `JsonSerializer.Deserialize<SpectraConfig>("{}")` produces non-null `Testimize` with `Enabled = false`; full JSON with all ABC settings round-trips
- [X] T023 [P] [US2] Add `tests/Spectra.CLI.Tests/Agent/Copilot/GenerationAgentTestimizeDisabledTests.cs` with cases: with `Testimize.Enabled = false`, `GenerateTestsAsync` does NOT call `TestimizeMcpClient.StartAsync` (verify via no child process or via a stub client); the rendered prompt does NOT contain "Testimize" or "GenerateTestData"
- [X] T024 [P] [US2] Add a regression test `tests/Spectra.CLI.Tests/Commands/Init/InitTestimizeDefaultTests.cs` asserting that `spectra init` writes `testimize.enabled = false` and the section is present in the resulting config file

**Checkpoint**: After Phase 4, regression protection is in place. Existing users see zero behavioral change.

---

## Phase 5: User Story 3 — Graceful Degradation When Tool Is Missing (P1)

**Goal**: When `testimize.enabled = true` but the tool is missing, crashes, returns garbage, or times out, generation continues with AI fallback values and exits with code 0.

**Independent test**: With `testimize.enabled = true` and `testimize-mcp` not on PATH, run `spectra ai generate --suite x --analyze-only`. Verify exit code 0, a non-fatal warning, and successful generation.

**Depends on**: Phase 3 (T004–T012) — the graceful paths live inside `TestimizeMcpClient` and `GenerationAgent`.

### Implementation

- [X] T025 [US3] In `TestimizeMcpClient.CallToolAsync`, wrap the JSON-RPC round trip in `try/catch (Exception)` returning null on any failure (process-died, IO, JSON parse, timeout); never propagate exceptions to the AI runtime
- [X] T026 [US3] In `TestimizeMcpClient.CallToolAsync`, enforce a 30-second timeout via a linked `CancellationTokenSource` and treat timeout as just another null return (FR-013)
- [X] T027 [US3] In `TestimizeTools.CreateGenerateTestDataTool`, when the underlying `CallToolAsync` returns null, return a JSON object indicating "no data — fall back to AI values" so the AI sees a tool result it can interpret as "use your own approximation"
- [X] T028 [US3] In `GenerationAgent.GenerateTestsAsync`, if `TestimizeMcpClient.StartAsync` returns false OR `IsHealthyAsync` returns false, log the warning, skip the tool registration, and continue normally — generation proceeds with the existing 7 tools (FR-011, FR-012)

### Tests

- [X] T029 [P] [US3] Add `tests/Spectra.CLI.Tests/Agent/Testimize/TestimizeMcpClientGracefulTests.cs` covering: `StartAsync` with bogus command name returns false (no exception); `CallToolAsync` after a closed pipe returns null; `DisposeAsync` after `StartAsync` failure is safe
- [X] T030 [P] [US3] Add a test asserting `CallToolAsync` honours the 30s timeout — use a stub child that hangs and verify cancellation kicks in within ~31s
- [X] T031 [P] [US3] Add `tests/Spectra.CLI.Tests/Agent/Copilot/GenerationAgentGracefulTests.cs` with cases: enabled + not installed → 7 tools registered, warning emitted, exit success; enabled + healthy + tool returns null → AI fallback path

**Checkpoint**: After Phase 5, enabling Testimize is safe even on machines that don't have it installed. Stories US1+US2+US3 form the MVP.

---

## Phase 6: User Story 4 — Health Check Command (P2)

**Goal**: A new `spectra testimize check` command reports the integration status (enabled, installed, healthy, mode, strategy, settings file) for human and JSON consumers.

**Independent test**: Run `spectra testimize check` in three states (disabled / enabled-not-installed / enabled-installed-healthy) and verify each produces the expected report. Run with `--output-format json` and verify the JSON contains `enabled`, `installed`, `healthy`.

**Depends on**: Phase 2 (T001), Phase 3 (T004 — uses `TestimizeMcpClient`).

### Implementation

- [X] T032 [US4] Create `src/Spectra.CLI/Results/TestimizeCheckResult.cs` matching `contracts/testimize-check-result.schema.json` (Enabled/Installed/Healthy/Mode/Strategy/SettingsFile/SettingsFileFound/Version/InstallCommand)
- [X] T033 [US4] Create `src/Spectra.CLI/Commands/Testimize/TestimizeCheckHandler.cs` that reads the SpectraConfig, short-circuits when `Enabled = false` (skip MCP startup per FR-028), otherwise spawns the MCP client, runs the health probe, populates a `TestimizeCheckResult`, and renders human or JSON output via existing presenters
- [X] T034 [US4] In `TestimizeCheckHandler`, when `Installed = false`, populate `InstallCommand = "dotnet tool install --global Testimize.MCP.Server"` (FR-029)
- [X] T035 [US4] Create `src/Spectra.CLI/Commands/Testimize/TestimizeCommandBuilder.cs` (or inline in `Program.cs`) that wires `spectra testimize check` as a System.CommandLine subcommand, exposing `--output-format` and `--verbosity` global options
- [X] T036 [US4] In `src/Spectra.CLI/Program.cs`, register the new `spectra testimize` parent command with the `check` subcommand

### Tests

- [X] T037 [P] [US4] Add `tests/Spectra.CLI.Tests/Commands/Testimize/TestimizeCheckHandlerTests.cs` with cases: disabled → result has `Enabled=false`, `Installed=false`, no MCP startup attempted; enabled + bogus command → `Installed=false`, `InstallCommand` set; JSON output contains the three required fields

**Checkpoint**: After Phase 6, users have a fast diagnostic for their Testimize setup.

---

## Phase 7: User Story 5 — Init Detects Installed Tool (P3)

**Goal**: `spectra init` shells out to `dotnet tool list -g`, detects `Testimize.MCP.Server`, and offers to enable Testimize in interactive mode.

**Independent test**: Install `Testimize.MCP.Server` globally. Run `spectra init` interactively and accept the Testimize prompt. Verify the resulting config has `enabled=true`. Run `spectra init --no-interaction` and verify `enabled=false`.

**Depends on**: Phase 2 (T001), Phase 4 (T021 — the default disabled section).

### Implementation

- [X] T038 [US5] In `src/Spectra.CLI/Commands/Init/InitHandler.cs`, add a private helper `IsTestimizeMcpInstalledAsync()` that runs `dotnet tool list -g`, parses stdout, and returns true if `testimize.mcp.server` appears (case-insensitive); on any failure (no `dotnet`, timeout, parse error) return false
- [X] T039 [US5] In `InitHandler`, after the existing interactive prompts, add a Testimize prompt — only when `IsTestimizeMcpInstalledAsync()` returned true AND the run is interactive (not `--no-interaction`); on accept, set `testimize.enabled = true` in the config being written

### Tests

- [X] T040 [P] [US5] Add `tests/Spectra.CLI.Tests/Commands/Init/InitTestimizeDetectionTests.cs` covering: `--no-interaction` never prompts and never sets enabled=true; the detection helper returns false when `dotnet` is unavailable (mock/stub the process call)

**Checkpoint**: After Phase 7, new users with Testimize already installed get a frictionless on-ramp.

---

## Phase 8: User Story 6 — Re-init Preserves Existing Settings (P3)

**Goal**: Re-running `spectra init` against a project with a customized `testimize` section preserves the section unchanged.

**Independent test**: Manually customize the `testimize` section, re-run `spectra init`, verify the section is byte-identical.

**Depends on**: Phase 4 (T021).

### Implementation

- [X] T041 [US6] In `InitHandler`, when an existing `spectra.config.json` is detected, deserialize it, preserve the existing `Testimize` subtree (do not overwrite even if defaults are different), and re-serialize; if no existing config, write the default disabled section per T021

### Tests

- [X] T042 [P] [US6] Add `tests/Spectra.CLI.Tests/Commands/Init/InitTestimizePreservationTests.cs` covering: existing config with `enabled=true` and custom strategy survives a re-init; existing config with custom ABC settings survives a re-init

**Checkpoint**: After Phase 8, all six user stories are delivered.

---

## Phase 9: Polish & Cross-Cutting

### Documentation

- [X] T043 [P] Create `docs/testimize-integration.md` with prerequisites, setup, how-it-works, configuration reference, modes, strategies, custom equivalence classes, and "without Testimize" sections per the spec
- [X] T044 [P] Update `docs/getting-started.md` with an optional "Enable Testimize" paragraph after the install step
- [X] T045 [P] Update `docs/configuration.md` with a `testimize` section reference (or link out to `testimize-integration.md`)
- [X] T046 [P] Update `docs/cli-reference.md` to add the `spectra testimize check` command entry
- [X] T047 [P] Update `README.md` to mention Testimize in the ecosystem table or feature list

### CLAUDE.md & SKILLs

- [X] T048 Add a "Recent Changes" entry for spec 038 to `CLAUDE.md` summarizing the optional integration, the conditional tool registration, the new command, and the regression-protection guarantees
- [X] T049 [P] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-help.md` to add a Testimize section pointing to `spectra testimize check`

### Build & verification

- [X] T050 Run `dotnet build` and resolve any compile errors
- [X] T051 Run `dotnet test` and resolve any failures (existing tests asserting tool counts may need updating to new "with/without testimize" siblings)
- [X] T052 Manually walk through `quickstart.md` Scenario Z (the regression scenario) on this branch to confirm zero regression for default-disabled users
- [X] T053 Manually walk through `quickstart.md` Scenarios A and B (health check, with and without tool installed) to confirm `spectra testimize check` works

---

## Dependencies

```text
Phase 2 (Foundational)        ← T001, T002, T003
        │
        ▼
Phase 3 (US1, P1)             ← T004–T020
        │
        ├─→ Phase 4 (US2, P1)   ← T021–T024     (regression — needs T010's `if Enabled` gate)
        │
        ├─→ Phase 5 (US3, P1)   ← T025–T031     (graceful — needs T004–T012)
        │
        ├─→ Phase 6 (US4, P2)   ← T032–T037     (health check — needs T004)
        │
        ├─→ Phase 7 (US5, P3)   ← T038–T040     (init detect — needs T021)
        │
        └─→ Phase 8 (US6, P3)   ← T041–T042     (init preserve — needs T021)
                  │
                  ▼
            Phase 9 (Polish)    ← T043–T053
```

US3 (graceful), US4 (health), US5 (init detect), US6 (init preserve) are independent of each other and can be developed in parallel after US1 and US2 land.

## Parallel Execution Examples

**Within Phase 3 (US1) tests** — different files, independent:

```text
T017, T018, T019, T020
```

**Within Phase 9 polish** — all docs are independent files:

```text
T043, T044, T045, T046, T047, T049
```

**Across stories after Phase 5** — Phase 6, Phase 7, Phase 8 can run in parallel by different developers:

```text
US4 (T032–T037) ∥ US5 (T038–T040) ∥ US6 (T041–T042)
```

## Implementation Strategy

- **MVP scope** = Phase 2 + Phase 3 (US1) + Phase 4 (US2) + Phase 5 (US3). After T031, the feature can ship: Testimize works when enabled and installed, breaks nothing when disabled, and degrades gracefully when enabled-but-unavailable. Stop here and ship if time-constrained.
- **Phase 6 (US4 — health check)** should follow immediately because it's the diagnostic users need to debug their setup.
- **Phases 7/8 (US5/US6 — init flow)** are quality-of-life improvements that compound with the MVP but are not blocking.
- **Phase 9 (polish)** ships as part of the same release as the MVP — docs and CLAUDE.md must land together so users can find the new command.

## Independent Test Criteria (per story)

| Story | Criterion |
|-------|-----------|
| US1 (P1) | Generated test files contain exact Testimize boundary values verbatim for a doc with a numeric range |
| US2 (P1) | Fresh `spectra init` writes `testimize.enabled=false`; `spectra ai generate` spawns no `testimize-mcp` child process |
| US3 (P1) | `spectra ai generate` with enabled-but-not-installed exits 0 with a warning and produces tests via AI fallback |
| US4 (P2) | `spectra testimize check` reports correct status in disabled/missing/healthy states; JSON output contains `enabled`/`installed`/`healthy` |
| US5 (P3) | `spectra init` interactive with installed tool offers a Testimize prompt; `--no-interaction` does not |
| US6 (P3) | Re-running `spectra init` against a project with custom `testimize` settings preserves them byte-for-byte |

## Format Validation

All 53 tasks above follow the required checklist format: `- [X] T### [P?] [US#?] description with file path`. Setup/Foundational/Polish tasks omit the `[US#]` label as required.
