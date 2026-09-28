# Feature Specification: Human Review Records & Reviewer Feedback

**Feature Branch**: `078-human-review-record`
**Created**: 2026-08-28
**Status**: Draft
**Input**: User description: "Add a durable 'reviewed by human' record for test cases, and extend the existing `spectra-review-flagged` skill so a reviewer can leave feedback on a test or trigger a fix directly from chat."

## Overview

SPECTRA flags AI-generated test cases for human attention when the critic verdict comes back
`partial`, and a reviewer can act on those flags today (retry-repair, accept, delete). What is
missing is **memory**. Accepting a flagged test clears the flag and writes nothing else, so the
resulting file is indistinguishable from a test that was never flagged at all. Nobody can tell
who looked at it, when, or why they were satisfied.

This feature makes every human review decision a permanent, visible part of the test artifact,
protects that record from being erased by later automated verification, and lets a reviewer
record feedback — or ask for a fix — in a single conversational turn.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A reviewer's decision is remembered (Priority: P1)

A QA lead opens the list of tests awaiting review, reads the critic's concerns about one of them,
confirms against the product documentation that the test is actually fine, and accepts it with a
short explanation of why. Six weeks later a different team member opens that test file and can
see at a glance that it was reviewed, by whom, when, and on what basis — without asking anyone
or digging through chat history.

**Why this priority**: This is the entire point of the feature and the part with no workaround
today. Everything else builds on the record existing. Delivered alone, it already converts an
invisible, unattributable process into an auditable one.

**Independent Test**: Accept a flagged test with a note, then reopen the test file and confirm
the reviewer identity, timestamp, and note are present and legible. Re-read and re-save the file
with no command acting on it and confirm nothing is lost.

**Acceptance Scenarios**:

1. **Given** a test awaiting review, **When** the reviewer accepts it with an explanation,
   **Then** the test file records who reviewed it, when, and their explanation, and it is no
   longer listed as awaiting review.
2. **Given** a test awaiting review, **When** the reviewer accepts it without an explanation,
   **Then** the acceptance still succeeds and the reviewer identity and timestamp are recorded.
3. **Given** a test file that already carries a review record, **When** the file is read and
   written back without any review action taking place, **Then** every field of the review record
   is unchanged.
4. **Given** any review action, **When** it completes, **Then** the critic's own assessment (its
   verdict and confidence score) is left exactly as it was.
5. **Given** a set of tests where some carry review records and some do not, **When** the reviewer
   lists tests needing attention, **Then** each listed test shows whether a review record exists
   and what it says, so an already-commented test is distinguishable from an untouched one.

---

### User Story 2 - The record survives automated re-verification (Priority: P1)

Weeks after a reviewer signs off on a test, the team re-runs verification across the whole suite —
documentation has moved on, and the critic re-examines everything. The critic's verdict and score
for the reviewed test are legitimately updated. The human's sign-off is not touched.

**Why this priority**: Equal to P1 because without it the record from User Story 1 is worthless in
practice — routine batch verification is a normal, frequent operation, and a record that a routine
operation silently deletes is not a record. This is the single highest-risk behaviour in the
feature.

**Independent Test**: Write a review record on a test, run a full re-verification pass over its
suite, then confirm the review fields are byte-identical afterward while the critic fields have
legitimately changed.

**Acceptance Scenarios**:

1. **Given** a test carrying a review record, **When** an automated verification pass rewrites
   that test's verification metadata for reasons unrelated to review, **Then** the reviewer
   identity, timestamp, and note are unchanged afterward.
2. **Given** a test carrying a review record, **When** that same verification pass legitimately
   updates the critic's verdict, score, or verification timestamp, **Then** those updates are
   applied as normal — preservation applies to the human record only, not to the critic's fields.
3. **Given** a batch verification pass over many tests where only some carry review records,
   **When** the pass completes, **Then** every existing review record survives and no review
   record is invented for a test that never had one.
4. **Given** a test a reviewer accepted *after* its last recorded verification, **When** a
   verification pass returns a *partially verified* result, **Then** the automatic re-raise is
   suppressed and the test stays cleared — the human's decision is the more recent of the two
   (Decision 8) — while the critic's verdict, score, and verification timestamp still update.
5. **Given** a test whose last recorded verification is *newer* than its review record, **When** a
   verification pass returns a *partially verified* result, **Then** the test is marked as needing
   attention again and the review record is preserved alongside the new concern.
