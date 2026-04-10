# Tasks: Customizable Root Prompt Templates

**Input**: Design documents from `/specs/030-prompt-templates/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests ARE included as this project has an established test suite (1279+ tests) and the constitution requires test coverage.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Core models, template engine, and built-in template resources

- [x] T001 Create PromptTemplate and PlaceholderSpec records in src/Spectra.Core/Models/PromptTemplate.cs
- [x] T002 Create CategoryDefinition record in src/Spectra.Core/Models/Config/CategoryDefinition.cs
- [x] T003 Create AnalysisConfig class with Categories list in src/Spectra.Core/Models/Config/AnalysisConfig.cs
- [x] T004 Add Analysis property (type AnalysisConfig) to SpectraConfig in src/Spectra.Core/Models/Config/SpectraConfig.cs with JSON property name "analysis" and default new()
- [x] T005 [P] Create PlaceholderResolver with {{var}}, {{#if var}}...{{/if}}, {{#each var}}...{{/each}} support and HTML comment stripping in src/Spectra.CLI/Prompts/PlaceholderResolver.cs
- [x] T006 [P] Create PromptTemplateParser to split markdown frontmatter (--- delimited YAML) from body in src/Spectra.CLI/Prompts/PromptTemplateParser.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Template loading infrastructure, built-in templates, and resource embedding that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T007 Create 5 built-in template .md files as embedded resources in src/Spectra.CLI/Prompts/Content/ (behavior-analysis.md, test-generation.md, criteria-extraction.md, critic-verification.md, test-update.md) — extract current hardcoded prompts from BehaviorAnalyzer.BuildAnalysisPrompt, GenerationAgent.BuildFullPrompt, CriteriaExtractor.BuildExtractionPrompt, CriticPromptBuilder.BuildSystemPrompt, and create test-update template for UpdateHandler AI rewrite step
- [x] T008 Add EmbeddedResource entries for src/Spectra.CLI/Prompts/Content/*.md in src/Spectra.CLI/Spectra.CLI.csproj
- [x] T009 Create BuiltInTemplates static class (loads embedded resources via assembly reflection, caches in dictionary, provides typed access per template ID) in src/Spectra.CLI/Prompts/BuiltInTemplates.cs
- [x] T010 Create PromptTemplateLoader (LoadTemplate with user-file-first fallback to built-in, Resolve method delegating to PlaceholderResolver, warning log on invalid user files) in src/Spectra.CLI/Prompts/PromptTemplateLoader.cs
- [x] T011 Create DefaultCategories static helper returning the 6 default CategoryDefinition objects (positive-path, negative-path, edge-case, boundary, error-handling, security) in src/Spectra.CLI/Prompts/DefaultCategories.cs

**Checkpoint**: Template engine infrastructure ready — user story implementation can begin

---

## Phase 3: User Story 1 - Use Default Prompt Templates Out of the Box (Priority: P1) MVP

**Goal**: All AI operations use template-driven prompts with built-in defaults. Existing behavior is preserved.

**Independent Test**: Run `dotnet test` — all 1279+ existing tests pass. Built-in templates produce equivalent prompts to the previous hardcoded versions.

### Tests for User Story 1

- [x] T012 [P] [US1] Create PlaceholderResolverTests with tests for: simple var substitution, missing var resolves to empty string, {{#if}} included when non-empty, {{#if}} excluded when empty, {{#each}} expands list items, {{#each}} empty list removes block, nested blocks throw, HTML comments stripped in tests/Spectra.CLI.Tests/Prompts/PlaceholderResolverTests.cs
- [x] T013 [P] [US1] Create PromptTemplateLoaderTests with tests for: user file preferred over built-in, invalid user file falls back to built-in with warning, missing file returns built-in, hash tracking detects modifications in tests/Spectra.CLI.Tests/Prompts/PromptTemplateLoaderTests.cs
- [x] T014 [P] [US1] Create BuiltInTemplatesTests verifying all 5 templates exist as embedded resources and parse without errors in tests/Spectra.CLI.Tests/Prompts/BuiltInTemplatesTests.cs
- [x] T015 [P] [US1] Create PromptTemplateTests for frontmatter parsing (valid YAML extracted, body preserved, invalid YAML returns error) in tests/Spectra.Core.Tests/Models/PromptTemplateTests.cs

### Implementation for User Story 1

- [x] T016 [US1] Replace hardcoded prompt in BehaviorAnalyzer.BuildAnalysisPrompt with PromptTemplateLoader.LoadTemplate("behavior-analysis") and Resolve() — inject PromptTemplateLoader, build placeholder dictionary from existing method parameters (documents, focusArea), add categories from config in src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs
- [x] T017 [US1] Replace hardcoded prompt in GenerationAgent.BuildFullPrompt with PromptTemplateLoader.LoadTemplate("test-generation") and Resolve() — map existing parameters (userPrompt, requestedCount, criteriaContext) to template placeholders in src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs
- [x] T018 [US1] Replace hardcoded prompt in CriteriaExtractor.BuildExtractionPrompt with PromptTemplateLoader.LoadTemplate("criteria-extraction") and Resolve() — map docPath, content, component to template placeholders in src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs
- [x] T019 [US1] Replace hardcoded prompt in CriticPromptBuilder.BuildSystemPrompt with PromptTemplateLoader.LoadTemplate("critic-verification") and Resolve() in src/Spectra.CLI/Agent/Critic/CriticPromptBuilder.cs
- [x] T020 [US1] Verify all existing tests pass (dotnet test across all 3 test projects) — no behavioral regression from template refactor

**Checkpoint**: All AI operations use template-driven prompts. All existing tests pass. Users see no behavior change.

---

## Phase 4: User Story 2 - Customize Prompt Templates for Domain-Specific Quality (Priority: P1)

**Goal**: Users can edit `.spectra/prompts/*.md` files and have their customizations used at runtime.

**Independent Test**: Create a custom template in `.spectra/prompts/behavior-analysis.md` with modified instructions, run generation, verify the custom prompt is used.

### Tests for User Story 2

- [x] T021 [P] [US2] Add integration test: custom behavior-analysis template in .spectra/prompts/ is loaded instead of built-in, and custom text appears in resolved prompt in tests/Spectra.CLI.Tests/Prompts/PromptTemplateLoaderTests.cs
- [x] T022 [P] [US2] Add integration test: invalid custom template (bad YAML frontmatter) falls back to built-in with warning logged in tests/Spectra.CLI.Tests/Prompts/PromptTemplateLoaderTests.cs

### Implementation for User Story 2

- [x] T023 [US2] Ensure PromptTemplateLoader.LoadTemplate checks .spectra/prompts/{templateId}.md first, parses it, and returns user template with IsUserCustomized=true (this should already work from T010, verify with T021/T022 tests)
- [x] T024 [US2] Verify {{#if}} conditional blocks work end-to-end: BehaviorAnalyzer passes focus_areas placeholder, template includes/excludes FOCUS section based on --focus flag presence

**Checkpoint**: Custom templates are loaded and used. Invalid templates fall back gracefully.

---

## Phase 5: User Story 3 - Manage Templates via CLI (Priority: P2)

**Goal**: `spectra prompts list/show/reset/validate` commands for template management.

**Independent Test**: Run `spectra prompts list` to see all templates, `spectra prompts validate` to check one, `spectra prompts reset` to restore.

### Tests for User Story 3

- [x] T025 [P] [US3] Create PromptsCommandTests with tests for: list shows 5 templates with status, list JSON output valid, show displays content, show --raw shows placeholders, reset restores default, reset --all resets all, validate valid exits 0, validate unknown placeholder exits 2, validate bad syntax exits 2 in tests/Spectra.CLI.Tests/Commands/Prompts/PromptsCommandTests.cs

### Implementation for User Story 3

- [x] T026 [P] [US3] Create PromptsListResult and TemplateStatus models in src/Spectra.CLI/Results/PromptsListResult.cs
- [x] T027 [US3] Create PromptsCommand root command (spectra prompts) with list/show/reset/validate subcommands registration in src/Spectra.CLI/Commands/Prompts/PromptsCommand.cs
- [x] T028 [US3] Create PromptsListHandler — enumerate 5 known template IDs, check file existence and hash against manifest to determine status (customized/default/missing), output human table or JSON via PromptsListResult in src/Spectra.CLI/Commands/Prompts/PromptsListHandler.cs
- [x] T029 [US3] Create PromptsShowHandler — load template by ID (user file or built-in), display content (--raw shows unresolved placeholders, default also shows raw since no runtime context) in src/Spectra.CLI/Commands/Prompts/PromptsShowHandler.cs
- [x] T030 [US3] Create PromptsResetHandler — overwrite user file with built-in default, update SkillsManifest hash, support --all flag in src/Spectra.CLI/Commands/Prompts/PromptsResetHandler.cs
- [x] T031 [US3] Create PromptsValidateHandler — parse template, check all {{placeholder}} names against declared list (warn on unknowns), check {{#if}}/{{#each}} blocks are closed, report errors, exit code 0/2 in src/Spectra.CLI/Commands/Prompts/PromptsValidateHandler.cs
- [x] T032 [US3] Register PromptsCommand in the root CLI command builder (same location where DashboardCommand, DocsCommand, etc. are registered) in src/Spectra.CLI/Commands/ root registration

**Checkpoint**: All 4 subcommands work with human and JSON output. Exit codes are correct.

---

## Phase 6: User Story 4 - Configure Domain-Specific Behavior Categories (Priority: P2)

**Goal**: Custom categories in `spectra.config.json` flow through analysis, generation, frontmatter, dashboard, and validation.

**Independent Test**: Add custom categories to config, run `spectra ai generate`, verify behaviors use custom categories.

### Tests for User Story 4

- [x] T033 [P] [US4] Create CategoryDefinitionTests: default 6 categories returned when config absent, custom categories loaded from config, category serialization round-trip in tests/Spectra.Core.Tests/Models/Config/CategoryDefinitionTests.cs
- [x] T034 [P] [US4] Add test: BehaviorAnalyzer injects categories into prompt text via {{#each categories}} placeholder in tests/Spectra.CLI.Tests/Agent/BehaviorAnalyzerTests.cs (extend existing test file)
- [x] T035 [P] [US4] Add test: spectra validate warns on unknown category value (not error) in tests/Spectra.CLI.Tests/Validation/ (extend existing validation tests)

### Implementation for User Story 4

- [x] T036 [US4] Wire category loading in BehaviorAnalyzer: read analysis.categories from SpectraConfig (or DefaultCategories if absent), format as list of {id, description} objects, pass to PromptTemplateLoader.Resolve as "categories" placeholder in src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs
- [x] T037 [US4] Add category validation warning in ValidateCommand: if test frontmatter has category field not matching any configured category ID, emit warning (not error) in src/Spectra.CLI/Commands/Validate/ValidateCommand.cs
- [x] T038 [US4] Update InitHandler to write default analysis.categories (6 categories) to spectra.config.json during spectra init in src/Spectra.CLI/Commands/Init/InitHandler.cs

**Checkpoint**: Custom categories work end-to-end. Existing tests with old categories still pass.

---

## Phase 7: User Story 5 - Safe Template Updates Across Versions (Priority: P2)

**Goal**: `spectra update-skills` updates unmodified templates, preserves customized ones.

**Independent Test**: Modify one template, run `spectra update-skills`, verify modified template preserved and unmodified templates updated.

### Tests for User Story 5

- [x] T039 [P] [US5] Add tests: update-skills updates unmodified templates, skips modified templates, creates new template files for new templates in tests/Spectra.CLI.Tests/Skills/ (extend existing UpdateSkills tests)

### Implementation for User Story 5

- [x] T040 [US5] Add prompt template entries to the file dictionary processed by UpdateSkillsHandler.ExecuteAsync — add all 5 template paths (.spectra/prompts/*.md) alongside existing SKILL/agent entries, using BuiltInTemplates content and FileHasher for hash comparison in src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs
- [x] T041 [US5] Update InitHandler to create .spectra/prompts/ directory and write all 5 default template files during spectra init, updating SkillsManifest with their hashes in src/Spectra.CLI/Commands/Init/InitHandler.cs

**Checkpoint**: Template lifecycle (create on init, update on update-skills, preserve customizations) works.

---

## Phase 8: User Story 6 - Invalid Template Graceful Fallback (Priority: P3)

**Goal**: Invalid user templates fall back to built-in defaults with warnings.

**Independent Test**: Create a template with broken YAML, run generation, verify it completes using fallback.

### Implementation for User Story 6

- [x] T042 [US6] Verify PromptTemplateLoader fallback behavior is already implemented from T010/T022 — add edge case tests: empty template file falls back with warning, template with valid frontmatter but empty body falls back with warning in tests/Spectra.CLI.Tests/Prompts/PromptTemplateLoaderTests.cs

**Checkpoint**: All error scenarios handled gracefully.

---

## Phase 9: User Story 7 - SKILL Integration for Copilot Chat (Priority: P3)

**Goal**: New `spectra-prompts` SKILL (11th SKILL) for Copilot Chat.

**Independent Test**: Verify SKILL file contains correct commands, step format, and is registered in SkillContent.

### Tests for User Story 7

- [x] T043 [P] [US7] Add tests: spectra-prompts SKILL content exists, contains step format, has correct CLI commands with --output-format json --no-interaction flags, SKILL count is 11 in tests/Spectra.CLI.Tests/Skills/ (extend existing SKILL tests)

### Implementation for User Story 7

- [x] T044 [US7] Create spectra-prompts.md SKILL file with step-by-step commands for list, show, validate, reset operations in src/Spectra.CLI/Skills/Content/Skills/spectra-prompts.md
- [x] T045 [US7] Add Prompts property to SkillContent.cs mapping to spectra-prompts SKILL in src/Spectra.CLI/Skills/SkillContent.cs
- [x] T046 [US7] Update spectra-generate SKILL to add note about customizable prompt templates in src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md
- [x] T047 [US7] Update agent prompts (generation and execution) to reference prompt template customization and add delegation row for spectra prompts command in src/Spectra.CLI/Skills/Content/Agents/

**Checkpoint**: SKILL is bundled and agents delegate to it.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, version bump, final validation

- [x] T048 [P] Update CLAUDE.md: add prompt templates to project structure, add spectra prompts commands to CLI reference, update SKILL count to 11, add 030 to recent changes in CLAUDE.md
- [x] T049 [P] Update README.md: add Prompt Templates section documenting customization, quick examples in README.md
- [x] T050 [P] Update PROJECT-KNOWLEDGE.md: add prompt template architecture section in docs/PROJECT-KNOWLEDGE.md
- [x] T051 Update CHANGELOG.md with version bump and feature summary in CHANGELOG.md
- [x] T052 Run full test suite (dotnet test) and verify all tests pass including new tests
- [x] T053 Run spectra prompts list and spectra prompts validate on all 5 templates to verify end-to-end CLI functionality

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (models + resolver + parser)
- **User Story 1 (Phase 3)**: Depends on Phase 2 (built-in templates + loader)
- **User Story 2 (Phase 4)**: Depends on Phase 3 (templates must be loaded before custom loading)
- **User Story 3 (Phase 5)**: Depends on Phase 2 (loader infrastructure) — can run parallel to US1/US2
- **User Story 4 (Phase 6)**: Depends on Phase 3 (BehaviorAnalyzer must use templates) — can run parallel to US3
- **User Story 5 (Phase 7)**: Depends on Phase 2 (built-in templates exist) — can run parallel to US1-US4
- **User Story 6 (Phase 8)**: Depends on Phase 3 (fallback behavior from US1)
- **User Story 7 (Phase 9)**: Depends on Phase 5 (CLI commands must exist for SKILL to reference)
- **Polish (Phase 10)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational — no story dependencies. MVP.
- **US2 (P1)**: Depends on US1 (templates must be loading before custom ones matter)
- **US3 (P2)**: Depends on Foundational only — can run parallel to US1
- **US4 (P2)**: Depends on US1 (BehaviorAnalyzer must use templates for categories to inject)
- **US5 (P2)**: Depends on Foundational only — can run parallel to US1
- **US6 (P3)**: Depends on US1 (fallback scenarios)
- **US7 (P3)**: Depends on US3 (CLI commands must exist for SKILL reference)

### Within Each User Story

- Tests written first (where applicable)
- Models/infrastructure before services
- Services before CLI commands
- All tests pass before checkpoint

### Parallel Opportunities

- T001-T004 (models) can run in parallel as they're separate files
- T005-T006 (resolver + parser) can run in parallel
- T012-T015 (US1 tests) can all run in parallel
- T016-T019 (US1 AI class modifications) are independent files, but T020 must follow
- T025-T026 (US3 tests + result model) can run in parallel
- T033-T035 (US4 tests) can all run in parallel
- T048-T050 (docs) can all run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel:
Task: "T012 PlaceholderResolverTests in tests/Spectra.CLI.Tests/Prompts/"
Task: "T013 PromptTemplateLoaderTests in tests/Spectra.CLI.Tests/Prompts/"
Task: "T014 BuiltInTemplatesTests in tests/Spectra.CLI.Tests/Prompts/"
Task: "T015 PromptTemplateTests in tests/Spectra.Core.Tests/Models/"

# Then launch all AI class modifications in parallel:
Task: "T016 BehaviorAnalyzer template integration"
Task: "T017 GenerationAgent template integration"
Task: "T018 CriteriaExtractor template integration"
Task: "T019 CriticPromptBuilder template integration"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T006)
2. Complete Phase 2: Foundational (T007-T011)
3. Complete Phase 3: User Story 1 (T012-T020)
4. **STOP and VALIDATE**: All existing tests pass, templates produce equivalent prompts
5. This is deployable — no user-visible change, but internal refactor is complete

### Incremental Delivery

1. Setup + Foundational -> Template engine ready
2. US1 -> All prompts template-driven (internal refactor, no user impact)
3. US2 -> Users can customize templates (core value delivered)
4. US3 -> CLI management commands (team workflow)
5. US4 -> Custom categories (domain customization)
6. US5 -> Safe updates (maintenance)
7. US6 -> Robustness (edge cases)
8. US7 -> SKILL integration (Copilot Chat)
9. Polish -> Docs, version, final validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Total: 53 tasks across 10 phases
- The built-in templates (T007) must faithfully reproduce the current hardcoded prompts to ensure no behavioral regression
- TestClassifier.cs is NOT modified — it uses local Jaccard similarity, not AI prompts
- CriteriaExtractor.BuildSplitPrompt (19 lines) stays hardcoded per YAGNI decision
- Template hashes go into existing SkillsManifest (no new manifest file)
