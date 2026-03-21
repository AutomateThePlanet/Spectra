# Quickstart: CLI UX Improvements

**Feature**: 013-cli-ux-improvements | **Date**: 2026-03-21

## Implementation Order

Follow this order for incremental, testable progress:

### Step 1: NextStepHints utility (P1, standalone)

1. Create `src/Spectra.CLI/Output/NextStepHints.cs`:
   - Static `Dictionary<string, HintSet>` with entries for each command
   - `Render(string commandName, bool success, VerbosityLevel verbosity, IAnsiConsole console)` method
   - Check `verbosity >= VerbosityLevel.Normal` before rendering
   - Use `[dim]` Spectre.Console markup for gray text
   - Print "Next steps:" header, then 2-3 hints with command + comment

2. Create `tests/Spectra.CLI.Tests/Output/NextStepHintsTests.cs`:
   - Test each command's success/failure hint set
   - Test quiet suppression (`VerbosityLevel.Quiet`)
   - Test rendering format

3. Wire `NextStepHints.Render()` into each command handler's return path:
   - `InitHandler`, `GenerateHandler`, `UpdateHandler`, `DashboardHandler`, `AnalyzeHandler`, `ValidateHandler`, `DocsIndexHandler`, `ConfigHandler`

### Step 2: Config automation dir subcommands (P2, standalone)

1. Create three new command classes under `Commands/Config/`:
   - `AddAutomationDirCommand.cs` — subcommand of `ConfigCommand`
   - `RemoveAutomationDirCommand.cs` — subcommand of `ConfigCommand`
   - `ListAutomationDirsCommand.cs` — subcommand of `ConfigCommand`

2. Implement handlers in `ConfigHandler.cs` (or separate handler classes):
   - Read `spectra.config.json` as `JsonDocument`
   - Navigate to `coverage.automation_dirs` array
   - Add/remove/list entries
   - Write back with `JsonSerializerOptions { WriteIndented = true }`

3. Register subcommands in `ConfigCommand` constructor

4. Test: `tests/Spectra.CLI.Tests/Commands/Config/AutomationDirCommandTests.cs`

### Step 3: Init automation dirs prompt (P2, depends on Step 2)

1. In `InitHandler.cs`, after existing directory setup:
   - Add Spectre.Console `TextPrompt` for comma-separated paths
   - Skip if `isInteractive == false`
   - Parse input, trim whitespace, write to config JSON

2. Test: Update `InitHandlerTests.cs` with new scenarios

### Step 4: Init critic model prompt (P3, standalone)

1. In `InitHandler.cs`, after AI provider setup:
   - Add `SelectionPrompt` for "Enable grounding verification?"
   - If yes, `SelectionPrompt` for provider choice
   - `TextPrompt` for API key env var (with default from `CriticConfig.GetDefaultApiKeyEnv()`)
   - Check if env var is set, warn if not
   - Write `ai.critic` section to config JSON
   - Skip all if `isInteractive == false`

2. Test: Update `InitHandlerTests.cs`

### Step 5: Interactive continuation menu (P4, standalone)

1. Create `src/Spectra.CLI/Interactive/ContinuationSelector.cs`:
   - Pattern after `GapSelector.cs`
   - `SelectionPrompt` with 4 choices
   - Return `ContinuationResult` record

2. Modify `GenerateHandler.ExecuteInteractiveModeAsync()`:
   - Wrap existing flow in `do { ... } while (continuation != Exit)` loop
   - After session completes, call `ContinuationSelector.Prompt()`
   - On GenerateMore: reset session with same suite, re-enter
   - On SwitchSuite: call `SuiteSelector`, reset session with new suite
   - On CreateSuite: prompt name, create dir, reset session
   - On Exit: break loop

3. Test: `tests/Spectra.CLI.Tests/Interactive/ContinuationSelectorTests.cs`

### Step 6: Critic pipeline verification (P3, investigation)

1. Run `spectra ai generate` with a valid critic config
2. Confirm grounding verdicts appear in output
3. If broken, trace through `GenerateHandler.VerifyTestsAsync()` → `CriticFactory.TryCreate()` → `CopilotCritic.VerifyTestAsync()` to find the issue
4. Fix if needed

## Key Patterns to Follow

- **Command registration**: See `AiCommand.cs` for parent + subcommand pattern
- **Spectre.Console prompts**: See `InitHandler.cs` lines 102-200 for SelectionPrompt/TextPrompt
- **Config modification**: See `ConfigHandler.SetConfigValueAsync()` for JSON read-modify-write
- **Interactive selectors**: See `GapSelector.cs` for IAnsiConsole-injected selector pattern
- **Progress output**: See `ProgressReporter.cs` for `.Success()`, `.Warning()`, `.Info()` methods

## Build & Test

```bash
dotnet build
dotnet test
dotnet run --project src/Spectra.CLI -- config list-automation-dirs
dotnet run --project src/Spectra.CLI -- init
```
