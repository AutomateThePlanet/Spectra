# Feature Specification: Verification Hardening (Phase 6, Part 2)

**Feature Branch**: `097-verification-hardening`
**Created**: 2026-10-14
**Status**: Draft
**Input**: User description: "097-verification-hardening: Phase 6, part 2 of 4 — a test's grounding block and the audit
must tell the truth. Collects the verification findings deferred from 090–093, the tracked --dry-run gap, and the critic
model question. No new features beyond the named options. Each fix needs a regression test that fails on 807047c."
(Background, F1–F7, constraints and scope as quoted in the request.)

## Context

A test's grounding block says "a critic checked this test against the docs". That block, and the audit that reports on
it, must only say so when it is true. Several gaps deferred from specs 090–093 let them say more than is true, or let a
stale verdict drive a repair.

Facts (verified on `807047c`):
- **Grounding batches without ids.** The generate skill's critic step (8a) and the update skill's re-verify step (5a)
  write grounding blocks with one batch call over the whole suite, without naming tests.
  - A test the skill reports as unverified (the critic's own line disagrees with the CLI's verdict) still gets a
    `grounded` block.
  - Fresh verdicts left over from other tests are written too. That inflates the written count against the number of
    tests kept as grounded, which raises a false 075 warning.
- **Audit lists too much and too little.**
  - The generate skill's resume path runs `audit-grounding --suite S` in the main conversation. Since 090 that lists
    every indexed test, so the whole suite lands in a context the 093 budget contract keeps small. The command has no
    filter besides `--suite`.
  - It reports each verdict file's verdict without the 090/091 freshness check, so a verdict that ingest refuses as
    `VERDICT_STALE` shows as current.
  - It walks every verdict file in `.spectra/verdicts/`, including other suites'.
- **Repairs from stale verdicts.** `compile-repair-prompt` and `compile-repair-batch` read the verdict file with no
  freshness check, so a stale verdict can drive a test's one repair attempt.
