# Quickstart: Verifying Spec 052 (Test Hardening & Docs Audit)

This feature ships tests and documentation only — no runtime behavior change. "Running it" means running the tests and reading the deliverables.

## Build

```bash
dotnet build Spectra.slnx
```

This compiles the new `tests/Spectra.Integration.Tests` project alongside the existing ones.

## Run the hardening tests

```bash
# Everything except the slow scale guard (fast feedback):
dotnet test --filter "Category!=Scale"

# Just the cross-spec end-to-end suite:
dotnet test tests/Spectra.Integration.Tests --filter "FullyQualifiedName~EndToEndScenarios"

# Just the named regression guards (display names are the user symptoms):
dotnet test tests/Spectra.Integration.Tests --filter "FullyQualifiedName~OriginalBugRegression"

# Just the scale guard (slow; runs in full CI):
dotnet test --filter "Category=Scale"

# Full pass (what CI runs on a fresh checkout):
dotnet test
```

## Expected signal

- All cross-spec tests pass, each exercising 2+ of specs 047–051 against fixture data.
- The five named regression tests pass; their displayed names are the original user symptoms (e.g. `Original bug: high priority filter from a suite returns whole suite`).
- The scale guard passes and proves per-document (not corpus-wide) extraction deadlines.

## Regression demonstration (manual confidence check)

Temporarily revert any one 047–051 fix and re-run; exactly the matching named test (and possibly its cross-spec sibling) fails, identifiable by name alone — then restore the fix:

| Revert | Failing named test |
|--------|--------------------|
| 047 cache gating | `Original bug: cache poisoning on parse failure` |
| 048 zero-criteria warning | `Original bug: first big-project index produced zero criteria silently` |
| 049 index registration | `Original bug: from-description test has different format and is missing from index` |
| 050 criteria injection | `Original bug: extract-criteria on generation not working` |
| 051 filter binding | `Original bug: high priority filter from a suite returns whole suite` |

## Read the deliverables

- `docs/specs/052-doc-audit-report.md` — every audited doc/SKILL file with its disposition.
- `docs/specs/052-skill-transcripts.md` — representative SKILL prompt→output evidence.
- `CHANGELOG.md` — the single consolidated 047–051 entry.
- `PROJECT-KNOWLEDGE.md` — Spec 052 row + silent-failure-pattern learning.
