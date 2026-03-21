# Implementation Plan: CLI UX Improvements

**Branch**: `013-cli-ux-improvements` | **Date**: 2026-03-21 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/013-cli-ux-improvements/spec.md`

## Summary

Four CLI UX improvements: (1) next-step hints after commands, (2) automation directory config during init + management subcommands, (3) critic model setup during init, (4) interactive mode suite continuation. All changes are in the CLI layer — no core model or MCP changes.

## Technical Context

**Language/Version**: C# 12 / .NET 8+
**Primary Dependencies**: Spectre.Console (terminal UX, prompts, markup), System.CommandLine (CLI framework), System.Text.Json (config read/write)
**Storage**: `spectra.config.json` (file-based config)
**Testing**: xUnit
**Target Platform**: Cross-platform CLI (.NET global tool)
**Project Type**: CLI tool
**Performance Goals**: Hints add <50ms to command execution (SC-006)
**Constraints**: Must respect `--quiet`, `--no-interaction`, `--no-review` flags. No new dependencies.
**Scale/Scope**: ~10 commands affected by hints, 3 new config subcommands, 2 init prompts, 1 interactive loop enhancement

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Config changes go to spectra.config.json (Git-tracked) |
| II. Deterministic Execution | N/A | UX improvements, no execution state changes |
| III. Orchestrator-Agnostic | N/A | CLI-only changes, no MCP/orchestrator impact |
| IV. CLI-First Interface | PASS | All new functionality exposed via CLI commands and flags |
| V. Simplicity (YAGNI) | PASS | Hardcoded hints (not AI-generated), reuses existing Spectre.Console patterns, no new abstractions until needed |

## Project Structure

### Documentation (this feature)

```text
specs/013-cli-ux-improvements/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── data-model.md        # Phase 1 data model (minimal — config changes only)
├── quickstart.md        # Phase 1 quickstart
├── contracts/           # Phase 1 contracts
│   └── cli-commands.md  # New/modified CLI commands
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
└── Spectra.CLI/
    ├── Output/
    │   └── NextStepHints.cs           # New: hint display helper
    ├── Commands/
    │   ├── Init/
    │   │   └── InitHandler.cs         # Update: add automation dir + critic prompts
    │   ├── Config/
    │   │   ├── ConfigCommand.cs       # Update: add automation-dir subcommands
    │   │   └── ConfigHandler.cs       # Update: add automation dir CRUD methods
    │   ├── Generate/
    │   │   └── GenerateHandler.cs     # Update: add hints + interactive continuation
    │   ├── Analyze/
    │   │   └── AnalyzeHandler.cs      # Update: add hints
    │   ├── Dashboard/
    │   │   └── DashboardHandler.cs    # Update: add hints
    │   ├── Validate/
    │   │   └── ValidateHandler.cs     # Update: add hints
    │   ├── Docs/
    │   │   └── DocsIndexHandler.cs    # Update: add hints
    │   └── Index/
    │       └── IndexHandler.cs        # Update: add hints
    └── Infrastructure/
        └── VerbosityLevel.cs          # Existing (no changes — Quiet already suppresses)

tests/
└── Spectra.CLI.Tests/
    ├── Output/
    │   └── NextStepHintsTests.cs      # New: hint logic tests
    ├── Commands/
    │   ├── InitCommandTests.cs        # Update: automation dir + critic prompt tests
    │   └── ConfigCommandTests.cs      # New: automation dir subcommand tests
    └── Integration/
        └── InteractiveGenerationTests.cs  # Update: continuation flow tests
