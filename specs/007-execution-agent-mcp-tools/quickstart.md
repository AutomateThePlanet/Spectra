# Quickstart: Bundled Execution Agent & MCP Data Tools

**Branch**: `007-execution-agent-mcp-tools` | **Date**: 2026-03-19

## Overview

This feature adds:
1. **Bundled execution agent** - Ready-to-use prompt for test execution via AI assistants
2. **MCP data tools** - Deterministic tools for validation, indexing, and coverage analysis
3. **Documentation** - Guides for using the agent with different orchestrators

---

## Quick Setup

### 1. Initialize Repository with Agent Files

```bash
# In your repository root
spectra init

# Or with force to update existing agent files
spectra init --force
```

This creates:
- `.github/agents/spectra-execution.agent.md` (Copilot Chat)
- `.github/skills/spectra-execution/SKILL.md` (Copilot CLI)

### 2. Start the MCP Server

```bash
spectra mcp start
```

The server exposes execution tools plus the new data tools.

---

## Using the Execution Agent

### GitHub Copilot Chat (VS Code)

1. Open VS Code with the repository
2. Open Copilot Chat panel
3. Type `@spectra-execution run tests`
4. Follow the interactive prompts

### GitHub Copilot CLI

```bash
# Invoke the skill
gh copilot suggest "run spectra tests for auth suite"
```

### Claude (with MCP)

Add to your Claude project instructions or paste the agent prompt from:
`.github/agents/spectra-execution.agent.md`

---

## Using MCP Data Tools

### Validate Test Files

```bash
# Via MCP client or direct call
# Validates all suites
spectra mcp call validate_tests

# Validate specific suite
spectra mcp call validate_tests '{"suite": "checkout"}'
```

**Example Output:**
```json
{
  "is_valid": false,
  "total_files": 42,
  "valid_files": 40,
  "errors": [
    {
      "code": "MISSING_ID",
      "message": "Test file missing required 'id' field",
      "file_path": "tests/auth/login-test.md"
    }
  ]
}
```

### Rebuild Indexes

```bash
# Rebuild all suite indexes
spectra mcp call rebuild_indexes

# Rebuild specific suite
spectra mcp call rebuild_indexes '{"suite": "auth"}'
```

**Example Output:**
```json
{
  "suites_processed": 3,
  "tests_indexed": 67,
  "files_added": 5,
  "files_removed": 2
}
```

### Analyze Coverage Gaps

```bash
# Analyze all documentation coverage
spectra mcp call analyze_coverage_gaps

# Analyze for specific suite
spectra mcp call analyze_coverage_gaps '{"suite": "checkout"}'
```

**Example Output:**
```json
{
  "docs_scanned": 15,
  "docs_covered": 12,
  "coverage_percent": 80,
  "gaps": [
    {
      "document_path": "docs/features/refunds/partial-refund.md",
      "document_title": "Partial Refund Processing",
      "severity": "high"
    }
  ]
}
```

---

## Execution Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    Test Execution Flow                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  User: "@spectra-execution run checkout tests"                  │
│                                                                  │
│  Agent: [calls list_available_suites]                           │
│         "I found 3 suites. Which one?"                          │
│         - checkout (42 tests)                                    │
│         - auth (18 tests)                                        │
│         - orders (7 tests)                                       │
│                                                                  │
│  User: "checkout, high priority only"                           │
│                                                                  │
│  Agent: [calls start_execution_run with filters]                │
│         "Starting run with 12 high-priority tests..."           │
│                                                                  │
│  Agent: [calls get_test_case_details]                           │
│         "## TC-201: Payment with expired card                    │
│          **Priority**: high                                      │
│                                                                  │
│          ### Steps                                               │
│          1. Add item to cart                                     │
│          2. Enter expired card details                           │
│          3. Click Pay Now                                        │
│                                                                  │
│          ### Expected Result                                     │
│          Error message: Card expired                             │
│                                                                  │
│          Progress: Test 1/12                                     │
│          What is the result?"                                    │
│                                                                  │
│  User: "passed"                                                  │
│                                                                  │
│  Agent: [calls advance_test_case status=passed]                 │
│         "✓ TC-201 passed. Progress: 1/12 — 1 passed"            │
│         [presents next test...]                                  │
│                                                                  │
│  ... continues until all tests complete ...                      │
│                                                                  │
│  Agent: [calls finalize_execution_run]                          │
│         "Run complete!                                           │
│          Total: 12 | Passed: 10 | Failed: 2 | Blocked: 0        │
│                                                                  │
│          Failed tests:                                           │
│          - TC-205: Payment timeout                               │
│          - TC-208: Currency mismatch                             │
│                                                                  │
│          Want me to create bugs for the failures?"              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Files Created by Init

| Path | Purpose |
|------|---------|
| `.github/agents/spectra-execution.agent.md` | Copilot Chat agent prompt |
| `.github/skills/spectra-execution/SKILL.md` | Copilot CLI skill |

**Note**: Existing files are not overwritten unless `--force` is provided.

---

## Tool Reference

| Tool | Purpose | Parameters |
|------|---------|------------|
| `validate_tests` | Validate test files against schema | `suite` (optional) |
| `rebuild_indexes` | Regenerate `_index.json` files | `suite` (optional) |
| `analyze_coverage_gaps` | Find uncovered documentation | `suite`, `docs_path` (optional) |

All tools are deterministic — no AI dependency, same inputs produce same outputs.

---

## Error Codes

### Validation Errors

| Code | Meaning |
|------|---------|
| MISSING_ID | Test file has no `id` in frontmatter |
| INVALID_ID_FORMAT | ID doesn't match pattern (default: TC-XXX) |
| MISSING_TITLE | No H1 heading in test file |
| INVALID_PRIORITY | Priority not in: high, medium, low |
| YAML_PARSE_ERROR | Invalid YAML frontmatter syntax |

### Tool Errors

| Code | Meaning |
|------|---------|
| SUITE_NOT_FOUND | Specified suite directory doesn't exist |
| TESTS_DIR_NOT_FOUND | No `tests/` directory in repository |
| DOCS_DIR_NOT_FOUND | No `docs/` directory in repository |

---

## Next Steps

1. Run `spectra init` to install agent files
2. Create test suites in `tests/` directory
3. Add documentation in `docs/` directory
4. Use `validate_tests` to check test file quality
5. Use `analyze_coverage_gaps` to find missing tests
6. Execute tests via your preferred AI assistant
