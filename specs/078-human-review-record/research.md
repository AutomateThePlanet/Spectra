# Phase 0 Research: Human Review Records & Reviewer Feedback

**Feature**: `078-human-review-record` | **Date**: 2026-08-31
**Mode**: read-only investigation against the working tree at branch point `a86f4d2`
**Status**: all decisions resolved; zero open questions

The spec deferred two assumptions to this phase. Both were in fact resolved during specification and
are recorded in spec Appendix A, Findings 1 and 2 — they are restated here as R5 and R6 for
completeness, not re-derived. R1–R4 and R7–R9 are new implementation decisions.

---

## R1 — Where the preservation and suppression logic lives

**Decision**: both in `GroundingWriteBackService.WriteAsync` (`src/Spectra.CLI/IO/`).

**Rationale**: it is the **only** place in the codebase that constructs `GroundingMetadata` from a
critic result. The two other construction sites are `GroundingFrontmatter.ToMetadata()` (deserialising
from disk, not from a verdict) and `VerificationResult.ToMetadata(string)` — which has **zero call
sites** and is a leftover from the retired in-process pipeline. Concentrating both behaviours here
satisfies FR-004 literally: a caller cannot lose the review record by omission, because there is no
other path by which critic-derived metadata comes into being.

Decisively, the method **already reads the original test from disk** before constructing the new
metadata, and holds it as a parsed `TestCase`. That means:

- the existing review record (`reviewed_by`, `reviewed_at`, `note`, `source`, `created_by`) is
  available to carry forward for FR-003;
- the **pre-write verification timestamp** — `original.Grounding.VerifiedAt` — is available for the
  FR-010 comparison, at exactly the moment before the new value is assigned.

No new read, no new plumbing, no signature change to reach the data. This is the reason the design
does not need a separate "preserve" pass or a repository abstraction.

**Alternatives considered**:

- *Preserve in each calling command.* Rejected — three commands call this service today and more will
  later; FR-004 explicitly forbids a per-caller obligation.
- *Preserve in `TestFileWriter`.* Rejected — the writer receives a fully-formed `TestCase` and has no
  access to the prior on-disk state, so it cannot know what to carry forward. It would have to
  re-read the file it is about to overwrite.
- *A `GroundingMerger` service.* Rejected under Principle V — one merge site, one caller, no third
  use case.

---

## R2 — The batch grounding path skips already-grounded tests

**Decision**: add an explicit re-verify mode to `ingest-grounding --all`; do not remove the existing
idempotency skip.

**Rationale**: `IngestGroundingCommand.RunBatchAsync` short-circuits on any test whose `.md` already
parses with a non-null grounding block:

> `if (parsedExisting.IsSuccess && parsedExisting.Value is { } existing && existing.Grounding is not null) { skippedAlreadyWritten++; continue; }`

The consequence is severe for this feature and easy to miss: **a second verification pass over an
already-grounded suite writes nothing at all**. `GroundingWriteBackService.WriteAsync` is never
reached, so FR-003's preservation is trivially "satisfied" because no rewrite occurs, and FR-010's
suppression comparison never executes. Every test in Phase 2 would pass while the real flow — spec
User Story 2, the highest-risk story in the feature — does nothing.

The per-test path (`RunAsync`) has no such skip and calls `WriteAsync` unconditionally, so the two
paths would diverge in behaviour. That divergence is itself a defect.

The skip cannot simply be deleted: it exists so the generate skill's post-loop batch call is safe to
repeat, and Specs 073 and 077 built their non-stop batch flows on that guarantee. An opt-in re-verify
mode preserves the existing contract while making re-verification expressible.

**Alternatives considered**:

- *Delete the skip.* Rejected — breaks the idempotency Specs 073/077 depend on, and would make the
  generate skill's repeat-safe batch call rewrite every test on every invocation.
- *Make the skip conditional on the verdict being unchanged.* Attractive, and close to the durable
  suppression variant named in spec Decision 8, but it is a larger behavioural change to a path two
  shipped specs depend on. Deferred with the same follow-up.
