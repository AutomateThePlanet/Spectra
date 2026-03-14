# Quickstart: MCP Execution Server

This guide walks through executing manual tests using the Spectra MCP Execution Server with any LLM assistant (GitHub Copilot, Claude, etc.).

## Prerequisites

1. **Test suites exist** - Created via `spectra ai generate` (Phase 1)
2. **Indexes are current** - Run `spectra validate` to confirm
3. **MCP server is running** - Via your LLM orchestrator's MCP configuration

## Basic Workflow

### 1. Discover Available Suites

Ask your assistant: "What test suites are available?"

The assistant calls `list_available_suites` and responds:

```
Available test suites:
- checkout (42 tests)
- authentication (18 tests)
- orders (25 tests)
```

### 2. Start an Execution Run

Ask: "Start a test run for the checkout suite"

The assistant calls `start_execution_run` and responds:

```
Started execution run for checkout suite.
Run ID: a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c
Total tests: 42
Progress: 0/42

First test ready: TC-101 - Checkout with valid Visa card
Would you like me to show the test details?
```

### 3. View Test Details

Ask: "Show me the test"

The assistant calls `get_test_case_details`:

```
## TC-101: Checkout with valid Visa card

**Priority**: High
**Tags**: smoke, payments
**Component**: checkout

### Preconditions
- User is logged in
- Cart contains at least one item

### Steps
1. Navigate to checkout
2. Select credit card payment
3. Enter valid Visa card details
4. Click "Pay Now"

### Expected Result
- Payment is processed successfully
- Order confirmation page displays
- Confirmation email is sent

### Test Data
- Card: 4111 1111 1111 1111
- Expiry: 12/2028
- CVV: 123
```

### 4. Record the Result

After executing the test, tell your assistant the outcome:

"The test passed" or "The test failed - error message was wrong"

The assistant calls `advance_test_case`:

```
Recorded: TC-101 PASSED

Next test: TC-102 - Checkout with Mastercard
Progress: 1/42
```

### 5. Skip a Test

If you can't execute a test:

"Skip this test - payment gateway is down"

The assistant calls `skip_test_case`:

```
Skipped: TC-102
Reason: payment gateway is down

Note: 2 dependent tests (TC-108, TC-109) are now blocked.

Next test: TC-103 - Checkout with PayPal
Progress: 2/42
```

### 6. Pause and Resume

Need to take a break?

"Pause the test run"

```
Run paused at 15/42 tests.
Run ID: a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c

You can resume anytime by saying "Resume my checkout test run"
```

Later: "Resume my checkout test run"

```
Resumed run a3f7c291-...
Progress: 15/42

Next test: TC-116 - Checkout with gift card
```

### 7. Retest a Failed Test

After fixing an issue:

"Retest TC-104"

```
Re-queued TC-104 for attempt #2
Progress: 40/43 (includes retest)
```

### 8. Complete the Run

When all tests are done:

"Finalize the test run"

The assistant calls `finalize_execution_run`:

```
## Execution Complete

**Suite**: checkout
**Duration**: 1h 45m
**Executed by**: user@example.com

### Summary
| Status  | Count |
|---------|-------|
| Passed  | 38    |
| Failed  | 2     |
| Skipped | 1     |
| Blocked | 2     |
| **Total** | **43** |

### Failed Tests
- TC-104: Checkout with expired card
  Note: Error message shows "Invalid card" instead of "Card expired"
- TC-107: Checkout with invalid CVV
  Note: No validation error displayed

Report saved: reports/a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c.json
```

---

## Filtered Runs

### Run Only High-Priority Tests

"Start a checkout run with only high priority tests"

```
Started filtered run for checkout suite.
Filter: priority=high
Matching tests: 12/42
```

### Run Tests by Tag

"Run smoke tests for checkout"

```
Started filtered run for checkout suite.
Filter: tags=["smoke"]
Matching tests: 8/42
```

### Run Specific Tests

"Run tests TC-101, TC-102, and TC-103"

```
Started filtered run for checkout suite.
Filter: test_ids=[TC-101, TC-102, TC-103]
Matching tests: 3/42
```

---

## View History

"Show me the last 5 checkout runs"

```
## Checkout Run History

| Date | User | Status | Passed | Failed |
|------|------|--------|--------|--------|
| 2026-03-14 | user@example.com | Completed | 38/43 | 2 |
| 2026-03-12 | qa@example.com | Completed | 40/42 | 1 |
| 2026-03-10 | user@example.com | Cancelled | 20/42 | 3 |
| 2026-03-08 | qa@example.com | Completed | 41/42 | 0 |
| 2026-03-05 | user@example.com | Completed | 39/42 | 2 |
```

---

## Cross-MCP Integration

The real power comes from combining with other MCP servers:

### Log a Bug While Testing

"This test failed. Log a bug in Azure DevOps - priority 2, assign to checkout team"

Your assistant orchestrates:
1. Records the failure via Spectra MCP
2. Creates a work item via Azure DevOps MCP

```
Test TC-104 recorded as FAILED.

Bug AB#4521 created in Azure DevOps:
- Title: Checkout - Expired card shows wrong error message
- Priority: 2
- Assigned to: Checkout Team
- Linked to test: TC-104
```

### Post Results to Teams

"Post the test summary to the QA channel"

Your assistant orchestrates:
1. Gets summary via Spectra MCP
2. Sends message via Teams MCP

```
Summary posted to #qa-channel:
"Checkout suite completed: 38 passed, 2 failed, 1 skipped. Report: [link]"
```

---

## Configuration

The MCP server reads configuration from `spectra.config.json`:

```json
{
  "execution": {
    "db_path": ".execution/spectra.db",
    "timeout_hours": 72,
    "reports_dir": "reports/"
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| db_path | .execution/spectra.db | SQLite database location |
| timeout_hours | 72 | Auto-abandon paused runs after |
| reports_dir | reports/ | Where to write report files |

---

## Troubleshooting

### "Active run exists"

You have an unfinished run on this suite. Either:
- "Resume my checkout run"
- "Cancel my checkout run"

### "Index stale"

Test files changed since index was built. Run:
```bash
spectra index
```

### "Suite not found"

Check available suites:
```bash
spectra list
```

### "Tests pending" on finalize

Some tests weren't executed. Either:
- Go back and complete them
- "Finalize with force" to complete anyway
