# Feature Specification: Grounding Oracle Fix + Batch Grounding-Ingest + Skill Routing (Spec 072 Amendment)

**Feature Branch**: `073-fix-grounding-oracle-batch`  
**Created**: 2026-06-20  
**Status**: Draft  
**Amendment to**: Spec 072 (Repair-Orchestration Hardening & Inspection Surface)

## Context

This is an amendment appended to Spec 072. A full uninterrupted `generate all` run on suite `unit-converter` (73 tests: 45 grounded / 28 partial / 0 hallucinated) confirmed three defects the base spec did not close:

1. The `grounding_written` oracle in `audit-grounding` always reports `false` even when the grounding block exists on disk — a confirmed contradiction (TC-100.md has the block; the oracle says it doesn't).
2. `compile-repair-batch` emits an empty manifest (`[]`) for all 28 partials — almost certainly because it filters against the broken oracle.
3. `ingest-grounding` has no batch form (`--suite --all`), so the agent is forced to shell-loop across all 45 grounded and 28 partial tests — the same batch-per-test-shell-loop disease base Spec 072 fixed for the repair loop, but missed for grounding-ingest.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Oracle Reports Correct Grounding State (Priority: P1)

An agent or developer runs `spectra ai audit-grounding --suite <s>` after a grounding-ingest pass and receives an accurate per-test grounding state report. Currently the command always reports `grounding_written: false`, making the resume checkpoint useless and forcing the agent into shell improvisation to debug what it can't trust.

**Why this priority**: The oracle is the foundation of the entire resume design from base Spec 072. Every downstream command (`compile-repair-batch` filter, session-restart behavior) depends on it being correct. While it lies, repair produces zero results and the partial loop is permanently broken.

**Independent Test**: Can be fully tested by running `audit-grounding --suite unit-converter` on a workspace where 45 tests have a grounding block in their `.md` frontmatter — a correct oracle reports 45 `grounding_written: true`, not 0.

**Acceptance Scenarios**:

1. **Given** a test whose `.md` frontmatter contains a `grounding:` block (verdict/score/verified_at), **When** `audit-grounding --suite <s>` is run, **Then** that test's entry reports `grounding_written: true`.
2. **Given** a test whose `.md` frontmatter has no `grounding:` block, **When** `audit-grounding --suite <s>` is run, **Then** that test's entry reports `grounding_written: false`.
3. **Given** the current `unit-converter` suite (45 grounded tests all with blocks on disk), **When** `audit-grounding --suite unit-converter` is run, **Then** summary shows `GroundingWritten: 45`, not `0`.

---

### User Story 2 — Repair Batch Targets the Right Tests (Priority: P2)

After the oracle fix, an agent runs `spectra ai compile-repair-batch --suite <s>` on a suite with 28 unresolved partial verdicts and receives a manifest with 28 entries — not the empty `[]` it currently produces.

**Why this priority**: The empty manifest is the direct blocker for the partial repair loop. With a correct oracle as the filter, this should resolve without additional code changes — but must be verified and, if a second independent bug exists, fixed explicitly.

**Independent Test**: Run `compile-repair-batch --suite unit-converter` after the oracle fix; a 28-entry manifest indicates the bug was oracle-downstream. An empty manifest indicates an independent second bug requiring its own investigation.

**Acceptance Scenarios**:

1. **Given** N partial verdict files whose tests lack a grounding block, **When** `compile-repair-batch --suite <s>` is run, **Then** the manifest contains exactly N entries.
2. **Given** all tests in a suite already have grounding blocks written, **When** `compile-repair-batch --suite <s>` is run, **Then** the manifest is empty (`[]`) — correct resume behavior.
3. **Given** the current `unit-converter` suite (28 partials without grounding blocks), **When** `compile-repair-batch --suite unit-converter` is run after the oracle fix, **Then** a manifest with 28 entries is produced.

---

### User Story 3 — Batch Grounding-Ingest in One Call (Priority: P3)

After all critics have run for a batch, an agent writes grounding blocks for all tests in the suite with a single `spectra ai ingest-grounding --suite <s> --all` call — instead of shell-looping across each test individually. The command is idempotent and skips tests that already have blocks.

**Why this priority**: This eliminates the shell-loop pattern for grounding-ingest, which currently breaks non-stop runs (bash syntax error → PowerShell foreach loop), and is the symmetric partner of what base Spec 072 did for the repair loop.

**Independent Test**: Run `ingest-grounding --suite unit-converter --all` on a suite where no grounding blocks have been written yet; verify all tests in `.spectra/verdicts/` get their blocks written in one pass with no shell commands issued.

**Acceptance Scenarios**:

1. **Given** N verdict files in `.spectra/verdicts/` for a suite, **When** `ingest-grounding --suite <s> --all` is run, **Then** grounding blocks are written to all N corresponding `.md` files in one pass.
2. **Given** some tests already have grounding blocks, **When** `ingest-grounding --suite <s> --all` is run again, **Then** those tests are skipped; only the remaining tests get blocks written (idempotent).
3. **Given** 28 partial tests needing grounding with `--repaired --repair-attempts 1`, **When** `ingest-grounding --suite <s> --all --repaired --repair-attempts 1` is run, **Then** all 28 get the flagged block with repair metadata in one call.

---

### User Story 4 — Zero Shell Improvisation in Generate Cycle (Priority: P4)

The `spectra-generate` skill's Steps 8–9 route all state reads and batch operations through CLI verbs. An agent running an unattended `--no-interaction` generate cycle never resorts to `find`, `grep`, `cat`, or `ls` to read grounding state, test paths, or config — and therefore never triggers an allowlist prompt that would halt the run.

**Why this priority**: This is a re-landing of the routing guidance from base Spec 072 FR4/FR5 that evidently did not take effect in the live run. Without it, the three other fixes only partially solve the non-stop problem — the agent will still improvise if the skill doesn't explicitly prohibit it.

**Independent Test**: A full `generate all` cycle with `--no-interaction` completes end-to-end with zero shell-improvisation allowlist prompts; every agent action is `Bash(spectra ai ...)` or a Write to a spectra-managed path.

**Acceptance Scenarios**:

1. **Given** the generate skill reaches Step 9 (grounding-ingest), **When** it writes grounding blocks for all tests, **Then** it uses `ingest-grounding --suite <s> --all`, not a loop of individual calls.
2. **Given** the agent needs to inspect grounding state or resume a partial repair, **When** it checks which tests still need attention, **Then** it uses `audit-grounding --suite <s>`, not `ls .spectra/verdicts/` or `grep grounding`.
3. **Given** the agent references a test's file path, **When** it constructs the path, **Then** it uses `test-cases/{suite}/{id}.md` (stated explicitly in the skill), not `find test-cases -name {id}.md`.
4. **Given** the agent needs to read the resolved config, **When** it fetches it, **Then** it uses `spectra config --raw`, not `cat spectra.config.json`.

---

### Edge Cases

- What happens when `ingest-grounding --suite --all` runs on a suite with zero verdict files? Should return success with 0 written, no error.
- What happens when a verdict file exists but the corresponding `.md` test file is missing? Should report the missing test (warn) and continue, not abort.
- What happens when `compile-repair-batch` runs on a suite with only grounded verdicts (no partials)? Should return `[]` — correct, not an error.
- What happens when `audit-grounding` runs on a suite whose `_index.json` is absent? Should derive test list from the verdict files and report `file: null` for tests not in the index.
- What happens when `ingest-grounding --suite --all` is run concurrently? Each `.md` write is atomic; concurrent runs on disjoint test sets are safe; concurrent on the same test should be last-writer-wins (acceptable given idempotent content).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-A1**: `audit-grounding` MUST detect the `grounding:` block from the test's `.md` frontmatter at `test-cases/{suite}/{id}.md` — the same path and field that `ingest-grounding` writes — and report `grounding_written: true` when the block is present.
- **FR-A2**: The root cause of the oracle defect MUST be identified with file and line number evidence before the fix is applied (likely: wrong path construction, reading `_index.json` instead of `.md`, or field name mismatch).
- **FR-A3**: After the oracle fix, `compile-repair-batch --suite unit-converter` MUST produce a manifest of 28 entries (the unresolved partials). If still empty after the oracle is correct, a second independent bug in the partial-selection filter MUST be investigated and fixed.
- **FR-A4**: `spectra ai ingest-grounding` MUST accept `--suite <s> --all` (or `--batch`) to write grounding blocks for all tests in a suite in one pass.
- **FR-A5**: `ingest-grounding --suite --all` MUST skip tests that already have a grounding block (idempotent), using the corrected FR-A1 oracle as the skip check.
- **FR-A6**: `ingest-grounding --suite --all` MUST accept `--repaired` and `--repair-attempts N` flags and apply them to all written blocks in that call — so partial-grounding batch writes need no per-test flags override.
- **FR-A7**: The per-test `--test` form of `ingest-grounding` MUST remain unchanged — it is still used inside the per-test repair loop.
- **FR-A8**: `ingest-grounding --suite --all` MUST reuse the existing per-test write logic without duplicating frontmatter-writing code.
- **FR-A9**: `spectra-generate.md` Step 9 (post-critic grounding-ingest) MUST specify `ingest-grounding --suite <s> --all`, not a per-test loop.
- **FR-A10**: `spectra-generate.md` Steps 8–9 MUST explicitly state that test files are at `test-cases/{suite}/{id}.md` and that grounding state is read via `audit-grounding`, config via `spectra config --raw` — no shell improvisation for these reads.
- **FR-A11**: Docs MUST document `ingest-grounding --suite --all` alongside the per-test form in `cli-reference.md`.
- **FR-A12**: Docs MUST note the corrected `grounding_written` semantics: the field reflects the `.md` frontmatter, which is the writer's source of truth.

### Key Entities

- **Grounding block**: A `grounding:` section in a test's `.md` YAML frontmatter written by `ingest-grounding`; its presence/absence is the done-marker for both the repair resume checkpoint and the oracle's `grounding_written` field.
- **Partial verdict**: A `.spectra/verdicts/critic-verdict-{id}.json` file with `verdict: partial`; eligible for repair if the corresponding `.md` has no grounding block.
- **Grounding audit entry**: Per-test record from `audit-grounding` containing `id`, `verdict`, `score`, `grounding_written`, `flagged_for_review`, `action_needed`.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `audit-grounding --suite unit-converter` run on the current workspace (45 grounded tests with blocks on disk) reports exactly 45 `grounding_written: true` — not 0. The oracle is correct.
- **SC-002**: `compile-repair-batch --suite unit-converter` after the oracle fix produces a manifest of 28 entries — not `[]`. Repair is unblocked.
- **SC-003**: Writing grounding blocks for a suite of N tests requires exactly one `ingest-grounding --suite <s> --all` call — zero shell loops, zero allowlist prompts.
- **SC-004**: A full `generate all` cycle with `--no-interaction` completes from generation through grounding-ingest with zero `find`/`grep`/`cat`/`ls` shell improvisation prompts.
- **SC-005**: Re-running `ingest-grounding --suite <s> --all` on a suite where all blocks are already written skips all tests and writes nothing — idempotent.
- **SC-006**: The oracle detector and the grounding writer agree on path (`test-cases/{suite}/{id}.md`) and field name — confirmed by reading both code locations with file+line evidence.

### Assumptions

- The oracle defect is at the path-construction step in `AuditGroundingHandler.cs` (likely `test-cases/{id}.md` instead of `test-cases/{suite}/{id}.md`), based on the spec evidence that the agent's direct `cat test-cases/TC-100.md` failed while the file was confirmed present under the suite subfolder. This must be verified before fixing.
- The `compile-repair-batch` empty-manifest bug is downstream of the oracle (the filter reads `grounding_written` from the same broken oracle path). If verified true after the oracle fix, no separate fix is needed.
- `ingest-grounding --suite --all` writes the same block content as the per-test form — the only change is iterating over all verdict files rather than one.