- **Two clocks.** 091's freshness rule compares `compiled_at` (the CLI process clock) with the verdict file's
  last-write time (the file system's clock). On a network share or container mount whose clock is more than 2 s off,
  every new verdict is refused as `VERDICT_STALE`.
- **Empty `--ids`.** `ingest-grounding --all --ids ""` (empty after trimming) without `--repaired` processes no tests;
  091's spec says an empty `--ids` counts as missing.
- **`--dry-run` silently ignored.** `--dry-run` is a global option, so `ingest-verdict --all`, `ingest-grounding --all`
  and `ingest-analysis` accept it — and then write anyway. Constitution IV requires it; 091's and 094's plans track
  the gap in Complexity Tracking.
- **The critic model.**
  - The critic agent pins `model: claude-sonnet-4-6`; every other bundled agent uses `model: inherit`.
  - The `ai.critic` config block was removed in 069. The config loader only flags three retired provider keys, so
    nothing selects the critic model, and CLAUDE.md's "`ai.critic.model` is its only selector" is stale.
  - The old in-process critic classes (the critic factory, the critic model resolver, the critic config model) are
    referenced by no production code, only by each other and by tests of themselves.

## Decisions

Made while writing this spec (open to change at `/speckit.clarify`):

- Critic-model option: **(b)**, as the request recommends: a configurable `ai.critic.model` (default the alias
  `sonnet`), written into the installed critic agent by `init` and `update-skills`. It keeps the critic independent of
  the generator by default (dual-model verification) without pinning a model id that goes stale.
- `--dry-run` with `ingest-grounding --test` (single test): refused (exit 1, "--dry-run requires --all"), so a dry run
  never writes. `ingest-verdict` only reads and classifies (it writes nothing in either form), so `--dry-run` is accepted
  there in both forms and only adds `dry_run: true` (plan research R6).
- The file-time rule (F4) doesn't apply to 090 records (no `compiled_at`): 091 kept them on the
  fingerprint-only rule until the critic prompt is compiled again; that stays.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A grounding block only for a test the critic confirmed (Priority: P1)

A QA engineer runs the generate or update skill. The critic checks each new or edited test. A test the skill reports as
"unverified (critic could not judge)" must not end up with a `grounded` block, and a verdict left over from an earlier
run of another test must not be written as part of this batch.

**Why this priority**: The grounding block is the evidence that a test was checked against the docs. A block on an
unconfirmed test is a false claim that downstream users (execution, coverage, review) trust.

**Independent Test**: Read the installed skills: both grounding calls name the confirmed ids and are skipped when there
are none. Run the CLI batch with the ids of two tests while a third test has a fresh `grounded` verdict on disk: only
the two get blocks.

**Acceptance Scenarios**:

1. **Given** the generate skill kept tests A and B as grounded and reported C as unverified, **When** step 8a writes
   grounding blocks, **Then** the call names A and B only, C gets no block, and the 075 check compares `written` with 2.
2. **Given** no test was kept as grounded in step 8a, **When** the step runs, **Then** no grounding call is made.
3. **Given** the update skill re-verified a group and the critic confirmed only some edited tests, **When** step 5a
   writes blocks, **Then** the call names exactly those ids.
4. **Given** a fresh `grounded` verdict of test D (not part of this run) is on disk, **When** the batch runs with ids A,B,
   **Then** D is not written.

---

### User Story 2 - The audit tells the truth and stays small (Priority: P1)

The generate skill resumes an interrupted repair. It asks the audit which tests still need repair. The audit must list
only that suite's tests, only the ones asked for, and must flag a verdict the ingest would refuse as stale.

**Why this priority**: The audit is how the skills (and users) decide what to do next. Stale verdicts shown as current
drive wrong actions; a full-suite listing breaks the main conversation's context budget.

**Independent Test**: A workspace with two suites, verdict files for both, one stray verdict and one stale verdict:
`audit-grounding --suite A` lists only A's indexed tests, marks the stale one, and `--action repair` / `--ids` narrow
the list.

**Acceptance Scenarios**:

1. **Given** verdict files for suites A and B and a verdict of an id in no index, **When** `audit-grounding --suite A`
   runs, **Then** only A's indexed tests are listed; B's and the stray verdict are not.
2. **Given** a test whose verdict file is older than its fingerprint record (or whose content changed since), **When**
   the audit runs, **Then** its entry has `stale: true` and `action_needed: "critic"`, and the summary's `stale` count
   includes it.
3. **Given** `--action repair`, **When** the audit runs, **Then** only entries with `action_needed: "repair"` are listed
   (the summary counts still describe the whole suite).
4. **Given** `--action critic,review` or `--action critic --action review`, **Then** entries needing either are listed.
5. **Given** `--ids A1,A2,ZZ`, **Then** only A1 and A2 are listed and ZZ is reported as not found.
6. **Given** the generate skill's resume path, **Then** it calls `audit-grounding --suite S --action repair`.

---

### User Story 3 - No repair from a stale verdict (Priority: P1)

A test was edited after its critic run, or its critic prompt was compiled again and the critic hasn't answered yet. The
old verdict must not drive the test's one repair attempt.

**Why this priority**: A repair driven by an outdated verdict "fixes" problems that no longer exist and spends the
test's only repair attempt.

**Independent Test**: With a stale verdict, `compile-repair-prompt` refuses with `VERDICT_STALE` (exit 4) and
`compile-repair-batch` lists the test under `stale` and emits no prompt for it.

**Acceptance Scenarios**:

1. **Given** a stale verdict for test T, **When** `compile-repair-prompt` runs for T, **Then** it exits 4 with
   `VERDICT_STALE` and the same message the ingest commands give; no prompt is written.
2. **Given** a batch where T is stale and U is fresh, **When** `compile-repair-batch` runs, **Then** U gets a prompt, T
   is listed under `stale` and gets none.
3. **Given** a verdict with no fingerprint record, **Then** both commands accept it, as today.

---

### User Story 4 - Freshness that works on any file system (Priority: P2)

A team keeps the workspace on a network share or a container mount whose clock differs from the machine running the CLI.
New verdicts must still be accepted, and an old verdict must still be refused.

**Why this priority**: On such mounts 091's rule refuses every new verdict, which blocks generation and update entirely.

**Independent Test**: A record and a verdict whose file times are set explicitly, with `compiled_at` shifted by ±1 h:
the decision follows the file times.

**Acceptance Scenarios**:

1. **Given** a record written at file time t and a verdict at t + 10 s, with `compiled_at` = t + 1 h, **Then** the
   verdict is fresh.
2. **Given** a record at t and a verdict at t − 10 s, with `compiled_at` = t − 1 h, **Then** the verdict is stale.
3. **Given** a verdict 1 s older than its record (within the 2 s allowance), **Then** it is fresh.
4. **Given** a record whose file time can't be read, **Then** `compiled_at` is used, as in 091.
5. **Given** a 090 record (no `compiled_at`), **Then** only the fingerprint rule applies, as in 091.

---

### User Story 5 - A dry run of the batch ingests (Priority: P2)

Before running a batch ingest on a large suite, a user (or a skill) wants to see what it would do without changing
anything.

**Why this priority**: Constitution IV requires `--dry-run` on writing commands; today these three accept the flag and
write anyway, which is worse than refusing it.

**Independent Test**: Run each command with and without `--dry-run` on copies of the same workspace: same JSON (plus
`dry_run: true`), same exit code, and the dry-run copy is byte-identical to before.

**Acceptance Scenarios**:

1. **Given** fresh verdicts for a suite, **When** `ingest-verdict --suite S --all --dry-run` runs, **Then** it reports
   the same classification and exit code as a real run, with `dry_run: true`, and writes nothing.
2. **Given** eligible verdicts, **When** `ingest-grounding --suite S --all --dry-run` runs (with or without `--ids`,
   `--repaired`), **Then** it reports the same `written`/skipped outcomes and exit code, and no test file changes.
3. **Given** an analysis response, **When** `ingest-analysis --dry-run` runs, **Then** it reports the same counts,
   plan, batches and refusals, and no plan file or analysis file is written.
4. **Given** `ingest-grounding --test T --dry-run`, **Then** the command exits 1 with "--dry-run requires --all" and
   writes nothing. `ingest-verdict --from F --dry-run` reports as without the flag, plus `dry_run: true` (it never
   writes).

---

### User Story 6 - Choosing the critic's model (Priority: P2)

A team lead wants the critic to run on a different model than the generator (dual-model verification) without editing
the agent file, and without a model id pinned in SPECTRA that goes stale.

**Why this priority**: The critic's independence is the point of verification; today the model is pinned to a specific
id nobody can configure.

**Independent Test**: The real `spectra init` installs the critic agent with `model: sonnet`; setting
`ai.critic.model` to `opus` and running `update-skills` updates an unedited agent file and never an edited one.

**Acceptance Scenarios**:

1. **Given** a new workspace, **When** `spectra init` runs, **Then** the config has `ai.critic.model: "sonnet"` and the
   installed critic agent's frontmatter says `model: sonnet`.
2. **Given** `ai.critic.model` set to `opus` (or a model id, or `inherit`), **When** `update-skills` runs, **Then** an
   unedited critic agent gets that model and is reported as updated.
3. **Given** the user edited the critic agent file, **When** the key changes and `update-skills` runs, **Then** the file
   is not touched and is reported as customized (096 rule).
4. **Given** a pre-097 workspace whose unedited critic agent pins `claude-sonnet-4-6` and whose config has no
   `ai.critic`, **When** `update-skills` runs, **Then** the agent gets `model: sonnet`.
5. **Given** `ai.critic.model` is empty, starts with something other than a letter or digit (e.g. `[1m]`), has
   spaces, quotes or a line break, or isn't a string, **Then** the value is invalid (`CONFIG_INVALID` for the commands
   that require a valid config). `update-skills` only reads the config for this one value, so it doesn't fail: it warns
   `spectra.config.json is invalid (<error_code>: <first error>); the critic agent's model was not changed`, keeps the critic agent's installed model line, refreshes every other file as usual and exits 0.
   (`init` always writes a fresh config from its template, so it never sees such a value; it keeps the 083 rule.)
