# Feature Specification: Review Fixes for Spec 082 — One Config Rule, Checked Against Real Workspaces

**Feature Branch**: `083-review-fixes-082`
**Created**: 2026-09-30
**Status**: Draft
**Input**: User description: "Review fixes for spec 082 — and closing the gap that produced three rounds of follow-up
fixes (080→081→082). No new features. Every fix needs a regression test that fails on 3594d62 and passes after." (Full
text in the triggering request: root cause, the one config rule, F1–F4, T1–T5.)

## Context

Specs 080–082 each added a config rule without checking what real workspaces contain.
- 082 made the criteria write commands refuse a config without a `coverage` section.
- The config that `spectra init` writes, the embedded template, has no `coverage` section. Its sections are source,
  tests, ai, generation, validation, dashboard, execution, bug_tracking, git, testimize and debug.
- So in every init-created workspace, `ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria` and
  `--import-criteria` exit 1 with `CONFIG_SECTION_MISSING`, and `compile-extraction-prompt` / `docs changed` warn on
  every run.
- The tests passed only because every fixture had a `coverage` section.

The 081 and 082 specs said that `spectra init` writes `coverage`. That was checked against the typed default config,
not against the template `init` actually writes.

This spec replaces the per-section rules of 081 C2 and 082 A1/A3 with one config rule that never requires a section.
It adds `coverage` to the init template. It rejects case-variant duplicate keys, and it sharpens when an angle-bracket
word counts as HTML. It also adds the guard tests that would have caught 082: they run against the real init template
and against a pre-existing workspace.

No new features. Every fix has a regression test that fails on `3594d62` and passes after the fix. Existing rules
still apply:
- human-readable summaries are never suppressed in JSON mode;
- failure output is one machine-readable JSON document on stderr;
- the CLI stays model-free and deterministic;
- nothing is written on `--dry-run`;
- git is the undo path.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Criteria commands work in a workspace created by `spectra init` (Priority: P1)

A user runs `spectra init` in a new repository and then extracts, retires and moves criteria. Every criteria and
compile command succeeds, with no config warnings, and criteria are written to the location the config names.

**Why this priority**: Today the whole criteria workflow is broken in every newly initialised workspace. This is the
regression 082 introduced.

**Independent Test**:
1. Run `spectra init` in an empty temporary directory, using the real embedded template.
2. Add a document.
3. Run `docs changed`, `compile-extraction-prompt`, `ingest-criteria` with a fixture response, `docs retire` and
   `docs apply-move`.
4. Check the exit codes, the warnings and where the criteria files were written.

**Acceptance Scenarios**:

1. **Given** a workspace created by `spectra init`, **When** the user runs `docs changed`, `compile-extraction-prompt`,
   `ingest-criteria`, `docs retire` and `docs apply-move`, **Then** every command exits as it would with a hand-written
   full config, and none of them prints a config warning.
2. **Given** a workspace created by `spectra init`, **When** the user opens `spectra.config.json`, **Then** it has a
   `coverage` section whose `criteria_dir` and `criteria_file` are `docs/criteria` and
   `docs/criteria/_criteria_index.yaml`.
3. **Given** a workspace created by `spectra init`, **When** criteria are ingested, **Then** they are written under
   `docs/criteria/`.

---

### User Story 2 - Existing workspaces keep working without editing their config (Priority: P1)

A user upgrades SPECTRA in a workspace whose `spectra.config.json` was written before this change, so it has no
`coverage` section. Every criteria and compile command works as before, with no warnings, and criteria stay in
`docs/criteria`. The user does not have to edit the config.

**Why this priority**: The same regression hits every existing workspace. The fix must not require a migration.

**Independent Test**: The real init template with its `coverage` section removed. Run the same commands as User
Story 1 and check the exit codes, the warnings and the criteria location.

**Acceptance Scenarios**:

