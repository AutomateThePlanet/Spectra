using Spectra.Core.Coverage;

namespace Spectra.Core.Tests.Coverage;

/// <summary>
/// Spec 078: rules and examples in specs/078-lossless-test-writes/contracts/source-ref-resolution.md.
/// </summary>
public sealed class SourceRefResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spectra-resolver-" + Guid.NewGuid().ToString("N"));
    private readonly SourceRefResolver _resolver;

    public SourceRefResolverTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs", "sub"));
        File.WriteAllText(Path.Combine(_root, "docs", "a.md"), "a");
        File.WriteAllText(Path.Combine(_root, "docs", "sub", "b.md"), "b");
        _resolver = new SourceRefResolver(_root, Path.Combine(_root, "docs"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("docs/a.md#Section-X", "docs/a.md")]
    [InlineData("docs\\a.md", "docs/a.md")]
    [InlineData("/docs/a.md", "docs/a.md")]
    [InlineData("a.md", "docs/a.md")]
    [InlineData("a.md#Intro", "docs/a.md")]
    [InlineData("  docs/a.md  ", "docs/a.md")]
    [InlineData("sub/b.md", "docs/sub/b.md")]
    [InlineData("docs/sub/b.md#Deep", "docs/sub/b.md")]
    public void Resolves(string sourceRef, string expectedRelative)
    {
        var result = _resolver.Resolve([sourceRef]);

        var resolved = Assert.Single(result.Resolved);
        Assert.Equal(sourceRef, resolved.Ref);
        Assert.Equal(expectedRelative, resolved.RelativePath);
        Assert.True(File.Exists(resolved.FullPath));
        Assert.Empty(result.Missing);
        Assert.True(result.AnyResolved);
    }

    [Fact]
    public void FullyQualifiedPathUnderRoot_Resolves()
    {
        var full = Path.Combine(_root, "docs", "a.md");

        var result = _resolver.Resolve([full, full.Replace('\\', '/') + "#S"]);

        Assert.All(result.Resolved, r => Assert.Equal("docs/a.md", r.RelativePath));
        Assert.Equal(2, result.Resolved.Count);
    }

    [Theory]
    [InlineData("", MissingReason.Empty)]
    [InlineData("   ", MissingReason.Empty)]
    [InlineData("#Only-Fragment", MissingReason.Empty)]
    [InlineData("/", MissingReason.Empty)]
    [InlineData("../outside.md", MissingReason.OutsideAllowedRoots)]
    [InlineData("docs/../../outside.md", MissingReason.OutsideAllowedRoots)]
    [InlineData("docs/gone.md", MissingReason.NotFound)]
    [InlineData("gone.md#X", MissingReason.NotFound)]
    public void Missing(string sourceRef, MissingReason reason)
    {
        var result = _resolver.Resolve([sourceRef]);

        var missing = Assert.Single(result.Missing);
        Assert.Equal(sourceRef, missing.Ref);
        Assert.Equal(reason, missing.Reason);
        Assert.False(result.AnyResolved);
    }

    [Fact]
    public void FullyQualifiedPathOutsideRoot_IsOutsideAllowedRoots()
    {
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere-" + Guid.NewGuid().ToString("N") + ".md");
        File.WriteAllText(outside, "x");
        try
        {
            var result = _resolver.Resolve([outside]);

            Assert.False(result.AnyResolved);
            Assert.Single(result.Missing);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void RepoRootWinsOverSourceDir_ForBareRefs()
    {
        File.WriteAllText(Path.Combine(_root, "a.md"), "root copy");

        var resolved = Assert.Single(_resolver.Resolve(["a.md"]).Resolved);

        Assert.Equal("a.md", resolved.RelativePath);
    }

    [Fact]
    public void DistinctFullPaths_DeduplicatesFragmentsAndSeparators()
    {
        var result = _resolver.Resolve(["docs/a.md#X", "docs\\a.md", "docs/a.md#Y", "a.md"]);

        Assert.Equal(4, result.Resolved.Count);
        Assert.Single(result.DistinctFullPaths);
    }

    [Fact]
    public void OrderIsPreserved_AcrossResolvedAndMissing()
    {
        var result = _resolver.Resolve(["gone-1.md", "docs/a.md", "gone-2.md", "sub/b.md"]);

        Assert.Equal(["docs/a.md", "sub/b.md"], result.Resolved.Select(r => r.Ref));
        Assert.Equal(["gone-1.md", "gone-2.md"], result.Missing.Select(m => m.Ref));
    }

    [Theory]
    [InlineData("docs/a\0.md")]
    [InlineData("docs/<>|?.md")]
    [InlineData("::::")]
    public void NeverThrows(string sourceRef)
    {
        var result = _resolver.Resolve([sourceRef]);

        Assert.False(result.AnyResolved);
        Assert.Single(result.Missing);
    }
}

/// <summary>
/// Spec 080 A2 (FR-002, FR-002a): a source directory outside the repository (<c>../product-docs/docs</c>) is an
/// allowed root. Refs that escape both the repository and the source directory are still refused, with a reason
/// that says so.
/// </summary>
public sealed class SourceRefResolverOutsideSourceDirTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "spectra-resolver-ext-" + Guid.NewGuid().ToString("N"));
    private readonly string _repo;
    private readonly string _docs;
    private readonly SourceRefResolver _resolver;

    public SourceRefResolverOutsideSourceDirTests()
    {
        _repo = Path.Combine(_base, "repo");
        _docs = Path.Combine(_base, "product-docs", "docs");
        Directory.CreateDirectory(Path.Combine(_repo, "docs"));
        Directory.CreateDirectory(_docs);
        Directory.CreateDirectory(Path.Combine(_base, "elsewhere"));
        File.WriteAllText(Path.Combine(_docs, "checkout.md"), "checkout");
        File.WriteAllText(Path.Combine(_base, "elsewhere", "x.md"), "x");
        File.WriteAllText(Path.Combine(_repo, "docs", "local.md"), "local");
        _resolver = new SourceRefResolver(_repo, "../product-docs/docs");
    }

    public void Dispose() => Directory.Delete(_base, recursive: true);

    [Theory]
    [InlineData("checkout.md#Payment")]
    [InlineData("checkout.md")]
    [InlineData("../product-docs/docs/checkout.md")]
    [InlineData("../product-docs/docs/checkout.md#Payment")]
    public void RefsIntoTheSourceDir_Resolve(string sourceRef)
    {
        var resolved = Assert.Single(_resolver.Resolve([sourceRef]).Resolved);

        Assert.Equal(Path.Combine(_docs, "checkout.md"), resolved.FullPath);
        Assert.Equal("../product-docs/docs/checkout.md", resolved.RelativePath);
    }

    [Fact]
    public void AbsoluteRefIntoTheSourceDir_Resolves()
    {
        var full = Path.Combine(_docs, "checkout.md");

        var result = _resolver.Resolve([full, full.Replace('\\', '/') + "#Payment"]);

        Assert.Equal(2, result.Resolved.Count);
        Assert.Empty(result.Missing);
    }

    [Theory]
    [InlineData("../elsewhere/x.md")]
    [InlineData("../../x.md")]
    public void RefsEscapingBothRoots_AreRefused(string sourceRef)
    {
        var missing = Assert.Single(_resolver.Resolve([sourceRef]).Missing);

        Assert.Equal(MissingReason.OutsideAllowedRoots, missing.Reason);
    }

    [Fact]
    public void AbsoluteRefEscapingBothRoots_IsRefused()
    {
        var missing = Assert.Single(_resolver.Resolve([Path.Combine(_base, "elsewhere", "x.md")]).Missing);

        Assert.Equal(MissingReason.OutsideAllowedRoots, missing.Reason);
    }

    [Fact]
    public void MissingFileInsideTheSourceDir_IsNotFound()
    {
        var missing = Assert.Single(_resolver.Resolve(["gone.md#X"]).Missing);

        Assert.Equal(MissingReason.NotFound, missing.Reason);
    }

    [Fact]
    public void RepoFiles_StillResolve_AndWinOverTheSourceDir()
    {
        File.WriteAllText(Path.Combine(_repo, "checkout.md"), "repo copy");

        Assert.Equal("docs/local.md", Assert.Single(_resolver.Resolve(["docs/local.md"]).Resolved).RelativePath);
        Assert.Equal("checkout.md", Assert.Single(_resolver.Resolve(["checkout.md"]).Resolved).RelativePath);
    }
}
