# Feature Specification: CLI UX Improvements

**Feature Branch**: `013-cli-ux-improvements`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "Several UX improvements for the SPECTRA CLI that make the tool more intuitive and reduce friction: init automation dirs, init critic model, interactive suite continuation, next-step hints"

## Clarifications

### Session 2026-03-21

- Q: What happens to the new init prompts (automation dirs, critic model) when `spectra init` runs in non-interactive mode (`--no-interaction`)? → A: Both prompts are skipped silently, using defaults (no automation dirs, critic disabled). Users can configure them afterward via `spectra config` subcommands or manual config editing.
- Q: What happens when `spectra config add-automation-dir` is called with a path already in the list? → A: Idempotent no-op with a notice ("path already configured").
- Q: Should next-step hints dynamically inspect repo state or use static per-command mappings? → A: Static predefined hints per command + outcome (success/failure). No file I/O at hint time.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Next-Step Hints After Commands (Priority: P1)

After running any Spectra command, the CLI displays 2-3 contextual suggestions for what to do next, printed in dimmed/gray text below the primary output. Hints are statically mapped per command and outcome (success/failure) — they do not perform file I/O or inspect repo state at hint time. Users running Spectra in CI or automated pipelines can suppress hints with a `--quiet` flag.

**Why this priority**: This is the highest-impact, lowest-risk improvement. Every user benefits on every command invocation. It reduces the learning curve for new users and reminds experienced users of capabilities they may forget. It is purely additive output that does not modify any existing interactive flows.

**Independent Test**: Can be fully tested by running any Spectra command (e.g., `spectra init`, `spectra ai generate`, `spectra dashboard`) and verifying that contextual suggestions appear in dimmed text after the primary output. Running with `--quiet` should suppress them.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init` successfully, **When** the command completes, **Then** the CLI prints 2-3 relevant next-step suggestions in dimmed text (e.g., "spectra init-profile", "spectra ai generate")
2. **Given** a user runs `spectra ai generate` and tests are written, **When** generation completes, **Then** hints suggest coverage analysis, generating more tests, or switching to interactive mode
3. **Given** a user runs `spectra ai analyze --coverage`, **When** analysis completes, **Then** hints suggest auto-linking, dashboard generation, or generating tests for uncovered areas
4. **Given** a user runs `spectra dashboard --output ./site`, **When** the dashboard is generated, **Then** hints suggest opening the HTML file and reference deployment documentation
5. **Given** a user runs any command with the `--quiet` flag, **When** the command completes, **Then** no next-step hints are displayed
6. **Given** a user runs `spectra validate` and errors are found, **When** validation fails, **Then** hints suggest fixing errors and re-running validation (different from success hints)
7. **Given** a user runs `spectra ai analyze --coverage --auto-link`, **When** auto-linking completes, **Then** hints suggest regenerating the dashboard to reflect updated coverage

---

### User Story 2 - Init: Configure Automation Directories (Priority: P2)

During the `spectra init` interactive setup, after configuring documentation and test directories, the CLI asks the user to specify automation test code directories for coverage analysis. Users can enter one or more comma-separated paths or press Enter to skip. The configured paths are written to the `coverage.automation_dirs` array in `spectra.config.json`. A standalone `spectra config` subcommand allows users to add, remove, and list automation directories after initialization.

**Why this priority**: Automation directory configuration is critical for accurate coverage analysis, which is a core Spectra value proposition. Currently users must manually edit the config file — most never discover this setting, leading to incomplete coverage reports.

**Independent Test**: Can be tested by running `spectra init` in a new directory and verifying the automation directory prompt appears, or by running `spectra config add-automation-dir ../tests` and confirming the config file is updated.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init` in a new repository, **When** the init wizard reaches the automation directory step, **Then** the CLI prompts for automation code directories with examples and a skip option
2. **Given** a user enters `../test_automation, src/IntegrationTests` at the prompt, **When** the input is confirmed, **Then** `spectra.config.json` contains `coverage.automation_dirs` with both paths
3. **Given** a user presses Enter without typing anything, **When** the prompt is skipped, **Then** no `automation_dirs` are added and init continues normally
4. **Given** a user runs `spectra config add-automation-dir ../new-tests`, **When** the command succeeds, **Then** the path is appended to `coverage.automation_dirs` in `spectra.config.json`
5. **Given** a user runs `spectra config remove-automation-dir ../old-tests`, **When** the path exists in the config, **Then** it is removed from the array
6. **Given** a user runs `spectra config remove-automation-dir ../nonexistent`, **When** the path does not exist in the config, **Then** the CLI shows a warning that the path was not found
7. **Given** a user runs `spectra config list-automation-dirs`, **When** dirs are configured, **Then** the CLI lists all configured automation directories
8. **Given** a user runs `spectra config list-automation-dirs`, **When** no dirs are configured, **Then** the CLI shows a message indicating no automation directories are configured