```

**Structure Decision**: All changes modify existing files except for the new `NextStepHints` helper. No new projects or major restructuring. Follows existing patterns (handlers call helper, helper checks verbosity).

## Implementation Phases

### Phase A: NextStepHints Helper (P1 — Hints)

**Goal**: Create a reusable helper that prints context-aware next-step hints.

**Design**:

1. **`NextStepHints.cs`** — Static class with:
   - `Print(string commandName, bool success, VerbosityLevel verbosity, HintContext? context = null)` — main entry point
   - Checks `verbosity >= Normal` and `Console.IsOutputRedirected == false` before printing
   - `HintContext` record with optional fields: `hasAutomationLinks`, `hasGaps`, `suiteCount`, `errorCount`, `outputPath`
   - Each command has a hardcoded hint set selected by `commandName` + `success` + context
   - Hints printed as `AnsiConsole.MarkupLine("[grey]  {hint}[/]")` — dimmed text, indented

2. **Hint content** (hardcoded per command):
   - `init` (success): `spectra ai generate`, `spectra init-profile`
   - `generate` (success): `spectra ai analyze --coverage`, `spectra ai generate` (interactive)
   - `analyze` (success): `spectra dashboard`, `spectra ai analyze --coverage --auto-link` (if no auto-link), `spectra ai generate` (if gaps)
   - `dashboard` (success): `Open {outputPath}/index.html`, deployment guide reference
   - `validate` (success): `spectra ai generate`, `spectra index`
   - `validate` (failure): `Fix errors, then spectra validate`
   - `docs index` (success): `spectra ai generate`

3. **Integration**: Each handler calls `NextStepHints.Print(...)` as the last action before returning exit code.

### Phase B: Init — Automation Directories (P2)

**Goal**: Add automation directory prompt to init and config subcommands.

**Changes**:

1. **`InitHandler.cs`** — After existing setup steps (line ~78), add:
   - Prompt: "Where is your automation test code?" with comma-separated input
   - Parse input, trim whitespace, filter empty strings
   - Write to `coverage.automation_dirs` in config
   - Skip if not interactive or empty input (use defaults)

2. **`ConfigCommand.cs`** — Add three subcommands:
   - `spectra config add-automation-dir <path>` — append to array
   - `spectra config remove-automation-dir <path>` — remove from array
   - `spectra config list-automation-dirs` — list with disk existence check

3. **`ConfigHandler.cs`** — Add methods:
   - `AddAutomationDirAsync(string path)` — read config, append if not duplicate, write back
   - `RemoveAutomationDirAsync(string path)` — read config, remove, write back
   - `ListAutomationDirsAsync()` — read config, print each dir with exists/missing indicator

4. **Config write strategy**: Read entire `spectra.config.json`, parse as `JsonNode`, modify the `coverage.automation_dirs` array, serialize back. This preserves all existing config.

### Phase C: Init — Critic Configuration (P2)

**Goal**: Add critic model setup prompt to init flow.

**Changes**:

1. **`InitHandler.cs`** — After AI provider setup (line ~195), add:
   - Prompt: "Do you want to enable grounding verification?" — Yes (default) / No
   - If Yes: SelectionPrompt for critic provider (google, anthropic, openai, same as primary)
   - Prompt for API key env var (with provider-specific default from `CriticConfig.GetDefaultApiKeyEnv()`)
   - If "Same as primary": copy provider/model from primary config
   - Write `ai.critic` section: `enabled: true`, `provider`, `model` (from `CriticConfig.GetEffectiveModel()`), `api_key_env`
   - Print confirmation with provider/model and `--skip-critic` reminder

2. **Skip when**:
   - Not interactive
   - User chose "Skip" for primary AI provider (no point configuring critic)

### Phase D: Interactive Mode Continuation (P3)

**Goal**: After generation completes for a suite, offer continuation options.

**Changes**:

1. **`GenerateHandler.cs`** — Wrap the existing interactive flow in an outer loop:
   - After `session.Complete()`, instead of returning, show continuation menu
   - Menu options via `SelectionPrompt`:
     1. "Generate more for {suiteName}" → reset session, restart inner loop with gap focus
     2. "Switch to a different suite" → show suite list with counts, user picks, restart
     3. "Create a new suite" → prompt for name, create dir, restart
     4. "Done — exit" → break outer loop
   - Before menu, display remaining gaps if any
   - Track session stats: suites worked on, total tests generated
   - On exit, print session summary

2. **Skip when**: `--no-interaction` flag (existing behavior — exits after first suite)

## Constitution Check — Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Config changes to spectra.config.json |
| II. Deterministic Execution | N/A | UX-only |
| III. Orchestrator-Agnostic | N/A | CLI-only |
| IV. CLI-First Interface | PASS | New subcommands, flags respected, CI-friendly |
| V. Simplicity (YAGNI) | PASS | Hardcoded hints, no dynamic generation, reuses Spectre.Console |

## Complexity Tracking

No constitution violations. All changes use existing patterns (Spectre.Console prompts, System.CommandLine subcommands, JSON config read/write).
