# Quickstart: CLI Non-Interactive Mode and Structured Output

## What Changed

All SPECTRA CLI commands now support two new global flags:
- `--output-format json` — get structured JSON output instead of formatted text
- `--no-interaction` — fail fast if required arguments are missing (instead of prompting)

## Usage Examples

### SKILL / Copilot Chat Integration
```bash
spectra ai generate checkout --count 10 --output-format json --verbosity quiet
```
Returns JSON with test generation results. No prompts, no spinners.

### CI Pipeline
```bash
spectra validate --output-format json --no-interaction
echo "Exit code: $?"  # 0 = valid, 2 = errors found
```

### Coverage Check in CI
```bash
spectra ai analyze --coverage --output-format json --no-interaction
```

### Human Usage (unchanged)
```bash
spectra ai generate  # Interactive prompts as before
```

## Key Behavior

1. **All args provided** → no prompts, runs directly
2. **Args missing + `--no-interaction`** → exit code 3, error listing missing args
3. **Args missing + no flag** → interactive prompts (existing behavior)
4. **`--output-format json`** → clean JSON on stdout, logs to stderr
