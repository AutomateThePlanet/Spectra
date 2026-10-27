using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Criteria;

/// <summary>
/// Spec 080 B1–B3 / Spec 081 C1: the shared-file provenance rule. Ownership and document lookup compare repo-aware
/// normalised paths, so a <c>source_doc</c> stored absolute, with <c>\</c> or with <c>./</c> matches its document.
/// </summary>
public sealed class CriteriaProvenanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spectra-provenance-" + Guid.NewGuid().ToString("N"));

    public CriteriaProvenanceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

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
    public void OwnedBy_MatchesRelativeBackslashAndDotSlash_CaseInsensitive()
    {
        AcceptanceCriterion[] criteria =
        [
            new() { Id = "AC-1", Text = "one", SourceDoc = "docs\\a\\login.md" },
            new() { Id = "AC-2", Text = "two", SourceDoc = "./Docs/A/Login.md" },
            new() { Id = "AC-3", Text = "three", SourceDoc = "docs/b/login.md" },
            new() { Id = "AC-4", Text = "four", SourceDoc = null },
            new() { Id = "AC-5", Text = "five", SourceDoc = "" }
        ];

        var owned = CriteriaProvenance.OwnedBy(criteria, "docs/a/login.md", _root);

        Assert.Equal(["AC-1", "AC-2"], owned.Select(c => c.Id));
    }

    [Fact]
    public void OwnedBy_AbsolutePathInsideTheRepository_IsOwned()
    {
        AcceptanceCriterion[] criteria =
        [
            new() { Id = "AC-1", Text = "one", SourceDoc = Path.Combine(_root, "docs", "a", "login.md") },
            new() { Id = "AC-2", Text = "two", SourceDoc = Path.Combine(_root, "docs", "a", "login.md").Replace('\\', '/') }
        ];

        var owned = CriteriaProvenance.OwnedBy(criteria, "docs/a/login.md", _root);

        Assert.Equal(["AC-1", "AC-2"], owned.Select(c => c.Id));
    }

    [Fact]
    public void OwnedBy_AbsolutePathOutsideTheRepository_BelongsToNoDocument()
    {
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere-" + Guid.NewGuid().ToString("N"), "docs", "a", "login.md");
        AcceptanceCriterion[] criteria = [new() { Id = "AC-1", Text = "one", SourceDoc = outside }];

        Assert.Empty(CriteriaProvenance.OwnedBy(criteria, "docs/a/login.md", _root));
    }

    [Fact]
    public void OwnedBy_CriteriaWithoutSourceDoc_BelongToNoDocument()
    {
        AcceptanceCriterion[] criteria = [new() { Id = "AC-9", Text = "legacy" }];

        Assert.Empty(CriteriaProvenance.OwnedBy(criteria, "docs/a/login.md", _root));
        Assert.Empty(CriteriaProvenance.OwnedBy(criteria, "", _root));
    }

    [Fact]
    public void SameDocument_ComparesRepoAwareNormalisedPaths()
    {
        var absolute = Path.Combine(_root, "docs", "a", "login.md");

        Assert.True(CriteriaProvenance.SameDocument(absolute, "docs/a/login.md", _root));
        Assert.True(CriteriaProvenance.SameDocument("./docs\\a\\login.md", "Docs/A/Login.md", _root));
        Assert.False(CriteriaProvenance.SameDocument("docs/a/login.md", "docs/b/login.md", _root));
        Assert.False(CriteriaProvenance.SameDocument(
            Path.Combine(Path.GetTempPath(), "elsewhere", "docs", "a", "login.md"), "docs/a/login.md", _root));
        Assert.False(CriteriaProvenance.SameDocument(null, "docs/a/login.md", _root));
    }
}
