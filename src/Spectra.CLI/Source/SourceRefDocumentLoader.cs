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
    /// <see cref="FileNotFoundException"/> whose message says why (<see cref="MissingMessage"/>); otherwise it is
    /// skipped. <paramref name="sourceDir"/> is the configured <c>source.local_dir</c> (Spec 080 A2/A5: callers
    /// read it through <c>ConfigLoader</c>; it may lie outside the repository).
    /// </summary>
    public static async Task<IReadOnlyList<SourceDocument>> LoadAsync(
        IReadOnlyList<string> sourceRefs,
        string repoRoot,
        string sourceDir,
        bool failOnMissing,
        CancellationToken ct)
    {
        var resolver = new SourceRefResolver(repoRoot, sourceDir);
        var resolution = resolver.Resolve(sourceRefs);

        if (failOnMissing && resolution.Missing.FirstOrDefault(m => m.Reason != MissingReason.Empty) is { } missing)
        {
            throw new FileNotFoundException(MissingMessage(missing), missing.Ref);
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

    /// <summary>Spec 080 A2: the refusal message names the reason, not just "not found".</summary>
    public static string MissingMessage(MissingSourceRef missing) => missing.Reason == MissingReason.OutsideAllowedRoots
        ? $"Source ref is outside the repository and the configured source directory: {missing.Ref}"
        : $"Source ref not found: {missing.Ref}";
}
