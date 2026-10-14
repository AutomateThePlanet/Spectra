# Feature Specification: Workspace Safety and Hygiene (Phase 6, Part 1)

**Feature Branch**: `096-workspace-safety`
**Created**: 2026-10-13
**Status**: Draft
**Input**: User description: "096-workspace-safety: Phase 6, part 1 of 4 — workspace safety and hygiene. Fixes for commands
that lose user edits, damage files, or report success when they shouldn't, plus the remaining gitignore and
test-infrastructure items deferred from 089–095. No new features. Each fix needs a regression test that fails on
23aa08c." (Background, F1–F9, constraints and scope as quoted in the request.)

## Context

Several commands can lose a user's work or misreport what happened. A few hygiene items deferred from specs 089–095
also remain open.

Facts (verified on `23aa08c`):
- `spectra init` records the installed files' hashes under two different key forms: repo-relative for prompt
  templates, the default profile and `CUSTOMIZATION.md`, absolute for skills and agents. `update-skills` looks files up
  by absolute path. When it finds no record, it treats an existing file as unedited and overwrites it when the bundled
  content changed — so the first `update-skills` after a bundled change overwrites an edited template, profile or
  `CUSTOMIZATION.md`. (094's batch compile already checks both key forms.)
- `spectra suite delete` removes dependency references with a pre-090 text rule:
  - it deletes any `- <ID>` list line in other suites' files, body included;
  - it collapses every blank-line run;
  - it ignores single-value `depends_on`.

  It leaves behind the suite's baseline, its staging folders, its generation plan and its tests' verdict files. 090's
  deletion service does all of this correctly for `spectra delete`.
- `spectra show <id>` looks a test up in the wrong folder, so it misses tests stored the standard way.
- `spectra validate` counts parse errors but exits 0 ("Validation passed") when tests fail to parse.
- The shared gitignore list lacks `.spectra/verdicts/` and `.spectra/dropped-tests.json`. The record of which patterns
  were already offered lives in a gitignored file, so it is per clone: a pattern the team removed comes back from a
  teammate's clone.
- `spectra delete` fully parses every test file in the workspace.
- The repo has no line-ending rules; with `core.autocrlf=true` the spec acceptance scripts check out with CRLF and fail in
  bash.
- Test infrastructure:
  - one test collection that changes the working directory has no definition, so it runs in parallel (flaky in 093);
  - one test has failed since 078 because it expects a heading the usage guide no longer has.
- `ai.generation_batch_size` is in the init template but read by nothing (094 added `generation.batch_size`).

Rules that still apply: no config section ever required (083); verify on a real `spectra init` workspace and a
pre-096 one; human summaries stay on stderr in JSON mode; `--dry-run` writes nothing; every fix has a test that fails on
`23aa08c`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - My edits to SPECTRA's files are never overwritten (Priority: P1)

A team customised a prompt template, the default profile and `CUSTOMIZATION.md`. They upgrade SPECTRA and run
`spectra update-skills`. Their edited files stay exactly as they were, and the command says which files it skipped and
how to get the new version if they want it.

**Why this priority**: Silent loss of user work is the worst failure a maintenance command can have.

**Independent Test**: Run the real init, edit a template, the profile and `CUSTOMIZATION.md`, change the bundled
content, run `update-skills`. All three files are byte-identical afterwards and reported as skipped. A pre-096 manifest
with mixed key forms gives the same result.

**Acceptance Scenarios**:

1. **Given** a workspace from the real `spectra init` and an edited prompt template, **When** `update-skills` runs with
   changed bundled content, **Then** the template is byte-identical and listed as skipped.
2. **Given** an installed file with no record in the manifest whose content differs from the bundled content, **When**
   `update-skills` runs, **Then** the file is not overwritten and is reported as "skipped (no record — it may have been
   edited)" with how to get the new version.
3. **Given** an installed file with no record whose content equals the bundled content, **When** `update-skills` runs,
   **Then** the file is recorded and later updates refresh it normally.
4. **Given** a pre-096 manifest with relative and absolute keys, **When** any command loads it, **Then** the keys are
   normalised to one form, keys from another location are re-rooted by their install-path tail, unrecognised ones are
   dropped with one summary warning, and no edited file is overwritten.

---

### User Story 2 - Deleting a suite touches nothing else (Priority: P1)

