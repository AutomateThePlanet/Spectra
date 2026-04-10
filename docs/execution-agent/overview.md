---
title: Overview
parent: Execution Agents
nav_order: 1
---

# Execution Agent Overview

How SPECTRA's MCP-based execution engine enables AI-driven test execution.

Related: [CLI Reference](../cli-reference.md) | [Copilot Spaces](../copilot-spaces-setup.md) | Platform setup: [Copilot Chat](copilot-chat.md) | [Copilot CLI](copilot-cli.md) | [Claude](claude.md) | [Generic MCP](generic-mcp.md)

---

## How Execution Works

SPECTRA's MCP execution engine provides a deterministic state machine that any LLM orchestrator can drive without holding state. The AI navigates; the engine enforces.

1. **Start a run** — Select a suite and optional filters (priority, tags, component)
2. **Present tests** — The engine serves tests one at a time with full details
3. **Record results** — Mark each test as passed, failed, skipped, or blocked
4. **Finalize** — Generate reports in JSON, Markdown, and HTML

The engine runs as an MCP server on stdio transport (JSON-RPC 2.0).

## Starting the Server

The MCP server is the separate `Spectra.MCP` global tool — it is **not** a `spectra` subcommand. Install it once and let your MCP client launch it:

```bash
dotnet tool install -g Spectra.MCP
spectra-mcp                # Started by your MCP client over stdio
```

Or from source:

```bash
dotnet run --project src/Spectra.MCP -- /path/to/project
```

## MCP Tools

### Run Management

| Tool | Description |
|------|-------------|
| `list_available_suites` | List all test suites with test counts |
| `start_execution_run` | Start a new test execution run |
| `get_execution_status` | Get current run status and next test |
| `pause_execution_run` | Pause an active run |
| `resume_execution_run` | Resume a paused run |
| `cancel_execution_run` | Cancel a run |
| `finalize_execution_run` | Complete run and generate reports |

### Test Execution

| Tool | Description |
|------|-------------|
| `get_test_case_details` | Get full test content (steps, expected result, preconditions) |
| `advance_test_case` | Record PASSED/FAILED result |
| `skip_test_case` | Skip test with reason (supports `--blocked` flag) |
| `bulk_record_results` | Bulk record results for multiple tests |
| `add_test_note` | Add notes to a test |
| `retest_test_case` | Re-queue a completed test |
| `save_screenshot` | Save screenshot attachment |

### Reporting

| Tool | Description |
|------|-------------|
| `get_run_history` | View past execution runs |
| `get_execution_summary` | Get run statistics |

### Data Tools

| Tool | Description |
|------|-------------|
| `validate_tests` | Validate test files against schema |
| `rebuild_indexes` | Rebuild `_index.json` files |
| `analyze_coverage_gaps` | Find uncovered documentation |

## Bulk Operations

The `bulk_record_results` tool allows processing multiple tests at once:

```json
// Skip all remaining tests
{"status": "SKIPPED", "remaining": true, "reason": "Environment unavailable"}

// Pass all remaining tests
{"status": "PASSED", "remaining": true}

// Process specific test IDs
{"status": "FAILED", "test_ids": ["TC-001", "TC-002"], "reason": "API down"}
```

## Response Format

All tools return responses wrapped in `McpToolResponse<T>`:

```json
{
  "data": { /* tool-specific result */ },
  "error": null
}
```

On error:

```json
{
  "data": null,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable message"
  }
}
```

## Error Codes

| Code | Description |
|------|-------------|
| `TESTS_DIR_NOT_FOUND` | No tests/ directory exists |
| `SUITE_NOT_FOUND` | Specified suite not found |
| `DOCS_DIR_NOT_FOUND` | No docs/ directory exists |
| `MISSING_ID` | Test file missing required ID |
| `INVALID_PRIORITY` | Invalid priority value |
| `INDEX_WRITE_ERROR` | Failed to write index file |

## Report Generation

Reports are generated in three formats:

- **JSON** — Machine-readable with all data
- **Markdown** — Human-readable summary
- **HTML** — Professional styled report with expandable test details

Report features:
- Test titles from `_index.json` (not just IDs)
- Human-readable durations ("1h 23m 45s")
- UTC-normalized timestamps
- Expandable non-passing tests with failure reasons

## Example Execution Flow

```
User: What test suites are available?
AI: [calls list_available_suites]
Available suites: checkout (10 tests), auth (18 tests)

User: Start a test run for checkout

AI: [calls start_execution_run]
Started run. First test: TC-100 - Checkout with valid credit card

User: Show me the test

AI: [calls get_test_case_details]
[presents full test with steps and expected result]

User: The test passed

AI: [calls advance_test_case with status=PASSED]
Recorded: TC-100 PASSED. Next: TC-101. Progress: 1/10

User: Skip this one - PayPal sandbox is down

AI: [calls skip_test_case]
Skipped: TC-101. Reason: PayPal sandbox is down

User: Finalize the test run

AI: [calls finalize_execution_run]
Run complete. 6 passed, 2 failed, 1 skipped, 1 blocked.
Report saved: .execution/reports/abc123.json
```

## Inline Documentation via Copilot Spaces

The execution agent supports inline documentation lookup during test execution via [GitHub Copilot Spaces](../copilot-spaces-setup.md). When a tester asks for clarification about a test step or expected behavior, the agent queries the configured Copilot Space to provide concise answers without leaving the execution flow.

Configure in `spectra.config.json` under `execution.copilot_space`. See [Copilot Spaces Setup](../copilot-spaces-setup.md) for details.

## Storage

Execution state is stored in `.execution/spectra.db` (SQLite). Reports are written to `.execution/reports/`.

```
.execution/
├── spectra.db                # Execution state database
└── reports/
    └── a1b2c3d4-....json     # Execution reports
```
