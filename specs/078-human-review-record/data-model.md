# Phase 1 Data Model: Human Review Records & Reviewer Feedback

**Feature**: `078-human-review-record` | **Date**: 2026-08-31
**Depends on**: [research.md](research.md) (R3, R4, R9)

One entity changes. No new entity, no new store, no index change.

---

## Entity: grounding metadata

The verification metadata attached to a test case. Exists as three coupled representations that must
stay in step — a YAML DTO, a domain record, and the writer that emits the block. All three live in
`src/Spectra.Core/Models/Grounding/` except the writer (`src/Spectra.CLI/IO/TestFileWriter.cs`).

### Fields

Existing fields are listed for context because the invariants and the FR-010 comparison reference
them. Only the **Change** column is this feature's work.

| YAML key | Type | Change | Semantics |
|---|---|---|---|
| `verdict` | enum: `grounded` \| `partial` \| `hallucinated` \| `manual` | — | The critic's assessment. Protected from every human action except repair. |
| `score` | double 0.0–1.0 | — | Critic confidence. Protected identically. |
| `generator` | string | — | Model that produced the test. |
| `critic` | string | — | Model that verified it. |
| `verified_at` | UTC ISO-8601 | — | When verification last ran. **Baseline for FR-010, read before being overwritten.** |
| `unverified_claims` | string[] | — | Flattened non-grounded findings. |
| `flagged_for_review` | bool | write conditions change (FR-008, FR-010) | The test needs human attention. |
| `repair_attempts` | int | — | Automatic repair cycles applied. |
| `repaired` | bool | — | A repair upgraded the test to verified. |
| `condensed_findings` | `{element, reason}[]` | — | Non-grounded findings for human scanning. |
| `source` | string? | **repair round-trip** (FR-002) | Provenance marker, e.g. `user-described`. Parsed today, dropped on write. |
| `created_by` | string? | **repair round-trip** (FR-002) | Who authored a hand-created test. Parsed today, dropped on write. |
| `note` | string? | **repair round-trip** (FR-002), and becomes the reviewer's note (Decision 5) | Free-text annotation. For `manual` tests, generator provenance; for `grounded`/`partial`, the reviewer's note. |
| `reviewed_by` | string? | **new** (FR-001) | Identity of the last human reviewer. **Presence is the sole "has been reviewed" signal** (Decision 4). |
| `reviewed_at` | UTC ISO-8601? | **new** (FR-001) | When that review happened. Compared against `verified_at` for FR-010. |
| `reraise_suppressed_at` | UTC ISO-8601? | **new** (FR-011, per R3) | When the re-raise currently being withheld was suppressed. **Non-null means suppression is in effect right now** — cleared the moment the test is re-raised or re-reviewed (see *"means currently suppressed"* below). Advances on each further suppression. Written by the verification pass, never by a reviewer. Mutually exclusive with `flagged_for_review: true`. |

Deliberately absent: any `reviewed: true` boolean (Decision 4 — redundant with `reviewed_by`, and the
project already has one write-only field as a cautionary tale), and any review history array
(Decision 3 — git carries it).

### The review record

Three of the fields above form the unit that FR-003 preserves and FR-012–FR-014 write:

```
reviewed_by + reviewed_at + note
```

`source` and `created_by` are repaired alongside because they share the same round-trip defect and the
same fix, but they are provenance rather than review — they are preserved by FR-003 for the same
reason, and no review action writes them.

`reraise_suppressed_at` is *about* the record but is not part of it: a verification pass writes it, a
reviewer never does, and a repair does not carry it forward (see transitions).

---

## YAML shape

Emitted by `TestFileWriter`. Every added field is conditional — omitted entirely when null — so files
without a review record are byte-identical to today (SC-007).

```yaml
grounding:
  verdict: partial
  score: 0.72
  generator: claude-code-session
  critic: claude-opus-5
  verified_at: 2026-08-20T09:14:00Z
  unverified_claims:
    - "Step 3: docs do not state the refund-email interval"
  flagged_for_review: false
  condensed_findings:
    - element: "Step 3"
      reason: "refund-email interval absent from documentation"
  reviewed_by: Oleg Kuzmanov
  reviewed_at: 2026-08-28T10:15:00Z
  note: "Confirmed the interval against the support runbook; the assumption holds."
```