A user deletes a suite. Tests in other suites that depended on its tests lose exactly that dependency; nothing else in
any other file changes; the suite's leftovers (baseline, staging, plan, verdicts) are removed; `--dry-run` shows all of
it without changing anything.

**Why this priority**: The current text rule edits unrelated lines in other suites' files.

**Independent Test**: Another suite's test has a body line `- TC-101` and a run of blank lines; a third has
`depends_on: TC-101`; a fourth lists TC-101 in a `depends_on` list. Delete TC-101's suite. The body line and blank lines
are byte-identical, the single-value dependency is cleared, the list dependency is reported as a warning, and the
suite's leftovers are gone.

**Acceptance Scenarios**:

1. **Given** another suite's test whose body contains `- TC-101` and a blank-line run, **When** TC-101's suite is
   deleted, **Then** that file is byte-identical.
2. **Given** a test elsewhere with `depends_on: TC-101`, **When** the suite is deleted, **Then** the dependency is
   removed through the lossless writer and nothing else in the file changes.
3. **Given** a test elsewhere that lists TC-101 in a list or can't be parsed, **When** the suite is deleted, **Then** it
   is reported as a warning and not edited.
4. **Given** the suite has a baseline, staging folders (incl. failed files), a generation plan and verdict files,
   **When** it is deleted, **Then** all of them are removed; with `--dry-run`, they are listed and nothing changes.

---

### User Story 3 - Commands report the truth (Priority: P1)

`spectra show` finds tests stored the standard way, and `spectra validate` fails when tests don't parse.

**Why this priority**: A validation that passes on broken files lets broken suites into CI.

**Independent Test**: `show <id>` on a suite created through the standard flow prints the test. A suite with one
unparseable test makes `validate` exit 1 with status "failed" and the error count, in human and JSON mode.

**Acceptance Scenarios**:

1. **Given** a test indexed with its bare file name, **When** `spectra show <id>` runs, **Then** it prints that test; the
   legacy `suite/file` index form still works.
2. **Given** a suite with one unparseable test, **When** `spectra validate` runs, **Then** it exits 1, reports "failed"
   and counts the error, in human and JSON mode.

---

### User Story 4 - The gitignore offer is a team decision (Priority: P2)

New scratch paths (`.spectra/verdicts/`, `.spectra/dropped-tests.json`) are offered once. Once a pattern has been
offered in the repository, no clone offers it again — so a pattern the team deliberately removed stays removed for
everyone.

**Why this priority**: The per-clone record undoes team decisions on every new clone.

**Independent Test**:
- A clone with no local record, whose `.gitignore` marks a pattern as offered but no longer contains the pattern →
  `update-skills` does not re-add it.
- The two new entries are offered exactly once.
- An existing per-clone record is migrated once.

**Acceptance Scenarios**:

1. **Given** a `.gitignore` that records a pattern as offered and no longer lists it, and no local record, **When**
   `update-skills` runs, **Then** the pattern is not re-added.
2. **Given** a workspace from before 096, **When** `update-skills` runs twice, **Then** `.spectra/verdicts/` and
   `.spectra/dropped-tests.json` are added once.
3. **Given** a workspace whose only record is the old per-clone one, **When** `update-skills` runs, **Then** its patterns
   are recorded as offered in `.gitignore`, once.

---

### User Story 5 - Faster deletes, reliable scripts and tests (Priority: P3)

`spectra delete` in a large workspace no longer reads every test in full. Acceptance scripts check out with Unix line
endings. The test suite is fully green with no order-dependent flakiness. The unused config key is gone from new
workspaces.

**Why this priority**: Quality-of-life and engineering hygiene; nothing is lost today, but it slows and confuses people.

**Independent Test**:
- Deleting one test among 2,500 is measured against `23aa08c` and is well under it, with the same results.
- An acceptance script checks out with LF endings.
- The full test run has 0 failures.
- A new `spectra init` config has no `ai.generation_batch_size`, while an old config with it still loads.

**Acceptance Scenarios**:

1. **Given** 2,500 tests, **When** one is deleted, **Then** the result equals today's and the time is well under the
   `23aa08c` time (both recorded).
2. **Given** the repository, **When** a `.sh` file is checked out, **Then** it has LF line endings.
3. **Given** the test suite, **When** it runs in full, **Then** no test fails, and the working-directory tests never run
   in parallel with each other.
