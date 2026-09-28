# Implementation Plan: Human Review Records & Reviewer Feedback

**Branch**: `078-human-review-record` | **Date**: 2026-08-31 | **Spec**: [spec.md](spec.md)
**Input**: `specs/078-human-review-record/spec.md`

## Summary

Make human review of a critic verdict durable, attributable, and reachable from a conversational
flow. Five frontmatter fields carry the record: three that already parse but are silently dropped on
write (`source`, `created_by`, `note`), two new ones (`reviewed_by`, `reviewed_at`), plus one
observability field the spec's FR-011 forces (`reraise_suppressed_at` — see Research R3). The record
is preserved at the single point where verification metadata is constructed, so no caller can lose it
by omission. `spectra ai review-flagged` gains a non-interactive action surface (`accept`,
`retry-repair`, `delete`, and the new `feedback`), and the `spectra-review-flagged` skill gains
plain-language triggers for all four.

Two findings from Phase 0 change the shape of the work relative to a naive reading of the spec, and
both are load-bearing:

- **The batch grounding path skips any test that already has a grounding block.** Re-verification
  therefore writes nothing today, which would make FR-003's preservation guarantee vacuous and
  FR-010's suppression unreachable. A re-verify mode is a prerequisite, not an extra (R2).
- **`feedback --fix` cannot complete inside one C# command.** Repair needs inference, and Principle
  III forbids an in-process model. The CLI records the review and emits the next step; the skill
  drives the compile → patch → ingest → re-critic chain (R8).

Implementation is four sequential phases: frontmatter round-trip → preservation and suppression →
action surface → skill and docs. Each is a prerequisite for the next.

---

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: YamlDotNet (grounding frontmatter DTO), System.CommandLine (CLI actions),
Spectre.Console (existing interactive review loop), System.Text.Json (verdict JSON, structured
results). No new package dependencies.
**Storage**: File-based. `test-cases/{suite}/*.md` frontmatter is the sole store for the review
record. `.spectra/verdicts/critic-verdict-{id}.json` (gitignored) remains the full critic findings.
No database, no sidecar, no index change (spec Decision 2).
**Testing**: xUnit — `Spectra.Core.Tests` (frontmatter round-trip, metadata invariants, suppression
comparison), `Spectra.CLI.Tests` (write-back preservation, action surface, note fidelity, skill
assertions). Structured result assertions; never throw on validation errors.
**Target Platform**: CLI / dotnet tool (win32, Linux, macOS)
**Project Type**: CLI tool (System.CommandLine), existing three-project solution
**Performance Goals**: Every new or changed command stays model-free and completes in <500 ms for a
single test; the batch form scales linearly with suite size (one file read + at most one write per
test).
**Constraints**: No model call in any C# path. Reviewer identity resolution must not spawn `git` more
than once per process (the existing resolver already caches process-wide). Note refusal must happen
before any file write (FR-006). No `_index.json` schema change.
**Scale/Scope**: 5 frontmatter fields (3 repaired, 2 new) + 1 observability field; 3 modified
services; 2 modified commands; 1 modified skill; 1 prompt-compiler signature change. Reference
corpus: the unit-converter suite (~35 tests) used by the Spec 071 verification run.

**Unknowns**: none. Both assumptions the spec deferred were resolved during specification (spec
Appendix A, Findings 1 and 2), and Phase 0 below resolves the remaining implementation choices
against the code. No `NEEDS CLARIFICATION` markers remain in the spec or this plan.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
Constitution version 2.0.0.

### Principle I — GitHub as Source of Truth

**Status: PASS, and reinforcing.** The review record lives in the test's own Markdown frontmatter,
committed to git. No external store is introduced. Spec Decision 3 (one record, not a history)
depends on git being authoritative — prior review values are recovered with `git log -p`, which is
exactly the rationale this principle states. The three fields being repaired (`source`,
`created_by`, `note`) are currently *lost* on every write, which is a live violation of this
principle; FR-002 fixes it.

### Principle II — Deterministic Execution

