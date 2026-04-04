# Feature Specification: CLI Non-Interactive Mode and Structured Output

**Feature Branch**: `020-cli-non-interactive`  
**Created**: 2026-04-04  
**Status**: Draft  
**Input**: User description: "Refactor SPECTRA CLI for non-interactive mode and structured output"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SKILL-Based Automated CLI Invocation (Priority: P1)

A Copilot Chat SKILL file invokes SPECTRA CLI commands in the terminal. The SKILL passes all required arguments and expects structured JSON output it can parse. No interactive prompts appear because the SKILL cannot respond to them.

**Why this priority**: This is the core use case that unlocks all SKILL-based workflows. Without non-interactive mode, Copilot Chat integration is impossible.

**Independent Test**: Can be fully tested by running any CLI command with all required args and `--output-format json` and verifying JSON output with no prompts.

**Acceptance Scenarios**:

1. **Given** all required arguments are provided, **When** a CLI command executes, **Then** it completes without any interactive prompts
2. **Given** `--output-format json` is specified, **When** a command completes successfully, **Then** stdout contains valid parseable JSON matching the defined schema
3. **Given** `--output-format json` is specified, **When** a command completes, **Then** no colors, spinners, or progress indicators appear in output

---

### User Story 2 - CI Pipeline Integration (Priority: P2)

A CI pipeline runs SPECTRA commands with `--no-interaction` and `--output-format json` to get structured results. If required arguments are missing, the command fails immediately with exit code 3 and a clear error listing the missing args.

**Why this priority**: CI integration is the second most common non-human usage. Reliable exit codes and structured output enable pipeline decisions.

**Independent Test**: Can be tested by running a command with `--no-interaction` but missing a required arg, verifying exit code 3 and error message listing missing arguments.

**Acceptance Scenarios**:

1. **Given** `--no-interaction` flag is set and required args are missing, **When** command is invoked, **Then** it exits with code 3 and outputs an error listing missing arguments
2. **Given** `--no-interaction` and all args provided, **When** command completes, **Then** exit code 0 indicates success
3. **Given** a CI pipeline parses JSON output, **When** validation errors are found, **Then** exit code 2 is returned with structured error details

---

### User Story 3 - Human Interactive Usage Preserved (Priority: P2)

A developer runs SPECTRA CLI without specifying all arguments. The familiar interactive prompts (suite selection, confirmations) appear exactly as before. Default behavior is unchanged.

**Why this priority**: Backward compatibility ensures existing human workflows are not disrupted.

**Independent Test**: Can be tested by running a command without required args and without `--no-interaction`, verifying interactive prompts appear.

**Acceptance Scenarios**:

1. **Given** a user runs a command without all required args and without `--no-interaction`, **When** the CLI starts, **Then** interactive prompts guide the user
2. **Given** no `--output-format` flag is provided, **When** a command completes, **Then** output uses the current human-friendly format with colors and tables

---

### User Story 4 - Verbosity Control (Priority: P3)

A user or automation tool controls the amount of output produced by CLI commands. SKILL files use `quiet` for minimal output, CI uses `normal`, developers debugging use `verbose`.

**Why this priority**: Enables tailored output for different contexts, but non-interactive and JSON output are more critical.

**Independent Test**: Can be tested by running any command with `--verbosity quiet` and verifying only the final result appears.

**Acceptance Scenarios**:

1. **Given** `--verbosity quiet` is specified, **When** a command runs, **Then** only the final result is output (no progress indicators)
2. **Given** `--verbosity verbose` is specified, **When** a command runs, **Then** step-by-step internal logging appears
3. **Given** no `--verbosity` flag, **When** a command runs, **Then** behavior matches current default (normal progress indicators)

---

### Edge Cases

- What happens when `--output-format json` is combined with `--verbosity verbose`? JSON output must remain valid; verbose logs go to stderr.
- What happens when a command fails mid-execution with `--output-format json`? A JSON error object is output with the error details and appropriate exit code.
- What happens when `--no-interaction` is set but the command has no required args? Command executes normally.
- What happens when stdout is redirected/piped? Non-interactive behavior should auto-activate (already partially implemented).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST execute all CLI commands without interactive prompts when all required arguments are provided
- **FR-002**: System MUST provide a `--output-format` flag accepting `human` (default) and `json` values on all commands
- **FR-003**: System MUST produce valid, parseable JSON on stdout when `--output-format json` is specified, with no ANSI colors, spinners, or progress indicators mixed in
- **FR-004**: System MUST provide a `--no-interaction` flag on all commands that fails with exit code 3 and lists missing arguments when required args are absent
- **FR-005**: System MUST use standardized exit codes: 0 (success), 1 (command failed), 2 (validation errors found), 3 (missing required arguments in non-interactive mode)
- **FR-006**: System MUST provide a `--verbosity` flag accepting `quiet`, `normal` (default), and `verbose` values on all commands
- **FR-007**: When `--verbosity quiet` is set, system MUST output only the final result (JSON or one-line summary)
- **FR-008**: When `--verbosity verbose` is set, system MUST output step-by-step internal operation logging
- **FR-009**: System MUST preserve current interactive behavior as the default when arguments are incomplete and `--no-interaction` is not set
- **FR-010**: Each command's JSON output MUST follow the defined schema specific to that command (generate, analyze, validate, dashboard, list, show, init)
- **FR-011**: When `--output-format json` is used and a command fails, system MUST output a JSON error object with error details rather than unstructured text
- **FR-012**: Verbose logging MUST be sent to stderr when `--output-format json` is active, so stdout remains valid JSON

### Key Entities

- **Command Result**: The structured output of any CLI command, containing command name, status, timestamp, and command-specific data
- **Output Format**: The rendering mode for command output (human-readable or JSON)
- **Verbosity Level**: Controls the amount of operational detail shown (quiet, normal, verbose)
- **Exit Code**: Numeric code communicating command outcome to calling processes

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All CLI commands (generate, analyze, validate, dashboard, list, show, init, docs index, config) complete without prompts when all required arguments are provided
- **SC-002**: 100% of CLI commands support `--output-format json` producing valid JSON that can be parsed by standard tools
- **SC-003**: Exit code 3 is returned within 1 second when `--no-interaction` is set with missing arguments
- **SC-004**: Existing interactive workflows continue to function identically when no new flags are provided
- **SC-005**: All defined JSON schemas are validated against actual command output with zero mismatches
- **SC-006**: Commands with `--verbosity quiet --output-format json` produce only a single JSON object on stdout with no extraneous output

## Assumptions

- The `--no-interaction` flag already exists on the generate command; this feature extends it to all commands with consistent behavior
- The `--format` flag on the analyze command will be unified under the new `--output-format` global flag
- SKILL files will always pass complete arguments, so the interactive fallback is only for direct human usage
- Verbose logging to stderr when JSON output is active follows standard Unix conventions for separating data from diagnostics
