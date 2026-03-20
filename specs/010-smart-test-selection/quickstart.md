# Quickstart: Smart Test Selection

## Scenario 1: Find Tests by Tag

```
User: "Find all payment-related tests"

→ find_test_cases(tags: ["payment"])

Response:
  matched: 8
  total_estimated_duration: "35m"
  tests: [TC-134, TC-135, TC-136, TC-137, TC-138, TC-139, TC-140, TC-141]
```

## Scenario 2: Find Tests with Multiple Filters

```
User: "High priority payment tests"

→ find_test_cases(tags: ["payment"], priorities: ["high"])

Response:
  matched: 3
  total_estimated_duration: "15m"
  tests: [TC-134, TC-135, TC-136]
```

## Scenario 3: Free-Text Search

```
User: "Find tests about timeout and retry"

→ find_test_cases(query: "timeout retry")

Response:
  matched: 5
  tests ranked by keyword hits:
    TC-134 (2 hits: title has "timeout", tags has "retry")
    TC-200 (1 hit: description mentions "timeout")
    TC-201 (1 hit: tags has "retry")
    ...
```

## Scenario 4: Start Run with Custom Test IDs

```
User: "Run TC-134, TC-100, and TC-201"

→ start_execution_run(
    test_ids: ["TC-134", "TC-100", "TC-201"],
    name: "Payment regression after timeout fix"
  )

Response:
  run_id: "abc123..."
  test_count: 3
  first_test: { handle: "abc1-TC-134-xK9f", ... }
```

## Scenario 5: Use Saved Selection

```
User: "Run the smoke tests"

→ list_saved_selections()
  → finds "smoke" selection

→ start_execution_run(
    selection: "smoke",
    name: "Pre-deploy smoke test"
  )

Response:
  run_id: "def456..."
  test_count: 12
```

## Scenario 6: Risk-Based Recommendation

```
User: "What should I test?"

→ find_test_cases(priorities: ["high", "medium"])
  → 40 tests found

→ get_test_execution_history(test_ids: [...all 40...])
  → 5 never executed, 3 last failed, 12 not run in 7+ days

Agent presents grouped recommendation:
  🔴 Never executed (5 tests)
  🟡 Last failed (3 tests)
  ⚪ Stale — not run in 7+ days (12 tests)
  ✅ Recently passed (20 tests)

User: "Run the risky ones"

→ start_execution_run(
    test_ids: [... 20 high-risk test IDs ...],
    name: "Risk-based selection — 20 high-risk tests"
  )
```

## Scenario 7: Validation Error — Invalid Test IDs

```
→ start_execution_run(test_ids: ["TC-134", "TC-999", "TC-888"])

Error:
  code: "INVALID_TEST_IDS"
  message: "Test IDs not found: TC-999, TC-888"
```

## Scenario 8: Validation Error — Mutual Exclusivity

```
→ start_execution_run(suite: "checkout", test_ids: ["TC-134"])

Error:
  code: "INVALID_PARAMS"
  message: "Parameters suite, test_ids, and selection are mutually exclusive"
```
