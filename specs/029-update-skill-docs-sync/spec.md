# Feature Specification: SPECTRA Update SKILL + Documentation Sync

**Feature Branch**: `029-update-skill-docs-sync`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "SPECTRA Update SKILL + Documentation Sync"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Update Tests via Copilot Chat (Priority: P1)

A user working in VS Code Copilot Chat says "update tests for the checkout suite" or "my docs changed, update the tests." The Chat agent recognizes this as an update request and delegates to the `spectra-update` SKILL, which runs the CLI command with progress tracking. The user sees a live progress page, then receives a classification breakdown (UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT) with next-step suggestions.

**Why this priority**: This is the core value of the feature. Without this, users must drop to the terminal for test updates, breaking the Chat-first workflow that SPECTRA is designed for. The update command is the maintenance counterpart to generate.

**Independent Test**: Can be tested by installing the SKILL via `spectra init`, opening Copilot Chat, and requesting a test update. Delivers the ability to maintain test suites entirely from Chat.

**Acceptance Scenarios**:

1. **Given** a user has SPECTRA initialized with bundled SKILLs, **When** they ask Copilot Chat "update tests for checkout," **Then** the `spectra-update` SKILL is invoked, showing a progress page and returning classification results.
2. **Given** the update command completes, **When** the SKILL reads `.spectra-result.json`, **Then** it presents total tests analyzed, classification breakdown, tests updated, and tests flagged for review.
3. **Given** a user asks "preview what would change in the login tests," **When** the SKILL runs with `--diff` flag, **Then** proposed changes are shown without modifying files.

---

### User Story 2 - SKILL Bundled with Init and Update-Skills (Priority: P1)

A user runs `spectra init` on a new project. The initialization creates all 10 SKILL files, including the new `spectra-update` SKILL, in `.github/skills/spectra-update/SKILL.md`. When a new version of SPECTRA ships, running `spectra update-skills` updates the SKILL content if the user hasn't customized it.

**Why this priority**: Equal to P1 because without the SKILL file being created and managed, User Story 1 cannot function. This is the delivery mechanism.

**Independent Test**: Run `spectra init` in an empty project directory and verify `.github/skills/spectra-update/SKILL.md` exists with correct frontmatter. Run `spectra update-skills` and verify it updates unmodified SKILLs and skips user-customized ones.

**Acceptance Scenarios**:

1. **Given** a fresh project directory, **When** the user runs `spectra init`, **Then** `.github/skills/spectra-update/SKILL.md` is created with correct frontmatter (name, description, tools list).
2. **Given** the SKILL file has not been modified by the user, **When** `spectra update-skills` runs with newer content, **Then** the file is updated.
3. **Given** the user has customized the SKILL file, **When** `spectra update-skills` runs, **Then** the file is preserved (hash mismatch detected, skip update).

---

### User Story 3 - Agent Delegation Routes Update Requests (Priority: P2)

Both the generation agent and execution agent receive update-related requests from users (e.g., "are there any orphaned tests?" or "refresh tests after the API docs update"). The agents delegate these requests to the `spectra-update` SKILL instead of handling them inline, following the delegation model established in spec 027.

**Why this priority**: Important for correct routing but lower than P1 because the SKILL itself works independently once invoked. Delegation ensures discoverability and correct routing from either agent.

**Independent Test**: Inspect agent content strings for delegation table entries pointing to `spectra-update`. Verify no raw `spectra ai update` CLI blocks exist in agent content (delegation only).

**Acceptance Scenarios**:

1. **Given** the generation agent prompt, **When** a user asks "update my tests," **Then** the agent delegates to the `spectra-update` SKILL.
2. **Given** the execution agent prompt, **When** a user asks "update tests for checkout," **Then** the agent delegates to the `spectra-update` SKILL.
3. **Given** either agent prompt, **When** inspected for raw CLI commands, **Then** no `spectra ai update` CLI blocks exist outside the delegation table.

---

### User Story 4 - Documentation Reflects 10 SKILLs (Priority: P3)

All documentation files (CLAUDE.md, PROJECT-KNOWLEDGE.md, README.md, CHANGELOG.md) accurately reflect the total of 10 bundled SKILLs. The SKILL table lists `spectra-update` in the correct position (second, after `spectra-generate`). Agent descriptions mention update delegation.

**Why this priority**: Documentation accuracy is important for onboarding and maintenance, but has no runtime impact. The feature works without documentation updates.

**Independent Test**: Search documentation files for SKILL count references and verify they say "10." Verify the SKILL table contains the `spectra-update` row.

**Acceptance Scenarios**:

1. **Given** PROJECT-KNOWLEDGE.md, **When** the SKILL section is read, **Then** it says "10 Bundled SKILLs" and the table includes `spectra-update`.
2. **Given** CLAUDE.md, **When** the Recent Changes section is read, **Then** it contains a spec 029 entry mentioning the 10th bundled SKILL.
3. **Given** README.md, **When** the Copilot Chat section is read, **Then** the SKILL count is updated to 10.

---

### Edge Cases

