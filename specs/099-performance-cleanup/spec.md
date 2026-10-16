# Feature Specification: Performance and Cleanup (Phase 6, Part 4)

**Feature Branch**: `099-performance-cleanup`
**Created**: 2026-10-14
**Status**: Draft
**Input**: User description: "099-performance-cleanup: Phase 6, part 4 — performance and code cleanup. No behaviour
change: every command produces byte-identical files and identical JSON/exit codes before and after (except where a fix
below says otherwise). Each performance change is proven by a deterministic count (parses, heading scans) rather than a
wall-clock assertion; timings are recorded in the Notes only (095/096 lesson). Docs and the constitution are 100."
(Background, F1–F7, constraints and scope as quoted in the request.)

## Context

Specs 078–098 each left a performance or cleanup finding that was deferred so correctness could come first. None of
them changes what SPECTRA says or writes; each one makes it do the same work more than once, carry code nothing calls,
or build with warnings. The CLI test suite has also grown slower (≈ 6.5 min → ≈ 10 min on the 098 run).

Facts (verified on `c24d23e`):
- **Two suite parses per ingest (085 review).** `ingest-tests` and `ingest-update` parse every test of the suite to load
  the existing tests, then recording the baseline scans and parses every file of the suite again. An N-test suite costs
  2N full parses per ingest call. (The index is already built from the in-memory list; it does not parse again.)
- **Headings re-scanned (085 review).** The baseline context caches each document's text, but the heading list is
  rebuilt from that text for every ref resolved and every heading lookup — once per ref, not once per document.
- **Layout capture on read-only paths (078 review).** Every test parse captures the 078 layout (raw frontmatter entries,
  body segments, newline style) that only the lossless writer uses. Index, list, show, coverage, dashboard, triage,
  audit and validate pay for it and never write.
- **The writer's no-layout path has two meanings.** A test with no layout is written canonically. That is correct for a
  brand-new test (no file yet) and wrong for a test read from a file: it would silently drop unknown keys, comments and
  unknown body sections. Today nothing parses without a layout, so the second case cannot happen; F3 makes it possible.
- **Triple JSON parse (089 review).** `ingest-update --all` reads each staged file's JSON array once in the command
  (`MULTIPLE_TESTS` / `ID_MISMATCH`), again in the update ingestor, and a third time in validation.
- **Dead code (095 review).** The analysis recommendation builder still takes a coverage snapshot argument that its only
  caller passes as null; its covered-behaviour count helper is used only by tests. The 058/059/069/070 retirements may
  have left other code with no production caller (e.g. the Copilot-era agent tool registry and document/index tool
  wrappers).
- **Four build warnings.** A clean build reports CS0108 (a `Name` member hiding the base `Name`) in the docs apply-move
  and docs retire commands, and CS8714 / CS8621 (nullable key type) in the criteria layout migrator.

## Decisions

Made while writing this spec (open to change at `/speckit.clarify`):

- **"Byte-identical" baseline.** Outputs are compared against golden files captured by running the `c24d23e` build
  over a fixed fixture workspace. Values that differ between any two runs of the same build (timestamps such as
  `verified_at` / `compiled_at` / `generated_at`, absolute temp paths, durations) are masked the same way in the capture
  and the check; everything else is compared byte for byte. The mask list is written down in research, and each masked
  field is shown to vary between two `c24d23e` runs.
