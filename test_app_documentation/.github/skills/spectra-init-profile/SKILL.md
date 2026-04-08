---
name: spectra-init-profile
description: Creates or updates the generation profile that controls how AI generates test cases.
tools: [execute/runInTerminal, execute/awaitTerminal, execute/getTerminalOutput, read/readFile, read/terminalLastCommand, read/problems, search/listDirectory, search/textSearch, browser/openBrowserPage]
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

### Tool call 1: runInTerminal
```
spectra init-profile --verbosity normal
```

### Tool call 2: awaitTerminal

### Tool call 3: terminalLastCommand
Read the terminal output to confirm the profile was created/updated.

### Your response:
Confirm what was configured and where the profile was saved.
