namespace Spectra.CLI.Skills;

/// <summary>
/// Bundled SKILL file contents for spectra init.
/// </summary>
public static class SkillContent
{
    private const string ToolsList = "vscode/getProjectSetupInfo, vscode/installExtension, vscode/memory, vscode/newWorkspace, vscode/resolveMemoryFileUri, vscode/runCommand, vscode/vscodeAPI, vscode/extensions, vscode/askQuestions, execute/runNotebookCell, execute/testFailure, execute/getTerminalOutput, execute/awaitTerminal, execute/killTerminal, execute/createAndRunTask, execute/runInTerminal, execute/runTests, read/getNotebookSummary, read/problems, read/readFile, read/viewImage, read/terminalSelection, read/terminalLastCommand, agent/runSubagent, edit/createDirectory, edit/createFile, edit/createJupyterNotebook, edit/editFiles, edit/editNotebook, edit/rename, search/changes, search/codebase, search/fileSearch, search/listDirectory, search/textSearch, search/usages, web/fetch, web/githubRepo, browser/openBrowserPage, todo";

    public static readonly Dictionary<string, string> All = new()
    {
        ["spectra-generate"] = Generate,
        ["spectra-coverage"] = Coverage,
        ["spectra-dashboard"] = Dashboard,
        ["spectra-validate"] = Validate,
        ["spectra-list"] = List,
        ["spectra-init-profile"] = InitProfile,
    };

    public const string Generate = $$"""
        ---
        name: SPECTRA Generate
        description: Generates test cases from documentation with AI verification and gap analysis.
        tools: [{{ToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        When the user asks to generate, create, or write test cases, follow this two-step flow.

        **CRITICAL RULES:**
        1. Use `runInTerminal` to execute CLI commands. After execution, use `readFile` to read `.spectra/last-result.json` for the result.
        2. Do NOT use MCP tools (like `list_available_suites`, `start_execution_run`, `rebuild_indexes`, etc.) — those are for test execution, not generation.
        3. Do NOT manually create test files, _index.json, or directories. SPECTRA handles all file creation.
        4. New suites are created automatically when they don't exist yet.

        ## Step 1: Analyze (always do this first)

        Run the analysis to find out how many test cases are recommended:

            spectra ai generate --suite {suite} --analyze-only --output-format json --verbosity quiet

        After the command finishes, read the result file:

            .spectra/last-result.json

        Present the analysis to the user:
        - "I found **{analysis.total_behaviors}** testable behaviors in the documentation"
        - "**{analysis.already_covered}** are already covered by existing tests"
        - "I recommend generating **{analysis.recommended}** new test cases"
        - Show breakdown by category if available (e.g., "8 happy path, 5 negative, 4 edge case")
        - Ask: "Would you like me to generate these {analysis.recommended} test cases?"

        **Wait for user approval before proceeding to Step 2.**

        ## Step 2: Generate (after user approves)

        Run the generation with the approved count:

            spectra ai generate --suite {suite} --count {approved_count} --output-format json --verbosity quiet

        After the command finishes, read the result file:

            .spectra/last-result.json

        Present the results:
        - "Generated **{generation.tests_written}** test cases for the {suite} suite"
        - "{generation.tests_rejected_by_critic} tests rejected by verification"
        - Show grounding breakdown if available (grounded/partial/hallucinated)
        - List the files created

        ## From Description (user describes a specific test)

            spectra ai generate --suite {suite} --from-description "{description}" --output-format json --verbosity quiet

        ## Troubleshooting

        - If the command produces no output, read `.spectra/last-result.json` for the result
        - If the result file doesn't exist, retry with `--verbosity normal` to see errors
        - Valid verbosity levels: `quiet`, `normal` (NOT `debug`)

        ### Examples of user requests:
        - "Generate test cases for the checkout suite"
        - "Create negative test cases for authentication"
        - "Generate 10 high-priority tests for payments"
        - "Add a test case for IBAN validation"
        """;

    public const string Coverage = $$"""
        ---
        name: SPECTRA Coverage
        description: Analyzes test coverage across documentation, requirements, and automation.
        tools: [{{ToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        **CRITICAL RULES:**
        1. You MUST use `runInTerminal` to execute CLI commands and `getTerminalOutput` to read the output. Do NOT just display commands.
        2. Do NOT use MCP tools (like `analyze_coverage_gaps`, etc.) for coverage analysis. Use the `spectra ai analyze` CLI command via the terminal.

        When the user asks about coverage, gaps, or what needs testing:

        1. Use `runInTerminal` to execute, then `getTerminalOutput` to read the result:

           spectra ai analyze --coverage --auto-link --output-format json --verbosity quiet

        2. Parse JSON and present:
           - Documentation coverage: X%
           - Requirements coverage: X%
           - Automation coverage: X%
           - Uncovered areas with specific docs/requirements
           - Undocumented tests count

        3. If the user asks about a specific area:
           - Reference the uncovered_areas from the JSON
           - Suggest generating tests for uncovered docs

        4. If the user wants to improve coverage:
           - Suggest: "I can generate tests for {uncovered_doc}. Want me to?"
           - If yes, use the spectra-generate SKILL

        ### Examples:
        - "How's our test coverage?"
        - "What areas don't have tests yet?"
        - "Show me coverage for the authentication module"
        - "Which requirements aren't tested?"
        """;