After a verification pass whose re-raise was suppressed (note that `verified_at` has advanced past
`reviewed_at`, which is why R3 requires the marker to be persisted rather than recomputed):

```yaml
grounding:
  verdict: partial
  score: 0.70
  verified_at: 2026-08-31T08:00:00Z      # advanced by this pass
  flagged_for_review: false               # suppressed — the review was newer at decision time
  reviewed_by: Oleg Kuzmanov              # carried forward unchanged
  reviewed_at: 2026-08-28T10:15:00Z       # carried forward unchanged
  note: "Confirmed the interval against the support runbook; the assumption holds."
  reraise_suppressed_at: 2026-08-31T08:00:00Z
```

`reviewed_by` is a **name**, not an email address — that is what identity resolution yields (FR-022,
research R6). Examples must not imply otherwise.

---

## Serialisation rules

**Emission order** — appended after `condensed_findings`, in the field order of the table above, so
existing blocks gain lines at the end and diffs stay readable.

**String escaping (FR-006, research R4)** — `TestFileWriter.EscapeYamlString` currently escapes `"`
and `\n` but **not backslash**, which silently corrupts any note containing `\` and breaks parsing on
a trailing one. The fix, in this order:

1. `\` → `\\`  *(must be first, or it double-escapes the backslashes step 2 introduces)*
2. `"` → `\"`
3. newline → `\n`
4. carriage return → `\r`

Applies to `note`, and to `condensed_findings[].reason` and `unverified_claims[]`, which share the
helper and carry the same latent exposure.

**Refusal before write** — where a note still cannot be represented faithfully after escaping, the
action is refused and **no file is modified** (FR-006). Validation runs before the write, not during.

**Timestamps** — UTC, `yyyy-MM-ddTHH:mm:ssZ`, matching the existing `verified_at` emission so a
reader sees one format throughout the block.

---

## Invariants

`GroundingMetadata.IsValid()` currently declares three rules and **has zero callers**, so none is
enforced (research R5). This feature revises the rules to stop them contradicting shipped behaviour;
enabling enforcement stays out of scope (spec Out of Scope).

| Rule | Status | Reason |
|---|---|---|
| `Score` within 0.0–1.0 | unchanged | — |
| `Generator` and `Critic` non-empty | unchanged | — |
| `Verdict == Partial ⇒ UnverifiedClaims` non-empty | unchanged | — |
| `FlaggedForReview ⇒ Verdict == Partial` | **revised** to `FlaggedForReview ⇒ Verdict ∈ {Grounded, Partial}` | FR-008 — a confidently-verified test must be markable by a human; the old rule contradicts it |
| `Repaired ⇒ Verdict == Grounded` | unchanged | — |
| `ReviewedAt` non-null iff `ReviewedBy` non-null | **new** | The two are written together by every action; one without the other is a malformed record |
| `Verdict ∈ {Hallucinated, Manual} ⇒ ReviewedBy` null | **new** | FR-007 — those verdicts are ineligible, so a review record on one indicates corruption or hand-editing |
| `ReraiseSuppressedAt` non-null ⇒ `ReviewedBy` non-null | **new** | Only a review can suppress; suppression without a reviewer is incoherent |
| `ReraiseSuppressedAt` non-null ⇒ `FlaggedForReview` **false** | **new** | The field means *currently* suppressed (option (a) above). A flagged test is by definition not withholding anything, so the two are mutually exclusive — this combination signals a hand-edited or corrupt file |
| A verification pass never clears a `FlaggedForReview` that was **true** before the pass | **new** | Suppression applies only to a human *clearance*, never to a human *raise*. This is the pre-write-flag clause of the suppression comparison; without it a `feedback` flag would be silently discarded |

The eligibility check that actually gates behaviour lives in the command layer (see
[contracts/review-flagged.md](contracts/review-flagged.md)), not in this dormant method.

---

## State transitions

The awaiting-review marker, `flagged_for_review`. Human transitions are solid; automated ones are the
verification pass.

