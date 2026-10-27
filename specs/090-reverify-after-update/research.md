# Research: Re-verify After Update (Phase 4)

All facts were checked against `fc82b7a`. FR-C1's scope was decided by the user on 2026-10-08 (R6): scalar only, with a
warning for list forms. No other clarifications are open.

## R1 — An edit drops the critic verdict (FR-A1)

**Decision.**
- `UpdatedTestIngestor.ApplyEdit` keeps the original's `Grounding` only when its verdict is `Manual`; otherwise it sets
  `Grounding = null`. The 078 writer then drops the `grounding:` entry in the same write, with nothing else moving.
- `IngestResult` gains `ManualVerdictKept`.
- After a written outcome, the command calls `VerdictFiles.RemoveAsync(root, id)`. It deletes
  `.spectra/verdicts/critic-verdict-<id>.json` and the fingerprint record `critic-input-<id>.json` (R2), in both the
  single form and `--all`, with or without `--keep-baseline`. A `--dry-run` deletes nothing (R5).
- `unchanged` and `unchanged_pending` don't persist, so they never reach this code.
- The JSON adds `manual_verdict_kept` (single: bool; batch: id list). The human line for such a test reads
  `TC-101: updated (manual verdict kept)`.

**Rationale.** A verdict describes the content it judged, so the write that changes the content must drop it. A Manual
verdict is a human judgement that the critic must never replace (FR-A3). Removing the block, rather than marking it
stale, lets the existing skip-if-present rule of `ingest-grounding --all` accept the new verdict (no `--replace` needed).

**Generation flow (FR-E2).**
- In Step 8b a partial test has no grounding block, because 8a writes grounded verdicts only.
- Its repair (`ingest-update --all --keep-baseline`) therefore removes only the old verdict file and record. The
  re-critic writes new ones, and `ingest-grounding --all --repaired --repair-attempts N` writes the block.
- The `written == 0` guard is about 8a's `ingest-grounding` and is untouched. T4 asserts this sequence end to end on
  the CLI.
- The flagged-review skill (`--test-id --from .spectra/repaired.json --keep-baseline`, then re-critic, then
  `ingest-grounding --test`) gets the same treatment. The block it replaces was a flagged partial, which FR-A1 now
  removes before the re-critic.

## R2 — Verdict fingerprint (FR-A2)

**Decision.**
- **Fingerprint.** `Verification/TestContentFingerprint.Of(TestCase)` returns `"sha256:" + hex(SHA-256(UTF-8(
  UpdatePromptCompiler.SerializeTest(test))))`, normalised to LF. `SerializeTest` covers id, title, priority, tags,
  component, preconditions, steps, expected result, test data, `source_refs`, scenario and `criteria`, which is what
  the critic judges. It excludes grounding, so writing a verdict's block doesn't make that verdict stale.
- **Record.** `compile-critic-prompt` (suite mode and legacy path mode) writes, per test it compiles successfully,
  `.spectra/verdicts/critic-input-<id>.json`, atomically: `{"id","suite","file","fingerprint"}`. `suite` is null in
  legacy path mode, and `file` is repo-relative. It writes nothing on a refusal. This makes `compile-critic-prompt` a
  writing command for one scratch file, inside the already-gitignored `.spectra/verdicts/`.
- **Check.** `VerdictFreshness.Check(root, id, current)` returns `Fresh`, `Stale` or `NoRecord`:
  - `ingest-verdict --suite --all`: a stale verdict is not classified and is reported in `skipped_stale` (count and
    ids).
  - `ingest-verdict --from <file>` (single): the id comes from the file name `critic-verdict-<id>.json` and the record
    from the same folder. The test is read from the record's `file`. A stale verdict exits 4 with `VERDICT_STALE`.
    Stdin, or a file without a matching record, gives `NoRecord` and is accepted as today.
  - `ingest-grounding --test` (single): checked before the write. Stale → exit 4 `VERDICT_STALE`.
  - `ingest-grounding --all`: stale → `skipped_stale`.
  - `NoRecord` (pre-090 verdicts) is accepted everywhere.