4. **Given** a config from before 096 with `ai.generation_batch_size`, **When** any command loads it, **Then** it loads
   without error; a new init config doesn't contain the key.

---

### Edge Cases

- **Manifest keys:**
  - a workspace moved to another folder: absolute keys are re-rooted by their install-path tail;
  - a foreign key that matches no install path is dropped, with one summary warning for all of them;
  - duplicate pre-096 manifest keys for one file (relative from init, absolute from update-skills): any of their hashes
    marks the file as unedited; one normalised entry is saved;
  - a key that names a file no longer bundled is kept but unused.
- **Missing file:** an installed file is missing from disk → recreated and recorded (as today).
- **Hash mismatch:** an installed file with a record whose hash differs from the file (edited) → skipped (as today); it
  stays skipped when the bundled content changes too.
- **Skip advice:**
  - templates: `spectra prompts reset <id>` (or delete the file);
  - skills, agents, the profile and `CUSTOMIZATION.md`: delete the file to get the new version.
- **Suite delete:**
  - a dependent in the same suite needs no edit (it is deleted too);
  - a dependent whose `depends_on` names several deleted ids is cleaned once;
  - a suite with none of the leftovers deletes without warnings.
- **`show`:** an id that is not in any index → "not found" as today, never a crash.
- **`validate`:** a profile error and a parse error both count; warnings alone keep exit 0.
- **Gitignore offer:**
  - `.gitignore` absent → nothing created (090 rule), nothing recorded;
  - a marker present and the pattern also present → nothing changes.
- **Delete performance:**
  - a suite without `_index.json` falls back to scanning its files;
  - a file whose frontmatter can't be read is treated as unparseable (warned) as today.

## Requirements *(mandatory)*

### Functional Requirements

**F1 — update-skills never overwrites an edited file**

- **FR-001**: Every manifest entry MUST use one key form: repo-relative with `/` separators. A manifest loaded with other
  forms is normalised: absolute keys under the repository become relative; absolute keys from another location are
  re-rooted by their install-path tail; keys that match no install path are dropped with one summary warning; duplicate
  keys for one file keep all their hashes as candidates (research R1).
  The next save writes the normalised form.
- **FR-002**: In `update-skills`, a file with no manifest record MUST be treated as possibly edited:
  - content equal to the bundled content → recorded;
  - otherwise skipped, never overwritten, and reported as "skipped (no record — it may have been edited)" with how to
    get the new version (`spectra prompts reset <id>` for templates; delete the file for the others).
- **FR-003**: FR-001/FR-002 MUST hold for skills, agents, prompt templates, the default profile and `CUSTOMIZATION.md`,
  and the 094 batch compile's unedited-copy check MUST use the same key rule.

**F2 — suite delete uses the 090 deletion rules**

- **FR-004**: `spectra suite delete` MUST remove its tests' dependencies in other suites exactly as `spectra delete`
  does since 090:
  - single-value `depends_on` cleared through the lossless writer;
  - list-form or unparseable dependents warned, never edited;
  - nothing else in any other file changes.
- **FR-005**: It MUST also remove the suite's baseline file, its staging folders (updates and repairs, including failed
  files), its generation plan, and its tests' verdict and fingerprint-record files. `--dry-run` lists all of these and
  changes nothing.

**F3, F4 — truthful show and validate**

- **FR-006**: `spectra show <id>` MUST find a test through its suite folder plus the index's file name, as the other
  commands do since 073/075; the legacy `suite/file` form stays accepted.
- **FR-007**: `spectra validate` MUST exit 1 with status "failed" and the error count when any test fails to parse (and
  for any error already counted), in human and JSON mode. Warnings alone keep exit 0.

**F5 — gitignore**

- **FR-008**: `.spectra/verdicts/` and `.spectra/dropped-tests.json` MUST be in the shared gitignore list; `spectra init`
  writes them, `update-skills` offers them once.
- **FR-009**: Whether a pattern was offered MUST be recorded in the repository itself — a marker line in `.gitignore`,
  `# spectra-offered: <pattern>`, written when the pattern is first offered. `update-skills` never re-adds a pattern
  whose marker exists, whatever the clone's local state. Existing per-clone records are migrated to markers once. With
  no `.gitignore`, nothing is created or recorded (090 rule).

**F6 — delete performance**

