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
| Generate test cases for a suite | "generate test cases for payments" |
| Generate for a new suite | "generate test cases for search" |
| Generate more test cases | "generate more test cases for authentication" |
| Generate specific count | "generate 50 test cases for gdpr-compliance" |
| Generate focused test cases | "generate negative test cases for payments" |
| Generate edge case test cases | "generate edge case test cases for citizen-registration" |
| Generate security test cases | "generate security test cases for authentication" |

## Test Execution (SPECTRA Execution agent)

| What you want | What to type |
|--------------|-------------|
| Run a test suite | "run test cases for notification" |
| Run high priority test cases | "run high priority test cases" |
| Run smoke test cases | "run the smoke test cases" |
| Resume a paused run | "resume the last run" |
| Check active runs | "what runs are active?" |
| Cancel all runs | "cancel all runs" |
| View run history | "show run history" |
| Smart test selection | "what should I test?" |

## Coverage Analysis

| What you want | What to type |
|--------------|-------------|
| Full coverage report | "show test coverage" |
| Find uncovered areas | "what areas don't have test cases?" |
| Check specific area | "show coverage for payments" |
| Find untested acceptance criteria | "which acceptance criteria aren't tested?" |

## Acceptance Criteria

| What you want | What to type |
|--------------|-------------|
| Extract from docs | "extract acceptance criteria" |
| Import from file | "import criteria from jira-export.csv" |
| List all criteria | "list acceptance criteria" |
| Filter by component | "show criteria for payments" |

## Documentation Index

| What you want | What to type |
|--------------|-------------|
| Index documentation | "index the docs" |
| Full reindex | "reindex all documentation" |

## Dashboard

| What you want | What to type |
|--------------|-------------|
| Generate and open dashboard | "generate the dashboard" |
| Rebuild dashboard | "update the dashboard" |

## Validation

| What you want | What to type |
|--------------|-------------|
| Validate all test cases | "validate all test cases" |
| Check for errors | "are there any test case errors?" |

## List & Browse Test Cases

| What you want | What to type |
|--------------|-------------|
| List all suites | "list all test suites" |
| Show a test case | "show me TC-100" |
| Find test cases by topic | "what test cases do we have for payments?" |

## Test Updates

| What you want | What to type |
|--------------|-------------|
| Update test cases after doc changes | "update test cases for notification" |
| Preview changes | "show diff for notification test cases" |

## ISTQB Test Design Techniques

SPECTRA's behavior analysis applies six ISTQB black-box techniques: Equivalence
Partitioning (EP), Boundary Value Analysis (BVA), Decision Table (DT), State
Transition (ST), Error Guessing (EG), and Use Case (UC). Analysis output now
includes a `technique_breakdown` map alongside the category breakdown.

| What you want | What to type |
|--------------|-------------|
| Get the latest technique-aware templates | run `spectra prompts reset --all` in the terminal |
| Customize a technique prompt | edit `.spectra/prompts/behavior-analysis.md` directly |
| Show a built-in template | run `spectra prompts show behavior-analysis` |

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

---

## Diagnose test ID issues

If duplicate test IDs are reported, or you suspect ID drift:

**Step 1** — runInTerminal:
```
spectra doctor ids --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`.

**Step 3** — If `duplicates` is non-empty, show the duplicate groups (id, file paths, mtimes). Ask the user to confirm before fixing.

**Step 4** (only after confirmation) — runInTerminal:
```
spectra doctor ids --fix --no-interaction --output-format json --verbosity quiet
```

The fix renumbers later occurrences (oldest by mtime keeps the original ID). Hardcoded `[TestCase("TC-NNN")]` references in automation files are reported as `unfixable_references` for manual review.
