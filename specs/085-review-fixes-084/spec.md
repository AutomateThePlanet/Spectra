# Feature Specification: Review Fixes for Spec 084 — Source Change Baseline

**Feature Branch**: `085-review-fixes-084`
**Created**: 2026-10-05
**Status**: Draft
**Input**: User description: "Review fixes for spec 084 (source-change baseline, Phase 2a). No new features. Every fix
needs a regression test that fails on 2a43298 and passes after. The baseline file format does not change (no
migration); every baseline written by 084 stays valid." The full text is in the triggering request: F1–F6, T1–T9, the
rules, out of scope and acceptance. The decisions are settled and are not reopened here.

## Context

A review of 084 found six problems. Each one either reports the wrong state for a test or changes something it
shouldn't:

| # | Problem in 084 | Effect |
|---|---|---|
| F1 | Anchors are compared with a heading's **raw** text | Headings with an HTML anchor, an `{#id}` block or a link, and GitHub's `-1` duplicate anchors, never match. A ref that never resolved is left out of the baseline, so the test is partial (unbaselined) for ever, and `source-baseline --init` can't fix it. A ref to a plain heading that later gains such markup turns REMOVED |
| F2 | Every baseline save drops entries for tests it didn't parse | One unparseable test file silently loses that test's baseline |
| F3 | A full rebuild leaves out refs that don't resolve now | After any edit, a ref to a deleted section stops showing as REMOVED |
| F4 | Only seven modal words make a change LOAD | "can delete" → "can't delete", or adding "except gift cards", is LIGHT |
| F5 | Every `ingest-update` refreshes the baseline | A critic repair silently accepts doc changes the test was never updated for |
| F6 | The UTF-8 fix sets the console encoding | It also switches the user's terminal window code page, and that persists after the command exits |

No new features. Each fix has a regression test that fails on `2a43298` and passes after the fix. The baseline file
format is unchanged, and every baseline 084 wrote stays valid.

Rules that still apply:
- human-readable tables and summaries are never suppressed in JSON mode;
- failure output is one machine-readable JSON document on stderr (shared shape, named codes);
- the CLI stays model-free and deterministic;
- nothing is written on `--dry-run`;
- git is the undo path.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tests that point at headings with markup or explicit IDs are triaged, not reported as removed (Priority: P1)

