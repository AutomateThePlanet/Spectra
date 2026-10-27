---
description: "Task list for 103-human-review-record"
---

# Tasks: Human Review Records & Reviewer Feedback

**Input**: Design documents from `/specs/103-human-review-record/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Each contract in `contracts/` carries an explicit *"Invariants a test suite must
assert"* list (15 + 15 + 9 items), and the constitution's Development Workflow requires tests for
public APIs and critical paths. Test tasks below implement those enumerated invariants — they are not
speculative additions.

**Organization**: Grouped by user story. The shared data-model work is in Phase 2 because all four
stories need the fields to exist and round-trip before any reviewer-facing behaviour is meaningful.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: `[US1]`–`[US4]`, mapping to spec.md's user stories
- Paths are repository-relative and exact

## Path Conventions

Existing three-project solution (plan.md → Structure Decision). No new projects.

- `src/Spectra.Core/` — models, parsing
- `src/Spectra.CLI/` — commands, IO, verification
- `src/Spectra.Execution/` — referenced, **not modified** (`UserIdentityResolver` consumed as-is)
- `tests/Spectra.Core.Tests/`, `tests/Spectra.CLI.Tests/` — all new tests extend existing files

---

## Phase 1: Setup

**Purpose**: Make the repo buildable, and capture a baseline to measure against.

- [X] T001 Resolve the SDK pin blocker in `global.json` — it pins `9.0.314` with `rollForward: latestFeature`, which does not cross a major, and only `10.0.101`/`10.0.102` are installed, so every `dotnet` invocation fails ("Install the [9.0.314] .NET SDK or update global.json"). Either install a 9.0.3xx SDK or bump `global.json` to `10.0.x`. This is a repo-wide, CI-affecting decision — get maintainer sign-off, do not fold it silently into a feature commit. See plan.md Risks.
- [X] T002 Capture the green baseline after T001 (verification step, no source file): run `dotnet test` and record per-project counts (expected ≈ Core 568 / CLI 1276 / Execution 228 per CLAUDE.md) in the PR description, so any later regression is attributable.

**⚠️ T001 blocks everything.** Until it is done, no task below can be verified — "tests pass" is not an available completion signal.

**Checkpoint**: `dotnet build` and `dotnet test` both run; baseline counts recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The six frontmatter fields exist, round-trip, and survive adversarial note content. No
behaviour changes yet — this phase is pure data model, and every user story depends on it.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 [P] Add six `init` members to `src/Spectra.Core/Models/Grounding/GroundingMetadata.cs`: `ReviewedBy` (string?), `ReviewedAt` (DateTimeOffset?), `Source` (string?), `CreatedBy` (string?), `Note` (string?), `ReraiseSuppressedAt` (DateTimeOffset?). Per data-model.md's field table (FR-001).
- [X] T004 [P] In `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs`, add `[YamlMember]` aliases `reviewed_by`, `reviewed_at`, `reraise_suppressed_at`, and — critically — **map the three already-declared-but-dropped members** (`source`, `created_by`, `note`) inside `ToMetadata()`, which currently ignores all three (FR-002, research R9).
- [X] T005 Revise `GroundingMetadata.IsValid()` in the same file per data-model.md's invariants table: change `FlaggedForReview ⇒ Partial` to `FlaggedForReview ⇒ Verdict ∈ {Grounded, Partial}` (FR-008), and add `ReviewedAt` non-null ⟺ `ReviewedBy` non-null; `ReraiseSuppressedAt` ⇒ `ReviewedBy` non-null; `ReraiseSuppressedAt` ⇒ `FlaggedForReview` false; `Verdict ∈ {Hallucinated, Manual}` ⇒ `ReviewedBy` null. Do **not** add callers — enabling enforcement is out of scope (spec Out of Scope). Depends on T003.
- [X] T006 Fix `EscapeYamlString` in `src/Spectra.CLI/IO/TestFileWriter.cs` (currently lines ~212–215): escape in the order `\` → `\\`, then `"` → `\"`, then LF → `\n`, then CR → `\r`. Backslash **must** come first or step 2's escapes get double-escaped. Today a note containing `C:\temp` is silently corrupted to a tab and a trailing backslash breaks frontmatter parsing (FR-006, research R4).
- [X] T007 Emit the six new fields conditionally in `TestFileWriter.cs`'s `grounding:` block, appended after `condensed_findings` in data-model.md's documented order; omit any null field entirely so existing files stay byte-identical (FR-002, SC-007). Depends on T003, T006.
- [X] T008 Add a refusal path in `src/Spectra.CLI/IO/TestFileWriter.cs` for a note that cannot be represented faithfully even after escaping — validate **before** any write, return `NOTE_UNREPRESENTABLE`, modify no file (FR-006). Depends on T006.
- [X] T009 [P] Extend `tests/Spectra.Core.Tests/Grounding/GroundingMetadataTests.cs`: all six fields survive `GroundingFrontmatter` → `ToMetadata()` → `GroundingMetadata`, including the three previously-dropped ones (AC-1).
- [X] T010 [P] Extend `tests/Spectra.Core.Tests/Models/Grounding/GroundingModelsTests.cs` with the revised and new `IsValid()` rules from T005, one test per rule.
- [X] T011 [P] Extend `tests/Spectra.CLI.Tests/IO/TestFileWriterGroundingTests.cs` with the adversarial note corpus from grounding-frontmatter.md: embedded quote, embedded backslash, **trailing** backslash, Windows path, embedded LF, embedded CR, leading/trailing whitespace, `: ` sequence, leading `#`, leading `-`, and an over-long string. Each must round-trip byte-identically or be refused before write (FR-006, SC-008).
- [X] T012 Audit and fix the three read-modify-write field-by-field copy sites for the same field-drop omission: `CopyWithGrounding` in `src/Spectra.CLI/IO/GroundingWriteBackService.cs`, `CopyWithGrounding` in `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs`, and the copy in `src/Spectra.CLI/Generation/UpdatedTestIngestor.cs`. All three already copy `Status`, which is write-only and always null on re-read — leave that alone (spec Out of Scope) but do not add to it.

