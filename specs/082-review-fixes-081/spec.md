# Feature Specification: Review Fixes for Spec 081

**Feature Branch**: `082-review-fixes-081`
**Created**: 2026-09-30
**Status**: Draft
**Input**: User description: "Review fixes for spec 081, found by code review before merge. No new features. Every fix needs a regression test that fails before the fix." (A1 coverage section on criteria writes, A2 capitalised placeholders stripped as HTML, A3 null config sections, A4 config error codes — full text in the triggering request.)

## Context

Spec 081 (merged as `7df3fef`) made each criteria and compile command declare which `spectra.config.json` sections it reads, extended the HTML tag list used when comparing criterion text, and gave all criteria commands one failure JSON shape. The review of 081 found four problems, all in that new behaviour:

- **A1** The rule "a write command never acts on default paths the user did not choose" guards the wrong section. Criteria write commands write to the location in `coverage`, which is never checked, while `tests`, which they only read for an informational count, makes them fail.
- **A2** The longer tag list, matched case-insensitively, strips words that docs use as angle-bracket placeholders (`<Source>`, `<Summary>`, `<Section>`, `<Input>`, `<Details>`, `<Caption>`), bringing back the problem 080 fixed for `<Limit>`.
- **A3** A section written as `null` counts as present, so it is not given defaults and then fails validation in every command, including commands that never read it.
- **A4** Config problems are reported with no error code (`error_code: null`), and the criteria skill relies on "no code means config problem".

No new features. Every fix has a regression test that fails before the fix.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Criteria are never written to a location the user did not configure (Priority: P1)

A user runs a criteria write command (extracting criteria, retiring or moving a document, migrating the criteria layout, importing criteria) in a workspace whose config has no `coverage` section. The command refuses and names the missing section instead of writing to the built-in default criteria location. A config that has `coverage` but no `tests` section works: the command writes as usual and only notes that it used the default tests location for its "tests still referencing this criterion" count.

**Why this priority**: Writing criteria to a location the user never chose is the exact outcome the write-command rule exists to prevent, and it creates files and index entries in the wrong place without any warning.

**Independent Test**: Run each criteria write command against a config without `coverage`, then against a config with `coverage` but without `tests`, and inspect the exit code, output and files on disk.

**Acceptance Scenarios**:

1. **Given** a config without a `coverage` section, **When** the user runs `ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria` or `--import-criteria`, **Then** the command exits 1, reports that the `coverage` section is missing, and writes nothing.
2. **Given** a config with `coverage` but without `tests`, **When** the user runs `ingest-criteria` or `docs retire`, **Then** the command succeeds, writes to the configured criteria location, and prints a warning that the default tests location was used for the referencing-test counts.
3. **Given** a config without `coverage`, **When** the user runs a read-only command that uses it (`compile-extraction-prompt`, `docs changed`), **Then** the command uses the built-in criteria location and prints a warning naming the section.
4. **Given** a config without `coverage` and `--dry-run`, **When** a criteria write command runs, **Then** it still refuses the same way (a dry run reports what a real run would do).

---

### User Story 2 - Placeholders in angle brackets are compared as text (Priority: P1)

A documentation author writes placeholders such as `<Source>`, `<Summary>` or `<Details>` in requirements text. When criteria are re-extracted after the doc changes, a change inside such a placeholder is detected as a real change, while genuine HTML markup (for example `<details open>`, `<figure>`, `</summary>`) is still ignored.

**Why this priority**: A missed change leaves the criterion with its old wording while the doc says something else, and later phases reuse the same comparison to decide which tests are affected.

**Independent Test**: Compare pairs of criterion texts that differ only in a capitalised placeholder, and pairs that differ only in real HTML markup, and check which pairs count as changed.

**Acceptance Scenarios**:

1. **Given** criterion text "Enter <Summary> and press Save" changing to "Enter <Details> and press Save", **When** the criteria are compared, **Then** the change is reported as a modification.
2. **Given** "Select <Source> from the list" changing to "Select <Target> from the list", **When** compared, **Then** the change is reported.
3. **Given** text that differs only in HTML markup written in HTML form (lowercase tags such as `<details open>`, `<figure>`, `<figcaption>`, a closing tag such as `</Summary>`, or a tag with attributes), **When** compared, **Then** no change is reported.
4. **Given** all comparison cases already fixed in specs 080 and 081 (`total<limit and qty>5`, `<Limit>`, bare boolean attributes, the extended tag list), **When** compared, **Then** they behave exactly as before.

---

### User Story 3 - A section set to null behaves like a missing section (Priority: P2)