Docs written in GitBook or with explicit heading IDs put markup in the heading line, for example
`## Create customer records <a href="#create-customer-records" id="create-customer-records"></a>` or
`## Title {#custom-id}`. Tests generated from those docs reference the anchor. Today:
- such a ref never resolves, so it is never baselined, and the test stays partial for ever (`source-baseline --init`
  can't add it);
- a ref to a plain heading that later gains such markup turns REMOVED, which buries real removals.

After this fix, the anchor finds its heading and the section is triaged normally. Adding an anchor tag, a link or a
custom ID to an existing heading is COSMETIC.

**Why this priority**: False REMOVED reports make triage useless on exactly the doc sources the feature targets.

**Independent Test**: For each heading form, a test referencing its anchor resolves to the right section, and the "no
test covers" list agrees and shows the cleaned heading text.

**Acceptance Scenarios**:

1. **Given** the heading `## Create customer records <a href="#create-customer-records" id="create-customer-records"></a>`, **When** a test references `#create-customer-records`, **Then** the section is found.
2. **Given** `## Title {#custom-id}`, **When** a test references `#custom-id` or `#title`, **Then** the section is found.
3. **Given** `## [Payments](payments.md)`, **When** a test references `#payments`, **Then** the section is found.
4. **Given** two `## Setup` headings, **When** a test references `#setup-1`, **Then** it gets the second one; `#setup` gets the first.
5. **Given** a heading whose explicit ID differs from its text (`## Overview {#intro}`), **When** a test references `#intro`, **Then** that heading is found, unless another heading's text key is `intro`, in which case the text match wins.
6. **Given** a ref whose anchor matches nothing by any rule, **Then** it is REMOVED (`anchor not found`), as before.
7. **Given** plain, emphasised and inline-code headings that 084 already resolved, **Then** every such ref resolves to the same section with the same fingerprint as under 084.
8. **Given** a changed document whose unreferenced heading carries HTML, **When** triage lists it under `no_tests`, **Then** the cleaned heading text is shown, not the raw HTML.
9. **Given** a ref baselined on a plain heading, **When** the heading gains an `<a … id>` anchor, a link or a trailing `{#id}`, **Then** the ref is COSMETIC (auto-acked), not REMOVED.

---

### User Story 2 - Baselines are never lost or loosened by a rebuild or an unparseable file (Priority: P1)

A test file that is temporarily malformed must not lose its baseline entry on the next save by any command. A rebuild
of a test's entry must keep the evidence that a section or criterion it still references is gone.

**Why this priority**: These silently destroy the record that change detection depends on. The loss is invisible
until a real change is missed.

**Independent Test**:
1. Make one test file unparseable and run each writer; that test's entry survives, and a warning names the file.
2. Rebuild a test with a REMOVED ref; it is still REMOVED in the next triage.

**Acceptance Scenarios**:

1. **Given** a suite with one unparseable test file, **When** `ingest-tests`, `ingest-update` (single and `--all`), `update-triage` (auto-ack), `update-ack` or `source-baseline --init` saves that suite's baseline, **Then** the unparseable test's entry is still there, and the command's warnings name the file and say that nothing was pruned.
2. **Given** every test file in the suite parses and one test was deleted, **When** a writer saves, **Then** the deleted test's entry is pruned, as in 084.
3. **Given** a test with a REMOVED ref and a CRITERIA_REMOVED criterion, **When** its entry is rebuilt by `ingest-update` or `source-baseline --init --force`, **Then** the next triage still reports REMOVED and CRITERIA_REMOVED.
4. **Given** that test, **When** the ref or criterion is dropped from the test, **Then** its key leaves the entry on the next rebuild, and the test is no longer REMOVED.

---

### User Story 3 - A change that flips a rule's meaning is load-bearing (Priority: P2)

"Users can delete" → "Users can't delete", "all items" → "all items except gift cards", and "always" ↔ "never" each
reverse or narrow a rule. They must land in LOAD (edit recommended), not LIGHT.

**Why this priority**: These are small word changes with large behavioural effect; LIGHT is skipped by the
recommended "LOAD only" option.

**Independent Test**: The state table gives LOAD for each meaning-flip row, and LIGHT for a one-word change outside
the word list.

**Acceptance Scenarios**:

1. **Given** "can delete" → "can't delete", with a straight apostrophe or with a curly one (’), **Then** LOAD.
2. **Given** "all items" → "all items except gift cards", **Then** LOAD.
3. **Given** "always" → "never", and "never" → "always", **Then** LOAD.
4. **Given** a one-word change of an ordinary word ("button" → "link"), **Then** LIGHT, as in 084.

---

### User Story 4 - A critic repair does not accept doc changes (Priority: P2)

During generation and flagged-test review, the critic's findings are repaired through `ingest-update`. A repair fixes
grounding, not doc drift, so it must leave the test's baseline where it was. A doc change still pending for that test
must stay pending.

**Why this priority**: Without it, any repaired test silently drops out of triage even though nobody reconciled it
with the changed doc.

**Independent Test**:
1. A test with a pending LOAD change is repaired through `ingest-update --all --keep-baseline`; the change is still
   LOAD in the next triage.
2. A ref the patch added gets an entry.
3. The same holds through the single-test path.

**Acceptance Scenarios**:

1. **Given** a test whose section changed (LOAD pending), **When** a repair is ingested with `--keep-baseline`, **Then** its existing fingerprints are unchanged and the next triage still reports LOAD.
2. **Given** a repair that adds a ref or a criterion, **When** it is ingested with `--keep-baseline`, **Then** the new ref or criterion gets an entry.
3. **Given** the same edit ingested without `--keep-baseline`, **Then** the behaviour is 084's, plus the F3 rule.
4. **Given** the installed skills, **Then** the `spectra-generate` repair step and `spectra-review-flagged` pass `--keep-baseline`, and `spectra-update` does not.

---

### User Story 5 - Running a command never changes the user's terminal (Priority: P2)

084 made output UTF-8 by setting the console encoding, which also switches the terminal window's code page for the
rest of the session. Output must be UTF-8 when it is redirected (how the skills read it), and the user's terminal must
be left as it was.

**Why this priority**: It's an unrequested side effect on the user's environment that persists after the command.

**Independent Test**:
1. Redirected stdout and stderr are UTF-8 without a BOM (the child-process test still passes).
2. Program startup no longer assigns the console encoding.

**Acceptance Scenarios**:

1. **Given** stdout or stderr is redirected, **When** a command prints `→` or `Ü`, **Then** the bytes are UTF-8 without a BOM.
2. **Given** the output goes to an interactive console, **Then** it behaves as before 084, and the terminal's code page is not changed.

---

### Edge Cases

- **A heading with an HTML entity** (`## Q&amp;A`): its text key is taken after normalisation (`qa`), so `#qa` now
  matches. An anchor written from the raw entity text (`#qampa`) no longer does. This is the only kind of ref whose
  resolution can change, and it is outside the F1 guarantee (see Assumptions).
- **Existing snapshots of sections whose headings carry `{#…}`** (whole-file or parent-section refs) still contain
  the block, so the first raw change to such a section compares the old snapshot with the new comparison text, which
  lacks it. The block's ID can then show as a changed word once. The re-baseline after that edit stores the new form.
- **Several explicit IDs in one heading**: any of them matches (rule 2).
- **A trailing `{#…}` block that is not an ID** (`{.class}`): it is removed from the text key; it adds no ID.
- **`#setup-0`**, a suffix with a non-numeric tail (`#setup-a`), or `-n` with fewer than n+1 matching headings: rule 3
  does not apply, and the ref is REMOVED unless rule 1 or 2 matched.
- **A heading whose text itself ends in `-2`** (`## Step-2`): rule 1 matches it first, so rule 3 never overrides a
  real text match.
- **A test file that becomes parseable again**: its entry was kept, so it is triaged normally.
- **A suite where every file fails to parse**: nothing is pruned; each file is named in the warnings.
- **`--keep-baseline` on a new test** (no entry): it creates the entry from current content, the same as
  `source-baseline --init`.
- **`--keep-baseline` with a ref the edit removed**: the stale key stays (add-only mode); it is ignored by triage,
  and dropped by the next full rebuild (084 FR-026a).
- **A dead ref that never had a fingerprint** (unbaselined): it stays unbaselined on rebuild; F3 only keeps an
  existing fingerprint.
- **Curly apostrophes in the word diff itself**: unchanged. Only the LOAD word check folds them, so the shown diff
  keeps the document's characters.

## Requirements *(mandatory)*

### Functional Requirements

**F1 — Anchor matching**

- **FR-001**: A heading's **text key** is the anchor key (084: lowercase, letters and digits only) of the heading
  text, after:
  - removing a trailing `{#…}` / `{…}` attribute block;
  - then applying the comparison-text normalisation, which removes HTML and links.
- **FR-001a**: A section's **comparison text** (the normalised text compared with the snapshot) is taken after
  removing a trailing `{#…}` / `{.…}` attribute block from every heading line in the section outside fenced code.
  So adding, changing or removing a custom ID is never a doc change. The fingerprint (over raw text) is unchanged.
  Anchor tags and links in a heading need no extra rule, because the normalisation already strips them.
- **FR-002**: A heading's **explicit IDs** are the values of every `id="…"` and `id='…'` inside the heading line, and
  the ID of a trailing `{#id}` block, each turned into an anchor key.
- **FR-003**: A ref's anchor resolves to a heading by the first rule that gives a match:
  1. the first heading whose text key equals the anchor's key;
  2. the first heading with an explicit ID whose key equals the anchor's key;
  3. if the anchor, as written, ends in `-<n>` with n ≥ 1, and at least n+1 headings' text keys equal the key of the
     anchor without that suffix: the (n+1)th of those headings, in document order.
  No match → REMOVED, reason `anchor not found`, as in 084. The 084 ambiguity note stays for rule 1 (several headings
  share the text key).
- **FR-004**: Section extraction, the headings a ref covers (for `no_tests`) and every other heading lookup use one
  matching function, so they always agree.
- **FR-005**: The `no_tests` list shows each heading's cleaned text: the normalised text with the attribute block
  removed, in the document's case. It never shows raw HTML or `{#…}`.
- **FR-006**: Guarantee: a ref that resolved under 084 to a plain, emphasised (`**…**`, `*…*`, `_…_`) or inline-code
  heading resolves to the same section under 085, so its fingerprint is unchanged.
  - A ref that resolves only under 085 has no fingerprint yet. It shows as partial (unbaselined ref) and
    `source-baseline --init` (without `--force`) adds it.

**F2 — Pruning**

- **FR-007**: A baseline save prunes entries for tests that no longer exist **only when every test file in the suite
  parsed**. Test files are `*.md` files whose names don't start with `_`. If any file failed to parse, nothing is
  pruned in that suite on that save.
- **FR-008**: When pruning is skipped, the command's warnings name each unparseable file and say that nothing was
  pruned in that suite. They appear in the JSON `warnings`, and in human mode as `Warning:` lines on stderr, as config
  warnings already are. This holds for all five writers.
- **FR-009**: FR-007/FR-008 are implemented once, in the baseline store's save, and apply to every writer:
  `ingest-tests`, `ingest-update` (single and `--all`), `update-triage` (auto-ack), `update-ack`, and
  `source-baseline --init`.

**F3 — Rebuilds keep dead refs**

- **FR-010**: When a test's entry is rebuilt from current content (a full recompute: `ingest-update` without
  `--keep-baseline`, `source-baseline --init --force`, `update-ack`):
  - a ref the test still lists that doesn't resolve now keeps its previous fingerprint, if the previous entry had
    one;
  - a criterion the test still links that no longer exists keeps its previous fingerprint, if the previous entry had
    one.
  Keys the test no longer lists are still dropped (084 FR-026a).