7. **Given** the config is invalid for an unrelated reason (malformed JSON, an out-of-range key, a duplicate key),
   **When** `update-skills` runs, **Then** it behaves as in scenario 5: one warning, the critic's model line unchanged,
   everything else refreshed, exit 0.
6. **Given** a config with the retired keys (`ai.critic.provider`, `ai.critic.api_key_env`, `ai.critic.base_url`,
   `ai.fallback_strategy`), **Then** `spectra validate` still shows the retired-key notice, and `ai.critic.model` next to
   them is honoured.

---

### User Story 7 - An empty id list means "no list" (Priority: P3)

A skill builds `--ids` from a list that turns out empty. The command must behave as if no `--ids` was given, not
silently process nothing.

**Why this priority**: Small, but a silent "nothing processed" is exactly the kind of false success the phase removes.

**Independent Test**: `ingest-grounding --all --ids ""` and `--ids " , "` behave like `--all`; with `--repaired` they
refuse with `IDS_REQUIRED`; `ingest-verdict --all --ids ""` behaves like `--all`.

**Acceptance Scenarios**:

1. **Given** `ingest-grounding --suite S --all --ids ""` (or only commas and spaces), **Then** every eligible test is
   processed, as without `--ids`.
2. **Given** the same with `--repaired`, **Then** the command exits 4 with `IDS_REQUIRED`.
3. **Given** `ingest-verdict --suite S --all --ids " "`, **Then** every verdict of the suite is classified.

