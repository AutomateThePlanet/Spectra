# Feature Specification: Bundled SKILLs and Agent Prompts

**Feature Branch**: `022-bundled-skills`  
**Created**: 2026-04-04  
**Status**: Draft  
**Input**: User description: "Create bundled SKILL files and agent prompts for SPECTRA"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Init Creates SKILL and Agent Files (Priority: P1)

A new user runs `spectra init` and receives a complete set of SKILL files and agent prompts alongside the standard configuration. These files enable Copilot Chat to invoke SPECTRA commands through natural language.

**Why this priority**: Without SKILL files, users must learn CLI flags manually. SKILLs are the primary Copilot Chat integration point.

**Independent Test**: Run `spectra init` in a fresh directory and verify all 6 SKILL files and 2 agent files are created in the correct paths.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init`, **When** initialization completes, **Then** 6 SKILL files are created in `.github/skills/` and 2 agent files in `.github/agents/`
2. **Given** the SKILL files exist, **When** a user opens Copilot Chat, **Then** the SKILLs appear as available commands
3. **Given** `spectra init --skip-skills` is used, **When** initialization completes, **Then** only core files are created (config, directories, templates) without SKILL or agent files

---

### User Story 2 - SKILLs Invoke CLI with Structured Output (Priority: P1)

Each SKILL file contains instructions for Copilot Chat to run the correct CLI command with `--output-format json --verbosity quiet`, parse the JSON result, and present it conversationally.

**Why this priority**: SKILLs that can't parse CLI output are useless. The JSON output contract from the non-interactive CLI spec makes this possible.

**Independent Test**: Read any SKILL file and verify it contains the correct CLI command with JSON output flags, parsing instructions, and example user requests.

**Acceptance Scenarios**:

1. **Given** the spectra-generate SKILL exists, **When** inspected, **Then** it contains the command `spectra ai generate --suite {suite} --output-format json --verbosity quiet`
2. **Given** the spectra-coverage SKILL exists, **When** inspected, **Then** it instructs Copilot to parse documentation, requirements, and automation percentages from JSON
3. **Given** any SKILL file, **When** inspected, **Then** it includes example user requests that would trigger it

---

### User Story 3 - Update SKILLs on CLI Upgrade (Priority: P2)

When SPECTRA CLI is updated, SKILL files may need updating. The `spectra update-skills` command safely updates bundled files while preserving user customizations.

**Why this priority**: Users should not lose customizations when upgrading, but should get new SKILL capabilities.

**Independent Test**: Modify a SKILL file, run update-skills, and verify the modified file is skipped while unmodified files are updated.

**Acceptance Scenarios**:

1. **Given** SKILL files were not modified by the user, **When** `spectra update-skills` runs, **Then** files are overwritten with the latest version
2. **Given** a SKILL file was modified by the user, **When** `spectra update-skills` runs, **Then** the modified file is skipped with a warning
3. **Given** the update command runs, **When** it completes, **Then** a summary shows which files were updated, unchanged, and skipped

---

### User Story 4 - Agent Prompts for Execution and Generation (Priority: P2)

Two agent prompt files configure specialized Copilot agents: one for test execution (MCP-based) and one for test generation (CLI-based via terminal).

**Why this priority**: Agent prompts enable focused workflows beyond ad-hoc SKILL invocations.

**Independent Test**: Read agent files and verify they contain correct frontmatter (name, description, tools, model) and appropriate body instructions.

**Acceptance Scenarios**:

1. **Given** the execution agent file exists, **When** inspected, **Then** its frontmatter specifies MCP tools and its body describes the test execution workflow
2. **Given** the generation agent file exists, **When** inspected, **Then** its frontmatter specifies terminal tool and its body describes the generation session flow

---

### Edge Cases

- What if `.github/skills/` already exists with custom files? Init should not overwrite existing files unless `--force` is used.
- What if `spectra update-skills` is run before `spectra init`? Should report "No SKILL files found. Run 'spectra init' first."
- What if a SKILL file is deleted by the user? `update-skills` should recreate it (no hash to compare = treat as missing).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `spectra init` MUST create 6 SKILL files in `.github/skills/` subdirectories
- **FR-002**: `spectra init` MUST create 2 agent prompt files in `.github/agents/`
- **FR-003**: Each SKILL file MUST contain CLI commands with `--output-format json --verbosity quiet` flags
- **FR-004**: Each SKILL file MUST include JSON parsing instructions and example user requests
- **FR-005**: `spectra init --skip-skills` MUST skip creation of SKILL and agent files
- **FR-006**: `spectra update-skills` MUST update unmodified SKILL files and skip user-modified ones
- **FR-007**: File modification detection MUST compare file content hash against the bundled version hash
- **FR-008**: `spectra update-skills` MUST report a summary of updated, unchanged, and skipped files
- **FR-009**: The generate SKILL MUST include instructions for `--from-suggestions`, `--from-description`, and `--auto-complete` flags
- **FR-010**: Agent prompt frontmatter MUST include name, description, tools list, and model specification
- **FR-011**: Init MUST NOT overwrite existing SKILL or agent files unless `--force` is used
- **FR-012**: The init JSON output MUST list all created SKILL and agent file paths

### Key Entities

- **SKILL File**: A Markdown file in `.github/skills/{name}/SKILL.md` that instructs Copilot Chat how to invoke a specific CLI command
- **Agent Prompt**: A Markdown file with YAML frontmatter in `.github/agents/` that configures a specialized Copilot agent
- **File Hash**: A content hash used to detect whether a bundled file has been modified by the user

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `spectra init` creates all 8 files (6 SKILLs + 2 agents) in under 2 seconds
- **SC-002**: Every SKILL file references at least one CLI command with `--output-format json` flag
- **SC-003**: `spectra update-skills` correctly identifies user-modified files with zero false positives
- **SC-004**: Users can invoke any SKILL via Copilot Chat and receive structured results within the same conversation
- **SC-005**: `spectra init --skip-skills` produces zero SKILL or agent files while still creating all core files

## Assumptions

- SKILL files are static Markdown — no code execution, no templates with variables at runtime
- The SKILL file format follows GitHub Copilot's SKILL.md convention with YAML frontmatter
- File hashes are computed from file content (not metadata like timestamps)
- The `spectra update-skills` command uses the same bundled file content that `spectra init` uses
- Agent prompt files follow the `.agent.md` naming convention expected by GitHub Copilot