1. **Given** a config with no `coverage` section, **When** any criteria write command runs, **Then** it succeeds and
   writes to `docs/criteria` and `docs/criteria/_criteria_index.yaml`, with no warning.
2. **Given** a config with `"coverage": null`, **When** any in-scope command runs, **Then** it behaves exactly as with
   `coverage` missing.
3. **Given** a config with no `coverage` section, **When** `compile-extraction-prompt` or `docs changed` runs,
   **Then** no config warning is printed.

---

### User Story 3 - One predictable config rule, with warnings only where they help (Priority: P1)

A user or a CI job gets one predictable rule for every criteria and compile command:
- no section is ever required;
- a missing config file is an error only for commands that write;
- a config that can't be read is always an error;
- a command warns only when a section it actually reads (`source` or `tests`) is missing, and the warning never
  changes the exit code.

**Why this priority**: Three rounds of section rules each broke something. One table-driven rule with a test for every
row stops the drift.

**Independent Test**: For every row of the rule table, and for one read command and one write command, check the exit
code, the `error_code`, the exact number of warnings and, for write commands, where criteria were written.

**Acceptance Scenarios**:

1. **Given** no `spectra.config.json`, **When** a read command runs, **Then** it uses the built-in defaults with no
   warning. **When** a write command runs, **Then** it exits 1 with `CONFIG_MISSING`.
2. **Given** a config that is not valid JSON, is not an object, or can't be read, **When** any in-scope command runs,
   **Then** it exits 1 with `CONFIG_INVALID`.
3. **Given** a config without `tests` (or with `"tests": null`), **When** a command that reads `tests` runs
   (`ingest-criteria`, `docs retire`, the critic/repair compilers), **Then** it uses the default tests location and
   reports exactly one warning naming `tests`: in `warnings` in JSON mode, as a `Warning:` line in human mode. The
   exit code is the same as without the warning.
4. **Given** a config without `source`, **When** a command that reads `source` runs (`docs changed`, the critic/repair
   compilers), **Then** it uses the default source location and reports exactly one warning naming `source`.
5. **Given** a config missing any section the command doesn't read, **When** it runs, **Then** nothing is reported.
6. **Given** the criteria skill, **When** it describes config problems, **Then** it names `CONFIG_MISSING` and
   `CONFIG_INVALID` only.

---

### User Story 4 - Case-variant duplicate keys are rejected (Priority: P2)

A user's config contains two top-level keys that differ only in case, for example `"coverage"` and `"Coverage"`
(perhaps one set to `null`). Every in-scope command refuses the config and names the key, instead of silently using
one of them.

**Why this priority**: Today the deserializer silently keeps the last one, which can be `null` and crash later. It is
rare but confusing, and easy to rule out.

**Independent Test**: Configs with a case-variant duplicate of a section the loader checks (`tests`) and of another
top-level key (`generation`). Run one read and one write command.

**Acceptance Scenarios**:

1. **Given** a config with `"tests": {…}` and `"Tests": null`, **When** any in-scope command runs, **Then** it exits 1
   with `CONFIG_INVALID` and a message naming `tests`.
2. **Given** a config with `"generation": {…}` and `"Generation": {…}`, **When** any in-scope command runs, **Then**
   it exits 1 with `CONFIG_INVALID` and a message naming `generation`.

---

### User Story 5 - A real wording change is never hidden by the text comparison (Priority: P2)

A documentation author writes angle-bracket placeholders (`<source>`, `<Summary>`, `<LIMIT>`) and real HTML
(`<details open>`, `<p>…</p>`, `<br>`) in requirement text. When criteria are compared after an edit, a change inside
a placeholder is always reported. Genuine HTML markup is ignored when it is written in an unmistakable HTML form.

**Why this priority**: A missed change leaves a criterion saying something the doc no longer says. A spurious
"modified" costs only a review. The rule must therefore lean towards "text".

**Independent Test**: A table of inputs covering every form listed in FR-012 and every existing comparison case from
080 B9, 081 C3 and 082 A2, with the expected comparison key for each.

