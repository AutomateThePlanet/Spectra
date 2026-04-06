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

        You generate test cases by running CLI commands. You MUST follow the exact tool sequence below.

        ## When user asks to generate test cases:

        ### Tool call 1: runInTerminal
        Run this command (replace {suite} with the suite name from the user's request):
        ```
        spectra ai generate --suite {suite} --analyze-only --output-format json --verbosity quiet
        ```

        ### Tool call 2: awaitTerminal
        Wait for the command to finish.

        ### Tool call 3: readFile
        Read the file `.spectra/last-result.json` to get the analysis results.

        ### Your response after reading the file:
        Parse the JSON and tell the user:
        - "I analyzed the documentation and found **{analysis.recommended}** testable behaviors to cover."
        - "Shall I generate **{analysis.recommended}** test cases for the **{suite}** suite?"

        Then STOP and wait for the user to respond.

        ---

        ## After user approves:

        ### Tool call 4: runInTerminal
        Run this command (replace {count} with the number the user approved):
        ```
        spectra ai generate --suite {suite} --count {count} --output-format json --verbosity quiet
        ```

        ### Tool call 5: awaitTerminal
        Wait for the command to finish. This takes 1-2 minutes.

        ### Tool call 6: readFile
        Read the file `.spectra/last-result.json` to get the generation results.

        ### Your response after reading the file:
        Parse the JSON and tell the user:
        - "Generated **{generation.tests_written}** test cases for the **{suite}** suite."
        - List each file from the `files_created` array.
        - If `generation.tests_rejected_by_critic` > 0, mention how many were rejected.
        """;
}
