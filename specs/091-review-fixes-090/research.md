# Research: Review Fixes for Spec 090

All facts verified on `5c10c44`. No NEEDS CLARIFICATION remained in the spec; these are the design decisions.

## R1 — Where the time rule lives, and what "older" means (F1)

**Decision**: `VerdictRecord` gains an optional `compiled_at` (`DateTimeOffset?`, UTC, ISO 8601, written by
`compile-critic-prompt` as the record's write time). `VerdictFreshness.Check(root, id, test, verdictPath)` takes the
path of the verdict file actually being ingested and returns `Stale` with reason `OlderThanPrompt` when
`File.GetLastWriteTimeUtc(verdictPath) < compiled_at − 2 s`. The fingerprint comparison is unchanged and checked first
(reason `ContentChanged`, the 090 message). A record without `compiled_at`, or a null/missing verdict path, gets the
fingerprint rule only. No record → `NoRecord` (accepted).

**Rationale**:
- One function already decides freshness for all four commands (`ingest-verdict --from` via
  `CheckRecordedTestAsync`, `ingest-verdict --all`, `ingest-grounding --test`, `ingest-grounding --all`), so the
  rule lands in one place.
- Passing the ingested path (not always the canonical `critic-verdict-<id>.json`) covers `ingest-grounding --test
  --from <file>`: the file actually written is the one whose age matters.
- The 2-second allowance covers coarse file-system timestamps (FAT, some network shares) and the gap between the
  process clock (`compiled_at`) and the file-system clock (mtime). The failure it guards against — a verdict from
  before a hand edit — is older than the compile by far more than 2 s. Equal or later times are fresh (spec edge case).
- Times, not contents: a verdict carries no timestamp, so a re-run can be byte-identical (FR-005).

**Alternatives considered**:
- Compare the verdict's mtime with the record file's mtime (same clock): equivalent in practice, but the request asks
  for a stored `compiled_at`, which also makes the record self-describing in JSON.
- Delete the old verdict when compiling: rejected by the spec (compile deletes and renames nothing).
- Hash the verdict into the record: impossible — the verdict doesn't exist yet at compile time.

## R2 — Compatibility with 090 records (F1)

**Decision**: `compiled_at` is an optional constructor parameter with a default, so 090 records (no key) deserialize
with `null` and keep the fingerprint-only rule. The writer omits nothing: every record written from 091 on has
`compiled_at`. An unparseable `compiled_at` makes the record unreadable, which is treated as no record (090 rule for
unreadable records).

**Rationale**: no migration; a 090 record gains `compiled_at` the next time the critic prompt is compiled for that test.

## R3 — `--ids` semantics (F2)

**Decision**:
- `--ids a,b` on `ingest-grounding`: split on `,`, trim, drop empties, distinct, case-insensitive (as the index lookup).
- `--ids` without `--all`, and `--test` together with `--all` (today `--all` silently wins), → the invalid-combination
  path: plain stderr message, exit 1 (like "Either --test <id> or --all must be specified."), nothing written. Not a new
  code. No skill, doc or test combines `--test` and `--all` (checked).
- `--all --repaired` with no ids (option absent or empty after trimming) → `IDS_REQUIRED`, exit 4, through
  `CriteriaCommandIO.WriteFailure` (shared shape), checked before the config, index or any verdict is read.
- With ids, the batch considers only verdict files of listed ids; listed ids not in the suite index → `not_found`
  (computed before the verdict folder check, so it is reported even when `.spectra/verdicts/` is missing). An id in the
  index without a verdict file contributes nothing, as today.
- `not_found` never changes the exit code.

**Rationale**: the skills know exactly which tests they re-critiqued; making the CLI require that list for `--repaired`
removes the whole class of "stamped as repaired by accident", including interrupted runs.

**Alternatives considered**: deriving the repaired set from the staging folder or from file times (fragile, and an
interrupted run leaves no reliable trace); a `--since` time filter (doesn't exclude a failed repair whose old verdict is
newer than the cut-off).

## R4 — Hallucinated in `--all` (F3)

**Decision**: the batch parses each verdict once (after the Manual / already-written / stale checks, which keep their
order). A parseable verdict whose `verdict` is `hallucinated` → added to `hallucinated`, counted in
`skipped_hallucinated`, not written. Unreadable, empty or unparseable files and failed writes stay `errors`. The partial
pre-repair filter reuses the same parse. Single `--test` is unchanged (exit 4 refusal).

**Rationale**: the update flow keeps such tests for the user; the generation flow deletes them (file gone → skipped
silently, unchanged), so only kept tests reach the new branch.

## R5 — Human summary (F2, F3)

**Decision**: the batch's human summary gains one line per hallucinated id (`TC-140: hallucinated, waiting for
review`) and one per unknown id (`TC-999: not found in suite 'storage'`). In human mode it goes to stdout as today; in
JSON mode the whole human summary (the existing one-line counts plus these lines) goes to stderr, following the project
rule that human summaries are never suppressed in JSON mode. The JSON on stdout keeps its shape plus the new fields.

**Rationale**: the spec requires the names in the human summary; 090 left the batch silent on stderr in JSON mode,
which the project rule doesn't allow. No existing test asserts an empty stderr for this command (checked).

## R6 — Skill changes (F2, F3)

**Decision**:
- `spectra-generate` Step 8b: the post-loop call becomes `spectra ai ingest-grounding --suite {suite} --all --repaired
  --repair-attempts 1 --ids {ids} --output-format json`, where `{ids}` is the comma-separated list of tests re-critiqued
  in this repair round (repair ingest reported `written` or `unchanged`) minus those dropped in the re-critic phase. If
  that list is empty, the call is skipped. A sentence says `skipped_hallucinated` is not a failure (report the ids).
  The NON-STOP contract and the written==0 guard text are not touched (the guard is about the post-8a call).
- `spectra-update` Step 5a step 4: `--ids <id>` (the per-test re-critic).
- `spectra-review-flagged`: unchanged (single `--test`).

## R7 — Critic agent text and the leftover doc line (F4)

**Decision**: `spectra-critic.agent.md` step 1: "It writes only the fingerprint record
(`.spectra/verdicts/critic-input-<id>.json`, with the time it was compiled) — never the verdict — and calls no model."
`docs/cli-reference.md` line ~470 ("Writes nothing; calls no model") is corrected in the same pass (a 090 leftover).
Workspaces get the new agent text through `spectra update-skills` (unmodified copies only, as for every bundled file).

## R8 — Deliberately changed assertions

| Test | Change | Why |
|---|---|---|
| `IngestGroundingCommandTests.AllMode_WritesPartialWithRepaired` | add `--ids TC-504` | FR-007 |
| `RepairLoopAfterEditTests.TheGenerationRepairLoop_StillWritesTheRepairedVerdict` | add `--ids TC-102`; `skipped_already_written` 1 → 0 (TC-101 is no longer considered) | FR-006/FR-007 |
| `SkillsManifestTests` rows asserting the `--repaired --repair-attempts 1` call | still pass (substring); new rows assert `--ids` | FR-009 |

Any other `--all --repaired` caller found during implementation is listed in tasks.md Notes.

## R9 — Verification workspaces (083 lesson)

**Decision**: T1–T7 run on `VerdictWorkspace` / `UpdatePromptWorkspace` created with the real `spectra init`
(`InitWithRealTemplateAsync`). A pre-091 row writes a 090-format record (no `compiled_at`) plus a verdict file and a
grounding block, then runs all four commands (T3). `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` stay green.
