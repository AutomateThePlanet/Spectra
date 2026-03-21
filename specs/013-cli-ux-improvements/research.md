# Research: CLI UX Improvements

**Date**: 2026-03-21 | **Feature**: 013-cli-ux-improvements

## Finding 1: Existing Terminal UX Patterns

**Decision**: Use Spectre.Console for all new prompts and styled output, matching existing init flow patterns.

**Rationale**: `InitHandler.cs` already uses `AnsiConsole.Prompt(new SelectionPrompt<string>())` for provider selection and `TextPrompt<string>()` for text input. The critic and automation dir prompts should follow the same patterns exactly.

**Alternatives considered**:
- Raw Console.Write — rejected (inconsistent with existing init UX)
- Custom prompt library — rejected (adds dependency, violates YAGNI)

## Finding 2: Hint Suppression Strategy

**Decision**: Suppress hints when `VerbosityLevel < Normal`, when `Console.IsOutputRedirected` is true, or when `--no-interaction` is set.

**Rationale**: `VerbosityLevel.Quiet` already suppresses verbose output throughout the codebase (handlers check `_verbosity >= VerbosityLevel.Normal`). The hint helper follows the same pattern. `Console.IsOutputRedirected` catches piped output scenarios. The `--no-interaction` flag (exists on generate/update) covers CI environments.

**Alternatives considered**:
- Separate `--no-hints` flag — rejected (too granular, `--quiet` or `-v quiet` covers this)
- Check `Environment.GetEnvironmentVariable("CI")` — rejected (fragile, not all CI systems set this)

## Finding 3: Config File Modification Strategy

**Decision**: Use `System.Text.Json.Nodes.JsonNode` to modify spectra.config.json in-place, preserving structure and comments.

**Rationale**: The existing `ConfigHandler` already reads and modifies config via `JsonSerializer.Deserialize<SpectraConfig>()` then serializes back. However, for array operations (add/remove automation dirs), using `JsonNode` allows surgical edits without round-tripping through the full model, which would lose unknown fields or formatting.

**Alternatives considered**:
- Full deserialize → modify → serialize — viable but may lose unknown fields
- String manipulation (regex) — rejected (fragile)
- `JsonDocument` (read-only) — can't modify in place

## Finding 4: Critic Pipeline Verification

**Decision**: The critic pipeline works correctly when configured. No fix needed.

**Rationale**: Research confirmed the full flow:
1. `ShouldVerify()` checks `CriticConfig.Enabled` and `--skip-critic` flag
2. `CriticFactory.TryCreate()` creates a `CopilotCritic`
3. `VerifyTestsAsync()` calls `critic.VerifyTestAsync()` for each test
4. Results filter out hallucinated tests, keep grounded/partial
5. Metadata stored in frontmatter

The reason most users never see it: `CriticConfig.Enabled` defaults to `false`. The init prompt (US3) solves this.

**Alternatives considered**: Adding debug logging to critic pipeline — deferred, not needed for this feature.

## Finding 5: Interactive Mode Loop Architecture

**Decision**: Wrap the existing `ExecuteInteractiveModeAsync` inner loop in an outer suite-switching loop.

**Rationale**: The current flow has a `while (!session.IsComplete)` loop for gap-filling within a single suite. The outer loop adds suite switching after session completion. The `InteractiveSession` can be reset or a new one created for each suite.

**Alternatives considered**:
- Refactor into a separate `MultiSuiteSession` class — rejected (YAGNI, simple loop is sufficient)
- Make a recursive call to `ExecuteInteractiveModeAsync` — rejected (stack depth concerns, harder to track session stats)

## Finding 6: Config Subcommand Architecture

**Decision**: Add `add-automation-dir`, `remove-automation-dir`, `list-automation-dirs` as subcommands of the existing `config` command.

**Rationale**: `spectra config` already exists with `key`/`value`/`--show-all` parameters. Adding subcommands follows System.CommandLine patterns. The config command already has the infrastructure to read/write config files.

**Alternatives considered**:
- Standalone `spectra automation-dirs` command — rejected (fragments the CLI surface)
- Generic `spectra config add-to-array coverage.automation_dirs <value>` — rejected (too low-level for a common operation)
