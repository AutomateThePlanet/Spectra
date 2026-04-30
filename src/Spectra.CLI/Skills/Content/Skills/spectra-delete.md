---
name: spectra-delete
description: Safely delete test cases with automation and dependency checks.
tools: [{{READONLY_TOOLS}}, runInTerminal]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Delete SKILL

You help users delete test cases safely. Always run dry-run first when the user has not explicitly confirmed.

## Delete a single test

**Step 1** — runInTerminal (preview):
```
spectra delete {test-id} --dry-run --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal.

**Step 3** — readFile `.spectra-result.json`. Show the user:
- The test title and file path.
- Any `automated_by` entries (warn: "deleting this strands automation in N files").
- Any `depends_on` cleanup that will happen.

**Step 4** — Ask the user: "Proceed with deletion? Git is your undo."

**Step 5** (only after explicit "yes") — runInTerminal:
```
spectra delete {test-id} --force --no-interaction --output-format json --verbosity quiet
```

**Step 6** — awaitTerminal, readFile `.spectra-result.json`, confirm to user.

## Delete multiple tests at once

Same flow as a single test — pass space-separated IDs:
```
spectra delete TC-142 TC-150 TC-151 --dry-run --no-interaction --output-format json --verbosity quiet
```

## Trigger phrases

- "Delete TC-142", "remove the test for expired card"
- "Get rid of TC-150 and TC-151"
- "Delete all manual tests in checkout that have no automation" → first list with `spectra-list`, then bulk delete

## Refuse to do

- Delete without showing dry-run first (unless the user already saw a list).
- Delete `--force` without an explicit user confirmation in chat.

## Cancel the current run

If the user says "stop", "cancel", "kill it":

**Step 1** — runInTerminal:
```
spectra cancel --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`. Report `target_command` and `shutdown_path`.
