# Feature Specification: Correctness Fixes (Phase 6, Part 3)

**Feature Branch**: `098-correctness-fixes`
**Created**: 2026-10-14
**Status**: Draft
**Input**: User description: "098-correctness-fixes: Phase 6, part 3 — the deferred correctness findings from 085–097.
No new features. Each fix needs a regression test that fails on 986da01. Performance and code cleanup (085 D5/D6, the
078 parse cost, 089's triple parse, 095's dead code, the 4 build warnings) are 099; docs and the constitution are 100."
(Background, F1–F9, constraints and scope as quoted in the request.)

## Context

Specs 085–097 each left correctness findings that were deferred so the phases could continue. Each one makes SPECTRA
say or do something that isn't true for a real workspace: a config that every command rejects, an audit that tells a
skill to repair a test that only needs its block written, a count that lists the same file twice, a manifest that
forgets which content SPECTRA wrote, a baseline recorded at the wrong moment, a heading anchor that doesn't match, a
prompt that tells the model a criterion changed for a test where it didn't, a refusal where a warning is enough, and
one config key doing two jobs.

Facts (verified on `986da01`):
- **Legacy critic block (097 review, top priority).** 097 made `ai.critic.model` live again (rendered into the critic
  agent by `init` / `update-skills`). Workspaces created before 069 still carry the old block:
  - The v1.45 init template wrote `"critic": {"enabled": true, "provider": "github-models", "model": "gpt-5-mini"}`;
    the 058–068 template wrote `{"enabled": true, "model": "claude-sonnet-4-6", "max_concurrent": 1, …}`.
  - `update-skills` installs `model: gpt-5-mini` from the v1.45 block — a GitHub Models id, meaningless to Claude Code.
  - A GitHub Models value with `/` (`openai/gpt-4.1`) fails 097's model pattern → `CONFIG_INVALID` for **every**
    command that loads the config. A non-string value fails the same way.
  - The retired-key notice (shown by `spectra validate`) lists only `provider`, `api_key_env`, `base_url` (and
    `ai.fallback_strategy`), and its text says `ai.critic.model` is the critic selector — true only for a new block.
- **Audit actions.** `audit-grounding` sets `action_needed` = stale → `critic`; block written → `review` / `none`;
  otherwise `repair`. So a test with a fresh `grounded` verdict whose block isn't written yet, and a `hallucinated` test
  the update flow keeps for the user, are both `repair`. The generate skill's resume path repairs every `repair` id.
- **Suite delete counts verdict files twice.** `suite delete` adds the plan's verdict files to its leftovers; the
  deletion service has already removed them, so the leftover step deletes nothing more but still lists them in
  `leftovers_removed` (and the dry run in `leftovers`), and the human summary counts them as leftovers.
- **Manifest candidates collapse to the oldest.** 096 keeps both hashes of a file recorded under two pre-096 key forms
  (init's relative key, update-skills' absolute key) as in-memory candidates, but the saved record is the first hash in
  ordinal key order — init's older relative one. For a file skipped as customized, the newer record is lost on save; if
  the user later restores the content SPECTRA last wrote, the next run calls it customized again.
- **Keep-baseline creates entries.** A critic repair ingests with `--keep-baseline`; for a test that had no baseline
  entry, the recorder creates a full entry at the current docs. A never-baselined test is then baselined at the moment
  of a repair, hiding every doc change since the test was written.
- **Heading ids and anchors.**
  - The explicit-id rule (`id="…"`) matches after any non-word character, so `data-id="x"` and `aria-id="x"` count as
    the heading's id — in both the section matcher and the canonical-ref builder (094).
  - A heading with an image (`## ![logo](x.png) Setup`) has the anchor key of "logo Setup"; GitHub's anchor for it is
    built from the rendered text, where an image contributes no text (`#-setup`), so `#setup` matches nothing.
  - 085 made a heading's trailing `{#id}` invisible to change detection for the current text, but snapshots stored by
    084 still contain the block. A section whose 084 snapshot was taken while a heading had `{#id}`, and whose id is
    later removed or changed, compares as changed once. (The opposite order — snapshot first, id added later — already
    compares as cosmetic.)
