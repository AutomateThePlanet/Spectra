using Spectra.Core.Models.Index;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Index;

/// <summary>
/// Reads <c>_manifest.yaml</c> (Spec 040 v2 layout) from disk.
/// </summary>
public sealed class DocIndexManifestReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Reads and validates the manifest at <paramref name="path"/>. Returns null
    /// if the file does not exist. Throws <see cref="InvalidOperationException"/>
    /// on schema-version mismatch, duplicate suite IDs, or malformed
    /// <c>excluded_pattern</c>/<c>excluded_by</c> combinations.
    /// </summary>
    public async Task<DocIndexManifest?> ReadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) return null;

        var content = await File.ReadAllTextAsync(path, ct);
        return Parse(content);
    }

    /// <summary>
    /// Parses YAML <paramref name="content"/> into a manifest. Same validation as
    /// <see cref="ReadAsync"/>; useful for unit tests that work with strings.
    /// </summary>
    public static DocIndexManifest Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        DocIndexManifest manifest;
        try
        {
            manifest = Deserializer.Deserialize<DocIndexManifest>(content)
                       ?? throw new InvalidOperationException("Manifest YAML deserialized to null.");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse manifest YAML: {ex.Message}", ex);
        }

        Validate(manifest);
        return manifest;
    }

    private static void Validate(DocIndexManifest manifest)
    {
        if (manifest.Version != 2)
        {
            throw new InvalidOperationException(
                $"Unsupported manifest version: {manifest.Version}. Expected 2.");
        }

        manifest.Groups ??= new List<DocSuiteEntry>();

        // Duplicate suite IDs.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in manifest.Groups)
        {
            if (!seen.Add(group.Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate suite ID in manifest: '{group.Id}'.");
            }

            // Pattern/excluded_by consistency per contracts/manifest-schema.md.
            if (group.ExcludedBy == "pattern" && string.IsNullOrEmpty(group.ExcludedPattern))
            {
                throw new InvalidOperationException(
                    $"Suite '{group.Id}' has excluded_by=pattern but no excluded_pattern.");
            }

            if (!string.IsNullOrEmpty(group.ExcludedPattern) && group.ExcludedBy != "pattern")
            {
                throw new InvalidOperationException(
                    $"Suite '{group.Id}' has excluded_pattern set but excluded_by='{group.ExcludedBy}'.");
            }

            // Defensive: normalise backslashes in stored paths.
            group.Path = group.Path.Replace('\\', '/');
            group.IndexFile = group.IndexFile.Replace('\\', '/');
        }
    }
}
