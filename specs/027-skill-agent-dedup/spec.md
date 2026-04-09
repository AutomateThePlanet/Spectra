# Feature Specification: SKILL/Agent Prompt Deduplication

**Feature Branch**: `027-skill-agent-dedup`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: User description: "Deduplicate CLI command blocks across agent prompts and SKILL files by making agents delegate to SKILLs instead of duplicating instructions"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Agent Delegates CLI Tasks to SKILLs (Priority: P1)

A QA engineer using the execution agent in Copilot Chat asks "generate the dashboard." Instead of the agent containing its own 18-line dashboard CLI block (which GPT-4o frequently ignores due to prompt length), the agent follows a single delegation instruction pointing to the `spectra-dashboard` SKILL. The SKILL provides the authoritative step-by-step instructions.

**Why this priority**: This is the core change — eliminating the 324 lines of duplicated content across 3 locations per command. Without this, the execution agent remains too long for GPT-4o to reliably follow, and updates require editing 3 files.

**Independent Test**: Can be tested by verifying that the refactored execution agent no longer contains full CLI command blocks for dashboard, coverage, criteria, validate, list, or docs index — only delegation references to the corresponding SKILL files.

**Acceptance Scenarios**:

1. **Given** the execution agent prompt, **When** a user asks for dashboard generation, **Then** the agent delegates to the `spectra-dashboard` SKILL instead of containing its own CLI instructions.
2. **Given** the execution agent prompt, **When** a user asks for coverage analysis, **Then** the agent delegates to the `spectra-coverage` SKILL.
3. **Given** the execution agent prompt, **When** a user asks for any of the 6 delegated tasks (dashboard, coverage, criteria, validate, list, docs index), **Then** the agent contains only a delegation line — not the full CLI command sequence.
4. **Given** the generation agent prompt, **When** a user asks for any of the 6 delegated tasks, **Then** the agent delegates to the corresponding SKILL.
5. **Given** a delegation table in either agent, **When** comparing the SKILL name referenced with the actual SKILL file, **Then** the SKILL name matches exactly.

---

### User Story 2 - SKILL Files Are Consistent (Priority: P2)

A developer updating the bundled SKILL files notices that all SKILLs follow the same conventions: "Step N" format for numbered steps, `readFile .spectra-result.json` for reading command output, and "do NOTHING between runInTerminal and awaitTerminal" for long-running commands.

**Why this priority**: Consistency across SKILLs is required for the delegation model to work reliably. If SKILLs use different patterns (e.g., `terminalLastCommand` vs `readFile`), the model's behavior becomes unpredictable.

**Independent Test**: Can be tested by inspecting all 8 SKILL files for consistent use of step format, result reading pattern, and wait instruction.

**Acceptance Scenarios**:

1. **Given** the `spectra-list` SKILL, **When** inspecting the result-reading step, **Then** it uses `readFile .spectra-result.json` (not `terminalLastCommand`).
2. **Given** the `spectra-init-profile` SKILL, **When** inspecting the result-reading step, **Then** it uses `readFile .spectra-result.json` and the CLI command includes `--output-format json --verbosity quiet`.
3. **Given** any SKILL with a long-running command (coverage, criteria, dashboard), **When** inspecting the steps around `runInTerminal` and `awaitTerminal`, **Then** an explicit "do NOTHING" instruction is present.
4. **Given** all 8 SKILL files, **When** inspecting step numbering format, **Then** all use "Step N" format (not "Tool call N").

---

### User Story 3 - Help SKILL Is Complete (Priority: P2)

A user asks the agent "what can I do?" Both agents delegate to the `spectra-help` SKILL, which serves as the single reference for all available commands. The help SKILL includes entries for acceptance criteria management and documentation indexing (previously missing).

**Why this priority**: With agents no longer containing their own help tables, the help SKILL must be comprehensive or users lose discoverability for those commands.

**Independent Test**: Can be tested by verifying the help SKILL contains entries for all 8 command categories including criteria and docs index.

**Acceptance Scenarios**:

1. **Given** the `spectra-help` SKILL, **When** checking for acceptance criteria entries, **Then** it includes extract, import, list, and filter examples.
2. **Given** the `spectra-help` SKILL, **When** checking for documentation index entries, **Then** it includes index and force-reindex examples.
3. **Given** either agent prompt, **When** a user asks for help, **Then** the agent references `spectra-help` instead of containing its own help table.

---

### User Story 4 - Update-Skills Delivers New Content (Priority: P3)

A user who previously ran `spectra init` runs `spectra update-skills` after upgrading. The command detects that the bundled content has changed (new SHA-256 hashes) and updates files that haven't been user-customized. Files that were customized are skipped with a message.

**Why this priority**: This is the delivery mechanism. Without hash updates, users never receive the refactored agents and fixed SKILLs.

**Independent Test**: Can be tested by running `spectra update-skills` against both unmodified and user-modified SKILL/agent files and verifying the correct update/skip behavior.

