# SPECTRA Feature Spec: Interactive Generation & Execution Agent

**Status:** Draft — ready for spec-kit cycle
**Depends on:** Phase 1 (CLI), Phase 2 (MCP Engine), Phase 4 (Profiles)

---

## 1. Overview

Test generation and update stay in the CLI. Two modes: **direct mode** (describe what you want, AI executes autonomously) and **interactive mode** (AI guides you step by step). Both write files directly — no review/accept step. Git is the review tool.

Execution stays in Copilot Chat / Claude via MCP tools with a bundled agent prompt.

---

## 2. CLI Generation — Two Modes

### Mode 1: Direct (current behavior, improved)

User describes what to do. AI figures out how.

```bash
$ spectra ai generate --suite checkout --focus "negative payment scenarios"
```

```
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
    • Zero-amount authorization
```

No prompts, no questions. Describe intent → get tests → done.

For fully autonomous (CI):

```bash
$ spectra ai generate --suite checkout --no-interaction
```

### Mode 2: Interactive (new)

User runs without arguments. AI asks questions.

```bash
$ spectra ai generate
```

```
  ┌ SPECTRA Test Generation
  │
  ◆ Which suite?
  │  ○ checkout (42 tests)
  │  ○ auth (18 tests)
  │  ○ orders (7 tests)
  │  ○ Create new suite
  └  ↑/↓ to select, enter to confirm

  ◆ checkout selected. What kind of tests?
  │  ○ Full coverage (happy path + negative + boundary)
  │  ○ Negative / error scenarios only
  │  ○ Specific area — let me describe
  └

  ◆ Describe what you need:
  │  > negative tests for payment validation, edge cases
  │    around card expiration and currency
  └

  ◐ Checking existing coverage...

  ℹ Existing payment validation tests:
    TC-105  Checkout with expired card
    TC-108  Checkout with invalid card number
    TC-112  Checkout with zero amount

  ℹ Uncovered areas:
    • Card expired this month (future year, past month)
    • Currency mismatch between cart and payment
    • Valid Luhn but wrong card length
    • Duplicate payment prevention
    • Payment timeout

  ◐ Generating 5 tests...

  ✓ Generated:

    TC-201  Payment with card expired this month        high   negative
    TC-202  Payment with currency mismatch              high   negative
    TC-203  Duplicate payment within 30 seconds         high   negative
    TC-204  Card number with valid Luhn wrong length    medium negative
    TC-205  Payment timeout after gateway delay         medium negative

  ✓ Written to tests/checkout/
  ✓ Index updated

  ℹ Still uncovered:
    • Refund after partial payment
    • 3D Secure authentication failure

  ◆ Generate tests for uncovered areas?
  │  ○ Yes, generate all
  │  ○ Let me pick
  │  ○ No, I'm done
  └
```

### Key Principles

**No review step.** Tests are written to disk immediately. User reviews via IDE, git diff, or any file browser. Revert with `git checkout .` if not happy.

**Always show existing tests.** Before generating, show what already exists for the requested area. Prevents duplicates proactively.

**Always show gaps after generation.** After writing tests, show what's still uncovered. User can continue or stop.

**Profile is loaded automatically.** If `spectra.profile.md` exists, AI follows it. Interactive mode adds user intent on top.

---

## 3. CLI Update — Two Modes

### Mode 1: Direct

```bash
$ spectra ai update --suite checkout
```

```
  ◐ Loading 42 tests from checkout...
  ◐ Comparing against documentation...

  Results:
    ✓ 35 up to date
    ⚠ 4 outdated — updated
    ✗ 2 orphaned — marked with WARNING header
    ↔ 1 redundant — flagged in index

  ✓ 4 test files updated
  ✓ 2 orphaned tests marked
  ✓ Index rebuilt

  ℹ Orphaned tests (documentation removed):
    TC-108  Guest checkout validation
    TC-122  Legacy payment gateway timeout
    Review these and delete if no longer needed.
```

Outdated tests are **updated in place**. Orphaned tests get a warning added to their frontmatter:

```yaml
---
id: TC-108
status: orphaned
orphaned_reason: "Source documentation docs/features/checkout/guest-checkout.md no longer exists"
---
```

User decides what to do with them via git.

### Mode 2: Interactive

```bash
$ spectra ai update
```

```
  ┌ SPECTRA Test Maintenance
  │
  ◆ Which suite to review?
  │  ○ checkout (42 tests, last updated 12 days ago)
  │  ○ auth (18 tests, last updated 3 days ago)
  │  ○ orders (7 tests, last updated 45 days ago)
  │  ○ All suites
  └

  ◐ Comparing 42 tests against documentation...

  Results:
    ✓ 35 up to date
    ⚠ 4 outdated — updated
    ✗ 2 orphaned — marked
    ↔ 1 redundant — flagged

  ✓ Written all changes

  ℹ Orphaned tests:
    TC-108  Guest checkout validation
    TC-122  Legacy payment gateway timeout

  ℹ Redundant test:
    TC-115  Nearly identical to TC-103

  ◆ Review changes in your IDE or run: git diff tests/checkout/
  └
```