**Acceptance Scenarios**:

1. **Given** text containing a known element name in angle brackets, **When** it is normalised, **Then** the span is
   markup only in one of the five forms in FR-012. Otherwise it is kept as text.
2. **Given** `Select <source> now` changing to `Select <target> now`, or `Enter <summary>` to `Enter <details>`,
   **When** compared, **Then** the change is reported.
3. **Given** `<Summary>` in text that also contains `</summary>`, **When** normalised, **Then** `<Summary>` is text:
   its closing tag is spelled differently.
4. **Given** every existing comparison case from specs 080, 081 and 082, **When** normalised, **Then** each keeps its
   current result. All 71 existing inputs were checked against the new rule and none changes.

---

### Edge Cases

- **A config with `"coverage": {}`**: present; built-in defaults, no warning.
- **A config with neither `source` nor `tests`**: a command that reads both reports two warnings, one per section. A
  command that reads one of them reports one.
- **A config with `"ai": null`, or any unread section set to `null`**: ignored silently.
- **A duplicate key that differs only in case, deep inside a section** (not top-level): out of scope. Only top-level
  keys are checked.
- **A key duplicated with exactly the same spelling** (`{"a":1,"a":2}`): the JSON parser rejects it. That rejection is
  reported as `CONFIG_INVALID`. Today it escapes as an unhandled exception and crashes the command (research R2).
- **A self-closing bare tag in capitals (`<BR/>`)**: markup, because the self-closing slash is HTML form. A bare `<BR>`
  without the slash is text.
- **Accepted residual (documented)**: a lowercase placeholder whose exact lowercase closing tag also appears later in
  the same text (`Enter <summary> … </summary>`) is treated as markup.
- **`--dry-run` on a write command in a workspace without `coverage`**: runs like a real run, writes nothing, and
  reports no warning.

## Requirements *(mandatory)*

### Functional Requirements

**The config rule (replaces 081 C2 and 082 A1/A3)**

- **FR-001**: Every in-scope command MUST follow this table:

  | Situation | Read commands | Write commands |
  |---|---|---|
  | No `spectra.config.json` | built-in defaults, no warning | exit 1, `CONFIG_MISSING` |
  | Not valid JSON, not an object, or unreadable | exit 1, `CONFIG_INVALID` | exit 1, `CONFIG_INVALID` |
  | Two top-level keys that differ only in case | exit 1, `CONFIG_INVALID` naming the key | exit 1, `CONFIG_INVALID` naming the key |
  | `source` or `tests` missing or `null`, and the command reads it | defaults + one warning naming the section | defaults + one warning naming the section |
  | `coverage` missing or `null` | defaults, no warning | defaults, no warning |
  | Any section the command doesn't read, missing or `null` | ignored silently | ignored silently |

- **FR-002**: No config section is ever required. For write commands, the protection against writing into the wrong
  folder is the required config file.
- **FR-003**: In-scope commands and the sections they read:

  | Command | Kind | Reads |
  |---|---|---|
  | `ingest-criteria` | write | `tests` |
  | `docs retire` | write | `tests` |
  | `docs apply-move` | write | none |
  | `docs migrate-criteria` | write | none |
  | `--import-criteria` | write | none |
  | `compile-extraction-prompt` | read | none |
  | `compile-critic-prompt` | read | `source`, `tests` |
  | `compile-repair-prompt` | read | `source`, `tests` |
  | `compile-repair-batch` | read | `source`, `tests` |
  | `docs changed` | read | `source` |

  Only these declared reads can produce a warning.
- **FR-004**: A warning MUST appear in the command's output: in the `warnings` field in JSON mode where the command
  has one, and as a `Warning:` line in human mode. It MUST NOT change the exit code. In JSON mode, a failure is still
  exactly one JSON document on stderr.
