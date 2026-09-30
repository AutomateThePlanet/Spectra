using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Criteria;

/// <summary>Spec 080 B1–B3: the shared-file provenance rule.</summary>
public class CriteriaProvenanceTests
{
    private static CriteriaSource Source(string file, string doc) =>
        new() { File = file, SourceDoc = doc, SourceType = "document" };

    [Fact]
    public void IsShared_WhenAnotherSourceUsesTheSameFile()
    {
        var a = Source("login.criteria.yaml", "docs/a/login.md");
        var b = Source("./login.criteria.yaml", "docs/b/login.md");
        var c = Source("docs/c.criteria.yaml", "docs/c.md");
        var index = new CriteriaIndex { Sources = [a, b, c] };

        Assert.True(CriteriaProvenance.IsShared(index, a));
        Assert.True(CriteriaProvenance.IsShared(index, b));
        Assert.False(CriteriaProvenance.IsShared(index, c));
    }

    [Fact]
    public void OwnedBy_MatchesSourceDocLexically_CaseInsensitive()
    {
        AcceptanceCriterion[] criteria =
        [
            new() { Id = "AC-1", Text = "one", SourceDoc = "docs\\a\\login.md" },
            new() { Id = "AC-2", Text = "two", SourceDoc = "./Docs/A/Login.md" },
            new() { Id = "AC-3", Text = "three", SourceDoc = "docs/b/login.md" },
            new() { Id = "AC-4", Text = "four", SourceDoc = null },
            new() { Id = "AC-5", Text = "five", SourceDoc = "" }
        ];

        var owned = CriteriaProvenance.OwnedBy(criteria, "docs/a/login.md");

        Assert.Equal(["AC-1", "AC-2"], owned.Select(c => c.Id));
    }

    [Fact]
    public void OwnedBy_CriteriaWithoutSourceDoc_BelongToNoDocument()
    {
        AcceptanceCriterion[] criteria = [new() { Id = "AC-9", Text = "legacy" }];

        Assert.Empty(CriteriaProvenance.OwnedBy(criteria, "docs/a/login.md"));
        Assert.Empty(CriteriaProvenance.OwnedBy(criteria, ""));
    }
}
