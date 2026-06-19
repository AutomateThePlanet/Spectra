---
description: Reviews and dispositions flagged (still-partial-after-repair) test cases. For each flagged test: list → retry repair cycle (compile-repair-prompt → patch in-session → ingest-update → re-critic → ingest-grounding). Also supports accept-as-is and delete via the review-flagged command.
tools: [{{GENERATE_TOOLS}}]
---

# spectra-review-flagged

Disposition tests that were flagged for human review after the bounded repair loop in `spectra-generate` did not upgrade them to grounded. You decide the action: **retry repair** (another bounded attempt), **accept as-is** (clear the flag, keep the partial block), or **delete** (trail + clean delete).

## Prerequisites

- `spectra ai review-flagged [--suite <suite>] --no-interaction --output-format json` lists all flagged tests.
- Choose a specific test to work on (by id) or process all.
- If the test has no verdict file (`.spectra/verdicts/critic-verdict-{id}.json`), retry repair is not available — use accept or delete.

---

## Action 1: Retry repair for a specific test

This action runs another bounded repair attempt (1 more cycle). Use it when the first auto-repair attempt failed but you believe the test is salvageable.

### Step 1 — Compile the repair prompt

```
spectra ai compile-repair-prompt --suite {suite} --test {id}
```

- Exit 0 → stdout is the repair prompt (plain text). Read it.
- Exit 4 → verdict is not partial (already grounded or verdict file missing). Use accept or delete instead.
- Exit 5 → verdict file missing. Use accept or delete instead.
- Exit 6 → verdict JSON parse failure. Investigate the verdict file.

### Step 2 — Patch the test IN-SESSION

Read the repair prompt. Rewrite ONLY the elements the critic flagged as ungrounded. Preserve the test id, priority, component, tags, and all grounded elements. Write the patched test as a JSON array to `.spectra/repaired.json`.

### Step 3 — Ingest the repair

```
spectra ai ingest-update {suite} --test-id {id} --from .spectra/repaired.json --output-format json
```

- Exit 0 → patched test persisted.
- Exit 5/6 → content/schema failure. Fix the JSON and retry within this step.

### Step 4 — Re-run the critic

Invoke the `spectra-critic` subagent for `{id}` (same procedure as in `spectra-generate` Step 8):

```
spectra ai compile-critic-prompt --suite {suite} --test {id}
```

The subagent writes `.spectra/verdicts/critic-verdict-{id}.json` and runs:

```
spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-{id}.json --output-format json
```

### Step 5 — Write the final grounding block

**Re-verdict `grounded`:**
```
spectra ai ingest-grounding --suite {suite} --test {id} \
  --from .spectra/verdicts/critic-verdict-{id}.json --repaired --repair-attempts 2 --output-format json
```
→ Test upgraded. Report: grounded after 2 repair attempts.

**Re-verdict `partial`:**
```
spectra ai ingest-grounding --suite {suite} --test {id} \
  --from .spectra/verdicts/critic-verdict-{id}.json --repair-attempts 2 --output-format json
```
→ Test still flagged. Recommend accept-as-is (the claim may not be verifiable from available docs) or delete.

**Re-verdict `hallucinated`:**
```
spectra ai record-drop --suite {suite} --test {id} \
  --from .spectra/verdicts/critic-verdict-{id}.json --output-format json
spectra delete {id} --force --no-interaction --output-format json --verbosity quiet
```
→ Drop trail written; test deleted.

---

## Action 2: Accept as-is (all flagged tests in a suite)

For each flagged test you want to accept without retry:

```
spectra ai review-flagged --suite {suite}
```

→ Interactive mode: press `[A]ccept` for each test to clear `flagged_for_review` while keeping the partial block.

For non-interactive acceptance (script mode), use the interactive command or handle each test individually via the retry flow above with a forced accept (the interactive command is the only accept path for now).

---

## Action 3: Delete a flagged test (manual trail + delete)

If the test is not salvageable and you want to remove it:

```
spectra ai record-drop --suite {suite} --test {id} --reason user_decided --output-format json
spectra delete {id} --force --no-interaction --output-format json --verbosity quiet
```

→ Trail entry written with `drop_reason: user_decided, source: review`, then clean delete.

---

## Post-review summary

After all flagged tests are dispositioned, report:
- Upgraded to grounded (retry repair succeeded): list ids
- Accepted as partial (acknowledged): list ids
- Deleted (user decision): list ids
- Remaining flagged (skipped): list ids

Run `spectra ai review-flagged --no-interaction --output-format json` to confirm the residual count.

---

## Guardrails

- **NEVER auto-advance** without reading the critic verdict first.
- **NEVER change verdict or score** via accept — the partial block stays as-is; only `flagged_for_review` is cleared.
- **NEVER delete without writing the trail first** (`record-drop` before `spectra delete`). If `record-drop` fails, do NOT delete.
- **NEVER run more than 1 automatic retry** per test in this session without explicit user confirmation for more.