- **FR-011**: As a result, such refs and criteria keep reporting REMOVED / CRITERIA_REMOVED until the test drops or
  fixes them. Tests with no previous entry are unaffected. `update-ack` keeps refusing tests with REMOVED refs or
  criteria (084 FR-024).

**F4 — Meaning flips**

- **FR-012**: A removed or added word makes a change LOAD if it is in this list, the 084 list extended:
  `must`, `should`, `may`, `required`, `no`, `not`, `never`, `none`, `nothing`, `cannot`, `can't`, `don't`,
  `doesn't`, `won't`, `isn't`, `aren't`, `shouldn't`, `mustn't`, `without`, `unless`, `except`, `only`, `always`,
  `all`, `any`, `every`.
- **FR-013**: For this check only, a curly apostrophe (`’` or `‘`) in a word is compared as a straight one (`'`).
  The word diff and its shown runs are unchanged.
- **FR-014**: `why` keeps its `modal: {removed}→{added}` form for these words.

**F5 — Repairs keep the baseline**

- **FR-015**: `spectra ai ingest-update` gains `--keep-baseline`, for the single form and `--all`. With it, the
  edited test's existing fingerprints are never changed. Refs and criteria the edit added get entries from current
  content, using the same add-missing-keys-only mode as `source-baseline --init` without `--force`. A test with no
  entry gets one.
