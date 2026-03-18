# CLI Command Contracts: Conversational Test Generation

**Branch**: `006-conversational-generation` | **Date**: 2026-03-18

## Command: `spectra ai generate`

### Synopsis

```
spectra ai generate [<suite>] [--focus <description>] [--no-interaction] [options]
```

### Arguments

| Argument | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `suite` | string | No | null | Suite name to generate tests for |

### Options

| Option | Short | Type | Default | Description |
|--------|-------|------|---------|-------------|
| `--focus` | `-f` | string | null | Focus area description for generation |
| `--no-interaction` | | flag | false | Disable interactive prompts (CI mode) |
| `--verbosity` | `-v` | enum | Normal | Output verbosity level |
| `--dry-run` | | flag | false | Show what would be generated without writing |

### Mode Detection

| Condition | Mode | Behavior |
|-----------|------|----------|
| `suite` provided | Direct | Execute autonomously, no prompts |
| `suite` not provided | Interactive | Guide through suite/type selection |
| `--no-interaction` without `suite` | Error | Exit with code 1, message to stderr |
| Non-TTY environment | Auto CI | Behave as `--no-interaction` |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (tests generated or no gaps found) |
| 1 | Error (missing args, AI failure, no docs) |

### Output Format

#### Direct Mode
```
◐ Loading {suite} suite... {count} existing tests
◐ Scanning documentation... {count} relevant files
◐ Checking for duplicates...
◐ Generating tests...

✓ Generated {count} tests:

  {ID}  {Title}  {priority}  {tags}
  ...

✓ Written to tests/{suite}/
✓ Index updated

ℹ Gaps still uncovered:
  • {gap description}
  ...
```

#### Interactive Mode
```
┌ SPECTRA Test Generation
│
◆ Which suite?
│  ○ {suite} ({count} tests)
│  ...
│  ○ Create new suite
└  ↑/↓ to select, enter to confirm

◆ {suite} selected. What kind of tests?
│  ○ Full coverage (happy path + negative + boundary)
│  ○ Negative / error scenarios only
│  ○ Specific area — let me describe
└

◆ Describe what you need:
│  > {user input}
└

◐ Checking existing coverage...

ℹ Existing {area} tests:
  {ID}  {Title}
  ...

ℹ Uncovered areas:
  • {gap}
  ...

◐ Generating {count} tests...

✓ Generated:
  {ID}  {Title}  {priority}  {tags}
  ...

✓ Written to tests/{suite}/
✓ Index updated

ℹ Still uncovered:
  • {gap}
  ...

◆ Generate tests for uncovered areas?
│  ○ Yes, generate all
│  ○ Let me pick
│  ○ No, I'm done
└
```

---

## Command: `spectra ai update`

### Synopsis

```
spectra ai update [<suite>] [--no-interaction] [options]
```

### Arguments

| Argument | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `suite` | string | No | null | Suite name to update tests for |

### Options

| Option | Short | Type | Default | Description |
|--------|-------|------|---------|-------------|
| `--no-interaction` | | flag | false | Disable interactive prompts (CI mode) |
| `--verbosity` | `-v` | enum | Normal | Output verbosity level |
| `--dry-run` | | flag | false | Show what would be updated without writing |

### Mode Detection

| Condition | Mode | Behavior |
|-----------|------|----------|
| `suite` provided | Direct | Execute autonomously, no prompts |
| `suite` not provided | Interactive | Guide through suite selection |
| `--no-interaction` without `suite` | Error | Exit with code 1, message to stderr |
| Non-TTY environment | Auto CI | Behave as `--no-interaction` |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (tests updated) |
| 1 | Error (missing args, AI failure, no docs) |

### Output Format

#### Direct Mode
```
◐ Loading {count} tests from {suite}...
◐ Comparing against documentation...

Results:
  ✓ {count} up to date
  ⚠ {count} outdated — updated
  ✗ {count} orphaned — marked with WARNING header
  ↔ {count} redundant — flagged in index

✓ {count} test files updated
✓ {count} orphaned tests marked
✓ Index rebuilt

ℹ Orphaned tests (documentation removed):
  {ID}  {Title}
  ...
  Review these and delete if no longer needed.
```

#### Interactive Mode
```
┌ SPECTRA Test Maintenance
│
◆ Which suite to review?
│  ○ {suite} ({count} tests, last updated {date})
│  ...
│  ○ All suites
└

◐ Comparing {count} tests against documentation...

Results:
  ✓ {count} up to date
  ⚠ {count} outdated — updated
  ✗ {count} orphaned — marked
  ↔ {count} redundant — flagged

✓ Written all changes

ℹ Orphaned tests:
  {ID}  {Title}
  ...

ℹ Redundant test:
  {ID}  Nearly identical to {ID}

◆ Review changes in your IDE or run: git diff tests/{suite}/
└
```

---

## Orphaned Test Frontmatter Format

When a test is classified as orphaned, its frontmatter is updated:

```yaml
---
id: TC-108
priority: high
tags:
  - smoke
component: checkout
status: orphaned
orphaned_reason: "Source documentation docs/features/checkout/guest-checkout.md no longer exists"
orphaned_date: 2026-03-18
---
```

### Added Fields

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | Set to "orphaned" |
| `orphaned_reason` | string | Why the test was orphaned |
| `orphaned_date` | date | When the test was marked orphaned |

---

## Index Flag for Redundant Tests

When a test is classified as redundant, the `_index.json` entry is updated:

```json
{
  "id": "TC-115",
  "file": "tc-115.md",
  "title": "Checkout with expired card",
  "priority": "high",
  "tags": ["checkout", "negative"],
  "redundant_of": "TC-103",
  "redundant_reason": "92% content similarity"
}
```

### Added Fields

| Field | Type | Description |
|-------|------|-------------|
| `redundant_of` | string | ID of the test this duplicates |
| `redundant_reason` | string | Why flagged as redundant |

---

## Error Messages

### Missing Required Arguments (CI Mode)

```
✗ Error: --suite is required when using --no-interaction

Usage: spectra ai generate <suite> [--focus <description>] [--no-interaction]

Run 'spectra ai generate --help' for more information.
```

### No Documentation Found

```
✗ Error: No documentation found in docs/

Please add documentation files or check your spectra.config.json.
```

### AI Generation Failed

```
✗ Error: AI generation failed: {error message}

{count} tests were written before the error.
You can retry with: spectra ai generate {suite} --focus "{focus}"
```

### Non-TTY Detection Warning

```
⚠ Non-interactive environment detected. Running in CI mode.
✗ Error: --suite is required in non-interactive mode.
```