### Edge Cases

- The generate skill keeps no test as grounded in 8a: no grounding call, and the 075 guard is not evaluated (nothing to
  compare).
- `audit-grounding --ids` names a test of another suite: reported as not found for this suite.
- `audit-grounding --action` with an unknown value: exit 1 naming the allowed values; nothing listed.
- A verdict with no fingerprint record: never stale (090 rule); the audit shows no `stale` field for it.
- A verdict read from stdin (no file): fingerprint rule only, as in 091.
- `compile-critic-prompt` run twice for the same test with an unchanged test: the record's file time must still move,
  since it marks the compile.
- `ai.critic.model: "inherit"`: written as `model: inherit`, same as the other agents.
- `ai.critic` present with only retired keys (no `model`): the default `sonnet` applies, the retired-key notice shows.
- `ai.critic` keys of the removed in-process critic (`enabled`, `timeout_seconds`, `max_concurrent`) stay ignored.
- `update-skills` with no config file: the default `sonnet` applies (no config section is required — 083).
- `update-skills` with an invalid config (any reason) and no installed critic agent: the agent is installed with the
  default `sonnet`, with the same warning.
- `update-skills` with an invalid config and an edited critic agent: skipped as customized (096 rule), with the
  warning.
- `ingest-analysis --dry-run` that a real run would refuse (e.g. a plan-write problem): the dry run reports the same
  refusal code where it can be known without writing; a failure that only a write can reveal is not simulated.

## Requirements *(mandatory)*

### Functional Requirements

**F1 — grounding batches name their tests**
- **FR-001**: The generate skill's step 8a MUST call `ingest-grounding --suite S --all --ids <kept-grounded ids>` and
  MUST skip the call when that list is empty.