**Checkpoint**: The fields exist and round-trip. Notes with backslashes no longer corrupt frontmatter. `spectra validate` still green. No reviewer-facing behaviour has changed yet.

---

## Phase 3: User Story 1 — A reviewer's decision is remembered (Priority: P1) 🎯 MVP

**Goal**: A reviewer can accept a flagged test, with or without a note, and the file permanently
records who reviewed it and when. The listing distinguishes an already-commented test from an
untouched one.

**Independent Test**: Accept a flagged test with a note; reopen the file and confirm reviewer,
timestamp, and note are present; re-read and re-save with no command acting on it and confirm nothing
is lost; confirm `verdict` and `score` are byte-identical to before.

### Implementation

- [X] T013 [US1] Add reviewer-identity resolution to `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs`: explicit `--reviewer` value first, else `Spectra.MCP.Identity.UserIdentityResolver.GetCurrentUser()` (reachable via the existing `Spectra.Execution` project reference — `src/Spectra.CLI/Spectra.CLI.csproj:10`). **No prompt and no failure path** — the resolver cannot fail (FR-020, FR-021, research R6).
- [X] T014 [US1] Extend `src/Spectra.CLI/Commands/Review/ReviewFlaggedCommand.cs` with `--action`, `--test`, `--all`, `--note`, `--reviewer`, and the rejected-combination validation from contracts/review-flagged.md (`TARGET_REQUIRED`, `TARGET_AMBIGUOUS`, `NOTE_REQUIRED`, `BULK_DELETE_REFUSED`) at exit code 2. Keep `--suite` and `--no-interaction` behaviour unchanged (FR-016).
- [X] T015 [US1] Add the eligibility gate to `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs`: allow verdict `grounded` or `partial`; refuse with **distinguishable** codes at exit 4 — `VERDICT_INELIGIBLE_MANUAL`, `VERDICT_INELIGIBLE_HALLUCINATED`, `NOT_VERIFIED`, `NOT_FLAGGED`. `hallucinated` needs a real branch, not an assertion that it cannot occur (FR-007, research R5). Modify nothing on refusal.
- [X] T016 [US1] Rework `AcceptAsync` in `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs` to write `ReviewedBy`, `ReviewedAt`, and `Note` (when given) in addition to clearing `FlaggedForReview`, leaving `Verdict` and `Score` byte-identical. A bare accept with no note stays valid (FR-012). Depends on T013.
- [X] T017 [US1] Extend `FindFlaggedAsync` and the listing result in `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs` so each listed test reports `reviewed_by`, `reviewed_at`, `note`, and `reraise_suppressed_at` (always null inside `tests`) (FR-018).
- [X] T018 [US1] Route the interactive Spectre.Console loop in `src/Spectra.CLI/Commands/Review/ReviewFlaggedCommand.cs` through the same handler methods the `--action` path uses, so the two cannot drift.