6. **Given** a test whose review and verification timestamps are identical, **When** a verification
   pass returns a *partially verified* result, **Then** the test is re-raised — equality resolves
   toward surfacing the concern rather than hiding it.
7. **Given** any test whose automatic re-raise was suppressed, **When** the reviewer inspects the
   listing or audit output, **Then** the suppression is reported along with the review that caused
   it, so a hidden concern is never invisible.

---

### User Story 3 - A reviewer leaves feedback without asking for a fix (Priority: P2)

A reviewer reading a test the critic was confident about spots a step referencing a field that
does not exist in the product. They are not ready to have it rewritten — maybe the documentation
is what is actually wrong — so they record what they found and move on. The test is now marked as
needing attention, carrying the reviewer's observation, and will show up for whoever picks it up
next.

**Why this priority**: This is what makes review a two-way process rather than a rubber stamp, and
it is the mechanism by which a confidently-but-wrongly verified test can be caught at all. Lower
than P1 only because the record infrastructure must exist first.

**Independent Test**: Leave feedback on a test that was never flagged, then confirm it now appears
in the awaiting-review list carrying the reviewer's note, with the critic's assessment untouched.

**Acceptance Scenarios**:

1. **Given** a test the critic verified confidently and that is not awaiting review, **When** the
   reviewer leaves feedback on it, **Then** it becomes a test awaiting review, carrying the
   reviewer identity, timestamp, and note, with the critic's verdict and score unchanged.
2. **Given** a test already awaiting review, **When** the reviewer leaves feedback on it, **Then**
   it remains awaiting review and the review record is updated to the reviewer's latest note.
3. **Given** a reviewer leaving feedback, **When** they supply no note, **Then** the action is
   rejected with a clear explanation that feedback requires a note — feedback without content
   records nothing useful.
4. **Given** a test whose verification was never performed by the critic, or which the critic
   rejected outright, **When** any review action is attempted against it, **Then** the action is
   refused with a specific reason.

---

### User Story 4 - A reviewer asks for a fix in the same breath (Priority: P3)

A reviewer spots the same problem but does want it fixed now. They say so, and their description
of the problem becomes the instruction that guides the rewrite — the fix addresses what the human
actually objected to, not just what the critic originally flagged. The test comes back rewritten
and freshly verified, with a record of who asked and why.

**Why this priority**: The highest-value convenience in the feature, but strictly additive — a
reviewer can already achieve the same outcome in two steps (leave feedback, then request a
repair). It also carries the most behavioural risk, so it benefits from landing after the
foundations are proven.

**Independent Test**: Leave feedback with a fix request on a flagged test, then confirm the test
content changed, verification was re-run, and the review record names the reviewer and reflects
the feedback that drove the rewrite.

**Acceptance Scenarios**:

1. **Given** a test awaiting review, **When** the reviewer leaves feedback and asks for it to be
   fixed, **Then** the test is rewritten, verification is re-run, and the resulting verdict and
   score are the fresh assessment — not the previous one carried forward.
2. **Given** that same action, **When** it completes, **Then** the review record names the
   reviewer, timestamps the request, and preserves the feedback text that guided the rewrite.
3. **Given** a reviewer's feedback text, **When** the rewrite is prepared, **Then** that text is
   part of what the rewrite is asked to address, alongside the critic's original concerns.
4. **Given** a reviewer requests a fix, **When** the rewrite or re-verification fails, **Then**
   the reviewer is told what failed, and the test is left in a state that is either fully
   pre-action or fully post-action — never a half-applied mixture.
5. **Given** a test that already carries a review record from an earlier reviewer, **When** a
   repair rewrites its content, **Then** the review record is replaced by the requester's — the
   record always describes the content currently on disk (Decision 7). The earlier review remains
   recoverable from version history.

---

### Edge Cases

- **A test the critic rejected outright appears on disk anyway.** No current operation writes such
  a test (see Appendix A, Finding 1), but nothing prevents a hand-edited or externally authored
  file from carrying that state, and nothing validates it on read. Review actions must refuse it
  explicitly rather than treating it as impossible.
- **A test created by hand rather than verified by the critic.** There is no critic assessment to
  confirm or correct, so review actions must refuse it with a specific reason.
- **A reviewer reviews an already-reviewed test.** The latest review replaces the previous one,
  including when a repair rewrites the content (Decisions 3 and 7). Prior values remain recoverable
  from version history.
