# Tasks: Bundled SKILLs and Agent Prompts

**Input**: Design documents from `/specs/022-bundled-skills/`

## Phase 1: Setup

- [x] T001 [P] Create SkillContent static class with 6 SKILL file content strings in src/Spectra.CLI/Skills/SkillContent.cs
- [x] T002 [P] Create AgentContent static class with 2 agent file content strings in src/Spectra.CLI/Skills/AgentContent.cs
- [x] T003 Create FileHasher utility (SHA-256 of file content) in src/Spectra.CLI/Infrastructure/FileHasher.cs

---

## Phase 2: Foundational

- [x] T004 Create SkillsManifest model and SkillsManifestStore (read/write .spectra/skills-manifest.json) in src/Spectra.CLI/Skills/SkillsManifest.cs

---

## Phase 3: User Story 1 - Init Creates SKILL and Agent Files (Priority: P1) MVP

- [x] T005 [US1] Add --skip-skills option to InitCommand in src/Spectra.CLI/Commands/Init/InitCommand.cs
- [x] T006 [US1] Update InitHandler to create all 6 SKILL files and 2 agent files during init, save manifest with hashes, respect --skip-skills and --force in src/Spectra.CLI/Commands/Init/InitHandler.cs

---

## Phase 4: User Story 2 - SKILLs Invoke CLI with Structured Output (Priority: P1)

- [x] T007 [US2] Write spectra-generate SKILL content with all CLI flags including --from-suggestions, --from-description, --auto-complete in src/Spectra.CLI/Skills/SkillContent.cs
- [x] T008 [US2] Write spectra-coverage SKILL content with coverage JSON parsing instructions in src/Spectra.CLI/Skills/SkillContent.cs
- [x] T009 [P] [US2] Write spectra-dashboard, spectra-validate, spectra-list, spectra-init-profile SKILL contents in src/Spectra.CLI/Skills/SkillContent.cs
- [x] T010 [US2] Write execution and generation agent prompt contents in src/Spectra.CLI/Skills/AgentContent.cs

---

## Phase 5: User Story 3 - Update SKILLs on CLI Upgrade (Priority: P2)

- [x] T011 [US3] Create UpdateSkillsCommand in src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsCommand.cs
- [x] T012 [US3] Create UpdateSkillsHandler with hash comparison logic in src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs
- [x] T013 [US3] Register UpdateSkillsCommand in Program.cs root command in src/Spectra.CLI/Program.cs

---

## Phase 6: Polish

- [x] T014 [P] Add unit tests for FileHasher in tests/Spectra.CLI.Tests/Infrastructure/FileHasherTests.cs
- [x] T015 [P] Add unit tests for SkillsManifestStore in tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs
- [x] T016 Verify all existing tests pass (dotnet test)

---

## Dependencies

- Setup (Phase 1): No dependencies
- Foundational (Phase 2): Depends on Setup
- US1 (Phase 3): Depends on Foundational
- US2 (Phase 4): Can run after Setup (content writing)
- US3 (Phase 5): Depends on US1 (needs manifest)
- Polish (Phase 6): After all stories
