---
name: spectra-validate
description: Validates all test case files for correct format, unique IDs, and required fields.
tools: [{{READONLY_TOOLS}}]
---

# SPECTRA Validate

You validate test cases by running a CLI command. Follow these steps:

**Step 1** — Run with the Bash tool:
```
spectra validate --no-interaction --output-format json --verbosity quiet
```

**Step 2** — Wait for the command to finish

**Step 3** — Read `.spectra-result.json`
Parse the JSON result.

**Your response**:
- If `status` is "success": "All **{totalFiles}** test cases are valid."
- If `status` is "failed": list each error from `errors` array with `file` and `message`. Suggest fixes.