- **A shared or automation machine account resolves as the reviewer.** Recorded as-is; the identity
  written is whatever resolution yields, with no plausibility check (Decision 6). A meaningless
  reviewer value is possible and will not be objected to.
- **A note containing characters that would break the file format** — quotation marks, colons, or
  line breaks. The record must survive a write-then-read cycle intact regardless of note content,
  or the note must be rejected with a clear reason before anything is written.
- **A very long note.** Behaviour must be defined rather than left to whatever the file format
  tolerates.
- **An accepted test is later re-verified and comes back needing attention again.** The re-raise is
  suppressed if the review is more recent than the test's last recorded verification; otherwise the
  test is re-raised with the review record preserved alongside the new concern (Decision 8,
  FR-010). Either way the critic's own fields update.
- **The review and verification timestamps are equal, or within the resolution of the timestamp
  format.** The test is **re-raised**. Ordering within one second is not reliable, and the failure
  modes are asymmetric: re-raising something already decided costs a reviewer one dismissal, while
  suppressing something new hides a real defect. This is the only boundary in the feature that
  resolves against the reviewer's convenience, deliberately.
- **A verification pass runs immediately after a review, in the same batch or minutes later.** The
  review is newer, so the pass is suppressed — but that pass also advances the verification
  timestamp, so the *next* pass will see verification as newer and re-raise. A review therefore
  suppresses the next verification pass and not the ones after it. See Decision 8 for why this is
  accepted and what removes it.
- **Documentation changes but no verification pass runs afterward.** Nothing re-raises the test, and
  the review record continues to stand even though what it was based on has moved. Suppression is
  not the cause here — no verification ran to be suppressed — but the effect on the reviewer is the
  same, and it is the reason Decision 8 records staleness as a real risk rather than a theoretical
  one.
- **A reviewer requests a fix, the rewrite succeeds, and re-verification then fails.** The test has
  changed but has no current assessment. The reviewer must be told, and the test must not be left
  appearing verified.
- **A test carrying a documentation-drift marker is accepted.** Today that marker is silently
  dropped by any operation that rewrites the file, because of a pre-existing defect explicitly out
  of scope here (see Out of Scope). Review actions inherit that behaviour; it must be recorded as a
  known interaction rather than discovered later.
- **Two reviewers act on the same test at nearly the same time.** Last write wins; there is no
  locking. Acceptable given review is a human-paced activity, but stated so it is not a surprise.

---

## Requirements *(mandatory)*

### Functional Requirements

**Recording the review**

- **FR-001**: The verification metadata carried by a test case MUST support recording the identity
  of the person who last reviewed it and the time at which they did so, both optional.
- **FR-002**: Test files MUST round-trip every field of the review record — the reviewer identity,
  the review time, and the reviewer's note — such that reading a file and writing it back
  unchanged loses nothing. This closes an existing defect: three annotation fields (a provenance
  marker, an authoring identity, and a free-text note) are read from files today and then silently
  discarded on write, so they never survive.
- **FR-003**: Any operation that rebuilds a test's verification metadata for a reason other than a
  human review action MUST first read the review record currently on disk and carry it forward
  unchanged. This includes routine re-verification and batch verification passes. Without this,
  ordinary automated operation erases human sign-off with no error or warning.
- **FR-004**: FR-003's preservation MUST be enforced at the single point where verification
  metadata is constructed from a critic result, so that every present and future caller inherits it
  by default rather than each having to remember it. A caller MUST NOT be able to lose the review
  record by omission.
- **FR-005**: The review record MUST hold only the most recent review, not a history. Prior values
  remain recoverable from version history (Decision 3).
- **FR-006**: A reviewer's note MUST survive a write-then-read cycle intact regardless of its
  content, including quotation marks, colons, and line breaks. Where a note cannot be represented
  faithfully, the action MUST be refused with a clear reason before any file is modified — a note
  MUST NEVER be silently truncated, altered, or written in a form that corrupts the file.

**Who may be reviewed**

- **FR-007**: A test MUST be eligible for review when its critic assessment is either *verified* or
  *partially verified*. A test the critic rejected outright, and a test created by hand without
  critic verification, MUST both be refused with a specific, distinguishable reason. Refusal MUST
  be explicit for both — neither may be treated as an unreachable case.
- **FR-008**: The restriction that only a *partially verified* test can be marked as needing
  attention MUST be lifted, so a reviewer can raise a concern about a test the critic verified
  confidently. This MUST be implemented at the point where the awaiting-review marker is actually
  determined today: that marker is currently derived mechanically from the verdict, with no way for
  any caller — human or otherwise — to request it. A caller-supplied path MUST be introduced.
  (See Appendix A, Finding 3: the declared validation rule expressing this restriction is not
  enforced anywhere, so relaxing that rule alone would change no behaviour.)
