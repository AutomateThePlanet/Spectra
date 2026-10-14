# Research: Verification Hardening (Phase 6, Part 2)

All findings were checked against `807047c` (the 096 merge). No NEEDS CLARIFICATION remains.

## R1 — F1: where the skills get the id lists

**Decision**: Only the two call lines and their id lists change.
- Generate 8a gets `--ids {kept-grounded ids}`, and the call is skipped when there are none.
- Update 5a gets `--ids {the group's grounded ids}`, also skipped when empty.

The ids come from what the skills already hold: 093's per-test `results` of `ingest-verdict --all --ids`, filtered by
the critic's own line. The 075 guard text (`GROUNDING-INGEST ANOMALY` / `WARNING`) and the NON-STOP contract stay
byte-identical. The skill guards compare them with the 807047c text.

**Rationale**: `ingest-grounding --all --ids` exists since 091 (`not_found`, per-id filtering). Today the call is made
without ids, so every fresh `grounded` verdict on disk gets a block: unverified tests and leftovers of other runs.

**Alternatives**: a CLI rule that skips tests "reported unverified" — rejected, because the CLI can't know what the
skill reported.

## R2 — F2: audit scope, filters and staleness

**Decision** (`AuditGroundingHandler`):
- **Scope.** Verdict files are read only for ids in the suite index (`indexLookup`). Today the handler globs every
  `critic-verdict-*.json` and lists stray ones with `file: null` and `action_needed: repair`. Entry order is unchanged:
  verdict-file entries first, by file name, then block entries.
- **Staleness.** For a verdict-file entry whose test parses, `VerdictFreshness.Check(root, id, test, verdictPath)`
  runs. When it's stale, the entry gets `stale: true` and `action_needed: "critic"`, whatever the block says. Fresh
  entries and entries without a record omit `stale` (the field is null-ignored), so the JSON only grows.
- **Summary.** It gains `stale`, and it is computed over all of the suite's entries before filtering.
  `partial_pending_repair` counts `action_needed == "repair"` as before, so a stale entry moves from repair to critic.
- **Filters.**
  - `--action` is an `Option<string[]>` with `AllowMultipleArgumentsPerToken = false`, so it is repeatable; each value
    is split on commas, trimmed and lowercased. The allowed set is `critic`, `repair`, `review`, `none`. An unknown
    value exits 1 on plain stderr: `--action must be one of critic, repair, review, none (got '<v>').`
  - `--ids` uses `IdList.Parse`. An empty list counts as not given (R5). Ids missing from the index go to
    `not_found` (JSON, present only when `--ids` was given), and the human output says `<id>: not found in suite '<s>'` (on stderr in JSON mode, FR-009a).
  - Filters apply to `tests` only.
- **Human table.** It shows `critic (stale)` in the Action column, and the summary line gains `| N stale` when N > 0.
- **No-entries case.** The empty-list message is unchanged. `EmitEmpty` gains `stale: 0`.

**Rationale**: The skill's resume path needs only the `repair` ids. Reporting a verdict the ingest would refuse as
current makes the audit lie.

**Alternatives**:
- Filtering the summary too — rejected: the summary is the suite overview the anomaly path prints.
- A separate `stale` action value — rejected: the action for a stale verdict *is* running the critic again.

## R3 — F3: repair commands

**Decision**:
- **`compile-repair-prompt`.** After the test parses and the verdict is found, but before classification,
  `VerdictFreshness.Check(currentDir, testId, test, verdictPath)` runs. Stale → `Console.Error` gets
  `VERDICT_STALE: <FreshnessResult.Message(id)>` and the exit code is 4 (`ExitRefused`). The command stays plain-stderr,
  as all its refusals are today. `--from` pointing outside `.spectra/verdicts/` still gets the check: the record is
  keyed by id. The time rule applies only when `--from` is the file the record describes, i.e. the default path. A
  custom `--from` gets the fingerprint rule only, as `ingest-grounding --from` does since 091.
- **`compile-repair-batch`.** Stdout stays the JSON array (the manifest consumers parse). A stale test is skipped, and
  stderr gets one JSON line `{"command":"compile-repair-batch","stale":[{"id":…,"reason":…}]}` plus a human line
  `<id>: stale verdict — run the critic again`, only when there are stale tests. This is the 094 precedent of a stderr
  JSON line next to a stdout payload. The `stale` list is in the same order as the verdict files.

