---
description: "Task list for Spec 094 — section-scoped generation batches (Phase 5c)"
---

# Tasks: Section-Scoped Generation Batches (Phase 5c)

**Input**: Design documents from `/specs/094-generation-batch-prompts/`: spec.md, plan.md, research.md, data-model.md,
contracts/contracts.md, quickstart.md.

**Tests**: Required (spec "Required Tests"; plan T1–T9). Each new rule must fail on `55b541f`.
- Guards are written in Phase 2 and run on the untouched sources (`git diff --quiet 55b541f -- src` must hold) before
  any source edit.
- CLI rows use `BaselineTestWorkspace` from the real `spectra init` (`InitWithRealTemplateAsync`) in the
  `CriteriaCommands` collection, with docs that have several sections and criteria with `source_doc` / `source_section`.
- A pre-094 workspace is a real init with:
  - `generation.batch_size` absent;
  - a customised `.spectra/prompts/test-generation.md` (the 093 shipped text plus one line);
  - a customised `.spectra/prompts/behavior-analysis.md` (bare doc paths);
  - an old `.spectra/analysis.json`;
  - a skills manifest whose `GitIgnoreOffered` is the 093 list.

**Organization**: by user story (spec.md).
- US1 = each batch is generated from exactly its own sections (P1);
- US2 = the plan never silently loses a batch (P1);
- US3 = stale or oversized batches are refused (P2);
- US4 = prompts stop naming dead tools (P2);
- US5 = existing workspaces keep working (P3).