- **FR-010**: `spectra delete` MUST find its targets through each suite's index (scanning only a suite without one) and
  find dependents by reading frontmatter only, fully parsing only files that name a deleted id. Results are identical to
  today's (the existing delete tests stay green).

**F7 — line endings**

- **FR-011**: Shell scripts in the repository MUST check out with LF line endings regardless of `core.autocrlf`; the
  existing acceptance scripts are normalised.

**F8 — test infrastructure**

- **FR-012**: The working-directory test collection MUST be defined as non-parallel; the usage-guide test MUST assert on
  a heading the embedded guide has. No production change; the full test run has 0 failures.

**F9 — the unused batch-size key**

- **FR-013**: `ai.generation_batch_size` MUST be removed from the init template. A config that still contains it MUST
  load as before (the property stays, ignored and marked obsolete). The docs say `generation.batch_size` is the one that
  applies.

**Constraints**

- **FR-014**: Every fix is verified on a workspace from the real `spectra init` and on a pre-096 workspace (pre-096
  manifest with relative and absolute keys, edited templates, an old config with `ai.generation_batch_size`). No config
  section becomes required; human summaries stay on stderr in JSON mode.

### Required Tests

Each fails on `23aa08c` (except stated guards):
- **F1:**
  - real init + edits of a template, the profile and `CUSTOMIZATION.md`, then update-skills with changed bundled content
    → byte-identical, reported skipped;
  - duplicate pre-096 keys with different hashes: a file matching either is unedited, an edited one is customized, one
    entry is saved;
  - no record + equal content → recorded;
  - a pre-096 mixed-key manifest → normalised, no edited file overwritten;
  - the 094 unedited-copy check on a normalised manifest.
- **F2:**
  - external body line `- TC-101` and blank-line run byte-identical;
  - scalar dependent cleaned;
  - list dependent warned;
  - leftovers removed;
  - `--dry-run` lists them and changes nothing.
- **F3:** `show` on a standard (bare file name) suite; the legacy form (guard).
- **F4:** one unparseable test → exit 1 / "failed" (human and JSON).
- **F5:**
  - a clone with a marker and the pattern removed → not re-added;
  - the two new entries offered once;
  - per-clone record migrated once.
- **F6:** a deterministic row — deleting one of 2,500 tests fully parses only the target and its scalar dependent (2,
  not 2,500); the time is recorded against `23aa08c`, not asserted; the existing delete tests (guards).
- **F7:** the attributes rule makes a `.sh` file LF on checkout.
- **F8:** the corrected usage-guide test; the collection definition (the suite runs green in full).
- **F9:**
  - the init template has no `ai.generation_batch_size`;
  - an old config with it loads (guard);
  - `ConfigRuleMatrixTests` / `InitTemplateWorkspaceTests` green.

### Key Entities

- **Skills manifest**: the per-clone record of installed files' hashes, keyed repo-relative with `/` separators.
- **Offered marker**: a comment line in `.gitignore`, `# spectra-offered: <pattern>`, recording that SPECTRA offered the
  pattern once, for every clone.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After `update-skills` with changed bundled content, 100% of user-edited SPECTRA files (templates, profile,
  `CUSTOMIZATION.md`, skills, agents) are byte-identical to before.
- **SC-002**: Deleting a suite changes 0 bytes of other suites' files beyond the removed dependencies, and leaves 0
  leftover baseline, staging, plan or verdict files of that suite.
- **SC-003**: `spectra validate` exits non-zero for 100% of suites containing an unparseable test.
- **SC-004**: A pattern removed from `.gitignore` after it was offered is re-added in 0 of the clones.
- **SC-005**: Deleting one test among 2,500 is at least 3× faster than at `23aa08c`, with identical results.
- **SC-006**: The full test run has 0 failing tests.

## Assumptions

- The offered marker lines live in the `# SPECTRA` section of `.gitignore`. They are plain comments, so git ignores them,
  and users can delete them to be offered a pattern again.
- "Bundled content changed" in tests is simulated by editing the installed file's recorded hash, or by a test hook for the
  bundled text, without changing the shipped content.
- The deletion-service behaviour of 090 (warnings for list and unparseable `depends_on`) is reused unchanged; list
  support stays out of scope.
- The 3× target for SC-005 is set against the measured `23aa08c` time on the same machine and data.

## Out of Scope

- The 097–099 items (verification hardening, baseline and generation precision, docs and constitution).
- `depends_on` list support.
- Any new command or flag.
