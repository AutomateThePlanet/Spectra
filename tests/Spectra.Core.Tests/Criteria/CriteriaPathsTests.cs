using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Criteria;

/// <summary>Spec 079 FR-015/FR-017: source_doc normalisation and per-doc criteria paths.</summary>
public sealed class CriteriaPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spectra-079-paths-" + Guid.NewGuid().ToString("N"));

    public CriteriaPathsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("docs/x.md")]
    [InlineData("docs\\x.md")]
    [InlineData("./docs/x.md")]
    [InlineData("/docs/x.md")]
    [InlineData("docs//x.md")]
    [InlineData("  docs/x.md  ")]
    [InlineData("docs/sub/../x.md")]
    public void NormalizeSourceDoc_AllSpellings_GiveTheSamePath(string input)
    {
        var result = CriteriaPaths.NormalizeSourceDoc(input, _root);

        Assert.True(result.IsSuccess);
        Assert.Equal("docs/x.md", result.Path);
    }

    [Fact]
    public void NormalizeSourceDoc_AbsolutePathInsideRepo_IsRelative()
    {
        var result = CriteriaPaths.NormalizeSourceDoc(Path.Combine(_root, "docs", "x.md"), _root);

        Assert.Equal("docs/x.md", result.Path);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void NormalizeSourceDoc_OutsideOrEmpty_IsRefused(string input)
    {
        var result = CriteriaPaths.NormalizeSourceDoc(input, _root);

        Assert.False(result.IsSuccess);
        Assert.Equal(CriteriaPaths.ErrorInvalidDocPath, result.ErrorCode);
    }

    [Theory]
    [InlineData("docs/a/login.md", "docs/a/login.criteria.yaml")]
    [InlineData("docs/b/login.md", "docs/b/login.criteria.yaml")]
    [InlineData("docs/login.MD", "docs/login.criteria.yaml")]
    [InlineData("specs/x.txt", "specs/x.txt.criteria.yaml")]
    [InlineData("docs\\a\\login.md", "docs/a/login.criteria.yaml")]
    public void DerivedFile_MirrorsTheRepoRelativePath(string doc, string expected)
    {
        Assert.Equal(expected, CriteriaPaths.DerivedFile(doc));
    }

    [Fact]
    public void DerivedFile_DistinctDocs_GiveDistinctFiles()
    {
        var docs = new[] { "docs/a/login.md", "docs/b/login.md", "docs/login.md", "login.md", "docs/a/login.txt" };

        Assert.Equal(docs.Length, docs.Select(CriteriaPaths.DerivedFile).Distinct().Count());
    }

    [Fact]
    public void IsOldLayout_DetectsBasenameFilesAndUnnormalisedDocs()
    {
        var derived = new CriteriaIndex
        {
            Sources = [new CriteriaSource { File = "docs/a/login.criteria.yaml", SourceDoc = "docs/a/login.md" }]
        };
        var basename = new CriteriaIndex
        {
            Sources = [new CriteriaSource { File = "login.criteria.yaml", SourceDoc = "docs/a/login.md" }]
        };
        var backslash = new CriteriaIndex
        {
            Sources = [new CriteriaSource { File = "docs/a/login.criteria.yaml", SourceDoc = "docs\\a\\login.md" }]
        };
        var importOnly = new CriteriaIndex
        {
            Sources = [new CriteriaSource { File = "imported/jira.criteria.yaml", SourceDoc = "jira.csv", SourceType = "import" }]
        };

        Assert.False(CriteriaPaths.IsOldLayout(derived));
        Assert.True(CriteriaPaths.IsOldLayout(basename));
        Assert.True(CriteriaPaths.IsOldLayout(backslash));
        Assert.False(CriteriaPaths.IsOldLayout(importOnly));
        Assert.False(CriteriaPaths.IsOldLayout(new CriteriaIndex()));
    }
}