```
                    ┌──────────────────────────────────────────────┐
                    │                                              │
                    ▼                                              │
            ┌───────────────┐                                      │
            │   UNMARKED    │  no review record                    │
            │  flagged=—    │                                      │
            └───────┬───────┘                                      │
                    │                                              │
    verification → partial          feedback (human)               │
    [FR-009]        │                    │                         │
                    ▼                    ▼                         │
            ┌────────────────────────────────────┐                 │
            │              MARKED                │                 │
            │           flagged=true             │                 │
            └───────┬─────────────┬──────────────┘                 │
                    │             │                               │
        accept ─────┘             └───── delete ──→ (file removed) │
      [FR-012]                          [FR-013]                   │
                    │                                              │
                    ▼                                              │
            ┌───────────────────────────┐                          │
            │         CLEARED           │  reviewed_by set         │
            │       flagged=false       │                          │
            └───────┬───────────────────┘                          │
                    │                                              │
      verification → partial, and reviewed_at > verified_at        │
                    │                                              │
                    ▼                                              │
            ┌───────────────────────────────────┐                  │
            │           SUPPRESSED              │                  │
            │  flagged=false                    │                  │
            │  reraise_suppressed_at set        │                  │
            └───────┬───────────────────────────┘                  │
                    │                                              │
      verification → partial, and verified_at ≥ reviewed_at ───────┘
                    (back to MARKED; record preserved,
                     reraise_suppressed_at CLEARED — the
                     suppression lapsed, so nothing is withheld)
```

**Transition rules**

| From | Trigger | To | Review record effect | `reraise_suppressed_at` |
|---|---|---|---|---|
| unmarked | verification returns `partial`, no review record | marked | none | stays null |
| unmarked / cleared / **suppressed** | `feedback` | marked | `reviewed_by`, `reviewed_at`, `note` written (overwrite) | **cleared** — the test is flagged again, so nothing is being withheld |
| marked | `accept` | cleared | `reviewed_by`, `reviewed_at` written; `note` if given | already null (a marked test is never suppressed); stays null |
| marked | `delete` | *file removed* | nothing recorded — the file is gone (FR-014) | n/a |
| marked | `retry-repair` | marked or cleared, per fresh verdict | record **replaced** by the requester's (Decision 7) | **cleared** — defensive; already null, since `retry-repair` requires a flagged test |
| cleared | verification `partial`, `reviewed_at` > pre-write `verified_at` | suppressed | preserved | **set to now** |
| **marked** | verification `partial` — regardless of timestamps | marked | preserved | stays null — **a pass never suppresses a human's raise**; see the guard below |
| cleared / **suppressed** | verification `partial`, pre-write `verified_at` ≥ `reviewed_at` | marked | preserved alongside the new concern | **cleared** — the suppression has lapsed and a real re-raise occurred |
| any | verification for a non-review reason, no marker change | unchanged | preserved (FR-003) | preserved |

There is deliberately **no `suppressed → suppressed` row.** An earlier draft had one, worded as
"`reviewed_at` still > pre-write `verified_at` → marker advanced to now". That transition is
**unreachable by construction**, and the wording implied a continuation that cannot continue:

> A pass suppresses only when `reviewed_at` > pre-write `verified_at`. The suppressing pass then writes
> `verified_at := ` its own execution time, which is by definition later than the `reviewed_at` that
> triggered it. So the *next* pass necessarily finds `verified_at ≥ reviewed_at` and re-raises. There is
> no sequence of verification passes that suppresses the same review twice.

A test *can* legitimately be suppressed again later — but the path runs through `cleared`, not through
`suppressed`, so it is row 6 and needs no row of its own. A new review is required, and every review
action on a suppressed test clears the marker first:

```
suppressed ──feedback (T3)──► marked ──accept (T4)──► cleared ──pass (T5)──► suppressed
             marker cleared            reviewed_at=T4              T4 > T2 ✓
```

`feedback` is the only action a suppressed test is eligible for — it is unflagged, so `accept`,
`retry-repair`, and `delete` all refuse with `NOT_FLAGGED` — and `feedback` sets the flag, which clears
the marker. The refreshed review then reaches suppression again from `cleared`.

### `reraise_suppressed_at` means *currently* suppressed

