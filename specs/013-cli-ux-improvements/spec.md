# Feature Specification: CLI UX Improvements

**Feature Branch**: `013-cli-ux-improvements`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "Several UX improvements for the SPECTRA CLI that make the tool more intuitive and reduce friction: init automation dirs, init critic config, interactive suite continuation, next-step hints."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Next-Step Hints After Commands (Priority: P1)

After running a CLI command, the user sees 2-3 contextual suggestions of what to do next, printed in dimmed/gray text below the primary output. This reduces the learning curve and guides new users through the natural workflow without requiring documentation.

**Why this priority**: Affects every command in the CLI and has the broadest user impact. Most users don't know the full command set or the recommended workflow order. Hints are low-friction to implement and immediately improve discoverability.

**Independent Test**: Run any supported command (init, generate, analyze, dashboard, validate) and verify that relevant, context-aware next-step suggestions appear after the primary output in dimmed text. Verify `--quiet` suppresses hints.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init` successfully, **When** the output completes, **Then** 2-3 next-step suggestions are printed in dimmed text, including `spectra ai generate` and `spectra init-profile`.
2. **Given** a user runs `spectra ai generate --suite authentication`, **When** generation completes, **Then** hints include `spectra ai analyze --coverage` and `spectra ai generate` (interactive mode).
3. **Given** a user runs `spectra ai analyze --coverage`, **When** analysis completes, **Then** hints include `spectra dashboard --output ./site` and `spectra ai analyze --coverage --auto-link` (if auto-link not already used).
4. **Given** a user runs `spectra dashboard --output ./site`, **When** generation completes, **Then** hints include opening the HTML file and a link to the deployment guide.
5. **Given** a user runs `spectra validate` with errors, **When** validation fails, **Then** hints suggest fixing errors and re-running `spectra validate`.
6. **Given** a user runs `spectra validate` with no errors, **When** validation passes, **Then** hints suggest `spectra ai generate` or `spectra index`.
7. **Given** the `--quiet` flag is used with any command, **When** the command completes, **Then** no hints are displayed.
8. **Given** a CI environment (non-interactive terminal or `--no-interaction` flag), **When** any command completes, **Then** no hints are displayed.

---

### User Story 2 - Init: Configure Automation Directories (Priority: P2)

During `spectra init`, after the existing setup steps, the user is prompted to configure automation code directories for coverage analysis. This tells the scanner where to find automated test code that references manual test IDs. Users can also manage these directories after init with dedicated config subcommands.

**Why this priority**: Coverage analysis accuracy depends on knowing where automation code lives. Currently users must manually edit config, and most don't discover the `automation_dirs` setting. Prompting during init ensures correct setup from day one.

**Independent Test**: Run `spectra init` in an empty directory, respond to the automation directories prompt, and verify the paths appear in `spectra.config.json` under `coverage.automation_dirs`. Then run `spectra config add-automation-dir` and verify the config updates.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init` interactively, **When** the init flow reaches the automation config step, **Then** a prompt asks for automation code directories with examples and a skip option.
2. **Given** the user enters `../test_automation, src/IntegrationTests` at the prompt, **When** init completes, **Then** `coverage.automation_dirs` in spectra.config.json contains `["../test_automation", "src/IntegrationTests"]`.
3. **Given** the user presses Enter without typing anything, **When** init continues, **Then** the default automation directories are used (existing defaults from CoverageConfig).
4. **Given** a user runs `spectra config add-automation-dir ../new-tests`, **When** the command completes, **Then** `../new-tests` is appended to `coverage.automation_dirs` in spectra.config.json without duplicating existing entries.
5. **Given** a user runs `spectra config remove-automation-dir ../old-tests`, **When** the command completes, **Then** `../old-tests` is removed from `coverage.automation_dirs`. If it wasn't present, a warning is shown.
6. **Given** a user runs `spectra config list-automation-dirs`, **When** the command completes, **Then** all configured automation directories are listed, one per line, with an indication of which ones exist on disk.
7. **Given** `spectra init` is run with `--no-interaction`, **When** the init flow runs, **Then** the automation directories prompt is skipped and defaults are used.

---

### User Story 3 - Init: Configure Critic Model (Priority: P2)

