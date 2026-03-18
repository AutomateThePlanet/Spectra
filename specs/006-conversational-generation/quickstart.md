# Quickstart: Conversational Test Generation

**Branch**: `006-conversational-generation` | **Date**: 2026-03-18

## Overview

This feature adds two modes for test generation and updates:

1. **Direct Mode** — Provide flags, AI executes autonomously
2. **Interactive Mode** — Run without flags, AI guides you step by step

Both modes write tests directly to disk. Git is the review tool.

---

## Direct Mode (Flags Provided)

### Generate Tests

```bash
# Generate tests for a suite with a specific focus
spectra ai generate checkout --focus "negative payment scenarios"

# Generate tests for a suite (full coverage)
spectra ai generate checkout

# CI mode (no prompts, required suite)
spectra ai generate checkout --no-interaction
```

### Update Tests

```bash
# Update all tests in a suite
spectra ai update checkout

# CI mode
spectra ai update checkout --no-interaction
```

---

## Interactive Mode (No Flags)

### Generate Tests

```bash
spectra ai generate
```

The CLI will:
1. Show available suites with test counts
2. Ask what kind of tests to generate
3. Accept a focus description
4. Show existing coverage
5. Generate and write tests
6. Offer to continue with remaining gaps

### Update Tests

```bash
spectra ai update
```

The CLI will:
1. Show suites with last-updated dates
2. Compare tests against documentation
3. Update/mark/flag tests as needed
4. Show summary with recommended actions

---

## Key Principles

### No Review Step

Tests are written immediately. Review via:
- Your IDE
- `git diff tests/{suite}/`
- `git status`

Revert if unhappy:
```bash
git checkout tests/{suite}/
```

### Coverage Gap Tracking

Before generating, the system shows existing tests to prevent duplicates.
After generating, it shows remaining gaps.

### Profile Auto-Loading

If `spectra.profile.md` exists, settings are applied automatically.
Interactive mode selections layer on top.

---

## CI Integration

Use `--no-interaction` with `--suite` for automated pipelines:

```bash
# In CI script
spectra ai generate checkout --no-interaction
if [ $? -ne 0 ]; then
  echo "Generation failed"
  exit 1
fi
```

Exit codes:
- `0` — Success
- `1` — Error (missing args, AI failure, no docs)

---

## Test Classification (Updates)

When updating, tests are classified as:

| Classification | Action |
|---------------|--------|
| Up-to-date | No change |
| Outdated | Content updated in place |
| Orphaned | `status: orphaned` added to frontmatter |
| Redundant | Flagged in `_index.json` |

Review orphaned and redundant tests manually:
```bash
git diff tests/checkout/
```

---

## Output Symbols

| Symbol | Meaning |
|--------|---------|
| ◆ | Interactive prompt |
| ◐ | Loading/progress |
| ✓ | Success |
| ✗ | Error |
| ⚠ | Warning |
| ℹ | Information |

---

## Example Session

### Direct Mode Generate

```
$ spectra ai generate checkout --focus "negative payment"

◐ Loading checkout suite... 42 existing tests
◐ Scanning documentation... 8 relevant files
◐ Checking for duplicates...
◐ Generating tests...

✓ Generated 5 tests:

  TC-201  Payment with card expired this month        high   negative
  TC-202  Payment with currency mismatch              high   negative
  TC-203  Duplicate payment within 30 seconds         high   negative
  TC-204  Card number with valid Luhn wrong length    medium negative
  TC-205  Payment timeout after gateway delay         medium negative

✓ Written to tests/checkout/
✓ Index updated

ℹ Gaps still uncovered:
  • Refund after partial payment
  • 3D Secure authentication failure
```

### Interactive Mode Generate

```
$ spectra ai generate

┌ SPECTRA Test Generation
│
◆ Which suite?
│  ● checkout (42 tests)
│  ○ auth (18 tests)
│  ○ orders (7 tests)
│  ○ Create new suite
└

◆ checkout selected. What kind of tests?
│  ○ Full coverage
│  ● Negative / error scenarios only
│  ○ Specific area
└

◐ Checking existing coverage...

ℹ Existing negative tests: 8
ℹ Uncovered negative scenarios: 5

◐ Generating 5 tests...

✓ Generated 5 tests
✓ Written to tests/checkout/

◆ Generate more for remaining gaps?
│  ○ Yes, generate all
│  ● No, I'm done
└

Done.
```

---

## Implementation Notes

**Status**: ✅ Complete (2026-03-18)

### Files Modified

- `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs` — Suite argument now optional, added --focus and --no-interaction
- `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` — Full rewrite with direct/interactive modes
- `src/Spectra.CLI/Commands/Update/UpdateCommand.cs` — Suite argument now optional, added --no-interaction
- `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` — Full rewrite with direct/interactive modes and classification
- `src/Spectra.Core/Models/TestCase.cs` — Added Status, OrphanedReason, OrphanedDate fields
- `src/Spectra.Core/Models/TestIndexEntry.cs` — Added RedundantOf, RedundantReason fields
- `src/Spectra.CLI/IO/TestFileWriter.cs` — Output orphaned status in frontmatter
- `src/Spectra.CLI/Agent/*.cs` — Enhanced error handling with retry suggestions

### New Files

**Core Models** (`src/Spectra.Core/Models/`):
- `SessionMode.cs` — Generate/Update mode enum
- `SessionState.cs` — Interactive flow state enum
- `TestTypeSelection.cs` — Test type selection enum
- `ClassifiedTest.cs` — Classified test model
- `SuiteSummary.cs` — Suite summary with test count
- `UpdateResult.cs` — Update operation result

**Interactive Components** (`src/Spectra.CLI/Interactive/`):
- `SuiteSelector.cs` — Suite selection with "Create new suite" option
- `SuiteScanner.cs` — Scan suites with test counts
- `TestTypeSelector.cs` — Test type selection (Full/Negative/Specific/Free)
- `FocusDescriptor.cs` — Focus description input
- `GapSelector.cs` — Gap selection for follow-up generation
- `InteractiveSession.cs` — State machine for interactive flow

**Output Components** (`src/Spectra.CLI/Output/`):
- `OutputSymbols.cs` — Unicode symbols (◆◐✓✗⚠ℹ)
- `ProgressReporter.cs` — Spinner/progress display
- `ResultPresenter.cs` — Table/summary display

**Coverage Components** (`src/Spectra.CLI/Coverage/`):
- `GapAnalyzer.cs` — Compare docs against test source_refs
- `GapPresenter.cs` — Display coverage gaps

**Classification Components** (`src/Spectra.CLI/Classification/`):
- `ClassificationPresenter.cs` — Display update classification results

### Dependencies

- Spectre.Console (existing) — Rich terminal UX
- System.CommandLine (existing) — CLI parsing
