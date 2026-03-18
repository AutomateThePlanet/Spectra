using System.Reflection;

namespace Spectra.CLI.Agent;

/// <summary>
/// Loads embedded agent prompt resources from the CLI assembly.
/// </summary>
public static class AgentResourceLoader
{
    private const string ResourcePrefix = "Spectra.CLI.Agent.Resources.";

    /// <summary>
    /// Gets the execution agent prompt content.
    /// </summary>
    /// <returns>The full markdown content of the execution agent prompt.</returns>
    public static string GetExecutionAgentPrompt()
    {
        return LoadResource("spectra-execution.agent.md");
    }

    /// <summary>
    /// Gets the skill file content for Copilot CLI.
    /// </summary>
    /// <returns>The full markdown content of the SKILL.md file.</returns>
    public static string GetSkillContent()
    {
        return LoadResource("SKILL.md");
    }

    /// <summary>
    /// Loads an embedded resource by filename.
    /// </summary>
    /// <param name="resourceName">The resource filename (e.g., "spectra-execution.agent.md").</param>
    /// <returns>The resource content as a string.</returns>
    /// <exception cref="InvalidOperationException">If the resource is not found.</exception>
    public static string LoadResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fullName = ResourcePrefix + resourceName;

        using var stream = assembly.GetManifestResourceStream(fullName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Expected: {fullName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Lists all available embedded agent resources.
    /// </summary>
    /// <returns>Resource names without the prefix.</returns>
    public static IEnumerable<string> ListResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();

        foreach (var name in names)
        {
            if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                yield return name[ResourcePrefix.Length..];
            }
        }
    }
}