- **F1 — what the single parse must cover.** The parse that loads the existing tests becomes the scan the baseline
  needs: exactly the files the baseline scan read before (the suite folder's `*.md`, not `_`-prefixed), the same tests,
  ids and unparseable-file list. The tests the ingest just wrote replace their parsed copies in that scan; they are not
  re-read from disk. The count is N full parses per ingest call for a suite of N test files (each parse attempt counts, an unparseable file included); any remaining extra parse is pinned by the guard and explained in the Notes; the written tests add 0.
- **F3 — the writer guard.** A test parsed without layout is marked as such. The writer refuses it with an internal
  error (a programming error, never a user-facing outcome) instead of writing canonical output. A test built in memory
  with no source file (a new generated test) still writes canonically, as today. Every path that later calls the writer
  parses with layout.
- **F5 — tests of deleted code.** A test that exercises only a deleted member (e.g. the covered-behaviour count helper)
  is removed with it and listed in research next to the member; that is not "weakening" — the code it covered no longer
  exists. A test that uses a deleted member as a helper for something else gets a local copy in the test project.
- **F5 — what counts as a reference.** A candidate is deleted only with zero references from production code (any
  project under `src/`), counting reflection, DI registration, `[JsonSerializable]`/serializer contexts and embedded
  resource names. References from tests alone don't keep it.
- **F7 — which process launches stay.** A test whose subject is process-level behaviour (stdout encoding on a redirected
  stream, exit codes from `Main`, environment-variable isolation, git child processes) keeps its process launch. Only
  launches whose assertion an in-process call proves equally are replaced.
- **F7 — fixture sharing.** A fixture is shared per class only when no test of that class writes to it; a class whose
  tests write gets a fresh copy per test made by copying the shared fixture, not by re-running `spectra init` or
  re-creating a git repository, where the copy proves the same thing.
- **F7 — serial collections.** Research found that the serial test collections (≈ 1,100 cases run one class at a
  time because their helpers change the current directory and `Console`) and the git fixtures dominate the run, more
  than process launches. A class that uses no process-global state may leave its serial collection; that removes no
  coverage. The existing wall-clock assertions (e.g. the 2,500-test triage setup bound) are kept as they are.
- **Warnings as errors.** `TreatWarningsAsErrors` is set for the CLI and Core projects only (as requested). Test projects
  and `Spectra.Execution` are not changed; their warnings (if any) are recorded in the Notes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Nothing a user sees changes (Priority: P1)

A team upgrades from 098 to 099. Every command they run writes the same files, prints the same JSON and exits with the
same codes. The work below only removes repeated work and unused code.

**Why this priority**: A cleanup that changes an output is a regression in disguise; every later story depends on this
guarantee holding.

**Independent Test**: The golden-output check: a fixture workspace driven through init, ingest (new tests and an
update), triage, update prompt compilation, audit and generation-plan ingest on `c24d23e` and on the 099 build; files,
stdout JSON and exit codes compared after masking.

**Acceptance Scenarios**:

1. **Given** the fixture workspace and the golden files captured on `c24d23e`, **When** the same command sequence runs
   on the 099 build, **Then** every file written, every stdout JSON document and every exit code is byte-identical
   after masking.
2. **Given** a masked field, **Then** research shows it differs between two runs of `c24d23e` (nothing is masked only
   because 099 changed it).
3. **Given** a failure path in the sequence (a refusal such as `TRIAGE_MISSING`, a `MULTIPLE_TESTS` staged file),
   **Then** its JSON and exit code are identical too.

---

### User Story 2 - An ingest reads the suite once (Priority: P1)

A team with a large suite (thousands of tests) ingests a batch of new tests, or an update. Each call should read and
parse the suite's test files once, not twice.

**Why this priority**: Ingest runs after every generation batch and every update group; the second parse doubles the
cost of the most frequent write commands.

**Independent Test**: A parse counter over a suite of N test files: one `ingest-tests` call, one `ingest-update
--test-id` call and one `ingest-update --all` call each show N full parses per ingest call for a suite of N test files (each parse attempt counts, an unparseable file included); any remaining extra parse is pinned by the guard and explained in the Notes; the baseline file and test files match
the golden output.

**Acceptance Scenarios**:

1. **Given** a suite of N test files, **When** `ingest-tests` writes new tests, **Then** N full parses per ingest call for a suite of N test files (each parse attempt counts, an unparseable file included); any remaining extra parse is pinned by the guard and explained in the Notes (not 2N),
   and the baseline file is byte-identical to `c24d23e`'s.
2. **Given** a suite of N test files, **When** `ingest-update` (single or `--all`) writes edits, **Then** N full parses per ingest call for a suite of N test files (each parse attempt counts, an unparseable file included); any remaining extra parse is pinned by the guard and explained in the Notes,
   and the baseline is the same.
3. **Given** a suite with an unparseable test file, **Then** it is still reported unparseable and the baseline still
   skips pruning with the same warning (085 F2).
4. **Given** `--keep-baseline` or `--dry-run`, **Then** behaviour and outputs are unchanged.

---

### User Story 3 - Triage resolves headings once per document (Priority: P2)

A suite of 2,500 tests whose refs point into a few dozen documents is triaged. Each document's headings should be found
once per run, however many refs point into it.

**Why this priority**: Triage, update-prompt compilation, relinking and grounding audits all resolve refs through the
same context; the repeated scan grows with refs × document size.

**Independent Test**: A scan counter: a context resolving many refs into 3 documents shows 3 heading scans; triage
detail JSON and compiled update prompts byte-identical.

**Acceptance Scenarios**:

1. **Given** 50 refs into one document, **When** they are resolved in one context, **Then** the document's headings are
   scanned once.
2. **Given** a heading lookup for a document already resolved, **Then** no further scan.
3. **Given** two separate runs (two contexts), **Then** each scans once (no cache outlives its context).
4. **Given** the 084/085/098 matcher, ambiguity and determinism rows, **Then** they stay green unchanged.

---

### User Story 4 - Read-only commands skip the write-only layout (Priority: P2)

A user lists, shows, indexes, validates, audits or triages tests, or builds the dashboard. These commands never write a
test file, so they should not pay for capturing what the writer needs.

**Why this priority**: These are the commands run most often and over whole workspaces.

**Independent Test**: Read-only commands parse without layout (asserted by a mode check); their outputs are
byte-identical; a writer given a test parsed without layout fails loudly; every writer path is shown to parse with
layout.

**Acceptance Scenarios**:

1. **Given** a read-only command, **When** it parses tests, **Then** no layout is captured and its output is
   byte-identical to `c24d23e`'s.
2. **Given** a test parsed without layout, **When** it is passed to the test writer, **Then** the writer fails with an
   internal error naming the test, and writes nothing.
3. **Given** a new test built in memory (no source file), **Then** it is written canonically, as today.
4. **Given** every path that writes an existing test (update ingest, grounding write-back, review accept, flagged
   review, orphan marking, relink, delete's `depends_on` cleanup, legacy update apply), **Then** a test proves it parses
   with layout, and lossless-write rows (078/080/081) stay green.

---

### User Story 5 - A staged update file is read once (Priority: P3)

The update skill stages one file per edited test and runs `ingest-update --all`. Each file's JSON should be read once.

**Why this priority**: Small per file, but the update flow is a batch over every edited test.

**Independent Test**: A parse counter: K staged files → K JSON parses; `MULTIPLE_TESTS`, `ID_MISMATCH` and ingest codes
and messages unchanged.

**Acceptance Scenarios**:

1. **Given** K staged files, **When** `ingest-update --all` runs, **Then** K JSON parses in total.
2. **Given** a staged file with two tests, an id mismatch, invalid JSON or a validation failure, **Then** the outcome,
   code, message, `failed-<id>.json` rename and exit code (8) are unchanged.
3. **Given** `ingest-update --test-id` with `--from`, **Then** the single-file path is unchanged in output.

---

### User Story 6 - A smaller, warning-free codebase (Priority: P3)

A maintainer builds SPECTRA and finds no warnings and no code that nothing calls; a new warning breaks the build.

**Why this priority**: Warnings and dead code hide real problems and mislead the next spec's research.

**Independent Test**: A clean build shows 0 warnings; a deliberately introduced warning in the CLI or Core project fails
the build; research lists every dead-code candidate with its production reference count.

**Acceptance Scenarios**:

1. **Given** a clean build, **Then** 0 warnings across the solution's CLI and Core projects.
2. **Given** a new warning in the CLI or Core project, **Then** the build fails.
3. **Given** the dead-code list in research, **Then** every deleted item had 0 production references, and every kept
   item states why (a reference, or an explicit keep: retired-key detection, `AiConfig.GenerationBatchSize`).
4. **Given** the recommendation builder, **Then** it no longer takes the unused snapshot argument, and the analysis
   ingest output is unchanged.

---

### User Story 7 - The test suite runs faster (Priority: P3)

A maintainer runs the full CLI test suite before every merge. It should be back near its earlier duration without
losing any coverage.

**Why this priority**: A 10-minute suite slows every spec; but it doesn't affect users, so it comes last.

**Independent Test**: The 20 slowest CLI test classes timed on `c24d23e` and on 099, recorded per class in the Notes;
the test count per class unchanged or higher; the total measured.

**Acceptance Scenarios**:

1. **Given** the 20 slowest classes on `c24d23e`, **Then** their durations are recorded in the Notes before any change.
2. **Given** a class changed for speed, **Then** its number of test cases and its assertions are unchanged (listed
   before/after), and its duration is recorded after.
3. **Given** the full CLI suite on this machine, **Then** its total duration is measured and recorded; the target is
   under ≈ 7 min, reported as measured, not asserted by a test.

### Edge Cases

- F1: the suite folder does not exist yet (first ingest of a new suite) → 0 parses, baseline as today.
- F1: a file changes on disk between the parse and the baseline record (another process) → the baseline reflects the
  ingest's parse plus its own writes; noted as an assumption, the baseline lock is unchanged.
- F1: an ingest that writes nothing (all unchanged, or every staged file failed) → still one parse, same outputs.
- F2: a document that cannot be read → no heading scan, the same missing reason as today.
- F2: the same document reached by two spellings of its path → cached under the key the text cache already uses (no new
  normalisation).
- F3: a test parsed without layout reaching the writer through a `with` copy → still refused (the mark survives copies).
- F3: a command that both reads many tests and writes one (e.g. delete's `depends_on` cleanup) → the written test is
  parsed with layout; the others may not be.
- F4: a staged file that is empty or not a JSON array → same `failed` outcome and code.
- F5: a type referenced only by another dead type → both deleted together, listed as a chain.
- F6: the `Name` fix must not change any command or option name in `--help` output.
- F7: a test that shares a fixture must not depend on another test's writes (order independence kept; xUnit order is
  not relied on).

## Requirements *(mandatory)*

### Functional Requirements

**Cross-cutting — no behaviour change**
- **FR-001**: Every command MUST write byte-identical files and print identical JSON and exit codes on the 099 build and
  on `c24d23e`, except where a requirement below says otherwise (none does for user-visible output).
- **FR-002**: A golden-output check MUST drive a fixture workspace through init, `ingest-tests`, `ingest-update` (single
  and `--all`, incl. one failing staged file), `update-triage`, `compile-update-prompt`, `audit-grounding` and
  `ingest-analysis` (generation plan), plus the help output of `spectra --help`, `spectra docs --help`, `spectra ai
  --help`, `spectra docs apply-move --help` and `spectra docs retire --help`, and the refusals `TRIAGE_MISSING`
  (`update-ack` before triage) and `GROUP_NOT_FOUND` (`compile-update-prompt --group G99`), with outputs captured on
  `c24d23e` and compared after masking. The mask list MUST
  be documented, and each masked field MUST be shown to vary between two `c24d23e` runs.
- **FR-003**: No CLI command, option, config key, template or file format MAY change.

**F1 — one suite parse per ingest**
- **FR-004**: `ingest-tests` and `ingest-update` (single, `--all`, `--keep-baseline`) MUST parse the suite's test files
  once per call and pass the result (tests, ids, unparseable files) to the baseline record, with the tests written by
  the call substituted for their parsed copies.
- **FR-005**: The single parse MUST cover exactly the files the baseline scan covered on `c24d23e` and report the same
  unparseable-file list, so pruning and its warning are unchanged.
- **FR-006**: A parse counter MUST count every test-file parse made during one ingest call (including the pending-change
  check and triage it runs), isolated per call so parallel tests never see each other's parses. For an N-test-file
  suite (each parse attempt counts, an unparseable file included) the target is N per call. Parses beyond N that
  remain because removing them would change behaviour MUST be pinned by the guard at their real number and listed in
  the Notes with the reason.

**F2 — cached headings**
- **FR-007**: The baseline context MUST compute each document's heading list once and reuse it for section extraction,
  covered-heading lookup and heading queries, keyed as the text cache is keyed.
- **FR-008**: A scan counter (same per-call isolation as FR-006) MUST show one heading scan per document per context. Triage detail
  JSON, triage stdout and compiled update prompts MUST be byte-identical.

**F3 — layout capture only when writing**
- **FR-009**: The test parser MUST offer a parse without layout capture; read-only callers (index, list, show,
  coverage, dashboard, triage, audit, validate and any other caller that never writes the parsed test) MUST use it.
- **FR-010**: Every path that passes a parsed test to the test writer MUST parse with layout.
- **FR-011**: The test writer MUST refuse a test that was parsed without layout (internal error, nothing written); a
  test built in memory without a source file MUST still be written canonically.
- **FR-012**: A guard test MUST fail if any writer path parses without layout; the 078/080/081 lossless-write rows MUST
  stay green.

**F4 — one JSON parse per staged file**
- **FR-013**: `ingest-update --all` MUST parse each staged file's JSON once and hand the parsed array to the multiple-test
  check, the id check and the ingestor's validation.
- **FR-014**: `MULTIPLE_TESTS`, `ID_MISMATCH`, every ingest code and message, the `failed-<id>.json` rename and exit 8
  MUST be unchanged. A parse counter MUST show K parses for K staged files.

**F5 — dead code**
- **FR-015**: The recommendation builder's unused snapshot parameter and the test-only covered-behaviour count helper
  MUST be removed; a test that only exercised the helper is removed with it (listed), a test using it as a helper gets a
  local copy.
- **FR-016**: Research MUST list every dead-code candidate left by the 058/059/069/070 retirements with its production
  reference count; only candidates with 0 production references MAY be deleted. Retired-key detection and
  `AiConfig.GenerationBatchSize` MUST be kept.

**F6 — build warnings**
- **FR-017**: The four warnings MUST be fixed without behaviour change; a clean build MUST show 0 warnings for the CLI and
  Core projects.
- **FR-018**: The CLI and Core projects MUST treat warnings as errors.

**F7 — test-suite duration**
- **FR-019**: The 20 slowest CLI test classes MUST be measured on `c24d23e` and recorded in the Notes.
- **FR-020**: Changes for speed MAY share fixtures that no test of the class writes to (or copy a once-built fixture per
  test), move a class out of a serial collection when it uses no process-global state (current directory, `Console`,
  static overrides, singletons), and replace a full-CLI process launch with an in-process call only where the assertion
  is the same; no test MAY be deleted or weakened (F5's listed removals excepted), and no timing threshold of an
  existing test MAY be relaxed. Before/after durations and test counts per changed class MUST be recorded in the Notes.
- **FR-021**: The full CLI suite duration MUST be measured and recorded; the target (under ≈ 7 min on this machine) is
  reported, not asserted.

**Cross-cutting — verification and docs**
- **FR-022**: Each performance change (F1, F2, F4) MUST be proven by a deterministic count, not a wall-clock assertion;
  timings appear only in the Notes.
- **FR-023**: The whole suite (Core, CLI, Execution, Integration) MUST stay green with 0 build warnings.
- **FR-024**: The 083 rule holds: the golden fixture MUST be made by the real `spectra init`, and the change MUST also be
  run against a pre-099 workspace.
- **FR-025**: Docs: CHANGELOG.md and a short CLAUDE.md entry. No user-facing doc changes (nothing user-facing changes).

### Key Entities

- **Golden output**: the files, stdout JSON and exit codes of the fixture command sequence on `c24d23e`, with a
  documented mask list.
- **Suite scan**: a suite's parsed tests, their ids and its unparseable files — the input the baseline record needs.
- **Heading list**: one document's headings with their keys, computed once per context.
- **Layout**: what the lossless writer needs to reproduce a test file; present only when the test was parsed for
  writing.
- **Dead-code candidate**: a member or type with its production reference count and a keep/delete decision.
- **Class timing**: a test class's duration and test count before and after.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 0 differences (after masking) between `c24d23e` and 099 over the golden-output sequence.
- **SC-002**: An ingest over a suite of N test files (each parse attempt counts, an unparseable file included) parses
  N times, down from 2N; any remaining extra parse is pinned and explained in the Notes.
- **SC-003**: Resolving any number of refs into D documents performs D heading scans per run.
- **SC-004**: 0 read-only commands capture layout; 0 writer paths parse without it.
- **SC-005**: `ingest-update --all` over K staged files performs K JSON parses, down from 3K.
- **SC-006**: 0 build warnings in the CLI and Core projects; a new warning fails the build.
- **SC-007**: Every deleted dead-code item had 0 production references (listed with counts).
- **SC-008**: The 20 slowest CLI test classes are timed before and after, with unchanged or higher test counts; the full
  CLI suite is measured and recorded. Reference: 420 s on this machine (baseline 597 s idle on `c24d23e`) — measured and
  recorded, not asserted.
- **SC-009**: The whole suite passes with 0 failures.

## Assumptions

- The golden capture can be made by building `c24d23e` in a separate worktree and running the same fixture sequence;
  the fixture workspace is deterministic apart from the masked fields.
- Between the ingest's parse and its baseline record, no other process changes the suite (the same assumption the
  second scan made implicitly within one call; the baseline lock is unchanged).
- A brand-new test has no source file; a test read from disk always does.
- Timings are machine-dependent; they are recorded, never asserted.

## Out of Scope

- 100: docs trim, constitution amendment.
- `depends_on` list support.
- Any behaviour change, including fixing behaviour found odd while measuring (recorded for a later spec instead).
- Warnings-as-errors for test projects and `Spectra.Execution`.
