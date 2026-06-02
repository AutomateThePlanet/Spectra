# Quickstart: Validating Spec 048 locally

**Branch**: `048-criteria-coverage-guards`
**Date**: 2026-06-02

This walkthrough validates the three guards manually after Phase 3 implementation. It uses a throwaway sandbox project and existing CLI commands — no real AI calls required (uses `--skip-criteria` and forced empty fixtures where applicable).

## Prerequisites

- This branch built locally: `dotnet build` succeeds.
- An empty scratch directory: `mkdir C:\Temp\spec048-sandbox && cd C:\Temp\spec048-sandbox`.

## Setup the sandbox

```pwsh
cd C:\Temp\spec048-sandbox
dotnet run --project C:\SourceCode\Spectra\src\Spectra.CLI -- init --skip-skills
# Creates spectra.config.json, docs/, test-cases/.
```

Add a single trivial documentation file so indexing has something to find:

```pwsh
@"
# Checkout Flow
This is placeholder content for the checkout flow. No testable acceptance criteria.
"@ | Out-File -Encoding utf8 docs\checkout.md
```

## Scenario A — Zero-criteria warning fires (US1)

Run `docs index` against the sandbox. The stub-empty doc above means extraction (with real AI) returns no criteria; on a CI box without a configured provider, the same outcome surfaces via the provider-absent branch in `TryExtractCriteriaAsync`. Either way the corpus criteria total is zero.

```pwsh
dotnet run --project C:\SourceCode\Spectra\src\Spectra.CLI -- docs index --output-format json | Out-File .spectra-result.local.json
```

**Expected**:

1. Console (when not in JSON mode): a yellow `warning:` line containing `extracted 0 acceptance criteria` and `spectra ai analyze --extract-criteria`.
2. JSON: `"criteria_warning"` key present with the same message string.
3. Exit code: `0` (success).

```pwsh
Get-Content .spectra-result.json | ConvertFrom-Json | Select-Object status, criteria_extracted, criteria_warning
```

Should show: `status=completed`, `criteria_extracted=0`, `criteria_warning="Indexed 1 document(s) but extracted 0 acceptance criteria. …"`.

## Scenario B — `--skip-criteria` suppresses the warning (Edge case)

```pwsh
dotnet run --project C:\SourceCode\Spectra\src\Spectra.CLI -- docs index --skip-criteria --output-format json | Out-File .spectra-result.json
Get-Content .spectra-result.json | ConvertFrom-Json | Select-Object status, criteria_extracted, criteria_warning
```

**Expected**: `criteria_warning` absent; `criteria_extracted` absent or null; `status=completed`.

## Scenario C — Generate no-match note (US2, batch flow)

Still in the sandbox (no criteria exist yet):

```pwsh
dotnet run --project C:\SourceCode\Spectra\src\Spectra.CLI -- ai generate checkout --count 1 --no-interaction --skip-critic --output-format json --dry-run | Out-File .gen.json
Get-Content .gen.json | ConvertFrom-Json | Select-Object status, suite, notes
```

**Expected**: `notes` array present, single entry containing `No acceptance criteria matched suite 'checkout'`. `status=completed`. (Use `--dry-run` to avoid spending real AI tokens; the note attaches regardless of dry-run since it is computed at result-build time.)

## Scenario D — Generate note suppressed when criteria match (negative)

Place a fixture criteria file that DOES match the suite:

```pwsh
mkdir docs\criteria
@"
criteria:
  - id: AC-001
    text: 'Checkout MUST complete in under 3 minutes'
    rfc2119: MUST
    component: checkout
    source_doc: docs/checkout.md
    source_type: document
"@ | Out-File -Encoding utf8 docs\criteria\checkout.criteria.yaml

@"
sources:
  - file: docs/criteria/checkout.criteria.yaml
    source_doc: docs/checkout.md
    source_type: document
    doc_hash: stub
    criteria_count: 1
    last_extracted: 2026-06-02T00:00:00Z
    outcome: extracted
"@ | Out-File -Encoding utf8 docs\criteria\_criteria_index.yaml
```

Re-run the dry-run generation:

```pwsh
dotnet run --project C:\SourceCode\Spectra\src\Spectra.CLI -- ai generate checkout --count 1 --no-interaction --skip-critic --output-format json --dry-run | Out-File .gen.json
Get-Content .gen.json | ConvertFrom-Json | Select-Object status, suite, notes
```

**Expected**: `notes` is `$null` (absent from JSON); no no-match note appears.

## Scenario E — Note present in JSON even under `--verbosity quiet` (FR-010)

Delete the fixture criteria file so the no-match condition holds again:

```pwsh
Remove-Item docs\criteria\checkout.criteria.yaml
Remove-Item docs\criteria\_criteria_index.yaml

dotnet run --project C:\SourceCode\Spectra\src\Spectra.CLI -- ai generate checkout --count 1 --no-interaction --skip-critic --verbosity quiet --output-format json --dry-run | Out-File .gen.json
Get-Content .gen.json | ConvertFrom-Json | Select-Object status, notes
```

**Expected**: `notes` array still present in the JSON despite `--verbosity quiet`. The human-facing console echo is suppressed but the structured channel is unaffected.

## Scenario F — Legacy `_criteria_index.yaml` deserializes as `outcome: extracted` (FR-002)

Write a pre-spec-048 entry (no `outcome:` key):

```pwsh
@"
sources:
  - file: docs/criteria/login.criteria.yaml
    source_doc: docs/login.md
    source_type: document
    doc_hash: legacy
    criteria_count: 0
    last_extracted: 2026-04-12T10:00:00Z
"@ | Out-File -Encoding utf8 docs\criteria\_criteria_index.yaml
```

Run a coverage report that reads the index:

```pwsh
dotnet run --project C:\SourceCode\Spectra\src\Spectra.CLI -- ai analyze --list-criteria --output-format json | Out-File .crit.json
```

**Expected**: the command reads the legacy entry without error. (Round-trip behaviour — if the analyze handler re-writes the entry, it'll carry `outcome: extracted`. The first read alone is the smoke test for FR-002.)

## Cleanup

```pwsh
cd C:\
Remove-Item -Recurse -Force C:\Temp\spec048-sandbox
```

## Validation summary

| Scenario | FR(s) covered | SC(s) covered |
|---|---|---|
| A — zero-criteria warning fires | FR-005, FR-006, FR-007, FR-014 | SC-001 |
| B — `--skip-criteria` suppresses | FR-008 | (negative confirmation for SC-002) |
| C — generate batch no-match | FR-009, FR-011 | SC-003 |
| D — match → no note | FR-009 (negative) | SC-002 |
| E — note in JSON when quiet | FR-010 | SC-004 |
| F — legacy index reads | FR-001, FR-002 | SC-006 |

After all six scenarios pass, the spec is validated end-to-end against a real CLI binary. The xUnit suite covers the same matrix via stubs (see Test Plan in `spec.md`).
