# Data Model: CLI UX Improvements

**Feature**: 013-cli-ux-improvements | **Date**: 2026-03-21

## Entities

### HintContext (new)

Contextual data passed to the hint helper to select appropriate suggestions.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| hasAutoLink | bool | false | Whether `--auto-link` was used in the current command |
| hasGaps | bool | false | Whether coverage gaps were detected |
| suiteCount | int | 0 | Number of test suites in the repository |
| errorCount | int | 0 | Number of validation errors found |
| outputPath | string? | null | Dashboard output path (for "open in browser" hint) |
| suiteName | string? | null | Suite that was just generated/updated |

### CoverageConfig (existing — no changes)

Already has `automation_dirs` field (`IReadOnlyList<string>`, default: `["tests", "test", "spec", "specs", "e2e"]`). The config subcommands modify this array in the JSON file directly.

### CriticConfig (existing — no changes)

Already has all needed fields: `enabled`, `provider`, `model`, `api_key_env`, `base_url`, `timeout_seconds`. The init prompt writes to these fields.

## Config Modifications

### automation_dirs Management

The `add-automation-dir` / `remove-automation-dir` commands modify `coverage.automation_dirs` in spectra.config.json:

```json
{
  "coverage": {
    "automation_dirs": ["tests", "../test_automation", "src/IntegrationTests"]
  }
}
```

**Operations**:
- **Add**: Append to array if not already present. Case-sensitive path comparison.
- **Remove**: Remove exact match from array. Warn if not found.
- **List**: Read array, print each entry with `[exists]` or `[missing]` indicator.

### critic Section Written by Init

```json
{
  "ai": {
    "critic": {
      "enabled": true,
      "provider": "google",
      "model": "gemini-2.0-flash",
      "api_key_env": "GOOGLE_API_KEY"
    }
  }
}
```

## Validation Rules

- `automation_dirs` entries are stored as-is (no path normalization). Existence is checked at list-time, not at add-time.
- `critic.provider` must be one of: `"google"`, `"anthropic"`, `"openai"`, `"github-models"`, `"azure-openai"`, `"azure-anthropic"`.
- `critic.model` defaults via `CriticConfig.GetEffectiveModel()` based on provider.
- `critic.api_key_env` defaults via `CriticConfig.GetDefaultApiKeyEnv()` based on provider.