- **FR-009**: Automatic marking MUST be unchanged for tests with no review record: a *partially
  verified* result still marks the test as needing attention on its own. No automatic marking is
  introduced for confidently verified tests — those can only be raised by a human through the
  feedback action. Where a review record *is* present, FR-010 governs whether the marking applies.
- **FR-010**: A prior human acceptance MUST suppress an automatic re-raise when the acceptance is
  the more recent of the two. When a verification pass produces a *partially verified* result for a
  test that carries a review record, the pass MUST compare the review timestamp against the
  verification timestamp **already recorded on the test — the value present before this pass writes
  its own**, since a pass that compared against the timestamp it is about to write would find
  verification newer every time and never suppress anything. The outcome MUST be:
  - **Review timestamp later than the recorded verification timestamp** → the automatic re-raise is
    suppressed. The awaiting-review marker is left exactly as the human set it, and the review
    record stands. The critic's own fields still update as normal (FR-003, and User Story 2
    scenario 2) — suppression governs the marker only.
  - **Verification timestamp later than the review timestamp** → the test is re-raised as normal and
    the review record is preserved alongside the new concern.
  - **Timestamps equal, or no review record present** → the test is re-raised. Equality resolves
    toward re-raising because ordering within the resolution of the timestamp is not reliable, and
    surfacing a concern that turns out to be already-decided is a smaller harm than hiding one that
    is not.
- **FR-011**: A suppressed re-raise MUST be observable. The listing and audit surfaces MUST report,
  for any test whose re-raise was suppressed, that suppression occurred and which review suppressed
  it. A concern that was raised by verification and then hidden by a timestamp comparison MUST NOT
  be silently invisible — otherwise suppression converts a visible queue item into no signal at all.

**The four reviewer actions**

- **FR-012**: **Accept** MUST, in addition to clearing the awaiting-review marker, record the
  reviewer identity and review time, and record the reviewer's note when one was given. A note is
  optional on accept. The critic's verdict and score MUST be left unchanged.
- **FR-013**: **Request-repair** MUST, in addition to rewriting the test and re-running
  verification, record the reviewer identity, the review time, and a note describing why the
  rewrite was requested. The verdict and score written afterward are the fresh assessment's own
  output; this is expected and is not a violation of FR-012's protection, which governs accept and
  feedback. Where the test already carried a review record from an earlier reviewer, that record
  MUST be **replaced** by the requester's, not merged or preserved — the record always describes
  the content currently on disk (Decision 7). This is the one exception to FR-003's preservation
  rule, and it applies only because a repair deliberately changes the content the earlier record
  described.
- **FR-014**: **Delete** is unchanged. A deleted test carries nothing forward and there is nothing
  to record. The existing requirement to write the removal trail before deleting is untouched.
- **FR-015**: **Feedback** MUST be added as a fourth action. It MUST:
  - be invocable on any eligible test (FR-007) whether or not it is currently awaiting review;
  - require a note, and refuse the action with a clear reason if none is supplied;
  - record the reviewer identity, review time, and note;
  - mark the test as needing attention if it was not already;
  - leave the critic's verdict and score unchanged;
  - accept an optional request to fix the test immediately, which hands off to the request-repair
    flow (FR-013) with the reviewer's note included in what the rewrite is asked to address.
    Without that request, the test is left in place, marked as needing attention, with the review
    record written — commentary only, fix deferred.

**Reaching the actions**

- **FR-016**: All four actions MUST be invocable non-interactively, in a single command per action,
  with the target test, the note, the reviewer identity, and the fix request all supplyable as
  explicit arguments. Today only listing is available non-interactively; accept and delete exist
  solely as keystrokes inside an interactive session (Appendix A, Finding 4), which no automated or
  chat-driven flow can reach.
- **FR-017**: Test selection MUST allow the feedback action to target any eligible test, not only
  those currently awaiting review. Accept, request-repair, and delete remain restricted to tests
  currently awaiting review, since acting on an unmarked test through those three has no defined
  meaning.
- **FR-018**: The listing surface MUST report, for each test it lists, whether a review record is
  present and what it says, so a reviewer can distinguish a test nobody has looked at from one a
  colleague has already commented on.