**Status: PASS.** Every new and changed command is model-free. The FR-010 suppression comparison is
two timestamp reads and an ordering test — same inputs, same outcome, no inference. Equal timestamps
resolve deterministically toward re-raising (spec FR-010), so there is no tie-break ambiguity.

One point needs care rather than a waiver: suppression makes the marker outcome depend on on-disk
state that a prior pass wrote. That is state-dependent but still deterministic — given the same file
on disk and the same verdict JSON, the result never varies. Research R1 fixes the comparison at a
single point so the behaviour cannot diverge between the per-test and batch paths.

### Principle III — Orchestrator-Agnostic Design

**Status: PASS, with one design constraint this principle imposes.** `feedback --fix` must not run
repair in-process, because repair requires inference and this principle forbids an in-process model.
The CLI therefore records the review and returns a structured next-step payload; the skill performs
the compile → in-session patch → ingest → re-critic chain, exactly as `spectra-generate` and
`spectra-review-flagged` already do. See R8. Every new command is self-contained and re-derives its
state from disk, so no orchestrator needs to remember prior calls.

### Principle IV — CLI-First Interface

**Status: PASS, and corrective.** Accept and delete are today reachable *only* as keystrokes inside
an interactive Spectre.Console loop (spec Appendix A, Finding 4). That is an existing violation of
this principle's "every operation is a named command with explicit parameters — no chat loops".
FR-016 and FR-019 fix it: all four actions become named, CI-friendly, non-interactively invocable
commands with deterministic exit codes, and the batch form satisfies the project's non-stop contract.
The interactive loop is retained as a convenience layer over the same handlers, not as the only path.

### Principle V — Simplicity (YAGNI)

**Status: PASS with one justified addition**, logged in Complexity Tracking.

Reused rather than rebuilt: `GroundingWriteBackService` (the single metadata construction point),
`TestFileWriter` (frontmatter emission), `ReviewFlaggedHandler.AcceptAsync`/`DeleteAsync`,
`DroppedTestsTrail`, `RepairPromptCompiler`, `UserIdentityResolver` from `Spectra.Execution`, and
`AuditGroundingHandler` as the inspection surface. No new project, no new persistence layer, no new
service where an existing one can carry the behaviour.

Deliberately *not* done: no `reviewed: true` boolean (spec Decision 4 — presence of `reviewed_by` is
the signal); no in-file review history (Decision 3); no new resolver (Finding 2); no index change
(Decision 2); no turning on the dormant `GroundingMetadata.IsValid()` (spec Out of Scope).

### Quality Gates

`spectra validate` is unaffected: all five gates operate on id, priority, index currency, and
`depends_on`, none of which this feature touches. New frontmatter fields are additive and optional,
so existing files continue to validate (SC-007). The one risk to a gate is note serialisation
corrupting frontmatter, which would break Schema Validation — R4 addresses it directly and FR-006
requires refusal before any write.

### Development Workflow

Test-required discipline applies: unit tests for parsing/round-trip in `Spectra.Core.Tests`,
integration tests for the CLI action workflows in `Spectra.CLI.Tests`. Coverage expectation for Core
is 80%+, CLI 60%+.

**Gate result: PASS. No unjustified violations. Proceed to Phase 0.**

---

## Project Structure

### Documentation (this feature)

