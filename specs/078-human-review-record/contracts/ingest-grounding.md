# Contract: `spectra ai ingest-grounding` (changes)

**Feature**: `078-human-review-record` | **Status**: extends an existing command
**Satisfies**: FR-003, FR-004, FR-008, FR-010, FR-011
**Existing surface**: `--suite` (required), `--test`, `--all`, `--from`, `--repaired`,
`--repair-attempts`, `--output-format`

This command is the only writer of grounding blocks, so it is where preservation and suppression
become observable. The behaviour itself lives one layer down in `GroundingWriteBackService.WriteAsync`
(research R1); this contract covers the command surface and its reporting.

---

## Change 1 — re-verify mode (research R2)

### The problem

`RunBatchAsync` currently skips any test whose `.md` already parses with a non-null grounding block:

> `if (… existing.Grounding is not null) { skippedAlreadyWritten++; continue; }`

So a **second** verification pass over an already-grounded suite writes nothing. `WriteAsync` is never
reached, which means FR-003's preservation is never exercised and FR-010's suppression never fires.
Every unit test would pass while spec User Story 2 — the feature's highest-risk story — does nothing
in the real flow. The per-test path (`RunAsync`) has no such skip, so the two paths diverge.

The skip cannot be removed: the generate skill's post-loop batch call relies on being repeat-safe, and
Specs 073 and 077 built their non-stop flows on that.

### The addition

```
spectra ai ingest-grounding --suite <s> --all --re-verify
```

| Flag | Effect |
|---|---|
| *(absent)* | Unchanged. Skip already-grounded tests. Repeat-safe. Reported as `skipped_already_written`. |
| `--re-verify` | Do not skip. Every test with a verdict file in scope goes through `WriteAsync`, so preservation and suppression apply. Existing review records are carried forward; the marker is set per the FR-010 comparison. |

`--re-verify` composes with `--repaired` and `--repair-attempts` exactly as today. It has no effect in
per-test mode, which never skipped.

The pre-repair filter (without `--repaired`, partial verdicts are skipped so they get a repair attempt
first) is **unchanged** — it is a separate concern from idempotency and Specs 073/077 depend on it.

---

## Change 2 — preservation (FR-003, FR-004)

Three groups, per data-model.md's Preservation contract — the third is the one to get right.

**Carried forward unchanged** (the review record and provenance): `source`, `created_by`, `note`,
`reviewed_by`, `reviewed_at`.

