# Quickstart: Human Review Records & Reviewer Feedback

**Feature**: `103-human-review-record` | **Date**: 2026-08-31

End-to-end flows for the four reviewer actions, plus the two behaviours that are easy to get wrong.
Every command here is model-free; the one inference step is marked and belongs to the skill.

Reviewer identity resolves from `--reviewer`, else git's configured user name, else the OS user name.
It cannot fail and there is never a prompt (spec Decision 6).

---

## 1. See what needs a human

```bash
spectra ai review-flagged --suite checkout --no-interaction --output-format json
```

```json
{
  "flagged_count": 2,
  "tests": [
    { "id": "TC-142", "score": 0.72,
      "condensed_findings": [{ "element": "Step 3", "reason": "refund interval absent from docs" }],
      "reviewed_by": null, "reraise_suppressed_at": null },
    { "id": "TC-151", "score": 0.95, "condensed_findings": [],
      "reviewed_by": "Oleg Kuzmanov", "reviewed_at": "2026-08-28T11:02:00Z",
      "note": "Step 2 references a loyalty tier field that does not exist in the checkout UI.",
      "reraise_suppressed_at": null }
  ],
  "suppressed_count": 0,
  "suppressed": []
}
```

TC-142 is untouched. TC-151 already carries a colleague's note — that distinction is new (FR-017), and
it is what stops two reviewers investigating the same thing twice.

The `suppressed` array is the second half of the listing: concerns verification raised that a newer
review is currently withholding. They cannot appear in `tests`, because suppression leaves them
unflagged — see step 5. Inside `tests`, `reraise_suppressed_at` is always null, since a flagged test
withholds nothing.

---

## 2. Accept a test, with the reason recorded

```bash
spectra ai review-flagged --suite checkout --action accept --test TC-142 \
  --note "Confirmed the refund interval against the support runbook; the assumption holds." \
  --output-format json
```

```json
{
  "command": "review-flagged", "status": "completed", "action": "accept",
  "test_id": "TC-142", "reviewed_by": "Oleg Kuzmanov",
  "reviewed_at": "2026-08-31T10:15:00Z", "flagged_for_review": false,
  "note_written": true, "verdict": "partial", "score": 0.72
}
```

The file now reads:

```yaml
grounding:
  verdict: partial            # unchanged
  score: 0.72                 # unchanged
  verified_at: 2026-08-20T09:14:00Z
  flagged_for_review: false
  reviewed_by: Oleg Kuzmanov
  reviewed_at: 2026-08-31T10:15:00Z
  note: "Confirmed the refund interval against the support runbook; the assumption holds."
```

`verdict` and `score` are echoed in the result so a caller can assert they did not move without a
second read. A bare `accept` with no `--note` is equally valid — the identity and timestamp are still
recorded.

---

## 3. Flag a confidently-verified test the critic got wrong

This is the path that did not exist before. TC-151 came back `grounded` at 0.95, but a human can see
step 2 is wrong.

```bash
spectra ai review-flagged --suite checkout --action feedback --test TC-151 \
  --note "Step 2 references a 'loyalty tier' field that does not exist in the checkout UI." \
  --output-format json
```

```yaml
grounding:
  verdict: grounded           # unchanged — the critic's assessment stands as a record
  score: 0.95                 # unchanged
  flagged_for_review: true    # set by feedback itself
  reviewed_by: Oleg Kuzmanov
  reviewed_at: 2026-08-31T11:02:00Z
  note: "Step 2 references a 'loyalty tier' field that does not exist in the checkout UI."
```

`feedback` is the only action valid on an unflagged test. `accept`, `retry-repair`, and `delete` refuse
with `NOT_FLAGGED`, since acting on an unmarked test through those three has no defined meaning.

`--note` is mandatory here — feedback without content records nothing a later reader can act on, and
omitting it exits 2 with `NOTE_REQUIRED`.

---

## 4. Ask for a fix in the same breath

```bash
spectra ai review-flagged --suite checkout --action feedback --test TC-151 \
  --note "Step 2 references a 'loyalty tier' field that does not exist in the checkout UI." \
  --fix --output-format json
```

The CLI records the review and hands back the chain. It does **not** run the repair — that needs
inference, and no SPECTRA command runs a model (Principle III):

```json
{
  "action": "feedback", "test_id": "TC-151", "flagged_for_review": true,
  "reviewed_by": "Oleg Kuzmanov", "reviewed_at": "2026-08-31T11:02:00Z",
  "next_step": {
    "reason": "fix_requested",
    "chain": [
      "spectra ai compile-repair-prompt --suite checkout --test TC-151 --feedback-from-review",
      "patch in session → .spectra/updates/checkout/updated-TC-151.json",
      "spectra ai ingest-update checkout --test-id TC-151",
      "re-critic via the spectra-critic subagent",
      "spectra ai ingest-grounding --suite checkout --test TC-151 --repaired --repair-attempts 1"
    ]
  }
}
```

The skill reads `next_step` and drives it — it must not reconstruct the chain from memory. The
reviewer's note reaches the rewrite as its own labelled section, so the fix addresses what the human
objected to and not only what the critic originally flagged.

