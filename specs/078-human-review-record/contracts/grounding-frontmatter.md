# Contract: the `grounding:` frontmatter block

**Feature**: `078-human-review-record` | **Status**: extends an existing file format
**Satisfies**: FR-001, FR-002, FR-006, FR-021, FR-022

This file format **is** the user-facing interface for this feature. A reviewer's audit trail is
whatever a person reads when they open `test-cases/{suite}/TC-NNN.md`, and it is what survives in git.
It therefore gets a contract of its own, not just a data model.

Field semantics, invariants, and state transitions are in [../data-model.md](../data-model.md). This
document pins the **wire format**: what is emitted, in what order, how it is escaped, and what a
reader may rely on.

---

## Reader guarantees

A consumer parsing a test file may rely on all of the following.

1. **Every new field is optional.** A file written before this feature parses identically after it.
   No migration, no schema version, no default injection (SC-007).
2. **Absent means absent.** A null field is omitted from the block entirely — never emitted as
   `reviewed_by:` with an empty value, `null`, or `""`. Presence of the key means a real value.
3. **`reviewed_by` non-null is the "has been human-reviewed" signal.** There is no boolean to
   cross-check and no possibility of the two disagreeing (spec Decision 4).
4. **`reviewed_by` is a person's name, not an email address.** It comes from git's configured user
   name, falling back to the OS user name. Documentation and examples must not imply email
   (FR-022, research R6).
5. **`reviewed_at` and `reviewed_by` always appear together.** One without the other is a malformed
   record.
6. **Timestamps are UTC** in `yyyy-MM-ddTHH:mm:ssZ`, matching the existing `verified_at` emission.
7. **`note` is reviewer prose for `grounded`/`partial` tests and generator provenance for `manual`
   tests** (spec Decision 5). The two never coexist on one file, because a `manual` test is
   ineligible for review.
8. **`reraise_suppressed_at` is machine-written and means *currently* suppressed.** A verification
   pass sets it; a reviewer never does. Non-null means an automatic re-raise is being withheld **right
   now** because a newer review stands. It is cleared the moment the test is re-raised, re-reviewed, or
   comes back with a non-`partial` verdict — it is never a historical record of a lapsed suppression
   (data-model.md, option (a)). Prior values are in `git log -p`.
9. **`reraise_suppressed_at` and `flagged_for_review: true` are mutually exclusive.** A flagged test
   withholds nothing, so the pair cannot both be set. Encountering both means the file was hand-edited
   or is corrupt; a reader should treat the flag as authoritative and the marker as suspect.

---

## Emitted shape

Order is fixed: existing fields keep their current positions, and the six this feature adds are
appended after `condensed_findings`. Existing blocks therefore gain lines at the end, keeping diffs
readable.

No single file can carry every field at once — two pairs are mutually exclusive by invariant — so the
shape is shown as two states. Field **order** is identical in both.

**A flagged test, awaiting a human:**

```yaml
grounding:
  verdict: partial                        # existing
  score: 0.72                             # existing
  generator: claude-code-session          # existing
  critic: claude-opus-5                   # existing
  verified_at: 2026-08-20T09:14:00Z       # existing
  unverified_claims:                      # existing, conditional
    - "Step 3: docs do not state the refund-email interval"
  flagged_for_review: true                # existing, conditional (emitted only when true)
  repair_attempts: 1                      # existing, conditional (emitted only when > 0)
  condensed_findings:                     # existing, conditional
    - element: "Step 3"
      reason: "refund-email interval absent from documentation"
  source: user-described                  # round-trip repaired
  created_by: Oleg Kuzmanov               # round-trip repaired
  note: "Raised by review — step 3 timing looks wrong."   # round-trip repaired
  reviewed_by: Oleg Kuzmanov              # new
  reviewed_at: 2026-08-28T10:15:00Z       # new
                                          # reraise_suppressed_at absent — see below
```

**The same test after a later pass suppressed its re-raise:**

```yaml
grounding:
  verdict: partial
  score: 0.70
  generator: claude-code-session
  critic: claude-opus-5
  verified_at: 2026-08-31T08:00:00Z       # advanced by the suppressing pass
  unverified_claims:
    - "Step 3: docs do not state the refund-email interval"
                                          # flagged_for_review absent — cleared by the review
  condensed_findings:
    - element: "Step 3"
      reason: "refund-email interval absent from documentation"
  source: user-described
  created_by: Oleg Kuzmanov
  note: "Confirmed against the support runbook; the assumption holds."
  reviewed_by: Oleg Kuzmanov
  reviewed_at: 2026-08-30T10:15:00Z
  reraise_suppressed_at: 2026-08-31T08:00:00Z   # new, machine-written
```