- **FR-002**: The update skill's step 5a MUST call the same batch with `--ids <the group's grounded ids>`, skipped when
  empty.
- **FR-003**: The 075 `written` guard text and the NON-STOP contract MUST stay verbatim; only the call lines and the id
  lists change. A test reported unverified MUST NOT be in either id list.

**F2 — audit-grounding filters and staleness**
- **FR-004**: `audit-grounding --suite S` MUST list only tests in suite S's index; verdict files of ids not in that index
  MUST be ignored.
- **FR-005**: `audit-grounding` MUST accept `--action` with values `critic`, `repair`, `review`, `none` (repeatable or
  comma-separated, case-insensitive) and list only entries whose `action_needed` is one of them; an unknown value MUST
  exit 1 naming the allowed values.
- **FR-006**: `audit-grounding` MUST accept `--ids a,b` with 091's parsing (trimmed, distinct, case-insensitive; an
  empty list counts as not given) and report ids not in the suite under `not_found`.
- **FR-007**: An entry whose verdict fails the 090/091 freshness check MUST get `stale: true` and
  `action_needed: "critic"`; the summary MUST gain a `stale` count. Summary counts MUST describe the whole suite, not the
  filtered list.
- **FR-008**: The generate skill's resume path MUST call `audit-grounding --suite S --action repair`.
- **FR-009**: Apart from the entries FR-004 removes (stray and other-suite verdicts), the audit JSON changes MUST be
  additive.
- **FR-009a**: In JSON mode, the audit's human lines (`<id>: not found in suite '<s>'`, stale notes) MUST go to stderr;
  stdout stays the JSON document. The table stays human-mode only, as before.

**F3 — repair commands check freshness**
- **FR-010**: `compile-repair-prompt` MUST refuse a stale verdict with `VERDICT_STALE` (exit 4, the ingest commands'
  message) and write no prompt.
- **FR-011**: `compile-repair-batch` MUST list stale tests under `stale` and emit no prompt for them; fresh tests are
  unaffected.
- **FR-012**: A verdict with no fingerprint record MUST be accepted by both, as today.

**F4 — one clock**
- **FR-013**: For a record that has `compiled_at`, the freshness reference MUST be the record file's own last-write
  time: a verdict file older than it by more than the 2 s allowance is stale. `compiled_at` MUST be used only when the
  record's file time can't be read.
- **FR-014**: `compile-critic-prompt` MUST rewrite the record on every compile, so its file time marks the compile.
- **FR-015**: 090 records (no `compiled_at`) and stdin verdicts MUST keep the fingerprint-only rule.

**F5 — empty `--ids`**
- **FR-016**: In `ingest-grounding` and `ingest-verdict`, an `--ids` value that is empty after trimming (including only
  commas and spaces) MUST be treated as not given; with `--repaired` it MUST still be `IDS_REQUIRED`.

**F6 — `--dry-run` on the batch ingests**
- **FR-017**: `ingest-verdict --all --dry-run`, `ingest-grounding --all --dry-run` and `ingest-analysis --dry-run` MUST
  produce the same JSON (plus `dry_run: true`), the same human summary and the same exit code as a real run, and MUST
  write nothing: no grounding blocks, no verdict or record changes, no plan or analysis file, no drop trail, no lock.
- **FR-018**: `--dry-run` with `ingest-grounding --test` MUST exit 1 with "--dry-run requires --all" and write
  nothing. `ingest-verdict` (read-only in both forms) MUST accept `--dry-run` in both forms and add `dry_run: true`.
- **FR-019**: The constitution IV rows for these three commands MUST be removed from 091's and 094's Complexity
  Tracking.

**F7 — the critic model**
- **FR-020**: The config MUST accept an optional `ai.critic.model`: a single token of up to 200 characters that starts
  with a letter or digit and otherwise uses letters, digits, `.`, `-`, `_`, `:`, `[`, `]`
  (`^[A-Za-z0-9][A-Za-z0-9._:\[\]-]{0,199}$`, so the unquoted frontmatter value is always a YAML string) — an alias
  (`sonnet`, `opus`, `haiku`), a model id, or `inherit`. Missing (key, `ai.critic` or `ai`) means `sonnet`. Any other
  value MUST be invalid (`CONFIG_INVALID` naming the key).
- **FR-021**: The init template MUST include `ai.critic.model: "sonnet"`.
- **FR-022**: With a valid config (see FR-022a otherwise), `init` and `update-skills` MUST install the critic agent with
  `model: <ai.critic.model>` in its
  frontmatter; the bundled content they compare with (096 manifest rule) is the agent with that model. An edited agent
  file MUST never be overwritten.
- **FR-022a**: `update-skills` MUST NOT fail on a config it reads only for the critic model. No config → the default
  model. A config invalid for any reason (malformed JSON, an unrelated out-of-range or duplicate key, an invalid
  `ai.critic.model`) → one warning `spectra.config.json is invalid (<error_code>: <first error>); the critic agent's model was not changed`; the critic agent keeps the model line it has installed (default
  `sonnet` when it isn't installed); every other file is refreshed as usual; exit 0. A valid config → the configured or
  default model is applied. `init` keeps the 083 rule for its own config.
