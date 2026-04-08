---
name: spectra-help
description: Shows all available SPECTRA commands and prompts you can use in Copilot Chat.
tools: []
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Help

When the user asks for help, what they can do, or what commands are available, respond with this:

---

## Test Case Generation (SPECTRA Generation agent)

| What you want | What to type |
|--------------|-------------|
| Generate tests for a suite | "generate test cases for payments" |
| Generate for a new suite | "generate test cases for search" |
| Generate more tests | "generate more tests for authentication" |
| Generate specific count | "generate 50 test cases for gdpr-compliance" |
| Generate focused tests | "generate negative tests for payments" |
| Generate edge case tests | "generate edge case tests for citizen-registration" |
| Generate security tests | "generate security tests for authentication" |

## Test Execution (SPECTRA Execution agent)

| What you want | What to type |
|--------------|-------------|
| Run a test suite | "run tests for notification" |
| Run high priority tests | "run high priority tests" |
| Run smoke tests | "run the smoke tests" |
| Resume a paused run | "resume the last run" |
| Check active runs | "what runs are active?" |
| Cancel all runs | "cancel all runs" |
| View run history | "show run history" |
| Smart test selection | "what should I test?" |

## Coverage Analysis

| What you want | What to type |
|--------------|-------------|
| Full coverage report | "show test coverage" |
| Find uncovered areas | "what areas don't have tests?" |
| Check specific area | "show coverage for payments" |
| Find untested acceptance criteria | "which acceptance criteria aren't tested?" |
| Extract acceptance criteria from docs | "extract acceptance criteria" |

## Dashboard

| What you want | What to type |
|--------------|-------------|
| Generate and open dashboard | "generate the dashboard" |
| Rebuild dashboard | "update the dashboard" |

## Validation

| What you want | What to type |
|--------------|-------------|
| Validate all tests | "validate all test cases" |
| Check for errors | "are there any test case errors?" |

## List & Browse Tests

| What you want | What to type |
|--------------|-------------|
| List all suites | "list all test suites" |
| Show a test | "show me TC-100" |
| Find tests by topic | "what tests do we have for payments?" |

## Test Updates

| What you want | What to type |
|--------------|-------------|
| Update tests after doc changes | "update tests for notification" |
| Preview changes | "show diff for notification tests" |

## CLI Commands (Terminal)

```bash
spectra ai generate --suite payments --count 20
spectra ai generate --suite payments --analyze-only
spectra ai analyze --coverage --auto-link
spectra ai analyze --extract-criteria --output-format json
spectra dashboard --output ./site
spectra validate
spectra list
spectra show TC-100
spectra docs index --force
spectra ai update --suite notification --diff
```
