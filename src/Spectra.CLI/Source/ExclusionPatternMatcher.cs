using Microsoft.Extensions.FileSystemGlobbing;

namespace Spectra.CLI.Source;

/// <summary>
/// Matches repo-relative document paths against the configured
/// <c>coverage.analysis_exclude_patterns</c> globs (Spec 040 §3.6).
/// Wraps <see cref="Microsoft.Extensions.FileSystemGlobbing.Matcher"/> for full
/// glob semantics (<c>**</c>, <c>*</c>, brace expansion). Phase 5 replacement
/// for the naive segment-matcher used in Phases 3–4.
/// </summary>
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