- **FR-005**: Everything made dead by FR-001–FR-003 MUST be removed, with no compatibility shim:
  - the "required section" concept;
  - the `CONFIG_SECTION_MISSING` code;
  - the `coverage` entry in the declared-sections vocabulary;
  - every mention of them in the `spectra-criteria` skill and the tests.

  `CONFIG_MISSING` and `CONFIG_INVALID` stay.

**F1 — Init template**

- **FR-006**: `spectra init` MUST write a `coverage` section with `criteria_dir` `docs/criteria` and `criteria_file`
  `docs/criteria/_criteria_index.yaml` (the current built-in defaults).
- **FR-007**: Existing workspaces MUST NOT be rewritten. Under FR-001 they need no change.

**F3 — Case-variant duplicate keys**

- **FR-008**: A config with two or more top-level keys that are equal ignoring case MUST be rejected with
  `CONFIG_INVALID` and a message naming the key, by every in-scope command. The deserializer must never silently keep
  one of them.

**F4 — When an angle-bracket word counts as HTML**

- **FR-009**: The comparison key MUST never hide a real wording change. A spurious "modified" is acceptable; a missed
  change is not.
- **FR-010**: The known element and boolean-attribute lists are unchanged from 081/082.
- **FR-011**: Stripping HTML comments is unchanged.
- **FR-012**: A known element name in angle brackets counts as markup only in one of these forms:
  1. a closing tag `</name>`, any case;
  2. an opening or self-closing tag with at least one well-formed attribute (valued, or a known bare boolean
     attribute), any case: `<details open>`, `<Details open>`, `<source srcset="a.webp">`, `<col span=2>`;
  3. a self-closing bare tag `<name/>` or `<name />`: `<br/>`, `<hr />`;
  4. the void elements `<br>`, `<hr>`, `<wbr>` written in lowercase;
  5. a bare opening tag with a closing tag of exactly the same spelling (case-sensitive) later in the same text:
     `<p>x</p>`, `<P>x</P>`, `<details><summary>x</summary></details>`.

  Everything else in angle brackets is text. That includes:
  - lowercase placeholders with no matching close: `<source>`, `<input>`, `<summary>`, `<section>`;
  - capitalised placeholders: `<Summary>`, `<Source>`;
  - all-caps: `<LIMIT>`, `<BR>`;
  - `<Summary>` when the text contains only `</summary>`.
- **FR-013**: The accepted residual MUST be documented in the normaliser and in `docs/cli-reference.md`: a lowercase
  placeholder whose exact lowercase closing tag also appears later in the same text is treated as markup.