A user blanks a config section by writing `"ai": null` (or `"source": null`, `"tests": null`, `"coverage": null`) instead of deleting it. Every command treats it exactly as if the section were absent: commands that don't read it are unaffected, read commands use defaults with a warning, and write commands that need it refuse and name it.

**Why this priority**: The mistake is easy to make and today it blocks every command, but it has a simple workaround (delete the line), so it ranks below the two P1 stories.

**Independent Test**: For each section, run a command that does not read it, a read command that does, and a write command that does, first with the section missing and then with it set to `null`, and compare the outcomes.

**Acceptance Scenarios**:

1. **Given** a config with `"ai": null`, **When** any criteria or compile command runs, **Then** it behaves exactly as with no `ai` key (none of them read `ai`).
2. **Given** a config with `"source": null`, **When** `docs changed` runs, **Then** it uses the default source location and warns, exactly as when `source` is missing.
3. **Given** a config with `"coverage": null`, **When** a criteria write command runs, **Then** it exits 1 naming the `coverage` section, exactly as when `coverage` is missing.

---

### User Story 4 - Config problems carry a named error code (Priority: P3)

A script, CI job or the criteria skill reads the failure JSON of a criteria or compile command. Every config problem has its own named error code, so the reader can tell "no config file", "config not valid" and "a needed section is missing" apart without inferring anything from a missing code.

**Why this priority**: The current behaviour works but is fragile; it only becomes a bug when another failure path forgets its code.

**Independent Test**: Trigger each config problem on each in-scope command in JSON mode and check the `error_code` in the single failure document; check the criteria skill instructions refer to the named codes.

**Acceptance Scenarios**:

1. **Given** no `spectra.config.json`, **When** a criteria write command runs in JSON mode, **Then** the failure JSON has `error_code` `CONFIG_MISSING`.
2. **Given** a config that is not valid JSON or fails validation, **When** any in-scope command runs in JSON mode, **Then** the failure JSON has `error_code` `CONFIG_INVALID`.
3. **Given** a config missing a section a write command needs, **When** it runs in JSON mode, **Then** the failure JSON has `error_code` `CONFIG_SECTION_MISSING` and names the section.
4. **Given** the updated criteria skill, **When** it describes how to handle exit 1, **Then** it names these codes and no longer treats a missing code as meaning a config problem.

---

### Edge Cases

- A config with neither `coverage` nor `tests`: a criteria write command refuses on `coverage` (the section it writes to); `tests` alone would only have produced a warning.
- A config file that is empty or contains `null` at the top level: reported as `CONFIG_INVALID`, never as a missing file.
- A section set to an empty object (`"coverage": {}`): present, so its built-in defaults apply without a warning, as today.
- Placeholders with a capital first letter that also have attributes (`<Details open>`) or are closing tags (`</Summary>`): these are HTML form and are ignored when comparing.
- All-capitals bare words in angle brackets (`<LIMIT>`, `<BR>`): treated as text (see Assumptions).
- A read command whose config lacks both a section it reads and a section it does not read: one warning, for the section it reads only.
- Human-mode output for config problems keeps its current wording; only the JSON gains the codes.

## Requirements *(mandatory)*

### Functional Requirements

**A1 — Criteria location is required on writes**

- **FR-001**: Every criteria write command (`ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`, `--import-criteria`) MUST declare that it reads the `coverage` section, and MUST exit 1 without writing anything when that section is missing, with a message naming the `coverage` section.
- **FR-002**: `ingest-criteria` and `docs retire` MUST NOT fail when the `tests` section is missing. They MUST use the built-in tests location for the referencing-test counts and print a warning naming the `tests` section.
- **FR-003**: Read-only commands that use `coverage` (`compile-extraction-prompt`, `compile-critic-prompt`, `docs changed`) MUST declare it and, when it is missing, use the built-in criteria location with a warning naming the section.
- **FR-004**: The refusal in FR-001 MUST also apply to `--dry-run`.

**A2 — Placeholders stay text**

- **FR-005**: When comparing criterion text, an angle-bracket word MUST be treated as HTML markup only when it is written in HTML form: a lowercase tag name, a closing tag, a tag with attributes (valued or a bare boolean attribute), or a bare opening tag followed later by its matching closing tag. Anything else, including capitalised placeholders such as `<Source>`, `<Summary>`, `<Section>`, `<Input>`, `<Details>` and `<Caption>`, MUST be kept as text.
- **FR-006**: Every comparison case fixed by specs 080 (B9) and 081 (C3) MUST keep its current result.

