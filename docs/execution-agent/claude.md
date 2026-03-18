# Using SPECTRA Execution Agent with Claude

## Overview

The SPECTRA Execution Agent can be used with Claude via MCP (Model Context Protocol) for interactive test execution.

## Setup with Claude Desktop

1. Initialize SPECTRA in your repository:
   ```bash
   spectra init
   ```

2. Add SPECTRA to your Claude Desktop MCP configuration:

   **Windows** (`%APPDATA%\Claude\claude_desktop_config.json`):
   ```json
   {
     "mcpServers": {
       "spectra": {
         "command": "spectra",
         "args": ["mcp", "start"],
         "cwd": "C:\\path\\to\\your\\repo"
       }
     }
   }
   ```

   **macOS** (`~/Library/Application Support/Claude/claude_desktop_config.json`):
   ```json
   {
     "mcpServers": {
       "spectra": {
         "command": "spectra",
         "args": ["mcp", "start"],
         "cwd": "/path/to/your/repo"
       }
     }
   }
   ```

3. Restart Claude Desktop

## Project Instructions

For optimal results, add the agent prompt to your Claude project:

1. Copy the content from `.github/agents/spectra-execution.agent.md`
2. Paste into your Claude project instructions

## Invocation

Simply ask Claude to run tests:

```
Run the checkout test suite
```

Or with filters:

```
Execute high priority authentication tests
```

## Available MCP Tools

Claude can call these SPECTRA tools:

| Tool | Purpose |
|------|---------|
| `list_available_suites` | List test suites |
| `start_execution_run` | Begin a test run |
| `get_test_case_details` | Get test details |
| `advance_test_case` | Record test result |
| `skip_test_case` | Skip with reason |
| `finalize_execution_run` | Complete the run |
| `validate_tests` | Validate test files |
| `rebuild_indexes` | Rebuild index files |
| `analyze_coverage_gaps` | Find uncovered docs |

## Example Session

```
You: What test suites are available?

Claude: [calls list_available_suites]
I found 3 test suites:
- auth (18 tests)
- checkout (42 tests)
- orders (7 tests)

Which suite would you like to run?

You: Run checkout, but only smoke tests

Claude: [calls start_execution_run with tags filter]
Starting run with 8 smoke tests from checkout suite.

[presents first test...]
```
