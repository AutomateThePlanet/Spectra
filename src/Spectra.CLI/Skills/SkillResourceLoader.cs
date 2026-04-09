using System.Reflection;

namespace Spectra.CLI.Skills;

/// <summary>
/// Loads SKILL and agent content from embedded .md resources.
/// Replaces tool list placeholders with actual tool lists.
/// </summary>
public static class SkillResourceLoader
{
    private const string SkillPrefix = "Spectra.CLI.Skills.Content.Skills.";
    private const string AgentPrefix = "Spectra.CLI.Skills.Content.Agents.";

    // Tool lists matching the original SkillContent/AgentContent constants
    private const string GenerateToolsList = "execute/runInTerminal, execute/awaitTerminal, read/readFile, search/listDirectory, browser/openBrowserPage";
    private const string ReadOnlyToolsList = "execute/runInTerminal, execute/awaitTerminal, read/readFile, read/problems, search/listDirectory, search/textSearch, browser/openBrowserPage";
    private const string ExecutionToolsList = "vscode/getProjectSetupInfo, vscode/installExtension, vscode/memory, vscode/newWorkspace, vscode/resolveMemoryFileUri, vscode/runCommand, vscode/vscodeAPI, vscode/extensions, vscode/askQuestions, execute/runNotebookCell, execute/testFailure, execute/getTerminalOutput, execute/awaitTerminal, execute/killTerminal, execute/createAndRunTask, execute/runInTerminal, execute/runTests, read/getNotebookSummary, read/problems, read/readFile, read/viewImage, read/terminalSelection, read/terminalLastCommand, agent/runSubagent, edit/createDirectory, edit/createFile, edit/createJupyterNotebook, edit/editFiles, edit/editNotebook, edit/rename, search/changes, search/codebase, search/fileSearch, search/listDirectory, search/textSearch, search/usages, web/fetch, web/githubRepo, browser/openBrowserPage, todo";

    private static readonly Dictionary<string, string> Placeholders = new()
    {
        ["{{GENERATE_TOOLS}}"] = GenerateToolsList,
        ["{{READONLY_TOOLS}}"] = ReadOnlyToolsList,
        ["{{EXECUTION_TOOLS}}"] = ExecutionToolsList,
        ["{{GENERATION_TOOLS}}"] = GenerateToolsList,
    };

    private static readonly Lazy<Dictionary<string, string>> CachedSkills = new(LoadAllSkills);
    private static readonly Lazy<Dictionary<string, string>> CachedAgents = new(LoadAllAgents);

    /// <summary>
    /// Gets all bundled SKILL file contents, keyed by SKILL name (e.g., "spectra-generate").
    /// </summary>
    public static Dictionary<string, string> GetAllSkills() => CachedSkills.Value;

    /// <summary>
    /// Gets all bundled agent file contents, keyed by filename (e.g., "spectra-execution.agent.md").
    /// </summary>
    public static Dictionary<string, string> GetAllAgents() => CachedAgents.Value;

    private static Dictionary<string, string> LoadAllSkills()
    {
        var result = new Dictionary<string, string>();
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(SkillPrefix, StringComparison.Ordinal))
                continue;

            var skillName = name[SkillPrefix.Length..];
            // Remove .md extension to get skill name (e.g., "spectra-generate")
            if (skillName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                skillName = skillName[..^3];

            var content = LoadAndReplace(assembly, name);
            result[skillName] = content;
        }

        return result;
    }

    private static Dictionary<string, string> LoadAllAgents()
    {
        var result = new Dictionary<string, string>();
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(AgentPrefix, StringComparison.Ordinal))
                continue;

            var agentName = name[AgentPrefix.Length..];
            var content = LoadAndReplace(assembly, name);
            result[agentName] = content;
        }

        return result;
    }

    private static string LoadAndReplace(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        foreach (var (placeholder, value) in Placeholders)
        {
            content = content.Replace(placeholder, value);
        }

        return content;
    }
}