```text
specs/078-human-review-record/
├── plan.md              # This file
├── research.md          # Phase 0 — 9 decisions, all resolved
├── data-model.md        # Phase 1 — fields, YAML shape, invariants, state transitions
├── quickstart.md        # Phase 1 — end-to-end reviewer flows
├── contracts/
│   ├── review-flagged.md      # the four actions, flags, exit codes, JSON shape
│   ├── ingest-grounding.md    # re-verify mode + suppression outcome reporting
│   ├── audit-grounding.md     # review-record and suppression columns
│   └── grounding-frontmatter.md  # the frontmatter contract itself
├── checklists/
│   └── requirements.md  # written by /speckit.specify — 16/16 pass
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

Files this feature touches, all within the existing three-project layout:

```text
src/
├── Spectra.Core/
│   ├── Models/Grounding/
│   │   ├── GroundingMetadata.cs        # + ReviewedBy, ReviewedAt, Source, CreatedBy, Note,
│   │   │                               #   ReraiseSuppressedAt; IsValid() rules revisited
│   │   └── GroundingFrontmatter.cs     # map the parked fields in ToMetadata(); + new aliases
│   └── Parsing/
│       └── TestCaseParser.cs           # unchanged — grounding already flows via ToMetadata()
└── Spectra.CLI/
    ├── IO/
    │   ├── TestFileWriter.cs           # emit the 6 fields; fix EscapeYamlString (R4)
    │   └── GroundingWriteBackService.cs # preservation (FR-003/004) + suppression (FR-010) +
    │                                    #   caller-supplied flag path (FR-008)
    ├── Generation/
    │   └── UpdatedTestIngestor.cs      # audit its field-by-field copy for the same omission
    ├── Verification/
    │   └── RepairPromptCompiler.cs     # + reviewer feedback parameter (R8)
    └── Commands/
        ├── Review/
        │   ├── ReviewFlaggedCommand.cs  # + --action/--test/--note/--reviewer/--fix/--all
        │   ├── ReviewFlaggedHandler.cs  # + FeedbackAsync; Accept/Delete record the review
        │   └── AuditGroundingHandler.cs # + review-record + suppression reporting (FR-011)
        └── Generate/
            ├── IngestGroundingCommand.cs      # + re-verify mode (R2); report suppression
            └── CompileRepairPromptCommand.cs  # + --feedback passthrough

tests/
├── Spectra.Core.Tests/Grounding/       # round-trip, invariants, suppression comparison
└── Spectra.CLI.Tests/
    ├── IO/                             # preservation, note fidelity, escaping
    ├── Commands/Review/                # the four actions, eligibility refusals, exit codes
    └── Skills/                         # skill-content assertions for FR-022/FR-023
```

**Structure Decision**: No new projects. The feature is a data-model extension plus a command-surface
extension, both landing in directories that already own the concern — `Spectra.Core/Models/Grounding`
for the record, `Spectra.CLI/Commands/Review` for the actions, `Spectra.CLI/IO` for persistence. The
legacy `Spectra.CLI/Review/` directory is explicitly **not** used: five of its seven files are dead
(spec Appendix A, §6 of the orientation audit), and the live pair (`DiffPresenter`, `UpdateReviewer`)
belongs to the update flow. `Spectra.Execution` is referenced but unmodified — `UserIdentityResolver`
is consumed as-is.

---

## Phase 0: Research

See [research.md](research.md) for the full write-up. Nine decisions, all resolved against the code;
no open questions. The four that change the plan's shape:

| # | Decision | Why it matters |
|---|----------|----------------|
| **R1** | Preservation and suppression both live in `GroundingWriteBackService.WriteAsync` | It is the single construction point for critic-derived metadata, and it already parses the original — so the pre-write verification timestamp FR-010 needs is in hand. Satisfies FR-004's "no caller can lose it by omission". |
| **R2** | The batch path needs an explicit re-verify mode | `IngestGroundingCommand.RunBatchAsync` skips any test whose `.md` already has a grounding block, so a second pass writes nothing. Without this, FR-003 is vacuous and FR-010 never fires. |
| **R3** | Suppression needs a persisted marker (`reraise_suppressed_at`), scoped to mean *currently* suppressed | The suppressing pass overwrites `verified_at`, destroying the comparison baseline, so audit cannot recompute after the fact. FR-011 is unimplementable without persisting the event. The field is cleared on re-raise, re-review, or a non-`partial` verdict, so it is mutually exclusive with `flagged_for_review: true` — see data-model.md, option (a). |
| **R8** | `--fix` chains in the skill, not in C# | Repair requires inference; Principle III forbids an in-process model. The CLI records and returns a next step. |

R4 is a concrete pre-existing defect: `TestFileWriter.EscapeYamlString` escapes `"` and `\n` but
**not** backslash, so a note containing `C:\temp` is silently corrupted to a tab on re-read, and a
trailing backslash breaks frontmatter parsing. FR-006 depends on fixing it.

