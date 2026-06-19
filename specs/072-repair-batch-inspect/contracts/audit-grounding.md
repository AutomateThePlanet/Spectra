# Contract: `spectra ai audit-grounding`

**Spec 072 FR2** | New command | Model-free, deterministic

---

## Synopsis

```
spectra ai audit-grounding --suite <suite> [--output-format json]
```

Reports grounding state for every test in a suite that has a verdict file on disk. Serves as both the human inspection surface and the resume oracle for `compile-repair-batch`.

---

## Options

| Option | Required | Description |
|--------|----------|-------------|
| `--suite` / `-s` | Yes | Suite name |
| `--output-format json` | No | Machine-readable JSON (default: human-readable table) |

---

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success — output emitted |
| 1 | Error — suite not found or other fatal error |

---

## stdout (exit 0, `--output-format json`)

```json
{
  "command": "audit-grounding",
  "status": "success",
  "suite": "unit-converter",
  "tests": [
    {
      "id": "TC-100",
      "verdict": "grounded",
      "score": 0.92,
      "grounding_written": true,
      "flagged_for_review": false,
      "action_needed": "none",
      "file": "test-cases/unit-converter/TC-100.md"
    },
    {
      "id": "TC-101",
      "verdict": "partial",
      "score": 0.65,
      "grounding_written": false,
      "flagged_for_review": false,
      "action_needed": "repair",
      "file": "test-cases/unit-converter/TC-101.md"
    },
    {
      "id": "TC-132",
      "verdict": "partial",
      "score": 0.71,
      "grounding_written": true,
      "flagged_for_review": true,
      "action_needed": "review",
      "file": "test-cases/unit-converter/TC-132.md"
    }
  ],
  "summary": {
    "total": 35,
    "grounding_written": 20,
    "partial_pending_repair": 14,
    "flagged_for_review": 1
  }
}
```

## stdout (exit 0, human-readable default)

```
Grounding audit — suite: unit-converter

  ID       Verdict   Score  Grounded  Flagged  Action
  ───────  ────────  ─────  ────────  ───────  ──────
  TC-100   grounded  0.92   yes       no       none
  TC-101   partial   0.65   no        no       repair
  TC-132   partial   0.71   yes       yes      review

Summary: 35 total | 20 grounded | 14 pending repair | 1 flagged
```

---

## `action_needed` derivation

| Condition | `action_needed` |
|-----------|-----------------|
| `grounding_written = false` | `repair` |
| `grounding_written = true` AND `flagged_for_review = true` | `review` |
| `grounding_written = true` AND `flagged_for_review = false` | `none` |

---

## Behavior

1. Resolves `testsDir` from `spectra.config.json`.
2. Lists all `critic-verdict-TC-NNN.json` in `.spectra/verdicts/`. Filters to those whose `suite` matches the requested suite (determined via the suite index lookup).
3. For each verdict file:
   a. Parses the verdict JSON (id, verdict, score).
   b. Looks up the test's `.md` path via the suite index.
   c. Parses the `.md` with `TestCaseParser`; checks `tc.Grounding is not null` for `grounding_written`.
   d. Reads `FlaggedForReview` from `tc.Grounding` if present.
   e. Computes `action_needed`.
4. Emits results sorted by test ID.

---

## Guarantees

- **Never calls a model.**
- **Idempotent.** Re-running after partial repair shows the updated grounding state.
- **Single source of truth.** Both humans and `compile-repair-batch` use this logic to determine which tests need repair.

---

## Test contract

```
AuditGroundingCommandTests:
  UngroundedPartial_ReportsActionRepair → action_needed = "repair", grounding_written = false
  GroundedTest_ReportsActionNone → action_needed = "none", grounding_written = true
  FlaggedTest_ReportsActionReview → action_needed = "review", grounding_written = true, flagged = true
  SummaryCountsMatchEntries → summary.partial_pending_repair == count of repair entries
  JsonOutput_MatchesSchema → deserializes to AuditGroundingResult without errors
  SuiteNotFound_Exits1
  NoVerdictFiles_EmptyTests → tests = [], summary all zeros, exit 0
  HumanOutput_ContainsHeaders → stdout contains "Grounding audit" and suite name
```