- **FR-016**: Without `--keep-baseline`, `ingest-update` behaves as in 084, plus FR-010.
- **FR-017**: `--keep-baseline` is passed by the repair callers: the `spectra-generate` repair step
  (`ingest-update {suite} --all --keep-baseline`) and `spectra-review-flagged`'s repair ingest. `spectra-update`
  (the doc-update flow) does not pass it. Skill-content tests assert all three.

**F6 — Console encoding**

- **FR-018**: Program startup no longer assigns the console's output encoding.
- **FR-019**: When stdout is redirected, its writer is replaced by a UTF-8 writer without a BOM; the same applies
  independently to stderr. A stream that is not redirected is left exactly as the runtime provides it.
- **FR-020**: The 084 child-process test (redirected stdout, `→` and `Ü` intact, no BOM) keeps passing, and the same
  is shown for stderr.

**Compatibility**

- **FR-021**: The baseline file format and its serialisation are unchanged. Every `_source-baseline.json` written by
  084 loads, and re-saving it unchanged is byte-identical.
- **FR-022**: On a workspace whose docs and tests haven't changed since 084 baselined it, triage gives the same
  result as under 084, except in these cases, each of which fails on `2a43298`:
  - **never resolved under 084:** a ref to an `<a … id>`, link or `{#id}` heading was left out of the baseline, so the
    test is partial, and `source-baseline --init` can't add it. Under 085 the ref resolves: still partial before
    `--init`, and `--init` (without `--force`) adds it, after which it is UNCHANGED;
  - **plain heading that later gained markup:** a ref baselined on a plain heading that then gained an `<a … id>`
    anchor, a link or a trailing `{#id}` is REMOVED under 084. Under 085 it resolves again and is COSMETIC
    (auto-acked): the anchor tag and the link are stripped by normalisation, and the `{#id}` block by FR-001a.

### Required Tests

