using Spectra.CLI.Source;

namespace Spectra.CLI.Tests.Source;

public class ExclusionPatternMatcherTests
{
    [Fact]
    public void IsExcluded_DoubleStarSegment_MatchesAtAnyDepth()
    {
        var matcher = new ExclusionPatternMatcher(new[] { "**/Old/**" });

        Assert.True(matcher.IsExcluded("docs/RD_Topics/Old/3-9-7.md", out var p));
        Assert.Equal("**/Old/**", p);
        Assert.True(matcher.IsExcluded("Old/anything.md", out _));
        Assert.False(matcher.IsExcluded("docs/RD_Topics/3-51-12.md", out _));
    }

    [Fact]
    public void IsExcluded_FilenameGlob_MatchesPrefix()
    {
        var matcher = new ExclusionPatternMatcher(new[] { "**/CHANGELOG*" });

        Assert.True(matcher.IsExcluded("docs/CHANGELOG.md", out var p));
        Assert.Equal("**/CHANGELOG*", p);
        Assert.True(matcher.IsExcluded("docs/CHANGELOG-2024.md", out _));
        Assert.False(matcher.IsExcluded("docs/changelog.md", out _));  // case-sensitive
    }

    [Fact]
    public void IsExcluded_ExactFilename_MatchesOnly()
    {
        var matcher = new ExclusionPatternMatcher(new[] { "**/SUMMARY.md" });

        Assert.True(matcher.IsExcluded("docs/SUMMARY.md", out _));
        Assert.True(matcher.IsExcluded("docs/inner/SUMMARY.md", out _));
        Assert.False(matcher.IsExcluded("docs/summary.md", out _));
        Assert.False(matcher.IsExcluded("docs/MY_SUMMARY.md", out _));
    }

    [Fact]
    public void IsExcluded_MultiplePatterns_OrSemantics()
    {
        var matcher = new ExclusionPatternMatcher(new[]
        {
            "**/Old/**",
            "**/legacy/**",
            "**/CHANGELOG*",
        });

        Assert.True(matcher.IsExcluded("docs/Old/x.md", out _));
        Assert.True(matcher.IsExcluded("docs/legacy/x.md", out _));
        Assert.True(matcher.IsExcluded("docs/CHANGELOG.md", out _));
        Assert.False(matcher.IsExcluded("docs/active/x.md", out _));
    }

    [Fact]
    public void IsExcluded_EmptyList_ExcludesNothing()
    {
        var matcher = new ExclusionPatternMatcher(Array.Empty<string>());

        Assert.True(matcher.IsEmpty);
        Assert.False(matcher.IsExcluded("docs/anything.md", out _));
    }

    [Fact]
    public void IsExcluded_BackslashesNormalizedToForwardSlashes()
    {
        var matcher = new ExclusionPatternMatcher(new[] { "**/Old/**" });

        // Windows-style backslashes should still match.
        Assert.True(matcher.IsExcluded(@"docs\Old\x.md", out _));
    }

    [Fact]
    public void IsExcluded_FirstMatchWins_ReturnsThatPattern()
    {
        var matcher = new ExclusionPatternMatcher(new[]
        {
            "**/Old/**",
            "**/legacy/**",
        });

        // Only the first matched pattern is returned.
        Assert.True(matcher.IsExcluded("docs/Old/x.md", out var pattern));
        Assert.Equal("**/Old/**", pattern);
    }

    [Fact]
    public void IsExcluded_WhitespacePatternsIgnored()
    {
        var matcher = new ExclusionPatternMatcher(new[] { "", "  ", "**/Old/**" });

        Assert.False(matcher.IsEmpty);
        Assert.True(matcher.IsExcluded("docs/Old/x.md", out _));
    }
}
