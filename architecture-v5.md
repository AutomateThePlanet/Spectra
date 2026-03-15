# AI-Native Manual Test Management System — Architecture Specification

**Version:** 3.0 (Final Consolidated)
**Status:** Ready for implementation scoping

---

## 1. Product Vision

### What This System Is

An **AI-native manual test management and execution system** that:

- Stores manual test cases as Markdown files in GitHub (the single source of truth)
- Uses an AI CLI agent (powered by GitHub Copilot SDK) to generate and maintain test cases from documentation
- Executes tests through a deterministic MCP-based execution engine
- Allows any LLM orchestrator (Copilot Chat, Claude, custom agents) to drive test execution without holding state
- Integrates with Azure DevOps, Jira, Teams, and Slack through the orchestrator-as-glue model — not by syncing data, but by letting the orchestrator call multiple MCP servers in one session

### What This System Is Not

This is **not a replacement for Azure DevOps**. Azure DevOps remains the system of record for boards, pipelines, work items, bugs, sprints, and enterprise governance. This system replaces **only the Test Case Management module** (Azure Test Plans, ~€52/user/month) with:

- Free Markdown storage in GitHub
- AI-powered test generation and maintenance using Copilot/Claude licenses teams already pay for
- A deterministic MCP execution engine that works from any LLM chat interface

### Positioning

```
Azure DevOps / Jira         ← enterprise tracking, bugs, boards, pipelines
      ↑ (bug logging via MCP)
LLM Orchestrator            ← Copilot Chat, Claude, custom agents
      ↓ (MCP tool calls)
This System                 ← test knowledge, generation, execution
      ↓ (reads)
GitHub                      ← source of truth for tests AND docs
```

The orchestrator is the glue. During test execution, a tester can fail a test and immediately say "log this as a bug in Azure DevOps, priority 2, assign to the checkout team" — the orchestrator calls the Azure DevOps MCP to create the work item. No sync, no mapping, no bidirectional state. Each system does what it's good at.

### Core Design Principles

1. GitHub is the source of truth for test definitions
2. Execution must be deterministic — the MCP server is the authoritative state machine
3. AI orchestrates but never manages state
4. The MCP API is orchestrator-agnostic — Copilot is the reference integration, not the only one
5. Tool responses must remain minimal to avoid context overflow
6. Every MCP tool call must be self-contained — the orchestrator must never need to remember prior calls
7. No bidirectional sync with external test management systems — one-directional integration only

---

## 2. Technology Stack

All core components are implemented in **C# and .NET**.

| Component          | Technology                |
| ------------------ | ------------------------- |
| CLI                | .NET CLI Application      |
| AI Runtime         | GitHub Copilot SDK (.NET) |
| MCP Server         | ASP.NET Core              |
| Execution Engine   | C# Library                |
| GitHub Integration | Octokit                   |
| Test Parsing       | Markdown Parser           |
| Execution Storage  | SQLite                    |
| Test Storage       | File System + GitHub      |

Optional:

| Component | Technology      |
| --------- | --------------- |
| Runner UI | React / Next.js |
| Styling   | Bootstrap       |

### Copilot SDK Role

The CLI uses the GitHub Copilot SDK as its AI runtime. The SDK provides the agent execution loop — planning, tool invocation, multi-turn conversations, streaming, and model routing — via the Copilot CLI in server mode over JSON-RPC. The CLI defines domain-specific tools and skills; the SDK handles the intelligence.

The SDK supports BYOK (Bring Your Own Key) for OpenAI, Azure AI Foundry, and Anthropic. This means the CLI works without a Copilot subscription when teams configure their own model access.

---

## 3. System Architecture

### Two Subsystems

The system consists of two independent subsystems that share the same test file format:

```
┌────────────────────────────────────┐
│     AI Test Generation CLI          │  ← Subsystem 1
│  (generate, update, analyze tests) │
│  Reads: docs/   Writes: tests/    │
├────────────────────────────────────┤
│     Copilot SDK + Custom Tools      │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│     MCP Execution Engine            │  ← Subsystem 2
│  (execute tests, track results)    │
│  Reads: tests/   Writes: reports/ │
├────────────────────────────────────┤
│     ASP.NET Core MCP Server         │
│     SQLite State Storage            │
└────────────────────────────────────┘
```

Subsystem 1 produces tests. Subsystem 2 consumes them. They are built and released independently. A team can use the CLI without the execution engine (executing tests manually or in their existing tool), and vice versa.

### Repository Structure

```
repo/
├── docs/                          # Source documentation (input for generation)
│   ├── features/
│   │   ├── checkout/
│   │   └── auth/
│   ├── api/
│   └── _index.md                  # Optional: curated doc map
├── tests/                         # Manual test case definitions (output)
│   ├── checkout/
│   │   ├── _index.json            # Auto-generated metadata index
│   │   └── *.md
│   └── auth/
│       ├── _index.json
│       └── *.md
├── reports/                       # Execution reports (gitignored by default)
├── .execution/                    # SQLite DB (gitignored)
├── .github/
│   ├── skills/                    # Copilot Agent Skills for test generation
│   │   ├── test-generation/
│   │   │   ├── SKILL.md
│   │   │   ├── test-template.md
│   │   │   └── examples/
│   │   ├── test-update/
│   │   │   └── SKILL.md
│   │   └── test-analysis/
│   │       └── SKILL.md
│   └── workflows/
│       └── validate-tests.yml     # CI: validate on PR
├── src/
│   ├── TestRunner.CLI/
│   ├── TestRunner.MCP/
│   ├── TestRunner.Core/
│   └── TestRunner.GitHub/
├── spec-kit/                      # Architecture decision records
├── runner-ui/                     # Optional web UI
└── testrunner.config.json
```

