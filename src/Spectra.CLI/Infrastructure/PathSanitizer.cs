namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Utilities for sanitizing and validating file paths.
/// </summary>
public static class PathSanitizer
{
    /// <summary>
    /// Characters not allowed in path segments.
    /// </summary>
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars()
        .Concat(new[] { ':', '*', '?', '"', '<', '>', '|' })
        .Distinct()
        .ToArray();

    /// <summary>
    /// Validates that a path segment (filename or directory name) is safe.
    /// </summary>
    /// <param name="segment">The path segment to validate.</param>
    /// <returns>True if the segment is safe, false otherwise.</returns>
    public static bool IsValidSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        // Check for path traversal attempts
        if (segment.Contains("..") || segment.Contains("./") || segment.Contains(".\\"))
        {
            return false;
        }

        // Check for absolute paths
        if (Path.IsPathRooted(segment))
        {
            return false;
        }

        // Check for invalid characters
        if (segment.IndexOfAny(InvalidPathChars) >= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sanitizes a path segment by removing or replacing invalid characters.
    /// </summary>
    /// <param name="segment">The path segment to sanitize.</param>
    /// <returns>The sanitized segment, or null if it cannot be sanitized.</returns>
    public static string? SanitizeSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return null;
        }

        // Remove path traversal sequences
        var sanitized = segment
            .Replace("../", "")
            .Replace("..\\", "")
            .Replace("..", "")
            .Replace("./", "")
            .Replace(".\\", "");

        // Replace invalid characters with underscore
        foreach (var c in InvalidPathChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Trim leading/trailing spaces and dots
        sanitized = sanitized.Trim().Trim('.');

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    /// <summary>
    /// Validates that a path is contained within a base directory.
    /// </summary>
    /// <param name="basePath">The base directory path.</param>
    /// <param name="targetPath">The target path to validate.</param>
    /// <returns>True if the target path is within the base directory.</returns>
    public static bool IsPathWithinBase(string basePath, string targetPath)
    {
        var baseFullPath = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var targetFullPath = Path.GetFullPath(targetPath);

        return targetFullPath.StartsWith(baseFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || targetFullPath.Equals(baseFullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Safely combines paths, ensuring the result stays within the base directory.
    /// </summary>
    /// <param name="basePath">The base directory path.</param>
    /// <param name="segments">Additional path segments.</param>
    /// <returns>The combined path, or null if invalid.</returns>
    public static string? SafeCombine(string basePath, params string[] segments)
    {
        // Validate all segments
        foreach (var segment in segments)
        {
            if (!IsValidSegment(segment))
            {
                return null;
            }
        }

        var combined = Path.Combine(new[] { basePath }.Concat(segments).ToArray());
        var fullPath = Path.GetFullPath(combined);

        // Ensure result is within base
        if (!IsPathWithinBase(basePath, fullPath))
        {
            return null;
        }

        return fullPath;
    }
}