- *Leave it and document that re-verification is out of scope.* Rejected — it would hollow out User
  Story 2, which the spec ranks P1 precisely because a record that routine operation deletes is not a
  record.

---

## R3 — Making a suppressed re-raise observable

**Decision**: persist the suppression event as a new grounding field, `reraise_suppressed_at` (UTC
timestamp, nullable), written by the suppressing pass.

**Rationale**: FR-011 requires that a suppressed re-raise be reportable. It cannot be recomputed
after the fact, because the suppressing pass **overwrites `verified_at` — the exact value the
comparison used**. Once written, the on-disk state of a suppressed test is:

```
verdict: partial, flagged_for_review: false, reviewed_at: <R>, verified_at: <T2 where T2 > R>
```

which is byte-indistinguishable from a test a reviewer plainly accepted and that was never
re-verified. The information is destroyed by the write that creates the situation, so it must be
recorded at the moment of the decision or not at all.

A timestamp rather than a boolean, because "when was a concern last suppressed" is the question a
reviewer or auditor actually asks, and it costs the same to store. It also makes repeated suppression
visible: the field advances each time.

This is the one field beyond the spec's enumerated five, and it is logged in the plan's Complexity
Tracking.

**Alternatives considered**:

- *Recompute in `audit-grounding`.* Rejected — impossible after the baseline is overwritten, per
  above. This was the first choice.
- *Reuse `note`.* Rejected — `note` is reviewer-authored prose that FR-002 exists to protect;
  appending machine text to it would destroy the record.
- *Log to `.spectra/`.* Rejected — that directory is gitignored (`.gitignore:37–39`), so the record
  would not survive a fresh clone, which is the same defect this whole feature exists to fix.
- *A boolean `reraise_suppressed`.* Rejected — spec Decision 4's reasoning against redundant
  booleans applies, and a timestamp answers strictly more questions at identical cost.

---

## R4 — Note fidelity: a real escaping defect

**Decision**: fix `TestFileWriter.EscapeYamlString` to escape backslash first, then quote, then
newline and carriage return. Refuse a note that still cannot be represented, before any write.

**Rationale**: the current implementation is two replacements:

> `value.Replace("\"", "\\\"").Replace("\n", "\\n")`

Backslash is not escaped. Because the output is a YAML double-quoted scalar, where `\` *is* an escape
character, this silently corrupts data and can break parsing outright:

| Note content | Emitted | Parsed back as |
|---|---|---|
| `C:\temp\build` | `"C:\temp\build"` | `C:<TAB>emp<BACKSPACE>uild` — silent corruption |
| `ends with a backslash \` | `"ends with a backslash \"` | unterminated scalar — frontmatter fails to parse |
| `carriage\rreturn` (literal CR) | raw CR inside the scalar | line break inside a quoted scalar |

This is pre-existing and already reachable through `condensed_findings[].reason`, which is
critic-authored and could contain a Windows path from documentation. FR-006 makes it this feature's
problem because reviewer notes are free-text by design and far more likely to contain a backslash.

Escape order matters: backslash must be replaced before quote, or the backslashes introduced by the
quote-escaping get double-escaped.

**Alternatives considered**:

- *Emit notes as YAML block scalars (`|`).* Cleaner for multi-line text and avoids escaping entirely,
  but it changes the emitted shape of existing fields and would make the frontmatter diff noisy for
  every already-grounded test. Rejected for this feature; worth revisiting if multi-line notes become
  common.
- *Strip offending characters.* Rejected — FR-006 forbids silent alteration.
- *Hand serialisation to YamlDotNet.* Rejected under Principle V: `TestFileWriter` hand-builds the
  whole frontmatter block by design, and switching one field to a serialiser would produce
  inconsistent formatting.

---

## R5 — `hallucinated` on disk (restates spec Appendix A, Finding 1)

**Decision**: reject `hallucinated` explicitly in every review action; do not treat it as
unreachable.

