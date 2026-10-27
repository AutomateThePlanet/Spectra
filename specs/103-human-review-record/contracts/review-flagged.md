# Contract: `spectra ai review-flagged`

**Feature**: `103-human-review-record` | **Status**: extends an existing command
**Satisfies**: FR-011 (reporting), FR-012 – FR-020, FR-022
**Existing surface**: `--suite`, `--no-interaction`, plus an interactive Spectre.Console loop whose
keystrokes are today the *only* way to reach accept and delete.

---

## Synopsis

```
spectra ai review-flagged [--suite <suite>]
                          [--action accept|retry-repair|delete|feedback]
                          [--test <id> | --all]
                          [--note "<text>"]
                          [--reviewer "<name>"]
                          [--fix]
                          [--no-interaction]
                          [--output-format json|human]
```

With no `--action`, behaviour is unchanged: list flagged tests (`--no-interaction`) or enter the
interactive loop. With `--action`, the command performs that action non-interactively and exits.

---

## Options

| Option | Type | Required | Notes |
|---|---|---|---|
| `--suite`, `-s` | string | no | Scope to one suite. Default: all suites. Unchanged. |
| `--action` | enum | no | `accept` \| `retry-repair` \| `delete` \| `feedback`. Omit for list/interactive. |
| `--test`, `-t` | string | with `--action`, unless `--all` | Target test id. |
| `--all` | flag | no | Apply the action to every eligible target in scope (FR-018). Mutually exclusive with `--test`. |
| `--note` | string | **yes for `feedback`**, optional for `accept` and `retry-repair` | Reviewer's note. FR-014 requires it for feedback; FR-012 makes it optional on accept. |
| `--reviewer` | string | no | Explicit identity override, first step of resolution (FR-019). |
| `--fix` | flag | no | `feedback` only. Requests immediate repair — see **Chaining** below. |
| `--no-interaction` | flag | no | Existing flag. With `--action`, suppresses all prompts; the action still runs. |
| `--output-format` | enum | no | Global option. `json` for structured output. |

**Rejected combinations** — each exits `2` (validation) with a named error:

- `--action` without `--test` or `--all` → `TARGET_REQUIRED`
- `--test` together with `--all` → `TARGET_AMBIGUOUS`
- `--action feedback` without `--note` → `NOTE_REQUIRED`
- `--fix` with any action other than `feedback` → `FIX_NOT_APPLICABLE`
- `--action delete` with `--all` → `BULK_DELETE_REFUSED`; deleting a whole suite's flagged tests in
  one call is not offered, because delete is the only irreversible action and `--all` makes the blast
  radius invisible at the call site

---

## Eligibility

Checked before any file is modified. Derives from the test's `grounding.verdict` (FR-007).

| Verdict | `accept` / `retry-repair` / `delete` | `feedback` |
|---|---|---|
| `partial`, currently flagged | allowed | allowed |
| `partial`, not flagged | refused — `NOT_FLAGGED` | allowed (marks it) |
| `grounded`, currently flagged | allowed | allowed |
| `grounded`, not flagged | refused — `NOT_FLAGGED` | **allowed** — this is the FR-008 path |
| `manual` | refused — `VERDICT_INELIGIBLE_MANUAL` | refused, same code |
| `hallucinated` | refused — `VERDICT_INELIGIBLE_HALLUCINATED` | refused, same code |
| no `grounding` block at all | refused — `NOT_VERIFIED` | refused, same code |

`manual` and `hallucinated` get **distinguishable** codes (FR-007): the first is a legitimate test
outside the review workflow, the second indicates a hand-edited or corrupt file. No production path
writes a `hallucinated` block (research R5), but the refusal is explicit rather than assumed
unreachable.