**Rationale.** The record is written by the CLI, not by the model, so it can be trusted. The critic subagent already
runs `compile-critic-prompt` for each test (generate skill 8a.1), so every 090 verdict gets a record without any skill
change.

**Alternatives.**
- The critic echoing the fingerprint in its JSON: model-written, so untrusted.
- A fingerprint inside the verdict file: the subagent writes that file.
- A whole-file hash: writing the grounding block would make the verdict stale.

## R3 — Manual verdicts are never overwritten (FR-A3)

**Decision.**
- `ingest-grounding --test`: when the test's current grounding verdict is `Manual`, refuse with exit 4
  `MANUAL_VERDICT` in the shared failure shape (`CriteriaCommandIO.WriteFailureJson`). Nothing is written.
- `ingest-grounding --all`: checked before the existing already-written skip, and counted as `skipped_manual` (count and
  ids). The existing `skipped_already_written` then counts only non-Manual blocks.
- Exit 4 already means a refusal in `ingest-grounding` (hallucinated), so the new codes join that class.

## R4 — Failed staging files (FR-B1)

**Decision.**
- In `RunBatchAsync`, after the lock body and only on the paths where 088 would delete consumed files, each `failed`
  outcome's file is renamed with `File.Move(path, <folder>/failed-<id>.json, overwrite: true)`. Its `file` in `results`
  becomes the new path.
- Each `written`, `unchanged` or `unchanged_pending` outcome deletes `<folder>/failed-<id>.json` when present.
- Enumeration stays `prefix + "*.json"` (`updated-` / `repaired-`), so `failed-*` is never read.
- When the baseline write fails after a persist (exit 1), nothing is renamed or deleted (088 rule). The files stay
  where they are and a re-run picks them up.
- The human line for a failure becomes `(kept .spectra/updates/<s>/failed-<id>.json)`.

**Rationale.** A distinct name keeps the file for inspection (the user or the skill reads the error) while removing it
from every later batch. Overwriting an older `failed-` file keeps one per id.

## R5 — `ingest-update --dry-run` (FR-B2)

**Decision.**
- A `--dry-run` option on the command, for both forms.
- `UpdatedTestIngestor.IngestAsync(…, persist: bool = true)`. With `persist: false` it returns the same result
  (Success with the edited test, NoChange, or Failure) without calling `TestPersistenceService`.
- **Dry run:**
  - loads the baseline with `BaselineStore.LoadSuiteAsync` (strict read, no `WithLockAsync`);
  - runs the same pending check (R1 of 089);
  - computes the same outcomes and exit code;
  - records no baseline, deletes no staging or verdict file, and renames nothing;
  - adds `"dry_run": true` and a zero `baseline` summary to the JSON;
  - prints the human summary with a `Dry run:` prefix line.
- A batch's later files are judged against the original tests (nothing was written), which is what the real run would
  see for distinct ids.

**Rationale.** The same code path with persistence switched off guarantees the dry run equals the real run (SC-004),
and T7 asserts it row by row. A strict read without the lock matches what other read-only dry runs do (`update-triage
--dry-run`).

## R6 — Safe delete (FR-C1–FR-C3)

**Decision (user, 2026-10-08): scalar only, warn on lists.** A new `Commands/Delete/TestDeletionService`:

**`PlanAsync(workspace, ids)`:**
- Resolves each id to its file through the suite `_index.json` entries, falling back to parsing each `.md` with
  `TestCaseParser` and matching the parsed `Id`. No `id:` regex.
- **Scalar dependents:** every parseable test in every suite whose parsed `DependsOn` (trimmed, case-insensitive)
  equals a deleted id.
- **List dependents (warnings):** files that fail to parse but whose frontmatter, parsed on its own with YamlDotNet's
  `YamlStream`, has a `depends_on` sequence (block or flow) containing a deleted id. The dependent's id comes from that
  frontmatter's `id`. These files are never written.
- **Baseline entries:** the deleted ids present in their suite's `_source-baseline.json`.
- **Staging files:** `.spectra/updates/<suite>/{updated,failed}-<id>.json` and
  `.spectra/repairs/<suite>/{repaired,failed}-<id>.json`.