### Tests for User Story 1

- [X] T019 [P] [US1] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs`: accept with a note records reviewer/timestamp/note; bare accept records reviewer/timestamp; `verdict` and `score` unchanged in both (contracts/review-flagged.md invariants 1–2; AC-3).
- [X] T020 [P] [US1] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — eligibility refusals: `manual` and `hallucinated` return **distinct** codes, no file is written; `accept` on an unflagged test returns `NOT_FLAGGED` (invariant 4; AC-6).
- [X] T021 [P] [US1] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — identity: every completed action writes a non-blank `reviewed_by` with no prompt at any point (invariant 6; SC-009). Note there is no unresolvable-identity case to test — spec Decision 6.
- [X] T022 [P] [US1] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — interactive-vs-`--action` parity: the same decision through either path produces identical on-disk results (invariant 9).
- [X] T023 [P] [US1] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — listing reports the review record per test, so a commented test is distinguishable from an untouched one (US1 scenario 5).

**Checkpoint**: A reviewer's accept is durable and attributable. **Not yet shippable alone — see US2.**

---

## Phase 4: User Story 2 — The record survives automated re-verification (Priority: P1)

**Goal**: Routine re-verification updates the critic's fields and leaves the human's record untouched.
A newer review suppresses an automatic re-raise, and every suppression is visible.

**Independent Test**: Write a review record, run a full re-verification pass over the suite, confirm
the review fields are byte-identical while the critic fields legitimately changed; confirm a
suppressed re-raise is reported by both the listing and the audit.

**Why P1 alongside US1**: a record that routine batch verification silently deletes is not a record.
US1 without US2 invites trust it cannot honour (plan.md, Phase 1+2 note).

### Implementation

- [X] T024 [US2] Implement preservation in `src/Spectra.CLI/IO/GroundingWriteBackService.cs`'s `WriteAsync`: read the parsed original's `Source`, `CreatedBy`, `Note`, `ReviewedBy`, `ReviewedAt` and carry all five forward unchanged into the newly constructed metadata. This is the single construction point for critic-derived metadata, so no caller carries the obligation (FR-003, FR-004, research R1).
- [X] T025 [US2] Replace the mechanical flag derivation at `GroundingWriteBackService.cs:93` (`FlaggedForReview = result.Verdict == Partial && !repaired`) with a caller-supplied path, so a human can request the marker on a `grounded` test. Note the dormant `IsValid()` rule was **not** the enforcement point (FR-008, research R3/Finding 3).
- [X] T026 [US2] Implement the FR-010 suppression comparison in `WriteAsync`, using **`original.Grounding.VerifiedAt` — the pre-write value** — and the pre-write `FlaggedForReview == false` clause. Full predicate in data-model.md. Equality re-raises. **This is the highest-risk line in the feature**: compared against the timestamp the pass is about to write, suppression can never fire and the feature silently degrades into the rejected Q3 → A behaviour.
- [X] T027 [US2] Set and clear `ReraiseSuppressedAt` per data-model.md's group-3 outcome table: set/advance on suppression; **cleared** on re-raise, on a non-`partial` verdict, and whenever the pre-write flag was true. It is never carried forward (FR-011). Depends on T026.
- [X] T028 [US2] Add `--re-verify` to `src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs`'s batch path, bypassing the already-has-grounding skip. Without it a second pass writes nothing, `WriteAsync` is never reached, and FR-003/FR-010 are unreachable in the real flow. **Do not remove the default skip** — Specs 073/077 depend on repeat-safety (research R2).
- [X] T029 [US2] Report suppression and preservation in both `ingest-grounding` result shapes: per-test `reraise_suppressed`, `suppressed_by_review_at`, `review_record_preserved`; batch `re_verify`, `reraises_suppressed`, `review_records_preserved`. Name suppressed ids in human output (contracts/ingest-grounding.md).
- [X] T030 [US2] Extend `src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs`: add the fourth `action_needed` value `reviewed`; add `reviewed_by`/`reviewed_at`/`note`/`reraise_suppressed_at` per entry; add `reviewed`, `reraises_suppressed`, and `reviews_stale` counters with the **disjoint** predicates from contracts/audit-grounding.md; warn on the impossible flagged-plus-marker combination without changing `action_needed`.
- [X] T031 [US2] Add the `suppressed[]` array and `suppressed_count` to the listing in `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs` (result model alongside `src/Spectra.CLI/Commands/Review/ReviewFlaggedCommand.cs`). Suppressed tests are unflagged, so a flagged-only listing would render withheld concerns invisible — the exact failure FR-011 exists to prevent. Keep `flagged_count`/`tests` shape unchanged for Spec 071/072 consumers.

### Tests for User Story 2

- [X] T032 [P] [US2] **The baseline regression guard**, in `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs`: a pass that writes a newer `verified_at` still suppresses based on the pre-write value (contracts/ingest-grounding.md invariant 1). If only one test from this feature survives, it is this one.
- [X] T033 [P] [US2] In `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs` — equality re-raises rather than suppresses (invariant 2).
- [X] T034 [P] [US2] In `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs` — no re-suppression without a new review: suppress at T2 given `reviewed_at = T1`, then run a second pass at T3 with `reviewed_at` unchanged — assert re-raised, `reraise_suppressed_at: null`, never suppressed again (invariant 7).
- [X] T035 [P] [US2] In `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs` — re-suppression is valid after a new review: from the post-suppression state, refresh `reviewed_at` to T3 > T2 via `feedback` then `accept`, then run a pass at T4 — assert suppressed again with the marker at T4. The *from* state is `cleared`; do **not** assert a direct `suppressed → suppressed` transition, which is unreachable by construction (invariant 8).
- [X] T036 [P] [US2] In `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs` — a human's raise is never suppressed: a test flagged by `feedback` at T3 with `verified_at = T0 < T3`, then a `partial` pass at T5 — assert still flagged, marker null. The timestamp comparison alone would have suppressed here; this guards the pre-write-flag clause (invariant 9).
- [X] T037 [P] [US2] In `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs` — marker lifecycle: cleared on re-raise, cleared when the next verdict is `grounded`, and `reraise_suppressed_at` non-null ⇒ `flagged_for_review` false after any pass (invariants 6, 11, 12).
- [X] T038 [P] [US2] In `tests/Spectra.CLI.Tests/Commands/IngestGroundingCommandTests.cs`: without `--re-verify` an already-grounded test is skipped and its record untouched (**hard regression gate** for Specs 073/077); with `--re-verify` the same test is rewritten, record byte-identical, critic fields updated (invariants 3–4; AC-2).
- [X] T039 [P] [US2] In `tests/Spectra.CLI.Tests/Commands/IngestGroundingCommandTests.cs` — three consecutive `--re-verify` passes leave the review record byte-identical each time (invariant 5; SC-002).
- [X] T040 [P] [US2] In `tests/Spectra.CLI.Tests/Commands/IngestGroundingCommandTests.cs` — per-test and batch `--re-verify` produce identical on-disk results (invariant 15).
- [X] T041 [P] [US2] In `tests/Spectra.CLI.Tests/Commands/AuditGroundingCommandTests.cs`: `reviewed` vs `none` distinction; the two counters never double-count; a lapsed suppression counts in neither; the three existing `action_needed` values unchanged for tests with no review record (**hard regression gate** — Spec 072's repair-batch resume reads this field) (audit invariants 1–8).

**Checkpoint**: US1 + US2 together are the shippable MVP. The record is durable across routine operation, and no concern is withheld invisibly.

---

## Phase 5: User Story 3 — A reviewer leaves feedback without asking for a fix (Priority: P2)

**Goal**: A reviewer can raise a concern on any eligible test — including one the critic verified
confidently — recording what they found without triggering a rewrite.

**Independent Test**: Leave feedback on a `grounded` test that was never flagged; confirm it now
appears in the awaiting-review list carrying the note, with `verdict` and `score` unchanged.

### Implementation

- [X] T042 [US3] Add `FeedbackAsync` to `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs`: require a note, write `ReviewedBy`/`ReviewedAt`/`Note`, set `FlaggedForReview: true` if not already set, clear `ReraiseSuppressedAt`, leave `Verdict`/`Score` unchanged (FR-015). Depends on T025 for the caller-supplied flag path.
- [X] T043 [US3] Wire `feedback` into the `--action` option in `src/Spectra.CLI/Commands/Review/ReviewFlaggedCommand.cs`, with `NOTE_REQUIRED` at exit 2 when `--note` is absent.
- [X] T044 [US3] Relax test selection in `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs` so `feedback` can target **any** eligible test, not only currently-flagged ones, while `accept`/`retry-repair`/`delete` stay restricted to flagged tests (FR-017).
- [X] T045 [US3] Implement `--all` batch mode across `src/Spectra.CLI/Commands/Review/ReviewFlaggedCommand.cs` and `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs`: process every eligible target in scope, skip-and-report ineligible ones with their reason rather than dropping them, and never fail the batch on a skip (FR-019, SC-006).

### Tests for User Story 3

- [X] T046 [P] [US3] `feedback` on an unflagged `grounded` test sets the marker and records the review, with `verdict`/`score` unchanged (contracts/review-flagged.md invariant 3; AC-4).
- [X] T047 [P] [US3] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — `feedback` with no `--note` exits 2 and modifies nothing (invariant 3).
- [X] T048 [P] [US3] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — `feedback` on a **suppressed** test sets the flag and clears `reraise_suppressed_at`; the mutual exclusion holds after every action (invariants 10–11, 15).
- [X] T049 [P] [US3] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — `--all` reports each skipped test with its reason (invariant 8; SC-006).

**Checkpoint**: Review is a two-way process — a confidently-but-wrongly verified test can now be caught.

---

## Phase 6: User Story 4 — A reviewer asks for a fix in the same breath (Priority: P3)

**Goal**: A reviewer's objection can drive the rewrite, so the fix addresses what the human flagged
and not only what the critic originally found.

**Independent Test**: Leave feedback with a fix request on a flagged test; confirm the record names
the reviewer, the `next_step` chain is emitted, and after the chain the test is rewritten and freshly
verified with the record replaced by the requester's.

### Implementation

- [X] T050 [US4] Add `--fix` to `ReviewFlaggedCommand` (feedback only; `FIX_NOT_APPLICABLE` at exit 2 otherwise). It records the review and emits the structured `next_step` payload from contracts/review-flagged.md. **It must not run repair in-process** — repair requires inference and Principle III forbids an in-process model (research R8).
- [X] T051 [US4] Add a nullable reviewer-feedback parameter to `RepairPromptCompiler.Compile` in `src/Spectra.CLI/Verification/RepairPromptCompiler.cs`, emitted as its own labelled section alongside the critic's findings.
- [X] T052 [US4] Add `--feedback` / `--feedback-from-review` to `src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs`, passing the text through to T051. Depends on T051.
- [X] T053 [US4] In `src/Spectra.CLI/Generation/UpdatedTestIngestor.cs`, ensure the repair ingest path **replaces** the review record with the requester's and clears `ReraiseSuppressedAt` (Decision 7) — deliberately, in `UpdatedTestIngestor`, not by dropping fields.

### Tests for User Story 4

- [X] T054 [P] [US4] `feedback --fix` emits the `next_step` chain and records the review; `--fix` on any other action returns `FIX_NOT_APPLICABLE` (contracts/review-flagged.md; AC-5).
- [X] T055 [P] [US4] In `tests/Spectra.CLI.Tests/Verification/RepairPromptCompilerTests.cs`: reviewer feedback appears in the compiled prompt as its own section, and omitting it leaves the existing output unchanged.
- [X] T056 [P] [US4] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — re-review after suppression returns through `cleared`: `feedback` then `accept` yields flagged-with-null-marker, then unflagged-with-null-marker and a refreshed `reviewed_at` (invariant 16).
- [X] T057 [P] [US4] In `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedHandlerTests.cs` — a repair replaces any earlier review record and clears the marker (Decision 7).

**Checkpoint**: All four actions work, independently testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T058 Reword the guardrail section of `src/Spectra.CLI/Skills/Content/Skills/spectra-review-flagged.md` per FR-023: accept and feedback never modify `verdict`/`score`; `retry-repair` is the only action that changes them, and only via its fresh verification; all three human actions write the review record **in addition to**, never instead of, their existing effect (AC-9).
- [X] T059 Add plain-language triggers for all four actions to the same skill file, per quickstart.md's table (FR-024). Include the `feedback` vs `feedback --fix` distinction.
- [X] T060 [P] Update `docs/cli-reference.md` with the new `review-flagged` flags, the `feedback` action, `ingest-grounding --re-verify`, and the `audit-grounding` columns (FR-025).
- [X] T061 [P] Update `docs/usage.md`'s verdict-disposition section with the review record, the suppression semantics, and the "suppression lasts one pass per review event" caveat (FR-025).
- [X] T062 [P] Add skill-content assertions to `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs` (or a sibling, following the existing `GenerationSkillContractTests` pattern) for the reworded guardrail and the four plain-language triggers (AC-9).
- [X] T063 Walk `quickstart.md` end to end against a real suite and correct any drift between documented and actual output.
- [X] T064 Verification step, no source file: confirm `spectra validate` is green on a corpus containing review records with adversarial notes, and report final per-project test counts against the T002 baseline.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: T001 blocks literally everything — no verification is possible until the toolchain runs.
- **Phase 2 (Foundational)**: depends on Phase 1. **Blocks all user stories** — every story needs the fields to exist and round-trip.
- **Phase 3 (US1)** and **Phase 4 (US2)**: both depend on Phase 2. US2's T024–T027 depend on Phase 2's model work but not on US1's command work, so **US1 and US2 can proceed in parallel** by different developers. Ship them together (see Strategy).
- **Phase 5 (US3)**: depends on Phase 2, and on **T025** (caller-supplied flag path) from US2 — `feedback` cannot set the marker without it. This is the one real cross-story dependency.
- **Phase 6 (US4)**: depends on Phase 5 (`feedback` must exist before `--fix` extends it).
- **Phase 7 (Polish)**: depends on all four stories.

### Critical path

```
T001 → T003/T004 → T005/T006/T007 → T024 → T025 → T026 → T027 → T042 → T050
       └── model ──┘  └── writer ──┘  └──── suppression ────┘   └ US3 ┘ └ US4 ┘