    public const string Dashboard = $$"""
        ---
        name: SPECTRA Dashboard
        description: Generates the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
        tools: [{{ToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        **CRITICAL RULES:**
        1. You MUST use `runInTerminal` to execute CLI commands and `getTerminalOutput` to read the output. Do NOT just display commands.
        2. Do NOT use MCP tools for dashboard generation. Use the `spectra dashboard` CLI command via the terminal.

        When the user asks to generate, update, or build the dashboard:

        1. Use `runInTerminal` to execute, then `getTerminalOutput` to read the result:

           spectra dashboard --output ./site --output-format json --verbosity quiet

        2. Parse JSON and confirm:
           - "Dashboard generated at ./site/index.html"
           - Include: X suites, Y tests, Z runs

        3. If the project has Cloudflare Pages configured:
           - Mention: "Push to main to auto-deploy, or open ./site/index.html locally"

        ### Examples:
        - "Generate the dashboard"
        - "Update the dashboard with latest results"
        - "Build the site"
        """;

    public const string Validate = $$"""
        ---
        name: SPECTRA Validate
        description: Validates all test case files for correct format, unique IDs, and required fields.
        tools: [{{ToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        **CRITICAL RULES:**
        1. You MUST use `runInTerminal` to execute CLI commands and `getTerminalOutput` to read the output. Do NOT just display commands.
        2. Do NOT use MCP tools (like `validate_tests`) for validation. Use the `spectra validate` CLI command via the terminal.

        When the user asks to validate, check, or verify test files:

        1. Use `runInTerminal` to execute, then `getTerminalOutput` to read the result:

           spectra validate --output-format json --verbosity quiet

        2. Parse JSON and present:
           - If all valid: "All {total} tests are valid"
           - If errors: list each error with file, line, and message

        3. If errors found, suggest fixes:
           - Missing field: "Add {field} to the frontmatter in {file}"
           - Duplicate ID: "Change the ID in {file} — {id} is already used in {other_file}"

        ### Examples:
        - "Validate all test cases"
        - "Are there any formatting errors?"
        - "Check if everything is valid before I push"
        """;

    public const string List = $$"""
        ---
        name: SPECTRA List
        description: Lists test suites, shows test case details, and browses the test repository.
        tools: [{{ToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        **CRITICAL RULES:**
        1. You MUST use `runInTerminal` to execute CLI commands and `getTerminalOutput` to read the output. Do NOT just display commands.
        2. Do NOT use MCP tools (like `list_available_suites`, `find_test_cases`) for listing. Use the `spectra list` CLI command via the terminal.

        When the user asks to list, show, browse, or find test cases:

        1. To list suites, use `runInTerminal` then `getTerminalOutput`:

           spectra list --output-format json --verbosity quiet

           Present: suite names with test counts

        2. To show a specific test, use `runInTerminal` then `getTerminalOutput`:

           spectra show {test-id} --output-format json --verbosity quiet

           Present: full test case details (title, steps, expected results, metadata)

        ### Examples:
        - "List all test suites"
        - "Show me TC-101"
        - "What tests do we have for checkout?"
        - "How many tests are in the authentication suite?"
        """;

    public const string InitProfile = $$"""
        ---
        name: SPECTRA Profile
        description: Creates or updates the generation profile that controls how AI generates test cases.
        tools: [{{ToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        **CRITICAL RULES:**
        1. You MUST use `runInTerminal` to execute CLI commands and `getTerminalOutput` to read the output. Do NOT just display commands.
        2. Do NOT use MCP tools for profile configuration. Use the `spectra init-profile` CLI command via the terminal.

        When the user asks to configure, set up, or change generation preferences:

        1. Ask what they want to configure:
           - Detail level (high-level / detailed / very detailed)
           - Negative scenario focus (minimum count per feature)
           - Domain-specific needs (payments, auth, GDPR, etc.)
           - Default priority
           - Formatting preferences

        2. Use `runInTerminal` to execute, then `getTerminalOutput` to read the result:

           spectra init-profile --output-format json --verbosity quiet --no-interaction

        3. Confirm the profile was created/updated

        ### Examples:
        - "Set up a generation profile"
        - "I want more detailed test steps"
        - "Configure SPECTRA for payment domain testing"
        """;
}
