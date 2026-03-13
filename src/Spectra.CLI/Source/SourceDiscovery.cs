using System.Text.RegularExpressions;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Source;

/// <summary>
/// Discovers source documentation files.
/// </summary>
public sealed class SourceDiscovery
{
    private readonly SourceConfig _config;

    public SourceDiscovery(SourceConfig? config = null)
    {
        _config = config ?? new SourceConfig();
    }

    /// <summary>
    /// Discovers all source files matching the configuration.
    /// </summary>
    public IEnumerable<string> Discover(string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        if (!Directory.Exists(basePath))
        {
            yield break;
        }

        var includePatterns = _config.IncludePatterns.Select(CreateGlobRegex).ToList();
        var excludePatterns = _config.ExcludePatterns.Select(CreateGlobRegex).ToList();

        var searchPath = Path.Combine(basePath, _config.LocalDir.TrimEnd('/', '\\'));
        if (!Directory.Exists(searchPath))
        {
            yield break;
        }

        foreach (var file in EnumerateFilesRecursively(searchPath))
        {
            var relativePath = Path.GetRelativePath(basePath, file).Replace('\\', '/');

            if (ShouldInclude(relativePath, includePatterns, excludePatterns))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Discovers files and returns relative paths.
    /// </summary>
    public IEnumerable<(string AbsolutePath, string RelativePath)> DiscoverWithRelativePaths(string basePath)
    {
        foreach (var absolutePath in Discover(basePath))
        {
            var relativePath = Path.GetRelativePath(basePath, absolutePath).Replace('\\', '/');
            yield return (absolutePath, relativePath);
        }
    }

    private static IEnumerable<string> EnumerateFilesRecursively(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path))
        {
            yield return file;
        }

        foreach (var directory in Directory.EnumerateDirectories(path))
        {
            // Skip hidden directories
            if (Path.GetFileName(directory).StartsWith('.'))
            {
                continue;
            }

            foreach (var file in EnumerateFilesRecursively(directory))
            {
                yield return file;
            }
        }
    }

    private static bool ShouldInclude(string path, List<Regex> includePatterns, List<Regex> excludePatterns)
    {
        // Check if path matches any exclude pattern
        foreach (var exclude in excludePatterns)
        {
            if (exclude.IsMatch(path))
            {
                return false;
            }
        }

        // Check if path matches any include pattern
        foreach (var include in includePatterns)
        {
            if (include.IsMatch(path))
            {
                return true;
            }
        }

        return false;
    }

    private static Regex CreateGlobRegex(string pattern)
    {
        // Convert glob pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", ".")
            + "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
