---
name: spectra-suite
description: Rename, delete, and manage test suites.
tools: [{{READONLY_TOOLS}}, runInTerminal]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Suite Management SKILL

You help users rename, delete, and inspect test suites. Always run dry-run first for destructive operations.

## List all suites

**Step 1** — runInTerminal:
```
spectra suite list --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`. Show each suite's name, test count, and automated count.

## Rename a suite

**Step 1** — runInTerminal (preview):
```
spectra suite rename {old} {new} --dry-run --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`.

**Step 3** — Show: directory move, count of selections that will be updated, config block updates.

**Step 4** — On user confirmation, runInTerminal without `--dry-run`:
```
spectra suite rename {old} {new} --force --no-interaction --output-format json --verbosity quiet
```

## Delete a suite

**Step 1** — runInTerminal (preview):
```
spectra suite delete {name} --dry-run --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`.

**Step 3** — Show: total tests, automation-linked tests, external dependencies. Warn loudly. Test IDs are global so deleting a suite does not free their numbers.

**Step 4** — On user confirmation, runInTerminal with `--force`:
```
spectra suite delete {name} --force --no-interaction --output-format json --verbosity quiet
```

## Trigger phrases

- "Rename suite checkout to payments"
- "Delete the auth suite", "remove all tests in legacy"
- "List all suites" → `spectra suite list`

## Refuse to do

- Delete or rename without showing dry-run first.
- `--force` without explicit user confirmation in chat.

## Cancel the current run

If the user says "stop", "cancel", "kill it":

**Step 1** — runInTerminal:
```
spectra cancel --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`. Report `target_command` and `shutdown_path`.