- **FR-019**: Where an action applies across many tests, a batch form MUST be available so the
  conversational flow never needs to construct a per-test loop in a shell. This follows the
  project's established non-stop contract for skill-driven flows.

**Reviewer identity**

- **FR-020**: Reviewer identity MUST be resolved in two steps only: an explicitly supplied value
  first; otherwise the existing project-wide identity resolution (git-configured user name, falling
  back to the operating system user name), which is directly reusable from the reviewing surface
  (Appendix A, Finding 2 — this resolves an open question from the input as *no new resolver
  needed*).
- **FR-021**: Resolution MUST always succeed, and the resolved value MUST be recorded as-is. There
  MUST be no interactive identity prompt, no plausibility or format check on the resolved value,
  and no failure path for unresolvable identity — the second step always yields a value
  (Decision 6). The recorded identity MUST NEVER be blank or a literal placeholder; beyond that,
  whatever resolution returns is authoritative, including a shared or automation account name.
- **FR-022**: The recorded identity is a **name**, not necessarily an email address, since that is
  what the existing resolution yields. Any documentation or example showing the review record MUST
  reflect that, so readers do not expect email-shaped attribution.

**Guidance for the reviewing flow**

- **FR-023**: The reviewing skill's guardrails MUST be reworded so they clearly distinguish the
  critic's fields from the human's record. They MUST state that accept and feedback never modify
  the critic's verdict or score; that request-repair is the only action that changes them, and only
  as a consequence of the fresh verification it runs; and that all three human actions record the
  reviewer identity, time, and note in addition to — never instead of — the action's existing
  effect. The current wording forbids "changing verdict or score on accept" without saying whether
  that also barred writing a separate human record.
- **FR-024**: The reviewing skill MUST expose all four actions through plain language, so a
  reviewer never needs to name a command: approval ("looks fine, accept it"), rewrite ("regenerate
  this one"), removal ("this one's redundant, remove it"), commentary ("the step about X is wrong",
  "leave a note that…"), and commentary-plus-fix ("the step about X is wrong, fix it").
- **FR-025**: Documentation covering the reviewing command and the review workflow MUST be updated
  to describe the four actions, the review record fields, and the preservation guarantee.

### Key Entities

- **Review record** — the durable statement that a human examined a test's automated assessment.
  Comprises the reviewer's identity, the time of review, and their free-text note. Lives with the
  test's verification metadata inside the test file itself; no separate store. Presence of a
  reviewer identity is the single signal that a test has been human-reviewed — deliberately no
  separate boolean (Decision 4).
- **Awaiting-review marker** — the existing signal that a test still needs human attention.
  Unchanged in meaning; gains a second way to be set (by a human, via feedback) alongside the
  existing automatic one.
- **Critic assessment** — the verdict and confidence score the automated critic produced.
  Protected from human actions; only re-verification changes it.
- **Reviewer identity** — a resolved string, not a persisted entity. Sourced per FR-020.
- **Reviewer action** — one of accept, request-repair, delete, feedback. Each has a defined effect
  on the awaiting-review marker, the review record, and the test content.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of human review actions that complete successfully leave a review record on the
  test file naming the reviewer and the time — no action can succeed silently.
- **SC-002**: Zero review records are lost across repeated automated re-verification. Measured by
  reviewing a set of tests, then running verification over their suite three times, and confirming
  every record is byte-identical each time.
- **SC-003**: Anyone opening a reviewed test file can answer "was this checked by a person, by
  whom, when, and what did they say?" from the file alone, with no other tool or system.
- **SC-004**: A reviewer can record feedback, or feedback plus a fix request, in one conversational
  turn — no command names typed, no file paths given, no follow-up prompts beyond confirming the
  action.
- **SC-005**: Every review action is reachable without an interactive session, so an automated or
  chat-driven flow can complete a full review pass end to end without a human at a keyboard.
- **SC-006**: For every ineligible test, a review attempt fails with a message naming the specific
  reason. Zero ineligible tests are silently skipped or partially modified.
- **SC-007**: Existing test files with no review record continue to be read without error or
  change in behaviour. No migration step is required of any user.
- **SC-008**: A reviewer's note is recoverable exactly as written, for every note that was
  accepted, across a full write-and-read cycle.
- **SC-009**: Every recorded review names a non-blank reviewer, in 100% of completed actions, with
  no interactive prompt required at any point.