After the chain completes, the review record names the requester and **replaces** any earlier record
(spec Decision 7) — the record always describes the content currently on disk.

---

## 5. The behaviour that is easy to get wrong: suppression

A month later, verification runs across the suite again.

```bash
spectra ai ingest-grounding --suite checkout --all --re-verify --output-format json
```

`--re-verify` is required. Without it the batch path skips every test that already has a grounding
block, so nothing is rewritten, nothing is preserved, and no suppression decision is ever made
(research R2). This is the single most likely way to ship the feature and have it do nothing.

```json
{
  "command": "ingest-grounding", "mode": "batch", "suite": "checkout", "re_verify": true,
  "written": 34, "reraises_suppressed": 1, "review_records_preserved": 5, "errors": 0
}
```

TC-142 comes back `partial` again. Its review (31 Aug) is newer than the verification recorded on the
file (20 Aug), so the re-raise is suppressed:

```yaml
grounding:
  verdict: partial
  score: 0.70                             # updated by this pass
  verified_at: 2026-09-30T08:00:00Z       # updated by this pass
  flagged_for_review: false               # suppressed — the review was newer
  reviewed_by: Oleg Kuzmanov              # preserved
  reviewed_at: 2026-08-31T10:15:00Z       # preserved
  note: "Confirmed the refund interval against the support runbook; the assumption holds."
  reraise_suppressed_at: 2026-09-30T08:00:00Z
```

Three things to hold onto:

- **The comparison uses the `verified_at` that was on the file, not the one being written.** Against
  the new value the review could never be newer and suppression would never fire.
- **Suppression lasts exactly one pass per review event.** This pass advanced `verified_at` past
  `reviewed_at`, so the *next* pass re-raises — a review defers a test past the next verification run,
  not indefinitely (spec Decision 8, consequence 1). To defer it again the reviewer has to review it
  again: `feedback` then `accept`, which refreshes `reviewed_at` past the `verified_at` just written.
  There is no way for two consecutive passes to suppress the same review.
- **A flag you raised yourself is never suppressed.** If you used `feedback` rather than `accept`, the
  test stays flagged through every later pass. Suppression applies only to a clearance.
- **Equal timestamps re-raise.** Sub-second ordering is unreliable, and hiding a new concern is worse
  than surfacing a decided one.

---

## 6. Confirm nothing was silently withheld

```bash
spectra ai audit-grounding --suite checkout --output-format json
```

```
Grounding audit — suite: checkout

  TC-142  partial   0.70  ✔ written    ✓ reviewed        reviewed
          reviewed by Oleg Kuzmanov on 2026-08-31
          re-raise suppressed 2026-09-30 — review is newer than the prior verification
  TC-158  partial   0.68  ✔ written    ✓ reviewed  ⚠ stale
          reviewed by Oleg Kuzmanov on 2026-08-20
          verification 2026-09-30 is newer than the review; not re-raised

Summary: 35 total, 35 grounded, 0 flagged, 5 reviewed, 1 suppressed, 1 stale
```

`suppressed` is the count of concerns raised and withheld; `stale` is the count of sign-offs
verification has overtaken. Watch the second one — a rising `stale` count is the signal that
timestamp-based suppression is not holding up and the durable variant is worth building.

Note the audit enumerates verdict files under `.spectra/`, which is gitignored — so on a fresh clone
it reports nothing. `review-flagged`'s listing reads the test files themselves and works after a
clone; prefer it when the audit comes back empty.

---

## 7. From chat

The skill exposes all four actions in plain language (FR-023), so a reviewer never types a command:

| What the reviewer says | Action |
|---|---|
| "looks fine, accept it" | `accept` |
| "accept it — I checked the runbook and the interval is right" | `accept` with `--note` |
| "regenerate this one" | `retry-repair` |
| "this one's redundant, remove it" | `delete` |
| "the step about loyalty tier is wrong" / "leave a note that…" | `feedback` |
| "the step about loyalty tier is wrong, fix it" | `feedback --fix` |

The guardrail the skill enforces (FR-022): accept and feedback never touch `verdict` or `score` —
those stay the critic's own assessment. `retry-repair` is the only action that changes them, and only
as a consequence of the fresh verification it runs. All three human actions write the review record in
addition to, never instead of, their existing effect.

---

## Refusals worth knowing

| Situation | Exit | Error |
|---|---|---|
| `feedback` with no `--note` | 2 | `NOTE_REQUIRED` |
| `--fix` on an action other than `feedback` | 2 | `FIX_NOT_APPLICABLE` |
| `--action delete --all` | 2 | `BULK_DELETE_REFUSED` |
| A note that cannot be represented in frontmatter | 2 | `NOTE_UNREPRESENTABLE` — nothing written |
| `accept` on an unflagged test | 4 | `NOT_FLAGGED` |
| Any action on a hand-created (`manual`) test | 4 | `VERDICT_INELIGIBLE_MANUAL` |
| Any action on a `hallucinated` test | 4 | `VERDICT_INELIGIBLE_HALLUCINATED` |

Every refusal happens before any file is touched. Notes containing backslashes, quotes, colons, and
newlines are all supported — the escaping is fixed as part of this feature; unrepresentable content is
refused rather than mangled.