- What happens when the user asks to "update all suites" with no suites present? The SKILL lists suites first; if empty, it should report no suites found.
- What happens when `spectra update-skills` encounters only the new SKILL missing (upgrade from 9 to 10 SKILLs)? It should create the missing SKILL file.
- What happens when the update command fails mid-execution? The `.spectra-result.json` should contain a failure status and the SKILL should present the error without re-running.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST include a `spectra-update` SKILL file with correct frontmatter (name, description, tools, model, disable-model-invocation) following the established SKILL conventions.
- **FR-002**: System MUST embed the SKILL content in `SkillContent.cs` as a string constant so it can be written during `spectra init`.
- **FR-003**: System MUST track the SKILL's SHA-256 hash in `SkillsManifest.cs` to support the `spectra update-skills` customization detection.
- **FR-004**: System MUST add `spectra-update` to the SKILL file creation list in `InitHandler.cs`.
- **FR-005**: The `spectra-update` SKILL MUST use the 5-step progress flow (show preview, runInTerminal, awaitTerminal, readFile result, suggest next steps) consistent with all other long-running SKILLs.
- **FR-006**: The SKILL MUST include `--no-interaction --output-format json --verbosity quiet` flags on all CLI commands.
- **FR-007**: The SKILL MUST include the "do NOTHING between runInTerminal and awaitTerminal" instruction.
- **FR-008**: The SKILL MUST include `browser/openBrowserPage` in its tools list.
- **FR-009**: The SKILL MUST use `**Step N**` format (not `### Tool call N:`) for step numbering.
- **FR-010**: The generation agent MUST contain a delegation entry for `spectra-update` in its delegation table.
- **FR-011**: The execution agent MUST contain a delegation entry for `spectra-update` in its delegation table.
- **FR-012**: Neither agent MUST contain raw `spectra ai update` CLI blocks outside the delegation table.
- **FR-013**: `AgentContent.cs` MUST be updated with the new delegation entries matching the agent prompt files.
- **FR-014**: All documentation files (PROJECT-KNOWLEDGE.md, CLAUDE.md, README.md, CHANGELOG.md) MUST reflect the updated SKILL count of 10.
- **FR-015**: The existing `UpdateResult` model MUST include all fields needed by the SKILL: command, status, success, suite, totalTests, classifications (upToDate, outdated, orphaned, redundant), testsUpdated, testsCreated, testsFlagged, flaggedTests, duration.
- **FR-016**: The existing `UpdateHandler` MUST write `.spectra-progress.html` and `.spectra-result.json` files (verify from spec 025 infrastructure).

### Key Entities

- **spectra-update SKILL**: The 10th bundled SKILL file, wrapping the `spectra ai update` CLI command for Copilot Chat integration. Contains step-by-step instructions for the Chat agent.
- **SkillContent.SpectraUpdate**: Embedded string constant holding the SKILL file content for `spectra init` file creation.
- **SkillsManifest entry**: SHA-256 hash entry enabling `spectra update-skills` to detect user customizations vs. stock content.
- **Agent delegation entries**: Rows in both agent delegation tables routing update requests to the SKILL instead of handling inline.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can trigger test updates entirely from VS Code Copilot Chat without dropping to the terminal, completing the Chat-first lifecycle for test maintenance.
- **SC-002**: `spectra init` creates all 10 SKILL files, including `spectra-update`, in a single invocation.
- **SC-003**: `spectra update-skills` correctly manages the new SKILL (updates unmodified, preserves customized).
- **SC-004**: Both agents (generation and execution) correctly delegate update requests to the SKILL, with no duplicated CLI instructions.
- **SC-005**: All documentation references consistently state 10 bundled SKILLs with no stale counts remaining.
- **SC-006**: All existing tests continue to pass with no regressions (full test suite green).
- **SC-007**: The SKILL follows all established conventions (5-step flow, flags, step format, wait instruction, tools list) verified by automated consistency tests.

## Assumptions

- The `UpdateResult` model and progress infrastructure (`.spectra-result.json`, `.spectra-progress.html`) were implemented in spec 025. This spec verifies they work correctly and adds missing fields if needed.
- The `--suite` flag on `spectra ai update` works the same way as on `spectra ai generate` (flag alternative to positional arg, added for LLM-friendly syntax).
- The SKILL file conventions (frontmatter format, tools list, step format, wait instruction) are stable and match what was established in specs 022, 024, 025, and 027.
- The delegation model from spec 027 is the correct pattern: agents delegate CLI tasks to SKILLs rather than containing inline CLI instructions.

## Dependencies

- **Spec 006** (Conversational Generation): Provides the `spectra ai update` command with classification logic.
- **Spec 025** (Universal SKILL Progress): Provides `ProgressManager`, `ProgressPhases.Update`, `UpdateResult`, and the progress/result file pipeline.
- **Spec 027** (SKILL/Agent Deduplication): Establishes the delegation model and SKILL consistency conventions.

## Scope Boundaries

**In scope**: New SKILL file, bundled content, manifest entry, init handler entry, agent delegation updates, documentation sync, tests.

**Out of scope**: Changes to update command classification logic, new CLI flags, MCP tools for test updates, interactive mode in the SKILL, batch update across all suites in a single command.