- **SC-010**: A reviewer is not asked to re-decide a test they have already decided, for as long as
  their decision is the most recent event on that test. Measured by accepting a set of tests and
  running one verification pass: zero of them return to the needs-attention list.
- **SC-011**: Every suppressed re-raise is visible on inspection, in 100% of cases, naming the
  review that suppressed it. No concern raised by verification is hidden without a trace — this is
  what keeps suppression auditable rather than silent.

---

## Design Decisions

These were settled in the input and are carried forward. Decision 5 is new — see Appendix A.

1. **Reviewability is not restricted to partially-verified tests.** Confidently-but-wrongly
   verified tests are exactly the failure mode a human reviewer exists to catch. Tests the critic
   rejected, and tests created by hand, stay out (FR-007). Automatic marking behaviour is
   unchanged (FR-009); this only widens what a human can do by hand.
2. **Review state does not propagate into the test index this spec.** Surfacing review state in
   listings, the dashboard, and execution filtering touches three independent consumers, each with
   its own open questions. Bundling that here would block the record fix — the part with no
   workaround today — behind unrelated scope. Field names chosen here are the ones a follow-up
   should propagate unchanged.
3. **One record, not a history.** Test files are version-controlled, so every prior review value
   is already recoverable. An in-file history array would duplicate that and complicate the format
   for no behavioural gain.
4. **Presence of the reviewer identity is the "reviewed" signal.** No separate boolean. The project
   already has one field that is written but never read back; a second flag needing to stay in sync
   with two other fields would recreate exactly that risk.
5. **The free-text note field is shared between manual-creation provenance and reviewer notes.**
   A hand-created test never enters the review workflow (FR-007), so the two meanings never need to
   coexist on one file. The field is documented as carrying both meanings depending on how the test
   was produced, and reviewer actions treat it as a straightforward overwrite.
6. **Reviewer identity is whatever resolution returns, with no failure path.** Resolved 2026-08-28
   (Q1 → A). An explicitly supplied value wins; otherwise the project's existing resolution applies
   and always succeeds. No interactive prompt, no plausibility check, no unresolvable-identity
   error. Rationale: this matches how the rest of the project already attributes work, and the
   existing resolver cannot fail (Appendix A, Finding 2), so a failure path would be dead code.
   **Accepted consequence**: a shared or automation machine account can be recorded as the reviewer
   and nothing will object. If that becomes a problem in practice, tightening the rule is a small,
   self-contained follow-up.
7. **A repair replaces any earlier review record.** Resolved 2026-08-28 (Q2 → A). When a repair
   rewrites a test's content, the requester's record overwrites whatever was there, so the record
   always describes the content currently on disk. Rationale: carrying a record forward across a
   rewrite would assert that a human reviewed content they never saw. The earlier review stays
   recoverable from version history, consistent with Decision 3. This is the sole exception to
   FR-003's preservation rule.
8. **A more recent human acceptance suppresses the automatic re-raise.** Resolved 2026-08-28
   (Q3 → **B**; revised from an initial Q3 → A the same day). When a verification pass returns a
   *partially verified* result for a test carrying a review record, the pass compares the review
   timestamp against the verification timestamp already on the test. Review newer → the re-raise is
   suppressed and the human's decision stands. Verification newer, or the two equal → the test is
   re-raised. The critic's own fields update either way; suppression governs the awaiting-review
   marker only. Rationale: the reviewer's decision should hold until something actually changes, and
   re-raising a test on every pass regardless is the fastest route to the review queue being
   ignored entirely.

   **Comparison baseline (load-bearing).** The comparison MUST use the verification timestamp
   *present on the test before the pass writes its own*. Compared against the timestamp the pass is
   about to write, a review could never be newer and suppression would never fire — the rule would
   silently collapse back into Q3 → A. FR-010 states this explicitly for that reason.

   **Accepted consequence 1 — suppression lasts exactly one pass per review event.** A suppressing
   pass still advances the verification timestamp (that update is required by FR-003 and User Story 2
   scenario 2), so the *next* pass sees verification as newer and re-raises. In practice a review defers
   a test past the next verification run, not indefinitely.

   *Caveat, for precision:* a test can be suppressed again on a later pass **only if it is reviewed
   again in the interim**, refreshing `reviewed_at` past the `verified_at` the previous pass wrote.
   Absent a new review, the next verification pass always finds `verified_at ≥ reviewed_at` and
   re-raises. So the guarantee is per *review event*, not per test: each review buys exactly one
   suppressed pass. Note also that the re-review path returns through the *cleared* state rather than
   re-suppressing directly from *suppressed* — a suppressed test is unflagged, so the only review action
   it is eligible for is `feedback`, which flags it and clears the marker. There is therefore no
   suppressed-to-suppressed transition; see data-model.md's transition table. This is inherent to comparing clocks rather than
   substance: the durable variant is to suppress while the *assessment itself* is unchanged —
   same verdict and same set of non-grounded findings as the reviewer saw — which does not expire
   because it tracks what the human actually ruled on. That is a self-contained follow-up requiring
   no field change beyond what this spec already introduces, and it is the recommended path if
   one-pass deferral proves too weak.

   **Accepted consequence 2 — a review can go stale.** Documentation can change without any
   verification pass running afterward, in which case nothing re-raises the test and the review
   continues to stand on a basis that has moved. A stale approval can therefore mask a real
   divergence for as long as verification is not re-run. Under Q3 → A this could not happen; it is
   the price of suppression.

   **Mitigation.** FR-011 requires every suppressed re-raise to be reported by the listing and audit
   surfaces, naming the review that suppressed it. Without that, suppression would turn a visible
   queue item into no signal at all, which would be strictly worse than either answer to Q3.

