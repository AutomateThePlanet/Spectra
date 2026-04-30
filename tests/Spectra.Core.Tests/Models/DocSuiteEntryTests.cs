using Spectra.Core.Models.Index;

namespace Spectra.Core.Tests.Models;

public class DocSuiteEntryTests
{
    [Theory]
    [InlineData("checkout")]
    [InlineData("SM_GSG_Topics")]
    [InlineData("RD_Topics.Old")]
    [InlineData("a-b-c")]
    [InlineData("1234")]
    public void IdRegex_AcceptsValidIds(string id)
    {
        Assert.Matches(DocSuiteEntry.IdRegex, id);
    }

    [Theory]
    [InlineData("foo/bar")]      // slashes
    [InlineData("foo bar")]      // space
    [InlineData(".hidden")]      // leading dot
    [InlineData("-leading")]     // leading dash
    [InlineData("")]             // empty
    [InlineData("foo\\bar")]     // backslashes
    public void IdRegex_RejectsInvalidIds(string id)
    {
        Assert.DoesNotMatch(DocSuiteEntry.IdRegex, id);
    }

    [Fact]
    public void NewlyConstructed_HasDefaultExcludedByNone()
    {
        var entry = new DocSuiteEntry();

        Assert.Equal("none", entry.ExcludedBy);
        Assert.False(entry.SkipAnalysis);
        Assert.Null(entry.ExcludedPattern);
        Assert.Null(entry.SpilloverFiles);
    }
}
