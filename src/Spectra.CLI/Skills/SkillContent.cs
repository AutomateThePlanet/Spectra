namespace Spectra.CLI.Skills;

/// <summary>
/// Bundled SKILL file contents for spectra init.
/// </summary>
public static class SkillContent
{
    public static readonly Dictionary<string, string> All = new()
    {
        ["spectra-generate"] = Generate,
        ["spectra-coverage"] = Coverage,
        ["spectra-dashboard"] = Dashboard,
        ["spectra-validate"] = Validate,
        ["spectra-list"] = List,
        ["spectra-init-profile"] = InitProfile,
    };

    public const string Generate = """
        ---
        name: SPECTRA Generate
        description: Generate test cases from documentation. Analyzes docs, recommends count, generates with AI verification.
        ---

        When the user asks to generate, create, or write test cases:

        1. Determine the suite name from the user's request
        2. Determine any filters: focus area, count, priority, tags
        3. Run the CLI command in terminal:

           spectra ai generate --suite {suite} [--count {n}] [--focus "{focus}"] --output-format json --verbosity quiet

        4. Parse the JSON output and present results:
           - How many tests were generated
           - Grounding breakdown (grounded/partial/rejected)
           - Any remaining gaps or suggestions

        5. If the user wants to generate from suggestions:

           spectra ai generate --suite {suite} --from-suggestions --output-format json --verbosity quiet

        6. If the user describes a test case to create:

           spectra ai generate --suite {suite} --from-description "{description}" [--context "{context}"] --output-format json --verbosity quiet

        7. For full automated generation:

           spectra ai generate --suite {suite} --auto-complete --output-format json --verbosity quiet

        8. Continue the conversation — offer to generate more, switch suites, or check coverage

        ### Examples of user requests:
        - "Generate test cases for the checkout suite"
        - "Create negative test cases for authentication"
        - "Generate 10 high-priority tests for payments"
        - "Add a test case for IBAN validation — it's not documented"
        - "What gaps do we still have in the search suite?"
        """;

    public const string Coverage = """
        ---
        name: SPECTRA Coverage
        description: Analyze test coverage across documentation, requirements, and automation.
        ---

        When the user asks about coverage, gaps, or what needs testing:

        1. Run:

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

    public const string Dashboard = """
        ---
        name: SPECTRA Dashboard
        description: Generate the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
        ---

        When the user asks to generate, update, or build the dashboard:

        1. Run:

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

    public const string Validate = """
        ---
        name: SPECTRA Validate
        description: Validate all test case files for correct format, unique IDs, and required fields.
        ---

        When the user asks to validate, check, or verify test files:

        1. Run:

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

    public const string List = """
        ---
        name: SPECTRA List
        description: List test suites, show test case details, and browse the test repository.
        ---

        When the user asks to list, show, browse, or find test cases:

        1. To list suites:

           spectra list --output-format json --verbosity quiet

           Present: suite names with test counts

        2. To show a specific test:

           spectra show {test-id} --output-format json --verbosity quiet

           Present: full test case details (title, steps, expected results, metadata)

        ### Examples:
        - "List all test suites"
        - "Show me TC-101"
        - "What tests do we have for checkout?"
        - "How many tests are in the authentication suite?"
        """;

    public const string InitProfile = """
        ---
        name: SPECTRA Profile
        description: Create or update the generation profile that controls how AI generates test cases.
        ---

        When the user asks to configure, set up, or change generation preferences:

        1. Ask what they want to configure:
           - Detail level (high-level / detailed / very detailed)
           - Negative scenario focus (minimum count per feature)
           - Domain-specific needs (payments, auth, GDPR, etc.)
           - Default priority
           - Formatting preferences

        2. Build the CLI command:

           spectra init-profile --output-format json --verbosity quiet --no-interaction

        3. Confirm the profile was created/updated

        ### Examples:
        - "Set up a generation profile"
        - "I want more detailed test steps"
        - "Configure SPECTRA for payment domain testing"
        """;
}