---

## Phase 1: Design & Contracts

See [data-model.md](data-model.md), [contracts/](contracts/), and [quickstart.md](quickstart.md).

**Data model** — one entity gains six fields; the review record is the trio `reviewed_by` /
`reviewed_at` / `note`, with `source` and `created_by` repaired alongside and `reraise_suppressed_at`
added for FR-011. Includes the marker state transitions (unmarked → marked → cleared → suppressed →
re-raised) and the revised `IsValid()` rules, since the existing `FlaggedForReview ⇒ Partial` rule
contradicts FR-008.

Fields are grouped **three** ways, not two, and this is the part to read before implementing: the
review record and provenance are always preserved; the critic fields are freely updated; and
**suppression state — `flagged_for_review` together with `reraise_suppressed_at` — is recomputed on
every pass, never carried forward.** `reraise_suppressed_at` is not part of the review record. It is
the companion output of the same comparison that sets the marker, and it means *currently* suppressed:
cleared on re-raise, on re-review, and on a non-`partial` verdict, hence mutually exclusive with
`flagged_for_review: true`. Treating it as a preserved field leaves a stale timestamp on a flagged
test, which is unambiguously wrong and would defeat FR-011.

**Contracts** — four documents. `review-flagged` is the substantive one (four actions, six new flags,
eligibility refusals with distinguishable exit codes, JSON result shape). `ingest-grounding` covers
the re-verify mode and how a suppressed re-raise is reported. `audit-grounding` covers the fourth
`action_needed` value the spec's §5 propagation table calls for. `grounding-frontmatter` pins the
YAML contract itself, since that file *is* the user-facing interface for this feature.

**Implementation order** — strictly sequential:

1. **Frontmatter round-trip** (FR-001, FR-002, FR-006). Map the parked fields, add the new ones,
   emit all of them, fix the escaping. Delivers spec User Story 1's round-trip half and is
   independently testable with no command changes.
2. **Preservation and suppression** (FR-003, FR-004, FR-008, FR-010, FR-011). One service, plus the
   re-verify mode from R2 and the suppression marker from R3. Delivers User Story 2.
3. **Action surface** (FR-011 reporting, FR-012 – FR-020). The four actions non-interactively, the
   feedback action, identity resolution, eligibility refusals. Delivers User Stories 3 and 4.
4. **Skill and docs** (FR-021 – FR-025). Guardrail rewording, plain-language triggers, `cli-reference`
   and `usage` updates.

Phases 1 and 2 must land together before any reviewer-facing behaviour is credible: a record that
writes but does not survive is worse than no record, because it invites trust it cannot honour.

**Agent context**: `.specify/scripts/bash/update-agent-context.sh claude` run at the end of Phase 1.
No new technology is introduced, so the update is a no-op beyond the feature entry.

---

## Post-Design Constitution Re-Check

Re-evaluated after Phase 1. **Result: PASS.**

- **I** — unchanged; the record is git-tracked frontmatter and the design adds no store.
- **II** — the design concentrates the suppression comparison at one point (R1), which removes the
  divergence risk flagged in the pre-design check. Strengthened, not weakened.
- **III** — R8 keeps inference out of C#. The contracts specify structured next-step payloads rather
  than in-process chaining, so the skill remains the orchestrator.
- **IV** — improved: the contract defines named, non-interactive, CI-friendly actions with
  deterministic exit codes for operations that were previously keystroke-only.
- **V** — one addition logged below. Everything else reuses an existing service or command.

---

## Complexity Tracking