### .gitignore Requirements

```
.execution/
reports/
```

Reports and execution state are local and transient by default. Teams that want persistent reports should configure an export target in `testrunner.config.json`.

---

## 4. Two-Folder Model

The CLI operates on a clear input/output contract: **read from docs, write to tests**.

### Configuration

```json
{
  "source": {
    "mode": "local",
    "local_dir": "docs/",
    "space_name": null
  },
  "tests": {
    "dir": "tests/"
  }
}
```

### Source Folder (Input)

Contains all documentation describing how the system works. No enforced structure — the agent discovers and navigates it.

```
docs/
├── features/
│   ├── checkout/
│   │   ├── checkout-flow.md
│   │   ├── payment-methods.md
│   │   └── refund-policy.md
│   └── auth/
│       └── login-flows.md
├── api/
│   └── rest-api-reference.md
└── _index.md                      # Optional curated doc map
```

### Tests Folder (Output)

```
tests/
├── checkout/
│   ├── _index.json
│   └── *.md
└── auth/
    ├── _index.json
    └── *.md
```

### Knowledge Source Modes

**Mode 1: Local Documentation Folder (Default)**
The CLI reads Markdown files from the source folder on disk. Works offline.

**Mode 2: GitHub Copilot Spaces**
For teams that maintain documentation in Copilot Spaces, the CLI can use a Space as the source. Spaces are accessible through the GitHub MCP server's dedicated Spaces toolset. The `--space` flag overrides the configured mode for any CLI command.

Spaces mode is a progressive enhancement. Local folder mode is the reliable baseline that always works. If Spaces access fails at runtime, the CLI logs a warning and prompts to fall back to local mode.

| Aspect              | Local Folder     | Copilot Spaces                        |
| ------------------- | ---------------- | ------------------------------------- |
| Works offline       | Yes              | No                                    |
| Auto-syncs          | Manual (git pull)| Automatic                             |
| Non-file content    | No               | Yes (issues, PRs, notes, images)      |
| Requires subscription | No (with BYOK)| Yes (Copilot)                         |

---

## 5. Test Case Format

Manual test cases are stored as Markdown files in `tests/{suite}/*.md`.

```markdown
---
id: TC-102
priority: high
type: manual
tags: [payments, negative]
component: checkout
preconditions: User is logged in with a valid account
environment: [staging, uat]
estimated_duration: 5m
depends_on: TC-101
requirements:
  - REQ-042: "Payment with expired card must be rejected"
  - US-15: "As a user, I want clear error messages on payment failure"
acceptance_criteria:
  - "Error message contains reason for rejection"
  - "User remains on checkout page"
  - "No charge is created"
source_refs: [docs/features/checkout/payment-methods.md]
automated_by: []
related_work_items: [AB#1234]
---

# Checkout with expired card

## Preconditions
- User is logged in
- Cart contains at least one item

## Steps
1. Navigate to checkout
2. Enter expired card details (exp: 01/2020)
3. Click "Pay Now"

## Expected Result
- Payment is rejected
- Error message displays: card expired
- User remains on checkout page

## Test Data
- Card number: 4111 1111 1111 1111
- Expiry: 01/2020
```

---

## 6. Test Metadata Schema

### Core Fields (validated by engine)

| Field    | Type     | Required | Description                      |
| -------- | -------- | -------- | -------------------------------- |
| id       | string   | yes      | Unique identifier (e.g., TC-102) |
| priority | enum     | yes      | high, medium, low                |
| type     | enum     | yes      | manual, automated, both (default: manual) |
| tags     | string[] | no       | Filterable labels                |
| component| string   | no       | System component under test      |

### Extended Fields (optional, passed through)