- **Group criterion state.** In a group update prompt, each criterion shows one state for the whole group (the
  strongest any test gives it), and each test block lists only ids. A test whose own baseline matches the criterion
  sees "changed since baseline" (or "not baselined") because another test in the group disagrees.
- **PLAN_STALE before the template decision.** `compile-prompt --batch` checks staleness before deciding the mode. In
  `count_fallback` mode (a customised pre-094 template — the generator reads the current docs itself), any doc edit
  since the analysis refuses the batch with `PLAN_STALE`, though nothing stale reaches the prompt.
- **One key, two jobs.** 095's per-behaviour coverage rule reads `generation.duplicate_threshold`, which elsewhere is the
  combined title+steps duplicate threshold. The init template writes `duplicate_threshold: 0.6` (= its default), so
  every init-created workspace has it set.

## Decisions

Made while writing this spec (open to change at `/speckit.clarify`):

- **F1 — where the critic-model warning appears.** The config never fails on `ai.critic` (every command runs). The
  warning appears where the critic model is used or the config is checked: `update-skills` (as one warning line, with
  the model it installed) and `spectra validate` (the retired-key notice). Other commands don't read the key and stay
  silent — adding a line to every criteria, baseline and generation command (and to their JSON `warnings`) for a key
  they don't use would be noise.