**Rationale**: no production path writes it — `GroundingWriteBackService.WriteAsync` refuses that
verdict with a distinct `HallucinatedRefused` outcome, and it is the sole verdict-to-metadata
constructor. But `GroundingFrontmatter.ToMetadata()` parses the string, `TestFileWriter` re-emits it,
and `GroundingMetadata.IsValid()` has no callers, so nothing validates it. A hand-edited or
externally authored file round-trips it unchallenged. FR-007 therefore needs a real refusal branch
with its own exit code, not a comment asserting the case cannot occur.

---

## R6 — Reviewer identity (restates spec Appendix A, Finding 2)

**Decision**: consume `Spectra.Execution`'s `UserIdentityResolver` directly. No new resolver. Add a
`--reviewer` override. No prompt, no failure path.

**Rationale**: the `Spectra.MCP` project the input named no longer exists (deleted by Spec 070). The
resolver is at `src/Spectra.Execution/Identity/UserIdentityResolver.cs` under the cosmetically-stale
namespace `Spectra.MCP.Identity`, and `src/Spectra.CLI/Spectra.CLI.csproj:10` already carries a
project reference to `Spectra.Execution` — so it is reachable with a `using` and nothing else.

Two properties matter for FR-019/FR-020. It **cannot fail**: `GetCurrentUser()` returns
`git config user.name`, falling back to `Environment.UserName`, which is always present — hence spec
Decision 6 dropping the prompt and the failure path. And it resolves **once per process** behind a
static cache, so adding a caller costs no additional `git` spawn.

It returns a *name*, not an email address — FR-022 requires docs and examples to reflect that.

---

## R7 — Shape of the non-interactive action surface

**Decision**: extend `ReviewFlaggedCommand` with `--action <accept|retry-repair|delete|feedback>`,
`--test <id>`, `--note <text>`, `--reviewer <name>`, `--fix`, and `--all`, keeping `--suite` and the
existing interactive loop.

**Rationale**: this mirrors `IngestGroundingCommand`'s established convention
(`--suite` / `--test` / `--all` / `--from`), which the non-stop contract flows already drive, so the
skill's call shape is familiar and the batch form satisfies FR-018 without inventing a second idiom.
Keeping one command with an `--action` selector rather than four sibling commands matches how the
spec frames these as four actions on one review surface, and avoids four near-identical option sets.

The interactive loop stays, refactored to call the same handler methods, so the Spectre.Console path
and the non-interactive path cannot drift.

**Alternatives considered**:

- *Four separate commands* (`review-accept`, `review-feedback`, …). Rejected — quadruples the option
  surface and splits eligibility logic across four registrations.
- *Positional action argument.* Rejected — inconsistent with every other `spectra ai` verb, which
  uses named options for everything except the suite.
- *A `--json` input file of actions.* Rejected under Principle V; no demand, and `--all` covers the
  batch case.

---

## R8 — How `feedback --fix` chains into repair

**Decision**: the CLI writes the review record and returns a structured next-step payload. The
**skill** performs the compile → in-session patch → ingest → re-critic chain. `--fix` never runs
repair inside the C# command.

**Rationale**: Principle III forbids an in-process model, and repair is inherently an inference step —
it rewrites test prose against documentation. Every comparable flow in the project already works this
way: `spectra-generate` and `spectra-review-flagged` both drive `compile-repair-prompt` → agent turn →
`ingest-update` → re-critic from the skill. `feedback --fix` must follow the same seam or it would be
the first command in the codebase to need a model.

So `--fix` on the CLI means: record the review, mark the test as needing attention, and emit
`next_step` naming the repair chain and the test id. The skill reads that and proceeds. This keeps the
command self-contained and re-derivable (Principle III) and keeps the chain observable.

To make the resulting repair actually reflect the reviewer's objection — FR-014's requirement that the
feedback text is "part of what the rewrite is asked to address" — `RepairPromptCompiler.Compile` gains
a nullable reviewer-feedback parameter, surfaced as `--feedback` on `CompileRepairPromptCommand`. The
compiler currently takes `(TestCase, IReadOnlyList<CriticFinding>, IReadOnlyList<SourceDocument>)` and
builds prose instructing the model to rewrite only the flagged elements; reviewer feedback becomes an
additional labelled section so the model addresses both the critic's findings and the human's.