**Tests (written as tables so the rule and the tests can't drift)**

- **FR-014 (T1)**: A config-matrix test covering every row of the FR-001 table with one read and one write command.
  It checks:
  - the exit code;
  - the `error_code`;
  - the exact number of warnings;
  - for write commands with `coverage` missing or `null`, that criteria were written to the default location.
- **FR-015 (T2)**: An end-to-end test that runs `spectra init` in a temporary directory with the real embedded
  template, then runs `docs changed`, `compile-extraction-prompt`, `ingest-criteria` (fixture response), `docs retire`
  and `docs apply-move`. All succeed with zero config warnings.
- **FR-016 (T3)**: The same run against the real init template with its `coverage` section removed, which is what
  existing workspaces have. All succeed, with zero config warnings and criteria in `docs/criteria`.
- **FR-017 (T4)**: A normaliser decision table covering every example in FR-012 (both lists) and every existing
  comparison case from 080 B9, 081 C3 and 082 A2. Cases whose result changes are listed under **Normaliser changes**
  below with the old and new result; all others keep theirs.
- **FR-018 (T5)**: Case-variant duplicate keys, for a checked section (`tests`) and for another top-level key
  (`generation`).
- **FR-019**: Every fix MUST have a regression test that fails on `3594d62` and passes after. The 082 tests that
  assert the removed behaviour are replaced, not kept:
  - `CONFIG_SECTION_MISSING`;
  - writes refused without `coverage`;
  - warnings for a missing `coverage`.
- **FR-020**: `CHANGELOG.md`, the `CLAUDE.md` "Recent Changes" entry and `docs/cli-reference.md` MUST be updated: the
  config rule table, the removed `CONFIG_SECTION_MISSING`, and the init template's new `coverage` section.
- **FR-021**: Any future config requirement MUST be checked against the embedded init template and a pre-existing
  workspace. T2 and T3 are that guard, and `docs/cli-reference.md` and the loader documentation say so.

### Normaliser changes

The 71 distinct angle-bracket inputs in the current test suites (080 B9, 081 C3, 082 A2, criteria matcher and
changeset tests) were compared under the 082 rule and the FR-012 rule. **None changes its result.** The inputs that
change are only the new FR-012 examples:

| Input | 082 result | FR-012 result | Reason |
|---|---|---|---|
| `send <source> now` | `send now` | `send <source> now` | lowercase bare placeholder with no matching close is text |
| `<input> field` | `field` | `<input> field` | same |
| `the <summary> text` | `the text` | `the <summary> text` | same |
| `the <section> box` | `the box` | `the <section> box` | same |
| `<Summary> and </summary>` | `and` | `<summary> and` | closing tag spelled differently, so `<Summary>` is text; `</summary>` is still a closing tag |
| `a<BR/>b` | `a<br/>b` | `a b` | a self-closing bare tag is HTML form in any case (Assumptions); found while writing T4 |

### Key Entities

- **Config rule table**: the single table in FR-001. It maps each config situation to the outcome for read and write
  commands.
- **Declared reads**: the sections each in-scope command reads (FR-003). Only these can warn.
- **Init template**: the config `spectra init` writes. It is the reference that every config rule is checked against.
- **Comparison key**: the normalised criterion text. FR-012 decides which angle-bracket spans it drops.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a workspace created by `spectra init`, 5 of 5 of the listed criteria and compile commands succeed,
  with 0 config warnings.
- **SC-002**: In a workspace whose config has no `coverage` section, 5 of 5 criteria write commands succeed, with 0
  warnings, and 100% of criteria files land under `docs/criteria`.
- **SC-003**: Every row of the config rule table is covered by at least one read-command and one write-command test,
  and 0 rows are untested.
- **SC-004**: A config with case-variant duplicate top-level keys is rejected by 10 of 10 in-scope commands with
  `CONFIG_INVALID` naming the key.
- **SC-005**: 0 of the 71 existing comparison cases change result, and 100% of the FR-012 examples produce the listed
  result.
- **SC-006**: Every fix has a test that fails on `3594d62` and passes after. The existing suites stay green apart from
  the known `ProfileFormatLoaderTests` failure.

## Assumptions

- **The 5 commands in SC-001** are `docs changed`, `compile-extraction-prompt`, `ingest-criteria`, `docs retire` and
  `docs apply-move`, as T2 requires. The other in-scope commands are covered by the T1 matrix.
- **Self-closing bare tags (FR-012, form 3) count as markup in any case.** The slash makes the HTML form unambiguous,
  and FR-012 names no case restriction for this form.
- **A `null` section is still handled internally by filling it with an empty object**, so that a `"coverage": null`
  can't crash. What changes is only which sections warn (FR-001).
- **The 082 JSON failure documents stay**: `compile-critic-prompt` and `docs changed` in JSON mode, and
  `ErrorResult.error_code` for `--import-criteria`. Only the `CONFIG_SECTION_MISSING` code goes.
- **`spectra init` in T2** runs non-interactively in the temporary directory, with no network use.

## Out of Scope

- Phase 2+ of the doc-change workflow (change detection, triage, relinking).
- The `DeleteHandler` `depends_on` regex bug (tracked separately).
- The deferred 078 finding about the parsing cost of test files.
- Config handling of commands outside the in-scope list (`ai update`, `docs index`, other `ai analyze` modes).
- Duplicate keys inside sections.
