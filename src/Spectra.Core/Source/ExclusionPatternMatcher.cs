using Microsoft.Extensions.FileSystemGlobbing;

namespace Spectra.Core.Source;

/// <summary>
/// Matches repo-relative document paths against a configured set of exclusion
/// globs. Wraps <see cref="Microsoft.Extensions.FileSystemGlobbing.Matcher"/> for
/// full glob semantics (<c>**</c>, <c>*</c>, brace expansion).
/// </summary>
/// <remarks>
/// Relocated from <c>Spectra.CLI.Source</c> to <c>Spectra.Core.Source</c> (Spec 060)
/// so the Core-resident coverage analyzer can reuse it without a CLI→Core inversion.
/// Behavior is unchanged from the original CLI implementation.
/// </remarks>
public sealed class ExclusionPatternMatcher
{
    private readonly List<(string Pattern, Matcher Matcher)> _matchers;

    public ExclusionPatternMatcher(IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        _matchers = new List<(string, Matcher)>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;
            var matcher = new Matcher(StringComparison.Ordinal);
            matcher.AddInclude(pattern);
            _matchers.Add((pattern, matcher));
        }
    }

    /// <summary>
    /// Returns true iff <paramref name="repoRelativePath"/> matches any of the
    /// configured patterns. <paramref name="matchedPattern"/> is set to the
    /// first matching glob on success, null otherwise. Path is treated as
    /// forward-slash-separated regardless of the host platform.
    /// </summary>
    public bool IsExcluded(string repoRelativePath, out string? matchedPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRelativePath);

        var normalized = repoRelativePath.Replace('\\', '/');
        foreach (var (pattern, matcher) in _matchers)
        {
            var result = matcher.Match(normalized);
            if (result.HasMatches)
            {
                matchedPattern = pattern;
                return true;
            }
        }
        matchedPattern = null;
        return false;
    }

    /// <summary>True when no patterns were configured.</summary>
    public bool IsEmpty => _matchers.Count == 0;
}
