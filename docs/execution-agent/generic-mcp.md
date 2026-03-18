# Using SPECTRA MCP Server

## Overview

SPECTRA exposes test execution and data tools via MCP (Model Context Protocol), allowing any MCP-compatible client to interact with your test suites.

## Starting the Server

```bash
spectra mcp start
```

The server runs on stdio transport and follows the MCP JSON-RPC 2.0 protocol.

## Available Tools

### Execution Tools

| Tool | Description |
|------|-------------|
| `list_available_suites` | List all test suites with test counts |
| `start_execution_run` | Start a new test execution run |
| `get_execution_status` | Get current run status |
| `get_test_case_details` | Get full details for a test |
| `advance_test_case` | Record pass/fail result |
| `skip_test_case` | Skip with reason |
| `pause_execution_run` | Pause the current run |
| `resume_execution_run` | Resume a paused run |
| `cancel_execution_run` | Cancel the current run |
| `finalize_execution_run` | Complete and generate report |

### Data Tools

| Tool | Description |
|------|-------------|
| `validate_tests` | Validate test files against schema |
| `rebuild_indexes` | Regenerate `_index.json` files |
| `analyze_coverage_gaps` | Find uncovered documentation |

## Tool Schemas

### validate_tests

```json
{
  "type": "object",
  "properties": {
    "suite": {
      "type": "string",
      "description": "Suite name (optional, validates all if omitted)"
    }
  }
}
```

### rebuild_indexes

```json
{
  "type": "object",
  "properties": {
    "suite": {
      "type": "string",
      "description": "Suite name (optional, rebuilds all if omitted)"
    }
  }
}
```

### analyze_coverage_gaps

```json
{
  "type": "object",
  "properties": {
    "suite": {
      "type": "string",
      "description": "Suite to analyze (optional)"
    },
    "docs_path": {
      "type": "string",
      "description": "Documentation directory (defaults to 'docs/')"
    }
  }
}
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

## Example: Direct Tool Invocation

```bash
# Via spectra CLI
spectra mcp call validate_tests
spectra mcp call validate_tests '{"suite": "checkout"}'

# Via JSON-RPC
echo '{"jsonrpc":"2.0","id":1,"method":"validate_tests","params":{}}' | spectra mcp start
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