**A3 — Null sections**

- **FR-007**: A top-level config section whose value is `null` MUST be handled exactly like a missing section, for every section and every in-scope command (ignored when not read, defaults plus a warning on read commands, exit 1 on write commands that need it).

**A4 — Named config error codes**

- **FR-008**: Every in-scope command's single failure JSON document MUST carry a named `error_code` for config problems: `CONFIG_MISSING` (no config file where one is required), `CONFIG_INVALID` (not valid JSON or fails validation), `CONFIG_SECTION_MISSING` (a section the command needs is missing or null; the message names it).
- **FR-009**: In-scope commands are the criteria commands (`ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`, `--import-criteria`) and the compile commands (`compile-extraction-prompt`, `compile-critic-prompt`, `compile-repair-prompt`, `compile-repair-batch`), plus `docs changed` where it reports a config failure. `compile-repair-prompt` and `compile-repair-batch` have no JSON mode, so they keep their plain-text config errors (FR-011).
- **FR-010**: The `spectra-criteria` skill MUST name these codes where it tells the session how to handle exit 1, and MUST NOT describe a missing or null `error_code` as meaning a config problem. Skill-content tests MUST be updated.
- **FR-011**: Human-mode messages for config problems keep their current wording.

**Rules that still apply**

- **FR-012**: Human-readable tables and summaries are never suppressed in JSON mode; failure output stays one machine-readable JSON document on stderr; the CLI stays model-free and deterministic; nothing is written on `--dry-run`.
- **FR-013**: Every item above MUST have a regression test that fails on `7df3fef` and passes after the fix.
- **FR-014**: `CHANGELOG.md`, the `CLAUDE.md` "Recent Changes" entry and `docs/cli-reference.md` MUST be updated where behaviour changes (the `coverage` requirement on criteria writes, the new error codes).

### Key Entities

- **Config section**: A top-level part of `spectra.config.json` (`source`, `tests`, `coverage`, `ai`, …). Each command declares which sections it reads; a missing or null section is handled by the rule in FR-001–FR-003 and FR-007.
- **Failure document**: The single JSON object a command writes to stderr on failure (`command`, `success`, `error_code`, `errors`, `warnings`, plus command-specific fields). This spec adds the three config error codes.
- **Comparison key**: The normalised form of criterion text used to decide whether a criterion changed. This spec narrows what counts as HTML markup when building it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a workspace without a `coverage` section, 0 of the 5 criteria write commands create or change any file, with or without `--dry-run`.
- **SC-002**: In a workspace with `coverage` but without `tests`, `ingest-criteria` and `docs retire` complete successfully in 100% of test runs and print exactly one warning about `tests`.
- **SC-003**: For each of the 6 named placeholders, a change from one placeholder to another is reported as a modification in 100% of cases, and every comparison case from specs 080 and 081 keeps its result.
- **SC-004**: For each of the 4 sections, the outcome with the section set to `null` is identical (exit code, output, files) to the outcome with the section missing.
- **SC-005**: Every config failure of every in-scope command in JSON mode carries one of the 3 named codes; none carries a null code.
- **SC-006**: Existing test suites stay green apart from the known pre-existing `ProfileFormatLoaderTests` failure.

## Assumptions

- "HTML form" means a lowercase tag name, a closing tag, or a tag with attributes. All-capitals bare words in angle brackets (`<LIMIT>`, `<BR>`) are treated as text: in these docs they are far more likely to be placeholders than legacy upper-case HTML, and treating a real tag as text only costs a spurious "modified" report, while the reverse hides a real change.
- An empty-object section (`"coverage": {}`) counts as present and configured; only a missing or `null` section triggers FR-001–FR-003.
- `ai` is still read by none of the in-scope commands, so it is never required.
- Human-mode wording is unchanged; scripts are expected to use the JSON codes.

## Plan-time amendments

Recorded in research R1, R2 and R4:
- FR-003 adds `compile-critic-prompt`, which reads `coverage` for its criteria context.
- FR-005 adds "a bare opening tag followed later by its matching closing tag". This keeps 080's `<P>Para</P>` case
  (FR-006), and a lone `<Summary>` placeholder stays text.
- FR-009 notes that the two repair compilers have no JSON mode.

## Out of Scope

- Phase 2 and later work (change detection, update triage, relinking).
- The `DeleteHandler` `depends_on` regex bug (tracked separately).
- The deferred 078 finding about the extra parsing cost of test files.
- Config handling of commands outside the in-scope list (`ai update`, `docs index`, other `ai analyze` modes).
