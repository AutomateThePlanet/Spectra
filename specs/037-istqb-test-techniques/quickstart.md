# Quickstart: Verifying ISTQB Technique-Driven Generation

**Feature**: 037-istqb-test-techniques
**Audience**: Reviewer / QA verifying the implementation

## Prerequisites

- A built `spectra` CLI from this branch
- Any project with a `docs/` folder containing at least one document that mentions a numeric range and a multi-condition rule (e.g., the bundled fixtures or a sample like Windows Calculator docs)
- A configured AI provider (any Copilot SDK provider works)

## Scenario A — New project: technique-driven analysis end-to-end

```bash
# 1. Init a fresh project
mkdir scratch && cd scratch
spectra init

# 2. Verify the new templates are present
spectra prompts show behavior-analysis | head -50
# Expect: "TECHNIQUE 1: Equivalence Partitioning (EP)" near the top

# 3. Add a docs file with a numeric range
mkdir -p docs
cat > docs/form.md <<'EOF'
# Username field
The username must be 3 to 20 characters. Only alphanumeric characters are allowed.
A discount is granted if the user is a member AND the order total exceeds $100.
EOF

# 4. Run analysis only
spectra docs index --no-interaction
spectra ai generate --suite signup --analyze-only --no-interaction --output-format json --verbosity quiet > result.json

# 5. Inspect the result
cat result.json | jq '.analysis'
```

**Expected**:
- `analysis.technique_breakdown` exists
- `BVA` count ≥ 4 (boundaries for the 3–20 char range)
- `DT` count ≥ 1 (member × order-total decision table)
- No single category in `analysis.breakdown` exceeds 40% of `total_behaviors`

## Scenario B — Generated test steps use exact boundary values

```bash
# Continue from Scenario A
spectra ai generate --suite signup --no-interaction --output-format json --verbosity quiet
# Read one of the generated boundary tests
grep -l 'technique:' tests/signup/*.md | head -1 | xargs cat
```

**Expected**: At least one test step contains the literal value `21` (one above max) or `2` (one below min). Step text does NOT use generic phrases like "very long" or "too short".

## Scenario C — Existing project: opt-in migration

```bash
# In a project that already has .spectra/prompts/behavior-analysis.md from the previous version
sha256sum .spectra/prompts/behavior-analysis.md > before.sum

# Upgrade SPECTRA, then run any command
spectra docs index --no-interaction

# Verify the file was NOT silently overwritten
sha256sum .spectra/prompts/behavior-analysis.md > after.sum
diff before.sum after.sum  # expect: no diff

# Opt in
spectra prompts reset behavior-analysis
sha256sum .spectra/prompts/behavior-analysis.md > after_reset.sum
diff before.sum after_reset.sum  # expect: diff (file changed)

grep "TECHNIQUE 1" .spectra/prompts/behavior-analysis.md
# expect: match
```

## Scenario D — Acceptance criterion technique hints

```bash
spectra ai analyze --extract-criteria --no-interaction --output-format json --verbosity quiet
# Find a criterion derived from "Username must be 3-20 characters"
grep -l "3-20" docs/criteria/*.criteria.yaml | head -1 | xargs cat
```

**Expected**: The criterion entry contains `technique_hint: BVA`.

## Scenario E — Critic catches boundary mismatch

Construct a synthetic test that claims `21` is above a `20`-char max, but point its source to a doc stating the max is `25`. Run `spectra ai generate --suite signup` (which triggers the critic) and inspect the verification verdict on that test.

**Expected**: Verdict is `partial` with a reason citing the boundary mismatch.

## Acceptance gate

All of the following must be true:

- [ ] Scenario A produces a `technique_breakdown` with non-zero `BVA` and `DT`
- [ ] Scenario B's generated tests contain literal boundary values
- [ ] Scenario C confirms no silent overwrite, and reset restores the new template
- [ ] Scenario D produces criteria with `technique_hint`
- [ ] Scenario E produces a partial verdict with boundary-mismatch reason
- [ ] `dotnet test` passes with at least 15 net new tests