---

## 4. CLI UX Style

The CLI output should follow the visual style of modern terminal tools (similar to GitHub Copilot CLI, clack prompts):

- `◆` for interactive prompts (selection, text input)
- `◐` for loading/progress spinners
- `✓` for success
- `✗` for errors
- `⚠` for warnings
- `ℹ` for informational messages
- `│` and `┌ └` for visual grouping
- Color: green for success, yellow for warnings, red for errors, cyan for info
- Tables for test listings (ID, title, priority, tags)
- No walls of text — concise, scannable output

Library recommendation: [Spectre.Console](https://spectreconsole.net/) for .NET — supports tables, trees, prompts, progress, and rich formatting.

---

## 5. Bundled Execution Agent Prompt

SPECTRA ships with a ready-to-use agent prompt for test execution via MCP tools.

### Location

Installed by `spectra init` into the target repo:

```
.github/agents/spectra-execution.agent.md
.github/skills/spectra-execution/SKILL.md     (same content, for Copilot CLI)
```

### Agent Prompt

```markdown
---
name: spectra-execution
description: >
  Execute manual test suites interactively using SPECTRA MCP tools.
  Use when asked to run tests, execute a test suite, or do manual testing.
---

# SPECTRA Test Execution Agent

You are a QA Test Execution Assistant. You execute manual test suites
interactively using SPECTRA MCP tools.

## Workflow
1. Call list_available_suites to show available suites
2. Ask which suite and any filters (priority, tags, type)
3. Call start_execution_run with chosen suite and filters
4. For each test:
   a. Call get_test_case_details with current handle
   b. Present: title, preconditions, steps, expected result, test data
   c. Ask: PASS, FAIL, BLOCKED, or SKIP
   d. If FAIL: ask for comment
   e. Call advance_test_case or skip_test_case
   f. Show progress: "Test 5/15 — 4 passed, 1 failed"
5. Call finalize_execution_run
6. Present summary, offer to log bugs via Azure DevOps MCP

## Presentation Rules
- ONE test at a time
- Numbered steps, each starting with action verb
- Always show progress after recording result

## Result Collection
- "it passed" → PASS
- "failed, button greyed out" → FAIL with comment
- "staging is down" → BLOCKED with reason
- "not applicable" → SKIP with reason

## Bug Logging (if Azure DevOps MCP connected)
When test fails and user wants a bug:
- Title: "[SPECTRA] {test_title} — {failure_summary}"
- Include test ID, steps, expected vs actual, user comment
- Priority: high→P1, medium→P2, low→P3
```

### Works Across Orchestrators

- **Copilot Chat (VS Code)** — as `.github/agents/spectra-execution.agent.md`
- **Copilot CLI** — as `.github/skills/spectra-execution/SKILL.md`
- **Claude** — paste into project instructions
- **Any MCP client** — reference documentation

---

## 6. MCP Tools (additions for data access)

These MCP tools support the CLI and provide value to any orchestrator:

| Tool                    | Purpose                                      |
| ----------------------- | -------------------------------------------- |
| `validate_tests`        | Validate all test files, return errors       |
| `rebuild_indexes`       | Rebuild all _index.json files                |
| `analyze_coverage_gaps` | Compare docs against tests, return uncovered areas |

These are **data tools** — no AI, no model dependency. Deterministic operations.

---

## 7. What Goes Where (Summary)

| Operation            | Where          | Why                                    |
| -------------------- | -------------- | -------------------------------------- |
| Generate tests       | CLI            | Needs provider chain, BYOK, model control |
| Update tests         | CLI            | Same — AI operation with model control |
| Analyze coverage     | CLI            | Same                                   |
| Execute tests        | MCP + Chat     | Ping-pong flow, chat is natural        |
| Validate             | MCP tool       | Deterministic, no AI                   |
| Rebuild indexes      | MCP tool       | Deterministic, no AI                   |
| List suites          | MCP tool       | Data access, no AI                     |
| Show test details    | MCP tool       | Data access, no AI                     |
| Init repo            | CLI            | One-time setup                         |
| Init profile         | CLI            | One-time setup, interactive            |
| Generate dashboard   | CLI            | Build step, not runtime                |

---

## 8. Spec-Kit Breakdown

### Cycle A: Interactive CLI Generation

- Refactor `spectra ai generate` to support two modes (direct + interactive)
- Refactor `spectra ai update` to support two modes (direct + interactive)
- No review/accept step — write directly, git is the review tool
- Always check existing tests before generating (dedup)
- Always show coverage gaps after generating
- Spectre.Console for rich terminal UX (tables, prompts, spinners, colors)
- `--no-interaction` flag for CI
- Profile loaded automatically

### Cycle B: Bundled Execution Agent

- Create `.github/agents/spectra-execution.agent.md`
- Create `.github/skills/spectra-execution/SKILL.md`
- `spectra init` installs agent files into target repo
- Add MCP tools: validate_tests, rebuild_indexes, analyze_coverage_gaps
- Documentation for usage with Copilot Chat, Claude, generic MCP clients
