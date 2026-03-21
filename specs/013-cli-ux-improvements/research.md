# Research: CLI UX Improvements

**Feature**: 013-cli-ux-improvements | **Date**: 2026-03-21

## R1: Existing `--quiet` / verbosity mechanism

**Decision**: Reuse existing `--verbosity quiet` (VerbosityLevel.Quiet) rather than adding a new `--quiet` flag.

**Rationale**: `GlobalOptions.cs` already defines `--verbosity` as a global option with `VerbosityLevel.Quiet` as the lowest level. All command handlers already receive `VerbosityLevel`. Adding a separate `--quiet` flag would duplicate existing functionality.

**Alternatives considered**:
- New `--quiet` boolean flag: Redundant; would require documenting two ways to suppress output.
- `-q` alias for `--verbosity quiet`: Possible but adds complexity to option parsing. Could be a future enhancement.

## R2: Existing `spectra config` command capabilities

**Decision**: Extend the existing `ConfigCommand` with subcommands rather than creating a new top-level command.

**Rationale**: `ConfigCommand` already exists at `src/Spectra.CLI/Commands/Config/ConfigCommand.cs` with `config <key> [value]` get/set behavior. System.CommandLine supports parent commands with both a default handler and subcommands. Adding `add-automation-dir`, `remove-automation-dir`, and `list-automation-dirs` as child commands is the idiomatic approach.

**Alternatives considered**:
- New `spectra automation` top-level command: Fragments config management across commands.
- Using `spectra config coverage.automation_dirs` with JSON array syntax: Poor UX for array manipulation.

## R3: CoverageConfig `AutomationDirs` field

**Decision**: The `CoverageConfig` model already has an `AutomationDirs` property (`IReadOnlyList<string>` defaulting to `["tests", "test", "spec", "specs", "e2e"]`). The init prompt and config subcommands will write to this existing field.

**Rationale**: No model changes needed. The field maps to `coverage.automation_dirs` in JSON (via `JsonPropertyName`).

**Alternatives considered**: None — the field already exists.

## R4: CriticConfig structure and init integration

**Decision**: The `CriticConfig` model already has all needed fields: `Enabled`, `Provider`, `Model`, `ApiKeyEnv`, `BaseUrl`, `TimeoutSeconds`. The init wizard writes these fields directly. `CriticConfig.GetEffectiveModel()` and `GetDefaultApiKeyEnv()` provide sensible defaults per provider.

**Rationale**: No model changes needed. The `CriticFactory.TryCreate()` method reads `CriticConfig` to create a `CopilotCritic`. The init wizard just needs to populate the config JSON.

**Alternatives considered**: None — existing model is complete.

## R5: Interactive session continuation architecture

**Decision**: Wrap the existing `ExecuteInteractiveModeAsync` flow in an outer `do-while` loop controlled by a new `ContinuationSelector`. After the session completes (all gaps handled or user done), the continuation selector offers 4 options. "Generate more" and "switch suite" reset the session and re-enter the loop. "Create new suite" creates the directory and re-enters. "Done" exits.

**Rationale**: Minimal changes to the existing state machine. `InteractiveSession` doesn't need new states — it already handles its own generation loop. The continuation is a handler-level concern.

**Alternatives considered**:
- Adding `Continuation` state to `InteractiveSession`: Mixes session-level and generation-level concerns. The session is designed for one suite per lifecycle.
- Creating a new `MultiSuiteSession` wrapper: Over-engineering for this use case.

## R6: Critic pipeline verification

**Decision**: The critic pipeline code exists and appears structurally complete in `GenerateHandler.VerifyTestsAsync()` (lines 832-902) and `CopilotCritic.VerifyTestAsync()`. The `ShouldVerify()` method checks `config.Ai.Critic.Enabled`. During implementation, we will verify this works end-to-end by confirming grounding verdicts appear in console output when a critic is configured.

**Rationale**: The code path is: `GenerateHandler.ExecuteDirectModeAsync()` → `ShouldVerify()` → `VerifyTestsAsync()` → `CriticFactory.TryCreate()` → `CopilotCritic.VerifyTestAsync()`. This chain is complete. If issues exist, they would be runtime configuration problems (missing API key, Copilot SDK unavailability), not code gaps.

**Alternatives considered**: N/A — this is an investigation item, not a design decision.