Refusals exit `4` (`NotFound`-class, command-namespaced per the project's exit-code convention) and
**modify nothing**.

---

## Per-action behaviour

### `accept` (FR-012)

1. Resolve reviewer identity (FR-019).
2. Validate the note if given; refuse before any write if it cannot be represented (FR-006).
3. Clear `flagged_for_review`.
4. Write `reviewed_by`, `reviewed_at`; write `note` if given.
5. Leave `verdict`, `score`, and every other critic field byte-identical.

### `retry-repair` (FR-013)

The CLI portion records the review and emits the repair chain as `next_step`; the rewrite itself is a
skill-driven inference step (research R8). On completion of that chain:

- `reviewed_by`, `reviewed_at`, and `note` reflect the requester, **replacing** any earlier record
  (Decision 7);
- `reraise_suppressed_at` is cleared — it described content that no longer exists;
- `verdict` and `score` are the fresh assessment's own output. This is not a violation of accept's
  protection, which governs `accept` and `feedback` only.

### `delete` (FR-014)

Unchanged. Writes the removal trail via `DroppedTestsTrail`, then the three-phase clean delete
(index entry, `depends_on` references, file). Records nothing — the file is gone. If the trail write
fails, the delete does **not** proceed.

### `feedback` (FR-015) — new

1. Eligibility per the table above — note this is the only action valid on an unflagged test.
2. Require `--note`; refuse with `NOTE_REQUIRED` if absent.
3. Resolve identity; validate the note.
4. Set `flagged_for_review: true` if not already set.
5. Write `reviewed_by`, `reviewed_at`, `note`.
6. Leave `verdict` and `score` unchanged.
7. If `--fix`, emit `next_step` (below). Otherwise the test is left in place, marked, with the record
   written — commentary only, fix deferred.

---

## Chaining `feedback --fix` (research R8)

`--fix` does **not** run repair. Repair requires inference, and Principle III forbids an in-process
model, so the CLI records the review and returns the chain for the skill to drive:

```json
{
  "command": "review-flagged",
  "status": "completed",
  "action": "feedback",
  "suite": "checkout",
  "test_id": "TC-142",
  "reviewed_by": "Oleg Kuzmanov",
  "reviewed_at": "2026-08-31T11:02:00Z",
  "flagged_for_review": true,
  "next_step": {
    "reason": "fix_requested",
    "chain": [
      "spectra ai compile-repair-prompt --suite checkout --test TC-142 --feedback-from-review",
      "patch in session, write to .spectra/updates/checkout/updated-TC-142.json",
      "spectra ai ingest-update checkout --test-id TC-142",
      "re-critic via the spectra-critic subagent",
      "spectra ai ingest-grounding --suite checkout --test TC-142 --repaired --repair-attempts 1"
    ]
  }
}
```

`compile-repair-prompt` gains `--feedback "<text>"` (or `--feedback-from-review`, reading the note
just written) so the reviewer's objection reaches the rewrite alongside the critic's findings, as
FR-015 requires. `RepairPromptCompiler.Compile` takes a nullable reviewer-feedback parameter and emits
it as its own labelled section.

The skill must not fabricate this chain from memory — it reads `next_step` from the result.

---

## Result shape

### Single-target success

```json
{
  "command": "review-flagged",
  "status": "completed",
  "action": "accept",
  "suite": "checkout",
  "test_id": "TC-142",
  "reviewed_by": "Oleg Kuzmanov",
  "reviewed_at": "2026-08-31T10:15:00Z",
  "flagged_for_review": false,
  "note_written": true,
  "verdict": "partial",
  "score": 0.72,
  "message": "Accepted TC-142. Verdict and score unchanged."
}
```

`verdict` and `score` are echoed so a caller can assert FR-012's protection without a second read.

### Batch (`--all`)

```json
{
  "command": "review-flagged",
  "status": "completed",
  "action": "accept",
  "suite": "checkout",
  "mode": "batch",
  "reviewed_by": "Oleg Kuzmanov",
  "processed": 12,
  "skipped_ineligible": 3,
  "errors": 0,
  "test_ids": ["TC-140", "TC-142"],
  "skipped": [
    { "test_id": "TC-151", "reason": "VERDICT_INELIGIBLE_MANUAL" }
  ]
}
```

An ineligible test inside a batch is **skipped and reported**, never silently dropped (SC-006), and
does not fail the batch.

### Listing (existing surface, extended by FR-011 and FR-017)

Each listed test gains the review record and the suppression marker, so an already-commented test is
distinguishable from an untouched one (US1 scenario 5) and a suppressed re-raise is visible
(FR-011, US2 scenario 7):

```json
{
  "flagged_count": 2,
  "tests": [
    {
      "id": "TC-142", "suite": "checkout", "title": "Refund within the stated window",
      "score": 0.72, "repair_attempts": 1,
      "condensed_findings": [{ "element": "Step 3", "reason": "interval absent from docs" }],
      "reviewed_by": null, "reviewed_at": null, "note": null,
      "reraise_suppressed_at": null
    },
    {
      "id": "TC-151", "suite": "checkout", "title": "Loyalty tier on the summary",
      "score": 0.95, "repair_attempts": 0, "condensed_findings": [],
      "reviewed_by": "Oleg Kuzmanov", "reviewed_at": "2026-08-28T11:02:00Z",
      "note": "Step 2 references a loyalty tier field that does not exist in the checkout UI.",
      "reraise_suppressed_at": null
    }
  ],
  "suppressed_count": 1,
  "suppressed": [
    {
      "id": "TC-160", "suite": "checkout", "title": "Guest checkout email receipt",
      "verdict": "partial", "score": 0.71,
      "condensed_findings": [{ "element": "Step 4", "reason": "receipt timing absent from docs" }],
      "reviewed_by": "Oleg Kuzmanov", "reviewed_at": "2026-09-02T09:40:00Z",
      "note": "Timing confirmed against the notification service runbook.",
      "reraise_suppressed_at": "2026-09-30T08:00:00Z"
    }
  ]
}
```

Two things about this shape.

**`reraise_suppressed_at` is always null inside `tests`.** That array holds *flagged* tests, and a
flagged test is by definition not withholding anything — the marker means *currently* suppressed and is
cleared the moment a test is re-raised (data-model.md, option (a)). The field is still emitted there,
always null, so a consumer reading one schema does not have to special-case which array it came from.
TC-151 carries a colleague's note and no marker, which is exactly the lapsed-or-never-suppressed case.

**Suppressed tests need their own array**, added here to satisfy FR-011's requirement that the
*listing* report suppression. They cannot appear in `tests`: suppression leaves
`flagged_for_review: false`, so a flagged-only listing would render withheld concerns invisible —
the precise failure FR-011 exists to prevent. `flagged_count` and `tests` keep their existing meaning
and shape, because Spec 071 and 072 consumers read them; `suppressed_count` and `suppressed` are
purely additive.

---

## Exit codes

Follows the project's `ExitCodes` convention; the result artifact's error field disambiguates codes
that share a numeric value.

| Code | Meaning | Error field |
|---|---|---|
| 0 | Success, including a batch with reported skips | — |
| 1 | I/O or unexpected failure | `IO_ERROR` |
| 2 | Invalid option combination, or a note that cannot be represented | `TARGET_REQUIRED`, `TARGET_AMBIGUOUS`, `NOTE_REQUIRED`, `FIX_NOT_APPLICABLE`, `BULK_DELETE_REFUSED`, `NOTE_UNREPRESENTABLE` |
| 4 | Target not found, or ineligible | `TEST_NOT_FOUND`, `SUITE_NOT_FOUND`, `NOT_FLAGGED`, `NOT_VERIFIED`, `VERDICT_INELIGIBLE_MANUAL`, `VERDICT_INELIGIBLE_HALLUCINATED` |
| 130 | Cancelled | `CANCELLED` |

No exit code is defined for unresolvable reviewer identity: resolution cannot fail (Decision 6,
research R6). The input's proposed failure path was dropped for that reason.

---

## Invariants a test suite must assert

1. `accept` leaves `verdict` and `score` byte-identical (AC-3, FR-012).
2. `feedback` on an unflagged `grounded` test sets the marker and leaves `verdict`/`score` unchanged
   (AC-4, FR-008).
3. `feedback` without `--note` exits 2 and modifies nothing (FR-015).
4. `manual` and `hallucinated` are refused with **distinct** codes, and no file is written (AC-6).
5. A note containing `C:\temp`, a trailing backslash, an embedded quote, and a newline round-trips
   exactly, or the action is refused before any write (FR-006, SC-008).
6. Every completed action writes a non-blank `reviewed_by`, with no prompt at any point (SC-009).
7. `delete` still writes the trail before removing the file, and aborts if the trail write fails.
8. `--all` reports each skipped test with its reason rather than dropping it (SC-006).
9. The interactive loop and the `--action` path produce identical on-disk results for the same
   decision — they call the same handler methods, and a test asserts they cannot drift.

### `reraise_suppressed_at` lifecycle (option (a) — *currently* suppressed)

10. **The mutual exclusion holds after every action.** For any test, after any of `accept`,
    `retry-repair`, or `feedback` completes, `reraise_suppressed_at` non-null ⇒
    `flagged_for_review: false`. No action may leave both set.
11. **`feedback` on a suppressed test clears the marker.** Given a test with
    `flagged_for_review: false` and a non-null `reraise_suppressed_at`, when the reviewer runs
    `feedback --note "…"`, then `flagged_for_review` becomes `true` **and** `reraise_suppressed_at`
    becomes null — the concern is no longer withheld, so the marker must not survive. This is the only
    action that can reach a suppressed test, since `accept`, `retry-repair`, and `delete` all require a
    flagged one.
12. **`accept` never encounters a live marker**, because a suppressed test is unflagged and `accept`
    refuses an unflagged test with `NOT_FLAGGED`. Assert the refusal rather than assuming it: a future
    change that relaxes accept's eligibility must not silently leave a stale marker behind.
13. **`retry-repair` clears the marker defensively.** It is unreachable on a suppressed test today for
    the same eligibility reason, but `feedback --fix` routes through feedback first (which clears it),
    so the post-repair state must have a null marker either way.
14. **A lapsed suppression leaves no residue.** Given a suppressed test, when a later verification pass
    re-raises it, then it appears in `tests` with `flagged_for_review: true` and
    `reraise_suppressed_at: null` — never in `suppressed`, and never with a stale timestamp. A reader
    of the file alone can always tell a live suppression from a lapsed one, which is the whole point of
    choosing option (a).
15. **`feedback` protects itself from the next verification pass.** Given a test flagged by `feedback`,
    when a later verification pass returns `partial`, then the test is **still flagged** afterward and
    `reraise_suppressed_at` is null. The reviewer's raise is never suppressed, even though `feedback`
    wrote a `reviewed_at` newer than the test's `verified_at` and the timestamp comparison alone would
    have suppressed it. The suppression comparison reads the pre-write flag for exactly this reason
    (data-model.md); this is the command-level half of that guard, and `ingest-grounding.md`
    invariant 9 is the service-level half.
16. **Re-review after a suppression returns through `cleared`.** Given a suppressed test, when the
    reviewer runs `feedback` and then `accept`, then the intermediate state is flagged with a null
    marker, and the final state is unflagged with a null marker and a refreshed `reviewed_at`. Only from
    there can a later pass suppress again. Do **not** write a test asserting a direct
    `suppressed → suppressed` transition — it is unreachable by construction, and asserting it would
    lock in a path the state machine does not have.
17. **The listing partitions cleanly.** `tests` and `suppressed` are disjoint for any corpus; every
    entry in `suppressed` has a non-null `reraise_suppressed_at` and a non-null `reviewed_by`; every
    entry in `tests` has a null `reraise_suppressed_at`. `flagged_count` equals `tests.length` and
    counts only flagged tests, unchanged from today (hard regression gate — Spec 071/072 consumers read
    it).
