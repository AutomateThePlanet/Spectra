# Quickstart: Repair-Orchestration Hardening & Inspection Surface

**Spec 072** — Resumable batch repair using `compile-repair-batch` + `audit-grounding`

---

## Scenario: Complete a partial-repair batch after a prior session exhausted

You have 15 partial tests from a Spec 071 generate run. The prior session ran out of context partway through. Here's the complete cycle with the new commands.

### Step 1: Audit current grounding state

```bash
spectra ai audit-grounding --suite unit-converter --output-format json
```

Output (excerpt):
```json
{
  "suite": "unit-converter",
  "tests": [
    { "id": "TC-101", "verdict": "partial", "grounding_written": false, "action_needed": "repair" },
    { "id": "TC-106", "verdict": "partial", "grounding_written": false, "action_needed": "repair" }
    ...
  ],
  "summary": { "total": 35, "grounding_written": 20, "partial_pending_repair": 15, "flagged_for_review": 0 }
}
```

15 tests need repair. 20 are already done.

### Step 2: Compile the repair batch

```bash
spectra ai compile-repair-batch --suite unit-converter
```

The harness saves this to a temp file. Output is a JSON array with one entry per ungrounded partial. Each entry has `id`, `file`, `source_refs`, and `repair_prompt`.

### Step 3: Process the manifest (agent-driven)

For each entry in the manifest, the agent does ~5 operations:

```
1. Read repair_prompt from the manifest entry
2. Write the patched test to .spectra/repairs/repaired-{id}.json
3. Spawn critic subagent (context: fork — agent-driven, irreducible)
4. spectra ai ingest-update --suite unit-converter --test-id {id} --from .spectra/repairs/repaired-{id}.json
5. spectra ai ingest-grounding --suite unit-converter --test {id} --repaired --repair-attempts 1
```

Flag-and-continue rule (Spec 071): if the critic returns partial again, call `ingest-grounding` without `--repaired`, then continue to the next entry.

### Step 4: Resume after interruption

If the session exhausts mid-batch:

```bash
# New session — re-audit to see where we left off
spectra ai audit-grounding --suite unit-converter --output-format json

# Re-compile — only ungrounded tests appear (done tests automatically excluded)
spectra ai compile-repair-batch --suite unit-converter
```

The batch now contains only the remaining incomplete tests. No double-processing.

### Step 5: Verify completion

```bash
spectra ai audit-grounding --suite unit-converter
```

When `partial_pending_repair: 0`, all partials have been processed.

---

## Scenario: Agent needs a test's file path without shell improvisation

### Before (improvisation — causes allowlist prompt)
```bash
# Agent would previously do:
cat test-cases/unit-converter/_index.json | python -c "import json,sys; data=json.load(sys.stdin); print(next(t['file'] for t in data['tests'] if t['id']=='TC-105'))"
```

### After (single Spectra command)
```bash
spectra show TC-105 --output-format json
# → { "test": { "id": "TC-105", "file": "test-cases/unit-converter/TC-105.md", ... } }
```

---

## Scenario: Read config without shell improvisation

### Before (improvisation)
```bash
cat spectra.config.json
```

### After (existing command — Spec 072 FR5 adds agent awareness)
```bash
spectra config --raw
```

---

## Step 8 of spectra-generate skill (new numbered structure)

After the critic pass in Step 7, the skill now prescribes:

```
**Step 8 — Repair partial verdicts (resumable, manifest-driven)**

Run `spectra ai audit-grounding --suite <s> --output-format json` to check current state.

If `summary.partial_pending_repair > 0`:

  8.1  Run `spectra ai compile-repair-batch --suite <s>` → manifest (auto-saved by harness).

  8.2  For each manifest entry:
       a. Read `entry.repair_prompt`
       b. Write patched test to `.spectra/repairs/repaired-{entry.id}.json`
       c. Spawn critic subagent on the patched test
       d. Run `spectra ai ingest-update --suite <s> --test-id {entry.id} --from .spectra/repairs/repaired-{entry.id}.json`
       e. Run `spectra ai ingest-grounding --suite <s> --test {entry.id} --repaired --repair-attempts 1`
          (if critic returned partial again: run without --repaired; test is flagged, continue to next entry)

  Resume: if session exhausts, re-run from 8.1 in a new session —
  the filter in compile-repair-batch skips any test that already has a grounding block.
```
