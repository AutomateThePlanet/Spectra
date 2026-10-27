# Research: Spec 088 — Update Prompt Context (Phase 3)

## R0 — What exists at `eb1151c` (checked)

| Fact | Where | Consequence |
|---|---|---|
| `compile-update-prompt` hand-rolls config loading, passes `sourceContext: null`, and fills criteria via `CriteriaContextLoader` (repo-wide last resort) | `Commands/Generate/CompileUpdatePromptCommand.cs:58-101` | Replace both: context from the baseline layer, criteria linked only |
| Refusals are `{refused, missing_input, message}` on stderr | same, `EmitRefusal` | Move to the shared failure shape (`CriteriaCommandIO.WriteFailure`, named codes) |
| `UpdatePromptCompiler.Compile/Assemble` fill `test_case`, `current_source`, `acceptance_criteria`, `profile_format`; `UpdatePromptCompilerTests` pin that API | `Generation/UpdatePromptCompiler.cs` | Keep it as the legacy path for custom templates without the new placeholders; add the new layout alongside |
| `PromptTemplateLoader.LoadTemplate` returns a user template (`IsUserCustomized = true`) when it parses, else the built-in | `Prompts/PromptTemplateLoader.cs` | Detect an outdated custom template by `IsUserCustomized` and its body lacking `{{tests}}` or `{{source_diff}}` |
| `UpdatedTestIngestor.IngestAsync` parses, drift-checks, `ApplyEdit`s (Title, Preconditions, Steps, ExpectedResult, TestData, ScenarioFromDoc, SourceRefs, Criteria) and persists | `Generation/UpdatedTestIngestor.cs` | "Unchanged" = `ApplyEdit(original, candidate)` equal to `original` on exactly those fields |
| `ingest-update --all` counts `written` / `skipped_no_original` / `errors`, never deletes, has no `--dry-run` | `Commands/Generate/IngestUpdateCommand.cs:216-340` | Add per-file outcomes and deletion; no `--dry-run` is added |
| The critic-repair flow (`spectra-generate` 8b) stages to the same path and runs `--all --keep-baseline` | skill | Benefits from deletion; "unchanged" with `--keep-baseline` leaves the baseline alone |
| `BaselineContext` caches file text, sections (fingerprint + normalised), criteria fingerprints, snapshots; no raw section text, no criterion text | `Baseline/BaselineContext.cs` | Add `GetRawSectionAsync` and criterion text |
| `ChangeClassifier.ClassifyRef(ref, baselineFp, current, snapshot, threshold)` gives the state, `why` and `DiffRun`s | Core | "What changed" is computed live with it — identical to triage |
| `UpdateAckHandler.ResolveGroupAsync` / `MatchesAsync` (private) validate a group against `.spectra/update-triage.json` | `Commands/Baseline/UpdateAckHandler.cs` | Extract to a shared resolver |
| `TokenEstimator.Estimate` (chars / 4); `PreFlightTokenChecker.DefaultBudgetTokens` = 96,000; `ai.analysis.max_prompt_tokens` | `Services/`, `Index/`, Core config | The size check (D7) |

## R1 — The update context (D1, D2)

**Decision.** `Spectra.CLI.Generation.UpdateContextBuilder.BuildAsync(tests, suite, BaselineContext, SourceBaseline,
threshold)` → `UpdateContext`:

- **Tests** sorted by ID. **Sections**: every distinct ref (as written) across the tests, in order of first appearance
  (tests by ID, each test's refs in its order).
- **Per ref** (`DescribeRefAsync`):
  - section missing with `not found` → `RelinkCandidateAsync`:
    - RELINK → `renamed to <target>`;
    - submodule not checked out → `REMOVED: not found; submodule not checked out: …`;
    - otherwise → `REMOVED: not found; <reason>`;
  - other missing reasons (`anchor not found`, `outside allowed roots`, `empty ref`) → `REMOVED: <reason>`;
  - resolves: current = `GetRawSectionAsync(ref)` (the 085 matcher over the raw file; the whole file for no anchor):
    - no baseline fingerprint for this ref (in any of the tests that list it) → "not baselined", previous unavailable;
    - fingerprint equal → UNCHANGED (current only);
    - otherwise snapshot = `GetSnapshotAsync`; verdict = `ChangeClassifier.ClassifyRef` → state (LOAD / LIGHT /
      COSMETIC), `why`, `DiffRun`s; previous = snapshot or "unavailable".
- **A ref shared by tests with different baseline fingerprints** (rare: one test re-baselined later): described once
  per distinct (ref, baseline fingerprint) pair, labelled with the tests it applies to. The common case — one
  fingerprint — gives one block.
- **Criteria**: union of linked IDs, sorted ordinal-ignore-case by ID; per ID: exists → current text; baseline
  fingerprint present and different → "changed since baseline"; missing → "removed". Never any other criterion.
- **Nothing to reconcile** (FR-006): a test whose refs all fail to resolve and whose linked criteria all are missing.

**BaselineContext additions**: `GetRawSectionAsync(ref)` (same resolver, same file cache, `SectionExtractor.Extract`
raw text) and `GetCriterionText(id)` (kept from the same criteria scan).

**Rationale.** Reuses every 084–087 decision (matcher, resolver, relink candidate, classifier), so the prompt shows
exactly what triage saw. Computing "what changed" live means `--test-id` needs no prior triage.

## R2 — Rendering (D4, D8)

**Decision.** `UpdatePromptRenderer` produces three deterministic strings (LF line endings, no timestamps):

- **`source_diff`**:
  ```
  #### S1 — docs/storage.md#Quotas  (tests: TC-101, TC-102)
  State: changed (LOAD) — numbers: 30→45
  Current section:
  ~~~~markdown
  <raw text>
  ~~~~
  Previous version (normalised: lowercased, markup removed):
  ~~~~text
  <snapshot>
  ~~~~
  What changed: removed "30"; added "45"
  ```
  Unchanged: `State: unchanged` + current only. Not baselined / no snapshot: `Previous version: unavailable (…)`.
  Unresolved: `State: REMOVED — not found; …` / `State: RELINK — renamed to …`, no text. Fences use `~~~~` so
  backtick fences inside docs survive.
- **`acceptance_criteria`**: `- AC-1 (changed since baseline): <text>` / `- AC-2: <text>` / `- AC-3 (removed)`.
- **`tests`**: per test `### TEST <id>`, `Refs: S1, S3`, `Criteria: AC-1`, then the serialized test (the existing
  `SerializeTest` JSON) in a fenced block.

## R3 — Group resolution (D4)

**Decision.** Extract `ResolveGroupAsync` + `MatchesAsync` from `UpdateAckHandler` into
`Spectra.CLI.Baseline.TriageGroupResolver.ResolveAsync(root, suite, group, byId, context, ct)` (same errors:
`TRIAGE_MISSING`, `GROUP_NOT_FOUND`, `TRIAGE_STALE`, plus a group in another suite → `GROUP_NOT_FOUND`).
`update-ack` keeps its own bucket refusals (REMOVED / RELINK) on top. `compile-update-prompt --group` accepts any
bucket (spec Assumptions).

## R4 — Template (D5)

**Decision.** The built-in `test-update.md`:
- placeholders `source_diff`, `acceptance_criteria`, `tests`, `profile_format` (plus `test_case` and `current_source`,
  documented as legacy and filled for compatibility);
- rules: edit only what the source change implicates; a test nothing implicates is returned unchanged; keep ids,
  priority, component, tags and manual content; return ONE JSON array with one object per `### TEST`, ids unchanged.

**Outdated custom template**: `IsUserCustomized` and the body lacks `{{tests}}` or `{{source_diff}}`.
- `--test-id`: compiled with the legacy values (`test_case` = the serialized test, `current_source` = the rendered
  `source_diff`, `acceptance_criteria` = the rendered criteria), plus a stderr warning `custom template
  .spectra/prompts/test-update.md has no {{tests}} / {{source_diff}}; run 'spectra prompts reset test-update'` (in JSON
  mode too).
- `--group`: refused `TEMPLATE_OUTDATED` (exit 4) with the same hint.

## R5 — Command surface and refusals

**Decision.** `compile-update-prompt --suite <s> (--test-id <id> | --group <gid>)`:
- **config**: `ConfigLoader.LoadForReadAsync(root, Source | Tests)` — the 083 read rule (defaults on a missing file,
  `CONFIG_INVALID` exit 1). This replaces the hand-rolled load (a missing config was exit 1 before).
- **failures**: one JSON document on stderr in the shared shape (`CriteriaCommandIO.WriteFailure`), exit 4 for
  refusals: `INVALID_ARGUMENTS` (no suite; neither or both of `--test-id` / `--group`), `SUITE_NOT_FOUND`,
  `TEST_NOT_FOUND`, `TRIAGE_MISSING` / `GROUP_NOT_FOUND` / `TRIAGE_STALE`, `NOTHING_TO_RECONCILE`, `PROMPT_TOO_LARGE`,
  `TEMPLATE_OUTDATED`; exit 1 for `CONFIG_INVALID`, `BASELINE_UNREADABLE`.
- **output**: the prompt on stdout (unchanged), warnings on stderr.
- reads only: no lock, no writes.

## R6 — Size (D7)

**Decision** (FR-013: the existing token estimator and budget). `estimated = TokenEstimator.Estimate(prompt)`; `limit = config.Ai?.Analysis?.MaxPromptTokens ??
PreFlightTokenChecker.DefaultBudgetTokens`. Over → `PROMPT_TOO_LARGE`: `The prompt is about {estimated} tokens, over
the {limit}-token budget (ai.analysis.max_prompt_tokens). Compile the tests one by one with --test-id, or lower
update.group_size and triage again.` (`PreFlightTokenChecker.EnforceBudget` is shaped for doc suites, so its estimator
and default are reused rather than the method.)

## R7 — Ingest (D6)

**Decision.**
- **`UpdatedTestIngestor.IsUnchanged(original, candidate)`**: `ApplyEdit(original, candidate)` equals `original` on the
  eight editable fields, lists by sequence. Each string is compared after normalising CRLF / CR to LF and trimming
  trailing whitespace per line and at the end, with null ≡ empty; whitespace inside a line counts (analysis U1). The
  normalisation is for the comparison only — an edit that is written is written as the model returned it.
- **`IngestAsync`** returns `IngestResult.UnchangedResult(original)` (no persist) for an unchanged edit. The drift
  guard still runs first, so a drifting edit fails as before.
- **Batch (`--all`)**, per staging file, a `StagedOutcome(File, TestId, Outcome, ErrorCode?, Errors)` with outcome
  `written`, `unchanged` or `failed`. `failed` covers: no original (`NO_ORIGINAL`, still counted in
  `skipped_no_original`), an unreadable file, a parse/validation/drift failure (its code), and a staged id that differs
  from the file name (`ID_MISMATCH`).
- **Baseline**: `Rebuild` records the written tests and the unchanged originals (`Replace` — as `update-ack`);
  `KeepBaseline` records only the written ones (085).
- **Deletion**: after the lock body completes and the baseline was recorded, the staging files of `written` and
  `unchanged` outcomes are deleted; `failed` files stay. If recording the baseline fails, nothing is deleted (the
  re-run would report the edits unchanged and record the baseline then).
- **Output** (additive): batch JSON gains `unchanged` (count), `results: [{test, file, outcome, error_code?,
  errors?}]` and keeps `written`, `skipped_no_original`, `errors`. Human summary lists each test with its outcome.
  Single form: `unchanged: true|false`, `persisted: 0|1`.

## R8 — Skill (FR-018)

**Decision.** `spectra-update` Step 5 becomes per group:
1. `spectra ai compile-update-prompt --suite <s> --group <G>`;
2. edit in-session;
3. Write one `.spectra/updates/<s>/updated-<id>.json` per test (a JSON array with that one test);
4. `spectra ai ingest-update <s> --all --output-format json --verbosity quiet`;
5. report `results` per test: updated / unchanged (acknowledged) / failed;
6. a failed test: up to 2 retries with `compile-update-prompt --test-id` → stage → `--all`, then report and continue.

Step 6 (`update-ack`) stays for tests reviewed without compiling. `.spectra/updated.json` is no longer used.

## R9 — Tests

- `UpdateContextBuilderTests` (T1, T2): baseline workspace fixtures, git only for the RELINK/submodule rows (`[GitFact]`).
- `CompileUpdatePromptGroupTests` (T3, T4, T6, T7): `BaselineTestWorkspace`, `CriteriaCommands` collection.
- `IngestUpdateStagingTests` (T5).
- `UpdatePromptEndToEndTests` (T8): the real `spectra init` template workspace.
- SC-001: a 10-test group sharing one section; prompt length ≤ 1.3 × the raw parts' length.
- `SkillsManifestTests` additions (T9).
- Existing `UpdatePromptCompilerTests` stay green: the legacy compile API is kept.
