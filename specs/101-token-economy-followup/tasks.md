---
description: "Task list for Spec 101 — token economy follow-up"
---

# Tasks: Token Economy Follow-up

**Input**: Design documents from `/specs/101-token-economy-followup/`: spec.md, plan.md, research.md, data-model.md,
contracts/contracts.md, quickstart.md.

**Tests**: Required (FR-024). Each new rule must fail on `6a4c41a`, except the stated guards.
- The rows are written in Phase 2 and run on the untouched sources (`git diff --quiet 6a4c41a -- src` must hold) before
  any source edit.
- Rows that use new members (`CriticSourceLoader`, `SkillReferenceContent`, `SkillInstallLayout.ReferencePath`) can't
  compile on the base. They are staged out for the proof run (as in 094–097); their failure is "member missing". Install
  rows check files on disk, so they compile on the base.
- CLI rows use `BaselineTestWorkspace` (real `spectra init`; `InitWithSkillsAsync` where skills are needed) in the
  non-parallel `CriteriaCommands` collection and run the real root command (`RunAsync`).
- Skill and agent rows read the bundled content through `SkillContent` / `AgentContent` (and, after T021,
  `SkillReferenceContent`), as `SkillsManifestTests` does. Text is compared after CRLF → LF (099 lesson).

**Organization**: by user story (spec.md).
- US1 = section-scoped critic input (F1, P1);
- US2 = the quality gate (F1, P1; manual);
- US3 = lean skills that survive compaction (F2 slimming, P1);
- US4 = the critic inside the work subagents (F3, P2);
- US5 = agent frontmatter Claude Code reads (F4, P2);
- US6 = reference files installed without losing edits (F2 packaging, P2).

Execution order (the same in plan.md): F1, then F2, F3, F4 — US1 → **US2 gate (STOP on failure)** → US6 → US4 → US3 →
US5. US3 is P1 but comes after US4, because it slims the skill text into its final, post-F3 form and needs the US6
install path. US5 (F4) doesn't depend on the gate and may run earlier (right after T010), but the default is last.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US6 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `101-token-economy-followup` (= `6a4c41a` plus spec docs): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1228 / CLI 2345 / Execution 228 / Integration 8 with 0 failures and 0 warnings (the plan-time run). Record everything in the Notes.
- [X] T002 Add the critic fixture helpers in tests/Spectra.CLI.Tests/TestFixtures/CriticSourceWorkspace.cs (extensions on `BaselineTestWorkspace`; all text built in code with `\n`, never read from checked-out files):
  - `LongDoc(path, sections: 40)` — `# Feature`, an intro line, then `## Section 1` … `## Section 40`, five lines each (> 8,000 characters before Section 30), plus `### Sub 12.1` under Section 12;
  - `CitingTest(suite, id, params string[] refs)` — a test file with id, title, steps, expected result and `source_refs`, plus its `_index.json` entry;
  - `MalformedCriteria()` — a `docs/criteria/x.criteria.yaml` that isn't valid YAML;
  - `CaptureGolden()` — documents how tests/Spectra.CLI.Tests/TestFixtures/CriticGolden/full-docs-6a4c41a.txt was made (T003 captures it).
- [X] T003 Capture the `--full-docs` golden on the base: with the `6a4c41a` build, compile `compile-critic-prompt --suite s --test TC-001` for the fixture (refs `docs/long.md#section-30`, `docs/long.md#section-2`, `docs/short.md`) and save the stdout, CRLF → LF, as tests/Spectra.CLI.Tests/TestFixtures/CriticGolden/full-docs-6a4c41a.txt (embedded or copied to output). Record the command and the SHA-256 in the Notes.

---

## Phase 2: Foundational — rule rows and proof

- [X] T004 [P] Write tests/Spectra.CLI.Tests/Source/CriticSourceLoaderTests.cs (F1 unit; FR-001, FR-002; R1). Rows (staged: `CriticSourceLoader` is new):
  - `#section-30` and `#section-2` → one item for `docs/long.md`, content = Section 30's raw text, a blank line, Section 2's raw text (first-reference order), nothing else of the file;
  - `#section-12` → includes `### Sub 12.1`; `#section-12` + `#sub-121` → Section 12 once (absorbed);
  - `#section-2` twice (or two spellings the matcher maps to one heading) → once;
  - `docs/long.md` + `docs/long.md#section-2` → the whole file, bytes equal to `File.ReadAllText`;
  - `#removed` → the whole file, first line `> Note: the anchor "#removed" was not found in this document, so the whole document is given.`; two unmatched anchors → two note lines in ref order;
  - two documents → two items in first-reference order, `Title` and `Path` as `SourceRefDocumentLoader` gives them;
  - a missing file and an outside-root ref → `FileNotFoundException` with `SourceRefDocumentLoader.MissingMessage` text;
  - **empty section (guard after the change):** `## Empty` followed directly by `## Next`, ref `#empty` → the content is the `## Empty` heading line only, with no fallback to the whole document;
  - **same section set as 094**: for refs `#section-12`, `#sub-121`, `#section-2`, the sections kept equal the ones `CompileBatchPrompt`'s absorption keeps (computed through `SectionExtractor.CoveredHeadings` in the test).
