# Quickstart: Generation Session Flow

## Interactive Session

```bash
spectra ai generate --suite checkout
# Phase 1: Analyzes docs, shows behavior breakdown
# Phase 2: Generates recommended tests
# Phase 3: Shows suggestions for uncovered areas
# Phase 4: Optionally describe your own tests
# Loops until you choose "Done"
```

## Non-Interactive (CI/SKILL)

```bash
# Full auto session
spectra ai generate --suite checkout --auto-complete --output-format json

# Generate from previous suggestions
spectra ai generate --suite checkout --from-suggestions --output-format json

# Generate specific suggestions (by index)
spectra ai generate --suite checkout --from-suggestions 1,3 --output-format json

# Create test from description
spectra ai generate --suite checkout \
  --from-description "When user enters invalid IBAN, show error" \
  --context "Bank transfer payment, checkout page" \
  --output-format json
```

## Key Behaviors

1. Sessions persist for 1 hour in `.spectra/session.json`
2. User-described tests get `grounding.verdict: manual`
3. Duplicate detection warns when >80% title similarity found
4. `--auto-complete` = analyze + generate + accept all suggestions