- **F1 — legacy keys.** The retired keys are `provider`, `api_key_env`, `base_url`, `enabled`, `timeout_seconds`,
  `max_concurrent`. `ai.fallback_strategy` stays a retired key with the notice, but it doesn't make `ai.critic` legacy
  (it isn't in that block). Comment keys (`_comment…`) don't count.
- **F2 — summary counts.** The audit summary gains `pending_ground` and `pending_review` counts; `partial_pending_repair`
  now counts only `repair` entries (partial verdicts), which is what its name always said.
- **F4 — "newest".** In a pre-096 manifest, update-skills wrote absolute keys and init wrote relative ones, and
  update-skills always runs after init, so the absolute-key record is the newer one. Among absolute keys, one under
  the current workspace root is newer than one re-rooted from another location (a moved workspace). When the newest
  can't be told apart (two records of the same rank with different hashes), all candidates are saved.
- **F5 — counts.** A keep-baseline ingest reports the tests it left unbaselined (`left_unbaselined`, only when > 0) so
  the "no entry" outcome is visible; the existing counts are unchanged.
- **F6 — image alt text.** The finding reproduces. The anchor rule gains GitHub's reading — an image contributes no
  text — as an *additional* text key; the 085 text key (alt text kept) still matches, so no ref that resolves today
  stops resolving.
- **F6 — `{#id}` snapshots.** The reproducing case is the 084 snapshot taken while the heading had `{#id}`. Stored
  snapshots get the 085 comparison rule (heading attribute blocks removed) before comparing; since a stored snapshot is
  normalised text without line breaks, a heading is recognised by its `#` marker and the block must directly follow its
  text.
- **F9 — the fallback notice.** `duplicate_threshold` counts as "set" for the one-release fallback only when it differs
  from the default 0.6. Every init-created workspace has `duplicate_threshold: 0.6`, which already equals the coverage
  default — falling back to it would change nothing, and a notice in every such workspace would be noise. The init
  template gains `coverage_threshold: 0.6`. The generation plan records the threshold it used as `coverage_threshold`
  (a 095 plan's `duplicate_threshold` is still read).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A pre-069 workspace keeps working (Priority: P1)

A team created its workspace with SPECTRA v1.45. Its config still has the old in-process critic block. After upgrading
to 097, every command must still run, and the critic subagent must not be installed with a GitHub Models model id.

**Why this priority**: Today a value like `openai/gpt-4.1` makes every SPECTRA command fail with `CONFIG_INVALID`, and
`gpt-5-mini` is installed as the critic's model — the workspace is broken or silently misconfigured.

**Independent Test**: Workspaces whose config is the real init template with the v1.45 critic block (and one with
`openai/gpt-4.1`): every command exits as it does with the plain template; `update-skills` installs `model: sonnet` and
prints the notice; `spectra validate` shows the extended notice.

**Acceptance Scenarios**:

1. **Given** the v1.45 block (`enabled`, `provider: github-models`, `model: gpt-5-mini`), **When** `update-skills` runs,
   **Then** the critic agent is installed with `model: sonnet` and one warning says the old in-process critic's
   `ai.critic.model` is ignored and how to set it again.
2. **Given** `ai.critic.model: "openai/gpt-4.1"` with no retired key, **When** any command runs, **Then** it doesn't fail
   on the config; `update-skills` warns that the value is invalid and installs `model: sonnet`.
3. **Given** the 058–068 block (`enabled`, `model: claude-sonnet-4-6`, `max_concurrent`), **Then** it is legacy: the model
   is ignored with the notice and `sonnet` is installed.
4. **Given** a new block `{"model": "opus"}`, **Then** `opus` is installed with no warning (097 behaviour).
5. **Given** a legacy block, **When** `spectra validate` runs, **Then** the retired-key notice lists the retired keys
   found (including `enabled` / `max_concurrent`) and says "ai.critic.model from the old in-process critic is ignored;
   set it again (alias, model id or inherit) to choose the critic subagent's model".
6. **Given** `ai.critic.model` is a number, an empty string or `[1m]`, **Then** no command fails; `update-skills` warns
   and uses `sonnet`.

---

### User Story 2 - The audit says what each test needs (Priority: P1)

The generate skill resumes an interrupted run. The audit must tell it which tests only need their grounding block
written, which need a repair, and which wait for the user — and the skill must act on each list differently.

**Why this priority**: Today a test the critic already confirmed is sent through its one repair attempt, and a
hallucinated test kept for the user is "repaired" without the user's decision.

**Independent Test**: A suite after an interrupted run — one `grounded` and one `partial` verdict with no blocks, one kept
`hallucinated` verdict: the audit lists `ground`, `repair`, `review`; the skill text routes each.

**Acceptance Scenarios**:

1. **Given** a fresh `grounded` verdict and no block, **Then** `action_needed: "ground"`.
2. **Given** a fresh `partial` verdict and no block, **Then** `action_needed: "repair"`.
3. **Given** a fresh `hallucinated` verdict and no block, **Then** `action_needed: "review"`.
4. **Given** a stale verdict, **Then** `critic` (097); **given** a block, `review` (flagged) or `none` (097).
5. **Given** `--action ground`, **Then** only `ground` entries are listed; an unknown value still exits 1 naming the
   allowed values (now including `ground`).
6. **Given** the generate skill's resume path, **Then** `ground` ids go to `ingest-grounding --all --ids`, `repair` ids
   to the repairer, and `review` ids are reported to the user, never repaired.

---

### User Story 3 - Honest counts and records (Priority: P2)

A user deletes a suite and reads the summary; another user restores a skill file they had edited. Each report must
count what happened once, and the manifest must remember what SPECTRA last wrote.

**Why this priority**: Double counts and forgotten records make the user distrust the reports and keep a restored file
"customized" forever.

**Independent Test**: Suite delete of a suite with 3 verdict files: they appear once, under the verdict-file list.
Manifest with relative v1 + absolute v2 records of an edited file: skipped; after the user restores v2, the next run
updates it.

**Acceptance Scenarios**:

1. **Given** a suite whose tests have 3 verdict files, **When** `suite delete` runs (and with `--dry-run`), **Then**
   `verdict_files_removed` lists the 3 files, `leftovers_removed` / `leftovers` list only staging folders and the
   generation plan, and the human summary counts the verdict files once.
2. **Given** a pre-096 manifest with a relative-key record (v1) and an absolute-key record (v2) of the same file, and the
   user edited the file, **When** `update-skills` runs, **Then** the file is skipped as customized and the saved record is
   v2.
3. **Given** the user then restores the file to v2, **When** `update-skills` runs again, **Then** the file is recognised
   as unedited and updated.
4. **Given** two records of the same rank with different hashes, **Then** the saved manifest keeps both (as candidates),
   and either content counts as unedited on the next run.

---

### User Story 4 - A repair never baselines a test (Priority: P2)

A critic repair edits a test that was never baselined. Triage must still list it as unbaselined, so the user decides
when its baseline starts.

**Why this priority**: Baselining at repair time hides every doc change since the test was written — the exact change
the update workflow exists to surface.

**Independent Test**: Repair (keep-baseline ingest) of a never-baselined test → still no entry; of a baselined test →
fingerprints unchanged.

**Acceptance Scenarios**:

1. **Given** a test with no baseline entry, **When** it is ingested with `--keep-baseline`, **Then** it still has no
   entry, triage lists it as unbaselined, and the ingest reports `left_unbaselined: 1`.
2. **Given** a baselined test, **When** it is repaired, **Then** its fingerprints are unchanged; a key it lacked is
   added, as today.
3. **Given** a normal ingest (no `--keep-baseline`), **Then** behaviour is unchanged (a new test gets an entry).

---

### User Story 5 - Anchors that match the heading the author meant (Priority: P2)

A tech writer adds a logo image to a heading, or a `data-id` attribute, or removes a `{#id}` from a heading. Refs must
still point at the right section, and a markup-only change must not be reported as a doc change.

**Why this priority**: A wrong match or a spurious change sends tests to triage for nothing, or links them to the wrong
section.

**Independent Test**: Headings with `data-id`, `aria-id`, an image, and an 084 snapshot with `{#id}`.

**Acceptance Scenarios**:

1. **Given** `## <span data-id="x">Setup</span>` and a ref `#x`, **Then** the ref doesn't match the heading by id (only a
   whole `id=` attribute counts); `## <a id="x"></a>Setup` with `#x` still matches. Same for the canonical-ref builder.
2. **Given** `## ![logo](x.png) Setup`, **Then** `#setup` and `#-setup` match it, and `#logo-setup` still matches (085).
3. **Given** an 084 snapshot of a section whose heading had `{#setup}`, and the id is now removed or changed, **When**
   triage runs, **Then** the section is COSMETIC (auto-acknowledged), not LIGHT.
4. **Given** the 085 matcher rows and the 084 determinism rows, **Then** they stay green.

---

### User Story 6 - Each test sees its own criterion state (Priority: P3)

The update skill compiles one prompt for a group of tests. A criterion changed since one test's baseline but not since
another's. Each test must be told its own state.

**Why this priority**: The model edits a test that doesn't need it when told "changed since baseline" for a criterion
that, for that test, didn't change.

**Independent Test**: A group of TC-201 (criterion unchanged for it) and TC-202 (changed): the shared list says
`changed since baseline`; TC-202's block says `AC-PAY-004 (changed)`, TC-201's says `AC-PAY-004 (unchanged)`.

**Acceptance Scenarios**:

1. **Given** tests disagree on a criterion's state, **Then** each test block shows its own state next to that id; the
   shared criteria list keeps the strongest state.
2. **Given** all tests agree (or a single test), **Then** the output is byte-identical to 097's.
3. **Given** identical inputs, **Then** the output is byte-identical across runs.

---

### User Story 7 - Count-mode batches survive a doc edit (Priority: P3)

A team with a customised pre-094 generation template edits a doc after the analysis. Its batches are compiled in
count mode (the generator reads the current docs anyway), so they must not be refused.

**Why this priority**: The refusal blocks generation for no reason; only the behaviour titles may be outdated.

**Independent Test**: Plan made; section edited; customised template: `count_fallback` with a warning; bundled template:
`PLAN_STALE`.

**Acceptance Scenarios**:

1. **Given** a customised pre-094 template and a changed (or no longer resolving) section, **When** `compile-prompt
   --batch B1` runs, **Then** it emits the count-mode prompt with the warning "the documentation changed since the
   analysis; behaviour titles may be outdated" (and which sections), exit 0.
2. **Given** the bundled template (batch mode) and a changed section, **Then** `PLAN_STALE`, exit 4, as today.
3. **Given** `--batch unsourced`, **Then** unchanged (no sections, no staleness check).

---

### User Story 8 - Coverage has its own threshold (Priority: P3)

A team tunes duplicate detection without wanting to change which behaviours count as already covered.

**Why this priority**: One key doing two jobs means tuning one silently changes the other.

**Independent Test**: Config rows with and without `coverage_threshold` / a non-default `duplicate_threshold`.

**Acceptance Scenarios**:

1. **Given** `generation.coverage_threshold: 0.8`, **Then** coverage uses 0.8 regardless of `duplicate_threshold`.
2. **Given** no `coverage_threshold` and `duplicate_threshold: 0.75`, **Then** coverage uses 0.75 and one notice asks
   to set `coverage_threshold`.
3. **Given** no `coverage_threshold` and `duplicate_threshold: 0.6` (the init template), **Then** coverage uses 0.6 with
   no notice.
4. **Given** `coverage_threshold` outside 0–1 or not a number, **Then** `CONFIG_INVALID`; missing or `null` → default.
5. **Given** a new workspace, **Then** its config has `coverage_threshold: 0.6`.

### Edge Cases

- `ai.critic` with only comment keys or only `model`: not legacy.
- `ai.critic` legacy and `model` missing: default `sonnet`, the notice still names the retired keys (no "model ignored"
  sentence needed, but harmless).
- `ai.critic` is not an object (e.g. a string): ignored with a warning in `update-skills`, never `CONFIG_INVALID`.
- Legacy block and invalid config for another reason: 097's rule (keep the installed model line) applies, with the
  config-invalid warning.
- Audit: a verdict file whose `verdict` is unreadable or unknown and no block → `critic`.
- Audit: a `manual` verdict file (not produced by the critic) → judged by its block, as today.
- Suite delete: a verdict file that fails to delete is a warning (service), listed once.
- Manifest: a record under one key only → saved as today; no `candidates` field.
- Keep-baseline: a repair of a test whose entry exists but lacks every ref → the refs are added (existing entry).
- Anchor: an image-only heading (`## ![logo](x.png)`) has an empty image-free key; it matches only by the 085 key.
- Anchor: `id = "x"` with spaces, single quotes, mixed case `ID=` keep matching.
- Snapshot: a `{…}` in body text (not after a heading's text) is never stripped.
- Group prompt: a criterion linked by only one test of the group carries no per-test annotation.
- count_fallback with a section of the batch that no longer resolves: a warning naming it, not PLAN_STALE.
- Coverage: `duplicate_threshold` outside (0, 1] used as a fallback keeps 095's out-of-range warning.

## Requirements *(mandatory)*

### Functional Requirements

**F1 — legacy critic block**
- **FR-001**: An `ai.critic` block that contains any retired key (`provider`, `api_key_env`, `base_url`, `enabled`,
  `timeout_seconds`, `max_concurrent`) MUST be treated as legacy: its `model` is ignored and the default (`sonnet`) is
  used.
- **FR-002**: A non-legacy `ai.critic.model` that is invalid (fails 097's pattern, is not a string, or `ai.critic` is not
  an object) MUST be ignored with a warning and the default used; it MUST NOT make the config invalid. No command fails
  because of `ai.critic`.
- **FR-003**: The retired-key notice (`spectra validate`) MUST list every retired `ai.critic` key present (the six of
  FR-001) and `ai.fallback_strategy`, and, for a legacy block with a `model`, MUST say "ai.critic.model from the old
  in-process critic is ignored; set it again (alias, model id or inherit) to choose the critic subagent's model".
- **FR-004**: `update-skills` MUST print one warning when it ignores `ai.critic.model` (legacy or invalid), naming the
  model it installed. A valid new-style value keeps 097's behaviour with no warning.
- **FR-005**: Tests MUST include rows built from the real init template with the v1.45 block and with
  `openai/gpt-4.1`: every command in the config rule matrix runs as with the plain template, the warning appears, and
  `update-skills` installs `model: sonnet`; plus a `ConfigRuleMatrixTests` row for the pre-069 template.

**F2 — audit actions from the verdict**
- **FR-006**: For a test with a verdict file, no grounding block and a fresh verdict, `action_needed` MUST be `ground`
  (grounded), `repair` (partial) or `review` (hallucinated); unknown → `critic`. Stale stays `critic`; a test with a
  block keeps `review` / `none`.
- **FR-007**: `--action` MUST accept `ground`; the summary MUST gain `pending_ground` and `pending_review`, and
  `partial_pending_repair` MUST count only `repair` entries.
- **FR-008**: The generate skill's resume path MUST send `ground` ids to `ingest-grounding --suite S --all --ids`,
  `repair` ids to the repairer, and report `review` ids without repairing them. The update skill's Step 5a and
  `spectra-review-flagged` MUST be unchanged.

**F3 — suite delete reporting**
- **FR-009**: `leftovers_removed` (and the dry run's `leftovers`) MUST list only what the leftover step removes: the
  suite's staging folders and generation plan. Verdict and record files MUST be reported once, under a new
  `verdict_files_removed` list (the deletion service's list, as `spectra delete` reports it), and counted once in the
  human summary.

**F4 — manifest candidates on save**
- **FR-010**: When a key has several recorded hashes, the saved record MUST be the newest: an absolute key under the
  current root before a re-rooted absolute key before a relative key. When the newest can't be told apart, the saved
  manifest MUST keep all candidates (an additive `candidates` field), which the next load reads back.
- **FR-011**: A file whose hash equals the saved record (or any saved candidate) MUST count as unedited.

**F5 — keep-baseline never creates an entry**
- **FR-012**: In keep-baseline mode, a test with no baseline entry MUST get none; triage MUST keep listing it as
  unbaselined; the ingest JSON MUST report `left_unbaselined` when > 0. An existing entry only gains missing keys, as
  today. Rebuild mode and `source-baseline --init` are unchanged.

**F6 — heading ids and anchors**
- **FR-013**: The id-attribute rule MUST match `id=` only as a whole attribute name (preceded by whitespace or the tag
  name), in both the section matcher and the canonical-ref builder; `data-id`, `aria-id`, `x-id` etc. are not ids.
- **FR-014**: A heading's text MUST also match an anchor by its image-free key (images contribute no text, as on
  GitHub), in addition to the 085 text key.
- **FR-015**: Before comparing with the current section, a stored snapshot MUST have the 085 rule applied (a heading's
  trailing attribute block removed), so an id-only heading change against an 084 snapshot is COSMETIC.
- **FR-016**: The 085 matcher rows and 084 determinism rows MUST stay green.

**F7 — per-test criterion state in group prompts**
- **FR-017**: When the tests of a group disagree on a criterion's state, each test block MUST show its own state next
  to that id (`changed`, `unchanged`, `not baselined`, `removed`); the shared criteria list MUST keep the strongest
  state. When they agree, the output MUST be byte-identical to 097's. Output MUST be byte-identical for identical
  inputs.

**F8 — PLAN_STALE and count_fallback**
- **FR-018**: `compile-prompt --batch` MUST decide the template mode before the staleness check. In `count_fallback`
  mode a changed or unresolvable section MUST be a warning ("the documentation changed since the analysis; behaviour
  titles may be outdated", naming the sections), not `PLAN_STALE`. Batch mode MUST still refuse with `PLAN_STALE`.
  `unsourced` is unchanged.

**F9 — coverage threshold key**
- **FR-019**: The config MUST accept an optional `generation.coverage_threshold` (a number 0–1, default 0.6; missing or
  `null` → default; anything else → `CONFIG_INVALID` naming the key). The per-behaviour coverage rule MUST use it.
  `duplicate_threshold` MUST be used only for duplicate detection.
- **FR-020**: When `coverage_threshold` is absent and `duplicate_threshold` is set to a value other than 0.6, coverage
  MUST use `duplicate_threshold` and print one notice asking to set `coverage_threshold` (this fallback is for one
  release).
- **FR-021**: The init template MUST include `generation.coverage_threshold: 0.6`. The plan MUST record the threshold
  used as `coverage_threshold`; a 095 plan's `duplicate_threshold` MUST still be read.

**Cross-cutting**
- **FR-022**: Every fix MUST have a regression test that fails on `986da01` (skill text guards included).
- **FR-023**: Every fix MUST be verified on a workspace made by the real `spectra init` and on a pre-098 workspace,
  including a pre-069 config with the old critic block (083 rule).
- **FR-024**: No config section becomes required; `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` stay green.
- **FR-025**: Human summaries MUST stay in JSON mode (stderr).
- **FR-026**: The test suite MUST stay fully green.
- **FR-027**: Docs: cli-reference (audit actions, `coverage_threshold`), configuration.md (`ai.critic` legacy rule,
  `coverage_threshold`), usage.md, CHANGELOG.md, a short CLAUDE.md entry.

### Key Entities

- **Legacy critic block**: an `ai.critic` object with at least one retired key; its `model` is not the subagent's model.
- **Audit entry**: one test with its verdict, block state, freshness and `action_needed` (`ground` / `repair` /
  `review` / `critic` / `none`).
- **Manifest record**: a file's recorded hash; with 098, possibly several `candidates`.
- **Baseline entry**: a test's recorded fingerprints; absent = unbaselined.
- **Heading keys**: the 085 text key, the image-free key, explicit ids.
- **Snapshot**: the normalised text of a section at a fingerprint, stored by 084+.
- **Per-test criterion state**: one test's view of a linked criterion (unchanged / not baselined / changed / removed).
- **Coverage threshold**: `generation.coverage_threshold`, the title-similarity bound for "already covered".

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With a pre-069 config (v1.45 block, or `openai/gpt-4.1`), 100 % of the commands in the config rule matrix
  run exactly as with the plain init template, and the installed critic model is `sonnet`.
- **SC-002**: After an interrupted generate run, 0 tests with a `grounded` verdict and 0 kept `hallucinated` tests are
  sent to the repairer.
- **SC-003**: Every file a suite delete removes is reported exactly once.
- **SC-004**: A file restored to the content SPECTRA last wrote is updated on the next `update-skills` run (0 false
  "customized").
- **SC-005**: 0 never-baselined tests gain a baseline entry through a repair.
- **SC-006**: 0 refs match a heading through `data-id`-style attributes; refs written GitHub-style for image headings
  resolve; an id-only heading change against an 084 snapshot is reported as cosmetic.
- **SC-007**: In a group prompt, 0 tests are told a criterion changed when it didn't change for them.
- **SC-008**: 0 count-mode batches are refused for a doc edit.
- **SC-009**: Changing `duplicate_threshold` changes 0 coverage decisions once `coverage_threshold` is set.
- **SC-010**: Every fix has a test that fails on `986da01`, and the whole suite passes with 0 failures.

## Assumptions

- Retired `ai.critic` keys only ever came from SPECTRA's own templates or docs (v1.x); a user would not add `enabled` to
  a new-style block on purpose.
- Pre-096 manifests were always written by init first and update-skills later (update-skills requires an initialised
  workspace).
- GitHub's heading anchors are built from the rendered heading text; images render no text.
- 084 stored snapshots with whitespace collapsed to single spaces; a heading line's `#` marker survives normalisation.
- `duplicate_threshold: 0.6` in an existing workspace came from the init template (the default), not a tuning decision.

## Out of Scope

- 099: performance and cleanup (085 D5/D6, the 078 parse cost, 089's triple parse, 095's dead code, the 4 build
  warnings).
- 100: docs trim, constitution amendment.
- `depends_on` list support.
