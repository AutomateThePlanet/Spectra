---
name: bellatrix-web-execute-spectra-test
description: Execute SPECTRA manual test cases automatically via the bellatrix web CLI — reads test steps, performs them through browser automation, validates expected results, and reports pass/fail back to SPECTRA.
---

# Execute SPECTRA Test Cases via the bellatrix web CLI

Automatically execute SPECTRA manual test cases by driving a live browser through the `bellatrix web` CLI. Each test step is performed through browser automation and the expected result is validated; results are reported back to SPECTRA for tracking. No test code is generated — for that, use `bellatrix-web-automate-spectra-test`.

**Two surfaces are used:**
- `bellatrix web` CLI — browser interaction (navigate, click, type, read, snapshot)
- `spectra/*` tools — execution state (start run, read test, advance test, finalize)

> **MANDATORY — context economy:** `snapshot` returns only `{ path, elementCount, summary }`. Never read the snapshot file, `snapshot --inline`, or `get-page-source` into this conversation. Resolve each locator with the **`bellatrix-web-locator-specialist`** subagent (description + snapshot `path`) or `bellatrix web find --session <s> --snapshot <path> --data-testid|--id|--role|--name|--text|--grep … --json`.

---

## Trigger Phrases

- "execute TC-205 via the bellatrix web CLI"
- "run TC-100 through TC-110 in the browser automatically"
- "auto-execute the checkout suite on the site"
- "run this test case against staging"
- "execute the smoke tests in Chrome"

---

## Workflow

### Step 1: Start the SPECTRA Execution Run

```
Tool: start_execution_run
Params: { "suite": "{suite_name}" }  or  { "test_ids": ["TC-205", "TC-206"] }
```

Record the `run_id` and `test_handles` from the response.

### Step 2: Start the Browser Session

