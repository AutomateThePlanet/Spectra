# SPECTRA Feature Spec: Interactive Generation & Execution Agent

**Status:** Draft — ready for spec-kit cycle
**Depends on:** Phase 1 (CLI), Phase 2 (MCP Engine), Phase 4 (Profiles)

---

## 1. Problem Statement

Current `spectra ai generate` is a batch command with flags:

```bash
spectra ai generate --suite checkout --count 20 --priority high --tags payments
```

This requires the user to know upfront what suite, how many tests, what filters, and what focus area. In reality, QA engineers want a conversational flow where the AI guides them through decisions, shows what already exists, and suggests what's missing.

Similarly, the MCP execution engine has tools but no guidance on how an orchestrator should use them. Every team writes their own prompt (like the RMH copilot-instructions.md). SPECTRA should ship with a ready-to-use execution agent prompt.

---

## 2. Interactive Generation Mode

### Design: `spectra ai generate` becomes conversational by default

When run without arguments, the command enters interactive chat mode. The AI guides the user step by step:

```
$ spectra ai generate

SPECTRA Test Generation

? Which suite would you like to generate tests for?
  1. checkout (42 existing tests)
  2. auth (18 existing tests)
  3. orders (7 existing tests)
  4. Create new suite
  > 1

Loading documentation map for checkout...
Found 8 relevant documents in docs/features/checkout/

? What kind of tests do you want to generate?
  1. Full coverage (happy path + negative + boundary + security)
  2. Only negative / error scenarios
  3. Only for a specific feature area
  4. Let me describe what I need
  > 4

? Describe what you need:
  > I want negative test cases for payment validation, especially
    edge cases around card expiration and currency handling

Checking existing tests...
Found 3 existing tests covering payment validation:
  - TC-105: Checkout with expired card
  - TC-108: Checkout with invalid card number
  - TC-112: Checkout with zero amount

These areas have NO existing coverage:
  - Card expiration with future year but past month
  - Currency mismatch between cart and payment
  - Card number with valid Luhn but wrong length
  - Duplicate payment prevention
  - Payment timeout handling

? How should I proceed?
  1. Generate only new tests (skip areas already covered)
  2. Generate new + update existing if they're incomplete
  3. Let me pick which gaps to cover
  > 3

? Select areas to cover (space to toggle, enter to confirm):
  [x] Card expiration with future year but past month
  [x] Currency mismatch between cart and payment
  [ ] Card number with valid Luhn but wrong length
  [x] Duplicate payment prevention
  [ ] Payment timeout handling

Generating 3 tests...

Generated:
  TC-201: Payment with card expired this month (priority: high)
  TC-202: Payment with currency mismatch (priority: high)
  TC-203: Duplicate payment within 30 seconds (priority: high)

? Review:
  (a)ccept all  (r)eview one by one  (e)dit  (g)enerate more  (q)uit
  > a

✓ 3 tests written to tests/checkout/
✓ _index.json updated

I noticed these areas still have no test coverage:
  - Card number with valid Luhn but wrong length
  - Payment timeout handling
  - Refund after partial payment
  - 3D Secure authentication flow

? Generate tests for any of these? (y/n)
```

### Key Behaviors

**Always check existing tests first.** Before generating anything, load the suite index and show what already exists for the requested area. This prevents duplicates proactively.

**Suggest gaps after generation.** After the user accepts generated tests, the AI analyzes what's still uncovered and offers to generate more. The user can stop at any time.

**Support quick mode with flags.** For CI or experienced users, flags still work:

```bash
# Quick mode — skips all interactive prompts
spectra ai generate --suite checkout --focus "negative payment scenarios" --no-interaction
```

When `--suite` and `--focus` are provided, the AI skips the interactive prompts but still runs dedup check and gap analysis silently, logging results.

**Load profile automatically.** If `spectra.profile.md` exists, the AI follows it. The interactive flow doesn't replace the profile — it adds the user's intent on top of the profile's rules.

---

## 3. Interactive Update Mode

Same conversational approach for updates:

```
$ spectra ai update

SPECTRA Test Maintenance

? Which suite to review?
  1. checkout (42 tests, last updated 12 days ago)
  2. auth (18 tests, last updated 3 days ago)
  3. orders (7 tests, last updated 45 days ago)
  4. All suites
  > 1

Comparing 42 tests against current documentation...

Results:
  ✓ 35 up to date
  ⚠ 4 outdated
  ✗ 2 orphaned (documentation removed)
  ↔ 1 redundant (duplicates TC-103)

? What would you like to do?
  1. Review all findings one by one
  2. Auto-accept updates, review orphaned/redundant only
  3. Show me only the critical changes
  > 2

4 tests updated automatically.

Orphaned tests (no matching documentation):
  TC-108: Guest checkout validation
    → docs/features/checkout/guest-checkout.md was deleted

  TC-122: Legacy payment gateway timeout
    → docs/features/checkout/legacy-gateway.md was deleted

? For each orphaned test:
  (d)elete  (k)eep  (m)ark as deprecated
  
TC-108 > d
TC-122 > k (keeping for regression reference)

Redundant test:
  TC-115: Refund processing flow
    → Nearly identical to TC-103: Refund request handling
    → TC-115 has 2 extra steps covering email notification

? (d)elete TC-115  (m)erge into TC-103  (k)eep both
  > m

✓ Merged TC-115 into TC-103 (added email notification steps)
✓ 4 tests updated, 1 deleted, 1 merged
✓ _index.json rebuilt
```

---

## 4. Bundled Execution Agent Prompt

SPECTRA ships with a ready-to-use agent prompt that orchestrators (Copilot Chat, Claude) can use to drive test execution through MCP tools.

### Location

```
.github/agents/spectra-execution.agent.md
```

Installed by `spectra init` into the target repo. Also available as a Copilot Agent Skill in `.github/skills/spectra-execution/SKILL.md`.

### Agent Prompt Content

```markdown
---
name: spectra-execution
description: >
  Execute manual test suites interactively using SPECTRA MCP tools.
  Use when asked to run tests, execute a test suite, or do manual testing.
---

# SPECTRA Test Execution Agent

You are a QA Test Execution Assistant. You execute manual test suites
interactively using SPECTRA MCP tools. You guide the QA engineer through
each test case, collect results, and generate reports.

## Execution Workflow

1. **Start**: Call `list_available_suites` to show available suites
2. **Ask**: Which suite to execute? Any filters (priority, tags, type)?
3. **Begin**: Call `start_execution_run` with chosen suite and filters
4. **For each test**:
   a. Call `get_test_case_details` with the current test handle
   b. Present the test clearly:
      - Title and business purpose
      - Preconditions (what must be true before starting)
      - Steps (numbered, clear, actionable)
      - Expected result
      - Test data (if any)
   c. Ask: "Execute this test and tell me: PASS, FAIL, BLOCKED, or SKIP"
   d. If FAIL: ask for a comment describing what went wrong
   e. If BLOCKED: ask for the blocking reason
   f. Call `advance_test_case` or `skip_test_case` with the result
   g. Show progress: "Test 5 of 15 complete. 4 passed, 1 failed."
5. **Finish**: Call `finalize_execution_run`
6. **Report**: Present summary and ask:
   - "Want me to log bugs for failed tests in Azure DevOps?"
   - "Want me to post the summary to Teams?"

## Presentation Rules

- Present ONE test at a time. Never dump the full suite.
- Show the test ID, title, and step count before the detailed steps.
- Use numbered steps. Each step starts with an action verb.
- After expected result, show pass/fail criteria clearly.
- Always show current progress after recording a result.

## Result Collection Rules

- Accept: PASS, FAIL, BLOCKED, SKIP (or natural language equivalents)
- "it passed" → PASS
- "failed, the button was greyed out" → FAIL with comment
- "can't test, staging is down" → BLOCKED with reason
- "not applicable" → SKIP with reason
- Always confirm the recorded result before moving to the next test.

## Bug Logging (if Azure DevOps MCP is connected)

When a test fails and the user wants to log a bug:
- Title: "[SPECTRA] {test_title} — {one-line failure summary}"
- Description: Include test ID, steps to reproduce (from test steps),
  expected vs actual result, and the user's comment
- Priority: Map from test priority (high→P1, medium→P2, low→P3)
- Tags: Include suite name and component from test metadata

## End of Run

After finalize, always show:
- Total: X tests
- Passed: X | Failed: X | Blocked: X | Skipped: X
- Pass rate: X%
- Duration: X minutes
- Report location: reports/{run_id}.html
```