- **Verdict files:** `.spectra/verdicts/critic-verdict-<id>.json` and `critic-input-<id>.json`.

**`ApplyAsync(plan)`**, in this order:
1. update the suite indexes (existing);
2. rewrite each scalar dependent as `test with { DependsOn = null }` through `TestFileWriter` (078: the key and its
   prelude are dropped, every other byte is kept);
3. remove the baseline entries under `BaselineStore.WithLockAsync` (new `BaselineStore.RemoveEntriesAsync(root,
   testsPath, suite, ids)`: strict load, remove the keys, atomic save, no prune);
4. delete the staging and verdict files;
5. delete the test files last (existing).

`DeleteHandler` uses the service for plan, dry-run and apply. Its JSON adds `baseline_entries_removed`,
`staging_files_removed`, `verdict_files_removed` and `warnings` (list-form dependents); `--dry-run` lists all of them.
`DependsOnLineRegex`, `StripDependsOnAsync` and the `id:` regexes are removed.

Legacy `UpdateHandler.ApplyChangesAsync` deletes its `ToDelete` proposals through `TestDeletionService`, with
automation checks off, as today.

**Notes.**
- A comment line directly above `depends_on:` is dropped with the key. That is the 078 writer's documented rule for a
  removed key, and is covered by T8.
- `spectra suite delete` (whole suite) isn't in scope. It removes the suite folder and its baseline file, but not
  `.spectra/updates/<suite>/` and the like. This is recorded as a finding.

**Rationale.** It goes through the parsed model and the lossless writer, as FR-C1 requires. List forms are unreadable to
every other command, so editing them as raw text would be the only exception, and the user chose not to.

## R7 — Gitignore (FR-D1–FR-D3)

**Decision.**
- **Manifest.** `SkillsManifest` gains `gitignore_offered` (`List<string>?`; null means the workspace predates 090).
- **`SpectraGitIgnore`:**
  - `Patterns`: unchanged list.
  - `Pre090Offered`: `Patterns` minus `.spectra/updates/` and `.spectra/repairs/`.
  - `IsPresent(lines, pattern)`: compares whole trimmed lines, skips lines starting with `#`, and treats `p` and
    `p.TrimEnd('/')` as equal.
  - `IsNegated(lines, pattern)`: a line `!p` or `!p-without-slash`.
  - `AppendAsync(path, patterns)`: reads the text, takes the newline from `Newlines.Detect`, falls back to `\n` when
    the file has none, adds one newline first only if the content isn't empty and doesn't end with `\n`, then
    `# SPECTRA` (only when absent) and the patterns, each with that newline. Existing bytes are never changed.
- **`init`:** a missing `.gitignore` is created with the full list (as today). An existing one gets the patterns that
  are neither present nor negated, through `AppendAsync`. Every pattern is recorded as offered.
- **`update-skills`:**
  - The candidates are the patterns not in `gitignore_offered`, or not in `Pre090Offered` when the record is null.
  - With no `.gitignore`, it warns `no .gitignore; add: <patterns>` and records nothing.
  - Otherwise it records every candidate that is already present or negated as offered, appends the rest and records
    those too.
  - It reports `Updated: .gitignore (added: …)`.
- **Newlines.** The newline detection moves from the private `TestCaseParser.DetectNewline` to a public
  `Spectra.Core.Text.Newlines.Detect`, used by both.

**Rationale.** A user who removes a pattern deliberately must not get it back; recording what was offered is the only
way to tell "removed" from "never offered". Byte-preserving appends keep a user's file style intact (089 findings 3–4).

## R8 — The update skill's re-verify step (FR-A4)

