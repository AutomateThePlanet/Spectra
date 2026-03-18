# Using SPECTRA Execution Agent with Copilot Chat

## Overview

The SPECTRA Execution Agent enables interactive test execution through GitHub Copilot Chat in VS Code.

## Setup

1. Initialize SPECTRA in your repository:
   ```bash
   spectra init
   ```

2. This creates the agent file at `.github/agents/spectra-execution.agent.md`

3. Open VS Code with the repository

4. Ensure you have GitHub Copilot extension installed

## Invocation

Open Copilot Chat and invoke the agent:

```
@spectra-execution run tests
```

Or with a specific suite:

```
@spectra-execution run checkout tests
```

## Workflow

1. **Suite Selection**: The agent lists available test suites
2. **Filter Options**: Choose priority, tags, or component filters
3. **Test Presentation**: Each test is presented one at a time with:
   - Title and priority
   - Preconditions
   - Numbered steps
   - Expected result
   - Test data (if any)
4. **Result Collection**: Say "passed", "failed", "blocked", or "skip"
5. **Progress Tracking**: See live progress after each test
6. **Summary**: Final pass/fail counts when complete

## Example Session

```
You: @spectra-execution run auth tests

Agent: I found 3 test suites:
- auth (18 tests)
- checkout (42 tests)
- orders (7 tests)

Which suite would you like to run? Any filters?

You: auth, high priority only

Agent: Starting run with 5 high-priority tests...

## TC-101: Login with valid credentials
**Priority**: high | **Component**: auth

### Steps
1. Navigate to the login page
2. Enter valid email "test@example.com"
3. Enter valid password "SecurePass123"
4. Click Sign In

### Expected Result
User is redirected to dashboard

---
**Progress**: Test 1/5
What is the result?

You: passed

Agent: ✓ TC-101 passed. Progress: 1/5 — 1 passed
[presents next test...]
```

## Tips

- Use natural language for results ("it worked" = pass, "bug found" = fail)
- The agent will ask for failure comments before moving on
- Test state is preserved if connection is lost
