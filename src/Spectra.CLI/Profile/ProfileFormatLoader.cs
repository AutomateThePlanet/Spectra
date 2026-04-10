using System.Reflection;
using YamlDotNet.RepresentationModel;

namespace Spectra.CLI.Profile;

/// <summary>
/// Resolves the JSON output schema string used for the {{profile_format}} prompt placeholder.
/// Resolution order:
///   1. <c>profiles/_default.yaml</c> on disk (parse YAML, extract <c>format</c> field).
///   2. Built-in embedded default (<c>Skills/Content/Profiles/_default.yaml</c>).
/// On parse failure or missing <c>format</c> field, falls back gracefully to the embedded default.
/// </summary>
public static class ProfileFormatLoader
{
    private const string EmbeddedProfileResource = "Spectra.CLI.Skills.Content.Profiles._default.yaml";
    private const string EmbeddedCustomizationResource = "Spectra.CLI.Skills.Content.Docs.CUSTOMIZATION.md";
    private const string DefaultProfileFile = "_default.yaml";
    private const string ProfilesDir = "profiles";

    /// <summary>
    /// Loads the JSON schema string used for {{profile_format}}.
    /// </summary>
    /// <param name="workingDirectory">Project root (used to resolve <c>profiles/_default.yaml</c>).</param>
    public static string LoadFormat(string workingDirectory)
    {
        var diskPath = Path.Combine(workingDirectory, ProfilesDir, DefaultProfileFile);
        if (File.Exists(diskPath))
        {
            try
            {
                var content = File.ReadAllText(diskPath);
                var format = ExtractFormatField(content);
                if (!string.IsNullOrWhiteSpace(format))
                {
                    return format!;
                }
            }
            catch
            {
                // Fall through to embedded default on any parse error.
            }
        }

        return LoadEmbeddedFormat();
    }

    /// <summary>
    /// Returns the raw bytes of the embedded <c>_default.yaml</c> profile,
    /// for use by <c>spectra init</c> and <c>spectra update-skills</c>.
    /// </summary>
    public static string LoadEmbeddedDefaultYaml()
    {
        return ReadEmbeddedResource(EmbeddedProfileResource);
    }

    /// <summary>
    /// Returns the raw bytes of the embedded <c>CUSTOMIZATION.md</c>,
    /// for use by <c>spectra init</c> and <c>spectra update-skills</c>.
    /// </summary>
    public static string LoadEmbeddedCustomizationGuide()
    {
        return ReadEmbeddedResource(EmbeddedCustomizationResource);
    }

    private static string LoadEmbeddedFormat()
    {
        var content = LoadEmbeddedDefaultYaml();
        return ExtractFormatField(content)
            ?? throw new InvalidOperationException(
                $"Embedded profile resource '{EmbeddedProfileResource}' is missing the required 'format' field.");
    }

    private static string? ExtractFormatField(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent)) return null;

        using var reader = new StringReader(yamlContent);
        var stream = new YamlStream();
        stream.Load(reader);

        if (stream.Documents.Count == 0) return null;
        if (stream.Documents[0].RootNode is not YamlMappingNode root) return null;

        foreach (var entry in root.Children)
        {
            if (entry.Key is YamlScalarNode key &&
                string.Equals(key.Value, "format", StringComparison.Ordinal) &&
                entry.Value is YamlScalarNode value)
            {
                return value.Value;
            }
        }

        return null;
    }

    private static string ReadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(ProfileFormatLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