**Rationale**: Changing stdout from an array to an object would break the existing consumers (docs, tests, any user
script) for one extra list.

**Alternatives**: wrapping the array in an object with `prompts` and `stale` — rejected, for the reason above.

## R4 — F4: the reference clock

**Decision**: `VerdictFreshness.Check` takes its reference from the record file, when the record has `compiled_at`:
`File.GetLastWriteTimeUtc(RecordPath(root, id))`. It falls back to `compiled_at` when the file time can't be read (IO
error, or a record read from somewhere without a file time).
- `WriteRecordAsync` already writes a new temp file and moves it, so every compile sets the record's file time. The
  move keeps the temp file's write time, which was set by the same file system as the verdict.
- `FreshnessResult` keeps `CompiledAt` for the message. A new `ReferenceAt` holds the time actually compared, and the
  message names it: `prompt compiled {ReferenceAt:u}`.
- 090 records (no `compiled_at`) and stdin verdicts keep the fingerprint-only rule. The 2 s allowance stays.

**Rationale**: Comparing two times taken by one file system removes the CLI-clock/file-system skew. Keeping
`compiled_at` as the gate keeps 091's promise for 090 records.

**Alternatives**:
- Write a time read back from the file system (touch a probe file) — rejected: an extra write.
- Drop the time rule — rejected: it's 091's fix for stale verdicts with an unchanged fingerprint.

**Impact on existing rows**: `VerdictOlderThanPromptTests` sets verdict times relative to `compiled_at` or now. The
record file is written within milliseconds of `compiled_at`, so those rows should hold. Any row that relies on
`compiled_at` being far from the record's file time is retargeted, and listed in the Notes.

## R5 — F5: empty `--ids`

**Decision**: In both commands, `idsGiven` becomes `IdList.Parse(value).Count > 0`; today it is
`FindResultFor(idsOption) is not null`.
- `ingest-grounding --all --repaired --ids ""` → `IDS_REQUIRED` (exit 4), unchanged.
- `--ids ""` without `--all` is "not given", so there is no usage error.

`audit-grounding --ids` uses the same rule.

**Rationale**: 091 FR-006 says an empty list counts as missing. A skill that builds `--ids` from an empty list must not
silently process nothing.

## R6 — F6: `--dry-run`

**Decision**: Read the existing global `GlobalOptions.DryRunOption` in each handler (it is parsed today and ignored).
- **`ingest-grounding --all --dry-run`.** `GroundingWriteBackService.WriteAsync(..., dryRun)` computes the same result
  (same outcomes, same `Grounding`) and skips `TestFileWriter.WriteAsync`. Counts, `written`, `not_found`, the summary
  and the exit code are as in a real run, and `dry_run: true` is added to the JSON. `--test` with `--dry-run` → exit 1
  `--dry-run requires --all` (plain stderr). It is checked after the existing argument checks and before any read.
- **`ingest-verdict`.** Both forms only read and classify; neither writes anything. So `--dry-run` is accepted in both
  and adds `dry_run: true`. Nothing else differs.
- **`ingest-analysis --dry-run`.** Everything up to `GenerationPlanStore.SaveAsync` runs (config load, parse, coverage,
  plan build). The save is skipped. The JSON and summary are identical (the plan path is still named), plus
  `dry_run: true`. `PLAN_WRITE_FAILED` can't be known without writing, so it isn't simulated (spec edge case). Config
  refusals (`CONFIG_MISSING` / `CONFIG_INVALID`) happen as in a real run.
- **Docs.** In 091's and 094's plan.md, the constitution IV row of the Constitution Check becomes "resolved by 097",
  and the Complexity Tracking rows for these commands are removed.

**Rationale**: The flag is accepted today and ignored, which is the worst case. Constitution IV requires it.

**Alternatives**: refusing `--dry-run` everywhere — rejected, since the constitution asks for support.

## R7 — F7: the critic model

**Decision**:
- **Config.** `AiConfig.Critic` (`ai.critic`, optional) is a trimmed `CriticConfig` with only `Model` (`model`). The old
  `enabled`, `timeout_seconds`, `max_concurrent`, … members are removed; their keys deserialize as unmapped and are
  ignored. The effective model is `Critic?.Model ?? "sonnet"`, through `CriticConfig.DefaultModel` / an
  `EffectiveModel` helper on `AiConfig`.
