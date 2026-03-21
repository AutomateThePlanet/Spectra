namespace Spectra.Core.Coverage;

/// <summary>
/// Shared utility for normalizing source_ref paths before comparison with filesystem paths.
/// Strips fragment anchors, normalizes slashes, and supports case-insensitive matching.
/// </summary>
public static class SourceRefNormalizer
{
    /// <summary>
    /// Strips the fragment anchor (everything after and including #) from a source ref.
    /// </summary>
    public static string StripFragment(string sourceRef)
    {
        if (string.IsNullOrEmpty(sourceRef))
            return sourceRef ?? string.Empty;

        var hashIndex = sourceRef.IndexOf('#');
        return hashIndex >= 0 ? sourceRef[..hashIndex] : sourceRef;
    }

    /// <summary>
    /// Normalizes a path by stripping fragment anchors, normalizing slashes to forward slashes,
    /// and trimming leading slashes.
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path ?? string.Empty;

        var stripped = StripFragment(path);
        return stripped.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    /// Normalizes a path for case-insensitive comparison by applying NormalizePath
    /// and converting to lowercase.
    /// </summary>
    public static string NormalizeForComparison(string path)
    {
        return NormalizePath(path).ToLowerInvariant();
    }
}