- **FR-023**: The in-process critic remnants that no production code uses (critic factory, critic model resolver, and
  the old critic config model and its unused keys) MUST be removed, with their self-tests; the retired-key notice
  (`ai.critic.provider`, `ai.critic.api_key_env`, `ai.critic.base_url`, `ai.fallback_strategy`) MUST stay.
- **FR-024**: CLAUDE.md's stale sentence about the critic model selector MUST be corrected.

**Cross-cutting**
- **FR-025**: Every fix MUST have a regression test that fails on `807047c` (skill text guards included).
- **FR-026**: No config section becomes required (083): `ai.critic` is optional; `ConfigRuleMatrixTests` and
  `InitTemplateWorkspaceTests` stay green.
- **FR-027**: Human summaries MUST stay in JSON mode (stderr).
- **FR-028**: The test suite MUST stay fully green.
- **FR-029**: Docs: cli-reference (audit filters and `stale`, repair refusals, `--dry-run`, `ai.critic.model`),
  usage.md, configuration.md, CHANGELOG.md, a short CLAUDE.md entry.

### Key Entities

- **Grounding block**: the `grounding:` frontmatter of a test — the claim that a critic checked it.
- **Verdict file**: `.spectra/verdicts/critic-verdict-<id>.json`, the critic's answer.
- **Fingerprint record**: `.spectra/verdicts/critic-input-<id>.json`, written when the critic prompt is compiled
  (`fingerprint`, and since 091 `compiled_at`). Its file time becomes the freshness reference.
- **Audit entry**: one test of the suite with its verdict, block state, `action_needed`, and now `stale`.
- **Critic model setting**: `ai.critic.model`, rendered into the installed critic agent's frontmatter.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a run where the critic confirms k of n tests, exactly k grounding blocks are written and the written
  count equals k (no false 075 warning).
- **SC-002**: On a two-suite workspace, the resume path's audit output lists only the suite's tests that need repair —
  0 entries of the other suite, 0 stray verdicts.
- **SC-003**: 0 repair prompts are produced from verdicts the ingest would refuse as stale.
- **SC-004**: With a clock skew of ±1 h between the record's `compiled_at` and the file system, new verdicts are accepted
  and old ones refused (100 % agreement with the file-time rule).
- **SC-005**: Each of the three batch ingests with `--dry-run` leaves the workspace byte-identical and reports the same
  outcome and exit code as the real run.
- **SC-006**: A new workspace's critic agent runs on the configured model (default `sonnet`); changing the key and
  running `update-skills` changes an unedited agent and 0 edited ones.
- **SC-007**: Every fix has a test that fails on `807047c`, and the whole suite passes with 0 failures.

## Assumptions

- The skills already hold the kept-grounded ids (8a) and the group's grounded ids (5a) from the CLI's per-test
  `results` (093); no new CLI output is needed for F1.
- The critic agent's frontmatter `model` value accepts Claude Code's aliases, full model ids and `inherit`; SPECTRA
  doesn't check that a model exists.
- `sonnet` as default keeps today's behaviour (the pinned id is a Sonnet model) without pinning a version.
- The workspace's verdict and record files are written by the same file system, so comparing their file times is
  meaningful even when the CLI's clock is not.
- Verification uses a workspace from the real `spectra init` and a pre-097 workspace (090 and 091 records, verdict
  files of two suites, an installed critic agent with the pinned model).

## Out of Scope

- 098: baseline and generation precision, the 096 review findings, the build warnings.
- 099: docs, CLAUDE.md trim, constitution.
- `--dry-run` for `ingest-grounding --test` (refused instead, FR-018).
- Checking that a configured critic model exists or is available to the user's account.