**Decision: option (a).** The field is non-null if and only if a re-raise is being withheld **right
now**. Every transition that sets `flagged_for_review: true` clears it; every transition that replaces
the review record clears it. It is never a historical marker of a lapsed suppression.

Formally, and this is the invariant to test:

```
reraise_suppressed_at is non-null   ⟺   flagged_for_review is false
                                     ∧  reviewed_by is non-null
                                     ∧  the last verification pass suppressed a re-raise
```

A corollary worth stating, because it bounds the field's lifetime: **the marker survives at most one
verification pass.** The pass that sets it also advances `verified_at` past `reviewed_at`, so the next
pass re-raises and clears it. A second suppression requires a second review (see the transition table's
`suppressed → suppressed` note).

`flagged_for_review: true` together with a non-null `reraise_suppressed_at` is therefore
**impossible**, and encountering it indicates a hand-edited or corrupt file — not a lapsed
suppression.

**Why (a) and not a permanent historical marker:**

- **Spec Decision 4 forbids exactly that shape.** It rejects a second field "that must be kept in
  sync with `reviewed_by`/`reviewed_at`" because the project already has one field that is written but
  never read back. A timestamp whose meaning depends on cross-checking `flagged_for_review` is the
  same trap: the file would read `flagged_for_review: true` and `reraise_suppressed_at: <past date>`
  simultaneously, with nothing on the file saying whether that suppression is live or lapsed.
- **Spec Decision 3 already settled where history lives.** Test files are git-tracked, so every prior
  value of this field is recoverable with `git log -p`. Keeping a lapsed timestamp in the file
  duplicates what git gives for free — the exact reasoning that ruled out an in-file review history.
- **It makes the two clearing paths consistent.** `retry-repair` already clears the field. Leaving the
  plain re-raise path *not* clearing it would mean two transitions that both end with a flagged test
  disagree about the field, which is worse than either uniform choice.
- **FR-011 becomes a single check.** "Is a concern being withheld?" is `reraise_suppressed_at != null`,
  with no second field to consult. Under a historical marker it would be a two-field predicate, and an
  auditor reading the file alone could not answer it.

