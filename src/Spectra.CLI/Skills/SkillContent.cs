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
        ["spectra-help"] = Help,
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

        **ALWAYS follow the full analyze → approve → generate flow, even if the user says "generate more tests" or "add more". Never skip the analysis step.**

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
        - `"analyzed"` → respond with EXACTLY this format (fill in values from JSON):

        **{analysis.already_covered}** tests already exist. I recommend generating **{analysis.recommended}** new test cases:

        - Happy Path: {breakdown.HappyPath}
        - Negative: {breakdown.Negative}
        - Edge Case: {breakdown.EdgeCase}
        - Security: {breakdown.Security}
        - Performance: {breakdown.Performance}

        Shall I proceed?

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
        - `"generating"` → output ONLY the `message` field, then `awaitTerminal` + `readFile` again. Keep going until done.
        - `"failed"` → tell user the `error`.
        - `"completed"` → "Generated **{generation.tests_written}** test cases." If `message` exists, show it. List `files_created`. If tests_written < tests_requested, say "Run again to generate more."
        """;

    public const string Coverage = $$"""
        ---
        name: SPECTRA Coverage
        description: Analyzes test coverage across documentation, requirements, and automation.
        tools: [{{ReadOnlyToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Coverage

        You analyze test coverage by running a CLI command. Follow these steps:

        ### Tool call 1: runInTerminal
        ```
        spectra ai analyze --coverage --auto-link --format markdown --output coverage.md --verbosity normal
        ```

        ### Tool call 2: awaitTerminal

        ### Tool call 3: readFile `coverage.md`
        Read the generated coverage report.

        ### Your response:
        Show the three coverage sections from the report:
        - **Documentation coverage**: X% (N/M documents) — list uncovered docs
        - **Requirements coverage**: X% (N/M requirements) — list untested requirements
        - **Automation coverage**: X% (N/M tests) — list unlinked tests

        If the user asks to improve coverage, suggest generating tests for uncovered areas.
        """;

    public const string Dashboard = $$"""
        ---
        name: SPECTRA Dashboard
        description: Generates the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
        tools: [{{ReadOnlyToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Dashboard

        You generate the dashboard by running CLI commands. Follow these steps:

        ### Tool call 1: runInTerminal
        First link automation and generate the dashboard:
        ```
        spectra ai analyze --coverage --auto-link --verbosity normal && spectra dashboard --output ./site --verbosity normal
        ```

        ### Tool call 2: awaitTerminal

        ### Tool call 3: runInTerminal
        Open the dashboard in the default browser:
        ```
        start ./site/index.html
        ```

        ### Your response:
        - "Dashboard generated and opened in your browser."
        - Show the coverage summary if visible in the output.
        """;

    public const string Validate = $$"""
        ---
        name: SPECTRA Validate
        description: Validates all test case files for correct format, unique IDs, and required fields.
        tools: [{{ReadOnlyToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Validate

        You validate test cases by running a CLI command. Follow these steps:

        ### Tool call 1: runInTerminal
        ```
        spectra validate --output-format json --verbosity normal
        ```

        ### Tool call 2: awaitTerminal

        ### Tool call 3: terminalLastCommand
        Read the terminal output to get the validation results.

        ### Your response:
        - If no errors: "All tests are valid."
        - If errors found: list each error with file and message. Suggest fixes.
        """;

    public const string List = $$"""
        ---
        name: SPECTRA List
        description: Lists test suites, shows test case details, and browses the test repository.
        tools: [{{ReadOnlyToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA List

        You list tests and suites by running CLI commands. Follow these steps:

        ## To list all suites:

        ### Tool call 1: runInTerminal
        ```
        spectra list --verbosity normal
        ```

        ### Tool call 2: awaitTerminal

        ### Tool call 3: terminalLastCommand
        Read the terminal output to get the list of suites and test counts.

        ### Your response:
        Show each suite with its test count.

        ---

        ## To show a specific test:

        ### Tool call 1: runInTerminal
        ```
        spectra show {test-id} --verbosity normal
        ```

        ### Tool call 2: awaitTerminal

        ### Tool call 3: terminalLastCommand
        Read the terminal output to get the test details.

        ### Your response:
        Show the test title, steps, expected result, priority, and tags.
        """;

    public const string InitProfile = $$"""
        ---
        name: SPECTRA Profile
        description: Creates or updates the generation profile that controls how AI generates test cases.
        tools: [{{ReadOnlyToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Profile

        You configure the generation profile by running a CLI command.

        First ask the user what they want to configure:
        - Detail level (high-level / detailed / very detailed)
        - Negative scenario focus
        - Domain-specific needs
        - Default priority

        ### Tool call 1: runInTerminal
        ```
        spectra init-profile --verbosity normal
        ```

        ### Tool call 2: awaitTerminal

        ### Tool call 3: terminalLastCommand
        Read the terminal output to confirm the profile was created/updated.

        ### Your response:
        Confirm what was configured and where the profile was saved.
        """;

    public const string Help = """
        ---
        name: SPECTRA Help
        description: Shows all available SPECTRA commands and prompts you can use in Copilot Chat.
        tools: []
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Help

        When the user asks for help, what they can do, or what commands are available, respond with this:

        ---

        ## Test Case Generation (SPECTRA Generation agent)

        | What you want | What to type |
        |--------------|-------------|
        | Generate tests for a suite | "generate test cases for payments" |
        | Generate for a new suite | "generate test cases for search" |
        | Generate more tests | "generate more tests for authentication" |
        | Generate specific count | "generate 50 test cases for gdpr-compliance" |
        | Generate focused tests | "generate negative tests for payments" |
        | Generate edge case tests | "generate edge case tests for citizen-registration" |
        | Generate security tests | "generate security tests for authentication" |

        ## Test Execution (SPECTRA Execution agent)

        | What you want | What to type |
        |--------------|-------------|
        | Run a test suite | "run tests for notification" |
        | Run high priority tests | "run high priority tests" |
        | Run smoke tests | "run the smoke tests" |
        | Resume a paused run | "resume the last run" |
        | Check active runs | "what runs are active?" |
        | Cancel all runs | "cancel all runs" |
        | View run history | "show run history" |
        | Smart test selection | "what should I test?" |

        ## Coverage Analysis

        | What you want | What to type |
        |--------------|-------------|
        | Full coverage report | "show test coverage" |
        | Find uncovered areas | "what areas don't have tests?" |
        | Check specific area | "show coverage for payments" |
        | Find untested requirements | "which requirements aren't tested?" |

        ## Dashboard

        | What you want | What to type |
        |--------------|-------------|
        | Generate and open dashboard | "generate the dashboard" |
        | Rebuild dashboard | "update the dashboard" |

        ## Validation

        | What you want | What to type |
        |--------------|-------------|
        | Validate all tests | "validate all test cases" |
        | Check for errors | "are there any test case errors?" |

        ## List & Browse Tests

        | What you want | What to type |
        |--------------|-------------|
        | List all suites | "list all test suites" |
        | Show a test | "show me TC-100" |
        | Find tests by topic | "what tests do we have for payments?" |

        ## Test Updates

        | What you want | What to type |
        |--------------|-------------|
        | Update tests after doc changes | "update tests for notification" |
        | Preview changes | "show diff for notification tests" |

        ## CLI Commands (Terminal)

        ```bash
        spectra ai generate --suite payments --count 20
        spectra ai generate --suite payments --analyze-only
        spectra ai analyze --coverage --auto-link
        spectra dashboard --output ./site
        spectra validate
        spectra list
        spectra show TC-100
        spectra docs index --force
        spectra ai update --suite notification --diff
        ```
        """;
}
