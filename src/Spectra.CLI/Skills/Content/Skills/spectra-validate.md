---
name: spectra-validate
description: Validates all test case files for correct format, unique IDs, and required fields.
tools: [{{READONLY_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Validate

You validate test cases by running a CLI command. Follow these steps:

### Tool call 1: runInTerminal
```
spectra validate --no-interaction --output-format json --verbosity quiet
```

### Tool call 2: awaitTerminal

### Tool call 3: readFile `.spectra-result.json`
Parse the JSON result.

### Your response:
- If `status` is "success": "All **{totalFiles}** tests are valid."
- If `status` is "failed": list each error from `errors` array with `file` and `message`. Suggest fixes.