What is lost: the file no longer shows that a test was *ever* suppressed once the suppression lapses.
That is the intended trade — git carries it, and the operational question ("what is being withheld
now?") is the one the reviewer and the audit surface actually ask.

**The comparison, stated once** (FR-010, research R1). Inside
`GroundingWriteBackService.WriteAsync`, before the new metadata is assigned:

```
suppress  ⟺  ReviewedBy is not null
          ∧  original.Grounding.FlaggedForReview == false     ← pre-write value; see below
          ∧  ReviewedAt > original.Grounding.VerifiedAt       ← pre-write value
          ∧  new verdict == Partial
```

**The second clause is not optional.** Without it, a pass would suppress a test a human had
*deliberately flagged* through `feedback`: that action writes a fresh `reviewed_at` and leaves
`verified_at` untouched, so the timestamp comparison passes and the pass would either clear the flag —
silently discarding the reviewer's raise, the exact opposite of what feedback means — or leave the flag
set while writing the marker, violating the mutual-exclusion invariant below. Reading the pre-write
flag distinguishes the two kinds of review: `false` means the human *cleared* the test (their decision
can suppress), `true` means the human *raised* it (their decision must never be suppressed).

This clause is what makes the `marked` row above hold, and it is the reason no
`suppressed → suppressed` row exists.

`original.Grounding.VerifiedAt` is the value parsed from disk, **not** the timestamp this pass is
about to write. Compared against the latter, `ReviewedAt` could never be greater and suppression would
never fire — the feature would degrade silently into the rejected Q3 → A behaviour. Equality resolves
to **re-raise**, deliberately: ordering within one second is unreliable, and surfacing an
already-decided concern costs one dismissal while hiding a new one hides a defect.

---

## Preservation contract

Applies to every path that reconstructs grounding metadata for a non-review reason (FR-003, FR-004).
Fields fall into **three** groups, not two — the third is what resolves the `reraise_suppressed_at`
question above.

**1. The review record and provenance — carried forward unchanged, always**:
`source`, `created_by`, `note`, `reviewed_by`, `reviewed_at`.

This is what FR-003 protects. A non-review write-back must never alter or drop any of them.

**2. Critic fields — freely updated by the pass**: `verdict`, `score`, `generator`, `critic`,
`verified_at`, `unverified_claims`, `condensed_findings`, `repair_attempts`, `repaired`.

**3. Suppression state — recomputed on every pass, never blindly carried forward**:
`flagged_for_review` **and** `reraise_suppressed_at`.

`reraise_suppressed_at` is deliberately **not** in group 1. It is not part of the review record; it is
the companion output of the same suppression decision that sets `flagged_for_review`, and the two are
always written together from that one comparison:

| Suppression decision this pass | `flagged_for_review` | `reraise_suppressed_at` |
|---|---|---|
| Suppressed (verdict `partial`, pre-write flag false, review newer than pre-write verification) | left false | set to now |
| Re-raised (verdict `partial`, verification newer or equal) | set true | **cleared** |
| Not suppressible — pre-write flag was **true** (a human raised it) | stays true | **cleared** |
| Verdict not `partial` — nothing to withhold | per the raise-preservation rule below | **cleared** |

### A human's raise survives an automated pass

The mirror image of suppression, and the same principle: **a human decision more recent than the
verification stands.** Suppression protects a human *clearance*; this protects a human *raise*.

When the caller supplies no explicit flag value — which is exactly what the batch
`ingest-grounding --all --re-verify` path does — the marker is derived as:

```
flagged_for_review  =  (pre-write flag was true  ∧  new verdict == pre-write verdict)
                    ∨  (new verdict == Partial   ∧  ¬repaired)
```

The first clause is the protection. Without it the marker is re-derived purely from the new verdict,
so an FR-008 raise on a `grounded` test derives to **false** and is silently discarded by the next
verification pass — the reviewer's objection disappears with no error and no trace beyond the note.

**Scoped to an unchanged verdict, deliberately.** A pass that merely agrees with the critic's prior
verdict has no new information and no business overruling a reviewer, so the raise persists across
any number of such passes — it is a standing objection, and unlike suppression it does **not** decay
after one pass. When the verdict actually *changes*, re-derivation is legitimate: a repair upgrading
`partial` → `grounded` must clear the flag, or every repaired test would stay flagged forever.

An explicit value from the caller always wins over both clauses: it is a deliberate decision, and
`accept` is the action that clears a raise.

One consequence worth noting: a `--repaired` pass whose re-verdict is still `partial` now **preserves**
an existing flag, where previously the `!repaired` clause cleared it. That matches what the Spec 071
skill already documents ("re-verdict `partial` → test still flagged"), so it brings the code into line
with the documented intent rather than changing it.

The third row matters and is easy to miss: a suppressed test whose next verification comes back
`grounded` has no concern left to withhold, so the marker must be cleared there too. Leaving it would
break the `⟺` invariant above — the file would claim a re-raise is being withheld when the critic no
longer raises one.

Treating it as "carried forward unchanged" — as an earlier draft of this section did — is precisely
the bug the transition table above resolves: it would leave a stale timestamp on a test that is
flagged and needs attention, with nothing on the file distinguishing a live suppression from a lapsed
one. Group 3 exists so that cannot happen.

**Enforced at**: `GroundingWriteBackService.WriteAsync` — the sole construction point, so no caller
carries the obligation (research R1).

**Also audited**: two further services perform their own field-by-field `TestCase` copies and must be
checked for the same omission — `UpdatedTestIngestor` (repair ingest; *should* overwrite the record
per Decision 7, but deliberately rather than by dropping fields) and `ReviewFlaggedHandler`
(`CopyWithGrounding`). Both currently copy `Status`, which is itself write-only and always null on
re-read — an unrelated pre-existing defect the spec puts out of scope.

---

## Compatibility

- Every new field is nullable and conditionally emitted; a file without them parses exactly as today
  (SC-007). No migration, no schema version bump.
- `_index.json` and `TestIndexEntry` are untouched (Decision 2, AC-8). Review state is invisible to
  `spectra list`, the dashboard, and `spectra run` by design, and the field names chosen here are the
  ones a follow-up should propagate unchanged.
- `spectra validate`'s five gates do not read grounding, so they are unaffected — provided note
  escaping is correct, which is why R4 is a prerequisite and not a nicety.
