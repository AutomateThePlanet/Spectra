using Spectra.Core.Models.Index;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Source;

/// <summary>
/// Assigns each discovered document to a suite. Spec 040 §3.5 priority order:
/// frontmatter override → config override → directory default → <c>_root</c> fallback.
/// </summary>
/// <remarks>
/// Phase 2 scope: directory-default rule = first path segment beneath
/// <c>source.local_dir</c>. Pattern-aware splitting (e.g., flagging
/// <c>RD_Topics/Old/</c> as its own <c>RD_Topics_Old</c> suite) is wired in
/// Phase 5 once <see cref="ExclusionPatternMatcher"/> exists. Frontmatter
/// validation regex is enforced now via <see cref="DocSuiteEntry.IdRegex"/>.
/// </remarks>
public sealed class SuiteResolver
{
    private const string RootSuiteId = "_root";

    /// <summary>
    /// Resolves a suite ID for every input document.
    /// </summary>
    /// <param name="documents">Input docs. <c>RelativePath</c> is repo-relative,
    /// forward-slash; <c>FrontmatterSuite</c>/<c>FrontmatterAnalyze</c> are the
    /// already-parsed frontmatter values (null when absent).</param>
    /// <param name="sourceConfig">Used to resolve <c>local_dir</c> and the
    /// per-path <c>group_overrides</c> map.</param>
    /// <returns>A result containing the resolved suite ID per document and any
    /// frontmatter validation errors. Callers SHOULD surface validation errors
    /// to the user before continuing — they indicate user-authored mistakes.</returns>
    public ResolutionResult Resolve(
        IReadOnlyList<DiscoveredDoc> documents,
        SourceConfig sourceConfig)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(sourceConfig);

        var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();
        var localDirPrefix = NormalizeLocalDirPrefix(sourceConfig.LocalDir);
        var groupOverrides = sourceConfig.GroupOverrides ?? new Dictionary<string, string>();

        foreach (var doc in documents)
        {
            var suiteId = ResolveOne(doc, localDirPrefix, groupOverrides, errors);
            assignments[doc.RelativePath] = suiteId;
        }

        return new ResolutionResult(assignments, errors);
    }

    private static string ResolveOne(
        DiscoveredDoc doc,
        string localDirPrefix,
        IReadOnlyDictionary<string, string> groupOverrides,
        List<string> errors)
    {
        // 1. Frontmatter override (highest priority).
        if (!string.IsNullOrEmpty(doc.FrontmatterSuite))
        {
            if (!DocSuiteEntry.IdRegex.IsMatch(doc.FrontmatterSuite))
            {
                errors.Add(
                    $"Invalid suite identifier in frontmatter of '{doc.RelativePath}': '{doc.FrontmatterSuite}'. " +
                    $"Suite IDs must match {DocSuiteEntry.IdRegex} (no slashes, spaces, or leading dot/dash).");
                // Fall through to defaults rather than failing the whole resolve.
            }
            else
            {
                return doc.FrontmatterSuite;
            }
        }

        // 2. Config override (per-path map).
        if (groupOverrides.TryGetValue(doc.RelativePath, out var configuredId))
        {
            return configuredId;
        }

        // 3. Directory-based default: first segment beneath local_dir.
        var beneathLocalDir = StripPrefix(doc.RelativePath, localDirPrefix);
        var firstSlash = beneathLocalDir.IndexOf('/');
        if (firstSlash < 0)
        {
            // Document lives directly in local_dir → root fallback.
            return RootSuiteId;
        }

        var firstSegment = beneathLocalDir.Substring(0, firstSlash);
        if (string.IsNullOrEmpty(firstSegment))
        {
            return RootSuiteId;
        }

        return Sanitize(firstSegment);
    }

    private static string NormalizeLocalDirPrefix(string localDir)
    {
        if (string.IsNullOrEmpty(localDir)) return string.Empty;
        var normalized = localDir.Replace('\\', '/').TrimEnd('/');
        return normalized + "/";
    }

    private static string StripPrefix(string relativePath, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return relativePath;
        return relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? relativePath.Substring(prefix.Length)
            : relativePath;
    }

    private static string Sanitize(string segment)
    {
        // Replace any path separators with '.' for nested joins.
        var sanitized = segment.Replace('/', '.').Replace('\\', '.');
        return sanitized.Trim('.');
    }
}

/// <summary>
/// One discovered document fed into <see cref="SuiteResolver"/>.
/// </summary>
public readonly record struct DiscoveredDoc(
    string RelativePath,
    string? FrontmatterSuite = null,
    bool? FrontmatterAnalyze = null);

/// <summary>
/// Result of <see cref="SuiteResolver.Resolve"/>.
/// </summary>
public sealed class ResolutionResult
{
    public ResolutionResult(
        IReadOnlyDictionary<string, string> assignments,
        IReadOnlyList<string> errors)
    {
        Assignments = assignments;
        Errors = errors;
    }

    /// <summary>Doc-relative-path → resolved suite ID.</summary>
    public IReadOnlyDictionary<string, string> Assignments { get; }

    /// <summary>Frontmatter-rejection messages. Non-empty when callers should warn.</summary>
    public IReadOnlyList<string> Errors { get; }
}
