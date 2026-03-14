# CLI Command Contracts

**Date**: 2026-03-13
**Feature**: 001-ai-test-generation-cli

This document defines the command-line interface contracts for the Spectra CLI.

---

## Global Options

Available on all commands:

| Option | Alias | Type | Default | Description |
|--------|-------|------|---------|-------------|
| `--verbosity` | `-v` | enum | normal | Output level: quiet, minimal, normal, detailed, diagnostic |
| `--dry-run` | | bool | false | Preview changes without executing |
| `--no-review` | | bool | false | Skip interactive review (CI mode) |
| `--help` | `-h` | | | Show help |
| `--version` | | | | Show version |

---

## Commands

### `spectra init`

Initialize SPECTRA in a repository.

```
spectra init [--force]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--force` | bool | false | Overwrite existing configuration |

**Exit Codes**:
- `0`: Success
- `1`: Error (filesystem issue, etc.)

**Output**:
- Creates `spectra.config.json`
- Creates `docs/` (if missing)
- Creates `tests/` (if missing)
- Creates `.github/skills/test-generation/SKILL.md`
- Updates `.gitignore` with `.spectra.lock`

---

### `spectra validate`

Validate all test files and indexes.

```
spectra validate [--suite <name>]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--suite` | string | (all) | Validate specific suite only |

**Exit Codes**:
- `0`: All tests valid
- `1`: Validation errors found

**Output** (normal verbosity):
```
Validating tests...
  ✓ checkout: 42 tests valid
  ✗ auth: 2 errors
    - TC-201: Missing required field 'priority' (auth/login-happy-path.md)
    - TC-205: Invalid depends_on reference 'TC-999' (auth/mfa-flow.md)

Validation failed: 2 errors in 1 suite
```

---

### `spectra index`

Rebuild metadata indexes for all suites.

```
spectra index [--suite <name>]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--suite` | string | (all) | Rebuild specific suite only |

**Exit Codes**:
- `0`: Success
- `1`: Error

**Output**:
```
Rebuilding indexes...
  ✓ checkout/_index.json (42 tests)
  ✓ auth/_index.json (18 tests)

Rebuilt 2 indexes
```

---

### `spectra list`

List suites and test counts.

```
spectra list [--format <text|json>]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--format` | enum | text | Output format |

**Exit Codes**:
- `0`: Success

**Output** (text):
```
Suite         Tests   Priority Distribution
checkout      42      high: 12, medium: 25, low: 5
auth          18      high: 8, medium: 10, low: 0
orders        31      high: 5, medium: 20, low: 6

Total: 91 tests in 3 suites
```

---

### `spectra show <test-id>`

Display a specific test case.

```
spectra show <test-id> [--format <text|json|yaml>]
```

| Argument | Type | Required | Description |
|----------|------|----------|-------------|
| `test-id` | string | yes | Test ID (e.g., TC-102) |

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--format` | enum | text | Output format |

**Exit Codes**:
- `0`: Success
- `1`: Test not found

---

### `spectra config`

Show effective configuration.

```
spectra config [--format <text|json>]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--format` | enum | text | Output format |

**Exit Codes**:
- `0`: Success
- `1`: No configuration found

---

### `spectra ai generate`

Generate tests from documentation.

```
spectra ai generate --suite <name> [options]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--suite` | string | (required) | Target suite name |
| `--count` | int | 15 | Maximum tests to generate |
| `--priority` | enum | (auto) | Auto-assign priority to all |
| `--tags` | string[] | | Auto-assign tags |
| `--space` | string | | Use Copilot Space as source |
| `--provider` | string | | Force specific AI provider |

**Exit Codes**:
- `0`: Success (tests written)
- `1`: Error (AI failure, validation failure)
- `130`: Cancelled by user

**Interactive Flow**:
```
Generating tests for suite: checkout

Loading documentation...
  Found 12 docs (340 KB total)

Generating tests...
  [============================] 18/18 tests generated

Summary:
  ✓ 15 valid tests
  ⚠ 2 potential duplicates
  ✗ 1 invalid (missing expected result)

Options:
  (r)eview one by one    (a)ccept all valid    (v)iew duplicates
  (e)xport to file       (q)uit

> a

Writing 15 tests to tests/checkout/...
Rebuilding index...

Done. 15 tests added to checkout suite.
```

**CI Mode** (`--no-review`):
```
Generating tests for suite: checkout
Generated 18 tests, 15 valid, 2 duplicates, 1 invalid
Writing 15 tests...
Done.
```

---

### `spectra ai update`

Update tests from changed documentation.

```
spectra ai update --suite <name> [options]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--suite` | string | (required) | Target suite (or `--all`) |
| `--all` | bool | false | Update all suites |
| `--diff` | string | | Git range to consider |
| `--space` | string | | Use Copilot Space as source |
| `--provider` | string | | Force specific AI provider |

**Exit Codes**:
- `0`: Success
- `1`: Error

**Output**:
```
Analyzing tests for suite: checkout

Classification:
  ✓ 38 up-to-date
  ⚠ 3 outdated (docs changed)
  ⚠ 1 orphaned (docs removed)
  ⚠ 0 redundant

Proposed changes:
  [1/3] TC-105: Update steps to reflect new checkout flow
  [2/3] TC-108: Update expected result for error handling
  [3/3] TC-112: Update preconditions
  [ORPHAN] TC-115: Documentation removed - suggest delete

Options:
  (a)ccept all    (r)eview one by one    (s)kip    (q)uit

> r
```

---

### `spectra ai analyze`

Analyze test coverage.

```
spectra ai analyze [--suite <name>] [options]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--suite` | string | (all) | Target suite |
| `--space` | string | | Use Copilot Space as source |
| `--provider` | string | | Force specific AI provider |
| `--output` | string | | Write report to file |
| `--format` | enum | md | Report format: md, json |

**Exit Codes**:
- `0`: Success

**Output** (Markdown):
```markdown
# Coverage Analysis: checkout

## Summary
- 42 tests covering checkout functionality
- Estimated documentation coverage: 78%

## Coverage Gaps
1. **Payment retry flow** - No tests for failed payment retry
2. **Cart abandonment** - No negative tests for timeout scenarios

## Redundant Tests
- TC-103 and TC-107 cover same scenario (checkout happy path)

## Priority Distribution
- High: 12 (29%)
- Medium: 25 (59%)
- Low: 5 (12%)

## Recommendations
1. Add 3-5 tests for payment retry scenarios
2. Consider merging TC-103 and TC-107
```

---

## Error Messages

Standard error message format:

```
Error: <brief description>

  <details if available>

For more information, run with -v or -vv
```

Example:
```
Error: Suite 'checkout' is locked by another process

  Lock held by PID 12345 since 2026-03-13T10:05:00Z
  Lock expires in 8 minutes

  To force unlock: delete tests/checkout/.spectra.lock
```

---

## JSON Output Schema

When `--format json` is specified, output follows this structure:

```json
{
  "success": true,
  "data": { ... },
  "errors": [],
  "warnings": []
}
```

For errors:
```json
{
  "success": false,
  "data": null,
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "Missing required field 'priority'",
      "file": "tests/auth/login.md",
      "line": 3
    }
  ],
  "warnings": []
}
```