- [X] T005 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/CriticSectionPromptTests.cs (F1 CLI; FR-001–FR-006; US1 scenarios). Rows:
  - **section mode:** refs `#section-30`, `#section-2`, `docs/short.md` → the prompt holds "Section 30 paragraph line 3", no "## Section 29" or "## Section 31", and the whole of `docs/short.md` (fails on the base: Section 30 is past the cut);
  - **unmatched anchor:** the note line is in the prompt (fails on the base);
  - **`--full-docs`:** stdout (CRLF → LF) equals the T003 golden (fails on the base: unrecognized option);
  - **`--docs docs/long.md`:** the same output with and without `--full-docs` (fails on the base: unrecognized option);
  - **determinism:** two runs → byte-identical stdout, in both modes (guard for section mode after the change; on the base, guard for the default);
  - **no criteria:** with a criteria file whose text holds `UNIQUECRITERIONMARKER`, neither mode's prompt contains it (guard: passes on the base);
  - **malformed criteria:** with `MalformedCriteria()`, `compile-critic-prompt` exits 0 with a prompt (expected to fail on the base if the dropped load throws; if the base already tolerates it, record it as a guard in the Notes);
  - **no `source_refs` (guard):** a test without refs → the prompt has today's "*No relevant documentation provided.*" and no document section, in both modes;
  - **unchanged:** JSON shape `{"prompts":[{"id","prompt"}]}`; unknown id → exit 1; missing ref file → exit 1 with `Source ref not found:`; the freshness record `critic-input-TC-001.json` is written in both modes (guards);
  - **ambiguous anchor:** two `## Setup` headings, ref `#setup` → the first section, and stderr has `more than one heading shares the anchor key` (fails on the base: no note).
