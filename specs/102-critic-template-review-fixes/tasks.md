---
description: "Task list for Spec 102 — review fixes for 101 and the critic prompt template"
---

# Tasks: Review Fixes for 101 and the Critic Prompt Template

**Input**: Design documents from `/specs/102-critic-template-review-fixes/`: spec.md, plan.md, research.md (R1–R10),
data-model.md, contracts/contracts.md, quickstart.md.

**Tests**: Required (FR-019). Each change F1–F5 and F7–F9 has a row that fails on `26bfe88` (F7's count row and F8's
guard included). F6 (the removed parameter and `using`) is checked by the compiler (TreatWarningsAsErrors); no row is
added for it.
- Rows are written in Phase 2 and run on the untouched sources (`git diff --quiet 26bfe88 -- src` must hold) before any
  source edit.
- Rows that use new members are staged out for the proof run (as in 094–101); their failure is "member missing". The
  new members are `CriticPromptBuilder(PromptTemplateLoader)`, `CriticPromptCompiler.Compile(test, docs, loader)`,
  `SourceRefResolver.PathComparer`, the internal `CriticSourceLoader.LoadAsync(resolution, comparer, …)` and
  `CriticSourceLoader.Content(…, match, lineStarts)`.
- CLI rows use `BaselineTestWorkspace` with `CriticSourceWorkspace` text (LF, built in code). They run in the
  non-parallel `CriteriaCommands` collection through `_ws.RunAsync`, and compare through `CriticSourceWorkspace.Lf`.
- Skill and agent rows read `SkillContent` / `AgentContent` / `SkillReferenceContent` after CRLF → LF, as
  `NestedCriticTests` does.

**Organization**: by user story (spec.md).
- US1 = the critic uses the template (F1 + F6, P1);
- US2 = mixed-case refs (F2, P1);
- US3 = unchanged repairs keep their verdict (F3, P2);
- US4 = line keywords (F4, P2);
- US5 = smaller fixes and guards (F5, F7, F8, F9, P3).

