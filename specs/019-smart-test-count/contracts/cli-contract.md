# CLI Contract: Smart Test Count

**Feature**: 019-smart-test-count

## Modified Command: `spectra ai generate`

### Existing Options (unchanged)

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `<suite>` | string? | null | Target suite name (omit for interactive mode) |
| `--count`, `-n` | int? | null | Exact test count to generate |
| `--focus`, `-f` | string? | null | Focus area for generation |
| `--skip-critic` | bool | false | Skip grounding verification |
| `--no-interaction` | bool | false | Non-interactive / CI mode |
| `--dry-run` | bool | false | Preview without writing |

### Behavior Change: `--count` omitted

**Before**: Defaults to 20 in direct mode, prompts in interactive mode.

**After**:
1. AI analyzes source documentation for testable behaviors
2. Displays categorized breakdown
3. **Direct mode** (`--no-interaction` or no TTY): auto-generates all identified new behaviors
4. **Interactive mode**: presents selection menu
5. Post-generation: shows gap notification if partial

### Exit Codes (unchanged)

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Error (no docs, provider failure, etc.) |

### Console Output Contract

**Analysis output** (written to stderr, respects `--quiet`):

```
Analyzing {suite} documentation...

Found {n} documents ({words} words total):
  * {doc1}
  * {doc2}

Identified {total} testable behaviors:
  * {n} happy path flows
  * {n} negative / error scenarios
  * {n} edge cases / boundary conditions
  * {n} security / permission checks

{covered} already covered by existing tests.
Recommended: {recommended} new test cases.
```

**Interactive menu** (only in interactive mode):

```
How many test cases to generate?
> All {n} - full coverage of identified behaviors
  {n} - happy paths only
  {n} - happy paths + negative scenarios
  Custom number
  Let me describe what I want
```

**Gap notification** (post-generation, when partial):

```
{remaining} testable behaviors not yet covered:
  * {n} negative / error scenarios
  * {n} edge cases
  * {n} security checks

Next steps:
  spectra ai generate --suite {suite}    # Generate remaining tests
```
