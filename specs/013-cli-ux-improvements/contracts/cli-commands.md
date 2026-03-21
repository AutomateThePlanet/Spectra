# CLI Command Contracts: CLI UX Improvements

**Feature**: 013-cli-ux-improvements | **Date**: 2026-03-21

## New Commands

### `spectra config add-automation-dir <path>`

Add an automation directory to coverage configuration.

| Aspect | Detail |
|--------|--------|
| Parent | `spectra config` |
| Arguments | `path` (required, string) — directory path to add |
| Options | `--verbosity` (global) |
| Exit 0 | Path added to `coverage.automation_dirs` in `spectra.config.json` |
| Exit 0 (idempotent) | Path already exists — prints notice, no modification |
| Exit 1 | `spectra.config.json` not found |
| Output | `✓ Added automation directory: <path>` or `ℹ Path already configured: <path>` |

### `spectra config remove-automation-dir <path>`

Remove an automation directory from coverage configuration.

| Aspect | Detail |
|--------|--------|
| Parent | `spectra config` |
| Arguments | `path` (required, string) — directory path to remove |
| Options | `--verbosity` (global) |
| Exit 0 | Path removed from `coverage.automation_dirs` |
| Exit 1 | `spectra.config.json` not found |
| Exit 1 | Path not found in config — prints warning |
| Output | `✓ Removed automation directory: <path>` or `⚠ Path not found: <path>` |

### `spectra config list-automation-dirs`

List all configured automation directories.

| Aspect | Detail |
|--------|--------|
| Parent | `spectra config` |
| Arguments | None |
| Options | `--verbosity` (global) |
| Exit 0 | Lists directories or shows "No automation directories configured" |
| Output (with dirs) | One path per line, prefixed with `  - ` |
| Output (empty) | `ℹ No automation directories configured.` |

## Modified Commands

### `spectra init` (modified)

Two new interactive prompts added after existing setup steps.

**New prompt 1: Automation directories** (after docs/tests directory setup)
- Prompt: "Where is your automation test code? (for coverage analysis)"
- Input: Comma-separated paths or Enter to skip
- Non-interactive mode: Skipped silently (no automation dirs added)

**New prompt 2: Critic model** (after AI provider setup)
- Prompt: "Do you want to enable grounding verification? (recommended)"
- Selection: Yes → provider list → API key env var; No → skip
- Non-interactive mode: Skipped silently (critic disabled)

### All commands (modified — hint output)

Every command handler adds a hint rendering call before returning. Hints are suppressed when `verbosity < VerbosityLevel.Normal`.

**Hint output format:**
```
  Next steps:
    spectra ai generate           # Generate your first test suite
    spectra ai analyze --coverage # Check coverage gaps
```

Rendered in dimmed/gray Spectre.Console markup (`[dim]`).

## Config File Contract

### `spectra.config.json` — affected sections

**`coverage.automation_dirs`** (existing field, newly populated by init):
```json
{
  "coverage": {
    "automation_dirs": ["../test_automation", "src/IntegrationTests"]
  }
}
```

**`ai.critic`** (existing section, newly populated by init):
```json
{
  "ai": {
    "critic": {
      "enabled": true,
      "provider": "google",
      "model": "gemini-2.0-flash",
      "api_key_env": "GOOGLE_API_KEY",
      "timeout_seconds": 30
    }
  }
}
```
