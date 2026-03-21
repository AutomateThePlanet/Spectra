# Contract: CLI Commands

**Feature**: 013-cli-ux-improvements | **Date**: 2026-03-21

## New Commands

### spectra config add-automation-dir

```
spectra config add-automation-dir <path>
```

**Arguments**: `<path>` — directory path to add (required)
**Exit codes**: 0 = success, 1 = error (no config file)
**Output (success)**: `✓ Added '<path>' to automation directories`
**Output (duplicate)**: `'<path>' is already configured as an automation directory`
**Config effect**: Appends to `coverage.automation_dirs` array

### spectra config remove-automation-dir

```
spectra config remove-automation-dir <path>
```

**Arguments**: `<path>` — directory path to remove (required)
**Exit codes**: 0 = success (or not found with warning), 1 = error (no config file)
**Output (success)**: `✓ Removed '<path>' from automation directories`
**Output (not found)**: `Warning: '<path>' was not found in automation directories`
**Config effect**: Removes from `coverage.automation_dirs` array

### spectra config list-automation-dirs

```
spectra config list-automation-dirs
```

**Exit codes**: 0 = success
**Output format**:
```
Automation directories:
  [exists]  tests
  [exists]  ../test_automation
  [missing] src/IntegrationTests
```

## Modified Commands

### spectra init (interactive mode additions)

**New prompt 1** — After existing setup, before doc index build:
```
◆ Where is your automation test code? (for coverage analysis)
  Enter paths comma-separated, or press Enter for defaults.
  >
```

**New prompt 2** — After AI provider setup:
```
◆ Enable grounding verification? (recommended)
  (1) Yes — configure a critic model  ← default
  (2) No — skip verification
```

If Yes:
```
◆ Select critic provider:
  (1) google (Gemini Flash — recommended)
  (2) anthropic (Claude Haiku)
  (3) openai (GPT-4o-mini)
  (4) Same as primary provider
```

Then:
```
◆ API key environment variable:
  > GOOGLE_API_KEY
```

**Skipped when**: `--no-interaction` flag or non-interactive mode

### spectra ai generate (interactive mode)

**New prompt** — After generation completes for a suite:
```
◆ What would you like to do next?
  (1) Generate more for <suite> (fill gaps)
  (2) Switch to a different suite
  (3) Create a new suite
  (4) Done — exit
```

**Skipped when**: `--no-interaction` flag or direct mode

### spectra dashboard --preview

**Existing** — no changes, but hints are added after output.

## Hint Output Format

All supported commands print hints after primary output:

```
  Next steps:
    spectra ai analyze --coverage     # Check coverage gaps
    spectra ai generate               # Interactive mode
```

**Style**: Dimmed/gray text, indented 4 spaces, preceded by "Next steps:" label
**Suppressed when**: `--quiet` / `-v quiet`, `--no-interaction`, piped output
