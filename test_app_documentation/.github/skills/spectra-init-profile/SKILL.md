---
name: spectra-init-profile
description: Creates or updates the generation profile that controls how AI generates test cases.
tools: [execute/runInTerminal, execute/awaitTerminal, read/readFile, read/problems, search/listDirectory, search/textSearch, browser/openBrowserPage]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Profile

You configure the generation profile by running a CLI command.

First ask the user what they want to configure:
- Detail level (high-level / detailed / very detailed)
- Negative scenario focus
- Domain-specific needs
- Default priority

**Step 1** — runInTerminal:
```
spectra init-profile --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal

**Step 3** — readFile `.spectra-result.json`
Parse the JSON result.

**Your response**:
Confirm what was configured and where the profile was saved.