During `spectra init`, after the primary AI provider is configured, the user is asked whether to enable grounding verification (the critic). If yes, they select a critic provider and configure the API key. This makes the critic discoverable — currently users must manually edit the `ai.critic` config section to enable it.

**Why this priority**: Same level as automation dirs — improves init flow completeness. Grounding verification is a key differentiator for SPECTRA but most users never enable it because they don't know it exists.

**Independent Test**: Run `spectra init` interactively, select "Yes" for grounding verification, pick a provider, and verify `ai.critic` is properly populated in spectra.config.json with `enabled: true`.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init` interactively and has configured a primary AI provider, **When** the provider step completes, **Then** a prompt asks whether to enable grounding verification with a brief explanation of what it does.
2. **Given** the user selects "Yes", **When** the critic setup begins, **Then** a list of critic providers is shown (google, anthropic, openai, same as primary) with recommendations.
3. **Given** the user selects "google" as critic provider, **When** the critic setup continues, **Then** the user is prompted for the API key environment variable name (defaulting to `GOOGLE_API_KEY`).
4. **Given** the user completes critic setup, **When** init finishes, **Then** `ai.critic` in spectra.config.json has `enabled: true`, the selected provider, default model for that provider, and the API key env var.
5. **Given** the user selects "No" for grounding verification, **When** init continues, **Then** `ai.critic` is either absent or has `enabled: false`.
6. **Given** the user selects "Same as primary provider", **When** the critic setup continues, **Then** the critic is configured with the same provider and model as the primary, and no additional API key is requested.
7. **Given** `spectra init` is run with `--no-interaction`, **When** the init flow runs, **Then** the critic prompt is skipped and critic remains disabled (default).

---

### User Story 4 - Interactive Mode: Continue to Other Suites (Priority: P3)

After completing test generation for a suite in interactive mode, instead of exiting, the user is offered options to continue working: generate more tests for the same suite, switch to a different suite, create a new suite, or exit. This eliminates the need to restart the CLI for each suite.

**Why this priority**: Improves power user workflow efficiency but is lower impact than the other improvements since most users generate for one suite at a time. Still valuable for comprehensive test generation sessions.

**Independent Test**: Run `spectra ai generate` in interactive mode, complete generation for one suite, verify the continuation prompt appears with all four options, select "Switch to a different suite", and verify the flow continues without restarting.

**Acceptance Scenarios**:

1. **Given** interactive generation completes for a suite, **When** tests are written, **Then** a menu appears with options: (1) Generate more for this suite, (2) Switch to a different suite, (3) Create a new suite, (4) Done — exit.
2. **Given** gaps were identified during generation, **When** the continuation menu appears, **Then** uncovered gaps are listed as context before the menu (e.g., "Gaps still uncovered: MFA recovery flows, SSO edge cases").
3. **Given** the user selects "Generate more for this suite", **When** the flow continues, **Then** the interactive generation loop restarts for the same suite, focusing on the remaining gaps.
4. **Given** the user selects "Switch to a different suite", **When** the suite list appears, **Then** existing suites are shown with test counts, and the user can pick one to generate tests for.
5. **Given** the user selects "Create a new suite", **When** the prompt appears, **Then** the user enters a suite name, the directory is created, and the interactive generation flow begins for the new suite.
6. **Given** the user selects "Done — exit", **When** the command exits, **Then** the exit code is 0 and a summary of all suites worked on in the session is printed.
7. **Given** the `--no-interaction` flag is used, **When** generation completes, **Then** the continuation menu is not shown and the command exits after the first suite.

---

### Edge Cases

- What happens when the user enters invalid directory paths during automation dir setup? The system accepts the paths as-is (they are configuration, not validated at init time) but `list-automation-dirs` shows which paths don't exist on disk.
- What happens when the user tries to add a duplicate automation directory? The command detects duplicates and prints a message that the directory is already configured, without adding it again.
- What happens when the user removes the last automation directory? The command allows it, resulting in an empty array. Coverage analysis will warn that no automation directories are configured.
- What happens when the critic API key environment variable doesn't exist at init time? Init accepts the variable name without validation — the key is only needed at generation time. A note is printed reminding the user to set the variable before running generation.
- What happens when hints would suggest a command that doesn't apply (e.g., `--auto-link` when already used)? The hint system checks recent command context and omits irrelevant suggestions.
- What happens when all suites have full coverage during the continue-to-other-suites flow? The "gaps" section is omitted and the menu still offers to switch suites or exit.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display 2-3 contextual next-step hints after command completion, formatted in dimmed/gray text, separated from primary output by a blank line.
- **FR-002**: System MUST suppress hints when `--quiet` flag is used, when `--no-interaction` flag is set, or when the terminal is non-interactive.
- **FR-003**: Hints MUST be context-aware — different suggestions based on the command that ran, its success/failure status, and the current repository state.
- **FR-004**: System MUST prompt for automation code directories during `spectra init` interactive mode, after the existing setup steps.
- **FR-005**: System MUST support `spectra config add-automation-dir <path>` to append a directory to `coverage.automation_dirs`.
- **FR-006**: System MUST support `spectra config remove-automation-dir <path>` to remove a directory from `coverage.automation_dirs`.
- **FR-007**: System MUST support `spectra config list-automation-dirs` to list all configured automation directories with existence status.
- **FR-008**: System MUST prompt for grounding verification (critic) configuration during `spectra init` interactive mode, after the primary AI provider is configured.
- **FR-009**: The critic prompt MUST offer at least three provider options (google, anthropic, openai) plus "same as primary" and "skip/disable".
- **FR-010**: System MUST write a valid `ai.critic` section to spectra.config.json when the user enables the critic during init.
- **FR-011**: After interactive generation completes for a suite, system MUST offer a continuation menu with options to: generate more for the same suite, switch suites, create a new suite, or exit.
- **FR-012**: The continuation menu MUST display any remaining coverage gaps identified during generation, if available.
- **FR-013**: The interactive session MUST support working on multiple suites in sequence without restarting the CLI.
- **FR-014**: System MUST print a session summary when the user exits after working on multiple suites (total tests generated, suites worked on).
- **FR-015**: All new prompts and menus MUST be skipped when `--no-interaction` is set, falling back to defaults or exiting as appropriate.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users complete initial setup (init through first generation) with 50% fewer documentation lookups — hints guide them through the natural workflow.
- **SC-002**: 100% of supported commands display relevant next-step hints in interactive mode (init, generate, analyze, dashboard, validate, docs index).
- **SC-003**: Users can configure automation directories and critic model entirely through the init flow without manually editing spectra.config.json.
- **SC-004**: Interactive generation sessions last 3x longer on average — users work across multiple suites without restarting the CLI.
- **SC-005**: All new interactive prompts are suppressed in CI mode (`--no-interaction` or `--quiet`), maintaining backward compatibility with automated workflows.
- **SC-006**: Hint display adds less than 50ms to command execution time.

## Assumptions

- Hints are printed to stderr or after the primary stdout output, so they don't interfere with piped/scripted usage.
- The "dimmed/gray text" styling uses the existing terminal UX library (Spectre.Console) or ANSI escape codes for muted text.
- The continuation menu in interactive mode reuses the existing Spectre.Console selection prompts used elsewhere in the CLI.
- The critic provider list is hardcoded at build time (google, anthropic, openai, same-as-primary). New providers can be added in future versions.
- The hint content is hardcoded per command, not dynamically generated by AI. This keeps hints fast and deterministic.
- `--quiet` and `--no-interaction` are existing global options that already suppress verbose output. Hints respect these flags.

## Scope Boundaries

**In scope**:
- Next-step hints for: init, generate (direct + interactive), analyze, dashboard, validate, docs index
- Automation directory init prompt and config subcommands (add/remove/list)
- Critic model init prompt
- Interactive mode continuation (suite switching, new suite creation)
- Session summary on exit
- CI/quiet mode suppression

**Out of scope**:
- Customizable hint content (user-defined hints)
- Hint localization/internationalization
- Persistent session state across CLI invocations
- Critic pipeline debugging or fixing (the critic is confirmed to work when configured)
- Changes to non-interactive (direct) generation mode beyond adding hints
- Tab completion for CLI commands
- Command history or recall