```

`T026` is the single highest-risk task. `T001` is the single hardest blocker.

### Within Each Story

Models → writer → service → command → tests. Test tasks marked `[P]` touch different files and can run
concurrently once their implementation task lands.

### Parallel Opportunities

- Phase 2: T003 and T004 together; then T009, T010, T011 together.
- Phase 3: T019–T023 all together after T013–T018.
- Phase 4: T032–T041 all together after T024–T031.
- Phase 5: T046–T049 together. Phase 6: T054–T057 together.
- Phase 7: T060, T061, T062 together.
- Cross-story: US1 (Phase 3) and US2's service work (T024–T030) by two developers simultaneously.

---

## Parallel Example: User Story 2 tests

```bash
# After T024–T031 land, launch the whole invariant suite together:
Task: "T032 baseline regression guard in tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs"
Task: "T034 no re-suppression without a new review"
Task: "T035 re-suppression valid after a new review (via cleared)"
Task: "T036 a human's raise is never suppressed"
Task: "T038 --re-verify vs default idempotency in tests/Spectra.CLI.Tests/Commands/IngestGroundingCommandTests.cs"
Task: "T041 audit counter disjointness in tests/Spectra.CLI.Tests/Commands/AuditGroundingCommandTests.cs"
```

---

## Implementation Strategy

### MVP is US1 **+** US2, not US1 alone

This departs from the usual "MVP = User Story 1" default, deliberately. Both stories are P1, and
spec.md ranks US2 there because *a record that routine batch verification silently deletes is not a
record*. Shipping US1 alone would put reviewer names and timestamps into files that the next
`ingest-grounding` run erases — inviting trust the feature cannot honour, which is worse than having no
record at all. Phases 3 and 4 land together.

### Incremental delivery

1. Phase 1 + Phase 2 → fields exist and round-trip; the pre-existing note-corruption bug is fixed. Shippable on its own as a bug fix.
2. + Phase 3 + Phase 4 → **MVP**: durable, attributable, survivable review records with visible suppression.
3. + Phase 5 → reviewers can raise concerns on confidently-verified tests.
4. + Phase 6 → one-turn feedback-and-fix.
5. + Phase 7 → conversational surface and docs.

### Parallel team strategy

Two developers after Phase 2: A takes Phase 3 (command surface), B takes Phase 4 (service + suppression).
B must land T025 early, since US3 blocks on it. A third developer has nothing independent to take until
Phase 4 completes — Phases 5 and 6 are sequential on the feedback action.

---

## Notes

- **T026's baseline is the thing to review hardest.** Getting it wrong produces a feature that passes
  its own tests and does nothing. T032 exists solely to catch that.
- **T028 is not optional polish.** Without `--re-verify`, US2 cannot be exercised in the real flow at all.
- Two hard regression gates: the default `ingest-grounding` idempotency skip (T038) and
  `audit-grounding`'s three existing `action_needed` values (T041). Specs 073, 077, and 072 depend on them.
- `Spectra.Execution` is referenced but never modified — `UserIdentityResolver` is consumed as-is.
- The legacy `src/Spectra.CLI/Review/` directory is **not** used: five of its seven files are dead code.
  All work lands in `src/Spectra.CLI/Commands/Review/`.
- No new project, no new dependency, no `_index.json` change (spec Decision 2, AC-8).
- Commit per task or logical group; stop at any checkpoint to validate.