---

## Out of Scope

- Surfacing review state in the test index, and any resulting listing, dashboard, or execution
  filtering on it (Decision 2 — follow-up spec).
- Fixing the pre-existing defect where the documentation-drift marker is written to test files but
  never read back, and so is erased by any rewrite. Named here only because review actions rewrite
  files and therefore inherit the erasure (see Edge Cases), and because it is the reason review
  state must not live in that field.
- An in-file review history log (Decision 3).
- Any change to how hand-created tests are produced or annotated.
- Enforcing the currently-unenforced validation rules on verification metadata beyond what FR-008
  requires (Appendix A, Finding 3). Turning that validation on across the board is its own change
  with its own blast radius.

---

## Assumptions

- **Non-interactive action flags are required, not optional.** The input's requirements are written
  as though a non-interactive accept already exists; it does not (Appendix A, Finding 4). Because
  the project's established contract for skill-driven flows prohibits interactive-only steps and
  per-test shell loops, FR-016 and FR-019 treat the non-interactive surface as in scope rather than
  asking. This is the largest piece of scope the input did not account for.
- **Review time is recorded in UTC**, in the same format the surrounding verification timestamps
  already use, for consistency within the file.
- **A note supplied to accept is optional; a note supplied to feedback is required.** Feedback
  without content records nothing a later reader could act on.
- **Last write wins** when two reviewers act on the same test concurrently. Review is human-paced;
  locking is not justified.
- **The recorded identity is a name, not an email address.** The input's examples show email
  addresses; the existing resolution reads the git-configured *user name*. Settled by Decision 6 —
  the resolved name is used as-is, and documentation/examples must reflect that (FR-022).
- **The follow-up index spec will reuse these field names unchanged**, so no rework is created by
  the Decision 2 deferral.

---

## Resolved Clarifications

Three questions were raised during specification and answered by the feature owner on 2026-08-28.
Each is now a design decision; the rationale and accepted consequences are recorded in Design
Decisions 6, 7, and 8 rather than repeated here. No open questions remain.

| # | Question | Answer | Landed as |
|---|----------|--------|-----------|
| Q1 | What counts as an unusable reviewer identity? | **A** — accept whatever resolves; drop the failure path entirely | Decision 6, FR-020, FR-021, FR-022 |
| Q2 | After a repair rewrites a test, does the earlier review record survive? | **A** — overwrite with the repair requester's record | Decision 7, FR-013, US4 scenario 5 |
| Q3 | When re-verification re-raises a test a human already accepted, who wins? | **B** — suppress the re-raise when the review post-dates the last verification (revised from A) | Decision 8, FR-010, FR-011, US2 scenarios 4–7 |

**Q3 was answered A and then revised to B** on 2026-08-28. Everything written around the original
answer has been rewritten: Decision 8, FR-010 (now requires the comparison it previously forbade),
FR-011 (new — suppression must be observable), User Story 2 scenarios 4–7, three edge cases, and
SC-010/SC-011. No text asserting "automatic marking always wins" remains.

Specifying B surfaced one thing the option summary did not: the comparison baseline must be the
verification timestamp *already on the test*, not the one the pass is about to write, or suppression
can never fire and B silently degrades into A. FR-010 pins this down, and Decision 8 records the two
consequences that follow — suppression lasting a single pass, and reviews going stale when
documentation changes with no verification run behind it.