| Field              | Type     | Description                                    |
| ------------------ | -------- | ---------------------------------------------- |
| preconditions      | string   | Human-readable precondition summary            |
| environment        | string[] | Valid environments (staging, uat, prod)         |
| estimated_duration | string   | Estimated execution time (e.g., 5m, 1h)       |
| depends_on         | string   | Test ID that must pass before this one         |
| requirements       | string[] | Traced requirement/story IDs (e.g., REQ-042, US-15) |
| acceptance_criteria| string[] | Acceptance criteria from the requirement       |
| source_refs        | string[] | Doc files this test was generated from         |
| automated_by       | string[] | Paths to automation test files that cover this test case |
| related_work_items | string[] | Azure DevOps/Jira IDs (e.g., AB#1234)         |

### Traceability Model

The metadata fields create a full traceability chain:

```
Documentation (docs/)
  → source_refs: which doc this test was generated from
  → requirements: which requirement/story this test validates
  → acceptance_criteria: specific criteria being verified

Test Case (tests/)
  → type: manual, automated, or both
  → automated_by: paths to automation code covering this test

Automation Code (e.g., BELLATRIX tests)
  → [TestCase("TC-102")] attribute links back to manual test
```

Bidirectional linking: `automated_by` in the Markdown points to automation code; `[TestCase("TC-102")]` in the automation code points back. `spectra ai analyze --coverage` scans both directions and reports mismatches.

### Extension Mechanism

Teams can add custom metadata under a `custom` namespace:

```yaml
custom:
  regulatory: true
  review_cycle: Q2-2026
```

The engine passes custom fields through to reports without validation.

---

## 7. Metadata Index

Each suite folder contains an auto-generated `_index.json`.

```json
{
  "suite": "checkout",
  "generated_at": "2026-03-13T10:00:00Z",
  "test_count": 42,
  "summary": {
    "manual": 28,
    "automated": 10,
    "both": 4
  },
  "tests": [
    {
      "id": "TC-101",
      "file": "checkout-happy-path.md",
      "title": "Checkout with valid Visa card",
      "priority": "high",
      "type": "both",
      "tags": ["smoke", "payments"],
      "component": "checkout",
      "depends_on": null,
      "requirements": ["REQ-041"],
      "source_refs": ["docs/features/checkout/checkout-flow.md"],
      "automated_by": ["tests/automation/checkout/HappyPathTest.cs"]
    }
  ]
}
```

### Rules

- Rebuilt by `testrunner index` or `testrunner validate`
- The MCP server reads the index for test selection — never parses all Markdown files at runtime
- Committed to the repo (deterministic output, helps CI)
- CI validates that the index is up to date on every PR

---

## 8. Test Suites

Suites are defined by folder structure.

```
tests/
├── checkout/
├── authentication/
└── orders/
```

Suite name = folder name.

### Test Selection

```
suite (folder) + metadata filters (from index)
```

Process:
1. Read `_index.json` for the target suite
2. Apply metadata filters (priority, tags, component, environment)
3. Resolve dependency ordering (if `depends_on` is used)
4. Create execution queue

---

# SUBSYSTEM 1: AI TEST GENERATION CLI

---

## 9. Test Generation Profile

A `spectra.profile.md` file at the repo root provides natural language instructions that customize how the AI generates test cases. The profile is optional — if absent, generation uses built-in defaults.

### Why Markdown

The profile is instructions for an AI agent. Natural language is the most effective format. JSON schema for "how detailed should steps be" is either too rigid or too complex. Markdown allows both structure and free-form guidance.

### Location

```
repo/
├── spectra.profile.md              ← repo-wide generation profile
├── spectra.config.json
└── tests/
    ├── checkout/
    │   ├── _profile.md             ← optional suite-level override
    │   ├── _index.json
    │   └── *.md
    └── auth/
        └── *.md                    ← uses repo-wide profile
```

Suite-level `_profile.md` overrides the repo-wide profile for that suite only. If neither exists, built-in defaults apply.

### Profile Content

The profile can customize any aspect of test generation:

- **Detail level**: how granular test steps should be
- **Scenario coverage**: minimum negative scenarios, security scenarios, boundary tests per feature
- **Domain-specific rules**: extra scenarios for payments, auth, PII/GDPR
- **Formatting**: bullet points vs paragraphs, action verb requirements, test data requirements
- **Priority rules**: which categories get which priority
- **Tag conventions**: required tags, naming patterns
- **Exclusions**: what NOT to generate

### `spectra init-profile` Command

Interactive questionnaire that generates `spectra.profile.md`:

```bash
spectra init-profile

? How detailed should test steps be?
  (1) High-level  (2) Detailed  (3) Very detailed

? Minimum negative scenarios per feature? > 3

? Do you handle payments? (y/n)
? Do you handle authentication? (y/n)
? Do you handle personal data (GDPR/PII)? (y/n)

? Default priority? (1) high  (2) medium  (3) low

? Expected Result format? (1) Bullet points  (2) Paragraphs

? Areas to EXCLUDE from generation?
  > third-party integrations, load testing

✓ Generated spectra.profile.md
```

### How It's Used

The `spectra ai generate` command automatically loads the profile:

1. Load `spectra.profile.md` (repo-wide)
2. If suite has `_profile.md`, use it instead
3. Include profile content in the AI session system context alongside the SKILL.md
4. Agent follows both SKILL.md (format, tools, process) and profile (content, quality, style rules)

The profile applies only to generation. `spectra ai update` does not use it — updates compare existing tests against documentation, not generate from scratch.

### Configuration

```json
{
  "generation": {
    "profile": "spectra.profile.md",
    "suite_profile_name": "_profile.md"
  }
}
```

---

## 10. CLI Architecture

### Design: Deterministic Workflow Shell + Copilot SDK Agent Steps

The CLI implements deterministic command workflows where specific steps invoke the Copilot SDK for AI reasoning. The CLI controls the flow; the SDK controls the intelligence within each step.

The agent never writes to the filesystem directly. All output goes through custom tool handlers that validate before accepting.

```
CLI Command
  → Load config, indexes, document map
  → Create CopilotSession (model, tools, skills)
  → Agent discovers docs, generates/analyzes tests
  → Agent calls batch tools → CLI validates
  → CLI presents results for review
  → CLI writes accepted changes
```

### Interaction Model: Command-First with Structured Review

Every operation is a named command with explicit parameters. No chat loop. CI-friendly.

Where human judgment is needed, the CLI enters a structured review flow — guided accept/reject/edit, not free-form chat.

---

## 11. Source Document Discovery

The agent doesn't load all documentation files at once. It uses a two-phase discovery pattern.

### Phase 1: Build Document Map (CLI, deterministic)

The CLI scans the source folder and builds a lightweight map:

```json
{
  "doc_count": 12,
  "total_size_kb": 340,
  "documents": [
    {
      "path": "docs/features/checkout/checkout-flow.md",
      "title": "Checkout Flow",
      "size_kb": 28,
      "headings": ["Overview", "Happy Path", "Error Handling", "Edge Cases"],
      "first_200_chars": "The checkout flow handles..."
    }
  ]
}
```

Built deterministically: scan files, extract first H1 as title, extract H2s as headings, take first 200 characters. Small enough to fit in context for any reasonable doc folder.

### Phase 2: Agent Selects Relevant Documents

The agent receives the document map plus suite-specific hints from config (`relevant_docs`), then calls `load_source_document` for only the files it needs.

### Optional: Curated Doc Map

Teams can create `docs/_index.md` that explicitly maps documents to components:

```markdown
# Documentation Index

## Checkout
- features/checkout/checkout-flow.md - Main checkout user flow
- features/checkout/payment-methods.md - Supported payment types
- api/rest-api-reference.md#payments - Payment API endpoints
```

If present, the agent uses this as a guide instead of discovering from the raw file listing. Recommended for large doc folders (50+ files).

---

## 12. Provider Chain

### Problem

Copilot subscriptions have premium request quotas. Batch generation can deplete quota quickly. Teams need seamless fallback to an external model.

### Solution: Ordered Provider Array

```json
{
  "ai": {
    "providers": [
      {
        "name": "copilot",
        "model": "gpt-5",
        "enabled": true,
        "priority": 1
      },
      {
        "name": "anthropic",
        "model": "claude-sonnet-4-5",
        "api_key_env": "ANTHROPIC_API_KEY",
        "enabled": true,
        "priority": 2
      }
    ],
    "fallback_strategy": "auto"
  }
}
```

### Fallback Strategies

| Strategy       | Behavior                                                              |
| -------------- | --------------------------------------------------------------------- |
| `auto`         | Silent switch on failure (rate limit, quota, auth error). Log the switch. |
| `manual`       | Prompt user before switching.                                         |
| `primary_only` | Never fall back. Fail with clear error.                               |

The Copilot SDK supports BYOK natively — the fallback provider uses the same SDK, same tools, same skills. Only the model changes.

The `--provider` flag overrides for any single run:

```bash
testrunner ai generate --suite checkout --provider anthropic
```

---

## 13. Batch Generation

### `testrunner ai generate`

```
Input:
  --suite <name>           Target suite (required)
  --count <n|unlimited>    Max tests (default: from config, typically 15)
  --priority <level>       Auto-assign priority
  --tags <tag1,tag2>       Auto-assign tags
  --space <name>           Use Copilot Space as source (overrides config)
  --provider <name>        Force specific AI provider
  --dry-run                Validate without writing
  --no-review              Skip interactive review (for CI)
```

### Workflow

```
1. LOAD CONTEXT (CLI)
   ├── Read testrunner.config.json
   ├── Read tests/{suite}/_index.json
   ├── Build document map from docs/
   ├── Read suite hints (relevant_docs)
   └── Select provider from chain

2. CREATE SESSION (SDK)
   ├── Provider from chain (or --provider override)
   ├── Tools: get_document_map, load_source_document,
   │   batch_write_tests, check_duplicates_batch,
   │   get_next_test_ids, read_test_index
   ├── Skill: .github/skills/test-generation/SKILL.md
   └── System context: format spec, suite config, existing count

3. AGENT LOOP (SDK handles)
   ├── Agent calls get_document_map → sees all docs
   ├── Agent reads suite hints → loads relevant docs
   ├── Agent loads additional docs if needed
   ├── Agent generates test batch
   ├── Agent calls check_duplicates_batch → flags conflicts
   ├── Agent calls batch_write_tests → CLI validates entire batch
   └── Agent fixes invalid tests and resubmits

4. REVIEW (CLI)
   ├── Summary: 18 valid, 1 duplicate, 1 invalid
   ├── User reviews (accept all / one by one / view duplicates)
   └── Collect final set

5. WRITE (CLI)
   ├── Write accepted .md files to tests/{suite}/
   ├── Rebuild _index.json
   ├── Create branch + commit (if auto_branch enabled)
   └── Print summary
```

### Batch Tool: `batch_write_tests`

The agent submits all generated tests in a single tool call. The handler validates the entire batch and returns per-test results:

```json
{
  "submitted": 12,
  "valid": 10,
  "duplicates": 1,
  "invalid": 1,
  "details": [
    { "id": "TC-201", "status": "valid" },
    { "id": "TC-203", "status": "duplicate", "similar_to": "TC-108" },
    { "id": "TC-204", "status": "invalid", "reason": "Missing expected result" }
  ]
}
```

### Batch Review UX

```
Generated 18 tests for suite: checkout

Summary:
  ✓ 15 valid tests
  ⚠ 2 potential duplicates
  ✗ 1 invalid (missing expected result)

Options:
  (r)eview one by one    (a)ccept all valid    (v)iew duplicates
  (e)xport to file       (q)uit
```

---

## 14. Batch Update

### `testrunner ai update`

```
Input:
  --suite <name>           Target suite (required, or --all)
  --all                    Update all suites
  --diff <git-range>       Also consider code changes
  --space <name>           Use Copilot Space as source
  --provider <name>        Force specific AI provider
  --dry-run                Show changes without applying
  --no-review              Skip interactive review
```

### Workflow

The update command sweeps all tests in a suite folder, compares against current documentation, and proposes batch changes.

```
1. Load ALL tests in target suite (full content)
2. Build document map
3. Create session with batch_read_tests + batch_propose_updates tools
4. Agent loads docs, compares each test, classifies:
   - UP_TO_DATE: matches current documentation
   - OUTDATED: documentation changed, test needs update
   - ORPHANED: no matching documentation (feature removed?)
   - REDUNDANT: duplicates another test
5. Agent calls batch_propose_updates with findings
6. CLI presents batch diff
7. User reviews changes
8. Write accepted updates, rebuild index
```

### Context Budget for Large Suites

- Under 50 tests: single session, load all content
- 50–200 tests: enable SDK infinite sessions with auto-compaction, process in chunks of 20
- 200+ tests: multiple independent sessions, one per chunk of ~30, merge results at CLI level

---

## 15. Coverage Analysis

### `testrunner ai analyze`

```
Input:
  --suite <name>           Target suite (or omit for all)
  --space <name>           Use Copilot Space as source
  --provider <name>        Force specific AI provider
  --output <path>          Report output path
  --format <md|json>       Report format (default: md)
```

Produces a coverage report: uncovered areas, redundant tests, priority suggestions, component coverage gaps. No file modifications — pure analysis.

---

## 16. CLI Tool Registry

### Source Navigation Tools

| Tool                   | Purpose                                                    |
| ---------------------- | ---------------------------------------------------------- |
| `get_document_map`     | Lightweight listing of all docs (paths, titles, headings, sizes) |
| `load_source_document` | Full content of a specific doc (capped at max_file_size_kb) |
| `search_source_docs`   | Keyword search across doc titles and headings              |

### Test Index Tools

| Tool                     | Purpose                                           |
| ------------------------ | ------------------------------------------------- |
| `read_test_index`        | Returns _index.json metadata for a suite          |
| `batch_read_tests`       | Full content of all tests in a suite (or chunk)   |
| `get_next_test_ids`      | Allocates N sequential test IDs                   |
| `check_duplicates_batch` | Checks array of titles/steps against index        |

### Write Tools

| Tool                      | Purpose                                            |
| ------------------------- | -------------------------------------------------- |
| `batch_write_tests`       | Submits batch of new tests; returns validation     |
| `batch_propose_updates`   | Submits batch of update proposals for existing tests |

---

## 17. Agent Skills

The CLI ships with Copilot Agent Skills in `.github/skills/`. Skills are loaded into the agent's context per the Agent Skills standard — they work across Copilot CLI, VS Code, and the SDK.

### test-generation SKILL.md (structure)

```markdown
---
name: test-generation
description: >
  Generate manual test cases as Markdown files with YAML frontmatter.
  Use when asked to create new tests from documentation.
---

# Test Case Generation

## Output Format
Every test case MUST be valid Markdown with YAML frontmatter.
Use `batch_write_tests` to submit all tests. NEVER write files directly.

## Required Frontmatter Fields
- id: Use `get_next_test_ids` to allocate IDs
- priority: high | medium | low
- source_refs: document paths this test was generated from

## Before Generating
1. Call `get_document_map` to see available documentation
2. Call `read_test_index` to see existing tests
3. Call `check_duplicates_batch` before submitting

## Quality Rules
- Each test covers ONE scenario
- Include negative and boundary tests
- Steps must be atomic — one action per step
- Test data should be explicit
- Auto-populate source_refs from the docs you read
```

---

## 18. CLI Commands (Complete)

### Core

```
spectra init                 Initialize repo (config, folders, skills, .gitignore)
spectra init-profile         Interactive questionnaire to generate spectra.profile.md
spectra validate             Validate all test files and indexes
spectra index                Rebuild _index.json for all suites
spectra list                 List suites and test counts
spectra show <test-id>       Display a test case
spectra config               Show effective configuration
```

### AI Generation and Maintenance

```
spectra ai generate          Batch generate tests for a suite
spectra ai update            Batch update tests against current docs
spectra ai analyze           Coverage and quality analysis
spectra ai chat              Interactive exploratory chat (Phase 3)
```

### Validation Rules (`testrunner validate`)

- All test files have valid YAML frontmatter
- All `id` fields are unique across the entire repo
- All `priority` values are in the allowed enum
- All `depends_on` references point to existing test IDs
- All `_index.json` files are up to date
- Exit code 0 = valid, exit code 1 = errors found (CI-ready)

---

# SUBSYSTEM 2: MCP EXECUTION ENGINE

---

## 19. Execution Engine

The execution engine is a deterministic state machine with explicit states and validated transitions.

### Run States

```
CREATED → RUNNING → PAUSED → RUNNING → COMPLETED
                  ↘ CANCELLED
         (timeout) → ABANDONED
```

| Transition          | Trigger                                        |
| ------------------- | ---------------------------------------------- |
| CREATED → RUNNING   | `start_execution_run`                          |
| RUNNING → PAUSED    | `pause_execution_run`                          |
| PAUSED → RUNNING    | `resume_execution_run`                         |
| RUNNING → COMPLETED | `finalize_execution_run` (all tests done)      |
| RUNNING → CANCELLED | `cancel_execution_run`                         |
| PAUSED → ABANDONED  | Configurable timeout (default: 72h)            |

### Test States

```
PENDING → IN_PROGRESS → PASSED / FAILED / BLOCKED / SKIPPED
```

### Transition Validation

The MCP server rejects any tool call that violates state transitions:

- Cannot call `advance_test_case` on a PAUSED run
- Cannot call `finalize_execution_run` if tests remain PENDING (unless `force: true`)
- Cannot record a result for a test not IN_PROGRESS
- If current test FAILED and has dependents, auto-skips dependents with reason

---

## 20. Execution State Storage

SQLite database at `.execution/testrunner.db`.

### Why SQLite

- Atomic writes — no corrupted state from crashes
- Concurrent read access — multiple tools can query safely
- Zero deployment overhead — single file
- Query capability for run history and filtering

### Conceptual Schema

```
runs
  run_id        TEXT PRIMARY KEY  (UUID)
  suite         TEXT
  status        TEXT
  started_at    DATETIME
  started_by    TEXT
  environment   TEXT
  filters       TEXT (JSON)
  updated_at    DATETIME

test_results
  run_id        TEXT
  test_id       TEXT
  test_handle   TEXT
  status        TEXT
  notes         TEXT
  started_at    DATETIME
  completed_at  DATETIME
  attempt       INTEGER
```

Run IDs are UUIDs.

---

## 21. Test Handle Pattern

Opaque, non-guessable handles prevent context explosion and handle forgery.

```
Format: {run_uuid_prefix}-{test_id}-{random_suffix}
Example: a3f7c291-TC104-x9k2
```

Validated on every tool call. Rejected if:
- Not belonging to the active run
- Test is not IN_PROGRESS
- Handle already resolved

### Progressive Disclosure

`get_test_case_details` returns structured content with step count:

```json
{
  "test_handle": "a3f7c291-TC104-x9k2",
  "test_id": "TC-104",
  "title": "Checkout with expired card",
  "step_count": 3,
  "preconditions": "User is logged in, cart has items",
  "steps": [
    { "number": 1, "action": "Navigate to checkout" },
    { "number": 2, "action": "Enter expired card details" },
    { "number": 3, "action": "Click Pay Now" }
  ],
  "expected_result": "Payment rejected, error displayed"
}
```

---

## 22. MCP Server

### Responsibilities

- Test selection via metadata index
- Execution queue management
- State machine enforcement
- Result storage
- Report generation

### Self-Contained Responses

Every response includes context the orchestrator needs without remembering history:

```json
{
  "run_status": "RUNNING",
  "progress": "8/15",
  "next_expected_action": "get_test_case_details"
}
```

---

## 23. MCP Tool API

### Run Management

| Tool                    | Description                                          |
| ----------------------- | ---------------------------------------------------- |
| `list_available_suites` | Returns all suite names and test counts from indexes |
| `start_execution_run`   | Creates a new run for a suite with filters           |
| `resume_execution_run`  | Resumes a PAUSED run by run_id                       |
| `pause_execution_run`   | Pauses the current run, preserving state             |
| `cancel_execution_run`  | Cancels a run, preserving partial results            |
| `get_execution_status`  | Returns run state, progress, current test info       |
| `finalize_execution_run`| Completes the run, generates report                  |

### Test Execution

| Tool                    | Description                                          |
| ----------------------- | ---------------------------------------------------- |
| `get_test_case_details` | Returns full test content for a given handle         |
| `advance_test_case`     | Records result for current test, returns next handle |
| `skip_test_case`        | Skips current test with reason, returns next handle  |
| `retest_test_case`      | Re-queues a completed test for another attempt       |
| `add_test_note`         | Attaches a note without changing status              |

### Reporting

| Tool                    | Description                                          |
| ----------------------- | ---------------------------------------------------- |
| `get_execution_summary` | Returns progress stats for the active run            |
| `get_run_history`       | Returns past runs with basic summary info            |

### `advance_test_case` — Core Atomic Tool

Atomically records result, checks dependencies, advances queue, returns next handle.

Request:
```json
{
  "test_handle": "a3f7c291-TC104-x9k2",
  "status": "PASSED",
  "notes": "Worked as expected"
}
```

Response:
```json
{
  "recorded": { "test_id": "TC-104", "status": "PASSED" },
  "next": {
    "test_handle": "a3f7c291-TC105-m3p7",
    "test_id": "TC-105",
    "title": "Checkout with insufficient funds"
  },
  "run_status": "RUNNING",
  "progress": "5/15",
  "next_expected_action": "get_test_case_details"
}
```

When no more tests:
```json
{
  "recorded": { "test_id": "TC-119", "status": "PASSED" },
  "next": null,
  "run_status": "RUNNING",
  "progress": "15/15",
  "next_expected_action": "finalize_execution_run"
}
```

### Error Responses

```json
{
  "error": "INVALID_TRANSITION",
  "message": "Cannot advance: run is PAUSED. Call resume_execution_run first.",
  "current_run_status": "PAUSED",
  "next_expected_action": "resume_execution_run"
}
```

---

## 24. Execution Flow

### Happy Path

```
list_available_suites
        ↓
start_execution_run (suite, filters)
        ↓
get_test_case_details (first handle from start response)
        ↓
    User executes test
        ↓
advance_test_case (handle, PASSED/FAILED)
        ↓
get_test_case_details (next handle)
        ↓
    ... repeat ...
        ↓
finalize_execution_run
```

### Interrupted Session

```
Session 1:
    start_execution_run → run tests → session lost

Session 2:
    get_execution_status (run_id) → sees RUNNING
    resume_execution_run (run_id) → continues
    advance_test_case → ... → finalize_execution_run
```

### Cross-MCP Integration (Orchestrator as Glue)

```
User in Copilot Chat:
  "Run the checkout smoke tests"
    → TestRunner MCP: start_execution_run

  walks through tests...
  test TC-104 fails

  "Log this as a bug, priority 2, assign to checkout team"
    → Azure DevOps MCP: create_work_item

  "Post the summary to the QA Teams channel"
    → Teams MCP: send_message

  finalize run
    → TestRunner MCP: finalize_execution_run
```

No sync between systems. The orchestrator calls each MCP server as needed.

---

## 25. Reports

### Storage

```
reports/
├── {run_id}.json              # Machine-readable report
├── {run_id}.html              # Self-contained HTML report (inline CSS, no external assets)
└── {run_id}/
    └── attachments/           # Screenshots and other files
        ├── TC-102-failure.png
        └── TC-105-screenshot.png
```

Gitignored by default. Configurable persistence:

```json
{
  "reports": {
    "persistence": "local",
    "export_path": null,
    "formats": ["json", "html"],
    "attachments": {
      "storage": "local"
    }
  }
}
```

Options for `persistence`: `local` (default), `export` (copy to configured path after finalization).
Options for `attachments.storage`: `local` (default, filesystem), `azure-blob` (Phase 3).

### Report Generation

`finalize_execution_run` generates both JSON and HTML reports automatically:
- **JSON**: Machine-readable, used by dashboard and CI integrations
- **HTML**: Self-contained, opens directly in any browser, no server needed. Uses inline CSS and embedded data. Includes pass/fail summary, per-test results with notes, duration, and links to attachments.

### Attachments

MCP tool `attach_file` accepts a local file path and associates it with the current test in the active run. Files are copied to `reports/{run_id}/attachments/` with a name prefix of the test ID.

### Report Structure (JSON)

```json
{
  "run_id": "a3f7c291-...",
  "suite": "checkout",
  "environment": "staging",
  "started_at": "2026-03-13T10:00:00Z",
  "completed_at": "2026-03-13T11:30:00Z",
  "executed_by": "anton@automate-the-planet.com",
  "status": "COMPLETED",
  "summary": {
    "total": 15,
    "passed": 12,
    "failed": 2,
    "skipped": 1,
    "blocked": 0
  },
  "results": [
    {
      "test_id": "TC-101",
      "status": "PASSED",
      "attempt": 1,
      "duration_seconds": 120,
      "notes": null,
      "attachments": []
    },
    {
      "test_id": "TC-102",
      "status": "FAILED",
      "attempt": 1,
      "duration_seconds": 95,
      "notes": "Error message shows generic text instead of 'card expired'",
      "attachments": ["reports/a3f7c291/attachments/TC-102-failure.png"]
    }
  ]
}
```

---

## 26. User Identity

### Resolution (priority order)

1. Explicit `--user` flag or `user` param on `start_execution_run`
2. Git config (`user.email`)
3. OS username as fallback

Recorded on the run and on each test result.

---

## 27. Concurrency Model

- Same user, different suites: Allowed
- Same user, same suite: Blocked (must finalize/cancel/timeout first)
- Different users, same suite: Allowed (independent runs)

---

## 28. Security

### Path Sanitization

All suite names and file paths from orchestrators are sanitized: reject `..`, `/`, `\`, null bytes. Resolve relative to `tests/` root.

### Handle Validation

Handles contain a random component. Single-use per attempt. Expired or foreign handles return clear errors.

### Orchestrator Guardrails

| Risk                       | Mitigation                                          |
| -------------------------- | --------------------------------------------------- |
| Out-of-order tool calls    | State machine rejects + `next_expected_action`      |
| Duplicate result submission| Rejects for already-resolved tests                  |
| Fabricated handles         | Validation on every call                            |
| Context loss mid-run       | Every response self-contained; `resume` available   |
| Skipping result recording  | `advance_test_case` requires result to proceed      |

---

# CONFIGURATION

---

## 29. Configuration File

### `testrunner.config.json`

```json
{
  "source": {
    "mode": "local",
    "local_dir": "docs/",
    "space_name": null,
    "doc_index": "docs/_index.md",
    "max_file_size_kb": 50,
    "include_patterns": ["**/*.md"],
    "exclude_patterns": ["**/CHANGELOG.md"]
  },

  "tests": {
    "dir": "tests/",
    "id_prefix": "TC",
    "id_start": 100
  },

  "ai": {
    "providers": [
      {
        "name": "copilot",
        "model": "gpt-5",
        "enabled": true,
        "priority": 1
      },
      {
        "name": "anthropic",
        "model": "claude-sonnet-4-5",
        "api_key_env": "ANTHROPIC_API_KEY",
        "enabled": true,
        "priority": 2
      }
    ],
    "fallback_strategy": "auto"
  },

  "generation": {
    "default_count": 15,
    "require_review": true,
    "duplicate_threshold": 0.6,
    "categories": ["happy_path", "negative", "boundary", "integration"],
    "profile": "spectra.profile.md",
    "suite_profile_name": "_profile.md"
  },

  "update": {
    "chunk_size": 30,
    "require_review": true
  },

  "suites": {
    "checkout": {
      "component": "checkout-service",
      "relevant_docs": ["features/checkout/", "api/rest-api-reference.md"],
      "default_tags": ["checkout"],
      "default_priority": "high"
    }
  },

  "git": {
    "auto_branch": true,
    "branch_prefix": "testrunner/",
    "auto_commit": true,
    "auto_pr": false
  },

  "reports": {
    "persistence": "local",
    "export_path": null
  },

  "validation": {
    "required_fields": ["id", "priority"],
    "allowed_priorities": ["high", "medium", "low"],
    "max_steps": 20,
    "id_pattern": "^TC-\\d{3,}$"
  }
}
```

---

# NON-FUNCTIONAL REQUIREMENTS

---

## 30. Non-Functional Requirements

| Requirement           | Detail                                                    |
| --------------------- | --------------------------------------------------------- |
| Deterministic         | Same inputs produce same execution queue                  |
| Offline-capable       | Full execution works without network after initial clone   |
| GitHub-native         | Tests live in Git, CI validates schema                    |
| Orchestrator-agnostic | MCP API works with any LLM or tool caller                |
| Open-source friendly  | Clear docs, contribution guide, ADRs                     |
| LLM-safe             | Handles, progressive disclosure, self-contained responses |
| Concurrent            | Multiple users can execute independently                  |
| Crash-resilient       | SQLite ensures no state loss on failure                   |
| Provider-flexible     | Copilot + BYOK fallback, no single-vendor lock-in         |

---

# DEVELOPMENT PHASES

---

## 31. Development Phases

### Phase 1: AI Test Generation CLI

The core product. Ship this first, get it used, iterate.

**Deliverables:**
- Markdown test format with full metadata schema (including type, requirements, acceptance_criteria, automated_by)
- `_index.json` per suite, `spectra validate`, `spectra index`
- `spectra init` (scaffolds config, folders, skills, .gitignore)
- `spectra init-profile` (interactive questionnaire → `spectra.profile.md`)
- Two-folder model (`docs/` → `tests/`)
- Document map builder + selective loading
- `spectra ai generate` with batch workflow (loads profile before generation)
- `spectra ai update` with suite-sweep
- `spectra ai analyze`
- Provider chain with auto-fallback (Copilot + BYOK)
- Batch review UX (summary-first)
- test-generation + test-update SKILL.md files
- `source_refs` auto-population in frontmatter
- GitHub Actions workflow for validation on PR
- `spectra list`, `spectra show`, `spectra config`

**Exit criteria:** A team can install the CLI, point it at their docs folder, and generate a complete test suite with one command.

### Phase 2: MCP Execution Engine

Only after the CLI is stable and useful on its own.

**Deliverables:**
- MCP server with full state machine
- `advance_test_case` as core atomic tool
- All run management tools (start, pause, resume, cancel, finalize)
- SQLite execution storage
- Test handles with validation
- Dependency-based auto-skip
- JSON + HTML report generation at finalize
- Screenshot/attachment support (local filesystem)
- Run history
- User identity integration
- Concurrency rules enforcement
- Test filtering by `type` (run only manual tests, skip automated)

**Exit criteria:** A tester can execute a full test suite from Copilot Chat or Claude using only MCP tool calls, and receive both JSON and HTML reports with attached screenshots.

### Phase 3: Dashboard, Coverage, and Integrations

**Deliverables:**

*Dashboard:*
- `spectra dashboard` CLI command generates static HTML site from indexes + reports
- Suite browser: navigate suites, filter by priority/tags/component/type
- Test case viewer: rendered Markdown with traceability metadata
- Run history: past runs with pass/fail summary and drill-down
- Coverage mind map: tree visualization showing docs → requirements → tests → automation, color-coded by coverage status (green=automated, yellow=manual only, red=no tests)
- GitHub OAuth authentication: only users with repo access can view the dashboard
- Deployment via GitHub Action to Cloudflare Pages (serverless OAuth callback function)

*Coverage Analysis:*
- `spectra ai analyze --coverage`: scans both test Markdown (`automated_by` field) and automation code (`[TestCase("TC-xxx")]` attributes) for bidirectional link verification
- Reports: unlinked manual tests (no automation), unlinked automation tests (no manual test), broken links (file references that don't exist), coverage percentage per suite/component

*Integrations:*
- Document cross-MCP patterns (Azure DevOps + SPECTRA + Teams)
- Copilot Spaces as knowledge source (`--space` flag)
- `spectra ai chat` interactive mode
- Azure Blob Storage for attachments (`attachments.storage: azure-blob`)
- Report export targets
- Notification patterns (Teams/Slack via orchestrator)

**Exit criteria:** A team can browse tests and run results in a web dashboard, see automation coverage gaps in a mind map, and deploy the dashboard with one GitHub Action.

---

## 32. Future Extensions

- Risk-based test selection
- AI coverage analysis against production usage data
- Change impact analysis (code change → affected tests)
- Test flakiness detection (pass/fail history tracking)
- Parallel execution support (split suite across testers)
- Embedding-based dedup for suites with 500+ tests
- CI mode for automated generation pipelines
- Automation code generation from manual test cases (manual → BELLATRIX test stub)
- Requirements import from Azure DevOps/Jira (populate requirements field automatically)
- Coverage trend tracking over time (historical mind maps)