Choose a short session name and start a browser (the CLI starts and later stops a local browser driver; with `--remote-url` the Selenium server is the user's to run — never start it yourself):

```
bellatrix web start-session --browser chrome --session run1 --json
```

Optional: `--headless`, `--width <px> --height <px>`, `--remote-url <selenium-server>`. If the start fails with a connection error, ask the user to start the Selenium server (remote) or install/update the browser (local), then retry.

### Step 3: Navigate and Capture the Initial Snapshot (MANDATORY)

```
bellatrix web navigate --session run1 --url "<start url from the test case or project config>" --json
bellatrix web wait-for-page-ready --session run1 --json
bellatrix web snapshot --session run1 --json
```

Dismiss a cookie/consent banner once here if it blocks the page (resolve its button, `click`, re-`snapshot`).

### Step 4: For Each Test Case

Loop through each test in the execution run.

#### 4a. Read Test Details from SPECTRA

```
Tool: get_test_case_details
Params: { "test_handle": "{handle}" }
```

Extract **Preconditions**, **Steps**, and **Expected Result**.

#### 4b. Execute Preconditions

If preconditions require UI actions ("user is logged in", "cart contains X", "on the checkout page"), perform them via the CLI, then `snapshot` to confirm the page is in the expected state.

#### 4c. Execute Each Step

Interpret each natural-language step and map it to a CLI call. Every element verb takes `--session <s> --locator <selector> [--strategy css|xpath|id|name|…]` and `--json`.

| Step pattern | `bellatrix web` command | Example |
|--------------|-------------------------|---------|
| "Open / navigate to {url}" | `navigate --url` | `navigate --session run1 --url "https://example.com/cart" --json` |
| "Click {button / link}" | `click` | `click --session run1 --locator "//button[normalize-space()='Apply coupon']" --json` |
| "Enter / type {text} in {field}" | `type-text` | `type-text --session run1 --locator "#coupon_code" --text "happybirthday" --json` |
| "Clear {field}" | `clear` | `clear --session run1 --locator "#coupon_code" --json` |
| "Select {option} from {dropdown}" | `select` | `select --session run1 --locator "#billing_country" --value "Bulgaria" --json` (`--value` = visible text) |
| "Check / tick {checkbox}" | `click` | `click --session run1 --locator "#terms" --json` |
| "Verify / check {text} is shown" | `get-text` | `get-text --session run1 --locator "[data-testid='message']" --json` |
| "Verify {element} is visible / enabled" | `is-visible` / `is-enabled` | `is-enabled --session run1 --locator "#place_order" --json` |
| "Verify the page / URL / title" | `get-url` / `get-title` | `get-url --session run1 --json` |
| "Wait for {condition}" | `wait-for-element` | `wait-for-element --session run1 --locator "[data-testid='results']" --timeout-ms 10000 --json` |
| "Go back / refresh" | `back` / `refresh` | `back --session run1 --json` |

**After each step:** if the page changed (navigation, modal, AJAX re-render) or the next step targets a different area, `wait-for-page-ready` and `snapshot` again — resolve the next locator from the NEW snapshot.

**If a step fails** (element not found, click intercepted, wrong page): take a screenshot (`take-screenshot --session run1 --out <file> --json`), capture a fresh snapshot, log the error, mark the test FAILED with the specific step, and move on to the next test.

#### 4d. Validate the Expected Result

1. Read the element(s) that should reflect the outcome (`get-text`, `get-attribute`, `is-visible`, `get-url`)
2. Compare the actual text/state with the expected result from the test case (exact unless the case says "contains")
3. Determine the verdict:

| Comparison | Verdict |
|------------|---------|
| Actual matches expected | PASS |
| Actual differs from expected | FAIL |
| Element not found | FAIL |
| Site down, 5xx page, browser crashed, session lost | BLOCKED |
| Precondition could not be established (e.g. test account locked) | BLOCKED |

#### 4e. Report the Result to SPECTRA

```
Tool: advance_test_case
Params: {
  "test_handle": "{handle}",
  "status": "PASSED|FAILED|BLOCKED",
  "notes": "Step 2: expected 'Coupon code applied successfully.' but got 'Coupon has expired.' Screenshot: shots/TC-205.png"
}
```

For FAILED tests include: the failing step, expected vs actual, and the screenshot path.

#### 4f. Reset Browser State

Before the next test, return to a clean state:
- `navigate` back to the start URL
- clear fields you filled, close open modals
- if the previous test logged in or changed the cart and the next one needs a clean session: `close-session`, then `start-session` again under a new name

### Step 5: Close the Browser and Finalize

```
bellatrix web close-session --session run1 --json
bellatrix web list-sessions --json
```

Make sure the session is gone (call `close-session` again if not), then:

```
Tool: finalize_execution_run
Params: { "run_id": "{run_id}" }
```

Open the HTML report:

```
show preview .execution/reports/{html_filename}
```

### Step 6: Present the Summary

```
Execution Complete
══════════════════════════════════

  Suite: Checkout
  Tests: 20
  Passed: 17
  Failed: 2
  Blocked: 1

  Failed tests:
    TC-205: Step 2 — expected "Coupon code applied successfully." but got "Coupon has expired."
    TC-212: Element [data-testid='place-order'] not found

  Blocked tests:
    TC-218: Payment provider sandbox returned HTTP 503

  Report: .execution/reports/run_2026-04-12_001.html
```

---

## Error Handling

| Error | Action |
|-------|--------|
| Element not found | `snapshot`, re-resolve via the locator specialist; still missing → FAIL "Element not found: {selector}" |
| Click intercepted (overlay, sticky header, cookie banner) | `snapshot`, dismiss the overlay or scroll (`execute-js --script "window.scrollBy(0, -200);"`), retry once; else FAIL |
| Page not responding / slow | `wait-for-page-ready`, retry once; still unresponsive → BLOCKED "Page not responding" |
| Navigation error / 5xx / DNS | log, BLOCKED for the current test, `navigate` to the start URL for the remaining tests |
| Session lost (`session_not_found`, browser crashed) | BLOCKED for the current test, `start-session` again for the remaining tests |
| Unexpected modal or banner | `snapshot`, dismiss if possible, continue; if it blocks the flow → BLOCKED |
| Native alert / confirm | element verbs cannot interact with it → BLOCKED with the dialog noted (or ask the user) |
| Selenium server unreachable (`--remote-url`) | stop and ask the user to start it — never start it yourself |

---

## Mode Selection

When the user asks to execute tests, ask which mode:

```
How should I execute these tests?

1. Auto-execute via the bellatrix web CLI — I'll drive the browser automatically,
   validate results, and report pass/fail (hands-free)

2. Assisted execution — I'll show each step and wait for you
   to confirm the result manually (you watch, I track)
```

**Mode 1 (Auto):** full autonomous execution as described above.

**Mode 2 (Assisted):** show each step, wait for the user to say pass/fail. Uses only SPECTRA (no `bellatrix web` CLI).

---

## Tips

- **Batch execution:** for large suites execute in batches of 10–20 so an early environment failure doesn't cascade
- **Screenshots:** capture one on every FAIL — always with `--out <file>` (never inline base64)
- **Flaky steps:** if a test fails on the first attempt, offer to retry once before marking it FAILED
- **Multi-page flows:** snapshot each new page/modal before interacting
- **Idle sessions:** sessions idle for more than 10 minutes are pruned by `list-sessions`; keep the run moving or start a new session
- **Always close:** `close-session` at the end — even after failures — so the local browser driver is stopped