**Alternatives considered**:

- *Run repair in-process.* Rejected — direct Principle III violation.
- *Have `--fix` shell out to the agent.* Rejected — the CLI does not and must not invoke the
  orchestrator; the dependency runs the other way.
- *Put the feedback text only in `note` and let the skill read it from frontmatter.* Workable, but it
  makes the repair prompt's completeness depend on the skill remembering a second read. Passing it
  explicitly is deterministic.

---

## R9 — Where the review record lives in the frontmatter

**Decision**: as sub-fields of the existing `grounding:` block. Reuse the parked `note`; add
`reviewed_by` and `reviewed_at` as the review record; repair `source` and `created_by`. The
suppression marker `reraise_suppressed_at` (R3) also lives in this block but is **not** part of the
review record — it is recomputed on every verification pass rather than preserved, and means
*currently* suppressed. See data-model.md's Preservation contract for the three-group split.

**Rationale**: `grounding:` is the block that already round-trips cleanly through a DTO, a
`ToMetadata()` conversion, and a writer — it is the only frontmatter region with working two-way
plumbing. The alternative, `TestCase.Status`, is disqualified: it is write-only (no alias on
`TestCaseFrontmatter`, not mapped in `TestCaseParser`), so it is erased by every rewrite, and it is
already claimed by the doc-drift workflow for `status: orphaned`. The spec records that as background;
it is also the deciding technical fact here.

The three parked fields are not speculative — `Agent/GroundedPromptBuilder.cs:123–128` instructs the
model to populate `grounding.source`, `grounding.created_by`, and `grounding.note` on the
user-described path. The prompt asks, the DTO accepts, and both `ToMetadata()` and `TestFileWriter`
discard. Repairing the round-trip makes an existing instruction work rather than adding a new one.

**Alternatives considered**:

- *A sibling `review:` block.* Rejected — needs a second DTO, a second conversion, and a second writer
  branch, and splits "what do we know about this test's quality" across two blocks.
- *`TestCase.Status`.* Rejected as above.
- *A `.spectra/reviews/` sidecar.* Rejected — gitignored, so it fails the durability requirement that
  motivates the feature, and violates Principle I.

---

## Summary of code facts this phase established

| Fact | Location | Consequence |
|---|---|---|
| Sole verdict-to-metadata construction point | `GroundingWriteBackService.WriteAsync` | R1 — one place to implement FR-003/004/008/010 |
| Pre-write verification timestamp is in scope there | same method, via the parsed original | R1 — FR-010's baseline needs no new plumbing |
| Batch path skips already-grounded tests | `IngestGroundingCommand.RunBatchAsync` | R2 — re-verify mode is a prerequisite |
| Per-test path has no such skip | `IngestGroundingCommand.RunAsync` | R2 — the two paths would otherwise diverge |
| `verified_at` is overwritten by the suppressing pass | `GroundingWriteBackService.WriteAsync` | R3 — suppression must be persisted, not recomputed |
| `EscapeYamlString` omits backslash | `TestFileWriter.cs:212–215` | R4 — silent corruption; concrete fix |
| `VerificationResult.ToMetadata(string)` has zero callers | `Spectra.Core/Models/Grounding/` | R1, R5 — dead path, not a construction site |
| `GroundingMetadata.IsValid()` has zero callers | same file | R5 — nothing validates verdict/marker consistency |
| `UserIdentityResolver` reachable from CLI, cannot fail, cached per process | `Spectra.Execution/Identity/` + `Spectra.CLI.csproj:10` | R6 — no new resolver, no failure path |
| `RepairPromptCompiler.Compile` takes no guidance parameter | `Spectra.CLI/Verification/` | R8 — signature change for `--fix` |
| Parked fields are actively requested by a prompt | `Agent/GroundedPromptBuilder.cs:123–128` | R9 — repairing the round-trip fixes a live instruction |