- **Validation.** `ConfigLoader.Validate` checks `ai.critic.model` when present: it must match
  `^[A-Za-z0-9][A-Za-z0-9._:\[\]-]{0,199}$` (analyze U1: a value starting with `[` would be read as a YAML list in the
  unquoted frontmatter). Otherwise `INVALID_CRITIC_MODEL` → `CONFIG_INVALID` with the text
  `ai.critic.model must be one word (an alias such as sonnet, a model id, or inherit)`. A non-string value fails
  deserialization, which is already `CONFIG_INVALID`. `"ai": {"critic": null}` means the default (the `ai` section is
  already filled silently).
- **Template.** The bundled `spectra-critic.agent.md` gets `model: {{CRITIC_MODEL}}`. `AgentContent.Render(name, content,
  criticModel)` replaces it at install, and only for the critic. `AgentContent.All` keeps the raw placeholder for tests
  of the text; a rendered view feeds init and update-skills.
- **Init.** The init template gains `"critic": { "model": "sonnet" }` under `ai`, and its `_comment_ai` is updated.
  Init writes that template and then installs the agent rendered with the template's value, read through
  `ConfigLoader.LoadForReadAsync` after the config is written.
- **update-skills** (analyze U2). It loads the config with `LoadForReadAsync(None)` only for the critic model, so it
  never fails on it:
  - no config → the default `sonnet`;
  - a valid config → the configured or default model;
  - invalid for any reason (malformed JSON, an unrelated out-of-range or duplicate key, an invalid `ai.critic.model`;
    the loader's `ErrorCode` and first error) → one warning `spectra.config.json is invalid (<error_code>: <first
    error>); the critic agent's model was not changed`. The critic is rendered with the `model:` value read from the
    installed agent's frontmatter (`sonnet` when the agent isn't installed or has no model line). Every other file is
    refreshed as usual, and the exit code is 0.

  It renders the critic with the model chosen above; the bundled hash is the rendered content's (096 manifest rule).
  `init` keeps the 083 rule for its own config.
  - An unedited agent from before 097 (pinned `claude-sonnet-4-6`, hash = record) is updated.
  - An edited one is skipped as customized.
  - Changing the key and rerunning updates an unedited agent.
- **Removals.** `CriticFactory`, `CriticModelResolver` and their tests (`CriticFactoryTests`, `CriticAuthTests`) go.
  Other tests of the old `CriticConfig` members are removed or retargeted (inventory in tasks). `DetectDeprecatedKeys`
  and the notice stay.
- **Docs.** CLAUDE.md's Tech Stack sentence and cli-reference's "`ai.critic.model` (default `claude-sonnet-4-6`)" line
  are corrected.

**Rationale**:
- An alias doesn't go stale.
- A per-workspace setting keeps the critic independent of the generator by default.
- Rendering at install is the only way the setting reaches Claude Code, which reads the agent file.

**Alternatives**:
- `model: inherit` (option a) — rejected by the spec's decision: the critic would run on the generator's model.
- Reading the config at critic time — impossible: Claude Code picks the model from the frontmatter.

## R8 — Verification workspaces

**Decision**:
- **Real init.** The real-init rows use `BaselineTestWorkspace.InitWithSkillsAsync` (096 fixture).
- **Pre-097 workspace.** The pre-097 rows build one workspace with:
  - 090 records (no `compiled_at`) and 091 records;
  - verdict files of suites A and B, plus one stray verdict;
  - the critic agent as 807047c installed it (`model: claude-sonnet-4-6`, recorded hash);
  - a config without `ai.critic`.
- **Proof.** It runs on untouched `src` at `807047c`. Rows that need new members are staged out, as in 096.

## R9 — What the skill guards pin

**Decision**: These rows check the content of the installed skill (`SkillsManifestTests`-style):
- generate 8a: the `--ids {kept-grounded ids}` call line and the "skip when empty" sentence;
- update 5a: the `--ids` line;
- generate resume: the `--action repair` call;
- the 075 guard blocks and the NON-STOP contract: equal to the 807047c text.

The equality is checked against a copy of the 807047c text in the test, so it fails if either is edited.

**Rationale**: FR-003 requires the guard text to stay verbatim.