**Acceptance Scenarios**:

1. **Given** unchanged SKILL files from a previous version, **When** running `spectra update-skills`, **Then** all changed files are updated to the new content.
2. **Given** a user-customized SKILL file (hash mismatch), **When** running `spectra update-skills`, **Then** the file is skipped with a message suggesting manual review.
3. **Given** the updated `SkillsManifest`, **When** checking SHA-256 hashes, **Then** all hashes correspond to the actual content of the bundled files.

---

### Edge Cases

- What happens when a user has customized only one of the 10 changed files? Only that file is skipped; the other 9 update normally.
- What happens when the execution agent receives an MCP-style request that looks like a CLI task (e.g., "validate my tests using MCP")? The delegation instruction directs to the SKILL, which uses CLI — not MCP tools.
- What happens when a SKILL file referenced in the delegation table doesn't exist on disk? The agent's delegation line is a textual reference; the Copilot Chat runtime resolves SKILLs by filename in `.github/skills/`. If missing, the runtime can't load it — this is the same behavior as today for any missing SKILL.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Execution agent MUST NOT contain full CLI command blocks for dashboard, coverage, criteria, validate, list, or docs index commands.
- **FR-002**: Execution agent MUST contain a delegation table mapping each CLI task to its corresponding SKILL name.
- **FR-003**: Generation agent MUST NOT contain full CLI command blocks for coverage, criteria, dashboard, validate, list, or docs index commands.
- **FR-004**: Generation agent MUST contain a delegation table mapping each CLI task to its corresponding SKILL name.
- **FR-005**: Both agents MUST delegate help requests to the `spectra-help` SKILL instead of containing inline help tables.
- **FR-006**: Execution agent MUST retain all MCP execution workflow content (steps 1–8), test presentation format, result collection, screenshot handling, error handling, smart test selection, and bug logging sections unchanged.
- **FR-007**: Generation agent MUST retain test generation workflow (analyze → approve → generate flow), update tests section, and critical instruction block unchanged.
- **FR-008**: All SKILL files MUST use "Step N" format for numbered steps (not "Tool call N").
- **FR-009**: All SKILL files that produce JSON output MUST use `readFile .spectra-result.json` for result reading (not `terminalLastCommand`).
- **FR-010**: All SKILL files with long-running commands MUST include an explicit "do NOTHING between runInTerminal and awaitTerminal" instruction.
- **FR-011**: The `spectra-help` SKILL MUST include entries for all 8 command categories: generate, coverage, dashboard, validate, list, criteria, docs index, and init-profile.
- **FR-012**: The `spectra-docs` SKILL MUST document the difference between incremental mode and `--force` full rebuild.
- **FR-013**: The `SkillsManifest` MUST contain updated SHA-256 hashes for all changed bundled content files.
- **FR-014**: Execution agent MUST be 200 lines or fewer.
- **FR-015**: Generation agent MUST be 100 lines or fewer.
- **FR-016**: Redundant "NEVER" warnings in the execution agent that existed solely due to prompt length (e.g., "NEVER use MCP tools for dashboard") MUST be removed.
- **FR-017**: Essential behavioral warnings ("NEVER use askQuestion", "NEVER fabricate failure notes") MUST be retained.

### Key Entities

- **Agent Prompt**: A markdown file defining an AI agent's behavior, tools, and instructions. Two exist: execution and generation.
- **SKILL File**: A markdown file containing step-by-step instructions for a single CLI command workflow. Eight exist, one per command category.
- **Delegation Table**: A markdown table in an agent prompt mapping task descriptions to SKILL names.
- **SkillsManifest**: A C# class containing SHA-256 hashes of all bundled SKILL and agent content, used by `spectra update-skills` to detect user modifications.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Total line count across agents and SKILLs is reduced from ~959 lines to ~630 lines or fewer.
- **SC-002**: Duplicated content across agents and SKILLs is reduced from ~324 lines to 20 lines or fewer (delegation tables only).
- **SC-003**: Each CLI command's instructions exist in exactly one location (its SKILL file), not three.
- **SC-004**: All existing tests continue to pass after refactoring (zero regressions).
- **SC-005**: New validation tests confirm agents contain no full CLI command blocks, SKILLs use consistent formatting, and help SKILL covers all command categories.
- **SC-006**: The `spectra update-skills` command correctly delivers updated content to users with unmodified files.

## Assumptions

- GPT-4o's instruction-following reliability improves with shorter prompts. The ~400-line execution agent is demonstrably problematic; ~180 lines should be within the model's reliable attention range.
- Copilot Chat's SKILL resolution mechanism allows agents to reference SKILLs by name and the runtime loads the corresponding file from `.github/skills/`.
- Users who have customized their SKILL/agent files prefer to manually review changes rather than having them overwritten.
- The "do NOTHING between runInTerminal and awaitTerminal" instruction is effective at preventing premature tool calls during long-running CLI operations.