---

### User Story 3 - Init: Configure Critic Model (Priority: P3)

During `spectra init`, after the user selects their primary AI provider and model, the CLI asks whether they want to enable grounding verification. If yes, the user selects a critic provider from a curated list and provides the API key environment variable name. The configuration is written to the `ai.critic` section of `spectra.config.json`. This story also includes verifying that the existing critic pipeline is actually invoked during test generation and fixing it if not.

**Why this priority**: The critic model is a differentiating feature (dual-model verification) but is currently invisible to users who don't read the config documentation. Surfacing it during init dramatically increases adoption. However, it depends on the generation flow working correctly, making it slightly lower priority than the purely additive features.

**Independent Test**: Can be tested by running `spectra init`, selecting "Yes" for grounding verification, choosing a provider, and verifying that `spectra.config.json` contains a properly populated `ai.critic` section.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init` and completes primary AI provider setup, **When** the wizard proceeds, **Then** it asks whether the user wants to enable grounding verification with a description of what it does
2. **Given** a user selects "Yes" for grounding verification, **When** the critic setup begins, **Then** the CLI presents a list of recommended critic providers with brief descriptions
3. **Given** a user selects a critic provider (e.g., Google Gemini Flash), **When** the provider is chosen, **Then** the CLI asks for the API key environment variable name with a sensible default for that provider
4. **Given** a user completes critic configuration, **When** the config is saved, **Then** `spectra.config.json` contains `ai.critic` with enabled, provider, model, and API key variable
5. **Given** a user selects "No" for grounding verification, **When** the choice is confirmed, **Then** critic configuration is skipped and the critic section is either omitted or set to disabled
6. **Given** a user selects "Same as primary provider", **When** the config is saved, **Then** the critic provider and model match the primary AI provider settings
7. **Given** the critic is enabled in config and a user runs `spectra ai generate`, **When** generation completes, **Then** grounding verdicts appear in the console output confirming the critic pipeline is active

---

### User Story 4 - Interactive Mode: Continue to Other Suites (Priority: P4)

After completing test generation for a suite in interactive mode, instead of exiting, the CLI presents a menu offering to: (1) generate more tests for the same suite to fill remaining coverage gaps, (2) switch to a different existing suite, (3) create a new suite and continue generating, or (4) exit. The session remains active until the user explicitly chooses to exit. When switching suites, the interactive flow restarts from the test type selection step for the new suite.

**Why this priority**: While this is a significant convenience improvement, it primarily benefits power users who generate tests across multiple suites in a single sitting. Most users generate for one suite at a time. The implementation also touches the interactive session state machine, which is more complex to modify safely.

**Independent Test**: Can be tested by running `spectra ai generate` in interactive mode, completing generation for one suite, and verifying the continuation menu appears with all four options functional.

**Acceptance Scenarios**:

1. **Given** a user completes test generation for a suite in interactive mode, **When** tests are written and the index is updated, **Then** the CLI displays a continuation menu with four options (generate more, switch suite, create suite, exit)
2. **Given** the user selects "Generate more tests for [current suite]", **When** the option is chosen, **Then** the session restarts the generation flow for the same suite, showing remaining coverage gaps as context
3. **Given** the user selects "Switch to a different suite", **When** the option is chosen, **Then** the CLI displays a suite selector listing all available suites with their test counts
4. **Given** the user selects a suite from the switch list, **When** the suite is selected, **Then** the interactive flow restarts from the test type selection step for that suite
5. **Given** the user selects "Create a new suite", **When** the option is chosen, **Then** the CLI prompts for a suite name, creates the directory, and continues with the normal interactive generation flow
6. **Given** the user selects "Done — exit", **When** the option is chosen, **Then** the session ends cleanly
7. **Given** the user enters an invalid suite name when creating a new suite, **When** validation fails, **Then** the CLI shows an error and re-prompts

---

### Edge Cases

- What happens if `spectra init` runs with `--no-interaction`? The new automation directory and critic model prompts are skipped silently, using defaults (no automation dirs, critic disabled).
- What happens if `--quiet` is combined with interactive mode? Hints are suppressed; interactive prompts remain unaffected.
- What happens if `spectra config add-automation-dir` is run before `spectra init`? The command fails with a clear message that `spectra.config.json` does not exist.
- What happens if the user configures a critic provider during init but the API key environment variable is not set at runtime? The init wizard warns that the variable is not currently set but still saves the configuration (the key may be set later).
- What happens if all suites have been fully covered when the continuation menu appears? The "switch to a different suite" option remains available (users may want to regenerate or add tests).
- What happens if the user creates a new suite with a name that already exists? The CLI warns and offers to generate tests for the existing suite instead.
- What happens if `spectra.config.json` has no `coverage` section when running `spectra config list-automation-dirs`? The CLI displays "No automation directories configured" rather than erroring.
- What happens if `spectra config add-automation-dir` is called with a path already in the list? The command is idempotent: it does nothing and prints a notice that the path is already configured.
- What happens if the terminal does not support ANSI color codes for dimmed text? Hints are printed as plain text without styling.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display 2-3 contextual next-step suggestions after each command completes, using dimmed/gray text styling
- **FR-002**: System MUST vary hint content based on the command executed and its outcome (success/failure), using static predefined mappings with no file I/O at hint time
- **FR-003**: System MUST support a `--quiet` flag on all commands to suppress next-step hints for CI/automation use
- **FR-004**: During `spectra init`, system MUST prompt users to configure automation code directories for coverage analysis, with examples and a skip option
- **FR-005**: System MUST provide `spectra config add-automation-dir`, `spectra config remove-automation-dir`, and `spectra config list-automation-dirs` subcommands
- **FR-006**: The `config` subcommands MUST read from and write to `spectra.config.json` in the current directory
- **FR-007**: During `spectra init`, after primary AI provider configuration, system MUST ask whether to enable grounding verification with an explanation of its purpose
- **FR-008**: System MUST present a curated list of critic providers with brief descriptions when the user opts to enable grounding verification
- **FR-009**: System MUST write critic configuration to the `ai.critic` section of `spectra.config.json`
- **FR-010**: After completing test generation in interactive mode, system MUST display a continuation menu with options to: generate more for the same suite, switch suite, create new suite, or exit
- **FR-011**: System MUST keep the interactive session alive across suite switches until the user explicitly selects exit
- **FR-012**: System MUST support creating a new suite directory from the continuation menu and seamlessly continuing the generation flow

### Key Entities

- **NextStepHint**: A suggestion displayed after command completion, consisting of a command string and a brief description. Associated with a command context (which command ran, success/failure state).
- **AutomationDirectory**: A file system path pointing to automation test code, stored in the project configuration's coverage section.
- **CriticProviderOption**: A selectable provider for grounding verification during init, consisting of a provider name, default model, and default API key environment variable name.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of supported commands display contextually appropriate next-step hints when `--quiet` is not specified
- **SC-002**: Users can complete automation directory configuration during init in under 30 seconds
- **SC-003**: Users can configure the critic model during init without referring to external documentation
- **SC-004**: Users generating tests interactively can switch between suites without restarting the CLI
- **SC-005**: All next-step hints are suppressed when `--quiet` flag is provided, producing clean output suitable for CI pipelines
- **SC-006**: The `spectra config` subcommands correctly modify the configuration file without corrupting existing settings
- **SC-007**: Grounding verification is confirmed functional during test generation when the critic is enabled

## Assumptions

- The `--quiet` flag follows existing Spectra CLI flag conventions and can be applied globally to any command.
- Dimmed/gray text is rendered using the existing Spectre.Console terminal UX library.
- The continuation menu in interactive mode uses the same selection prompt pattern as existing interactive components (suite selector, test type selector).
- Default critic provider suggestions: Google (Gemini Flash, fast and economical), Anthropic (Claude Haiku), OpenAI (GPT-4o-mini), plus a "same as primary" option.
- The `spectra config` command is a new top-level command group with no existing conflicts.
- Automation directory paths are stored as provided (not normalized to absolute paths) to support portable configurations.