Two requirements from the input were **dropped** as a direct consequence of Q1 → A, and are listed
here so their absence is not read as an omission:

- The input's three-step identity resolution ended in "if neither resolves and running
  interactively, prompt the reviewer once per session." That step is gone — the second step always
  succeeds, so a prompt would be unreachable.
- The input's acceptance criterion for `--no-interaction` mode failing on unresolvable identity is
  gone for the same reason. It was untestable as written.

## Appendix A: Implementation context resolved during specification

The input asked that two assumptions be verified during `/speckit.plan`. Both were checked against
the code while writing this spec and are resolved below, along with two further findings that
change what the plan must implement. This appendix is the one place in this document that names
internal code locations; requirements above are stated behaviourally so they remain testable
independently of these details.

### Finding 1 — A critic-rejected test cannot be written by any current operation, but can exist on disk

**Resolves the input's first open assumption.** The service that builds verification metadata from
a critic result explicitly refuses a rejected verdict and returns a distinct refusal outcome, and
it is the only place in the codebase that constructs verification metadata from a critic result.
A second conversion method that would have passed a rejection through has **no callers at all** —
it is a leftover from a retired in-process pipeline.

However, the frontmatter reader accepts the rejected value, and the file writer re-emits it. A
hand-edited or externally authored file can therefore carry that state and round-trip through the
system unchallenged. **Conclusion**: FR-007 must reject it explicitly rather than treat it as
unreachable, which is what the input's fallback position anticipated.

### Finding 2 — The identity resolver is directly reusable; no new resolver is needed

**Resolves the input's second open assumption, and corrects its premise.** The project the input
names (`Spectra.MCP`) no longer exists — it was deleted when the execution surface was consolidated
to the CLI. The resolver now lives in the shared execution library, which the CLI project already
references directly. It is reachable as-is; no parallel implementation is required, and no
behaviour in the execution library changes.

**But it cannot fail.** It resolves the git-configured user name and falls back to the operating
system user name, which is always available. It never returns null or empty. This makes the
input's third resolution step and its corresponding acceptance criterion unreachable as written —
so both were dropped (Decision 6). It also returns a *name*, not an email address, which the
input's examples assumed — see FR-022.

### Finding 3 — The validation rule the input targets is not enforced anywhere

**New finding; changes what FR-008 must modify.** The declared validation method on verification
metadata — which holds that the awaiting-review marker requires a partially-verified verdict, that
a repaired marker requires a verified verdict, and that a partial verdict requires at least one
unverified claim — has **zero callers in the codebase**. Every one of those rules is currently
unenforced.

Relaxing that rule, as the input proposes, would therefore change no behaviour at all. The actual
enforcement point is the single line in the write-back service that *derives* the marker
mechanically from the verdict. There is no parameter, flag, or other path by which any caller can
request the marker. **Conclusion**: FR-008 must introduce a caller-supplied path at that
derivation point. Whether to switch the dormant validation on is a separate decision, listed under
Out of Scope.

### Finding 4 — There is no non-interactive accept, and no `--action` argument

**New finding; adds scope the input did not account for.** The reviewing command accepts only a
suite filter and a listing-only switch. Accept and delete are reachable *solely* as keystrokes
inside its interactive loop. The input's requirements are written against an `--action accept`
argument that does not exist, and the reviewing skill already concedes in its own text that the
interactive command is the only accept path available.

Because the project's contract for skill-driven flows prohibits interactive-only steps and
per-test shell loops, a chat-driven review flow cannot be built on the current surface at all.
**Conclusion**: FR-016 and FR-019 are load-bearing, not polish — this is the largest addition to
the input's scope.

### Finding 5 — Where the review record must be preserved

The write-back service rebuilds verification metadata from scratch on every call, copying selected
fields from the parsed original. It is the single construction point for critic-derived metadata,
which makes it the correct and sufficient place to satisfy FR-003 and FR-004: preserving there
covers routine re-verification, batch verification, and any future caller, with no per-caller
obligation. Two other services perform their own field-by-field copies when rewriting a test and
must be checked for the same omission.

### Corrections to stale references in the input

- References to "the MCP execution engine" as a consumer no longer apply; there is no MCP
  execution surface, and the CLI run command is the sole execution path.
- The retired `Spectra.MCP` project is named twice in the input; see Finding 2.
- The input's spec-slug guess (`078-human-review-record`) matched the next available number exactly.