Execution order (plan.md): US1 → US2 → US3 → US4 → US5. Within US5: F7 + F5 after US2 (same file), F8 after US3 and US4
(it guards their final text), F9 last.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `102-critic-template-review-fixes` (= `26bfe88` plus spec docs): `dotnet build --no-incremental` and `dotnet test`. Record per-project totals, failures, and code / NuGet warnings in the Notes (101's last totals: Core 1228 / CLI 2438 / Execution 228 / Integration 8; NU1902/NU1903 ImageSharp warnings are out of scope).
- [X] T002 [P] Add test fixtures:
  - tests/Spectra.CLI.Tests/TestFixtures/CaseInsensitiveFsFactAttribute.cs — a `FactAttribute` that sets `Skip` on Linux ("the file system is case-sensitive (Linux); covered by the comparer-seam rows"), mirroring `CaseInsensitiveGitFactAttribute` without the git check.
  - In tests/Spectra.CLI.Tests/TestFixtures/CriticSourceWorkspace.cs:
    - `WriteCriticTemplate(this BaselineTestWorkspace ws, string body)` — writes `.spectra/prompts/critic-verification.md` with the bundled frontmatter (`spectra_version`, `template_id: critic-verification`, `description`, the three placeholders) and `body`, LF;
    - `const string Golden102File = "TestFixtures/CriticGolden/full-docs-102.txt"` and `Golden102()` (LF);
    - `UserPart(string prompt)` — the slice from `"\n\n---\n\n## Test Case to Verify"` on (throws if absent).

---

## Phase 2: Foundational — rule rows and proof

- [X] T003 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/CriticTemplatePromptTests.cs (F1; FR-001, FR-002, FR-005; US1; R2; contracts C1), `[Collection(CriteriaCommandsCollection.Name)]`, golden workspace. Rows:
  - **template in every form and mode:** `--suite s --test TC-001`, the same with `--full-docs`, `--suite s` (all tests), `--suite s --test TC-001 --docs docs/short.md`, and the legacy `--test test-cases/s/TC-001.md --docs docs/short.md` each print `## Technique Verification` and `## Arithmetic Verification` (fail on the base: hard-coded prompt);
  - **workspace edit:** `WriteCriticTemplate("You are the CUSTOM critic. UNIQUECRITICMARKER\n\nReturn JSON only.")` → the suite form and the legacy form contain `UNIQUECRITICMARKER` and not "Technique Verification" (fail on the base);
  - **placeholders resolve to empty:** `WriteCriticTemplate("Custom critic. [{{acceptance_criteria}}] [{{test_case}}]\n\nReturn JSON only.")` → the prompt contains `Custom critic. [] []` and neither `{{acceptance_criteria}}` nor `{{test_case}}` (fails on the base: the workspace template isn't used);
  - **broken workspace copy:** a `.spectra/prompts/critic-verification.md` with no frontmatter → exit 0, and the bundled sections are present (guard: the loader's fallback, not a refusal; fails on the base only for its "Technique Verification" assertion);
  - **JSON:** `--output-format json` → stdout parses as exactly one JSON document (`JsonDocument.Parse` on the whole stdout), and `prompts[0].prompt` contains `## Arithmetic Verification` (fails on the base);
  - **user part unchanged:** `UserPart(--full-docs output) == UserPart(Golden())` byte for byte (guard: passes on the base and after);
  - **new golden:** `--full-docs` output equals `Golden102()` (staged until T013 records the file);
  - **comments removed:** the system part doesn't contain `<!--` and doesn't start with a blank line (guard after the change; on the base the hard-coded text passes it too).
- [X] T004 [P] Write tests/Spectra.CLI.Tests/Source/CriticSourceLoaderReviewTests.cs (F2, F5, F7; FR-007, FR-008, FR-014, FR-016; US2, US5; R3, R7). Rows:
  - **mixed-case anchors (seam, every OS):** write `docs/auth.md` with `## Login` and `## Logout`. Build a `SourceRefResolution` by hand: `Resolved` = [`docs/auth.md#login` → `<root>/docs/auth.md`, `docs/Auth.md#logout` → `<root>/docs/Auth.md`], `DistinctFullPaths` = [`<root>/docs/auth.md`]. Call the internal `LoadAsync(resolution, StringComparer.OrdinalIgnoreCase, note, ct)` → one item holding both sections in that order (staged: new overload);
  - **mixed-case anchor-less (seam):** the same with the second ref `docs/Auth.md` (no anchor) → one item, the whole file (staged);
  - **public loader:** `[CaseInsensitiveFsFact]` with `docs/auth.md` on disk. Refs `docs/Auth.md#login` + `docs/auth.md#logout` through the public `LoadAsync` → both sections; refs `docs/Auth.md#login` + `docs/auth.md` → the whole file (fails on the base on Windows/macOS);
  - **end to end through the command:** `[CaseInsensitiveFsFact]`, `[Collection(CriteriaCommandsCollection.Name)]` (put this row in its own small class in the same file if the collection can't apply to the whole class). Use a `BaselineTestWorkspace` with `docs/auth.md` (`## Login`, `## Logout`) and a test `TC-010` in suite `s` citing `docs/Auth.md#login` and `docs/auth.md#logout` (`CitingTest`). Run `_ws.RunAsync(["ai", "compile-critic-prompt", "--suite", "s", "--test", "TC-010"])`: exit 0, and the prompt holds both the Login and the Logout section. The bug is in the command's path, so this row goes through it (fails on the base on Windows/macOS);
  - **resolver comparer:** `SourceRefResolver.PathComparer` is `StringComparer.Ordinal` on Linux, else `StringComparer.OrdinalIgnoreCase` (staged);
  - **F5 kept:** headings `## Setup`, `## Setup`, `## Other`; ref `#setup` → one note `more than one heading shares the anchor key`. Guard: passes on the base and after;
  - **F5 absorbed:** headings `## Parent`, `### Setup`, `### Setup`; refs `#parent`, `#setup` → the content is Parent's section only, and no note (fails on the base: the note is emitted);
  - **F7 counts:** a document with 8 `##` sections, one with two `###` subsections. Call `Content(raw, refs, note, match: counting wrapper over SectionExtractor.Match, lineStarts: counting wrapper over SectionExtractor.LineStarts)` with 6 anchored refs, some absorbed. Assert `match` called exactly 6 times, `lineStarts` exactly once, and the content equals the public path's output for the same refs (staged: the seam is new);
  - **F7 whole-file and unmatched:** an anchor-less ref → `match` 0 times, `lineStarts` 0 times; an unmatched anchor → `lineStarts` 0 times (staged).
- [X] T005 [P] Write tests/Spectra.CLI.Tests/Skills/ReviewFixLineAndToolTests.cs (F3, F4, F9; FR-009–FR-013, FR-018; US3, US4, US5; R4, R5, R9; contracts C3–C5). Rows:
  - **F3 repairer:** the step-5 heading paragraph of `spectra-repairer.agent.md` (from `**5. Verify with the critic**` to "start the") says "your final `written` lines" and not "`written` and `unchanged` lines"; the agent says `unchanged` ids get no critic run but go to the same `ingest-verdict --suite <suite> --all --ids` call; its final-message section still says "one verdict line per `written` / `unchanged` test" (fail on the base);
  - **F3 generate** (SKILL.md only; Steps 7–9 never read reference.md): Step 8b says "verify each `written` repair (an `unchanged` one keeps its verdict)", not "each repair that landed". The re-verify bullet says that a line without a verdict line, or `critic: not run`, goes through the fallback, with "the critic for `written` ids only, `unchanged` ids straight to its ingest call". The `--repaired` paragraph says "re-verified", not "re-critiqued" (fail on the base);
  - **F3 update:** the repair section says:
    - the critic re-verifies each `written` repair;
    - an `unchanged` repair keeps its verdict;
    - the 091 note says "re-verified tests";
    - the repair fallback (repairer returned `critic: not run`) starts the critic only for `written` ids, and the `unchanged` ids go into the same `ingest-verdict --all --ids` call.

    Fails on the base.
  - **F4 generator:** the final-message list has `` `<id> | created: <title>` ``, the example's title lines start `TC-121 | created: ` and `TC-122 | created: `, and no line form in the list is `` `<id> | <title>` `` (fail on the base);
  - **F4 keyword rule:** across the three work agents, every documented `<id> | …` form starts with one of `created:`, `written`, `unchanged`, `failed `, `verdict `, `critic-disagrees `, `unverified: ` after `<id> | `. The updater's two existing longer forms, `<id> | written (manual verdict kept)` and `<id> | unchanged_pending <BUCKET>: <why>`, already start with a fixed keyword and are not changed; list them in the test as allowed (fails on the base for the generator's title form);
  - **F4 skill** (SKILL.md only): `spectra-generate` Step 7's line list names `` `<id> | created: <title>` `` and no longer `` `<id> | <title>` ``, and its verdict-line bullet still lists `verdict`, `critic-disagrees` and `unverified:` (fails on the base);
  - **F9 tools:** the rendered `tools:` line of `spectra-generate`, `spectra-update`, `spectra-criteria` and `spectra-review-flagged` (`SkillContent.All`) contains `Agent` and not `Task` (fails on the base);
  - **F9 wording:** no bundled skill (`SkillContent.All`), reference (`SkillReferenceContent`) or agent (`AgentContent.All`) contains "Task tool", "Task call" or "Task prompt"; the `| Task | SKILL | CLI command |` header is allowed (fails on the base).
- [X] T006 [P] Write tests/Spectra.CLI.Tests/Skills/CriticStepConsistencyTests.cs (F8; FR-017; R8; contracts C3). For each of `spectra-generator`, `spectra-repairer`, `spectra-updater`:
  - take the text from `**<n>. Verify with the critic**` to "routes each test by your verdict line." (LF);
  - split it at "`` `spectra-critic` subagent with the Agent tool ``";
  - assert the shared parts are identical. On failure, name the agent and the first differing line;
  - assert each prefix matches its row in a table kept in the test: step `4` / `5` / `6`; the id-selection words (generator "each id in `ids`"; repairer "your final `written` lines" and the sentence on `unchanged` ids; updater "your final `written` lines" with its exclusions); the `verify:` rule (generator none; repairer "Skip it with `verify: no`"; updater "Only with `verify: yes`").

  Expected on the base: the shared parts already match (a guard), and the repairer's prefix row fails (it still says `written` and `unchanged`). Record which in the Notes.
- [X] T007 Run the proof: with `git diff --quiet 26bfe88 -- src` holding, stage out the rows that need new members (list them in the Notes), build, and run the T003–T006 classes. Record each row as fails-as-expected / guard-passes in the Notes. Any row that should fail but passes is rewritten before Phase 3. Restore the staged rows.

---

## Phase 3: User Story 1 — The critic uses the verification template (Priority: P1) 🎯 MVP

**Goal**: every critic prompt's system part comes from `critic-verification` (workspace copy, else bundled); the
hard-coded prompt and the criteria parameter are gone; the `--full-docs` user part is unchanged.

**Independent Test**: CriticTemplatePromptTests (T003) green; the golden user-part row green; `spectra prompts validate`
row green.

- [X] T008 [US1] In src/Spectra.CLI/Agent/Critic/CriticPromptBuilder.cs (R1, R6): add a required `CriticPromptBuilder(PromptTemplateLoader templateLoader)` constructor (`ArgumentNullException.ThrowIfNull`); remove `SetTemplateLoader`, the `_templateLoader` null branch and the hard-coded raw string; `BuildSystemPrompt()` loads `critic-verification`, resolves `test_case`, `source_document`, `acceptance_criteria` to `""` and returns the result `.Trim()`. Update the class and method doc comments (one prompt, from the template; Spec 102).
- [X] T009 [US1] In src/Spectra.CLI/Verification/CriticPromptCompiler.cs (R1, R6): `Compile(TestCase? test, IReadOnlyList<SourceDocument>? documents, PromptTemplateLoader templateLoader)` and `Assemble(TestCase test, IReadOnlyList<SourceDocument> documents, PromptTemplateLoader templateLoader)`. Drop `criteriaContext`, null-check the loader, and build `new CriticPromptBuilder(templateLoader)`. Keep the `{system}\n\n---\n\n{user}` join and the refusal rule. Update the doc comment.
- [X] T010 [US1] In src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs (R1, R6, R9): create `var templateLoader = new PromptTemplateLoader(currentDir);` once in `RunAsync` and pass it to `RunSuiteAsync` and to the legacy `Compile` call (line 108). Use it in the suite loop's `Compile` (line 255) and replace the "Spec 101 F1 (FR-003): no criteria — the built-in critic prompt never used them" comment with the 102 rule. Remove `using Spectra.CLI.Criteria;` (and any other import the build reports unused). Fix the class doc comment: the critic is the `spectra-critic` subagent on `ai.critic.model`, with no `context: fork`.
- [X] T011 [P] [US1] Retarget the rows that built the builder or compiler without a loader. Pass `new PromptTemplateLoader(<temp dir without .spectra>)` (a small helper per class) in:
  - tests/Spectra.CLI.Tests/Agent/Critic/CriticPromptBuilderTests.cs;
  - tests/Spectra.CLI.Tests/Commands/Generate/VerificationIntegrationTests.cs (4 constructions);
  - tests/Spectra.CLI.Tests/Verification/CriticPromptCompilerTests.cs.

  Keep every assertion; they hold because the template contains the old text (R1). Add one row in CriticPromptBuilderTests: `BuildSystemPrompt()` contains `## Technique Verification` and `## Arithmetic Verification`. List the retargeted rows in the Notes.
- [X] T012 [US1] Build with 0 warnings and run CriticTemplatePromptTests, CriticSectionPromptTests, CriticSourceLoaderTests, CriticPromptBuilderTests, CriticPromptCompilerTests, VerificationIntegrationTests, PromptsCommandTests, BuiltInTemplatesTests and CriticVerificationTemplateTests. Expect everything except the `Golden102` row green.
- [X] T013 [US1] Record the new golden full-docs-102.txt (R2; full-docs-6a4c41a.txt stays unchanged): run `spectra ai compile-critic-prompt --suite s --test TC-001 --full-docs` (human mode) on the golden workspace (`GoldenWorkspace()`) with the 102 build. Save stdout with CRLF → LF as tests/Spectra.CLI.Tests/TestFixtures/CriticGolden/full-docs-102.txt; the csproj's `TestFixtures\CriticGolden\*.txt` glob copies it. Check by diff that it differs from full-docs-6a4c41a.txt only before the `## Test Case to Verify` line. Record the command and SHA-256 in the Notes. Retarget the 101 row `FullDocs_IsByteIdenticalTo6a4c41a` in tests/Spectra.CLI.Tests/Commands/Generate/CriticSectionPromptTests.cs to compare user parts (`UserPart`), and rename it `FullDocs_UserPart_IsByteIdenticalTo6a4c41a`. The full-output comparison is now T003's `Golden102` row. Run both classes: green.

**Checkpoint**: US1 complete — the critic's instructions are the template; customisation works.

---

## Phase 4: User Story 2 — Every cited section reaches the critic, whatever its path casing (Priority: P1)

**Goal**: one path comparer for resolution and grouping.

**Independent Test**: the F2 rows of CriticSourceLoaderReviewTests (T004) green on every OS; end-to-end row green on
Windows.

- [X] T014 [US2] In src/Spectra.Core/Coverage/SourceRefResolver.cs (R3): add `public static StringComparer PathComparer { get; } = PathComparison == StringComparison.Ordinal ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;` with a doc comment ("the one rule for whether two resolved paths name one file"), and use it in `Resolve` instead of the local `comparer`.
- [X] T015 [US2] In src/Spectra.CLI/Source/CriticSourceLoader.cs (R3): split `LoadAsync` into the public overload (resolve and refuse as today, then call the new one with `SourceRefResolver.PathComparer`) and `internal static async Task<IReadOnlyList<SourceDocument>> LoadAsync(SourceRefResolution resolution, StringComparer comparer, Action<string>? note, CancellationToken ct)`. The new overload groups `resolution.Resolved` with `GroupBy(r => r.FullPath, comparer)` (first-reference order) and reads each group from its first ref's `FullPath`. `Path` and `Title` stay as today. Update the class doc comment (Spec 102 F2).
- [X] T016 [US2] Run CriticSourceLoaderReviewTests (F2 rows), CriticSourceLoaderTests, CriticSectionPromptTests, CriticTemplatePromptTests and the Core `SourceRefResolver` tests: green.

**Checkpoint**: US2 complete.

---

## Phase 5: User Story 3 — A repair that changed nothing is not judged again (Priority: P2)

**Goal**: the repairer runs the critic only for `written` repairs; `unchanged` ones are routed by their existing
verdict (R4; 101 FR-026 changed for unchanged repairs only).

**Independent Test**: the F3 rows of ReviewFixLineAndToolTests (T005) green; NestedCriticTests and SkillsManifestTests
still green.

- [X] T017 [P] [US3] In src/Spectra.CLI/Skills/Content/Agents/spectra-repairer.agent.md (R4, contracts C3), step 5's heading paragraph:
  - "For each id in your final `written` lines (the repairs that landed, leftovers included), one at a time, start the";
  - one sentence before the shared block: your `unchanged` ids (the test didn't change, so its verdict still stands) get no critic run, but go to the ingest call below with the `written` ids, and each gets a verdict line from its `results` entry.

  Keep the shared block from "`` `spectra-critic` subagent with the Agent tool ``" on unchanged. Keep "one verdict line per `written` / `unchanged` test" in the final-message section.
- [X] T018 [US3] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md (R4, contracts C4): first free space, then edit 8b, the re-critic phase and the `--repaired` paragraph. All routing stays in SKILL.md: reference.md is never read during Steps 7–9 (SKILL.md lines 19 and 37; Spec 101's design).

  **Size rule (C4, supersedes C1–C3).** `LeanSkillTests` caps the skill at 16,000 characters; never raise the cap, which comes from the 5,000 tokens Claude Code re-attaches after compaction. Numbers measured on an in-memory copy, `SkillContent.Generate` rendered and LF-normalised:

  | Point | Characters |
  |---|---|
  | Base | 15,850 |
  | After freeing (Key rule −128, annotations −86) | 15,636 |
  | After T018 (+140), T022 (+9) and T029 (+6) | ≈ 15,791 |
  | Target | ≤ 15,900 (≈ 209 characters of headroom) |

  Freeing comes first, so the skill never goes over 16,000 at T020, T023 or T031.

  **1. Free space first, before any routing edit:**
  - a. In "## How to choose between generation flows", the **Key rule** line becomes exactly: "**Key rule**: a single test case title → `--from-description`; a topic to explore → the main flow. Never ask about count or scope to decide." (frees 128). Its longer explanation goes into reference.md's "## Test Creation Intent Routing". The heading and the two-row table stay.
  - b. Remove the five "(Spec …)" annotations, each with its leading space (frees 86): " (Specs 093, 094, 101)", " (Spec 093)", " (Spec 072, Spec 093, Spec 101)", " (Spec 095)", " (Spec 094)".

  Step 1b is an explicit exception to "never remove anything from Steps 7–9": the annotations are not instructions. No test pins them, and section markers ("### Step 8", "### Step 9") are prefixes that still match.

  Nothing else is moved or removed: not the NON-STOP contract, the 075 guard, a question, a STOP, or "## MANDATORY: Analyze first, every time". Every line left in "Analyze first" is a decision point.

  **2. Then make exactly these routing edits (≈ +140 in total):**
  - 8b: "has `spectra-critic` verify each repair that landed" → "has `spectra-critic` verify each `written` repair (an `unchanged` one keeps its verdict)". No separate sentence: the repairer still returns a verdict line for an `unchanged` repair (from the same `ingest-verdict` call), so routing by verdict lines is unchanged.
  - Replace the bullet "- Every `written` line is re-verified, leftovers from an earlier run included; one without a verdict line goes through the fallback." with "- Every `written` line is re-verified, leftovers from an earlier run included; one without a verdict line, or `critic: not run`, goes through the fallback — the critic for `written` ids only, `unchanged` ids straight to its ingest call."
  - "re-critiqued" → "re-verified" where it names the `--repaired` id list. Keep "reported `written` or `unchanged` in `results`, minus any dropped — never a failed repair".

  Keep the rest of the 101 text. Check the bundled reference (src/Spectra.CLI/Skills/Content/References/spectra-generate.md) for any sentence saying unchanged repairs are re-criticked; fix it if found, and note the result in the Notes.

  Retarget any test that pins moved text to where the text now lives (GenerateSkillContentTests lines 29 and 60, the `## How to choose between generation flows` end marker in GenerateOrchestratorTests line 148), if either changes; GenerateSkillContentTests line 105's "count or scope" is in the agent text, not this skill. List each retarget in the Notes. `LeanSkillTests`' verbatim and decision-point rows stay green.
- [X] T019 [US3] In src/Spectra.CLI/Skills/Content/Skills/spectra-update.md (R4, contracts C4), repair section:
  - "has `spectra-critic` re-verify each repair that landed" becomes "re-verify each `written` repair (an `unchanged` repair keeps the verdict that sent it to repair)";
  - "then a verdict line per `written` / `unchanged` test" stays;
  - the 091 parenthesis becomes "`--ids` names exactly the re-verified tests";
  - the repair fallback (repairer returned `critic: not run`): the critic only for `written` ids, and the `unchanged` ids go into the same `ingest-verdict --all --ids` call.

  All of this stays in SKILL.md: routing is never moved to reference.md, for the same compaction reason as T018. `SkillContent.Update` stays ≤ 16,000 characters (~15,290 on the base). "Still partial" / "still hallucinated" handling stays. Check src/Spectra.CLI/Skills/Content/References/spectra-update.md the same way as in T018.
- [X] T020 [US3] Run ReviewFixLineAndToolTests (F3 rows), NestedCriticTests, SkillsManifestTests, OrchestratorSkillTests, GenerateOrchestratorTests, SkillReferenceInstallTests and LeanSkillTests (size caps ≤ 16,000 characters still hold): green. If a 101 row pins the old repairer wording ("`written` and `unchanged` lines"), or text T018's freeing step moved or removed (the Key rule explanation, a "(Spec …)" annotation), retarget it and list it in the Notes. Measure `SkillContent.Generate` and `SkillContent.Update` (rendered, LF) and record both in the Notes (expected: generate ≈ 15,776 after T018).

**Checkpoint**: US3 complete.

---

## Phase 6: User Story 4 — A test title can never be mistaken for a verdict (Priority: P2)

**Goal**: every agent result line kind starts with a fixed keyword.

**Independent Test**: the F4 rows of ReviewFixLineAndToolTests (T005) green.

- [X] T021 [P] [US4] In src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md (R5, contracts C3), "Your final message":
  - "one `<id> | created: <title>` line per created test (the ids from `ids`, the titles from your JSON)";
  - the example lines become `TC-121 | created: Upload is refused above the 30 GB quota` and `TC-122 | created: Quota is shown in the account settings`;
  - add one sentence: every line starts, after `<id> | `, with its keyword, and a title only ever follows `created:`.
- [X] T022 [US4] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md, Step 7 (R5, contracts C4):
  - "one `<id> | <title>` line per created test" → "one `<id> | created: <title>` line per created test" (≈ +9 characters). That is the only edit (C3, superseding C2's keyword-rule line): the next bullet already lists every verdict-line form, and with `created:` no title line can be read as one. Step 9 needs no change.

  The from-description flow reads no generator line (R5): no change. Nothing goes to reference.md (it is never read during Steps 7–9). The T018 size rule (C4) applies.
- [X] T023 [US4] Retarget `TheGenerator_GeneratesOneBatch_AndReturnsOneLinePerTest` in tests/Spectra.CLI.Tests/Skills/GenerateAgentTests.cs from `` `<id> | <title>` `` to `` `<id> | created: <title>` ``. Run GenerateAgentTests, ReviewFixLineAndToolTests (F4 rows), NestedCriticTests and LeanSkillTests: green. List the retarget in the Notes. Measure `SkillContent.Generate` (rendered, LF) and record it in the Notes (expected ≈ 15,785).

**Checkpoint**: US4 complete.

---

## Phase 7: User Story 5 — Smaller fixes and guards (Priority: P3)

**Goal**: F5 warning only for kept sections; F7 match once / slice once; F8 consistency guard green; F9 one tool name.

**Independent Test**: the F5/F7 rows of CriticSourceLoaderReviewTests, CriticStepConsistencyTests, and the F9 rows of
ReviewFixLineAndToolTests green; the update-skills rows green.

- [X] T024 [US5] In src/Spectra.Core/Baseline/SectionExtractor.cs: `LineStarts` from `private` to `internal` (returns `List<int>`; doc comment: also used by the critic loader, Spec 102 F7). No other Core change.
- [X] T025 [US5] In src/Spectra.CLI/Source/CriticSourceLoader.cs (R7, data-model "Cited section"), rewrite `Content` as `internal static string Content(string raw, IReadOnlyList<string> refs, Action<string>? note, Func<IReadOnlyList<MarkdownHeading>, string?, HeadingMatch?>? match = null, Func<string, IReadOnlyList<int>>? lineStarts = null)`, defaulting to `SectionExtractor.Match` and `SectionExtractor.LineStarts`:
  - the anchor-less and unmatched branches stay as they are;
  - `matches[i]` comes from one `match` call per anchor;
  - `end_i` = the first heading index after `start_i` with level ≤ `headings[start_i].Level`, else `headings.Count`;
  - ref i is absorbed iff some j has `start_j < start_i < end_j`;
  - kept = the distinct `start_i` of refs not absorbed, in first-reference order;
  - one `lineStarts(text)` call per document;
  - each kept section's text = `text[lineStarts[headings[start].StartLine] .. (end < headings.Count ? lineStarts[headings[end].StartLine] : text.Length)].TrimEnd('\n')`, joined as today;
  - **F5:** the ambiguity note is emitted after the kept set is known, for each ref (once per ref text, ref order) whose match is ambiguous and whose `start_i` is kept.

  Remove the `CoveredHeadings` and `Extract` calls from this file.
- [X] T026 [US5] Run CriticSourceLoaderReviewTests (all rows), CriticSourceLoaderTests, CriticSectionPromptTests and CriticTemplatePromptTests (both golden rows): green with no expectation changed. Record the T004 count results (match 6 / lineStarts 1) in the Notes.
- [X] T027 [US5] Run CriticStepConsistencyTests (T006) against the final US3/US4 text. If a shared part differs, make the three blocks identical in src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md, spectra-repairer.agent.md and spectra-updater.agent.md, keeping only the listed prefix differences. Green.
- [X] T028 [US5] In src/Spectra.CLI/Skills/SkillResourceLoader.cs (R9): `GenerateToolsList = "Read, Write, Edit, Bash, Glob, Grep, Agent"`. Rewrite the comment: Spec 056 tool model; the authoring set adds Agent (Claude Code's subagent tool, `Task` before v2.1.63) to start SPECTRA's subagents (analyst, generator, updater, repairer, extractor, and the critic on fallback); the read-only set as today; the execution set as today.
- [X] T029 [P] [US5] Skill wording (R9):
  - "with the Task tool" → "with the Agent tool" in src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md (4×), spectra-update.md (3×) and spectra-criteria.md (1×);
  - "Task calls" → "Agent calls" in spectra-generate.md (Context budget contract) and spectra-update.md (two places);
  - "a Task call to the" → "an Agent call to the" in spectra-update.md.

  Leave the `| Task | SKILL | CLI command |` table headers in spectra-execute.md and References/spectra-generate.md alone. The T018 size rule (C4) applies: `SkillContent.Generate` and `SkillContent.Update` stay ≤ 16,000 characters (the rendered `tools:` line grows by one character).
- [X] T030 [P] [US5] Agent wording (R9):
  - "(from the Task prompt)" → "(from the Agent call's prompt)" in spectra-analyst, spectra-extractor, spectra-generator, spectra-repairer and spectra-updater (`src/Spectra.CLI/Skills/Content/Agents/*.agent.md`);
  - "(no Task calls)" → "(no Agent calls)" in spectra-analyst and spectra-extractor.
- [X] T031 [US5] Retarget the rows pinning "Task" (list each in the Notes), then run them:
  - tests/Spectra.CLI.Tests/Skills/GenerationSkillContractTests.cs lines 62–63 → "Agent" (comment: the skill starts its subagents with the Agent tool);
  - GenerateOrchestratorTests.cs line 43 and OrchestratorSkillTests.cs line 39 → "Agent";
  - SkillsManifestTests.cs line 668 → "`spectra-critic` subagent with the Agent tool".

  `OrchestratorAgentTests` (`DoesNotContain("Task", tools)`) stays. Run these classes plus ReviewFixLineAndToolTests (F9 rows), CriticStepConsistencyTests, NestedCriticTests and LeanSkillTests: green. Measure `SkillContent.Generate` (rendered, LF) now that T018, T022 and T029 are all in, and record it in the Notes (expected ≈ 15,791). It must be ≤ 15,900. No freeing step is applied here: if it is over 15,900, stop and report, because the estimate was wrong; don't cut more text.
- [X] T032 [US5] Run the update-skills rows unchanged (096; FR-018): UpdateSkillsKeepsEditsTests, UpdateSkillsReportTests, ManifestNewestTests, SkillsManifestTests and SkillReferenceInstallTests. They're content-agnostic and should be green. Record in the Notes that the four rendered skills and five agents change text, so unedited installed copies update and edited ones are kept.

**Checkpoint**: US5 complete — all nine findings fixed.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T033 [P] Update docs/customization.md (R10, FR-006). Line ~93 stays. Line ~353 says:
  - edits to `.spectra/prompts/critic-verification.md` are used by `compile-critic-prompt` (Spec 102; before that the critic ignored them);
  - the critic model is `ai.critic.model` (`spectra config ai.critic.model <model>`, then `spectra update-skills` to render it into `.claude/agents/spectra-critic.agent.md`), not an edit to the agent's `model:`.
- [X] T034 [P] Update docs/cli-reference.md, `compile-critic-prompt` section (R10, contracts C1): the system part comes from the `critic-verification` template (workspace copy, else bundled) in every form and mode, Spec 102; the output shape is unchanged.
- [X] T035 [P] Update docs/grounding-verification.md "How It Works" (R10): the critic's instructions come from the `critic-verification` template (customisable); after a repair, only a test the repair changed is judged again, and an unchanged one keeps its verdict.
- [X] T036 [P] Add a "Spec 102 — review fixes for 101" section to CHANGELOG.md after the Spec 101 section (line ~669). It has Fixed (F1, F2, F4, F5) and Changed (F3, F6, F7, F8, F9) entries, and states that F3 changes 101 FR-026 for unchanged repairs only and that F1 may change verdicts through the 037 / 075 checks.
- [X] T037 [P] Add the CLAUDE.md Recent Changes row at the top of the table: `| 102 | Review fixes for 101: critic uses the critic-verification template, case-insensitive ref grouping, no re-critic for unchanged repairs, `created:` lines, `Agent` tool name |`, and set "Last updated" to 2026-10-20. Update the CLAUDE.md Tech Stack critic sentence only if it describes the prompt source.
- [X] T038 Full verification: `dotnet build --no-incremental` (0 C# warnings) and `dotnet test` (0 failures). Record totals against T001 in the Notes.
- [X] T039 Fresh checkout (quickstart §6): `git worktree add` this branch into a scratch folder with `core.autocrlf=true`, then `dotnet build --no-incremental` and `dotnet test` there. Record the totals and remove the worktree. Investigate any line-ending failure; the system part has checkout line endings by design (R1), so rows must compare through `Lf`.
- [X] T040 Run quickstart §1, §2 (on Windows) and §5 on a scratch workspace made by the real `spectra init`, and record the results. §3 and §4 need a live Claude Code run and are optional. Do them if a session is available; otherwise record them in the Notes as open manual checks. They are not a merge blocker: SC-004's merge criterion is the T005 F3 rows (the repairer starts the critic only for `written` ids, and `unchanged` ids appear in the ingest call).
- [ ] T041 Fill in the Notes: baseline, proof, retargeted rows, golden SHA-256, counts, reference-file check, workspace checks. Ask the user before committing and before merging into `fix/doc-change-update-workflow`; never push.

---

## Dependencies & Execution Order

- **Setup** (T001–T002) → **Foundational** (T003–T007). T007 comes before any source edit.
- **US1** (T008–T013): T008 → T009 → T010 (each builds on the previous signature); T011 after T009; T013 after T012.
- **US2** (T014–T016) after US1 (T015 and T025 both edit CriticSourceLoader.cs; US1 doesn't, but the golden rows of
  T013 must be green before the loader changes).
- **US3** (T017–T020) after Phase 2; independent of US1/US2 code. T018 and T022 both edit `spectra-generate.md`: run
  them one after the other.
- **US4** (T021–T023) after US3 (shared `spectra-generate.md`).
- **US5**: T024–T026 after US2 (same file); T027 after US3 and US4; T028–T032 last (T029 edits skills that T018/T019/T022
  also edit; T030 edits agents that T017/T021 also edit).
- **Polish**: T033–T037 after the stories; T038–T041 last.

### Parallel opportunities

```text
Phase 2:  T003 T004 T005 T006            (different test files)
US1:      T011 ‖ T010                     (tests vs command, after T009)
US3:      T017 ‖ (T018 → T019)            (agent file vs skills)
US5:      T029 ‖ T030                     (skills vs agents)
Polish:   T033 T034 T035 T036 T037        (different docs)
```

## Implementation Strategy

- **MVP:** US1 — the critic gets the instructions it was meant to have since 037/075, and customisation works. It is the
  largest effect on verdict quality and can ship alone.
- **Then:** US2, the second correctness fix, which needs the same test workspace. Then US3 and US4, agent text.
- **Finish:** US5 (cleanups and guards), docs, full suite, fresh worktree, quickstart.
- The 101 F1 quality gate (101 T016–T019) runs after 102 is merged, by hand, on the new prompt (spec Out of Scope).

## Notes

**Baseline (T001, `26bfe88` + spec docs).** `dotnet build --no-incremental`: 0 errors, 0 code warnings, 10 NuGet
warnings (NU1902/NU1903, ImageSharp — out of scope). `dotnet test`: Core 1228 / CLI 2438 / Execution 228 /
Integration 8, 0 failures.

**Proof (T007, `src` untouched).** The new rows ran against 26bfe88's sources:

| Result | Rows |
|---|---|
| Ran | 55 |
| Failed as expected | 33 |
| Guards that pass on the base | 22 |
| Staged out (they need new members) | 5 |

The guards that pass on the base:
- `FullDocs_UserPart_IsByteIdenticalToThePre101Golden`;
- `TheSystemPart_HasNoTemplateComments_AndNoLeadingBlankLine`;
- `TheSharedBlock_IsIdentical_InTheThreeWorkAgents`;
- `AnAmbiguousAnchor_WhoseSectionIsGiven_IsNoted`;
- `EveryResultLine_StartsWithAKeyword` for the repairer and the updater;
- 16 rows of `NoBundledText_CallsTheSubagentToolTask` for texts that never said "Task".

The staged rows:
- `MixedCaseAnchors_GiveBothSections_UnderTheCaseInsensitiveComparer`;
- `AMixedCaseAnchorlessRef_GivesTheWholeFile_UnderTheCaseInsensitiveComparer`;
- `TheResolversComparer_IsOrdinalOnLinux_ElseIgnoreCase`;
- `EachRef_IsMatchedOnce_AndTheDocumentIsSlicedOnce`;
- `AWholeFileRef_OrAnUnmatchedAnchor_ScansNoLineStarts`.

The F2 rows came back at T016 and the F7 rows at T026.

**Deviations found while implementing.**
- **F6:** `using Spectra.CLI.Criteria;` in CompileCriticPromptCommand.cs is *not* unused: `CriteriaCommandIO` (the config
  failure output) lives in that namespace, and the build fails without it. It stays. F6 is the removed `criteriaContext`
  parameter only.
- **F9 vs a verbatim guard:** the update skill's NON-STOP contract, pinned verbatim by `LeanSkillTests`
  (`UpdateNonStop6a4c41a`), said "a Task call to the `spectra-updater`". Per FR-018 ("unless a test pins the old text,
  which is then updated"), the pinned copy changed by that one phrase, to "an Agent call", with a comment. Nothing else
  in the contract changed.
- **Fifth analysis (U4, U5, I5), applied in place:**
  - spectra-update.md is hard-wrapped, so the T019 edits were re-wrapped at about 120 columns. The fallback rule went
    into the existing "(the fallback for a missing line …)" parenthesis.
  - The repairer's `unchanged` sentence sits before "For each id …", so "start the" still runs into the shared block.
  - spectra-update.md has one "Task calls" (line 41) and one "a Task call" (line 49).
- **ReviewFixLineAndToolTests:** the keyword row matches only non-empty forms (`[^`]+`). The generator's new sentence
  quotes `` `<id> | ` `` alone.

**Retargeted rows.**
- `CriticPromptBuilderTests`: the constructor now takes a template loader, plus a new row for the 037 and 075 sections.
- `VerificationIntegrationTests`: four builder constructions take a loader.
- `CriticPromptCompilerTests`: eight `Compile` / `Assemble` calls take a loader.
- `CriticSectionPromptTests`: `FullDocs_IsByteIdenticalTo6a4c41a` is now `FullDocs_UserPart_IsByteIdenticalTo6a4c41a`
  and compares user parts.
- `GenerateAgentTests`: `` `<id> | created: <title>` ``.
- `GenerationSkillContractTests` lines 62–63, `GenerateOrchestratorTests` line 43, `OrchestratorSkillTests` line 39 and
  `SkillsManifestTests` line 668: "Task" → "Agent".
- `LeanSkillTests` `UpdateNonStop6a4c41a`: see Deviations.
- Not affected: `GenerateSkillContentTests` lines 29 and 60, and the `GenerateOrchestratorTests` line 148 marker. The
  heading, the table and the `**Key rule**` prefix stayed.

**Golden (T013).** The recorder was a temporary test, since deleted. It ran `compile-critic-prompt --suite s --test
TC-001 --full-docs` (human mode) on `GoldenWorkspace()` with the 102 build and saved stdout with CRLF → LF to
`TestFixtures/CriticGolden/full-docs-102.txt`.
- SHA-256: `623facf009c1ffa748a0edbb553ac9eac12124f80a527949d1d909e05365ef4f`, 12,820 bytes.
- Its user part (from `\n\n---\n\n## Test Case to Verify`) equals `full-docs-6a4c41a.txt`'s byte for byte.
- The system part grew from 1,285 to 4,354 characters.

**Skill sizes** (`SkillContent.*` rendered, LF):

| Point | spectra-generate | spectra-update |
|---|---|---|
| Base | 15,850 | 15,287 |
| After the T018 freeing steps (−128 Key rule, −86 annotations) | 15,636 | |
| After T018 / T019 | 15,776 | 15,475 |
| After T022 | 15,785 | |
| After T029 | 15,791 | 15,482 |

Both freeing steps were applied inside T018. The T031 target of ≤ 15,900 holds, with no further freeing. The Key rule's
explanation is now "Key rule (in full)" in References/spectra-generate.md's "Test Creation Intent Routing".

**Reference check (T018, T019).** Neither reference file says unchanged repairs are re-criticked, so neither changed
for F3.

**F7 counts (T026).**
- 6 anchored refs (2 absorbed) to one document: `match` 6 calls, `lineStarts` 1 call.
- An anchor-less ref: 0 and 0.
- An unmatched anchor: `lineStarts` 0.

The existing `CriticSourceLoaderTests` and both golden rows are unchanged and green.

**F8 (T027).** The shared block was already identical in the three agents; the guard is green with no agent change.

**Update-skills (T032).** `UpdateSkillsKeepsEditsTests`, `UpdateSkillsReportTests`, `ManifestNewestTests`,
`SkillsManifestTests` and `SkillReferenceInstallTests` are green unchanged. Four rendered skills (`tools:` now ends in
`Agent`) and five agents change text, so `update-skills` updates unedited installed copies and keeps edited ones (096).

**Full verification (T038).** `dotnet build --no-incremental`: 0 errors, 0 code warnings (the 10 NuGet ImageSharp
warnings as at baseline). `dotnet test`: Core 1228 / CLI 2499 (+61 against T001) / Execution 228 / Integration 8.
- The first run had 1 failure: `GoldenOutputTests.FreshWorkspace_MatchesTheGoldenOutput` (099). Its `files.txt` holds
  init's `CUSTOMIZATION.md`, which embeds docs/customization.md, so T033's deliberate sentence (FR-006) changed it.
- A record run to a scratch folder showed `files.txt` was the only difference: the `CUSTOMIZATION.md` hash, size and
  one line. All 27 step outputs were byte-identical.
- `files.txt` was re-captured, as Spec 100 did, with a note in the class remarks. All three golden rows are green.

**Fresh checkout (T039).** A detached worktree of a temporary commit object holding the working tree (no branch or
ref changed), checked out with `core.autocrlf=true`; the critic template and the 102 golden were CRLF on disk.
`dotnet build --no-incremental`: 0 errors, 0 code warnings. `dotnet test`: Core 1228 / CLI 2499 / Execution 228 /
Integration 8, 0 failures. The worktree has been removed.

**Quickstart (T040)**, on scratch workspaces made by the real `spectra init`:
- §1: in every mode and form (section mode, `--full-docs`, legacy `--test <path> --docs`), the prompt holds both
  `## Technique Verification` and `## Arithmetic Verification`. A `QUICKSTARTMARKER` line added to
  `.spectra/prompts/critic-verification.md` appears in the output. `prompts validate critic-verification` passes (the
  quickstart line was corrected: the command takes a template id). JSON mode parses as one document holding the marker.
- §2 (Windows): refs `docs/Auth.md#login` + `docs/auth.md#logout` gave both sections; `docs/Auth.md#login` +
  `docs/auth.md` gave the whole file once.
- §5: a workspace from the `26bfe88` build (`tools: [… Task]`) with an edited `spectra-update` SKILL.md. The 102
  `update-skills` updated `spectra-generate` (`tools` ends in `Agent`) and the agents (the generator now says
  `created: <title>`), reported `spectra-update` under "Skipped (customized by user)" and kept the edit.
- §3 and §4 (live Claude Code runs: an unchanged repair skipping its critic call, and `created:` lines from a real
  batch) were not run. They are open manual checks and not a merge blocker (SC-004: the T005 F3 rows are the merge
  criterion).

