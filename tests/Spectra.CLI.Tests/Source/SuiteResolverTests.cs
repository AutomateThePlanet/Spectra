using Spectra.CLI.Source;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Source;

public class SuiteResolverTests
{
    [Fact]
    public void Resolve_DirectoryDefault_PicksFirstSegmentBeneathLocalDir()
    {
        var resolver = new SuiteResolver();
        var docs = new[]
        {
            new DiscoveredDoc("docs/SM_GSG_Topics/manage-items/standard-items.md"),
            new DiscoveredDoc("docs/SM_GSG_Topics/manage-items/serialized-items.md"),
        };
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(docs, config);

        Assert.Equal("SM_GSG_Topics", result.Assignments["docs/SM_GSG_Topics/manage-items/standard-items.md"]);
        Assert.Equal("SM_GSG_Topics", result.Assignments["docs/SM_GSG_Topics/manage-items/serialized-items.md"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Resolve_DocumentDirectlyInLocalDir_AssignsRoot()
    {
        var resolver = new SuiteResolver();
        var docs = new[] { new DiscoveredDoc("docs/SUMMARY.md") };
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(docs, config);

        Assert.Equal("_root", result.Assignments["docs/SUMMARY.md"]);
    }

    [Fact]
    public void Resolve_FrontmatterOverride_TakesPrecedenceOverDirectory()
    {
        var resolver = new SuiteResolver();
        var docs = new[]
        {
            new DiscoveredDoc("docs/RD_Topics/x.md", FrontmatterSuite: "custom-suite"),
        };
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(docs, config);

        Assert.Equal("custom-suite", result.Assignments["docs/RD_Topics/x.md"]);
    }

    [Fact]
    public void Resolve_FrontmatterWithSlash_RejectedAndFallsBackToDirectory()
    {
        var resolver = new SuiteResolver();
        var docs = new[]
        {
            new DiscoveredDoc("docs/RD_Topics/x.md", FrontmatterSuite: "bad/suite"),
        };
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(docs, config);

        Assert.Single(result.Errors);
        Assert.Contains("bad/suite", result.Errors[0]);
        Assert.Equal("RD_Topics", result.Assignments["docs/RD_Topics/x.md"]);
    }

    [Theory]
    [InlineData(".hidden")]
    [InlineData("with space")]
    [InlineData("-leading-dash")]
    public void Resolve_FrontmatterWithInvalidId_Rejected(string badId)
    {
        var resolver = new SuiteResolver();
        var docs = new[] { new DiscoveredDoc("docs/x/y.md", FrontmatterSuite: badId) };
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(docs, config);

        Assert.Single(result.Errors);
        Assert.Contains(badId, result.Errors[0]);
    }

    [Fact]
    public void Resolve_ConfigOverride_UsedWhenFrontmatterAbsent()
    {
        var resolver = new SuiteResolver();
        var docs = new[] { new DiscoveredDoc("docs/RD_Topics/x.md") };
        var config = new SourceConfig
        {
            LocalDir = "docs/",
            GroupOverrides = new Dictionary<string, string>
            {
                ["docs/RD_Topics/x.md"] = "configured",
            },
        };

        var result = resolver.Resolve(docs, config);

        Assert.Equal("configured", result.Assignments["docs/RD_Topics/x.md"]);
    }

    [Fact]
    public void Resolve_FrontmatterWinsOverConfigOverride()
    {
        var resolver = new SuiteResolver();
        var docs = new[]
        {
            new DiscoveredDoc("docs/x/y.md", FrontmatterSuite: "from-frontmatter"),
        };
        var config = new SourceConfig
        {
            LocalDir = "docs/",
            GroupOverrides = new Dictionary<string, string>
            {
                ["docs/x/y.md"] = "from-config",
            },
        };

        var result = resolver.Resolve(docs, config);

        Assert.Equal("from-frontmatter", result.Assignments["docs/x/y.md"]);
    }

    [Fact]
    public void Resolve_PreservesCaseInSuiteId()
    {
        var resolver = new SuiteResolver();
        var docs = new[]
        {
            new DiscoveredDoc("docs/SM_GSG_Topics/x.md"),
            new DiscoveredDoc("docs/cm_ug_topics/y.md"),
        };
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(docs, config);

        Assert.Equal("SM_GSG_Topics", result.Assignments["docs/SM_GSG_Topics/x.md"]);
        Assert.Equal("cm_ug_topics", result.Assignments["docs/cm_ug_topics/y.md"]);
    }

    [Fact]
    public void Resolve_EmptyInput_ReturnsEmptyResult()
    {
        var resolver = new SuiteResolver();
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(Array.Empty<DiscoveredDoc>(), config);

        Assert.Empty(result.Assignments);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Resolve_DocOutsideLocalDir_FallsBackToFirstSegment()
    {
        var resolver = new SuiteResolver();
        var docs = new[]
        {
            new DiscoveredDoc("other-root/Topic/x.md"),
        };
        var config = new SourceConfig { LocalDir = "docs/" };

        var result = resolver.Resolve(docs, config);

        // StripPrefix returns the path unchanged when prefix doesn't match,
        // so the first segment is "other-root".
        Assert.Equal("other-root", result.Assignments["other-root/Topic/x.md"]);
    }
}
