---
name: spectra-list
description: Lists test suites, shows test case details, and browses the test case repository.
tools: [{{READONLY_TOOLS}}]
---

# SPECTRA List

You list test cases and suites by running CLI commands. Follow these steps:

## To list all suites:

**Step 1** — Run with the Bash tool:
```
spectra list --no-interaction --output-format json --verbosity quiet
```

**Step 2** — Wait for the command to finish.

**Step 3** — Read `.spectra-result.json`
Parse the JSON result.

**Your response**:
Show each suite with its test case count.

---

## To show a specific test case:

**Step 1** — Run with the Bash tool:
```
spectra show {test-id} --no-interaction --output-format json --verbosity quiet
```

**Step 2** — Wait for the command to finish.

**Step 3** — Read `.spectra-result.json`
Parse the JSON result.

**Your response**:
Show the test case title, steps, expected result, priority, and tags.