**Decision.** `spectra-update` gains **Step 5a: Re-verify edited tests**, after each group's ingest, for `results`
entries with outcome `written`, except ids in `manual_verdict_kept`.
- **Ask once per run** (before the first group's re-verify): "Re-verify the edited tests with the critic now?
  (recommended)". If declined, collect them as unverified.
- **Per test:** the `spectra-critic` subagent through the Task tool (suite and id only, the same procedure as the
  generate skill's 8a.1: it runs `compile-critic-prompt --suite <s> --test <id>`, which writes the fingerprint record,
  and writes the verdict file).
- **Then once:** `spectra ai ingest-verdict --suite <s> --all`, then `spectra ai ingest-grounding --suite <s> --all`.
- **Read each verdict file:**
  - **grounded** → done.
  - **partial** or **hallucinated** → one repair: `spectra ai compile-repair-prompt --suite <s> --test <id>`, patch,
    Write `.spectra/repairs/<s>/repaired-<id>.json`, `ingest-update <s> --all --keep-baseline`, re-critic,
    `ingest-verdict --suite <s> --all`, `ingest-grounding --suite <s> --all --repaired --repair-attempts 1`.
  - **Still partial** → flagged for review (existing behaviour).
  - **Still hallucinated** → stop for that test and ask the user: edit again, restore the previous version from git
    (the user runs it), or delete through the `spectra-delete` skill. Never dropped or deleted by this skill.
- **Step 7's summary** gains grounded / partial-flagged / hallucinated / unverified / manual-kept counts with ids.
- The NON-STOP allowed writes gain `.spectra/repairs/<suite>/repaired-<id>.json`. The "never run delete" rule stays.

**`compile-repair-prompt` accepts hallucinated verdicts.** It now refuses only grounded ones (it refuses everything
except partial today). The generation flow drops hallucinated tests before any repair, so its behaviour is unchanged
(FR-E2). The update flow needs one repair attempt for a test that existed before the edit. This changes an existing
command and is recorded in the Notes.

## R8a — `audit-grounding` shows unverified tests (FR-A7, analysis I1)

**Decision.** `AuditGroundingHandler` keeps its verdict-file entries exactly as they are, then adds one entry per
indexed test that has no verdict file. The source of truth is the test's parsed grounding block:
- a critic block → its verdict and score, `grounding_written: true`, and `action_needed` from the existing rule
  (`review` if flagged, else `none`);
- `manual` → `verdict: "manual"`, `grounding_written: true`, `action_needed: "none"`;
- no block → `verdict: "none"`, `grounding_written: false`, `action_needed: "critic"`.

The summary gains `unverified` (entries with verdict `none`). The order is the verdict-file entries as today, then the
added ones by test id. The exit code is unchanged.

**Rationale.** FR-A1 deletes the verdict file, so without this the edited test would vanish from the audit.
`.spectra/verdicts/` is gitignored, so a fresh clone has blocks but no verdict files; reading the block keeps those
tests correctly reported instead of turning them all into "none".

## R9 — Existing assertions that change

| Test | Today | After 090 | Why |
|---|---|---|---|
| `DeleteHandlerTests.Delete_CleansUpDependsOn` | the dependent's block-list `depends_on` is rewritten | the fixture writes a scalar `depends_on` (the block form doesn't parse), and the key is dropped; a separate T8 row covers the list-form warning | FR-C1 |
| `TempWorkspace.AddTestCase(dependsOn:)` | writes a block list | writes the scalar form for one id; for a list it writes the block form, which tests expect to be unparseable | FR-C1 |
| `UpdatedTestIngestorTests` (063/078) asserting that a non-Manual grounding survives an edit | kept | dropped | FR-A1 |
| `CompileRepairPromptTests` asserting a refusal for hallucinated | refused | accepted (grounded is still refused) | R8 |

The T001 full run and a grep at implementation time confirm the list. Each change is recorded in the tasks.md Notes.

## R10 — Verification on real workspaces (083 lesson)

- **Real init:** tests run `InitHandler` with the embedded template, for A (edit → unverified → re-critic CLI sequence),
  B, C and D.
- **Pre-090 workspace:** baselines, a verdict file without a record, a critic grounding block, a Manual block, a
  skills manifest without `gitignore_offered`, and a `.gitignore` with a pattern the user removed.
- **Quickstart:** the built CLI in a scratch workspace.
- **Guards:** `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` stay green.