**Why two examples**, not one — both are invariants, and a single combined example would violate them:

- `reraise_suppressed_at` and `flagged_for_review: true` cannot coexist (guarantee 9). The marker means
  *currently* suppressed, and a flagged test withholds nothing.
- `repaired: true` requires `verdict: grounded`, so it cannot appear on a `partial` test either. It is
  omitted from both examples for that reason.

`source`, `created_by`, and `note` are marked *round-trip repaired* because they are already parsed
from disk today and then silently discarded on write — the DTO accepts them, `ToMetadata()` ignores
them, and the writer never emits them. `Agent/GroundedPromptBuilder.cs:123–128` actively instructs
the model to populate all three on the user-described path, so today's behaviour is a live defect,
not an unused feature (research R9).

---

## String escaping (FR-006, research R4)

Free-text fields are emitted as YAML double-quoted scalars. The current escape helper handles `"` and
newline but **not backslash**, which is itself an escape character in a double-quoted scalar. The
result today:

| Note content | Emitted | Parsed back as |
|---|---|---|
| `C:\temp\build` | `"C:\temp\build"` | `C:<TAB>emp<BS>uild` — silent corruption |
| `ends with backslash \` | `"ends with backslash \"` | unterminated scalar — the whole file fails to parse |
| text with a literal CR | raw CR inside the scalar | line break inside a quoted scalar |

**Required escape order** — backslash first, or step 2's escapes get double-escaped:

1. `\` → `\\`
2. `"` → `\"`
3. LF → `\n`
4. CR → `\r`

Applies to `note`, `created_by`, `source`, `condensed_findings[].reason`, and `unverified_claims[]` —
they share one helper, and the last two carry the same latent exposure today via critic text quoting
a documented file path.

**Refusal, not mangling.** Where a value still cannot be represented faithfully, the action is
refused with `NOTE_UNREPRESENTABLE` and **no file is modified**. Validation runs before the write.
Silent truncation or alteration is forbidden (FR-006).

**Round-trip is the test.** For any accepted note, parse-then-write-then-parse yields the identical
string (SC-008). The adversarial corpus: embedded quote, embedded backslash, trailing backslash,
Windows path, embedded newline, embedded CR, leading/trailing whitespace, a colon-space sequence, a
leading `#`, a leading `-`, and a string long enough to exceed any line-wrapping the writer might
apply.

---

## What is *not* in this contract

- **`_index.json` / `TestIndexEntry`** — untouched by design (spec Decision 2, AC-8). Review state is
  invisible to `spectra list`, the dashboard, and `spectra run`. The field names here are the ones a
  follow-up "surface review state" spec should propagate unchanged, so that spec needs no rename.
- **`status:` / `orphaned_reason:` / `orphaned_date:`** — the write-only frontmatter fields. They are
  emitted by the writer but have no alias on the frontmatter DTO and are not mapped by the parser, so
  they are erased by every rewrite, including the ones this feature performs. That defect is
  explicitly out of scope; it is named here so a reader does not mistake the erasure for something
  this feature introduced.
- **`.spectra/verdicts/critic-verdict-{id}.json`** — unchanged. It remains the full critic findings
  and remains gitignored, which is precisely why the review record must live in frontmatter instead.

---

## Compatibility matrix

| File written by | Read after this feature | Result |
|---|---|---|
| Pre-feature SPECTRA, no grounding block | ✔ | Parses as today; grounding null |
| Pre-feature SPECTRA, grounding block | ✔ | Parses as today; the six new fields null |
| Pre-feature SPECTRA, block containing `source`/`created_by`/`note` | ✔ | **Now retained** on the next write instead of silently dropped |
| This feature, full review record | ✔ | All fields round-trip |
| This feature, then read by pre-feature SPECTRA | ✔ | Unknown keys are ignored by the DTO; the block parses, and the review record is dropped on the next write by that older binary |

The last row is the only lossy direction and it requires running an older binary against newer files.
No mitigation is planned — the project ships one binary per workspace.