**Freely updated by the pass** (the critic's own fields): `verdict`, `score`, `generator`, `critic`,
`verified_at`, `unverified_claims`, `condensed_findings`, `repair_attempts`, `repaired`.

**Recomputed every pass, never carried forward** (suppression state): `flagged_for_review` **and**
`reraise_suppressed_at`. The marker is *not* part of the review record — it is the companion output of
the suppression comparison, and it means *currently* suppressed. Carrying it forward would leave a
stale timestamp on a test this pass just re-raised, so a reader could not tell a live suppression from
a lapsed one. See the outcome table under Change 3.

Enforced inside `WriteAsync`, not in this command, so no caller can lose the record by omission
(FR-004). The command's only obligation is to report what happened.

---

## Change 3 — suppression (FR-010) and its reporting (FR-011)

### The comparison

Inside `WriteAsync`, before the new metadata is assigned:

```
suppress  ⟺  ReviewedBy is not null
          ∧  original.Grounding.FlaggedForReview == false     ← the PRE-WRITE value
          ∧  ReviewedAt > original.Grounding.VerifiedAt       ← the PRE-WRITE value
          ∧  new verdict == Partial
```

The **flag clause** distinguishes a human *clearance* from a human *raise*. A `feedback` action writes a
fresh `reviewed_at` and leaves `verified_at` alone, so without this clause the timestamp comparison
would pass and the next verification would suppress a concern the reviewer had just raised. Only a
pre-write `false` — the state `accept` leaves behind — is suppressible.

**The baseline is the most load-bearing detail in this feature.** `original.Grounding.VerifiedAt` is the value
parsed from disk. Compared instead against the timestamp this pass is about to write, `ReviewedAt`
could never be greater, suppression would never fire, and the feature would silently behave exactly
like the rejected Q3 → A. A test must assert that a pass writing a *newer* `verified_at` still
suppressed on the *pre-write* value.

Outcomes:

| Condition (verdict `partial`) | `flagged_for_review` | `reraise_suppressed_at` |
|---|---|---|
| Pre-write flag false, review newer than pre-write verification | left false (suppressed) | **set to now** |
| Pre-write flag **true** — a human raised it | stays true; never suppressed | **cleared** |
| Pre-write verification newer than review | true (re-raised) | **cleared** — the suppression has lapsed |
| Timestamps equal | true (re-raised) | **cleared** |
| No review record | true (normal FR-009 marking) | stays null |
| Verdict not `partial` — nothing to withhold | per the raise-preservation rule below | **cleared** |

### A human's raise survives a pass that agrees with the prior verdict

This command's batch path supplies **no** explicit flag value, so the marker is derived. Deriving it
purely from the new verdict silently discards a flag a reviewer set through `feedback`: an FR-008
raise on a `grounded` test derives to `false` and vanishes on the next `--re-verify` pass.

The derivation therefore preserves a pre-write `true` flag when the new verdict **equals** the
pre-write verdict:

```
flagged_for_review  =  (pre-write flag  ∧  new verdict == pre-write verdict)
                    ∨  (new verdict == Partial  ∧  ¬repaired)
```

A pass that merely agrees with the critic has no new information and does not overrule a reviewer, so
the raise persists indefinitely — it does **not** decay after one pass the way suppression does. Only
an explicit caller value (what `accept` effectively supplies) clears it. When the verdict genuinely
changes, re-derivation applies as before, so a repair that upgrades `partial` → `grounded` still
clears the flag.

The marker is cleared on every re-raise, not preserved. A test that is flagged and still carries a
suppression timestamp is a contradiction: the invariant is
`reraise_suppressed_at` non-null ⟺ `flagged_for_review: false`. Prior suppression timestamps are
recoverable from `git log -p`, consistent with spec Decision 3.

**There is no "already suppressed → advance the marker" case.** An earlier draft of this table said the
marker was "set to now — or advanced, if already suppressed". That second branch is unreachable: the
suppressing pass writes `verified_at :=` its own execution time, which is later than the `reviewed_at`
that triggered it, so the next pass finds `verified_at ≥ reviewed_at` and re-raises. A given review
suppresses **exactly one** pass. Re-suppression needs a fresh review, and it arrives at row 1 from the
`cleared` state, not from `suppressed` — see data-model.md's transition table.

Equality re-raises deliberately: sub-second ordering is unreliable, and the failure modes are
asymmetric — surfacing an already-decided concern costs one dismissal, suppressing a new one hides a
defect.

### Reporting

Per-test result gains three fields:

```json
{
  "success": true,
  "id": "TC-151",
  "suite": "checkout",
  "verdict": "partial",
  "score": 0.70,
  "flagged_for_review": false,
  "repair_attempts": 0,
  "repaired": false,
  "reraise_suppressed": true,
  "suppressed_by_review_at": "2026-08-28T11:02:00Z",
  "review_record_preserved": true
}
```

Batch result gains two counters:

```json
{
  "command": "ingest-grounding",
  "mode": "batch",
  "suite": "checkout",
  "re_verify": true,
  "written": 34,
  "skipped_already_written": 0,
  "skipped_partial_pre_repair": 0,
  "reraises_suppressed": 2,
  "review_records_preserved": 5,
  "errors": 0
}
```

`reraises_suppressed` is the number that matters operationally: it is how a reviewer or a CI log sees
that concerns were raised and withheld. A run reporting `reraises_suppressed: 0` on a suite with known
review records is the signature of the baseline bug described above.

---

## Human output

```
Grounding written for TC-151 in suite 'checkout': verdict=partial score=0.70
  Re-raise suppressed — review by Oleg Kuzmanov on 2026-08-28 is newer than the prior verification.
  Review record preserved.
```

Batch tail:

```
Re-verified 34 test(s) in 'checkout'.
  Review records preserved: 5
  Re-raises suppressed:     2   (TC-151, TC-158)
```

Naming the suppressed ids in human output matters — a count alone does not tell a reviewer which
concerns were withheld.

---

## Exit codes

Unchanged: `0` success, `1` error, `4`/`5` for the existing not-found and verdict-empty cases. Neither
suppression nor preservation is an error condition, so neither changes the exit code. A suppressed
re-raise is a normal, successful outcome.

---

## Invariants a test suite must assert

1. **The baseline.** A pass that writes a newer `verified_at` still suppresses based on the pre-write
   value. This is the regression guard for the whole feature.
2. Equal `reviewed_at` and pre-write `verified_at` re-raises, not suppresses.
3. Without `--re-verify`, an already-grounded test is skipped and its record untouched — the existing
   Specs 073/077 idempotency contract is intact (hard regression gate).
4. With `--re-verify`, the same test is rewritten, its review record byte-identical afterward, and its
   critic fields updated (AC-2).
5. Three consecutive `--re-verify` passes leave the review record — `reviewed_by`, `reviewed_at`,
   `note`, `source`, `created_by` — byte-identical each time (SC-002).
6. **`reraise_suppressed_at` tracks only the current state.** Given a suppressed test, when the next
   pass re-raises it, then the marker is **null** afterward and `flagged_for_review` is true. Assert
   the null explicitly — carrying it forward is the likely implementation slip, and it is the one that
   makes a lapsed suppression indistinguishable from a live one.
7. **No re-suppression without a new review.** Given `reviewed_at = T1` and pre-write
   `verified_at = T0 < T1`, run a pass at `T2` — it suppresses and writes `verified_at = T2`. Then run a
   second pass at `T3` with `reviewed_at` **unchanged**. Assert the test is **re-raised**:
   `flagged_for_review: true`, `reraise_suppressed_at: null`. It must never return to suppressed. A
   given review suppresses exactly one pass.
8. **Re-suppression is valid after a new review.** From the same post-suppression state
   (`verified_at = T2`, marker set), simulate a second human clearance so `reviewed_at = T3 > T2` — via
   `feedback` then `accept`, the only eligible route, which leaves the test in `cleared` with the marker
   already null. Then run a pass at `T4`. Assert it suppresses again: `flagged_for_review: false`,
   `reraise_suppressed_at = T4`, `verified_at = T4`. Note the *from* state is `cleared`, not
   `suppressed` — asserting a direct `suppressed → suppressed` transition would encode an unreachable
   path.
9. **A human's raise is never suppressed.** Given a test flagged by `feedback` at `T3` with
   `verified_at = T0 < T3`, run a pass at `T5` returning `partial`. Assert `flagged_for_review` stays
   **true** and `reraise_suppressed_at` stays null — the timestamp comparison alone would have
   suppressed here, so this is the regression guard for the pre-write-flag clause.
10. **A human's raise survives a pass that agrees with the prior verdict.** Given a `grounded` test
    flagged through `feedback`, when a pass returns `grounded` again with **no** explicit flag value,
    then `flagged_for_review` is still true afterward. Assert it across three consecutive passes — the
    raise is a standing objection and must not decay. This is the same class of defect as invariant 9
    reached through the flag-derivation route instead of the suppression route, and the batch
    `--re-verify` path is exactly the shape that hits it.
11. A repair that upgrades `partial` → `grounded` still clears the flag: the verdict changed, so
    re-derivation is legitimate and a repaired test must not stay flagged forever.
12. An explicit `flaggedForReview` value from the caller wins over raise-preservation.
13. A pass that suppresses writes the marker at its own execution time; the marker never carries a
    timestamp from an earlier pass.
14. A suppressed test whose next verdict is `grounded` has its marker cleared — there is no concern left
    to withhold.
15. After any pass, `reraise_suppressed_at` non-null ⇒ `flagged_for_review` false. No input produces
    both.
16. A suppressing pass reports `reraise_suppressed: true` and names the suppressing review (FR-011,
    SC-011).
17. A test with no review record behaves exactly as today — normal FR-009 marking, no new fields
    emitted (SC-007).
18. Per-test and batch `--re-verify` produce identical on-disk results for the same input.
