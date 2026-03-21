# Data Model: CLI UX Improvements

**Feature**: 013-cli-ux-improvements | **Date**: 2026-03-21

## Entities

### NextStepHint (NEW)

A single hint displayed after a command completes.

| Field | Type | Description |
|-------|------|-------------|
| Command | string | The CLI command to suggest (e.g., `spectra ai generate`) |
| Comment | string | Brief description of what the command does |

### HintSet (NEW)

A collection of hints for a specific command outcome.

| Field | Type | Description |
|-------|------|-------------|
| OnSuccess | NextStepHint[] | Hints to show when command succeeds |
| OnFailure | NextStepHint[] | Hints to show when command fails |

### HintRegistry (NEW, static)

Static mapping from command names to HintSets.

| Key | Type | Description |
|-----|------|-------------|
| CommandName → HintSet | Dictionary<string, HintSet> | Maps command identifiers (e.g., "init", "ai generate", "dashboard") to their hint sets |

### ContinuationAction (NEW, enum)

Action selected from the post-generation continuation menu.

| Value | Description |
|-------|-------------|
| GenerateMore | Generate more tests for the same suite |
| SwitchSuite | Switch to a different existing suite |
| CreateSuite | Create a new suite and continue |
| Exit | End the session |

### ContinuationResult (NEW, record)

Result of the continuation menu selection.

| Field | Type | Description |
|-------|------|-------------|
| Action | ContinuationAction | The action chosen |
| SuiteName | string? | Suite name (for SwitchSuite or CreateSuite) |
| SuitePath | string? | Suite path (for SwitchSuite or CreateSuite) |

## Existing Entities (unchanged)

### CoverageConfig

Already has `AutomationDirs` (`IReadOnlyList<string>`). No changes needed.

### CriticConfig

Already has `Enabled`, `Provider`, `Model`, `ApiKeyEnv`, `BaseUrl`, `TimeoutSeconds`. No changes needed.

### InteractiveSession

No structural changes. The continuation menu is handled outside the session lifecycle — the session is reset/recreated when switching suites.

### VerbosityLevel (existing enum)

Already has `Quiet` value. Hints check `verbosity < VerbosityLevel.Normal` to decide rendering.

## State Transitions

### Interactive Generation with Continuation (modified flow)

```
[Start]
  │
  ▼
SuiteSelection ──► TestTypeSelection ──► FocusInput (optional) ──► GapAnalysis
  │                                                                     │
  │                                                                     ▼
  │                                                              Generating ◄─┐
  │                                                                     │     │
  │                                                                     ▼     │
  │                                                               Results     │
  │                                                                     │     │
  │                                                                     ▼     │
  │                                                            GapSelection ──┘
  │                                                                     │
  │                                                              (Done / No gaps)
  │                                                                     │
  │                                                                     ▼
  │◄─────────────────────────────────────────────────── ContinuationMenu
  │  (GenerateMore / SwitchSuite / CreateSuite)                         │
  │                                                                     │ (Exit)
  │                                                                     ▼
  │                                                                  [End]
```

The `ContinuationMenu` is not a session state — it's a handler-level prompt that decides whether to reset and re-enter the session loop or exit.