| Violation / addition | Why needed | Simpler alternative rejected because |
|---|---|---|
| A sixth frontmatter field, `reraise_suppressed_at`, beyond the five the spec enumerates | FR-011 requires a suppressed re-raise to be observable, and the suppressing pass overwrites `verified_at` — the very value the comparison used — so nothing downstream can reconstruct that suppression happened | Recomputing at audit time was the first choice and does not work: after the pass, a suppressed test is byte-indistinguishable from a plainly-accepted one. Reusing `note` was rejected because it is reviewer-authored prose and overwriting it would destroy the record FR-002 exists to protect |
| A re-verify mode on `ingest-grounding --all` (R2) | Without it the batch path never rewrites an already-grounded test, so FR-003's preservation guarantee is never exercised and FR-010's suppression can never fire — the feature would pass its unit tests and do nothing in the real flow | Removing the idempotency skip outright was rejected: it exists to make the generate skill's post-loop batch call safe to repeat, and Spec 073/077 depend on that. An opt-in mode preserves both behaviours |
| Revising `GroundingMetadata.IsValid()`'s `FlaggedForReview ⇒ Partial` rule | FR-008 requires a confidently-verified test to be markable; the rule contradicts it | Leaving it alone was tempting since it has zero callers, but a dormant rule that contradicts shipped behaviour is a trap for the next reader. Turning the whole method *on* stays out of scope (spec Out of Scope) — this revises the rule without enabling enforcement |

No new projects. No new dependencies. No new persistence layer.

---

## Risks

| Risk | Mitigation |
|---|---|
| **The FR-010 comparison baseline is got wrong**, and suppression silently never fires — the feature ships looking complete and behaves exactly like the rejected Q3 → A | Highest-risk item in the plan, flagged as such in the requirements checklist. R1 fixes the comparison at one point; the contract states the baseline explicitly; a dedicated test asserts that a pass which *writes* a newer verification timestamp still suppressed based on the *pre-write* value |
| **Suppression lasts exactly one pass per review event** (spec Decision 8, consequence 1) — inherent to comparing clocks | Accepted by the feature owner and documented in the spec. A test can be suppressed again only if it is reviewed again in the interim, refreshing `reviewed_at` past the `verified_at` the prior pass wrote; absent a new review, the next pass always finds `verified_at ≥ reviewed_at` and re-raises. The re-review path returns via the *cleared* state, so there is **no** suppressed-to-suppressed transition. The durable variant (compare assessment substance rather than clocks) is named as a follow-up needing no new field. The plan does not attempt it |
| **A verification pass could clear a flag a human deliberately raised** — `feedback` writes a fresh `reviewed_at` and leaves `verified_at` alone, so the timestamp comparison alone would suppress the reviewer's own raise | Found while checking the transition table's reachability. The suppression comparison gains a pre-write `flagged_for_review == false` clause, so only a human *clearance* (what `accept` leaves) is suppressible, never a human *raise*. `ingest-grounding.md` invariant 9 is the regression guard |
| A note containing a backslash corrupts frontmatter and breaks `spectra validate` | R4 fixes the escape order (backslash first, then quote, then newline and carriage return) and adds round-trip tests over adversarial note content |
| `UpdatedTestIngestor` retains the same field-copy omission and erases the record on a repair ingest | Phase 2 audits all three read-modify-write copy sites, not just the one the spec names. The repair path *should* overwrite (Decision 7) but must do so deliberately, not by dropping fields |
| Reviewers read `reviewed_by` as an email address and it is a git display name | FR-022 requires docs and examples to show a name. The contract's examples use a name throughout |
| **The toolchain cannot build or test this repo as configured — blocks implementation, not planning.** `global.json` pins SDK `9.0.314` with `rollForward: latestFeature`, which does not cross a major version; only `10.0.101` and `10.0.102` are installed. Every `dotnet` invocation fails with "Install the [9.0.314] .NET SDK or update global.json" — `build`, `test`, even `--version`. Pre-existing and unrelated to this feature | **Decide before Phase 1 starts, not during it.** Two options: install a 9.0.3xx SDK to honour the existing pin, or bump `global.json` to `10.0.x` to match what is installed. Bumping is the likely choice but is a repo-wide change that affects CI and every contributor, so it is a decision for the maintainer rather than a side effect of this feature's first commit. **Consequence for this plan:** none of the invariants in `contracts/*.md` can be executed as tests until this is resolved, so "tests pass" cannot be a completion signal for any phase until then. No verification claim in this feature should be made on an unbuilt tree |
