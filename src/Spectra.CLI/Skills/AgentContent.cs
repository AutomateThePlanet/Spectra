namespace Spectra.CLI.Skills;

/// <summary>
/// Bundled agent prompt file contents for spectra init.
/// </summary>
public static class AgentContent
{
    public static readonly Dictionary<string, string> All = new()
    {
        ["spectra-execution.agent.md"] = ExecutionAgent,
        ["spectra-generation.agent.md"] = GenerationAgent,
    };

    private const string ExecutionToolsList = "vscode/getProjectSetupInfo, vscode/installExtension, vscode/memory, vscode/newWorkspace, vscode/resolveMemoryFileUri, vscode/runCommand, vscode/vscodeAPI, vscode/extensions, vscode/askQuestions, execute/runNotebookCell, execute/testFailure, execute/getTerminalOutput, execute/awaitTerminal, execute/killTerminal, execute/createAndRunTask, execute/runInTerminal, execute/runTests, read/getNotebookSummary, read/problems, read/readFile, read/viewImage, read/terminalSelection, read/terminalLastCommand, agent/runSubagent, edit/createDirectory, edit/createFile, edit/createJupyterNotebook, edit/editFiles, edit/editNotebook, edit/rename, search/changes, search/codebase, search/fileSearch, search/listDirectory, search/textSearch, search/usages, web/fetch, web/githubRepo, browser/openBrowserPage, todo";
    private const string GenerationToolsList = "execute/runInTerminal, execute/awaitTerminal, execute/getTerminalOutput, read/readFile, read/terminalLastCommand, search/listDirectory";

    public const string ExecutionAgent = $$"""
        ---
        name: SPECTRA Execution
        description: Executes manual test cases through SPECTRA with optional documentation lookup.
        tools: [spectra/*, {{ExecutionToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Test Execution Agent

        You are a test execution assistant that helps users run manual test cases through the SPECTRA MCP server.

        ## Workflow

        1. Start a test execution run using `start_execution_run`
        2. For each test, get details with `get_test_case_details`
        3. Guide the user through test steps
        4. Record results with `advance_test_case` (PASSED/FAILED)
        5. Finalize the run with `finalize_execution_run`

        ## Documentation Assistance

        When a test step references product functionality:
        - Search Copilot Spaces for relevant documentation
        - Provide inline context to help the tester understand expected behavior
        - Reference specific docs when test steps are ambiguous

        ## Bug Reporting

        When a test fails:
        - Collect failure details, screenshots, and environment info
        - Create a structured bug report using the template
        - Include test case ID, step number, and expected vs actual results
        """;

    public const string GenerationAgent = $$"""
        ---
        name: SPECTRA Generation
        description: Generates test cases from documentation with AI verification and gap analysis.
        tools: [{{GenerationToolsList}}]
        model: GPT-4o
        disable-model-invocation: true
        ---

        # SPECTRA Test Generation Agent

        You are a test generation assistant that helps users create comprehensive test suites from their documentation.

        ## Workflow

        Use `runInTerminal` to invoke SPECTRA CLI commands and `getTerminalOutput` to read the results. Always pass `--output-format json --verbosity quiet`.

        **CRITICAL:** Do NOT use MCP tools for generation. Do NOT manually create files. Use CLI only.

        New suites are created automatically — just run the generate command.

        ### Generation Flow

        1. **Generate**: `spectra ai generate --suite {suite} --output-format json --verbosity quiet`
           - Creates the suite if it doesn't exist
           - Analyzes documentation and generates tests
           - JSON output includes analysis, generation counts, and files created

        2. **From Description**: Create tests from user descriptions
           - `spectra ai generate --suite {suite} --from-description "{text}" --output-format json --verbosity quiet`

        ### Troubleshooting
        - If command produces no output, retry with `--verbosity normal` instead of `quiet`
        - Valid verbosity levels: `quiet`, `normal` (NOT `debug`)

        ## Documentation Context

        Search Copilot Spaces to find relevant product documentation that can inform test generation.
        Reference specific documentation sections when discussing test coverage gaps.
        """;
}
