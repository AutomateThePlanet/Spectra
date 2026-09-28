# Contract: `spectra ai audit-grounding` (changes)

**Feature**: `078-human-review-record` | **Status**: extends an existing command
**Satisfies**: FR-011, FR-017
**Existing surface**: `--suite` (required), `--output-format`

This is the model-free inspection surface added by Spec 072 — the human's oracle and the resume oracle
for repair batches. The spec's propagation analysis identified it as needing a fourth `action_needed`
value; this feature also makes it the primary place a suppressed re-raise becomes visible.

---

## Change 1 — a fourth `action_needed` value

Today the field is a three-way derived from whether a grounding block exists and whether it is
flagged:

| Current value | Condition |
|---|---|
| `repair` | no grounding block written |
| `review` | grounding written and `flagged_for_review: true` |
| `none` | grounding written and not flagged |

`none` is now ambiguous: it means both "the critic verified this confidently and nobody needed to look"
and "a human looked and cleared it". Those are different states and a reviewer needs to tell them
apart.

| Value | Condition | Meaning |
|---|---|---|
| `repair` | no grounding block | unchanged |
| `review` | flagged | unchanged — needs a human |
| `reviewed` | **new** — not flagged, `reviewed_by` non-null | a human cleared this; no action |
| `none` | not flagged, no review record | never needed a human; no action |

`reviewed` is placed before `none` in precedence: a cleared-by-human test is `reviewed`, not `none`.

---

## Change 2 — review record and suppression columns (FR-011)

Each entry gains four fields:

```json
{
  "id": "TC-151",
  "verdict": "partial",
  "score": 0.70,
  "grounding_written": true,
  "flagged_for_review": false,
  "action_needed": "reviewed",
  "file": "test-cases/checkout/TC-151.md",
  "reviewed_by": "Oleg Kuzmanov",
  "reviewed_at": "2026-08-28T11:02:00Z",
  "note": "Step 2 references a loyalty tier field that does not exist in the checkout UI.",
  "reraise_suppressed_at": "2026-08-31T08:00:00Z"
}
```

Summary gains three counters:

```json
{
  "total": 35,
  "grounding_written": 35,
  "partial_pending_repair": 0,
  "flagged_for_review": 2,
  "reviewed": 5,
  "reraises_suppressed": 2,
  "reviews_stale": 1
}
```

### The two counters are disjoint, deliberately

`reraise_suppressed_at` means **currently** suppressed, never "was suppressed once"
(data-model.md, option (a)). The moment a test is re-raised, re-reviewed, or comes back non-`partial`,
the field is cleared. So:

| Counter | Predicate | Means |
|---|---|---|
| `reraises_suppressed` | `reraise_suppressed_at` non-null | A concern is being **withheld right now**. Implies `flagged_for_review: false`. |
| `reviews_stale` | `verified_at` > `reviewed_at` ∧ `flagged_for_review: false` ∧ `reraise_suppressed_at` **null** | Verification has overtaken the sign-off and nothing is being withheld — so the next `partial` verdict **will** re-raise. |

The third clause of `reviews_stale` is load-bearing. Without it the two counters would overlap: a
suppressing pass writes `verified_at = now`, which is newer than `reviewed_at`, so every suppressed
test would also count as stale and the numbers would double-count the same condition. The `null`
check separates "withheld" from "no longer protected".

`reraises_suppressed` is what makes FR-011 measurable (SC-011): without it, suppression converts a
visible queue item into no signal at all. `reviews_stale` is the observable form of spec Decision 8's
accepted consequences — one-pass decay and staleness when documentation moves without a verification
run — and is the number a team watches to decide whether the durable-suppression follow-up is worth
building.

### An impossible combination

`flagged_for_review: true` together with a non-null `reraise_suppressed_at` **cannot occur** under
option (a) — every transition that flags a test clears the marker. If the audit encounters it, the
file was hand-edited or is corrupt. Report it as a warning on the entry rather than silently picking
one of the two to believe:

```
  TC-163  partial   0.66  ✔ written    ⚑ review   ⚠ inconsistent
          flagged for review, but reraise_suppressed_at is set (2026-08-14)
          — these are mutually exclusive; the file may have been edited by hand
```

