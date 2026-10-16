using Spectra.Core.Diagnostics;

namespace Spectra.Core.Tests.Diagnostics;

/// <summary>Spec 099 R8: the scoped counter behind the performance guards.</summary>
public class ParseCounterTests
{
    [Fact]
    public void WithoutAScope_CountingIsANoOp()
    {
        ParseCounter.FullParse();
        ParseCounter.LayoutCapture();
        ParseCounter.DocumentHeadingScan();
        ParseCounter.JsonParse();

        using var scope = ParseCounter.Begin();
        Assert.Equal(0, scope.FullParses);
    }

    [Fact]
    public void AScope_CountsEachKind()
    {
        using var scope = ParseCounter.Begin();
        ParseCounter.FullParse();
        ParseCounter.FullParse();
        ParseCounter.LayoutCapture();
        ParseCounter.DocumentHeadingScan();
        ParseCounter.JsonParse();
        ParseCounter.JsonParse();
        ParseCounter.JsonParse();

        Assert.Equal(2, scope.FullParses);
        Assert.Equal(1, scope.LayoutCaptures);
        Assert.Equal(1, scope.DocumentHeadingScans);
        Assert.Equal(3, scope.JsonParses);
    }

    [Fact]
    public void NestedScopes_CountInTheInnermost_AndDisposeRestoresTheParent()
    {
        using var outer = ParseCounter.Begin();
        ParseCounter.FullParse();
        using (var inner = ParseCounter.Begin())
        {
            ParseCounter.FullParse();
            ParseCounter.FullParse();
            Assert.Equal(2, inner.FullParses);
        }

        ParseCounter.FullParse();
        Assert.Equal(2, outer.FullParses);
    }

    [Fact]
    public async Task ParallelFlows_SeeOnlyTheirOwnCounts()
    {
        async Task<int> CountAsync(int n)
        {
            using var scope = ParseCounter.Begin();
            for (var i = 0; i < n; i++)
            {
                await Task.Yield();
                ParseCounter.FullParse();
            }

            return scope.FullParses;
        }

        var results = await Task.WhenAll(Enumerable.Range(1, 8).Select(n => Task.Run(() => CountAsync(n * 10))));

        Assert.Equal(Enumerable.Range(1, 8).Select(n => n * 10), results);
    }

    [Fact]
    public async Task ChildTasks_CountIntoTheCallersScope()
    {
        using var scope = ParseCounter.Begin();

        await Parallel.ForEachAsync(Enumerable.Range(0, 200), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (_, _) =>
        {
            await Task.Yield();
            ParseCounter.FullParse();
        });
        await Task.Run(ParseCounter.JsonParse);

        Assert.Equal(200, scope.FullParses);
        Assert.Equal(1, scope.JsonParses);
    }
}