- [X] T006 [P] Write tests/Spectra.CLI.Tests/Skills/LeanSkillTests.cs (F2 text; FR-009–FR-011; US3). Rows:
  - `SkillContent.Generate` ≤ 16,000 characters and `SkillContent.Update` ≤ 16,000 (CRLF → LF) (fail on the base: 37,176 / 22,914);
  - each links `[reference.md](reference.md)` and names when to read it (fails on the base);
  - **verbatim (guards, pass on the base):** generate "## NON-STOP CONTRACT — Steps 7–9" block with bullets (a)–(d), the "NON-STOP CONTRACT (restated)" paragraph, the `written == 0` / ANOMALY / WARNING lines; update "## NON-STOP CONTRACT" block — compared with copies of the `6a4c41a` text kept in the test;
  - **decision points (guards):** the test keeps the list of every user question and STOP in the `6a4c41a` text of both skills (extract it when writing the row: Step 4 "STOP", the approval, the count / scope questions, the update skill's triage approval, "Re-verify the edited tests with the critic now? (recommended)", "still hallucinated" choices, every `**STOP`) and asserts each is still in SKILL.md.
- [X] T007 [P] Write tests/Spectra.CLI.Tests/Skills/SkillReferenceInstallTests.cs (F2 install; FR-012; US6). Disk-only checks (compile on the base). Rows:
  - **init:** `.claude/skills/spectra-generate/reference.md` and `.claude/skills/spectra-update/reference.md` exist, and `.spectra/skills-manifest.json` has both keys (fail on the base);
  - **`--skip-skills`:** neither file exists (guard);
  - **pre-101:** delete both reference files and their manifest keys (simulating a `6a4c41a` workspace), run `update-skills` → both written and listed under "Updated" (fails on the base);
  - **edited SKILL.md:** append a line to `spectra-update/SKILL.md` → after `update-skills` it is byte-identical and listed under "Skipped (customized by user)", and its `reference.md` is still written (fails on the base for the reference part);
  - **edited agent:** append a line to `.claude/agents/spectra-updater.agent.md`, run `update-skills` → byte-identical (its old frontmatter stays) and listed under "Skipped (customized by user)" (096 rule; guard: passes on the base);
  - **edited reference:** append a line to `spectra-generate/reference.md`, run `update-skills` → byte-identical, listed as customized (fails on the base: no file);
  - **moved workspace:** copy the workspace, run `update-skills` → no "another location" line and both reference keys survive (fails on the base: no file);
  - **count:** `SkillContent.All.Count` stays 16 (guard).
- [X] T008 [P] Write tests/Spectra.CLI.Tests/Skills/NestedCriticTests.cs (F3; FR-014–FR-019; US4; contracts C3, C4). Rows:
  - **agents:** `spectra-generator`, `spectra-updater`, `spectra-repairer` frontmatter `tools: [Bash, Read, Write, Agent]`; the body starts `spectra-critic` per test with suite + id, then one `spectra ai ingest-verdict --suite {suite} --all --ids` call, the one retry for ids without a `results` entry, and lists the verdict line forms `| verdict `, `| critic-disagrees `, `| unverified: `, and `critic: not run` (fail on the base);
  - **agents never write grounding:** none of the three bodies contains `ingest-grounding`, `record-drop` or `spectra delete` (guard, passes on the base);
  - **updater:** runs the critic only with `verify: yes` and never for `written (manual verdict kept)` (fails on the base);
  - **generate skill:** Step 8 routes by the generator's verdict lines; `critic: not run` (or a missing line) → the per-test procedure in `reference.md`; the `ingest-grounding --suite {suite} --all --ids {kept-grounded ids}` line and the 075 guard stay (fails on the base for the routing part);
  - **update skill:** the re-verify question appears before the first `spectra-updater` invocation and passes `verify: yes|no` (fails on the base: it comes after the first `written` line);
  - **critic `full_docs` input (FR-007):** `spectra-critic.agent.md` names `full_docs` and `--full-docs` (fails on the base);
  - **unchanged flows:** the from-description flow and `spectra-review-flagged` still start `spectra-critic` from the main session (guard).
- [X] T009 [P] Write tests/Spectra.CLI.Tests/Skills/AgentFrontmatterTests.cs (F4; FR-020–FR-023; US5). For each `AgentContent.All` entry and each `AgentContent.Rendered("sonnet")` entry, rows:
  - frontmatter keys ⊆ {`name`, `description`, `tools`, `model`} (fails on the base: `disable-model-invocation`, `context`);
  - description starts with `Only for use by the ` and names the skills of contract C6, and contains `Do not delegate to it directly` (fails on the base);
  - no agent body or skill (`SkillContent.All`) says `context: fork` or `never auto-invoked` (fails on the base).
- [X] T010 Proof run. With `git diff --quiet 6a4c41a -- src` holding, stage out the rows that use new members and run T004–T009. Every row except the stated guards fails for the stated reason. Record the counts and reasons in the Notes and in the scratchpad (`101/proof-6a4c41a.txt`). Restore the staged rows.
- [X] T011 Inventory the existing rows affected (research R8) and record "retarget" or "unchanged" for each in the Notes: `GenerateSkillContentTests`, `SkillsManifestTests` (:329, :393-401, :415-428, :575-578, :776), `GenerateOrchestratorTests` :143, `OrchestratorSkillTests` :187, `ManifestNewestTests` :54-56, `GenerateAgentTests` :33-39, `OrchestratorAgentTests` :42-51, `CriticSubagentSkillTests` :25/:32, `NoCopilotIsmsTests` :290-291, `GenerationSkillContractTests` :129, `UpdateSkillsReportTests` :89-98, `AgentInstallLayoutTests` :99/:140. Grep `tests/` again for `context: fork`, `disable-model-invocation`, `tools: [Bash, Read, Write]`, `CriteriaContextLoader` and the moved headings, and add anything new.

**Checkpoint**: rows written and proven; no source edited yet.

---

## Phase 3: User Story 1 — section-scoped critic input (P1) 🎯 MVP

**Goal**: The critic gets the sections a test cites; `--full-docs` gives today's input.

**Independent Test**: T004 and T005 pass; the T003 golden matches.

- [X] T012 [US1] Create src/Spectra.CLI/Source/CriticSourceLoader.cs (research R1; data-model "Critic source item"): `LoadAsync(IReadOnlyList<string> sourceRefs, string repoRoot, string sourceDir, Action<string>? note, CancellationToken ct)` → `IReadOnlyList<SourceDocument>`.
  - Resolve with `SourceRefResolver`; throw `FileNotFoundException(SourceRefDocumentLoader.MissingMessage(...))` for the first non-empty missing ref (today's rule);
  - group by resolved full path in first-reference order;
  - any anchor-less ref → `File.ReadAllTextAsync` whole;
  - otherwise normalise (`SectionExtractor.NormalizeNewlines`), `Headings`, `Match` per anchor; any unmatched → whole file prefixed by one note line per unmatched anchor;
  - else `Extract` each matched anchor, drop sections covered by another cited section of the same document (`CoveredHeadings`) and duplicates, join with `"\n\n"`;
  - ambiguous match → `note("<ref>: more than one heading shares the anchor key '<key>'; the first was used")`;
  - `Title` / `Path` exactly as `SourceRefDocumentLoader` sets them.
- [X] T013 [US1] In src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs (research R2; contract C1):
  - add `--full-docs` (`Option<bool>`, "Give the critic each referenced document whole (today's input) instead of the cited sections"), read it in the handler and pass it to both run paths;
  - in `RunSuiteAsync`, when `sharedDocs` is null and the test has refs: `--full-docs` → `SourceRefDocumentLoader.LoadAsync(...)` (unchanged call); otherwise `CriticSourceLoader.LoadAsync(..., note: l => Console.Error.WriteLine($"Warning: {l}"))`; keep the same `FileNotFoundException` handling (exit 1);
  - remove the `CriteriaContextLoader.LoadCriteriaContextAsync` call and pass no criteria to `CriticPromptCompiler.Compile` (FR-003);
  - the legacy path form and `--docs` ignore `--full-docs`; `RecordAsync` stays in both modes.
- [X] T014 [US1] In src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md (research R3): document the optional input `full_docs: true` → add `--full-docs` to its `compile-critic-prompt` call; say the skills never pass it. (US5's T039 edits the frontmatter of the same file — run them one after the other.)
- [X] T015 [US1] Run T004 and T005; all pass. Check that `dotnet build` has 0 warnings and that `CriticPromptBuilder` is unchanged (`git diff 6a4c41a -- src/Spectra.CLI/Agent/Critic` is empty).

**Checkpoint**: section mode is the default; `--full-docs` matches the golden.

---

## Phase 4: User Story 2 — the quality gate (P1, manual) ⛔ STOP point

**Goal**: Measured evidence that section mode judges at least as well as full-docs mode.

**Independent Test**: quickstart §1, recorded.

- [ ] T016 [US2] Build the branch and pick the gate workspace (≥ 30 tests with section refs, ideally from long documents). Class each test "seen" or "cut" with `--full-docs` (quickstart §1.2). Record the workspace, the suite, the test count per class, the Claude Code version and `ai.critic.model` in the Notes.
- [ ] T017 [US2] Measure prompt sizes in both modes (quickstart §1.3): median and max characters per mode, and how many section-mode items still hit the 8,000-character cut. Record them in the Notes.
- [ ] T018 [US2] Run the critic per test in section mode, then with `full_docs: true` (quickstart §1.4–§1.5), ingesting each run with `ingest-verdict --suite S --all`. Save both verdict sets in the scratchpad (`101/gate-section.json`, `101/gate-full.json`).
- [ ] T019 [US2] Evaluate the gate (quickstart §1.6–§1.7) and record in the Notes: agreement on "seen" tests (≥ 90%), grounded counts on "cut" tests (section ≥ full), overall grounded rates (section ≥ full − 5 points), and every differing test with both verdicts and one line on why. **If any condition fails: stop, report to the user, and do not start Phases 5–7 (F2, F3).** Phase 8 (US5, F4) may still proceed.

**Checkpoint**: gate passed and recorded.

---

## Phase 5: User Story 6 — reference files installed without losing edits (P2)

**Goal**: `init` and `update-skills` install, record and refresh `.claude/skills/<skill>/reference.md` under the 096 rules.

**Independent Test**: T007 passes; quickstart §2.1–§2.3.

- [X] T020 [US6] Create src/Spectra.CLI/Skills/Content/References/spectra-generate.md and src/Spectra.CLI/Skills/Content/References/spectra-update.md with a title line and a short "Reference for the <skill> skill — read only the part SKILL.md names" header (content arrives in US3/US4). In src/Spectra.CLI/Spectra.CLI.csproj add `<EmbeddedResource Include="Skills\Content\References\*.md" />`.
- [X] T021 [US6] In src/Spectra.CLI/Skills/SkillResourceLoader.cs add `ReferencePrefix = "Spectra.CLI.Skills.Content.References."` and `GetAllReferences()` (skill name → content, same `.md` stripping and `{{…TOOLS}}` replacement); make sure `GetAllSkills()` still only takes the `Skills.` prefix. Create src/Spectra.CLI/Skills/SkillReferenceContent.cs with `All`, `Generate`, `Update`.
- [X] T022 [US6] In src/Spectra.CLI/Skills/SkillInstallLayout.cs add `ReferencePath(root, skillName)` → `.claude/skills/<skill>/reference.md`. In src/Spectra.CLI/Skills/ManifestKey.cs teach `InstallTail` the `.claude/skills/<n>/reference.md` ending.
- [X] T023 [US6] In src/Spectra.CLI/Commands/Init/InitHandler.cs (`CreateBundledSkillFilesAsync`), after the skills, install each `SkillReferenceContent.All` entry at `ReferencePath` with `InstallAgentFileAsync` and record its hash. Not run with `--skip-skills`.
- [X] T024 [US6] In src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs, after the skills loop, run each reference through `ProcessFileAsync` (same buckets and wording).
- [X] T025 [US6] Retarget `ManifestNewestTests` :54-56 (`IsSkipped` matches `spectra-generate/SKILL.md`, not the bare name). Run T007 and the existing install rows (`AgentInstallLayoutTests`, `ClaudeSkillsInstallTests`, `SkillsManifestTests`, `UpdateSkillsReportTests`, `ManifestNewestTests`, `UpdateSkillsKeepsEditsTests`, `InitCommandTests`); all pass.
- [X] T026 [US6] Check that tests/Spectra.CLI.Tests/Docs/SourceStringsGuardTests.cs covers src/Spectra.CLI/Skills/Content/References/ (it scans `Skills/Content` recursively) and that `DocChangeGuideTests` doesn't count reference files as skills; add a row to T007's class asserting `SkillReferenceContent.All` has exactly `spectra-generate` and `spectra-update`.

**Checkpoint**: reference files install and refresh like any bundled file.

---

## Phase 6: User Story 4 — the critic inside the work subagents (P2)

**Goal**: The work subagents run the critic for their own tests; the main session routes by one line per test.

**Independent Test**: T008 passes; quickstart §3–§4.

- [X] T027 [P] [US4] In src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md (contract C3): `tools: [Bash, Read, Write, Agent]`; after the ingest, the critic loop over the created ids (suite + id, one fresh critic each, record its final line), one `spectra ai ingest-verdict --suite {suite} --all --ids <ids> --output-format json`, the one retry, and the verdict lines; the `critic: not run` fallback; "never run `ingest-grounding`, `record-drop` or `spectra delete`; never read verdict files". Add the verdict lines to "## Your final message" and its example.
- [X] T028 [P] [US4] The same in src/Spectra.CLI/Skills/Content/Agents/spectra-updater.agent.md, gated by `verify: yes` (no critic with `verify: no`), over the `written` ids only (never `written (manual verdict kept)`).
- [X] T029 [P] [US4] The same in src/Spectra.CLI/Skills/Content/Agents/spectra-repairer.agent.md over its `written` and `unchanged` ids; with the update skill it follows `verify: yes|no`, with the generate skill it always runs.
- [X] T030 [US4] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md (contract C4): Steps 5–7 report the verdict lines with the batch lines; Step 8 routes by them with today's table (kept-grounded, partial → 8b, hallucinated + agreeing line → `record-drop` + `delete`, disagreement / unverified → unverified); `critic: not run` or a missing line → "read the critic fallback in [reference.md](reference.md) and run it for those ids". 8b's re-critic phase routes by the repairer's lines. Keep the `ingest-grounding … --ids` lines, the 075 block and the NON-STOP text verbatim. Update the flow diagram in the intro. Move the per-test critic procedure (today's 8a.1 and its progress JSON) verbatim into src/Spectra.CLI/Skills/Content/References/spectra-generate.md under "## Critic fallback (when a subagent returns `critic: not run`)".
- [X] T031 [US4] In src/Spectra.CLI/Skills/Content/Skills/spectra-update.md (contract C4; research R5): ask "Re-verify the edited tests with the critic now? (recommended)" once, before the first `spectra-updater` call, and pass `verify: yes|no` to every updater and repairer call; Step 5a routes by the verdict lines (today's table; with `no`, the `written` tests are unverified as today); `critic: not run` or a missing line → the fallback in [reference.md](reference.md). Move today's per-test critic procedure verbatim to src/Spectra.CLI/Skills/Content/References/spectra-update.md under "## Critic fallback".
- [X] T032 [US4] In src/Spectra.CLI/Skills/Content/Skills/spectra-review-flagged.md line 48, retarget "same procedure as in `spectra-generate` Step 8" to the generate reference's "Critic fallback" section (it still runs the critic in the main session, FR-019).
- [X] T033 [US4] Retarget the `tools` asserts in `GenerateAgentTests` (generator) and `OrchestratorAgentTests` (updater, repairer) to `[Bash, Read, Write, Agent]` (analyst, extractor unchanged; still no `Edit`), and any 5a / 8a row from T011 that pinned the per-test procedure in SKILL.md to the reference content. T008 passes.

**Checkpoint**: routing unchanged; per-test critic calls leave the main session.

---

## Phase 7: User Story 3 — lean skills that survive compaction (P1)

**Goal**: Both SKILL.md bodies fit within the 5,000 tokens re-attached after compaction; reference material moves to `reference.md`.

**Independent Test**: T006 passes; quickstart §3.5 (token counts) and §3.6 (compaction run).

- [X] T034 [US3] Slim src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md to ≤ 16,000 characters (research R7). Move verbatim to src/Spectra.CLI/Skills/Content/References/spectra-generate.md: "CLI flags reference" with the pre-flight check, "Other tasks (delegation)", "Notes", the intent-routing examples and "How to choose between generation flows", the from-description detail, and the ISTQB / boundary-gap detail. Keep in SKILL.md: a short intent routing table (the decision), a short from-description flow, every question and STOP, the NON-STOP text and the 075 block. Add an "## Additional resources" list naming each reference part and when to read it.
- [X] T035 [US3] Slim src/Spectra.CLI/Skills/Content/Skills/spectra-update.md to ≤ 16,000 characters (research R7): move "Exit codes", "Buckets", "Suggest next steps" and "Example user requests" verbatim to src/Spectra.CLI/Skills/Content/References/spectra-update.md, plus step detail that repeats CLI output fields or long examples (~2,000 characters); tighten Steps 1–5 prose without dropping a question, STOP or CLI call. Add the "## Additional resources" list.
- [X] T036 [US3] Retarget the moved-text rows from T011 to `SkillReferenceContent`, each with a "Spec 101" comment: `GenerateSkillContentTests`, `SkillsManifestTests` :329, :393-401, :415-428, :575-578, :776, and the slice end markers in `GenerateOrchestratorTests` :143 and `OrchestratorSkillTests` :187. List every retargeted row in the Notes (FR-013).
- [ ] T037 [US3] Run T006 and the whole `tests/Spectra.CLI.Tests/Skills` folder; all pass. Record both SKILL.md and reference sizes (characters) in the Notes. Then measure each SKILL.md's token count (quickstart §3.5: `/context` right after invoking the skill in a fresh session, or the API's token count) and record it; each must be under 5,000. If either is over, move more reference material out and measure again.

**Checkpoint**: both skills within their caps; nothing lost.

---

## Phase 8: User Story 5 — agent frontmatter Claude Code reads (P2)

**Goal**: Only documented fields; descriptions route Claude to the skills.

**Independent Test**: T009 passes; quickstart §2.4–§2.5.

- [X] T038 [P] [US5] Edit the frontmatter and line 12 of src/Spectra.CLI/Skills/Content/Agents/spectra-analyst.agent.md and src/Spectra.CLI/Skills/Content/Agents/spectra-extractor.agent.md (contract C6): remove `disable-model-invocation` and `context`; new descriptions starting `Only for use by the spectra-generate skill —` / `Only for use by the spectra-criteria skill —` and ending `Do not delegate to it directly; use the <skill> skill.`; body line 12 "You run in a **fresh, isolated context**" (no `context: fork`).
- [X] T039 [US5] (after T014) The same for src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md (description names the spectra-generate, spectra-update and spectra-review-flagged skills and their subagents; line 15 drops "never auto-invoked" and says it is started only by those skills and their subagents), and for src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md, spectra-updater.agent.md, spectra-repairer.agent.md (descriptions per contract C6; their `tools` lines already changed in US4, T027–T029).
- [X] T040 [US5] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md, in the critic paragraph (line 200 on `6a4c41a`; find it by its text after US3), replace "never skipped, never auto-invoked" and "(a fresh, isolated `context: fork`)" with "never skipped; this skill (or its generator) always starts it explicitly" and "(a fresh, isolated context per test)" (FR-023). Grep the other skills for the same wording; none expected.
- [X] T041 [US5] Retarget the frontmatter rows (T011 list), each with a "Spec 101" comment: `GenerateAgentTests` :33-39 and `OrchestratorAgentTests` :42-51 (drop the `disable-model-invocation` / `context: fork` asserts; keep `name`, `model: inherit`; the `tools` asserts were retargeted in T033), `CriticSubagentSkillTests` :25/:32, `NoCopilotIsmsTests` :290-291, `GenerationSkillContractTests` :129 (assert the new wording). T009 passes.

**Checkpoint**: `update-skills` refreshes unedited agents; edited ones are skipped (existing 096 rows stay green).

---

## Phase 9: Polish, docs, verification

- [X] T042 [P] Update docs/cli-reference.md: `compile-critic-prompt --full-docs`, section-scoped input (what the critic sees, the note for an unmatched anchor, the cut), and line 595's `context: fork` wording.
- [X] T043 [P] Update docs/usage.md (the critic runs inside the generator / updater / repairer; the fallback; the re-verify question now comes before editing; `full_docs` for one test) and docs/grounding-verification.md (what the critic sees; lines 23 and 145).
- [X] T044 [P] Replace the `context: fork` subagent wording with "a subagent with a fresh context" in docs/ai-models-cost-guide.md, docs/claude-code-v2-migration.md, docs/customization.md, docs/getting-started.md and docs/skills-integration.md; mention `reference.md` in docs/customization.md (an edited reference file is kept by `update-skills`).
- [X] T045 [P] Update CHANGELOG.md with "Changed (Spec 101 — token economy follow-up)": F1–F4, the corrected facts (no criteria, the 8,000-character cut), and the re-verify question's new timing.
- [X] T046 [P] Update CLAUDE.md: the Recent Changes row for 101; the Tech Stack line on the critic ("run by the work subagents; the main session only on fallback"); the File-based state line gains `.claude/skills/<name>/reference.md`.
- [X] T047 Run the full `dotnet build --no-incremental` (0 warnings) and `dotnet test` (0 failures). Record the counts against T001. Check that `git diff --stat 6a4c41a -- src` touches only the files in plan.md.
- [X] T048 Fresh checkout (quickstart §5): clone into a new folder, check out the branch, build and test; record the result.
- [ ] T049 Run quickstart §2 (real init, `--skip-skills`, pre-101 workspace with an edited skill, agent and reference, moved workspace, `claude --debug`, auto-delegation spot check) and record the results.
- [ ] T050 Run quickstart §3 and §4 (generation of ≥ 24 tests and update of ≥ 3 groups with `/context` on 101 and on `6a4c41a`; the compaction run; the fallback with `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1`; the "no" answer) and record per-test growth against the 300-token target.
- [ ] T051 Fill in the Notes (baseline, proof, inventory, gate, sizes, retargeted rows, workspace checks, budgets). Ask the user before committing and before merging into `fix/doc-change-update-workflow`; never push.

---

## Dependencies & Execution Order

- **Setup** (T001–T003) → **Foundational** (T004–T011). T010 comes before any source edit; T003 needs the base build.
- **US1** (T012–T015) → **US2 gate** (T016–T019). **A failed gate stops Phases 5–7 (F2, F3).**
- **US6** (T020–T026) after the gate. **US4** (T027–T033) after US6 (its fallback text goes into the reference files). **US3** (T034–T037) after US4.
- **US5** (T038–T041) last by default; it depends only on Phase 2 and may run earlier. T014 (US1) and T039 (US5) both edit `spectra-critic.agent.md`; T027–T029 (US4) and T039 edit the same agent files; T030/T034 and T040 edit `spectra-generate.md`: run them one after the other.
- T027–T029 edit different agent files ([P]); T030 and T034 both edit `spectra-generate.md`; T031 and T035 both edit `spectra-update.md`.
- **Polish:** T042–T046 after the stories; T047–T051 last.

### Parallel opportunities

```text
Phase 2:  T004 T005 T006 T007 T008 T009          (different test files)
US5:      T038 ‖ (T039 after T014)
US4:      T027 T028 T029                          (different agent files)
Polish:   T042 T043 T044 T045 T046               (different docs)
```

## Implementation Strategy

- **MVP:** US1 + US2 — section-scoped critic input, proven by the gate. Smaller and better-targeted critic prompts ship
  even if F2/F3 stop.
- **Then:** US6 (reference install), US4 (critic in subagents), US3 (slim skills), US5 (frontmatter).
- **Finish:** full suite, fresh checkout, quickstart on real-init and pre-101 workspaces, budgets.

## Notes

**Baseline (T001, `6a4c41a`).** Core 1228 / CLI 2345 / Execution 228 / Integration 8, 0 failures. `dotnet build
--no-incremental`: 0 code warnings; 10 NuGet warnings (NU1902/NU1903, the ImageSharp advisories — out of scope).

**Golden (T003).** tests/Spectra.CLI.Tests/TestFixtures/CriticGolden/full-docs-6a4c41a.txt, captured with `src` at
`6a4c41a` by a throwaway test (deleted) that ran `spectra ai compile-critic-prompt --suite s --test TC-001` (human mode)
on `GoldenWorkspace()` and saved stdout with CRLF → LF: 9,658 characters, SHA-256
`0a583faaf73e939d19646d9a8a78d5d64f8f12950a93d44597ce3af76bb7e84c`. It holds `docs/long.md` up to Section 23 and the cut
marker — no Section 30, as research R0 found.

**Proof (T010, `src` untouched at `6a4c41a`).** 92 rows in 6 classes. `CriticSourceLoaderTests` (11 rows) staged out:
"member missing" (`CriticSourceLoader`). Of the other 81: 47 failed, each for the intended reason, and 34 guards passed.
Output: scratchpad `101/proof-6a4c41a.txt`, `101/proof.trx`, `101/proof2.trx`.
- **Critic prompt:** Section 30 not in the prompt (cut); no note for an unmatched anchor; no stderr note for an ambiguous
  anchor; every `--full-docs` row → "Unrecognized command or argument '--full-docs'".
- **Skills:** 36,427 / 22,321 characters (CRLF → LF) over the 16,000 cap; no `[reference.md](reference.md)`.
- **Install:** no `reference.md` after init or `update-skills`; an edited reference "file" is just an unrecorded new file.
- **Nested critic:** work agents' tools `[Bash, Read, Write]`; no critic loop or verdict lines; no `verify:`; the critic
  agent has no `full_docs`; the update skill asks the re-verify question after the first updater call.
- **Frontmatter:** `disable-model-invocation` and `context` in all six agents (bundled and installed); descriptions
  don't start "Only for use by the"; 7 agents/skills say `context: fork` or "never auto-invoked".
- **Guards that passed (34):** section-mode determinism and no-criteria (default mode); no `source_refs` (default);
  unknown id / missing ref exits; JSON shape; the freshness record (default); a malformed criteria file (the base
  already tolerates it — `CriteriaContextLoader` didn't throw, so the spec's edge case "could fail the command" was
  pessimistic; the row stays as a guard); all verbatim NON-STOP / 075 copies and all 16 decision points; `--skip-skills`;
  an edited agent kept (096); 16 skills; the work agents never run `ingest-grounding` / `record-drop` / `spectra delete`;
  the from-description flow and review-flagged still run the critic.
- Two rows were adjusted during the proof so they test what they claim: `AMalformedCriteriaFile…` and `JsonShape…` now
  check `**Path**: docs/long.md` instead of Section 30 (which only section mode has); the install rows' report parser
  slices by header (the progress reporter prefixes headers with a symbol).

**Inventory (T011)** — "retarget" unless marked:
- `GenerateAgentTests` :36-39 (tools line, `disable-model-invocation`, `context: fork`) and :49 ("Never start a
  subagent", generator) — **new**: F3 lets the generator start `spectra-critic` (retarget in T033);
- `OrchestratorAgentTests` :46-49 (same) and :61 ("Never start a subagent", updater / repairer / extractor) — **new**;
- `CriticSubagentSkillTests` :25, :32; `NoCopilotIsmsTests` :68-69; `GenerationSkillContractTests` :22 ("never
  auto-invoked" in the generate skill);
- `GenerateSkillContentTests` :28, :67 and the intent-routing rows; `GenerateOrchestratorTests` :143 (end marker);
- `OrchestratorSkillTests` :114 (end marker "## Exit codes"); `SkillsManifestTests` :776 (`## Buckets` slice), and the
  `CONFIG_MISSING` / `LOCK_TIMEOUT`, delegation-row and CLI-flags-row rows (research R8);
- `ManifestNewestTests` `IsSkipped` (bare skill name);
- unchanged: `ExecutionAgentPortTests` :22 and `NoCopilotIsmsTests` :16 (assert the *absence* of
  `disable-model-invocation` in skills); `UpdateSkillsReportTests`, `AgentInstallLayoutTests` (stay green, checked in
  T025); `Spectra.Integration.Tests/Support/IntegrationWorkspace.cs` uses `CriteriaContextLoader` for generation, not
  the critic.

**US1 (T012–T015).** `CriticSourceLoader` added; `compile-critic-prompt` gains `--full-docs`, picks the loader, and no
longer loads criteria; the critic agent documents `full_docs: true`. T004 + T005 + the `full_docs` row: 29/29 pass.
`CriticPromptBuilder` unchanged (`git diff 6a4c41a -- src/Spectra.CLI/Agent/Critic` empty). Build: 0 code warnings.
Critic / verdict / verification / grounding / config rows: 455 pass; the only failures are the 9 F3 rows not built yet.

**Gate deferred (user decision, 2026-10-19).** T016–T019 stay open: the user runs the F1 quality gate (quickstart §1)
by hand in a real Claude Code session after US3–US6. F2, F3 and F4 are implemented before it. **Decision rule:** if the
gate fails, section mode stops being the default — `compile-critic-prompt` defaults to `--full-docs` (today's input) —
and nothing else changes (section mode stays available behind an explicit option; skills, agents and reference files
stay as built).

**US6 (T020–T026).** `Skills/Content/References/<skill>.md` embedded under its own resource prefix
(`SkillResourceLoader.GetAllReferences`, `SkillReferenceContent`), installed at `SkillInstallLayout.ReferencePath`
by `init` (not with `--skip-skills`) and processed by `update-skills` with the 096 `ProcessFileAsync` rules;
`ManifestKey.InstallTail` knows `.claude/skills/<n>/reference.md`. Install / manifest / init rows: 163 pass.
`SkillContent.All` stays 16 skills.

**US4 + US3 (T027–T037), written together.** Both SKILL.md files had to drop under 16,000 characters, so each was
rewritten once in its final form (F3 routing and F2 slimming in one pass) instead of a routing edit followed by a
slimming edit; the agents got the critic step first (T027–T029). Deviations from the task text:
- **The critic fallback stays in SKILL.md** (see the T006 deviation): the generate skill's verbatim NON-STOP contract
  allows only `spectra` calls and `Write`s in Steps 7–9, so Step 8 cannot send the session to `reference.md`. The
  fallback is a short paragraph in Step 8 (generate) and Step 5a (update). `reference.md` holds only material read
  outside those steps. T032 (`spectra-review-flagged`): nothing to retarget — "same procedure as in
  `spectra-generate` Step 8" still names the fallback, which is in Step 8.
- **The update skill reads its reference.md as instructions, not as a workflow step.** Its verbatim NON-STOP contract
  says "every step below" is a `spectra` call, a Task call or a `Write`; the skill says, above that contract, that
  reading its own `reference.md` is looking up instructions. The prohibited-reads list (docs, prompts, tests, staging,
  verdicts) doesn't include it.
- **What moved to `reference.md`.** Generate: CLI flags reference with the pre-flight check, Step 4 presentation,
  ISTQB and boundary gaps, the resume procedure, the analyze-first trigger phrases, the from-description detail, Test
  Creation Intent Routing, Other tasks (delegation), Notes (all verbatim from `6a4c41a`). Update: Exit codes, Buckets,
  Submodules and first-time setup, Clearing REMOVED entries, Suggest next steps, Example user requests (verbatim).
- **Agents' "never" line** says "Never write grounding, record a drop or delete a test" instead of naming
  `ingest-grounding`, so `OrchestratorAgentTests.TheRepairer_…` (the repairer never mentions `ingest-grounding`)
  stays as it was.
- The update skill's re-verify question is in Step 4 (after the bucket choice, before the first updater call),
  verbatim; `verify: yes|no` goes to every updater and repairer call.

**Sizes (characters, LF).** `spectra-generate` SKILL.md 15,827 (was 36,427) + reference.md 15,253; `spectra-update`
SKILL.md 15,287 (was 22,321) + reference.md 5,294. At 3.5 characters per token ≈ 4,520 and ≈ 4,370 tokens.
**T037 stays open for its manual part:** the measured token count of each SKILL.md (quickstart §3.5, `/context` in a
fresh session) — the automated part (T006 and the whole Skills folder green) is done.

**US5 (T038–T041).** The six agents carry only `name`, `description`, `tools`, `model`; descriptions per contract C6;
"fresh, isolated context" replaces "(`context: fork`)" in every agent body; the critic's "never auto-invoked" line
names who starts it. The generate skill's critic paragraph was rewritten in US3 (T040 done there).

**Retargeted existing rows (FR-013), each with a "Spec 101" comment:**
- moved to `reference.md` → `SkillReferenceContent`: `GenerateSkillContentTests` (intent rows, via
  `AgentContentText` = skill + reference), `SkillsManifestTests.GenerationAgent_ContainsUpdateDelegation`,
  `.UpdateDelegationRows_UseTriage_WithDryRun("spectra-generate")`,
  `.GenerateSkill_StagesRepairsInTheRepairsFolder_NeverTheUpdateFolder` (CLI-flags row; the skill is still checked
  for no `.spectra/updates/`), `.UpdateSkill_DrivesTriage` (exit codes), `.UpdateSkill_RemovedStep_PointsAtTheFixAck…`
  (Step 3 + the reference's "Clearing REMOVED entries"), `.UpdateSkill_RemovedStep_HintsAtMinSimilarity…`,
  `.UpdateSkill_NamesRelinkInTheBucketList`, `.UpdateSkill_WithholdsInit_WhileTheSubmoduleNoteIsPresent`,
  `.UpdateSkill_FirstTimeSetup_RelinksBeforeInit_ThenOffersFromDate` (the skill offers no `--init` after Step 2a; the
  reference's first-time setup does, after relinking, with `--from-date`), `AuditActionTests.TheResumePath_…`,
  `AuditTruthTests.TheGenerateSkillsResumePath_…`, `GenerateOrchestratorTests.GenerateSkill_ResumesFromTheGroundingAudit`;
- slice end markers: `GenerateOrchestratorTests.FromDescription_…` (now ends at "## How to choose between generation
  flows"; the flow is a numbered list), `OrchestratorSkillTests.UpdateSkill_AllSuites_…` (now ends at "## Stop");
- F3 wording: `GenerateOrchestratorTests.GenerateSkill_RoutesByTheClisPerTestVerdicts` and
  `.UpdateSkill_Step5a_RoutesByTheClisPerTestVerdicts` ("progress only" → "never read the verdict file");
  `SkillsManifestTests.UpdateSkill_ReverifiesEditedTests_AskingOnce_WithOneRepair` (the question and "don't ask again"
  are checked in Step 4, `manual_verdict_kept` in Step 5 — the deliberate timing change);
- F3 tools: `GenerateAgentTests` (generator `[Bash, Read, Write, Agent]`), `OrchestratorAgentTests` (updater, repairer);
- F4: `GenerateAgentTests`, `OrchestratorAgentTests` (no `disable-model-invocation` / `context:`, description prefix),
  `CriticSubagentSkillTests` (`CriticAgent_DeclaresForkIsolation` → `CriticAgent_RunsInAFreshContext`,
  `CriticAgent_DisablesAutoInvocation` → `CriticAgent_IsOnlyForTheSkills`), `NoCopilotIsmsTests.CriticSubagent_IsExempt…`,
  `GenerationSkillContractTests.GenerationSkill_InvokesCriticSubagent_AsMandatoryStep` ("never auto-invoked" →
  "mandatory and explicit");
- `ManifestNewestTests.IsSkipped` matches `spectra-generate/SKILL.md` (T025).
Phrases other guards pin were kept in the new text rather than retargeted ("critic of record", `compile-critic-prompt`,
"retries a failed repair ingest at most twice (the same repair attempt)", "only when the critic's line agrees or there
is no line", "created test ids", "Nothing is regenerated automatically", the relink command lines and the exact
wrapping of three update lines).

**Docs (T042–T046).** cli-reference (`--full-docs`, what the critic sees), usage (critic in the subagents, fallback,
question timing, `--full-docs`), grounding-verification, ai-models-cost-guide, claude-code-v2-migration, customization
(`reference.md`, the four agent fields), getting-started, skills-integration (no `context: fork`), CHANGELOG
("Changed (Spec 101 …)"), CLAUDE.md (Recent Changes row, critic line, `reference.md` in file-based state,
`--full-docs` in Commands; 147 lines).

**Full run (T047).** `dotnet build --no-incremental`: 0 code warnings (the 10 NuGet ImageSharp advisories, as on the
baseline). `dotnet test`: Core 1228 / CLI 2438 / Execution 228 / Integration 8, 0 failures (CLI +93 against the
baseline: the 6 new classes plus the T026 row). `git diff --stat 6a4c41a -- src`: 15 files, plus the new
`CriticSourceLoader.cs`, `SkillReferenceContent.cs` and `Skills/Content/References/` — all named in plan.md.
- **Golden re-capture (099 precedent from Spec 100, research R8).** `GoldenOutputTests.FreshWorkspace_…` failed on the
  `CUSTOMIZATION.md` hash: `docs/customization.md` is embedded and `init` writes it, and T044 changed its text. Re-captured
  with `SPECTRA_GOLDEN_RECORD=1` (fresh run only). `git diff -- tests/Spectra.CLI.Tests/Golden/099/`: `files.txt` only,
  7+/3−: the `"CUSTOMIZATION.md": "…"` manifest hash line, the `### CUSTOMIZATION.md (13925 → 14283 bytes)` header and
  the changed paragraph (reference files, the four agent fields). `pre099-workspace/` and `files-pre099.txt` untouched;
  both golden runs pass.

**Fresh checkout (T048, uncommitted work).** Nothing is committed yet, so instead of a clone: every tracked and new
file (`git ls-files -co --exclude-standard`, 1,960 files) was copied to a scratch folder with text files converted to
CRLF as an autocrlf checkout would (1,899 converted; `-text` and `eol=lf` files kept), made a throwaway local git
repository (scratch only), then `dotnet build --no-incremental` (0 code warnings) and `dotnet test`: CLI 2438/2438,
Core 1228, Execution 228, Integration 8 — 0 failures. (A first run without `.git` failed 27 docs / attribute / relink
rows that locate the repository through git — an artifact of the copy, gone with the repository.) Repeat with a real
clone after the commit.

**Still open (manual, Claude Code sessions):** T016–T019 (the F1 gate, run by the user — decision rule above), T037's
token count, T049 (quickstart §2: real init / pre-101 workspace with edits / `claude --debug` / auto-delegation spot
check — the automated install rows cover the file side), T050 (§3–§4 budgets, compaction run, nesting fallback), T051
(final Notes, commit and merge — only when the user says so; never push).

**Deviation found while writing T006 (to apply in T030/T031).** The generate skill's verbatim NON-STOP contract says
every step in Steps 7–9 is a single `spectra` call or a `Write`, and the update skill's says every step "below" is a
`spectra` call, a Task call or a `Write`. So a step can't tell the session to *read* `reference.md` for the critic
fallback. The fallback (start `spectra-critic` per id, then `ingest-verdict --all --ids`, route by `results`) stays in
SKILL.md in a short form; `reference.md` holds only material read outside those steps. `spectra-review-flagged`'s "same
procedure as in `spectra-generate` Step 8" stays valid, so T032 has nothing to retarget.

- **Kept on purpose (analysis L5):** the 075 GROUNDING-INGEST ANOMALY text stays verbatim as the regression gate,
  although after F3 "marked grounded in the critic loop" refers to a loop that runs inside the work subagents.