The entry still reports `action_needed: "review"`, because a flagged test needs a human regardless of
what the stray marker says. The warning is additive and never changes the action.

---

## Human output

```
Grounding audit — suite: checkout

  TC-140  grounded  0.94  ✔ written                      none
  TC-142  partial   0.72  ✔ written    ⚑ review          review
  TC-151  partial   0.70  ✔ written    ✓ reviewed        reviewed
          reviewed by Oleg Kuzmanov on 2026-08-28
          re-raise suppressed 2026-08-31 — review is newer than the prior verification
  TC-158  partial   0.68  ✔ written    ✓ reviewed  ⚠ stale
          reviewed by Oleg Kuzmanov on 2026-08-20
          verification 2026-08-31 is newer than the review; nothing withheld —
          the next partial verdict will re-raise this test
  TC-160  partial   0.71  ✔ written    ⚑ review          review
          reviewed by Oleg Kuzmanov on 2026-08-19
          previously suppressed; the suppression has lapsed and this was re-raised

Summary: 35 total, 35 grounded, 3 flagged, 5 reviewed, 1 suppressed, 1 stale
```

Suppressed and stale entries print their explanation inline. A count alone does not tell a reviewer
*which* concerns were withheld or which sign-off has been overtaken, and either is exactly the thing
a person needs to act on.

TC-160 shows the lapsed case. It carries a review record and is flagged, with **no**
`reraise_suppressed_at` — because the marker was cleared when the re-raise happened. The audit can
still say "previously suppressed" only in the loose sense that a review record older than the
verification is present; it does not read a stale timestamp, because there is none to read. If the
exact suppression history is needed, `git log -p` on the test file has it (spec Decision 3).

---

## Known scope limit (carried from Spec 072, unchanged)

`AuditGroundingHandler` enumerates **verdict files** in `.spectra/verdicts/`, not tests, so a test with
no verdict JSON never appears in the output. That directory is gitignored, so on a fresh clone the
audit reports nothing at all.

This feature does not change that, and the limitation matters more now than it did: the review record
lives in git and survives a clone, while the audit surface that reports it does not. A reviewer on a
fresh checkout sees an empty audit and must read the test files directly, or use
`review-flagged`'s listing (which enumerates test files, not verdict files, and therefore does work
after a clone).

Widening the audit to enumerate tests rather than verdict files is a small, self-contained change but
belongs to the surfacing follow-up alongside spec Decision 2's index work, not here — it would change
the output for every existing caller of a command two shipped specs depend on.

---

## Exit codes

Unchanged: `0` on success (including an empty report), `1` when the suite or index cannot be found.
Neither suppression nor staleness is an error — this command reports, it does not gate.

---

## Invariants a test suite must assert

1. A test cleared by a human reports `action_needed: "reviewed"`, not `"none"`.
2. A test that never needed review reports `"none"`, and emits no review fields.
3. A test whose re-raise is **currently** suppressed reports a non-null `reraise_suppressed_at` and is
   counted in `reraises_suppressed` (SC-011).
4. A test whose suppression has **lapsed** — re-raised by a later pass — reports
   `reraise_suppressed_at: null`, `flagged_for_review: true`, and `action_needed: "review"`, and is
   counted in **neither** `reraises_suppressed` nor `reviews_stale`.
5. A test whose `verified_at` exceeds `reviewed_at` while unflagged **and** with a null marker is
   counted in `reviews_stale`.
6. The two counters never double-count: for any corpus,
   `reraises_suppressed` and `reviews_stale` select disjoint entry sets. A suppressed test must not
   appear in `reviews_stale`, even though its `verified_at` is newer than its `reviewed_at`.
7. An entry with `flagged_for_review: true` and a non-null `reraise_suppressed_at` is reported with an
   inconsistency warning, keeps `action_needed: "review"`, and does not corrupt either counter.
8. The three existing `action_needed` values are unchanged for tests with no review record — a hard
   regression gate, since Spec 072's repair-batch resume logic reads this field.
9. The existing summary counters keep their names and meanings; the three new ones are additive.
