namespace Spectra.CLI.Skills;

/// <summary>
/// Bundled SKILL file contents for spectra init.
/// </summary>
public static class SkillContent
{
    // Only include tools the SKILLs actually need — restricting the list prevents
    // GPT-4o from using edit/createFile to manually create test files or MCP tools.
    private const string GenerateToolsList = "execute/runInTerminal, execute/awaitTerminal, execute/getTerminalOutput, read/readFile, read/terminalLastCommand, search/listDirectory";
    private const string ReadOnlyToolsList = "execute/runInTerminal, execute/awaitTerminal, execute/getTerminalOutput, read/readFile, read/terminalLastCommand, read/problems, search/listDirectory, search/textSearch";

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
        tools: [{{GenerateToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Test Generation

        You generate test cases by running CLI commands. Follow the exact tool sequence below.

        **IMPORTANT: When showing progress, ONLY output the `message` field — one short line, nothing else. If the message is the same as last time, say nothing — just poll again silently.**

        ## When user asks to generate test cases:

        ### Tool call 1: runInTerminal
        ```
        spectra ai generate --suite {suite} --analyze-only --output-format json --verbosity quiet
        ```

        ### Tool call 2: awaitTerminal

        ### Tool call 3: readFile `.spectra-result.json`

        **Check `status`:**
        - `"analyzing"` → output ONLY: the `message` field — then `awaitTerminal` + `readFile` again.
        - `"failed"` → tell user the `error`.
        - `"analyzed"` → "I found **{analysis.recommended}** testable behaviors. Generate **{analysis.recommended}** test cases for **{suite}**?"

        STOP. Wait for user.

        ---

        ## After user approves:

        ### Tool call 4: runInTerminal
        ```
        spectra ai generate --suite {suite} --count {count} --output-format json --verbosity quiet
        ```

        ### Tool call 5: awaitTerminal

        ### Tool call 6: readFile `.spectra-result.json`

        **Check `status`:**
        - `"generating"` → output ONLY: the `message` field — then `awaitTerminal` + `readFile` again. Keep going until done.
        - `"failed"` → tell user the `error`.
        - `"completed"` → "Generated **{generation.tests_written}** test cases." + list `files_created`.
        """;

    public const string Coverage = $$"""
        ---
        name: SPECTRA Coverage
        description: Analyzes test coverage across documentation, requirements, and automation.
        tools: [{{ReadOnlyToolsList}}]
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
        tools: [{{ReadOnlyToolsList}}]
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
        tools: [{{ReadOnlyToolsList}}]
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
        tools: [{{ReadOnlyToolsList}}]
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
        tools: [{{ReadOnlyToolsList}}]
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