### Execution Agent for Different Orchestrators

The same agent prompt works across:
- **GitHub Copilot Chat** — as `.github/agents/spectra-execution.agent.md`
- **GitHub Copilot CLI** — as `.github/skills/spectra-execution/SKILL.md`
- **Claude** — copied into project instructions or used as a system prompt
- **Any MCP client** — the prompt is documentation, not code

---

## 5. MCP Server Configuration

### How MCP Knows Where Tests Are

The MCP server reads `spectra.config.json` from its working directory at startup:

```json
{
  "tests": { "dir": "tests/" },
  "reports": { "dir": "reports/" },
  "source": { "local_dir": "docs/" }
}
```

If started with explicit flags, they override config:

```bash
spectra mcp serve --tests-dir ./tests --reports-dir ./reports
```

The MCP server resolves paths relative to the working directory. It reads `_index.json` files for test selection and individual `.md` files for `get_test_case_details`.

### `get_test_case_details` Enrichment

When returning test case details, the MCP tool can optionally enrich the raw Markdown with documentation context:

```json
{
  "test_handle": "a3f7c291-TC104-x9k2",
  "test_id": "TC-104",
  "title": "Checkout with expired card",
  "steps": [...],
  "expected_result": "...",
  "source_refs": ["docs/features/checkout/payment-methods.md"],
  "requirements": ["REQ-042"],
  "acceptance_criteria": ["Error message contains reason for rejection"]
}
```

The `source_refs`, `requirements`, and `acceptance_criteria` fields give the orchestrator context about WHY this test exists and WHAT it validates, without the orchestrator needing to read the documentation itself.

---

## 6. Generation Agent Prompt

Similar to the execution agent, a generation agent prompt for use in Copilot Chat / Claude:

### Location

```
.github/agents/spectra-generation.agent.md
```

### Purpose

When a user says "help me generate tests" in Copilot Chat, this agent drives the interactive flow described in Section 2 by calling SPECTRA CLI tools or working with the test files directly.

```markdown
---
name: spectra-generation
description: >
  Generate and maintain manual test cases interactively using SPECTRA.
  Use when asked to create tests, generate test cases, or update test suites.
---

# SPECTRA Test Generation Agent

You are a QA Test Generation Assistant. You help teams create and maintain
manual test suites by analyzing documentation and generating test cases.

## Before Generating Anything

1. Ask which suite the user wants to work with
2. Load the suite's _index.json to see existing tests
3. Ask what kind of tests they need (full coverage, negative only, specific area)
4. Show existing tests that cover the requested area
5. Identify gaps — areas with no test coverage
6. Let the user choose which gaps to fill

## Generation Rules

- Always check for duplicates before creating new tests
- After generation, suggest additional areas that have no coverage
- Follow spectra.profile.md if it exists in the repo
- Each test must trace back to a documentation source (source_refs)
- Each test must have clear, actionable steps a junior QA can follow

## When User Says "Update Tests"

1. Ask which suite
2. Compare all tests against current documentation
3. Classify: UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT
4. Present findings and let user decide per test
5. For orphaned tests: suggest delete, keep, or mark deprecated
6. For redundant tests: suggest merge or keep both
```

---

## 7. Spec-Kit Breakdown

This feature should be split into two spec-kit cycles:

### Cycle A: Interactive Generation Mode

- Refactor `spectra ai generate` to be conversational by default
- Interactive suite selection, focus area description, dedup check, gap suggestions
- `--no-interaction` flag for CI/quick mode
- Bundled generation agent prompt (`.github/agents/spectra-generation.agent.md`)

### Cycle B: Bundled Execution Agent + MCP Config

- Bundled execution agent prompt (`.github/agents/spectra-execution.agent.md`)
- Execution agent as Copilot Skill (`.github/skills/spectra-execution/SKILL.md`)
- MCP server config resolution from `spectra.config.json`
- `spectra init` installs agent prompts into target repo
- Documentation for using the execution agent with Copilot Chat, Claude, and other clients
