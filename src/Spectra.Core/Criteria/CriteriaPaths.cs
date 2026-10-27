using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Criteria;

/// <summary>
/// Path rules for acceptance-criteria storage (Spec 079): normalised <c>source_doc</c> values and the per-doc
/// criteria file each document maps to.
/// </summary>
public static class CriteriaPaths
{
    public const string ErrorInvalidDocPath = "INVALID_DOC_PATH";
    public const string ErrorFileCollision = "CRITERIA_FILE_COLLISION";

    private const string CriteriaSuffix = ".criteria.yaml";

    /// <summary>
    /// Normalises a document path as typed (<c>docs\x.md</c>, <c>./docs/x.md</c>, <c>/docs/x.md</c>, or an absolute
    /// path inside the repository) to the repository-relative, forward-slash form. Paths outside the repository
    /// are refused with <see cref="ErrorInvalidDocPath"/>; this never throws.
    /// </summary>
    public static SourceDocResult NormalizeSourceDoc(string? input, string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(repoRoot))
        {
            return SourceDocResult.Fail();
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repoRoot));
        var candidate = input.Trim().Replace('\\', '/');

        try
        {
            if (Path.IsPathFullyQualified(candidate))
            {
                var absolute = Path.GetFullPath(candidate);
                if (IsUnder(absolute, root))
                {
                    return SourceDocResult.Ok(Relative(root, absolute));
                }
            }

            var relative = candidate.TrimStart('/');
            if (relative.Length == 0)
            {
                return SourceDocResult.Fail();
            }

            var full = Path.GetFullPath(Path.Combine(root, relative));
            return IsUnder(full, root) ? SourceDocResult.Ok(Relative(root, full)) : SourceDocResult.Fail();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return SourceDocResult.Fail();
        }
    }

    /// <summary>
    /// Lexical-only normalisation (no filesystem, no repository root): <c>\</c> → <c>/</c>, no leading
    /// <c>./</c> or <c>/</c>. Used to compare index entries written by older versions.
    /// </summary>
    public static string NormalizeLexical(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var s = path.Trim().Replace('\\', '/');
        while (s.StartsWith("./", StringComparison.Ordinal))
        {
            s = s[2..];
        }

        return s.TrimStart('/');
    }

    /// <summary>
    /// The per-doc criteria file, relative to the criteria directory: the repository-relative doc path with a
    /// trailing <c>.md</c> removed, plus <c>.criteria.yaml</c>. Injective over repository paths.
    /// </summary>
    public static string DerivedFile(string normalizedSourceDoc)
    {
        var doc = NormalizeLexical(normalizedSourceDoc);
        var stem = doc.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? doc[..^3] : doc;
        return stem + CriteriaSuffix;
    }

    /// <summary>
    /// True when any document source still uses the pre-079 layout: a non-normalised <c>source_doc</c> or a
    /// <c>file</c> that is not its derived path. Imports are ignored.
    /// </summary>
    public static bool IsOldLayout(CriteriaIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.Sources.Any(IsOldLayoutSource);
    }

    /// <summary>True when this document source is not yet in the per-doc layout.</summary>
    public static bool IsOldLayoutSource(CriteriaSource source)
    {
        if (!IsDocumentSource(source))
        {
            return false;
        }

        var lexical = NormalizeLexical(source.SourceDoc);
        return !string.Equals(source.SourceDoc, lexical, StringComparison.Ordinal)
               || !string.Equals(NormalizeLexical(source.File), DerivedFile(lexical), StringComparison.Ordinal);
    }

    /// <summary>A source that stands for a document on disk (not an import).</summary>
    public static bool IsDocumentSource(CriteriaSource source) =>
        string.Equals(source.SourceType, "document", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(source.SourceDoc);

    private static bool IsUnder(string fullPath, string root) =>
        fullPath.StartsWith(root + Path.DirectorySeparatorChar,
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private static string Relative(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('\\', '/');
}

/// <summary>Result of <see cref="CriteriaPaths.NormalizeSourceDoc"/>.</summary>
public sealed record SourceDocResult(string? Path, string? ErrorCode)
{
    public bool IsSuccess => Path is not null;

    internal static SourceDocResult Ok(string path) => new(path, null);

    internal static SourceDocResult Fail() => new(null, CriteriaPaths.ErrorInvalidDocPath);
}
