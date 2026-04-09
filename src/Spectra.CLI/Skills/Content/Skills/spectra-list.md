---
name: spectra-list
description: Lists test suites, shows test case details, and browses the test repository.
tools: [{{READONLY_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA List

You list tests and suites by running CLI commands. Follow these steps:

## To list all suites:

**Step 1** — runInTerminal:
```
spectra list --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal

**Step 3** — readFile `.spectra-result.json`
Parse the JSON result.

**Your response**:
Show each suite with its test count.

---

## To show a specific test:

**Step 1** — runInTerminal:
```
spectra show {test-id} --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal

**Step 3** — readFile `.spectra-result.json`
Parse the JSON result.

**Your response**:
Show the test title, steps, expected result, priority, and tags.
