using Spectra.Core.Baseline;

namespace Spectra.Core.Tests.Baseline;

/// <summary>Spec 084 (FR-013, research R3): tokenising and the bounded, deterministic word diff.</summary>
public class WordDiffTests
{
    [Theory]
    [InlineData("10gb.", "10gb")]
    [InlineData("(must)", "must")]
    [InlineData("$5", "5")]
    [InlineData("30%", "30")]
    [InlineData("snake_case", "snake_case")]
    [InlineData("1,000", "1,000")]
    public void Tokenize_TrimsPunctuationAtTheEdges(string word, string token) =>
        Assert.Equal([token], WordDiff.Tokenize(word));

    [Fact]
    public void Tokenize_DropsPunctuationOnlyTokens() =>
        Assert.Equal(["a", "b"], WordDiff.Tokenize("a — b ..."));

    [Fact]
    public void Replacement_GivesOneRemovedAndOneAddedRun()
    {
        var diff = WordDiff.Compute("each user gets 10gb of space", "each user gets 50gb of space");

        Assert.True(diff.Computed);
        Assert.Equal([new DiffRun(false, ["10gb"]), new DiffRun(true, ["50gb"])], diff.Runs, new RunComparer());
        Assert.Equal(1, diff.Removed);
        Assert.Equal(1, diff.Added);
    }

    [Fact]
    public void InsertionAndDeletion_AreCountedSeparately()
    {
        var diff = WordDiff.Compute("a b c d", "a x c d e");

        Assert.Equal(["b"], diff.RemovedWords);
        Assert.Equal(["x", "e"], diff.AddedWords);
    }

    [Fact]
    public void Identical_GivesNoRuns()
    {
        var diff = WordDiff.Compute("same text here", "same text here");

        Assert.True(diff.Computed);
        Assert.Empty(diff.Runs);
    }

    [Fact]
    public void TieBreak_IsDeleteBeforeInsert_AndDeterministic()
    {
        var first = WordDiff.Compute("a b", "b a");
        var second = WordDiff.Compute("a b", "b a");

        Assert.Equal(first.Runs, second.Runs, new RunComparer());
        Assert.False(first.Runs[0].Added);
    }

    [Fact]
    public void OverTheCap_IsNotComputed()
    {
        var big = string.Join(' ', Enumerable.Range(0, 5001).Select(i => $"w{i}"));

        var diff = WordDiff.Compute("short", big);

        Assert.False(diff.Computed);
        Assert.Equal("over 5000 words", diff.Reason);
    }

    [Fact]
    public void AtTheCap_IsComputed()
    {
        var big = string.Join(' ', Enumerable.Range(0, 5000).Select(i => $"w{i}"));

        Assert.True(WordDiff.Compute("short", big).Computed);
    }

    [Fact]
    public void OverTheCellBudget_TheWholeMiddleIsRemovedAndAdded()
    {
        var a = string.Join(' ', Enumerable.Range(0, 2100).Select(i => $"a{i}"));
        var b = string.Join(' ', Enumerable.Range(0, 2100).Select(i => $"b{i}"));

        var diff = WordDiff.Compute("start " + a + " end", "start " + b + " end");

        Assert.True(diff.Computed);
        Assert.Equal(2100, diff.Removed);
        Assert.Equal(2100, diff.Added);
        Assert.Equal(2, diff.Runs.Count);
    }

    private sealed class RunComparer : IEqualityComparer<DiffRun>
    {
        public bool Equals(DiffRun? x, DiffRun? y) => x!.Added == y!.Added && x.Words.SequenceEqual(y.Words);

        public int GetHashCode(DiffRun obj) => obj.Added.GetHashCode();
    }
}