| ID | Covers | Content |
|---|---|---|
| T1 | FR-001–FR-003 | Anchor-matching table. Each heading form × its anchor resolves to the right section: GitBook `<a … id="…">`, `{#id}` (by ID and by text), a link in the heading, GitHub `-1`/`-2` duplicates, and an explicit ID that differs from the text. Also: plain headings unchanged; rule order (a text-key match on one heading beats an explicit ID on another); the rule-3 edge cases (`-0`, too few duplicates, a text that ends in `-2`); no match → REMOVED |
| T2 | FR-006 | Guarantee: on a fixture with plain, emphasised and inline-code headings, every ref's section and fingerprint equal what the 084 rule produces (both computed from the same input) |
| T3 | FR-004, FR-005 | For every T1 case, covered headings and section extraction agree; `no_tests` shows cleaned heading text |
| T4 | FR-007–FR-009 | A suite with one unparseable test file: after each of the five writers saves, that test's entry is still there and a warning names the file. With every file parseable, a deleted test's entry is pruned |
| T5 | FR-010, FR-011 | A test with a REMOVED ref and a removed criterion, rebuilt via `ingest-update` and via `source-baseline --init --force`, is still REMOVED / CRITERIA_REMOVED in the next triage. After the test drops the ref, its key is gone and the test is no longer REMOVED |
| T6 | FR-012–FR-014 | State-table rows: "can delete" → "can't delete" (straight and curly), "all items" → "all items except gift cards", "always" ↔ "never" → LOAD; a one-word ordinary change stays LIGHT |
| T7 | FR-015–FR-017 | A pending LOAD change, repaired via `ingest-update --all --keep-baseline`, is still LOAD in the next triage, and a ref the patch added gets an entry. The same via the single-test path. Skill-content assertions |
| T8 | FR-018–FR-020 | Startup no longer assigns the console encoding; redirected stdout and stderr are UTF-8 without a BOM |
| T9 | FR-001a, FR-021, FR-022 | A baseline written by 084 loads unchanged and re-saves byte-identically. Triage of an unchanged workspace equals the 084 output, captured by running the same fixture on the `2a43298` build. The FR-022 cases: never-resolved refs (partial, then UNCHANGED after `--init`); a plain heading that gained an `<a id>`, a link and a `{#id}` (REMOVED on 084, COSMETIC on 085) |

### Key Entities

- **Heading match**: for a heading, its text key, its explicit IDs and its cleaned display text. For a ref, which rule
  matched and which heading it found.
- **Prune permission**: per suite and per save, whether every test file parsed. If not, the list of files that
  didn't.
- **Rebuild mode**: *full* (recompute, keeping dead refs' previous fingerprints) or *keep-baseline* (add missing keys
  only).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the T1 heading forms resolve to the right section; 0 of them are REMOVED.
- **SC-002**: 0 fingerprints change for refs to plain, emphasised or inline-code headings (T2, T9).
- **SC-003**: An unparseable test file keeps its entry under every writer: 5 of 5 (T4).
- **SC-004**: A REMOVED ref or criterion stays reported after a rebuild in both rebuild paths: 2 of 2 (T5).
- **SC-005**: Every meaning-flip row in T6 is LOAD, and the ordinary one-word change stays LIGHT.
- **SC-006**: A pending change survives a repair in both ingest paths: 2 of 2 (T7).
- **SC-007**: Running any command leaves the terminal's code page unchanged, and redirected output is UTF-8 (T8).
- **SC-008**: Existing suites stay green, except the known `ProfileFormatLoaderTests` failure. `CHANGELOG.md`,
  `CLAUDE.md` "Recent Changes", `docs/cli-reference.md` (`--keep-baseline`, the anchor rules) and `docs/usage.md` are
  updated.

## Assumptions

- **The F1 guarantee covers** plain, emphasised and inline-code headings, which is what generators and authors write.
  A heading containing an HTML entity or a tag that an 084 ref matched through its raw text (for example `#qampa` for
  `Q&amp;A`) is the one case whose resolution can change. Such refs are not produced by any slug style the spec
  supports, so the case is treated as a documented residual, not a migration.
- **Cleaned heading text keeps the document's case**, so `no_tests` reads like the doc.
- **`update-ack` rebuilds with FR-010.** Its REMOVED refusal means a dead ref never reaches the rebuild, but the rule
  is shared, so it's covered for consistency.
- **The extended word list replaces the 084 modal list as a whole.** `cannot`, `only` and `not` were already in it.
  `can't` and the other contractions are matched after the apostrophe folding.
- **Redirect detection** uses the runtime's per-stream "is redirected" signal. A stream redirected to a file and one
  piped to another process are treated the same.

## Out of Scope

- Phase 2b (git, rename detection, relinking, baseline from history).
- Phase 3 (section text and diff in the update prompt).
- The CS0108 / CS8714 build warnings from 079.
- The `spectra show` bare-filename issue (tracked separately).
- Any change to the baseline file format.
