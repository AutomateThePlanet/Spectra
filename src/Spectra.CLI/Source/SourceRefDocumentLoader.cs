using System.Text.Json;
using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.CLI.Source;

/// <summary>
/// Loads the documents a test's <c>source_refs</c> point to, using the shared <see cref="SourceRefResolver"/>
/// (Spec 078). Used by the critic / repair prompt compilers so they resolve refs exactly like
/// <c>spectra ai update</c> does.
/// </summary>
public static class SourceRefDocumentLoader
{
    /// <summary>
    /// Resolves and reads the referenced documents, each file once, in first-reference order.
    /// Blank refs are ignored. With <paramref name="failOnMissing"/>, a ref that does not resolve throws
    /// <see cref="FileNotFoundException"/> (<c>Source ref not found: {ref}</c>); otherwise it is skipped.
    /// </summary>
    public static async Task<IReadOnlyList<SourceDocument>> LoadAsync(
        IReadOnlyList<string> sourceRefs,
        string repoRoot,
        bool failOnMissing,
        CancellationToken ct)
    {
        var resolver = new SourceRefResolver(repoRoot, await ReadSourceDirAsync(repoRoot, ct));
        var resolution = resolver.Resolve(sourceRefs);

        if (failOnMissing && resolution.Missing.FirstOrDefault(m => m.Reason != MissingReason.Empty) is { } missing)
        {
            throw new FileNotFoundException($"Source ref not found: {missing.Ref}", missing.Ref);
        }

        var docs = new List<SourceDocument>();
        foreach (var fullPath in resolution.DistinctFullPaths)
        {
            var resolved = resolution.Resolved.First(r => r.FullPath == fullPath);
            docs.Add(new SourceDocument
            {
                Path = resolved.RelativePath,
                Title = Path.GetFileNameWithoutExtension(fullPath),
                Content = await File.ReadAllTextAsync(fullPath, ct)
            });
        }

        return docs;
    }

    /// <summary>The configured <c>source.local_dir</c> (default <c>docs</c>), tolerant of a missing or invalid config.</summary>
    private static async Task<string> ReadSourceDirAsync(string repoRoot, CancellationToken ct)
    {
        const string fallback = "docs";
        var configPath = Path.Combine(repoRoot, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            return fallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(configPath, ct));
            return doc.RootElement.TryGetProperty("source", out var source)
                   && source.TryGetProperty("local_dir", out var dir)
                   && dir.ValueKind == JsonValueKind.String
                   && !string.IsNullOrWhiteSpace(dir.GetString())
                ? dir.GetString()!
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