US2's count rule lives in the plan builder US1 creates, so US1 runs first. US4's template text and US1's batch blocks
are one template file, so they are edited in sequence (see Dependencies).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `094-generation-batch-prompts` (= `55b541f`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1219 / CLI 2092 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.
- [X] T002 Write specs/094-generation-batch-prompts/acceptance/setup-workspace.sh (usage `<empty-dir> [spectra command]`, works with the 093 and the 094 build). It builds a scratch workspace from the real `spectra init`:
  - three docs (`docs/storage.md`, `docs/sharing.md`, `docs/retention.md`), each with 4–6 `##`/`###` sections of concrete rules, numbers and limits;
  - indexed (`spectra docs index`);
  - criteria for each doc ingested from a prepared `criteria.json` (with `source_section` set) through `spectra ai ingest-criteria`, so no model is needed;
  - a `storage` suite ready for "generate tests for the storage area".

  Run it once with the built 093 CLI and record the doc / section / criteria listing in the Notes. Tell the user the SC-003 baseline (two 093 runs, grounded share) can now be measured (quickstart "Before implementing").

---

## Phase 2: Foundational — guard tests, proof against `55b541f`, shared code

- [X] T003 [P] Write tests/Spectra.Core.Tests/Baseline/SectionRefTests.cs (T1; R1). Rows:
  - `SectionRef.Canonical` gives the path for a whole doc;
  - `path#upload-quota` for "Upload quota";
  - the explicit id for `## Limits {#quota-limits}`;
  - `-1` for the second "Limits";
  - a round trip through `SectionExtractor.Match` returns the same index for every heading of a mixed fixture (Unicode, punctuation, duplicates);
  - a heading with no letters/digits (`## ---`) → null, so the caller keeps the source.
- [X] T004 [P] Write tests/Spectra.CLI.Tests/Generation/Plan/GenerationPlanBuilderTests.cs (T2 — pure; FR-002–FR-005; R3, R4). Rows:
  - ids `B1…` in input order after the focus filter;
  - grouping by (path, heading index), not by spelling;
  - document order (ordinal path), then section order with the whole doc first;
  - packing up to `batch_size` (default 8 and set to 3);
  - a section with more behaviours than `batch_size` split over consecutive batches;
  - a batch never spans two docs;
  - batch ids `1…n`, then `unsourced` last in input order;
  - count rule `proportional`: 20 behaviours / 5 batches / R 12 → counts ≥ 1, Σ = 12, ties to the earlier batch;
  - `one_per_batch`: 5 batches / R 3 → all 1, `planned_total` 5;
  - R 0 → all 1;
  - no behaviours → no batches;
  - no batch with behaviours has count 0 or is missing;
  - the plan serializes byte-identically twice;
  - the strict parse rejects a missing key, a wrong type and `version` 2.
- [X] T005 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/GenerationPlanIngestTests.cs (T3; FR-001–FR-003, FR-007). Use a real-init workspace with `docs/storage.md` (sections "Upload quota", "Quota settings", "Limits" ×2) and `docs/sharing.md`, and an analysis whose sources are:
  - `docs/storage.md#Upload quota`;
  - `docs/storage.md#upload-quota`;
  - `docs/storage.md`;
  - `docs/sharing.md#links`;
  - `docs/nope.md`;
  - `docs/storage.md#missing`;
  - `""`.

  Rows:
  - `ingest-analysis --output-format json` writes `.spectra/generation-plan/storage.json`;
  - the JSON has `plan`, `planned_total`, `count_rule`, `batches` (`id`, `count`, `source_refs`, `behaviors`) and `unresolved` with the reasons `not found`, `anchor not found` and `empty ref`;
  - the two quota spellings are one ref `docs/storage.md#upload-quota`;
  - the plan's `sections` holds fingerprints equal to `SourceFingerprint.OfSection` of the raw sections;
  - the stderr summary has the `batch 1 | count … | … | …` and `unresolved: B5 … — not found` lines; a title containing `|` shows `/` in the line and the original in the JSON;
  - `docs/storage.md#limits` (two "Limits" headings) resolves to the first heading (085 matcher) and the plan has exactly one `notes` entry naming the ambiguity;
  - re-ingest → identical plan bytes;
  - a second suite's plan is untouched;
  - an empty / unparseable analysis (exit 5/6) writes no plan and keeps an existing one;
  - the existing fields (`already_covered`, `recommended`, `breakdown`, `technique_breakdown`, `boundary_gaps`) are unchanged (guard).
- [X] T006 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/CompileBatchPromptTests.cs (T4; FR-009, FR-011–FR-013; R6, R8). Use the T005 workspace, with criteria for "Upload quota", "Quota settings", a doc-level storage criterion, a sharing criterion and a criterion whose section does not exist. Rows:
  - `compile-prompt --suite storage --batch 1` contains exactly batch 1's behaviour lines (`- B1 [boundary · BVA] …`);
  - each of its sections once, in a `~~~~markdown` fence;
  - an outer ref absorbs an inner one (whole doc + section → the doc once);
  - only the matching criteria (not the sharing criterion);
  - the doc-level criterion only with a whole-doc ref;
  - the unmatched-section criterion treated as doc-level with a stderr warning;
  - the "complete source for this batch" sentence and the `source_refs` rule;
  - no `ON-DEMAND`, `ListDocumentationFiles`, `ReadTestIndex`, `CheckDuplicates`, `GetNextTestIds` or "read the relevant documentation";
  - `--count 7` overrides the plan count; `--focus` appears;
  - two runs → byte-identical stdout;
  - criteria fallback: a batch whose sections have no criteria → all criteria of its docs plus a warning;
  - `--batch unsourced` → the count-mode prompt with the unsourced titles as focus;
  - `--batch` with `--from-description` → exit 1 with the usage message.
- [X] T007 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/CompileBatchRefusalTests.cs (T5; FR-010, FR-010a — refusals and template modes). Refusal rows exit 4 with the shared failure shape on stderr in JSON mode and write nothing; the template-mode rows (US5) exit 0:
  - `PLAN_MISSING` (no plan; an unreadable plan);
  - `BATCH_NOT_FOUND` (lists the ids);
  - `PLAN_STALE`: after an edit inside "Upload quota", `errors` names `docs/storage.md#upload-quota … changed since the analysis`; after the heading is renamed, `no longer resolves: anchor not found`; a batch without that section still compiles;
  - no `TEMPLATE_OUTDATED` (exit 0, FR-010a; R16, R17):
    - an unedited pre-094 copy (the 093 shipped text, manifest record under the init relative key) → mode `batch` on the stderr JSON line, the bundled template's batch blocks in the prompt, warning "using the bundled test-generation template; run spectra update-skills to refresh your copy";
    - the same copy with a record under the absolute key (as `update-skills` writes it) → the same;
    - a customised copy (one line added) → mode `count_fallback`, the prompt contains the added line, the batch's `<title> (<ref>)` focus and the batch's count, warning "custom test-generation template predates batch prompts — this batch is compiled in count mode (the generator reads the docs); run spectra prompts reset test-generation to use section batches";
    - no manifest → `count_fallback`, with the no-record warning that also names `spectra update-skills`;
    - after `spectra update-skills` on the unedited copy → mode `batch`, no warning;
  - `MISSING_INPUT` (`missing_input: "criteria_context"`) when the batch's docs have no criteria;
  - `PROMPT_TOO_LARGE` with the top-level `analysis.max_prompt_tokens` set low;
  - the check order plan → batch → staleness → criteria → size (one row with two faults);
  - `--batch unsourced`: only the plan and batch checks, then count mode's suite-match criteria (a suite without criteria → `MISSING_INPUT` in the shared shape, not the pre-094 shape); no staleness check (a changed section does not matter);
  - `count_fallback` with a suite without criteria → `MISSING_INPUT` in the shared shape; no size check in `count_fallback`.
- [X] T008 [P] Write tests/Spectra.CLI.Tests/Generation/GenerationTemplateToolTextTests.cs (T6; FR-012; US4). Rows:
  - the shipped `test-generation.md`, count-mode `compile-prompt --suite storage --count 3` and `PromptCompiler.Assemble` with a null loader (the fallback) contain none of the four tool names nor `ON-DEMAND` / "Use the tools";
  - each says to read the documentation with the file tools and that test ids are assigned when the tests are ingested;
  - `--from-description` likewise;
  - the shipped template declares `source_sections` and `read_docs` and passes `prompts validate`;
  - (guard) count-mode refusal without criteria still has the pre-094 shape `{refused, missing_input, message}`.
- [X] T009 [P] Write tests/Spectra.CLI.Tests/Config/GenerationBatchSizeConfigTests.cs (T7; FR-006). Rows:
  - `generation.batch_size` 0 / 51 / `"x"` → `ingest-analysis` exit 1 `CONFIG_INVALID` naming 1–50;
  - 1 and 50 → accepted;
  - absent (the real init template) → 8, no warning;
  - `"generation": null` → defaults;
  - a config without `generation` → defaults;
  - `ConfigRuleMatrixTests` / `InitTemplateWorkspaceTests` untouched (guard: run them).
- [X] T010 [P] Write tests/Spectra.CLI.Tests/Commands/Init/GenerationPlanGitIgnoreTests.cs (T8; FR-008; R10). Rows:
  - `spectra init` writes `.spectra/generation-plan/` and records it as offered;
  - a 093 workspace (recorded list without it) → `update-skills` appends it once; a second run doesn't;
  - a user who removed it after the offer → not re-added;
  - a pre-090 workspace (no record) → offered (the explicit 089 list does not contain it).
- [X] T011 [P] Write tests/Spectra.CLI.Tests/Skills/BatchGenerationAgentTests.cs (T9; FR-014–FR-017).
  - **Analyst:** it copies the CLI's `batch <id> | count <k> | <section refs> | <behaviour titles>` lines "verbatim" and `unresolved:` lines; no "Plan the batches", "at most 8 behaviours" or "drop a batch".
  - **Generator:**
    - input `batch:`;
    - `spectra ai compile-prompt --suite <suite> --batch <batch>`;
    - for a numbered batch in `batch` mode "never" Read a document under `docs/`; `unsourced` and `count_fallback` read the docs and relay the compile's warning as `warning:`;
    - `PLAN_STALE`, `PROMPT_TOO_LARGE`, `PLAN_MISSING`, `BATCH_NOT_FOUND` → `failed <error_code>`; no `TEMPLATE_OUTDATED`;
    - no `--count <count> --focus` compile line.
  - **Skill:**
    - Task `spectra-generator` with `batch:`; `count` only for an explicit user total;
    - `PLAN_STALE` / `PROMPT_TOO_LARGE` → report and offer to re-run the analysis (`spectra-analyst` again);
    - a 0-count batch is never sent to the generator and is listed in Step 9 as "not generated (count 0 after splitting your total)";
    - after a re-run of the analysis: back to Step 4 (present, STOP), naming the batches that already ran with their created test ids and suggesting a focus on the stale sections; "nothing is regenerated automatically";
    - each distinct template warning is reported once per run;
    - generator: `mode` and `warnings` come from the stderr line starting `{"command":"compile-prompt"`; the rest of the output is the prompt;
    - no `focus: "<user focus>; <batch focus>"`.
  - **Guards (pass before and after):** the NON-STOP contract and written==0 guard text verbatim; routing by `results`; from-description unchanged.
- [X] T012 Proof run: with `git diff --quiet 55b541f -- src` holding, run T003–T011. Expect every row except the stated guards to fail for the stated reason (missing type / option / file / text). Save the output to the session scratchpad (`094/proof-55b541f.txt`) and summarise it in the Notes.
- [X] T013 Inventory the existing rows that pin text this spec changes, and record each as "retarget" / "unchanged" in the Notes:
  - `GenerateAgentTests` (analyst `batch <n> | count <k> | focus <`, "at most 8 behaviours", "add up to `recommended`"; generator `compile-prompt --suite <suite> --count <count> --focus`);
  - `GenerateOrchestratorTests` (`focus: "<user focus>; <batch focus>"`);
  - `PromptCompilerTests` and any test asserting the old template / fallback text;
  - `IngestAnalysisBoundaryGapTests` (config loader change);
  - `SpectraGitIgnore` / `StagingGitIgnoreTests` rows over `Pre090Offered`.
- [X] T014 Add `BatchSize` (`[JsonPropertyName("batch_size")] int?`) and `EffectiveBatchSize => BatchSize ?? 8` to src/Spectra.Core/Models/Config/GenerationConfig.cs (validation follows in US5).
- [X] T015 [P] Add src/Spectra.Core/Baseline/SectionRef.cs: `Canonical(string relativePath, IReadOnlyList<MarkdownHeading> headings, int? index)` → the canonical ref or null (R1; slug + explicit id + `-n` duplicates; null when the round trip fails). T003 passes.

**Checkpoint**: guards exist and are proven to fail on `55b541f`; shared helpers in place.

---

## Phase 3: User Story 1 — each batch is generated from exactly its own sections (P1) 🎯 MVP

**Goal**: analysis → plan → `compile-prompt --batch` → generator without doc reads.
**Independent test**: T005, T006 and the T011 agent / skill rows pass.

- [X] T016 [US1] Add src/Spectra.CLI/Generation/Plan/GenerationPlan.cs: records `GenerationPlan`, `PlannedBehavior`, `PlannedBatch`; `Serialize` (`Utf8JsonWriter`, data-model key order, `sections` sorted ordinal, LF, no BOM, no timestamps); strict `Parse` → `GenerationPlanFormatException`.
- [X] T017 [US1] Add src/Spectra.CLI/Generation/Plan/GenerationPlanBuilder.cs: `Build(suite, focus, batchSize, recommended, IReadOnlyList<ResolvedBehavior>)`:
  - ids `B<n>`;
  - ordering (path ordinal, whole doc −1 / heading index, input order);
  - packing per R4;
  - `unsourced` last.

  Counts are added in US2 (T026); until then each batch gets 1.
- [X] T018 [US1] Add src/Spectra.CLI/Generation/Plan/GenerationPlanStore.cs: `PathFor(root, suite)` = `.spectra/generation-plan/<suite>.json`; `SaveAsync` via `BaselineStore.WriteAtomicAsync`; `LoadAsync` → plan, or a `Missing` / `Unreadable(reason)` outcome.
- [X] T019 [US1] Change src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs:
  - load the config through `ConfigLoader.LoadForWriteAsync(root, ConfigSections.Source, ct)` (failures via `CriteriaCommandIO.WriteFailure`, exit 1);
  - after a successful recommendation, resolve every behaviour's source with `BaselineContext.GetSectionAsync` and the headings (`GetHeadingsAsync`) → `SectionRef.Canonical` + fingerprint + reason;
  - build and save the plan (`PLAN_WRITE_FAILED`, exit 1, on an I/O error);
  - add `plan`, `planned_total`, `count_rule`, `batches`, `unresolved`, `notes` to the JSON;
  - add the batch and unresolved lines to the human summary, which goes to stderr in JSON mode.

  Expose the behaviours with their sources from `AnalysisRecommendationBuilder` (no change to the counts). T005 passes (count rows after T026).
- [X] T020 [US1] Change src/Spectra.CLI/Prompts/Content/behavior-analysis.md (FR-001): `source` = "the most specific section ref the behaviour comes from, `doc#anchor` as in `source_refs` (an anchor from the Sections list), or the bare document path when it spans the document"; update both JSON examples to `docs/form.md#username`.
- [X] T021 [P] [US1] Extract `CriteriaContextLoader.Format(IEnumerable<AcceptanceCriterion>)` in src/Spectra.CLI/Generation/CriteriaContextLoader.cs (same output; the existing tests stay green). Make the fence helper in src/Spectra.CLI/Generation/UpdatePromptRenderer.cs `internal static AppendFenced` (no output change).
- [X] T022 [US1] Add src/Spectra.CLI/Generation/Plan/BatchCriteriaSelector.cs (R6): scan once; `source_doc` via `CriteriaPaths.NormalizeSourceDoc` + `CriteriaProvenance.SameDocument`; `source_section` via `SectionExtractor.Match`; whole-doc / `CoveredHeadings` inclusion; an unmatched section → doc-level + warning; first id wins; sort ordinal; fallback to the batch's docs + warning; an empty result → none.
- [X] T023 [US1] Add src/Spectra.CLI/Generation/Plan/BatchPromptCompiler.cs (R7, R8):
  - behaviour lines into `behaviors`;
  - non-absorbed sections as `### <ref>` + fence into `source_sections`;
  - `read_docs` empty;
  - criteria via `Format`;
  - count / focus / profile_format / testimize as count mode;
  - `PredatesBatch(template)` = user-loaded and no `{{source_sections}}` (R7); the template choice of R16: the manifest lookup (absolute key, relative key with the platform separator, relative key with `/`) and `FileHasher.ComputeFileHashAsync` → the bundled template (`batch`) or count mode with the user's template and the `<title> (<ref>); …` focus (`count_fallback`), each with its warning.

  Add the `source_sections` / `read_docs` blocks to src/Spectra.CLI/Prompts/Content/test-generation.md (top-level `{{#if}}` blocks; frontmatter placeholders declared), and pass `read_docs = "yes"`, `source_sections = ""` from `PromptCompiler.Assemble` in src/Spectra.CLI/Generation/PromptCompiler.cs.
- [X] T024 [US1] Change src/Spectra.CLI/Commands/Generate/CompilePromptCommand.cs:
  - add `--batch`; `--batch` with `--from-description` → exit 1 usage;
  - batch mode: `ConfigLoader.LoadForReadAsync(root, ConfigSections.Source, ct)`, load the plan, find the batch;
  - `unsourced` → the count-mode path with the titles as focus and the plan count;
  - otherwise resolve and compare fingerprints (`BaselineContext`), select criteria, compile, estimate the tokens;
  - print the prompt to stdout; on stderr the mode line (JSON mode: `{"command":"compile-prompt","success":true,"mode":"batch|count_fallback|count","batch":"<id>","warnings":[…]}`; human mode: `Mode:` + `Warning:` lines, R17).

  Count mode is unchanged. T006 passes.
- [X] T025 [US1] Rewrite the generator and analyst agents (FR-014, FR-015):
  - src/Spectra.CLI/Skills/Content/Agents/spectra-analyst.agent.md: Step 4 = copy the CLI's batch and unresolved lines verbatim; the final message and example updated.
  - src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md:
    - input `suite`, `batch`, `count?`;
    - Step 1 `spectra ai compile-prompt --suite <suite> --batch <batch> [--count <count>] --output-format json`;
    - Step 2: a numbered batch in `batch` mode never Reads a document (the prompt is the complete source; every test's `source_refs` from its behaviours' refs); `unsourced` and `count_fallback` (read from the stderr mode line) read the docs, and the compile's warning is relayed as a `warning:` line;
    - any exit 4 → `failed <error_code>: <errors[0]>` (`missing_input` when there is no `error_code`).

  Then change src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md (FR-016, FR-017):
  - Steps 1–3 text (the analyst returns the CLI's batch lines and the `unresolved:` lines);
  - Step 4 shows them;
  - Steps 5–7 pass `batch: <id>` and `count` only for an explicit user total (split as in 093);
  - never invoke the generator for a 0-count batch; Step 9 lists it as "not generated (count 0 after splitting your total)";
  - after the loop, report `PLAN_STALE` / `PROMPT_TOO_LARGE` batches and offer once to re-run the analysis (invoke `spectra-analyst` again); after a re-run return to Step 4 — present the new recommendation, name the batches that already ran in this session with their created test ids, suggest a focus on the stale sections, STOP; nothing is regenerated automatically;
  - report a template `warning:` from the generators once per run;
  - the reference table: `compile-prompt --batch`.

  No other text changes (the NON-STOP and written==0 blocks byte-identical). Retarget the T013 rows. T011 passes.

**Checkpoint**: a batch prompt holds exactly its sections; the generator reads no docs for it.

---

## Phase 4: User Story 2 — the plan never silently loses a batch (P1)

**Goal**: counts follow one stated rule; unresolved behaviours still generate.
**Independent test**: the T004 count rows and the T005 count / unsourced rows pass.

- [X] T026 [US2] Add the count rule (R3) to src/Spectra.CLI/Generation/Plan/GenerationPlanBuilder.cs: `proportional` with largest remainder (ties → earlier batch) when R ≥ n, else `one_per_batch`; `planned_total`; the plan's `count_rule`.
- [X] T027 [US2] In src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs add the human line `counts raised to one per batch (recommended R < n batches)` for `one_per_batch`, show `|` inside a title as `/` in the `batch …` line (the JSON keeps it), and check that the `unsourced` batch line has an empty refs field (`batch unsourced | count 1 |  | …`).

**Checkpoint**: T004 and T005 fully green.

---

## Phase 5: User Story 3 — stale or oversized batches are refused (P2)

**Goal**: the refusals in FR-010, in their order.
**Independent test**: T007 passes.

- [X] T028 [US3] Add `PLAN_MISSING`, `BATCH_NOT_FOUND`, `PLAN_STALE` and `MISSING_INPUT` to the refusal set in src/Spectra.CLI/Baseline/BaselineCommandIO.cs (exit 4).
- [X] T029 [US3] In src/Spectra.CLI/Commands/Generate/CompilePromptCommand.cs implement the refusals in the order plan → batch → staleness → criteria → size, through `BaselineCommandIO.Fail` (command `compile-prompt`):
  - `PLAN_STALE` lists every changed / unresolved section ref with "re-run the analysis";
  - `BATCH_NOT_FOUND` lists the plan's ids;
  - no template refusal (R16 decides the mode);
  - `--batch unsourced`: only the plan and batch checks, then count mode;
  - `PROMPT_TOO_LARGE` uses `TokenEstimator` against `config.Analysis?.MaxPromptTokens ?? PreFlightTokenChecker.DefaultBudgetTokens` (top-level `analysis`, the same lookup as `CompileUpdatePromptCommand`).

**Checkpoint**: T007 green.

---

## Phase 6: User Story 4 — the prompts stop naming dead tools (P2)

**Goal**: no generation prompt names the four tools or says ON-DEMAND.
**Independent test**: T008 passes.

- [X] T030 [US4] Rewrite the tool text in src/Spectra.CLI/Prompts/Content/test-generation.md (after T023):
  - CRITICAL RULES without "ON-DEMAND" / "check for duplicates";
  - no WORKFLOW list;
  - OUTPUT FORMAT without "After using tools to gather information";
  - IMPORTANT without items 1 and 3;
  - a `{{#if read_docs}}` block: read the relevant documentation with your file tools before writing; test ids are assigned when the tests are ingested — do not invent them.

  Keep the technique rules, Testimize block, schema and criteria block byte-identical.
- [X] T031 [US4] Rewrite the built-in fallback string in src/Spectra.CLI/Generation/PromptCompiler.cs to the same count-mode text (no tool names; read the docs with the file tools; ids from the ingest). The criteria block is unchanged. T008 passes.

**Checkpoint**: T008 green; from-description unchanged apart from the tool text.

---

## Phase 7: User Story 5 — existing workspaces keep working (P3)

**Goal**: config range, gitignore offer, pre-094 templates.
**Independent test**: T009 and T010 pass; quickstart CLI check 8.

- [X] T032 [P] [US5] In src/Spectra.Core/Config/ConfigLoader.cs:
  - add `("generation", ConfigSections.None)` to `CheckedSections`;
  - add validation `INVALID_GENERATION_BATCH_SIZE` ("generation.batch_size must be an integer from 1 to 50") for values outside 1–50.

  A non-integer is already a typed-load `CONFIG_INVALID`. T009 passes.
- [X] T033 [P] [US5] In src/Spectra.CLI/Commands/Init/SpectraGitIgnore.cs add `.spectra/generation-plan/` (comment: Spec 094 scratch plan), and make `Pre090Offered` explicitly exclude the updates, repairs and generation-plan entries. T010 passes.

**Checkpoint**: T009, T010 green.

---

## Phase 8: Polish, docs, verification

- [X] T034 [P] Update docs/cli-reference.md:
  - `ingest-analysis`: the plan, `batches`, `unresolved`, `planned_total`, `count_rule`, the stderr summary in JSON mode, `PLAN_WRITE_FAILED`, the config loader;
  - `compile-prompt --batch`, with the refusal table and order;
  - `generation.batch_size`;
  - the count-mode text change.
- [X] T035 [P] Update docs/usage.md: generation runs per batch of doc sections; the plan; what a stale batch means and how to re-run the analysis; `generation.batch_size`.
- [X] T036 [P] Update CHANGELOG.md under `[2.6.0]` before `### Notes`:
  - "Changed (Spec 094 — section-scoped generation batches, Phase 5c)";
  - "Fixed" (dropped zero-count batches; dead tool names in generation prompts);
  - Notes entries (run `update-skills` / `prompts reset test-generation` for a customised template; `.spectra/generation-plan/` offered once).
- [X] T037 [P] Update CLAUDE.md: a short "Recent Changes" entry for 094 with the counts from T038 (the 045 entry's `ai.analysis.max_prompt_tokens` is already corrected to `analysis.max_prompt_tokens`).
- [X] T038 Run the full `dotnet build --no-incremental` (zero new warnings) and `dotnet test`. Expect Core ≥ 1219 / CLI ≥ 2092 with only `ProfileFormatLoaderTests` failing / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts. Check that `git diff --stat 55b541f -- src` touches only the files in plan.md.
- [X] T039 Run the quickstart "CLI checks" 1–8 with the built CLI on a fresh `spectra init` workspace and on a pre-094 workspace (the 083 lesson), and record the results in the Notes. Run `acceptance/setup-workspace.sh` with the 094 build.
- [X] T040 Fill in the Notes:
  - counts, the T012 proof, the T013 inventory outcome, deviations, changed assertions;
  - a placeholder for the user's SC-001–SC-005 results and the SC-003 baseline.

  Check the line endings of every touched file (no mixed CRLF/LF), mark every task [X], and stop. Ask the user before committing as `feat(094): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push. The manual acceptance is the user's run.

---

## Dependencies & Execution Order

- **Setup** (T001, T002) → **Foundational** (T003–T015). T012 before any source edit; T014 and T015 after T012.
- **US1** (T016–T025):
  - T016 → T017 → T018 → T019; T020 any time;
  - T021 [P] → T022 → T023 → T024;
  - T025 after T024 (its texts name the CLI's lines and codes).
- **US2** (T026, T027): after T017 / T019.
- **US3** (T028, T029): after T024.
- **US4** (T030, T031): T030 after T023 (same template file); T031 after T023 (same `PromptCompiler.cs`).
- **US5** (T032, T033): T032 any time after T014; T033 any time after T012.
- **Polish:** T034–T037 after the stories; T038–T040 last.

## Parallel Examples

```text
# Guards (different files):
T003 SectionRefTests  T004 GenerationPlanBuilderTests  T005 GenerationPlanIngestTests  T006 CompileBatchPromptTests
T007 CompileBatchRefusalTests  T008 GenerationTemplateToolTextTests  T009 GenerationBatchSizeConfigTests
T010 GenerationPlanGitIgnoreTests  T011 BatchGenerationAgentTests
# After US1:
T032 ConfigLoader  T033 SpectraGitIgnore
# Docs:
T034 cli-reference  T035 usage  T036 CHANGELOG  T037 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: plan + `compile-prompt --batch` + agents/skill — the generator stops reading docs.
2. **US2**: the count rule (no dropped batch).
3. **US3**: refusals (stale, oversized).
5a. **US5** also covers the pre-094 template modes (implemented in T023/T024, tested in T007).
4. **US4**: dead tool text gone from every generation prompt.
5. **US5**: config range, gitignore offer, pre-094 checks.
6. **Polish**:
   - docs, full run, built-CLI checks on both workspaces, the acceptance script;
   - commit and merge on approval;
   - the user runs the manual acceptance against their 093 baseline.

## Notes

- **Baseline (T001):** `55b541f` — Core 1219 / CLI 2092 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 /
  Integration 8; 0 errors, the 4 known warnings.
- **Acceptance script (T002):** `acceptance/setup-workspace.sh` builds three docs (storage 5 sections, sharing 4,
  retention 4) and 25 criteria with `source_section`, ingested model-free (`ingest-criteria --component storage`).
  Ran with the 093 build (`ws093`) and the 094 build (`ws094`). The SC-003 baseline (two 093 runs) can be measured on it.
- **Proof (T012):** with `git diff --quiet 55b541f -- src` holding, 58 rows of T005–T011 ran: 53 failed for the stated
  reason (no plan / no `--batch` / dead tool text / no config key / no gitignore entry / old agent and skill text). The
  two guards passed (count-mode refusal shape, the 093 skill guards). Three rows passed trivially on the base (empty output
  compared with empty output, or a zero count) and were tightened (assert exit 0, the complete-source sentence, the entry
  present after the first offer); re-run: all three fail on the base. T003/T004 (`SectionRef`, `GenerationPlanBuilder`)
  don't compile on the base (types missing) — that is their failure. Output: scratchpad `094/proof-55b541f*.txt`.
- **Inventory (T013) / changed assertions (deliberate):** `GenerateAgentTests.TheAnalyst_AnalysesAndProposesBatches`
  (`batch <n> | count <k> | focus <`, "at most 8 behaviours", "add up to `recommended`" → the CLI's batch line and
  "verbatim"); `GenerateAgentTests.TheGenerator_GeneratesOneBatch_AndReturnsOneLinePerTest` (`--count <count> --focus` →
  `--batch <batch>`); `GenerateOrchestratorTests.GenerateSkill_DelegatesAnalysisBatchesAndRepairs`
  (`focus: "<user focus>; <batch focus>"` and `` `count` `` → `` `batch: <id>` `` and `` `count: <k>` ``). Unchanged and
  green: `PromptCompilerTests`, `IngestAnalysisBoundaryGapTests` (its old-style config loads through the checked
  loader), `StagingGitIgnoreTests`, `GitIgnoreOfferTests`.
- **Final run (T038):** `dotnet build --no-incremental`: 0 errors, the same 4 warnings (an xUnit2029 analyzer warning in
  a new test was fixed). `dotnet test`: Core 1228 (+9) / CLI 2167 (+75; only `ProfileFormatLoaderTests` fails) /
  Execution 228 / Integration 8. `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` green. Scope: the files in
  plan.md, plus `AnalysisRecommendation(Builder).cs` (the filtered behaviours, `documents_analyzed` by document),
  `PlanSectionResolver.cs` (shared by ingest and compile) and `CompileBatchPrompt.cs` (the `--batch` path).
- **Built-CLI checks (T039):** on `ws094` (real init) — plan (recommended 8 = 2 + 4 + 2, `unsourced` with `not found`,
  identical bytes on re-ingest); `--batch 2` (whole doc absorbs its sections: one `### docs/storage.md`, the 12 storage
  criteria, 0 dead-tool lines, byte-identical rerun, 6,466 bytes); `--batch 1` (two sharing sections, only their 4
  criteria); an edit in `## Quotas` → `PLAN_STALE` naming `docs/storage.md` and `docs/storage.md#quotas`, the sharing
  batch still compiles; `PLAN_MISSING`, `BATCH_NOT_FOUND`, `PROMPT_TOO_LARGE`; the 093 `test-generation.md` unedited →
  `batch` + bundled warning, customised → `count_fallback` with the user's line, no manifest → `count_fallback`, after
  `update-skills` → `batch` without a warning; count mode without dead tools; `batch_size: 0` → `CONFIG_INVALID`, no key
  → 8 without a warning; the gitignore entry once; a pre-094 analysis with bare doc paths → whole-document batches.
- **Decisions taken without an answer:** analyze M1/M2/L1–L4 (second round) applied with the recommended remediations
  (shared failure shape for every `--batch` refusal; per-mode check order; T007 retitled; the no-record warning also
  names `update-skills`; generator reads `mode` from the stderr line; "each distinct template warning once").
- **Deviations:** `CriteriaContextLoader.Format` takes an optional newline (`
` for batch prompts; count mode keeps the
  platform newline, so its output is unchanged). Line endings: six edited files had been rewritten LF; restored to CRLF
  (the working-copy convention with `core.autocrlf=true`).
- **Observed, out of scope:** `spectra init` records prompt-template hashes under a relative key while `update-skills`
  uses absolute paths, so the first `update-skills` after `init` may overwrite an edited template — flagged as a
  separate task. `docs/cli-reference.md` still says `ai.analysis.max_prompt_tokens` for the analysis budget (the key is
  top-level `analysis`); left for a docs pass.
- **Manual acceptance (the user's run):** SC-001 (0 doc Reads by numbered-batch generators), SC-002 (`source_refs`
  within the batch's refs), SC-003 (grounded share ≥ the 093 average − 5 points over 2 runs each), SC-004, SC-005 —
  results to be recorded here: 093 baseline: _pending_; 094: _pending_.
